using System;
using System.Threading.Tasks;
using Godot;

public partial class NetworkStatus : Node
{
	public static NetworkStatus Instance { get; private set; }

	public bool IsOnline { get; private set; } = true;
	public bool IsServerReachable { get; private set; }
	public long LastCheckUnixSeconds { get; private set; }
	public string LastError { get; private set; } = "";

	private static readonly System.Net.Http.HttpClient Client = new()
	{
		Timeout = TimeSpan.FromSeconds(6)
	};

	private const float CheckIntervalSeconds = 45f;
	private float _checkTimer;

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
		CheckNow();
	}

	public override void _Process(double delta)
	{
		_checkTimer += (float)delta;
		if (_checkTimer >= CheckIntervalSeconds)
		{
			_checkTimer = 0f;
			CheckNow();
		}
	}

	public void CheckNow()
	{
		var endpoint = GameState.Instance?.PurchaseValidationEndpoint ?? "";
		if (string.IsNullOrWhiteSpace(endpoint))
		{
			IsOnline = true;
			IsServerReachable = false;
			LastError = "No server configured.";
			LastCheckUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			return;
		}

		try
		{
			using var msg = new System.Net.Http.HttpRequestMessage(
				System.Net.Http.HttpMethod.Get,
				$"{endpoint.TrimEnd('/')}/health");
			using var resp = Client.Send(msg);
			IsOnline = true;
			IsServerReachable = resp.IsSuccessStatusCode;
			LastError = IsServerReachable ? "" : $"HTTP {(int)resp.StatusCode}";
		}
		catch (Exception ex)
		{
			IsOnline = false;
			IsServerReachable = false;
			LastError = ex is System.Net.Http.HttpRequestException ? "Network unreachable" :
				ex is TaskCanceledException ? "Server timeout" : ex.Message;
		}

		LastCheckUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
	}

	public string GetStatusLabel()
	{
		if (string.IsNullOrWhiteSpace(GameState.Instance?.PurchaseValidationEndpoint))
			return "Offline mode";
		if (IsServerReachable)
			return "Online";
		if (IsOnline)
			return "Server unreachable";
		return "No network";
	}

	public Color GetStatusColor()
	{
		if (IsServerReachable) return new Color("3fb950");
		if (IsOnline) return new Color("d29922");
		return new Color("f85149");
	}
}
