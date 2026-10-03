using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

public partial class PrivacyReview : Node
{
    private readonly ConcurrentQueue<(string Path, string Body, string Authorization)> _requests = new();
    private int _failures;
    private string _restoreJson = "{}";

    public override void _Ready() => Callable.From(Run).CallDeferred();

    private void Check(bool pass, string label)
    {
        GD.Print($"PRIVACY_CHECK: {(pass ? "PASS" : "FAIL")} {label}");
        if (!pass) _failures++;
    }

    private async Task Serve(HttpListener listener)
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await listener.GetContextAsync(); }
            catch (Exception) when (!listener.IsListening) { return; }
            using var reader = new System.IO.StreamReader(context.Request.InputStream);
            var body = await reader.ReadToEndAsync();
            _requests.Enqueue((context.Request.Url!.AbsolutePath, body, context.Request.Headers["Authorization"] ?? ""));
            var result = context.Request.Url.AbsolutePath == "/cloud-save/download"
                ? JsonSerializer.Serialize(new { status = "ok", saveData = _restoreJson, saveVersion = 42 })
                : "{\"status\":\"ok\",\"saveHash\":\"test-hash\"}";
            var bytes = Encoding.UTF8.GetBytes(result);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
    }

    private async void Run()
    {
        using var listener = new HttpListener();
        Task serving = Task.CompletedTask;
        try
        {
            if (!OS.GetCmdlineUserArgs().Any(arg => arg.StartsWith("--save-suffix=privacy-review-")))
                throw new InvalidOperationException("Requires an isolated privacy-review save.");
            var state = GameState.Instance;
            if (state.IsReleaseBackendConfigured)
                throw new InvalidOperationException("Run this local network test with the release API origin unset.");
            Check(!state.AnalyticsConsent && !state.CrashReportingConsent, "Fresh install defaults both optional uploads off");

            using var portProbe = new TcpListener(IPAddress.Loopback, 0);
            portProbe.Start();
            var port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
            portProbe.Stop();
            var endpoint = $"http://127.0.0.1:{port}";
            listener.Prefixes.Add(endpoint + "/");
            listener.Start();
            serving = Task.Run(() => Serve(listener));

            state.ApplyPlayerProfileSession("CVY-PRIVACY-TEST", "Tester", "current-device-token", 123);
            state.SetPurchaseValidationEndpoint(endpoint);
            state.SetAnalyticsConsent(false);
            state.SetCrashReportingConsent(false);
            AnalyticsService.Track("declined");
            AnalyticsService.TryFlush();
            CrashReporter.ReportWarning("test", "declined");
            Check(_requests.IsEmpty, "Declining both options sends no analytics or crash requests");

            state.SetAnalyticsConsent(true);
            AnalyticsService.Track("withdrawn-event");
            state.SetAnalyticsConsent(false);
            state.SetAnalyticsConsent(true);
            AnalyticsService.TryFlush();
            Check(_requests.IsEmpty, "Revocation discards queued events even after opting in again");
            AnalyticsService.Track("allowed-event");
            AnalyticsService.TryFlush();
            Check(_requests.Count == 1 && _requests.Last().Path == "/analytics/ingest", "Explicit analytics consent sends gameplay events");
            CrashReporter.ReportWarning("test", "still declined");
            Check(_requests.Count == 1, "Analytics consent never opts into crash reports");

            state.SetAnalyticsConsent(false);
            state.SetCrashReportingConsent(true);
            CrashReporter.ReportWarning("test", "allowed diagnostic");
            Check(_requests.Count == 2 && _requests.Last().Path == "/crash-report", "Crash reporting has an independent opt-in");
            state.SetCrashReportingConsent(false);
            CrashReporter.ReportWarning("test", "withdrawn diagnostic");
            Check(_requests.Count == 2, "Withdrawing crash consent stops reports immediately");

            Check(CloudSaveService.Upload(out var uploadMessage), "Cloud upload succeeds: " + uploadMessage);
            var upload = _requests.Last();
            using (var json = JsonDocument.Parse(upload.Body))
            {
                var save = json.RootElement.GetProperty("saveData").GetString()!;
                Check(!save.Contains("current-device-token") && !save.Contains("PlayerAuthToken"), "Upload body excludes the session credential");
                Check(!save.Contains("Consent") && !save.Contains("Endpoint"), "Upload excludes device consent and endpoint settings");
                Check(json.RootElement.GetProperty("saveVersion").GetInt32() == state.BuildSaveData().Version, "Upload reports the actual save format version");
            }
            Check(upload.Authorization == "Bearer current-device-token", "Upload still authenticates with the active session header");
            Check(SaveSystem.Instance.TryLoad(out var local) && local.PlayerAuthToken == "current-device-token", "Sanitization leaves the device's saved credential intact");

            var old = new GameSaveData
            {
                Gold = 777, PlayerProfileId = "CVY-OTHER", PlayerAuthToken = "stale-cloud-token",
                PurchaseValidationEndpoint = "https://untrusted.invalid", AnalyticsConsent = true,
                HasShownConsentPrompt = true, AnalyticsConsentVersion = GameState.CurrentAnalyticsConsentVersion,
                CrashReportingConsent = true
            };
            _restoreJson = JsonSerializer.Serialize(old);
            Check(CloudSaveService.Download(out var restoreMessage), "Legacy cloud restore succeeds: " + restoreMessage);
            Check(state.Gold == 777, "Restore recovers gameplay progress");
            Check(state.PlayerProfileId == "CVY-PRIVACY-TEST" && state.PlayerAuthToken == "current-device-token", "Restore preserves the current profile and session");
            Check(state.PurchaseValidationEndpoint == endpoint, "Restore cannot redirect future authenticated requests");
            Check(!state.AnalyticsConsent && !state.CrashReportingConsent && state.HasShownConsentPrompt, "Restore preserves local privacy choices");
            Check(SaveSystem.Instance.TryLoad(out local) && local.PlayerAuthToken == "current-device-token", "Preserved session is persisted across restart");

            _restoreJson = "{\"Version\":1,\"Gold\":555,\"Food\":12,\"HighestUnlockedStage\":1}";
            Check(CloudSaveService.Download(out _) && state.Gold == 555, "Pre-session save formats still restore gameplay progress");
            Check(state.PlayerProfileId == "CVY-PRIVACY-TEST" && state.PlayerAuthToken == "current-device-token"
                && state.PurchaseValidationEndpoint == endpoint && state.HasShownConsentPrompt,
                "Save-format migration cannot discard current device identity or choices");

            local.AnalyticsConsent = true;
            local.HasShownConsentPrompt = true;
            local.AnalyticsConsentVersion = 0;
            SaveSystem.Instance.Save(local);
            state.ReloadFromDisk();
            Check(!state.AnalyticsConsent && !state.HasShownConsentPrompt, "Old inaccurate notice requires a fresh consent choice");
            state.SetAnalyticsConsent(true);
            state.SetCrashReportingConsent(true);
            state.ReloadFromDisk();
            Check(state.AnalyticsConsent && state.HasShownConsentPrompt && state.CrashReportingConsent, "New choices survive a device save and reload");
            if (OS.GetCmdlineUserArgs().Contains("--capture")) await ReviewUi();
        }
        catch (Exception ex)
        {
            GD.PrintErr(ex);
            _failures++;
        }
        finally
        {
            listener.Stop();
            await serving;
        }
        GD.Print($"PRIVACY_REVIEW_RESULT: {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private async Task ReviewUi()
    {
        GetWindow().Mode = Window.ModeEnum.Windowed;
        GetWindow().Size = new Vector2I(1280, 720);
        var state = GameState.Instance;
        state.SetAnalyticsConsent(false);
        state.SetCrashReportingConsent(false);
        state.SetShowHints(false);
        state.SetPurchaseValidationEndpoint("");
        var freshNotice = state.BuildSaveData();
        freshNotice.HasShownConsentPrompt = false;
        SaveSystem.Instance.Save(freshNotice);
        state.ReloadFromDisk();
        var main = GD.Load<PackedScene>("res://scenes/MainMenu.tscn").Instantiate<Control>();
        AddChild(main);
        await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
        Check(!main.FindChildren("*", "Button", true, false).OfType<Button>().Any(button => button.Text == "Allow Analytics" || button.Text == "No Thanks")
            && !main.GetChildren().OfType<CenterContainer>().Any(), "A fresh startup opens the map without an analytics modal");
        Check(!state.AnalyticsConsent && !state.CrashReportingConsent, "Opening home without a prompt keeps optional uploads disabled");
        await Capture("startup");
        main.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var settings = GD.Load<PackedScene>("res://scenes/SettingsMenu.tscn").Instantiate<Control>();
        AddChild(settings);
        settings.FindChildren("*", "Button", true, false).OfType<Button>()
            .Single(button => button.Text == "Account" && button.GetParent().HasMeta("realm_tabs")).EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
        Check(settings.FindChildren("*", "Button", true, false).OfType<Button>().Any(button => button.IsVisibleInTree() && button.Text == "Enable Analytics"), "Analytics remains an optional choice in Settings");
        var crash = settings.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "Enable Crash Reports");
        Node ancestor = crash.GetParent();
        while (ancestor is not ScrollContainer) ancestor = ancestor.GetParent();
        var scroll = (ScrollContainer)ancestor;
        scroll.EnsureControlVisible(crash);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(scroll.GetGlobalRect().Encloses(crash.GetGlobalRect()), "Separate crash-report choice remains reachable in Settings");
        await Capture("privacy-settings");
        settings.QueueFree();
    }

    private async Task Capture(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var directory = ProjectSettings.GlobalizePath("res://artifacts/privacy-review");
        System.IO.Directory.CreateDirectory(directory);
        GetViewport().GetTexture().GetImage().SavePng($"{directory}/{name}.png");
    }
}
