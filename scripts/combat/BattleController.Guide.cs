using System.Linq;
using Godot;

/// <summary>
/// The battle menu's guide, on three tabs: this battle's enemies and how to counter them (the prep briefing's cards),
/// the signs drawn on the field (status chips, the ground sigil of a charging attack, the colours of the numbers),
/// and the caravan spell.
/// </summary>
public partial class BattleController
{
    private void OpenCombatGuide()
    {
        var page = GuidePage.Open(this, "CombatGuide", "Combat guide", "codex", 66);
        page.Tabs(0, ("This battle", body => EncounterBriefing.Build(body, _stageData)), ("Signs", BuildGuideSigns), ("Caravan spell", BuildGuideCaravan));
    }

    private void BuildGuideSigns(VBoxContainer body)
    {
        // The chips drawn over units; the body-shown ones only appear as chips with high contrast on.
        var all = GameState.Instance.HighContrast;
        var grid = GuidePage.Grid(2);
        grid.AddThemeConstantOverride("v_separation", 6);
        body.AddChild(grid);
        foreach (var (label, meaning) in CombatGuideCatalog.Signs.Where(sign => all || !Unit.StatusOnBody(sign.Label)))
            grid.AddChild(GuidePage.Explained(GuidePage.Chip(label, Unit.StatusTint(label)), meaning, 86));
        if (!all) body.AddChild(RealmUi.Label("Stun, root, silence, burn and shield show on the unit; High contrast adds chips.", 18, true));
        // Then the ground sigil of a charging attack and the colours of the numbers.
        var sigil = GuidePage.Picture(SpellVfx.Tex("sigil_war"), new Vector2(64, 40), new Color("e8b876"));
        body.AddChild(GuidePage.Explained(sigil, "A rune circle on the ground: an attack is charging there. Move out before it fills.", 86));
        var numbers = GuidePage.Grid(4);
        body.AddChild(numbers);
        foreach (var (text, kind, critical, meaning) in new[] {
            ("48", CombatTextKind.Damage, false, "Damage"), ("72", CombatTextKind.Damage, true, "Critical"),
            ("+12", CombatTextKind.Heal, false, "Healing"), ("+5", CombatTextKind.Courage, false, "Courage") })
            numbers.AddChild(GuidePage.Explained(new CombatNumberSample(text, kind, critical), meaning, 86));
    }

    private void BuildGuideCaravan(VBoxContainer body)
    {
        var spell = CaravanSpell;
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 16); body.AddChild(row);
        var path = $"res://assets/ui/royal/items/{spell.Id}.png";
        row.AddChild(GuidePage.Portrait(ResourceLoader.Exists(path) ? RoyalArt.Load(path) : null, 132));
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; column.AddThemeConstantOverride("separation", 8);
        row.AddChild(column);
        column.AddChild(RealmUi.SectionTitle(spell.DisplayName, 22));
        if (!string.IsNullOrWhiteSpace(spell.Description)) column.AddChild(RealmUi.Label(spell.Description, 18));
        column.AddChild(ArmoryDetailUi.Stats(new ArmoryDetailUi.Stat[] {
            new("mana", "Mana", $"{spell.ManaCost:0}"), new("duration", "Barrage", $"{spell.Duration:0.#}s"), new("clock", "Recharge", $"{spell.Cooldown:0}s") }, 3));
        body.AddChild(GuidePage.Callout("icon-flame-gold", "It opens beside your cards when the mana bar is full. Tap it, or press Space."));
    }
}

/// <summary>A sample of the battle's floating numbers in their own face and gradient, for the guide's key.</summary>
public partial class CombatNumberSample : Control
{
    private const int FontSize = 30;
    private static readonly Vector2 Box = new(64, 36);
    private readonly string _text;

    public CombatNumberSample(string text, CombatTextKind kind, bool critical)
    {
        _text = text;
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = Box;
        // The field's gradient runs about the figures' centre; here that is the box's middle.
        var fill = BattleFloatText.Fill(kind, critical, FontSize);
        fill.SetShaderParameter("top_y", Box.Y / 2 - FontSize * .36f);
        fill.SetShaderParameter("bottom_y", Box.Y / 2 + FontSize * .36f);
        Material = fill;
    }

    public override void _Draw()
    {
        var font = RoyalFonts.Gothic(900);
        var width = font.GetStringSize(_text, HorizontalAlignment.Left, -1, FontSize).X;
        // As on the field: outlined in ink, the white fill taking the gradient.
        var at = new Vector2((Box.X - width) / 2, Box.Y / 2 + FontSize * .34f);
        DrawStringOutline(font, at, _text, HorizontalAlignment.Left, -1, FontSize, 5, new Color(.11f, .07f, .045f, .8f));
        DrawString(font, at, _text, HorizontalAlignment.Left, -1, FontSize, Colors.White);
    }
}
