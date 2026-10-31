using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    /// <summary>
    /// Holds a button of every kind and captures it pressed beside the same screen at rest: painted plate buttons and
    /// hotspots on the home HUD, a map landmark, the armory's cards and tabs, theme buttons in a dialog and a battle
    /// card. Each held press must sink, and a release that acts must spring the button up past rest.
    /// </summary>
    private async Task ReviewButtonPress()
    {
        _output = ProjectSettings.GlobalizePath(GetWindow().Size.Y < 500 ? "res://artifacts/button-press/mobile" : "res://artifacts/button-press/desktop");
        System.IO.Directory.CreateDirectory(_output);
        var state = GameState.Instance; state.ResetProgress(); state.SetShowHints(false); state.SetAnalyticsConsent(false);
        async Task Frames(int count) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        async Task HoldAndCapture(string name, params BaseButton[] buttons)
        {
            await Capture(name + "-rest");
            foreach (var button in buttons) ButtonFeel.Hold(button, true);
            await Frames(20);
            Check(buttons.All(button => ButtonFeel.Amount(button) > .99f), $"{name}: every held button is fully pressed ({string.Join(", ", buttons.Select(b => b.Name))})");
            await Capture(name + "-held");
            foreach (var button in buttons) ButtonFeel.Hold(button, false);
            await Frames(30);
            Check(buttons.All(button => ButtonFeel.Amount(button) == 0), $"{name}: every button rises back to rest");
        }

        await Open("MainMenu");
        var menu = (MapMenu)GetTree().CurrentScene;
        RoyalButton Named(Node root, string name) => Walk(root).OfType<RoyalButton>().FirstOrDefault(x => x.IsVisibleInTree() && x.Name == name);
        var warband = Named(menu, "WarbandTab");
        var settings = Walk(menu).OfType<RoyalButton>().FirstOrDefault(x => x.IsVisibleInTree() && x.AccessibilityName == "Settings");
        var card = Named(menu, "PlayerCard");
        var gold = Walk(menu).OfType<RoyalButton>().FirstOrDefault(x => x.IsVisibleInTree() && x.AccessibilityName == "Royal storehouse");
        var home = new BaseButton[] { warband, settings, card, gold }.Where(x => x != null).ToArray();
        Check(home.Length == 4 && home.All(x => x.HasMeta(ButtonFeel.Painted)), "The home HUD's dock, settings ring, player card and resources are pressed in as painted art");
        await HoldAndCapture("01-home", home);

        // A landmark on the map: the map reads the touch and presses the painted landmark in itself.
        var token = Walk(menu).OfType<AdventureMapToken>().FirstOrDefault(x => x.IsVisibleInTree() && !x.Disabled
            && GetViewport().GetVisibleRect().HasPoint(x.GetGlobalTransformWithCanvas() * x.MarkerCenter));
        if (token != null)
        {
            var at = token.GetGlobalTransformWithCanvas() * (token.MarkerCenter - new Vector2(0, 30));
            await Capture("02-landmark-rest");
            Send(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Position = at, GlobalPosition = at });
            await Frames(20);
            Check(ButtonFeel.Amount(token) > .99f, $"Holding a map landmark presses it in ({token.Site.Title}, {ButtonFeel.Amount(token):0.00})");
            await Capture("02-landmark-held");
            // Sliding away turns the touch into a pan: the landmark rises and nothing opens.
            var away = at + new Vector2(60, 0);
            Send(new InputEventMouseMotion { Position = away, GlobalPosition = away, ButtonMask = MouseButtonMask.Left });
            Send(new InputEventMouseButton { Pressed = false, ButtonIndex = MouseButton.Left, Position = away, GlobalPosition = away });
            await Frames(30);
            Check(ButtonFeel.Amount(token) == 0, "A landmark rises again once the touch pans the map");
        }
        else Check(false, "A map landmark is on screen to press");

        // Theme buttons in a dialog (RealmButton with the engraved surfaces).
        NicknameDialog.Show(menu); await Wait(.4);
        var dialogButtons = Walk(menu).OfType<Button>().Where(x => x.IsVisibleInTree() && x.Name == "ConfirmNickname").Cast<BaseButton>()
            .Concat(Walk(menu).OfType<Button>().Where(x => x.IsVisibleInTree() && x.Text == "Cancel")).ToArray();
        Check(dialogButtons.Length == 2, "The nickname dialog shows its confirm and cancel buttons");
        await HoldAndCapture("03-dialog", dialogButtons);
        dialogButtons.Last().EmitSignal(BaseButton.SignalName.Pressed); await Wait(.3);

        // The armory: plate tabs, kit tabs and the roster's hotspot cards over their own art.
        warband.EmitSignal(BaseButton.SignalName.Pressed); await Wait(.8);
        var armory = Walk(menu).OfType<ShopMenu>().FirstOrDefault(x => x.IsVisibleInTree());
        var armoryButtons = armory == null ? new RoyalButton[0] : Walk(armory).OfType<RoyalButton>().Where(x => x.IsVisibleInTree() && !x.Disabled).ToArray();
        var hotspot = armoryButtons.FirstOrDefault(x => x.HasMeta(ButtonFeel.Painted) && x.Caption == null && x.Glyph == null && x.Size.Y > 60);
        var tab = armoryButtons.FirstOrDefault(x => x.ToggleMode && !x.ButtonPressed);
        var kit = armoryButtons.FirstOrDefault(x => !x.HasMeta(ButtonFeel.Painted) && x.Caption != null);
        var armoryHeld = new BaseButton[] { hotspot, tab, kit }.Where(x => x != null).ToArray();
        Check(armoryHeld.Length == 3, $"The armory has a hotspot card, a tab and a kit button to press ({armoryHeld.Length})");
        await HoldAndCapture("04-armory", armoryHeld);

        // A real release over the button acts and springs it up past rest.
        if (tab != null)
        {
            ButtonFeel.Hold(tab, true); await Frames(10);
            ButtonFeel.Hold(tab, false, acted: true);
            var lowest = 0f;
            for (var frame = 0; frame < 30; frame++) { await Frames(1); lowest = Mathf.Min(lowest, ButtonFeel.Amount(tab)); }
            Check(lowest < -.2f && ButtonFeel.Amount(tab) == 0, $"A release that acts springs the button up past rest, then settles ({lowest:0.00})");
        }

        // A battle card reads its own touch: holding it presses it, lifting off deploys and springs it up.
        var fixture = state.BuildSaveData();
        fixture.OwnedPlayerUnitIds = GameData.PlayerRosterIds.ToArray(); fixture.ActiveDeckUnitIds = GameData.PlayerRosterIds.Take(3).ToArray();
        state.RestoreCloudSave(fixture);
        state.PrepareCampaignBattle(); await Open("Battle");
        var battle = (BattleController)GetTree().CurrentScene;
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        T Read<T>(string name) => (T)typeof(BattleController).GetField(name, hidden)!.GetValue(battle)!;
        void Write(string name, object value) => typeof(BattleController).GetField(name, hidden)!.SetValue(battle, value);
        if (Read<bool>("_battlePaused")) typeof(BattleController).GetMethod("TogglePause", hidden)!.Invoke(battle, null);
        Write("_courage", 100f); Read<BattleDeckState>("_deck").ReduceCooldowns(1000);
        typeof(BattleController).GetMethod("UpdateHud", hidden)!.Invoke(battle, null);
        await Frames(5);
        var actionCard = Walk(battle).OfType<BattleActionCard>().First(x => x.IsVisibleInTree());
        var cardButton = (BaseButton)actionCard.GetParent();
        var point = actionCard.CostPlate.GetGlobalTransformWithCanvas() * (actionCard.CostPlate.Size * .5f);
        await Capture("05-battle-card-rest");
        Send(new InputEventMouseMotion { Position = point, GlobalPosition = point });
        Send(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Position = point, GlobalPosition = point });
        await Frames(20);
        Check(ButtonFeel.Amount(cardButton) > .99f, $"Holding a battle card presses it in ({ButtonFeel.Amount(cardButton):0.00})");
        await Capture("05-battle-card-held");
        var deployments = Read<int>("_playerDeployments");
        Send(new InputEventMouseButton { Pressed = false, ButtonIndex = MouseButton.Left, Position = point, GlobalPosition = point });
        var spring = 0f;
        for (var frame = 0; frame < 30; frame++) { await Frames(1); spring = Mathf.Min(spring, ButtonFeel.Amount(cardButton)); }
        Check(Read<int>("_playerDeployments") == deployments + 1 && spring < -.2f, $"Lifting off a held battle card deploys and springs the card up ({spring:0.00})");

        GD.Print($"BUTTON_PRESS_REVIEW_RESULT: {_failures} failures");
        QuitAfterAudio(_failures == 0 ? 0 : 1);
    }
}
