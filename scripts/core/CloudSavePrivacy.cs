using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

/// <summary>Shared client/server boundary for data that belongs only on this device.</summary>
public static class CloudSavePrivacy
{
    private static readonly HashSet<string> DeviceOnlyFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "PlayerAuthToken", "SessionToken", "AuthToken", "AccountProvider", "AccountLabel",
        "LastPlayerProfileSyncAtUnixSeconds", "ChallengeSyncProviderId",
        "ChallengeSyncEndpoint", "ChallengeSyncAutoFlush", "PurchaseValidationEndpoint",
        "AnalyticsConsent", "HasShownConsentPrompt", "AnalyticsConsentVersion", "CrashReportingConsent"
    };

    public static string Sanitize(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("A cloud save must be a JSON object.");

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(document.RootElement, writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Write(JsonElement element, Utf8JsonWriter writer)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject())
            {
                if (DeviceOnlyFields.Contains(property.Name)) continue;
                writer.WritePropertyName(property.Name);
                Write(property.Value, writer);
            }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray()) Write(item, writer);
            writer.WriteEndArray();
        }
        else element.WriteTo(writer);
    }
}
