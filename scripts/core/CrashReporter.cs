using System;
using System.Text;
using System.Text.Json;
using Godot;

public partial class CrashReporter : Node
{
	public static CrashReporter Instance { get; private set; }

	private static readonly System.Net.Http.HttpClient Client = new()
	{
		Timeout = TimeSpan.FromSeconds(10)
	};

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = false,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;
	}

	public override void _Ready()
	{
		AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
	}

	private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
	{
		if (args.ExceptionObject is Exception ex)
		{
			ReportError(ex.GetType().Name, ex.Message, ex.StackTrace ?? "");
		}
	}

	public static void ReportError(string errorType, string errorMessage, string stackTrace)
	{
		var endpoint = GameState.Instance?.PurchaseValidationEndpoint ?? "";
		if (string.IsNullOrWhiteSpace(endpoint)) return;

		var profileId = GameState.Instance?.PlayerProfileId ?? "";
		var platform = OS.HasFeature("ios") ? "ios" : OS.HasFeature("android") ? "android" : "desktop";
		var scene = "";
		try
		{
			scene = SceneRouter.Instance?.GetTree()?.CurrentScene?.SceneFilePath ?? "";
		}
		catch { /* ignore */ }

		try
		{
			var body = new
			{
				profileId,
				errorType,
				errorMessage,
				stackTrace,
				clientVersion = 31,
				platform,
				scene
			};
			var json = JsonSerializer.Serialize(body, JsonOptions);

			using var msg = new System.Net.Http.HttpRequestMessage(
				System.Net.Http.HttpMethod.Post,
				$"{endpoint.TrimEnd('/')}/crash-report");
			msg.Headers.TryAddWithoutValidation("X-Convoy-Profile", profileId);
			msg.Content = new System.Net.Http.StringContent(json, Encoding.UTF8, "application/json");
			Client.Send(msg);
		}
		catch
		{
			// Silent — crash reporting should never cause another crash
		}
	}

	public static void ReportWarning(string context, string message)
	{
		ReportError("warning", $"[{context}] {message}", "");
	}
}
