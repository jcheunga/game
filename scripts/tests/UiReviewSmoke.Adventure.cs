using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    private async Task ChooseAdventureSite(string id)
    {
        var canvas = Walk(GetTree().CurrentScene).OfType<MapPathCanvas>().Single();
        if (!GameState.Instance.IsAdventureSiteDiscovered(id))
        {
            throw new InvalidOperationException($"Review fixture has not opened adventure tile {id}.");
        }
        canvas.FocusSite(id); await Wait(.1);
        var token = Walk(GetTree().CurrentScene).OfType<AdventureMapToken>().Single(x => x.Site.Id == id && x.IsVisibleInTree());
        token.EmitSignal(BaseButton.SignalName.Pressed); await Wait(.2);
    }
    /// <summary>The visible text of the battle preparation open over the map.</summary>
    private string[] PreparationText() => Walk(GetTree().CurrentScene).OfType<LoadoutMenu>().Where(menu => menu.IsVisibleInTree())
        .SelectMany(menu => Walk(menu).OfType<RoyalLabel>()).Where(label => label.IsVisibleInTree()).Select(label => label.Text).ToArray();
    private async Task FinishTravel()
    {
        for (var i = 0; i < 60 && Walk(GetTree().CurrentScene).OfType<MapPathCanvas>().Any(x => x.IsTravelling); i++) await Wait(.1);
        Check(!Walk(GetTree().CurrentScene).OfType<MapPathCanvas>().Any(x => x.IsTravelling), "Tile travel has completed");
        await Wait(.1);
    }
}
