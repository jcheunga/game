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
		GetViewport().SizeChanged -= UpdateSafeArea;
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public override void _Ready()
	{
		UpdateSafeArea();
		GetViewport().SizeChanged += UpdateSafeArea;
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
        // Desktop safe rectangles are display coordinates, not window content insets.
        // Applying them to a windowed game incorrectly shifts the HUD below the menu bar.
        if (!OS.HasFeature("android") && !OS.HasFeature("ios"))
        {
            SafeArea = new Rect2I(Vector2I.Zero, DisplayServer.WindowGetSize());
            MarginLeft = MarginRight = MarginTop = MarginBottom = 0;
            return;
        }
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

		// Safe area is in physical screen pixels, while controls use stretched
		// canvas coordinates. The inverse also accounts for letterbox padding.
		var insets = LogicalInsets(SafeArea, GetViewport().GetScreenTransform(), GetViewport().GetVisibleRect().Size);
		MarginLeft = Mathf.CeilToInt(insets.X);
		MarginTop = Mathf.CeilToInt(insets.Y);
		MarginRight = Mathf.CeilToInt(insets.Z);
		MarginBottom = Mathf.CeilToInt(insets.W);
	}

	internal static Vector4 LogicalInsets(Rect2 safeArea, Transform2D canvasToScreen, Vector2 canvasSize)
	{
		if (safeArea.Size.X <= 0 || safeArea.Size.Y <= 0) return Vector4.Zero;
		var inverse = canvasToScreen.AffineInverse();
		var start = inverse * safeArea.Position;
		var end = inverse * safeArea.End;
		return new Vector4(Mathf.Clamp(start.X, 0, canvasSize.X / 3), Mathf.Clamp(start.Y, 0, canvasSize.Y / 3),
			Mathf.Clamp(canvasSize.X-end.X, 0, canvasSize.X / 3), Mathf.Clamp(canvasSize.Y-end.Y, 0, canvasSize.Y / 3));
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
