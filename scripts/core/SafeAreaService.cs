using Godot;

public partial class SafeAreaService : Node
{
	public static SafeAreaService Instance { get; private set; }

	public Rect2I SafeArea { get; private set; }
	public int MarginLeft { get; private set; }
	public int MarginRight { get; private set; }
	public int MarginTop { get; private set; }
	public int MarginBottom { get; private set; }
	public bool HasInsets => MarginLeft > 0 || MarginRight > 0 || MarginTop > 0 || MarginBottom > 0;

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public override void _Ready()
	{
		UpdateSafeArea();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationWMSizeChanged)
		{
			UpdateSafeArea();
		}
	}

	private void UpdateSafeArea()
	{
		SafeArea = DisplayServer.GetDisplaySafeArea();
		var windowSize = DisplayServer.WindowGetSize();

		if (windowSize.X <= 0 || windowSize.Y <= 0)
		{
			MarginLeft = 0;
			MarginRight = 0;
			MarginTop = 0;
			MarginBottom = 0;
			return;
		}

		MarginLeft = SafeArea.Position.X;
		MarginTop = SafeArea.Position.Y;
		MarginRight = windowSize.X - SafeArea.End.X;
		MarginBottom = windowSize.Y - SafeArea.End.Y;

		// Clamp to reasonable bounds
		MarginLeft = Mathf.Clamp(MarginLeft, 0, 120);
		MarginRight = Mathf.Clamp(MarginRight, 0, 120);
		MarginTop = Mathf.Clamp(MarginTop, 0, 80);
		MarginBottom = Mathf.Clamp(MarginBottom, 0, 80);
	}

	public void ApplyToControl(Control control)
	{
		if (control == null || !HasInsets) return;

		control.OffsetLeft = MarginLeft;
		control.OffsetTop = MarginTop;
		control.OffsetRight = -MarginRight;
		control.OffsetBottom = -MarginBottom;
	}

	public MarginContainer CreateSafeMarginContainer()
	{
		var margin = new MarginContainer();
		if (HasInsets)
		{
			margin.AddThemeConstantOverride("margin_left", MarginLeft);
			margin.AddThemeConstantOverride("margin_right", MarginRight);
			margin.AddThemeConstantOverride("margin_top", MarginTop);
			margin.AddThemeConstantOverride("margin_bottom", MarginBottom);
		}
		return margin;
	}

	public string BuildStatusSummary()
	{
		if (!HasInsets)
		{
			return "Safe area: no insets detected.";
		}

		return $"Safe area: L={MarginLeft} T={MarginTop} R={MarginRight} B={MarginBottom}";
	}
}
