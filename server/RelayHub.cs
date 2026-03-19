using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace CrownroadServer;

/// <summary>
/// WebSocket relay hub for real-time online room state sync.
/// Each room has a set of connected peers. Messages from any peer
/// are broadcast to all other peers in the same room.
/// </summary>
public static class RelayHub
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, WebSocket>> Rooms = new();
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PeerTimeout = TimeSpan.FromSeconds(60);

    // Track last activity per peer for timeout eviction
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, long>> PeerLastActive = new();

    public static async Task HandleConnection(HttpContext context, string roomId)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        var profileId = context.Request.Headers["X-Convoy-Profile"].FirstOrDefault()
                        ?? context.Request.Query["profileId"].FirstOrDefault()
                        ?? $"anon-{Guid.NewGuid().ToString("N")[..6]}";

        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        var roomPeers = Rooms.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, WebSocket>());
        var roomActivity = PeerLastActive.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, long>());
        roomPeers[profileId] = ws;
        roomActivity[profileId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Notify room about new peer
        await BroadcastSystemMessage(roomPeers, profileId, new
        {
            type = "peer_joined",
            roomId,
            profileId,
            peerCount = roomPeers.Count,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });

        try
        {
            var buffer = new byte[4096];
            while (ws.State == WebSocketState.Open)
            {
                using var cts = new CancellationTokenSource(PeerTimeout);
                WebSocketReceiveResult result;
                try
                {
                    result = await ws.ReceiveAsync(buffer, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Peer timed out — no messages received within PeerTimeout
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    roomActivity[profileId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await BroadcastToOthers(roomPeers, profileId, message);
                }
            }
        }
        catch (WebSocketException)
        {
            // Peer disconnected
        }
        finally
        {
            roomPeers.TryRemove(profileId, out _);
            roomActivity.TryRemove(profileId, out _);

            if (roomPeers.IsEmpty)
            {
                Rooms.TryRemove(roomId, out _);
                PeerLastActive.TryRemove(roomId, out _);
            }
            else
            {
                await BroadcastSystemMessage(roomPeers, profileId, new
                {
                    type = "peer_left",
                    roomId,
                    profileId,
                    peerCount = roomPeers.Count,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });
            }

            if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
            {
                try
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", CancellationToken.None);
                }
                catch
                {
                    // Already closed
                }
            }
        }
    }

    private static async Task BroadcastToOthers(ConcurrentDictionary<string, WebSocket> peers, string senderId, string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        var segment = new ArraySegment<byte>(bytes);

        var tasks = new List<Task>();
        foreach (var (peerId, peer) in peers)
        {
            if (peerId == senderId || peer.State != WebSocketState.Open)
            {
                continue;
            }

            tasks.Add(SendSafe(peer, segment));
        }

        if (tasks.Count > 0)
            await Task.WhenAll(tasks);
    }

    private static async Task BroadcastSystemMessage(ConcurrentDictionary<string, WebSocket> peers, string excludeId, object message)
    {
        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var bytes = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(bytes);

        var tasks = new List<Task>();
        foreach (var (peerId, peer) in peers)
        {
            if (peer.State != WebSocketState.Open)
            {
                continue;
            }

            tasks.Add(SendSafe(peer, segment));
        }

        if (tasks.Count > 0)
            await Task.WhenAll(tasks);
    }

    private static async Task SendSafe(WebSocket ws, ArraySegment<byte> data)
    {
        try
        {
            if (ws.State == WebSocketState.Open)
            {
                using var cts = new CancellationTokenSource(SendTimeout);
                await ws.SendAsync(data, WebSocketMessageType.Text, true, cts.Token);
            }
        }
        catch
        {
            // Peer gone or send timed out, ignore
        }
    }

    public static int GetRoomCount() => Rooms.Count;

    public static int GetPeerCount(string roomId) =>
        Rooms.TryGetValue(roomId, out var peers) ? peers.Count : 0;
}
