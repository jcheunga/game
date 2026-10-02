using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    private async Task ReviewMapRewards(MapMenu menu, MapPathCanvas canvas)
    {
        var state = GameState.Instance;
        var baseline = state.BuildSaveData();
        var restore = typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var refresh = typeof(MapMenu).GetMethod("RefreshUi", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var close = typeof(MapMenu).GetMethod("CloseSiteDetails", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var camp = AdventureMapCatalog.ForMap("city").First();

        async Task Prepare(Vector2 point, string requiredVisit = "", int food = 100)
        {
            var fixture = state.BuildSaveData();
            fixture.Gold = 1000; fixture.Food = food;
            fixture.FoodRechargedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            fixture.VisitedAdventureSites = string.IsNullOrEmpty(requiredVisit) ? Array.Empty<string>() : new[] { requiredVisit };
            fixture.ClaimedAdventureDiscoveries = Array.Empty<string>();
            fixture.AdventureOpenTiles = AdventureTileCatalog.ForMap("city").Select(tile => tile.Id).ToArray();
            fixture.AdventureReachedTiles = new[] { camp.Id };
            fixture.AdventureCaravanTiles["city"] = camp.Id;
            fixture.AdventureHeroNodes["city"] = camp.Id;
            fixture.AdventureHeroPositions["city"] = new[] { point.X, point.Y };
            fixture.AdventureExploredCells["city"] = Array.Empty<int>();
            fixture.AdventureTravelledCells["city"] = new[] { AdventureTerrain.Cell(camp.Point), AdventureTerrain.Cell(point) };
            restore.Invoke(state, new object[] { fixture });
            canvas.ShowMap("city", camp.Id);
            close.Invoke(menu, null); refresh.Invoke(menu, null);
            await Wait(.15);
        }

        AdventureMapToken SiteToken(string id) => Walk(canvas).OfType<AdventureMapToken>().Single(token => token.Site.Id == id);
        AdventureDiscoveryToken DiscoveryToken(string id) => Walk(canvas).OfType<AdventureDiscoveryToken>().Single(token => token.Discovery.Id == id);
        Vector2 Neighbor(Vector2 point) => camp.Point;

        var supplies = AdventureMapCatalog.ForMap("city");
        foreach (var reward in new[] { supplies.First(site => site.Kind == AdventureSiteKind.Gold), supplies.First(site => site.Kind == AdventureSiteKind.Food), supplies.Single(site => site.Id == "hidden-city") })
        {
            await Prepare(Neighbor(reward.Point), reward.RequiredVisit);
            canvas.FocusSite(reward.Id);
            var token = SiteToken(reward.Id);
            Check(token.IsVisibleInTree() && !state.HasVisitedAdventureSite(reward.Id), reward.Id + ": a revealed, uncollected cache is selectable");
            await Capture("reward-" + reward.Id + "-before");
            await TapModal(token); await FinishTravel();
            Check(state.HasVisitedAdventureSite(reward.Id) && state.Gold == 1000 + reward.GoldReward && state.Food == 99 + reward.FoodReward,
                reward.Id + ": one native selection travels and grants the advertised supplies exactly once");
            Check(!token.Visible && token.Disabled && !menu.GetNode<PanelContainer>("HomeHud/SelectedSite").Visible,
                reward.Id + ": collection removes the marker without leaving an open details panel");
            Check(!Walk(menu).OfType<Control>().Any(control => control.Name == "TravelNotice"), reward.Id + ": collection never opens a bottom message popup");
            await Capture("reward-" + reward.Id + "-collected");
            var gold = state.Gold; var food = state.Food;
            token.EmitSignal(BaseButton.SignalName.Pressed); await Wait(.05);
            Check(state.Gold == gold && state.Food == food && !canvas.IsTravelling, reward.Id + ": stale selections cannot collect or travel again");
            state.ReloadFromDisk(); canvas.ShowMap("city", camp.Id); await Wait(.15); canvas.FocusSite(reward.Id);
            Check(state.HasVisitedAdventureSite(reward.Id) && !SiteToken(reward.Id).Visible && state.Gold == gold && state.Food == food,
                reward.Id + ": reopening the saved map keeps the collected cache removed");
        }

        foreach (var kind in Enum.GetValues<AdventureDiscoveryKind>())
        {
            var reward = AdventureDiscoveryCatalog.ForMap("city").First(discovery => discovery.Kind == kind);
            await Prepare(Neighbor(reward.Point)); canvas.FocusSite(reward.Id);
            var token = DiscoveryToken(reward.Id);
            var before = state.BuildSaveData();
            Check(token.IsVisibleInTree() && !state.HasClaimedAdventureDiscovery(reward.Id), kind + ": an uncollected tile reward remains visible");
            await TapModal(token); await FinishTravel();
            Check(state.HasClaimedAdventureDiscovery(reward.Id) && !token.Visible
                && state.Gold == before.Gold + (kind == AdventureDiscoveryKind.Gold ? reward.Amount : 0)
                && state.Food == before.Food - 1 + (kind == AdventureDiscoveryKind.Food ? reward.Amount : 0)
                && state.Tomes == before.Tomes + (kind == AdventureDiscoveryKind.Tomes ? reward.Amount : 0)
                && state.Essence == before.Essence + (kind == AdventureDiscoveryKind.Essence ? reward.Amount : 0),
                kind + ": selecting a tile reward grants its contents and removes its marker");
            var claimed = state.BuildSaveData();
            token.EmitSignal(BaseButton.SignalName.Pressed); await FinishTravel();
            Check(state.Gold == claimed.Gold && state.Food == claimed.Food && state.Tomes == claimed.Tomes && state.Essence == claimed.Essence,
                kind + ": a collected tile reward cannot grant its contents again");
            state.ReloadFromDisk(); canvas.ShowMap("city", camp.Id); await Wait(.15); canvas.FocusSite(reward.Id);
            Check(state.HasClaimedAdventureDiscovery(reward.Id) && !DiscoveryToken(reward.Id).Visible, kind + ": the removed marker stays gone after reload");
        }

        var blocked = supplies.First(site => site.Kind == AdventureSiteKind.Gold);
        await Prepare(Neighbor(blocked.Point), food: 0); canvas.FocusSite(blocked.Id);
        await TapModal(SiteToken(blocked.Id));
        Check(SiteToken(blocked.Id).Visible && !state.HasVisitedAdventureSite(blocked.Id) && state.Gold == 1000 && !canvas.IsTravelling,
            "A reward stays available when food prevents reaching it");
        Check(!Walk(menu).OfType<Control>().Any(control => control.Name == "TravelNotice"), "Food requirements do not open a bottom message popup");
        Check(menu.GetNode<PanelContainer>("HomeHud/SelectedSite").Visible && Walk(menu).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text.Contains("Need 1 food")), "A blocked resource selection explains its food requirement inside site details");
        var blockedDiscovery = AdventureDiscoveryCatalog.ForMap("city").First();
        await Prepare(camp.Point, food: 0); canvas.FocusSite(blockedDiscovery.Id);
        await TapModal(DiscoveryToken(blockedDiscovery.Id));
        Check(!state.HasClaimedAdventureDiscovery(blockedDiscovery.Id) && !canvas.IsTravelling && menu.GetNode<PanelContainer>("HomeHud/SelectedSite").Visible
            && Walk(menu).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text.Contains("Need 1 food")), "A blocked discovery also explains its food requirement without collecting");
        await Prepare(Neighbor(blocked.Point)); canvas.FocusSite(blocked.Id);
        var pending = SiteToken(blocked.Id); var center = pending.GetGlobalRect().GetCenter();
        Send(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left, Position = center, GlobalPosition = center });
        Send(new InputEventMouseButton { Pressed = false, ButtonIndex = MouseButton.Left, Position = center, GlobalPosition = center });
        await Wait(.06);
        Check(canvas.IsTravelling && pending.Visible && !state.HasVisitedAdventureSite(blocked.Id), "A distant reward is removed only after the caravan reaches it");
        canvas.ShowMap("city", camp.Id); await Wait(.35); canvas.FocusSite(blocked.Id);
        Check(!state.HasVisitedAdventureSite(blocked.Id) && SiteToken(blocked.Id).Visible && state.Gold == 1000,
            "Interrupting travel keeps an unreached reward available");

        foreach (var landmark in new[] { camp, supplies.First(site => site.Kind == AdventureSiteKind.Shrine), supplies.First(site => site.Kind == AdventureSiteKind.Watchtower), supplies.First(site => site.Kind == AdventureSiteKind.Leader) })
        {
            await Prepare(landmark.Point, landmark.Id); canvas.FocusSite(landmark.Id);
            Check(state.HasVisitedAdventureSite(landmark.Id) && SiteToken(landmark.Id).Visible, landmark.Kind + ": permanent landmarks remain on the map after visiting");
        }

        restore.Invoke(state, new object[] { baseline });
        typeof(MapMenu).GetMethod("SelectSite", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(menu, new object[] { camp });
        close.Invoke(menu, null); canvas.ShowMap("city", camp.Id); refresh.Invoke(menu, null);
        await Wait(.15);
    }
}
