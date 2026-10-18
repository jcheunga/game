using System.Data.Common;
using System.Text.Json;

namespace CrownroadServer;

/// <summary>
/// Takes back what a refunded store purchase granted, and gives it back if the
/// store reverses the refund. Every store signal (an App Store notification, a
/// Google Play voided purchase, an operator's correction) is recorded once in
/// store_events; a repeated signal changes nothing.
///
/// A refund marks its purchase 'refunded', writes reversing wallet_ledger
/// entries and leaves a wallet_adjustments row. The server never edits the
/// player's save: the device fetches its adjustments (GET /wallet/adjustments),
/// applies each one once by ID, and reports what it could take back
/// (POST /wallet/adjustments/ack). Gold or food already spent is not taken
/// below zero; the device records the shortfall and the acknowledgement stores
/// it here. The Adventurer's Kit's unit unlock is kept (see docs/DEPLOYMENT.md,
/// "Refunds").
/// </summary>
public static class PurchaseRefunds
{
    public enum Action { Refund, Reinstate, Ignore }

    /// <summary>One store event about a transaction.</summary>
    /// <param name="Source">apple, google, admin or store_verification.</param>
    /// <param name="EventKey">Unique per event: the App Store notification UUID, the Google order and void time.</param>
    /// <param name="Platform">The purchase's platform to match, or empty for any.</param>
    /// <param name="TransactionId">The store transaction (Apple) or order (Google) ID the purchase was recorded under.</param>
    /// <param name="ReceiptFingerprint">A second way to find a Google purchase: the hash of its purchase token.</param>
    /// <param name="PurchaseId">The server purchase ID, when an operator names one.</param>
    /// <param name="StoreProductId">The store's product ID, checked against the purchase's product when given.</param>
    public sealed record StoreSignal(
        string Source,
        string EventKey,
        string EventType,
        string Platform = "",
        string TransactionId = "",
        string ReceiptFingerprint = "",
        string PurchaseId = "",
        string StoreProductId = "",
        string Environment = "",
        string Detail = "");

    /// <summary>
    /// Status is refunded, reinstated, duplicate_event, already_refunded,
    /// not_refunded, not_refundable, product_mismatch, unknown_transaction or ignored.
    /// </summary>
    public sealed record Outcome(string Status, string EventId, string PurchaseId, string AdjustmentId, string ProfileId);

    private sealed record PurchaseRow(string PurchaseId, string ProfileId, string ProductId, string Platform, int Gold, int Food, bool UnitUnlock);

    public static Outcome Record(StoreSignal signal, Action action)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();

        // The event row comes first: its unique key makes a repeated signal (an
        // App Store retry, the next Google poll) wait for the first and stop here.
        var eventId = NewId("EVT");
        using (var insert = Command(conn, tx, """
            INSERT INTO store_events (event_id, source, event_key, event_type, environment, transaction_id, outcome, detail, received_at)
            VALUES (@id, @source, @key, @type, @environment, @tid, 'received', @detail, @now)
            ON CONFLICT (event_key) DO NOTHING
        """))
        {
            insert.Parameters.AddWithValue("@id", eventId);
            insert.Parameters.AddWithValue("@source", signal.Source);
            insert.Parameters.AddWithValue("@key", signal.EventKey);
            insert.Parameters.AddWithValue("@type", signal.EventType);
            insert.Parameters.AddWithValue("@environment", signal.Environment);
            insert.Parameters.AddWithValue("@tid", signal.TransactionId);
            insert.Parameters.AddWithValue("@detail", Clip(signal.Detail, 500));
            insert.Parameters.AddWithValue("@now", now);
            if (insert.ExecuteNonQuery() == 0)
            {
                tx.Rollback();
                return Duplicate(conn, signal.EventKey);
            }
        }

        string Finish(string status, PurchaseRow? purchase = null)
        {
            using var update = Command(conn, tx, "UPDATE store_events SET outcome = @outcome, purchase_id = @pid WHERE event_id = @id");
            update.Parameters.AddWithValue("@outcome", status);
            update.Parameters.AddWithValue("@pid", purchase?.PurchaseId ?? "");
            update.Parameters.AddWithValue("@id", eventId);
            update.ExecuteNonQuery();
            tx.Commit();
            return status;
        }

        if (action == Action.Ignore)
            return new Outcome(Finish("ignored"), eventId, "", "", "");

