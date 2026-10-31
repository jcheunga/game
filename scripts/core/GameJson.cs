using System.Collections.Generic;
using System.Text.Json.Serialization;

// Every type the game serializes, with System.Text.Json source generation. The iOS export is
// NativeAOT, which cannot run reflection-based serialization: there the game could read none of
// its data, saves or server answers. Each context matches the options its callers used before,
// so the files on disk and the requests on the wire are unchanged. Pass a context's type info,
// for example JsonSerializer.Serialize(save, SaveJson.Default.GameSaveData): a type that is not
// listed here then fails to compile instead of failing on a phone. A type that is only reached
// through another one's properties needs no entry of its own.

/// <summary>Read-only files whose property names may come in any case: res://data, and an account's cloud save at sign-in.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GameData.UnitCollection))]
[JsonSerializable(typeof(GameData.SpellCollection))]
[JsonSerializable(typeof(GameData.StageCollection))]
[JsonSerializable(typeof(GameData.CombatCollection))]
[JsonSerializable(typeof(GameData.EquipmentCollection))]
[JsonSerializable(typeof(WaveVersion))]
[JsonSerializable(typeof(List<ShopProduct>))]
[JsonSerializable(typeof(GameSaveData))]
internal sealed partial class DataJson : JsonSerializerContext
{
}

/// <summary>The save file: indented, property names as declared.</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(GameSaveData))]
internal sealed partial class SaveJson : JsonSerializerContext
{
}

/// <summary>One-line records on the device: the account backup taken before a sign-in, and the challenge journal.</summary>
[JsonSerializable(typeof(GameSaveData))]
[JsonSerializable(typeof(ChallengeSyncBatchEnvelope))]
internal sealed partial class CompactJson : JsonSerializerContext
{
}

/// <summary>Requests to the game server and the answers read back: camelCase, read in any case.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AccountSignIn.EmptyRequest))]
[JsonSerializable(typeof(AccountSignIn.GuestProfileRequest))]
[JsonSerializable(typeof(AccountSignIn.EmailStartRequest))]
[JsonSerializable(typeof(AccountSignIn.EmailVerifyRequest))]
[JsonSerializable(typeof(AccountSignIn.PollRequest))]
[JsonSerializable(typeof(AccountSignIn.DevSignInRequest))]
[JsonSerializable(typeof(AccountSignIn.PlayGamesRequest))]
[JsonSerializable(typeof(AccountSignIn.GameCenterRequest))]
[JsonSerializable(typeof(CloudSaveUploadRequest))]
[JsonSerializable(typeof(PurchaseValidationApiRequest))]
#if !GODOT_MOBILE
[JsonSerializable(typeof(StripeCheckoutApiRequest))]
#endif
[JsonSerializable(typeof(PlayerProfileSyncApiRequest))]
[JsonSerializable(typeof(AchievementSyncApiRequest))]
[JsonSerializable(typeof(DailyCompleteApiRequest))]
[JsonSerializable(typeof(AnalyticsService.AnalyticsBatch))]
[JsonSerializable(typeof(CrashReporter.CrashReport))]
[JsonSerializable(typeof(ChallengeSyncApiRequest))]
[JsonSerializable(typeof(ChallengeSyncApiResponse))]
[JsonSerializable(typeof(OnlineRoomCreateApiRequest))]
[JsonSerializable(typeof(OnlineRoomMatchmakeApiRequest))]
[JsonSerializable(typeof(OnlineRoomJoinApiRequest))]
[JsonSerializable(typeof(OnlineRoomJoinApiResponse))]
[JsonSerializable(typeof(OnlineRoomSeatLeaseApiRequest))]
[JsonSerializable(typeof(OnlineRoomSeatLeaseApiResponse))]
[JsonSerializable(typeof(OnlineRoomActionApiRequest))]
[JsonSerializable(typeof(OnlineRoomResultApiRequest))]
[JsonSerializable(typeof(OnlineRoomTelemetryApiRequest))]
[JsonSerializable(typeof(OnlineRoomReportApiRequest))]
[JsonSerializable(typeof(OnlineRoomReportApiResponse))]
internal sealed partial class ServerJson : JsonSerializerContext
{
}
