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
	private Color _backgroundColor = new(0f, 0f, 0f, 0.45f);
	private Color _frameColor = new(1f, 1f, 1f, 0.25f);
	private Color _flashColor = Colors.White;
	private string _label = "";
	private string _valueText = "";
	private bool _showLabel = true;

	public void Setup(Color fillColor, Color frameColor, string label, bool showLabel = true)
	{
		_fillColor = fillColor;
		_frameColor = frameColor;
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

		DrawRect(barRect, _backgroundColor, true);

		var fillWidth = barRect.Size.X * _displayRatio;
		if (fillWidth > 0.5f)
		{
			var fillRect = new Rect2(barRect.Position, new Vector2(fillWidth, barRect.Size.Y));
			var fillColor = _flashTimer > 0.05f
				? _fillColor.Lerp(_flashColor, Mathf.Clamp(_flashTimer / 0.2f, 0f, 1f) * 0.4f)
				: _fillColor;
			DrawRect(fillRect, fillColor, true);
		}

		DrawRect(barRect, _frameColor, false, 1.5f);

		if (!_showLabel || (string.IsNullOrWhiteSpace(_label) && string.IsNullOrWhiteSpace(_valueText)))
		{
			return;
		}

		var font = ThemeDB.FallbackFont;
		var fontSize = Mathf.Max(10, Mathf.RoundToInt(barRect.Size.Y * 0.65f));

		if (!string.IsNullOrWhiteSpace(_label))
		{
			var labelSize = font.GetStringSize(_label, HorizontalAlignment.Left, -1f, fontSize);
			var labelPos = new Vector2(5f, (barRect.Size.Y + labelSize.Y) * 0.5f - 2f);
			DrawString(font, labelPos, _label, HorizontalAlignment.Left, -1f, fontSize, new Color(1f, 1f, 1f, 0.7f));
		}

		if (!string.IsNullOrWhiteSpace(_valueText))
		{
			var valueSize = font.GetStringSize(_valueText, HorizontalAlignment.Right, -1f, fontSize);
			var valuePos = new Vector2(barRect.Size.X - valueSize.X - 5f, (barRect.Size.Y + valueSize.Y) * 0.5f - 2f);
			DrawString(font, valuePos, _valueText, HorizontalAlignment.Left, -1f, fontSize, Colors.White);
		}
	}
}