        var purchase = FindPurchase(conn, tx, signal);
        if (purchase == null)
            return new Outcome(Finish("unknown_transaction"), eventId, "", "", "");
        if (signal.StoreProductId.Length > 0 &&
            !string.Equals(StorePurchaseVerification.StoreProductId(purchase.Platform, purchase.ProductId), signal.StoreProductId, StringComparison.Ordinal))
            return new Outcome(Finish("product_mismatch", purchase), eventId, purchase.PurchaseId, "", purchase.ProfileId);

        // The status guard makes concurrent signals for one purchase take turns:
        // the second waits on the row, then finds the status already changed.
        var (from, to) = action == Action.Refund ? ("validated", "refunded") : ("refunded", "validated");
        using (var transition = Command(conn, tx, """
            UPDATE purchases SET status = @to, refunded_at = @refundedAt
            WHERE purchase_id = @pid AND status = @from
        """))
        {
            transition.Parameters.AddWithValue("@to", to);
            transition.Parameters.AddWithValue("@refundedAt", action == Action.Refund ? now : 0);
            transition.Parameters.AddWithValue("@pid", purchase.PurchaseId);
            transition.Parameters.AddWithValue("@from", from);
            if (transition.ExecuteNonQuery() == 0)
            {
                // Read the status again: a concurrent signal may have changed it
                // after this transaction first read the purchase.
                using var current = Command(conn, tx, "SELECT status FROM purchases WHERE purchase_id = @pid");
                current.Parameters.AddWithValue("@pid", purchase.PurchaseId);
                var status = action == Action.Refund
                    ? current.ExecuteScalar() as string == "refunded" ? "already_refunded" : "not_refundable"
                    : "not_refunded";
                return new Outcome(Finish(status, purchase), eventId, purchase.PurchaseId, "", purchase.ProfileId);
            }
        }

        // A deleted account's purchase is kept under a pseudonym with no player, wallet or device behind
        // it (AccountDeletion), so the refund is recorded on the purchase and there is nothing to take back.
        if (AccountDeletion.IsDeletedProfile(purchase.ProfileId))
            return new Outcome(Finish(action == Action.Refund ? "refunded" : "reinstated", purchase), eventId, purchase.PurchaseId, "", purchase.ProfileId);

        var sign = action == Action.Refund ? -1 : 1;
        var kind = action == Action.Refund ? "refund" : "refund_reversed";
        var reverses = action == Action.Reinstate ? LatestRefundAdjustment(conn, tx, purchase.PurchaseId) : "";
        var adjustmentId = NewId("ADJ");
        using (var adjustment = Command(conn, tx, """
            INSERT INTO wallet_adjustments (adjustment_id, profile_id, purchase_id, product_id, kind, gold_delta, food_delta,
                unit_unlock_kept, reverses_adjustment_id, event_id, reason, created_at)
            VALUES (@id, @profile, @pid, @product, @kind, @gold, @food, @unitKept, @reverses, @event, @reason, @now)
        """))
        {
            adjustment.Parameters.AddWithValue("@id", adjustmentId);
            adjustment.Parameters.AddWithValue("@profile", purchase.ProfileId);
            adjustment.Parameters.AddWithValue("@pid", purchase.PurchaseId);
            adjustment.Parameters.AddWithValue("@product", purchase.ProductId);
            adjustment.Parameters.AddWithValue("@kind", kind);
            adjustment.Parameters.AddWithValue("@gold", sign * purchase.Gold);
            adjustment.Parameters.AddWithValue("@food", sign * purchase.Food);
            // The kit's unit stays unlocked: it may be leveled, equipped and in
            // the loadout, and the kit can never be bought again.
            adjustment.Parameters.AddWithValue("@unitKept", purchase.UnitUnlock ? 1 : 0);
            adjustment.Parameters.AddWithValue("@reverses", reverses);
            adjustment.Parameters.AddWithValue("@event", eventId);
            adjustment.Parameters.AddWithValue("@reason", $"{signal.Source}:{signal.EventType}");
            adjustment.Parameters.AddWithValue("@now", now);
            adjustment.ExecuteNonQuery();
        }

