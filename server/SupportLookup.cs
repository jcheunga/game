using System.Data.Common;

namespace CrownroadServer;

/// <summary>
/// Read-only player lookup for support: what a player bought, what their server wallet holds and why
/// it changed. Find the player by profile ID, or by a store order or transaction ID when that is all
/// the player has. It never returns session tokens, store receipts or sign-in identifiers (only the
/// names of the linked providers), so an operator can share the answer in a ticket.
/// </summary>
public static class SupportLookup
{
    private const int LedgerLimit = 100;

    public static IResult AdminPlayer(HttpRequest request)
    {
        if (AdminAuth.Require(request) is { } adminError) return adminError;

        var profileId = request.Query["profileId"].FirstOrDefault()?.Trim() ?? "";
        var transactionId = request.Query["transactionId"].FirstOrDefault()?.Trim() ?? "";
        if (profileId.Length == 0 && transactionId.Length == 0)
            return Results.BadRequest(new { error = "profile_or_transaction_required" });

        using var conn = Database.Open();
        if (profileId.Length == 0)
        {
            using var find = Command(conn, "SELECT profile_id FROM purchases WHERE transaction_id = @tx OR purchase_id = @tx LIMIT 1",
                ("@tx", transactionId));
            profileId = find.ExecuteScalar() as string ?? "";
            if (profileId.Length == 0) return Results.NotFound(new { status = "not_found" });
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        object? player = null;
        using (var cmd = Command(conn, "SELECT callsign, created_at, synced_at FROM players WHERE profile_id = @pid", ("@pid", profileId)))
        using (var reader = cmd.ExecuteReader())
        {
            if (reader.Read())
            {
                player = new
                {
                    callsign = reader.GetString(0),
                    createdAtUnixSeconds = Database.ToInt64(reader.GetValue(1)),
                    syncedAtUnixSeconds = Database.ToInt64(reader.GetValue(2))
                };
            }
        }

        var providers = new List<string>();
        using (var cmd = Command(conn, "SELECT DISTINCT provider FROM account_identities WHERE profile_id = @pid ORDER BY provider", ("@pid", profileId)))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read()) providers.Add(reader.GetString(0));
        }

        long activeSessions = 0, lastSeenAt = 0;
        using (var cmd = Command(conn, """
            SELECT COUNT(CASE WHEN revoked_at = 0 AND expires_at > @now THEN 1 END), COALESCE(MAX(last_seen_at), 0)
            FROM auth_sessions WHERE profile_id = @pid
        """, ("@pid", profileId), ("@now", now)))
        using (var reader = cmd.ExecuteReader())
        {
            if (reader.Read())
            {
                activeSessions = Database.ToInt64(reader.GetValue(0));
                lastSeenAt = Database.ToInt64(reader.GetValue(1));
            }
        }

        object? wallet = null;
        using (var cmd = Command(conn, "SELECT gold_balance, food_balance, unit_unlock_balance, updated_at FROM player_wallets WHERE profile_id = @pid",
            ("@pid", profileId)))
        using (var reader = cmd.ExecuteReader())
        {
            if (reader.Read())
            {
                wallet = new
                {
                    gold = Database.ToInt64(reader.GetValue(0)),
                    food = Database.ToInt64(reader.GetValue(1)),
                    unitUnlocks = Database.ToInt64(reader.GetValue(2)),
                    updatedAtUnixSeconds = Database.ToInt64(reader.GetValue(3))
                };
            }
        }

