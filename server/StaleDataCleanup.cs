using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CrownroadServer;

public class StaleDataCleanup : BackgroundService
{
    private readonly ILogger<StaleDataCleanup> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private const long StaleRoomAgeSeconds = 3600 * 6;
    private const long StaleTelemetryAgeSeconds = 3600 * 24;
    private const long StaleReportRetentionSeconds = 3600 * 24 * 90;
    private const long StaleAnalyticsRetentionSeconds = 3600 * 24 * 30;
    private static readonly TimeSpan BackupInterval = TimeSpan.FromHours(6);
    private DateTime _lastBackupTime = DateTime.MinValue;

    public StaleDataCleanup(ILogger<StaleDataCleanup> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StaleDataCleanup: started, interval={Interval}", Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken);

            try
            {
                var cleaned = RunCleanup();
                if (cleaned > 0)
                {
                    _logger.LogInformation("StaleDataCleanup: removed {Count} stale rows", cleaned);
                }

                if (DateTime.UtcNow - _lastBackupTime > BackupInterval)
                {
                    var backupPath = Database.Backup();
                    Database.CleanupOldBackups();
                    _lastBackupTime = DateTime.UtcNow;
                    if (!string.IsNullOrWhiteSpace(backupPath))
                    {
                        _logger.LogInformation("StaleDataCleanup: database backed up to {Path}", backupPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StaleDataCleanup: error during cleanup");
            }
        }
    }

    private static int RunCleanup()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var totalRemoved = 0;

        using var conn = Database.Open();

        // Expire stale rooms that have been in lobby/racing for too long
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE rooms SET status = 'expired'
                WHERE status IN ('lobby', 'racing')
                AND updated_at > 0
                AND updated_at < $cutoff
            """;
            cmd.Parameters.AddWithValue("$cutoff", now - StaleRoomAgeSeconds);
            totalRemoved += cmd.ExecuteNonQuery();
        }

        // Remove seats for expired rooms
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE room_seats SET status = 'expired'
                WHERE room_id IN (SELECT room_id FROM rooms WHERE status = 'expired')
                AND status NOT IN ('left', 'expired')
            """;
            totalRemoved += cmd.ExecuteNonQuery();
        }

        // Purge old telemetry data
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM room_telemetry WHERE reported_at < $cutoff";
            cmd.Parameters.AddWithValue("$cutoff", now - StaleTelemetryAgeSeconds);
            totalRemoved += cmd.ExecuteNonQuery();
        }

        // Purge very old reports (keep 90 days)
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM room_reports WHERE reported_at < $cutoff";
            cmd.Parameters.AddWithValue("$cutoff", now - StaleReportRetentionSeconds);
            totalRemoved += cmd.ExecuteNonQuery();
        }

        // Purge old analytics events (keep 30 days)
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM analytics_events WHERE recorded_at < $cutoff";
            cmd.Parameters.AddWithValue("$cutoff", now - StaleAnalyticsRetentionSeconds);
            totalRemoved += cmd.ExecuteNonQuery();
        }

        return totalRemoved;
    }
}