        foreach (var (currency, amount) in new[] { ("gold", purchase.Gold), ("food", purchase.Food) })
        {
            if (amount <= 0) continue;
            using var ledger = Command(conn, tx, """
                INSERT INTO wallet_ledger (entry_id, profile_id, currency_type, amount, reason, purchase_id, adjustment_id, created_at)
                VALUES (@entry, @profile, @currency, @amount, @reason, @pid, @adjustment, @now)
            """);
            ledger.Parameters.AddWithValue("@entry", NewId("WLT"));
            ledger.Parameters.AddWithValue("@profile", purchase.ProfileId);
            ledger.Parameters.AddWithValue("@currency", currency);
            ledger.Parameters.AddWithValue("@amount", sign * amount);
            ledger.Parameters.AddWithValue("@reason", kind);
            ledger.Parameters.AddWithValue("@pid", purchase.PurchaseId);
            ledger.Parameters.AddWithValue("@adjustment", adjustmentId);
            ledger.Parameters.AddWithValue("@now", now);
            ledger.ExecuteNonQuery();
        }

        // The server wallet counts paid currency only, so it always holds at
        // least what the purchase credited and never goes below zero here.
        using (var wallet = Command(conn, tx, """
            UPDATE player_wallets SET gold_balance = gold_balance + @gold, food_balance = food_balance + @food, updated_at = @now
            WHERE profile_id = @profile
        """))
        {
            wallet.Parameters.AddWithValue("@gold", sign * purchase.Gold);
            wallet.Parameters.AddWithValue("@food", sign * purchase.Food);
            wallet.Parameters.AddWithValue("@now", now);
            wallet.Parameters.AddWithValue("@profile", purchase.ProfileId);
            wallet.ExecuteNonQuery();
        }