        var purchases = new List<object>();
        using (var cmd = Command(conn, """
            SELECT purchase_id, product_id, platform, transaction_id, gold_credited, food_credited, granted_unit_unlock,
                status, price_cents, purchased_at, refunded_at
            FROM purchases WHERE profile_id = @pid ORDER BY purchased_at DESC, id DESC
        """, ("@pid", profileId)))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                purchases.Add(new
                {
                    purchaseId = reader.GetString(0),
                    productId = reader.GetString(1),
                    platform = reader.GetString(2),
                    transactionId = reader.GetString(3),
                    goldCredited = Database.ToInt64(reader.GetValue(4)),
                    foodCredited = Database.ToInt64(reader.GetValue(5)),
                    grantedUnitUnlock = Database.ToInt64(reader.GetValue(6)) != 0,
                    status = reader.GetString(7),
                    priceCents = Database.ToInt64(reader.GetValue(8)),
                    purchasedAtUnixSeconds = Database.ToInt64(reader.GetValue(9)),
                    refundedAtUnixSeconds = Database.ToInt64(reader.GetValue(10))
                });
            }
        }

        var ledger = new List<object>();
        using (var cmd = Command(conn, $"""
            SELECT entry_id, currency_type, amount, reason, purchase_id, adjustment_id, created_at
            FROM wallet_ledger WHERE profile_id = @pid ORDER BY created_at DESC, entry_id DESC LIMIT {LedgerLimit}
        """, ("@pid", profileId)))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                ledger.Add(new
                {
                    entryId = reader.GetString(0),
                    currency = reader.GetString(1),
                    amount = Database.ToInt64(reader.GetValue(2)),
                    reason = reader.GetString(3),
                    purchaseId = reader.GetString(4),
                    adjustmentId = reader.GetString(5),
                    createdAtUnixSeconds = Database.ToInt64(reader.GetValue(6))
                });
            }
        }

        var adjustments = new List<object>();
        using (var cmd = Command(conn, """
            SELECT adjustment_id, purchase_id, product_id, kind, gold_delta, food_delta, unit_unlock_kept, reason, event_id,
                created_at, acknowledged_at, gold_applied, food_applied
            FROM wallet_adjustments WHERE profile_id = @pid ORDER BY created_at DESC, id DESC
        """, ("@pid", profileId)))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                adjustments.Add(new
                {
                    adjustmentId = reader.GetString(0),
                    purchaseId = reader.GetString(1),
                    productId = reader.GetString(2),
                    kind = reader.GetString(3),
                    goldDelta = Database.ToInt64(reader.GetValue(4)),
                    foodDelta = Database.ToInt64(reader.GetValue(5)),
                    unitUnlockKept = Database.ToInt64(reader.GetValue(6)) != 0,
                    reason = reader.GetString(7),
                    eventId = reader.GetString(8),
                    createdAtUnixSeconds = Database.ToInt64(reader.GetValue(9)),
                    // Zero until the player's device has applied it.
                    appliedOnDeviceAtUnixSeconds = Database.ToInt64(reader.GetValue(10)),
                    goldApplied = Database.ToInt64(reader.GetValue(11)),
                    foodApplied = Database.ToInt64(reader.GetValue(12))
                });
            }
        }

        object? cloudSave = null;
        using (var cmd = Command(conn, "SELECT save_version, uploaded_at, LENGTH(save_data) FROM cloud_saves WHERE profile_id = @pid",
            ("@pid", profileId)))
        using (var reader = cmd.ExecuteReader())
        {
            if (reader.Read())
            {
                cloudSave = new
                {
                    saveVersion = Database.ToInt64(reader.GetValue(0)),
                    uploadedAtUnixSeconds = Database.ToInt64(reader.GetValue(1)),
                    sizeBytes = Database.ToInt64(reader.GetValue(2))
                };
            }
        }

        return Results.Ok(new
        {
            status = "ok",
            profileId,
            // A deleted account's purchases live on under a DELETED- pseudonym with no player behind it.
            player,
            signInProviders = providers,
            activeSessions,
            lastSeenAtUnixSeconds = lastSeenAt,
            wallet,
            purchases,
            ledger,
            adjustments,
            cloudSave
        });
    }

    private static DbCommand Command(DbConnection conn, string sql, params (string Name, object Value)[] parameters)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
        return cmd;
    }
}
