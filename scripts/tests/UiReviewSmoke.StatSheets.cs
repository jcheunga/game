using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    /// <summary>Every visible stat sheet keeps each name clear of its value, both inside the cell at the reading size.</summary>
    private void CheckStatSheets(string screen)
    {
        foreach (var sheet in Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<StatSheet>().Where(sheet => sheet.IsVisibleInTree()))
            foreach (var cell in sheet.GetChildren().OfType<Control>())
            {
                var labels = cell.GetChildren().OfType<RoyalLabel>().ToArray();
                var (name, value) = (labels[0], labels[1]);
                var nameEnd = name.Position.X + name.TextWidth(name.FittedSize());
                var valueStart = value.Position.X + value.Size.X - value.TextWidth(value.FittedSize());
                var fits = nameEnd + 4 <= valueStart && value.Position.X + value.Size.X <= cell.Size.X - 4
                    && name.FittedSize() >= ReadingText.PhoneBody && value.FittedSize() >= ReadingText.PhoneBody;
                if (!fits) Check(false, $"{screen}: stat \"{cell.TooltipText}\" fits its cell");
            }
    }

    /// <summary>Selects every entry of a codex section, page by page, checking its stat sheet.</summary>
    private async Task CheckCodexSection(string section, string[] titles)
    {
        await Press(section);
        var checkedEntries = 0;
        for (var page = 0; page < 40; page++)
        {
            var root = LiveUiReview.ActiveRoot(GetTree());
            // The audio director moves hints into accessible names, so a cell's title is in either.
            var shown = Walk(root).OfType<Button>().Where(b => b.IsVisibleInTree() && !b.Disabled)
                .Select(b => titles.Contains(b.AccessibilityName) ? b.AccessibilityName : b.TooltipText).Where(titles.Contains).Distinct().ToArray();
            foreach (var title in shown) { await PressHint(title); CheckStatSheets($"Codex {title}"); checkedEntries++; }
            if (!Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<Button>().Any(b => b.IsVisibleInTree() && !b.Disabled && b.AccessibilityName == "Next codex page")) break;
            await PressHint("Next codex page");
        }
        Check(checkedEntries == titles.Length, $"Codex {section}: checked the stats of {checkedEntries} of {titles.Length} entries");
    }
}