        return new Outcome(Finish(action == Action.Refund ? "refunded" : "reinstated", purchase), eventId, purchase.PurchaseId, adjustmentId, purchase.ProfileId);
    }

    private static Outcome Duplicate(DbConnection conn, string eventKey)
    {
        using var cmd = Command(conn, null, "SELECT event_id, purchase_id FROM store_events WHERE event_key = @key");
        cmd.Parameters.AddWithValue("@key", eventKey);
        using var reader = cmd.ExecuteReader();
        return reader.Read()
            ? new Outcome("duplicate_event", reader.GetString(0), reader.GetString(1), "", "")
            : new Outcome("duplicate_event", "", "", "", "");
    }

    private static PurchaseRow? FindPurchase(DbConnection conn, DbTransaction tx, StoreSignal signal)
    {
        foreach (var (column, value) in new[]
                 {
                     ("purchase_id", signal.PurchaseId),
                     ("transaction_id", signal.TransactionId),
                     ("receipt_token", signal.ReceiptFingerprint)
                 })
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            using var cmd = Command(conn, tx, $"""
                SELECT purchase_id, profile_id, product_id, platform, gold_credited, food_credited, granted_unit_unlock
                FROM purchases WHERE {column} = @value AND (@platform = '' OR platform = @platform)
                ORDER BY id LIMIT 1
            """);
            cmd.Parameters.AddWithValue("@value", value.Trim());
            cmd.Parameters.AddWithValue("@platform", signal.Platform);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new PurchaseRow(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    Database.ToInt32(reader.GetValue(4)), Database.ToInt32(reader.GetValue(5)), Database.ToInt32(reader.GetValue(6)) != 0);
            }
        }
        return null;
    }

    private static string LatestRefundAdjustment(DbConnection conn, DbTransaction tx, string purchaseId)
    {
        using var cmd = Command(conn, tx, "SELECT adjustment_id FROM wallet_adjustments WHERE purchase_id = @pid AND kind = 'refund' ORDER BY id DESC LIMIT 1");
        cmd.Parameters.AddWithValue("@pid", purchaseId);
        return cmd.ExecuteScalar() as string ?? "";
    }

    // ── Device endpoints ────────────────────────────────────

    /// <summary>
    /// The player's adjustments, oldest first. All of them, not only those not
    /// yet acknowledged: a save restored from the cloud may predate one, and
    /// the device applies each ID once per save.
    /// </summary>
    public static IResult List(HttpRequest request)
    {
        var profileId = request.Query["profileId"].FirstOrDefault()
            ?? request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? "";
        if (string.IsNullOrWhiteSpace(profileId))
            return Results.BadRequest(new { error = "missing profileId" });
        if (!SessionAuth.TryAuthorize(request, profileId, out _))
            return SessionAuth.Unauthorized();

        using var conn = Database.Open();
        using var cmd = Command(conn, null, """
            SELECT adjustment_id, purchase_id, product_id, kind, gold_delta, food_delta, unit_unlock_kept,
                reverses_adjustment_id, reason, created_at, acknowledged_at
            FROM wallet_adjustments WHERE profile_id = @pid ORDER BY id DESC LIMIT 200
        """);
        cmd.Parameters.AddWithValue("@pid", profileId);
        var adjustments = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            adjustments.Add(new
            {
                adjustmentId = reader.GetString(0),
                purchaseId = reader.GetString(1),
                productId = reader.GetString(2),
                kind = reader.GetString(3),
                goldDelta = Database.ToInt32(reader.GetValue(4)),
                foodDelta = Database.ToInt32(reader.GetValue(5)),
                unitUnlockKept = Database.ToInt32(reader.GetValue(6)) != 0,
                reversesAdjustmentId = reader.GetString(7),
                reason = reader.GetString(8),
                createdAtUnixSeconds = Database.ToInt64(reader.GetValue(9)),
                acknowledged = Database.ToInt64(reader.GetValue(10)) > 0
            });
        }
        adjustments.Reverse();
        return Results.Ok(new { status = "ok", adjustments });
    }

    /// <summary>
    /// The device reports what it applied: for a refund, the gold and food it
    /// could take back (negative, never more than the adjustment). The first
    /// acknowledgement is kept; the difference is the unrecovered shortfall.
    /// </summary>
    public static async Task<IResult> Acknowledge(HttpRequest request)
    {
        JsonDocument document;
        try { document = await JsonDocument.ParseAsync(request.Body); }
        catch (JsonException) { return Results.BadRequest(new { error = "invalid json" }); }
        using var parsed = document;
        var body = document.RootElement;
        var profileId = Text(body, "profileId");
        if (profileId.Length == 0) profileId = request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? "";
        var adjustmentId = Text(body, "adjustmentId");
        if (string.IsNullOrWhiteSpace(profileId) || adjustmentId.Length == 0)
            return Results.BadRequest(new { error = "missing profileId or adjustmentId" });
        if (!SessionAuth.TryAuthorize(request, profileId, out _))
            return SessionAuth.Unauthorized();

        using var conn = Database.Open();
        (int GoldDelta, int FoodDelta, long AcknowledgedAt, int GoldApplied, int FoodApplied)? Stored()
        {
            using var read = Command(conn, null, """
                SELECT gold_delta, food_delta, acknowledged_at, gold_applied, food_applied
                FROM wallet_adjustments WHERE adjustment_id = @id AND profile_id = @pid
            """);
            read.Parameters.AddWithValue("@id", adjustmentId);
            read.Parameters.AddWithValue("@pid", profileId);
            using var reader = read.ExecuteReader();
            if (!reader.Read()) return null;
            return (Database.ToInt32(reader.GetValue(0)), Database.ToInt32(reader.GetValue(1)), Database.ToInt64(reader.GetValue(2)),
                Database.ToInt32(reader.GetValue(3)), Database.ToInt32(reader.GetValue(4)));
        }

        if (Stored() is not { } row) return Results.NotFound(new { error = "unknown adjustment" });
        var (goldDelta, foodDelta, acknowledgedAt, goldApplied, foodApplied) = row;
        var status = "already_acknowledged";
        if (acknowledgedAt == 0)
        {
            // A device can only apply part of an adjustment, in its direction.
            var gold = Math.Clamp(Int(body, "goldApplied"), Math.Min(0, goldDelta), Math.Max(0, goldDelta));
            var food = Math.Clamp(Int(body, "foodApplied"), Math.Min(0, foodDelta), Math.Max(0, foodDelta));
            using var update = Command(conn, null, """
                UPDATE wallet_adjustments SET acknowledged_at = @now, gold_applied = @gold, food_applied = @food
                WHERE adjustment_id = @id AND profile_id = @pid AND acknowledged_at = 0
            """);
            update.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            update.Parameters.AddWithValue("@gold", gold);
            update.Parameters.AddWithValue("@food", food);
            update.Parameters.AddWithValue("@id", adjustmentId);
            update.Parameters.AddWithValue("@pid", profileId);
            if (update.ExecuteNonQuery() > 0) (status, goldApplied, foodApplied) = ("ok", gold, food);
            else if (Stored() is { } winner) (goldApplied, foodApplied) = (winner.GoldApplied, winner.FoodApplied); // another device acknowledged first
        }

        return Results.Ok(new
        {
            status,
            adjustmentId,
            goldApplied,
            foodApplied,
            goldUnrecovered = Math.Abs(goldDelta - goldApplied),
            foodUnrecovered = Math.Abs(foodDelta - foodApplied)
        });
    }

    // ── Operator endpoints ──────────────────────────────────

    /// <summary>
    /// Records a refund or a reinstatement the stores do not report, such as a
    /// Stripe refund or a support correction: { purchaseId | transactionId,
    /// action: "refund" | "reinstate", note }.
    /// </summary>
    public static async Task<IResult> AdminRecord(HttpRequest request)
    {
        if (AdminAuth.Require(request) is { } adminError) return adminError;
        JsonDocument document;
        try { document = await JsonDocument.ParseAsync(request.Body); }
        catch (JsonException) { return Results.BadRequest(new { error = "invalid json" }); }
        using var parsed = document;
        var body = document.RootElement;
        var purchaseId = Text(body, "purchaseId");
        var transactionId = Text(body, "transactionId");
        if (purchaseId.Length == 0 && transactionId.Length == 0)
            return Results.BadRequest(new { error = "missing purchaseId or transactionId" });
        Action? action = Text(body, "action") switch
        {
            "" or "refund" => Action.Refund,
            "reinstate" => Action.Reinstate,
            _ => null
        };
        if (action == null) return Results.BadRequest(new { error = "action must be refund or reinstate" });

        var outcome = Record(new StoreSignal(
            "admin", $"admin:{Guid.NewGuid():N}", action == Action.Refund ? "REFUND" : "REFUND_REVERSED",
            TransactionId: transactionId, PurchaseId: purchaseId, Detail: Text(body, "note")), action.Value);
        return outcome.Status is "unknown_transaction"
            ? Results.NotFound(new { status = outcome.Status })
            : Results.Ok(new { status = outcome.Status, outcome.EventId, outcome.PurchaseId, outcome.AdjustmentId });
    }

    /// <summary>The latest store events and the adjustments they made, for support.</summary>
    public static IResult AdminList(HttpRequest request)
    {
        if (AdminAuth.Require(request) is { } adminError) return adminError;
        using var conn = Database.Open();
        using var cmd = Command(conn, null, """
            SELECT e.event_id, e.source, e.event_type, e.environment, e.transaction_id, e.purchase_id, e.outcome, e.detail, e.received_at,
                COALESCE(a.adjustment_id, ''), COALESCE(a.profile_id, ''), COALESCE(a.gold_delta, 0), COALESCE(a.food_delta, 0),
                COALESCE(a.acknowledged_at, 0), COALESCE(a.gold_applied, 0), COALESCE(a.food_applied, 0)
            FROM store_events e LEFT JOIN wallet_adjustments a ON a.event_id = e.event_id
            ORDER BY e.received_at DESC, e.event_id DESC LIMIT 100
        """);
        var events = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var goldDelta = Database.ToInt32(reader.GetValue(11));
            var foodDelta = Database.ToInt32(reader.GetValue(12));
            var acknowledged = Database.ToInt64(reader.GetValue(13)) > 0;
            events.Add(new
            {
                eventId = reader.GetString(0),
                source = reader.GetString(1),
                eventType = reader.GetString(2),
                environment = reader.GetString(3),
                transactionId = reader.GetString(4),
                purchaseId = reader.GetString(5),
                outcome = reader.GetString(6),
                detail = reader.GetString(7),
                receivedAtUnixSeconds = Database.ToInt64(reader.GetValue(8)),
                adjustmentId = reader.GetString(9),
                profileId = reader.GetString(10),
                goldDelta,
                foodDelta,
                acknowledged,
                goldUnrecovered = acknowledged ? Math.Abs(goldDelta - Database.ToInt32(reader.GetValue(14))) : 0,
                foodUnrecovered = acknowledged ? Math.Abs(foodDelta - Database.ToInt32(reader.GetValue(15))) : 0
            });
        }
        return Results.Ok(new { status = "ok", events });
    }

    // ── Helpers ─────────────────────────────────────────────

    private static DbCommand Command(DbConnection conn, DbTransaction? tx, string sql)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        return cmd;
    }

    private static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
    private static string Clip(string value, int length) => value.Length > length ? value[..length] : value;

    private static string Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? ""
            : "";

    private static int Int(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;
}
