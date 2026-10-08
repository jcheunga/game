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
        var start = AdventureTileCatalog.Starting("city").Site;

        async Task Prepare(string requiredVisit = "", int food = 100)
        {
            var fixture = state.BuildSaveData();
            fixture.Gold = 1000; fixture.Food = food;
            fixture.FoodRechargedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            fixture.VisitedAdventureSites = string.IsNullOrEmpty(requiredVisit) ? Array.Empty<string>() : new[] { requiredVisit };
            fixture.ClaimedAdventureDiscoveries = Array.Empty<string>();
            // Everything but the resources is charted, so every cache and find waits on the frontier.
            fixture.AdventureOpenTiles = AdventureTileCatalog.ForMap("city").Where(tile => !tile.IsResource).Select(tile => tile.Id).ToArray();
            fixture.AdventureReachedTiles = new[] { start.Id };
            fixture.AdventureCaravanTiles["city"] = start.Id;
            restore.Invoke(state, new object[] { fixture });
            canvas.ShowMap("city", start.Id);
            close.Invoke(menu, null); refresh.Invoke(menu, null);
            await Wait(.15);
        }

        AdventureMapToken SiteToken(string id) => Walk(canvas).OfType<AdventureMapToken>().Single(token => token.Site.Id == id);
        AdventureDiscoveryToken DiscoveryToken(string id) => Walk(canvas).OfType<AdventureDiscoveryToken>().Single(token => token.Discovery.Id == id);

        var supplies = AdventureMapCatalog.ForMap("city");
        foreach (var reward in new[] { supplies.First(site => site.Kind == AdventureSiteKind.Gold), supplies.First(site => site.Kind == AdventureSiteKind.Food), supplies.Single(site => site.Id == "hidden-city") })
        {
            await Prepare(reward.RequiredVisit);
            canvas.FocusSite(reward.Id);
            var token = SiteToken(reward.Id);
            Check(token.IsVisibleInTree() && !state.HasVisitedAdventureSite(reward.Id), reward.Id + ": a revealed, uncollected cache is selectable");
            await Capture("reward-" + reward.Id + "-before");
            await TapModal(token); await FinishTravel();
            Check(state.HasVisitedAdventureSite(reward.Id) && state.Gold == 1000 + reward.GoldReward
                && state.Food == 100 - GameState.AdventureTileFoodCost + reward.FoodReward,
                reward.Id + ": one native selection spends 2 food and grants the advertised supplies exactly once");
            Check(!token.Visible && token.Disabled && !menu.GetNode<PanelContainer>("HomeHud/SelectedSite").Visible,
                reward.Id + ": collection removes the marker without leaving an open details panel");
            Check(!Walk(menu).OfType<Control>().Any(control => control.Name == "TravelNotice"), reward.Id + ": collection never opens a bottom message popup");
            await Capture("reward-" + reward.Id + "-collected");
            var gold = state.Gold; var food = state.Food;
            token.EmitSignal(BaseButton.SignalName.Pressed); await Wait(.05);
            Check(state.Gold == gold && state.Food == food && !canvas.IsTravelling, reward.Id + ": stale selections cannot collect or travel again");
            state.ReloadFromDisk(); canvas.ShowMap("city", start.Id); await Wait(.15); canvas.FocusSite(reward.Id);
            Check(state.HasVisitedAdventureSite(reward.Id) && !SiteToken(reward.Id).Visible && state.Gold == gold && state.Food == food,
                reward.Id + ": reopening the saved map keeps the collected cache removed");
        }

        foreach (var kind in Enum.GetValues<AdventureDiscoveryKind>())
        {
            var reward = AdventureDiscoveryCatalog.ForMap("city").First(discovery => discovery.Kind == kind);
            await Prepare(); canvas.FocusSite(reward.Id);
            var token = DiscoveryToken(reward.Id);
            var before = state.BuildSaveData();
            Check(token.IsVisibleInTree() && !state.HasClaimedAdventureDiscovery(reward.Id), kind + ": an uncollected tile reward remains visible");
            await TapModal(token); await FinishTravel();
            Check(state.HasClaimedAdventureDiscovery(reward.Id) && !token.Visible
                && state.Gold == before.Gold + (kind == AdventureDiscoveryKind.Gold ? reward.Amount : 0)
                && state.Food == before.Food - GameState.AdventureTileFoodCost + (kind == AdventureDiscoveryKind.Food ? reward.Amount : 0)
                && state.Essence == before.Essence + (kind == AdventureDiscoveryKind.Essence ? reward.Amount : 0),
                kind + ": selecting a tile reward spends 2 food, grants its contents and removes its marker");
            var claimed = state.BuildSaveData();
            token.EmitSignal(BaseButton.SignalName.Pressed); await FinishTravel();
            Check(state.Gold == claimed.Gold && state.Food == claimed.Food && state.Essence == claimed.Essence,
                kind + ": a collected tile reward cannot grant its contents again");
            state.ReloadFromDisk(); canvas.ShowMap("city", start.Id); await Wait(.15); canvas.FocusSite(reward.Id);
            Check(state.HasClaimedAdventureDiscovery(reward.Id) && !DiscoveryToken(reward.Id).Visible, kind + ": the removed marker stays gone after reload");
        }

        var cache = supplies.First(site => site.Kind == AdventureSiteKind.Gold);
        await Prepare(food: 0); canvas.FocusSite(cache.Id);
        await TapModal(SiteToken(cache.Id));
        Check(SiteToken(cache.Id).Visible && !state.HasVisitedAdventureSite(cache.Id) && state.Gold == 1000
            && state.Food == 0 && !canvas.IsTravelling, "With zero rations a gold cache stays closed and charges nothing");
        Check(menu.GetNode<PanelContainer>("HomeHud/SelectedSite").Visible
            && Walk(menu).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text.Contains($"costs {GameState.AdventureTileFoodCost} food"))
            && !Walk(menu).OfType<Control>().Any(control => control.Name == "TravelNotice"), "A cache the caravan can't afford explains its 2 food cost in the site panel");
        AuditText("Tile map / cache needs food"); await Capture("reward-cache-needs-food");
        var provisions = AdventureDiscoveryCatalog.ForMap("city").First(discovery => discovery.Kind == AdventureDiscoveryKind.Food);
        await Prepare(food: GameState.AdventureTileFoodCost); canvas.FocusSite(provisions.Id);
        await TapModal(DiscoveryToken(provisions.Id));
        Check(state.HasClaimedAdventureDiscovery(provisions.Id) && state.Food == provisions.Amount && !canvas.IsTravelling
            && !menu.GetNode<PanelContainer>("HomeHud/SelectedSite").Visible, "Provisions can be gathered with just 2 rations and grant their full amount");
        AuditText("Tile map / provisions"); await Capture("reward-provisions");
        await Prepare(food: 0); canvas.FocusSite(start.Id);
        await TapModal(SiteToken(start.Id));
        Check(!state.TrySpendStageEntryFood(start.Stage, out _) && state.Food == 0
            && menu.GetNode<PanelContainer>("HomeHud/SelectedSite").Visible
            && Walk(menu).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text.Contains($"Need {state.GetStageEntryFoodCost(start.Stage)} food")),
            "Zero rations blocks battle entry and explains the entry cost in stage details");
        AuditText("Tile map / blocked entry"); await Capture("reward-blocked-entry");
        await Prepare(); canvas.FocusSite(cache.Id);
        var pending = SiteToken(cache.Id); var center = pending.GetGlobalRect().GetCenter();
        Send(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left, Position = center, GlobalPosition = center });
        Send(new InputEventMouseButton { Pressed = false, ButtonIndex = MouseButton.Left, Position = center, GlobalPosition = center });
        await Wait(.06);
        Check(!canvas.IsTravelling && !pending.Visible && state.HasVisitedAdventureSite(cache.Id)
            && state.Gold == 1000 + cache.GoldReward && state.Food == 100 - GameState.AdventureTileFoodCost, "A distant reward collects immediately for its 2 food");
        canvas.ShowMap("city", start.Id); await Wait(.35); canvas.FocusSite(cache.Id);
        Check(state.HasVisitedAdventureSite(cache.Id) && !SiteToken(cache.Id).Visible
            && state.Gold == 1000 + cache.GoldReward && state.Food == 100 - GameState.AdventureTileFoodCost,
            "Reopening the map keeps an immediately collected reward removed without another charge");

        foreach (var landmark in supplies.Where(site => site.Kind == AdventureSiteKind.Leader))
        {
            await Prepare(landmark.Id); canvas.FocusSite(landmark.Id);
            Check(state.HasVisitedAdventureSite(landmark.Id) && SiteToken(landmark.Id).Visible, landmark.Kind + ": permanent landmarks remain on the map after visiting");
        }

        restore.Invoke(state, new object[] { baseline });
        // Reselect the start without entering it (selecting a playable stage would open its preparation).
        typeof(MapMenu).GetField("_selected", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(menu, start);
        close.Invoke(menu, null); canvas.ShowMap("city", start.Id); refresh.Invoke(menu, null);
        await Wait(.15);
    }
}
