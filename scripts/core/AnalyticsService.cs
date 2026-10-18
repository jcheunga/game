using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Godot;

public static class AnalyticsService
{
	private static readonly System.Net.Http.HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };

	private static readonly List<AnalyticsEvent> Queue = new();
	private const int FlushThreshold = 10;
	private const int MaxQueueSize = 200;

	public static void Track(string eventType, string data = "")
	{
		if (string.IsNullOrWhiteSpace(eventType)) return;
		if (GameState.Instance?.AnalyticsConsent != true) return;

		lock (Queue)
		{
			if (Queue.Count >= MaxQueueSize)
			{
				Queue.RemoveAt(0);
			}

			Queue.Add(new AnalyticsEvent { Type = eventType, Data = data });

			if (Queue.Count >= FlushThreshold)
			{
				TryFlush();
			}
		}
	}

	public static void TrackStageStart(int stage, string difficulty, string[] deckUnitIds)
	{
		Track("stage_start", $"stage={stage},diff={difficulty},deck={string.Join("+", deckUnitIds)}");
	}

	public static void TrackStageEnd(int stage, bool won, int stars, float elapsed, float hullRatio)
	{
		Track("stage_end", $"stage={stage},won={won},stars={stars},time={elapsed:F1},hull={hullRatio:F2}");
	}

	public static void TrackEndlessEnd(string route, int wave, float elapsed)
	{
		Track("endless_end", $"route={route},wave={wave},time={elapsed:F1}");
	}

	public static void TrackUnitPurchase(string unitId, int cost)
	{
		Track("unit_purchase", $"unit={unitId},cost={cost}");
	}

	public static void TrackSpellPurchase(string spellId, int cost)
	{
		Track("spell_purchase", $"spell={spellId},cost={cost}");
	}

	public static void TrackIAPPurchase(string productId, string platform)
	{
		Track("iap_purchase", $"product={productId},platform={platform}");
	}

	public static void TrackDailyChallenge(int score, bool completed)
	{
		Track("daily_challenge", $"score={score},completed={completed}");
	}

	public static void TrackSessionStart()
	{
		var stage = GameState.Instance?.HighestUnlockedStage ?? 1;
		var gold = GameState.Instance?.Gold ?? 0;
		Track("session_start", $"stage={stage},gold={gold}");
	}

	public static void TryFlush()
	{
		if (GameState.Instance?.AnalyticsConsent != true)
		{
			ClearPending();
			return;
		}

		List<AnalyticsEvent> batch;
		lock (Queue)
		{
			if (Queue.Count == 0) return;
			batch = new List<AnalyticsEvent>(Queue);
			Queue.Clear();
		}

		var endpoint = GameState.Instance?.PurchaseValidationEndpoint ?? "";
		if (string.IsNullOrWhiteSpace(endpoint)) return;

		var profileId = GameState.Instance?.PlayerProfileId ?? "";
		var platform = OS.HasFeature("ios") ? "ios" : OS.HasFeature("android") ? "android" : "desktop";

		try
		{
			var requestBody = new AnalyticsBatch
			{
				ProfileId = profileId,
				ClientVersion = 31,
				Platform = platform,
				Events = batch
			};
			var json = JsonSerializer.Serialize(requestBody, ServerJson.Default.AnalyticsBatch);

			using var msg = new HttpRequestMessage(HttpMethod.Post, $"{endpoint.TrimEnd('/')}/analytics/ingest");
			PlayerSessionHttp.Apply(msg, profileId);
			msg.Content = new StringContent(json, Encoding.UTF8, "application/json");
			using var response = Client.Send(msg);
		}
		catch
		{
			// Silent — analytics should never block gameplay
		}
	}

	public static void ClearPending()
	{
		lock (Queue) Queue.Clear();
	}

	internal sealed class AnalyticsBatch
	{
		public string ProfileId { get; set; } = "";
		public int ClientVersion { get; set; }
		public string Platform { get; set; } = "";
		public List<AnalyticsEvent> Events { get; set; } = new();
	}

	internal sealed class AnalyticsEvent
	{
		public string Type { get; set; } = "";
		public string Data { get; set; } = "";
	}
}
