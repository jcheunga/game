using Godot;

/// <summary>
/// A compact, animated HUD bar for displaying a ratio (courage, wave progress, etc.).
/// </summary>
public partial class BattleHudBar : Control
{
	private float _targetRatio;
	private float _displayRatio;
	private float _flashTimer;
	private Color _fillColor = new("80ed99");
	private Color _flashColor = Colors.White;
	private string _label = "";
	private string _valueText = "";
	private bool _showLabel = true;
	private StyleBoxTexture _track, _fill;

	public void Setup(Color fillColor, Color frameColor, string label, bool showLabel = true)
	{
		_fillColor = fillColor;
		_flashColor = fillColor.Lightened(0.35f);
		_label = label;
		_showLabel = showLabel;
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public void SetValue(float ratio, string valueText = "")
	{
		var oldRatio = _targetRatio;
		_targetRatio = Mathf.Clamp(ratio, 0f, 1f);
		_valueText = valueText;

		if (Mathf.Abs(_targetRatio - oldRatio) > 0.02f)
		{
			_flashTimer = 0.2f;
		}
	}

	public override void _Process(double delta)
	{
		var deltaF = (float)delta;
		_displayRatio = Mathf.MoveToward(_displayRatio, _targetRatio, deltaF * 3f);
		_flashTimer = Mathf.Max(0f, _flashTimer - deltaF);
		QueueRedraw();
	}

	public override void _Draw()
	{
		var barRect = new Rect2(Vector2.Zero, Size);

		_track ??= MedievalUi.Engraved("meter_track", 0, 0);
		_fill ??= MedievalUi.Engraved("meter_fill", 0, 0);
		DrawStyleBox(_track, barRect);

		var well = barRect.Grow(-3);
		var fillWidth = Mathf.Max(0,well.Size.X) * _displayRatio;
		if (fillWidth > 0.5f)
		{
			var fillRect = new Rect2(well.Position, new Vector2(fillWidth, well.Size.Y));
			var fillColor = _flashTimer > 0.05f
				? _fillColor.Lerp(_flashColor, Mathf.Clamp(_flashTimer / 0.2f, 0f, 1f) * 0.4f)
				: _fillColor;
			_fill.ModulateColor = fillColor.Darkened(.32f);
			DrawStyleBox(_fill, fillRect);
		}

		if(GameState.Instance?.HighContrast ?? false) DrawRect(barRect, new Color("ead7a3"), false, 1.5f);

		if (!_showLabel || (string.IsNullOrWhiteSpace(_label) && string.IsNullOrWhiteSpace(_valueText)))
		{
			return;
		}

		var font = ThemeDB.FallbackFont;
		var fontSize = Mathf.Max(18, Mathf.RoundToInt(barRect.Size.Y * 0.65f));

		if (!string.IsNullOrWhiteSpace(_label))
		{
			var labelSize = font.GetStringSize(_label, HorizontalAlignment.Left, -1f, fontSize);
			var labelPos = new Vector2(5f, (barRect.Size.Y + labelSize.Y) * 0.5f - 2f);
			DrawStringOutline(font, labelPos, _label, HorizontalAlignment.Left, -1f, fontSize, 2, new Color("060c11"));
			DrawString(font, labelPos, _label, HorizontalAlignment.Left, -1f, fontSize, new Color("e6e5d8"));
		}

		if (!string.IsNullOrWhiteSpace(_valueText))
		{
			var valueSize = font.GetStringSize(_valueText, HorizontalAlignment.Right, -1f, fontSize);
			var valuePos = new Vector2(barRect.Size.X - valueSize.X - 5f, (barRect.Size.Y + valueSize.Y) * 0.5f - 2f);
			DrawStringOutline(font, valuePos, _valueText, HorizontalAlignment.Left, -1f, fontSize, 2, new Color("060c11"));
			DrawString(font, valuePos, _valueText, HorizontalAlignment.Left, -1f, fontSize, Colors.White);
		}
	}
}
