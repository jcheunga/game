using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    /// <summary>
    /// Settings → Account → Delete account, tapped like a player. A signed-in player sees their sign-in named, and
    /// with no server nothing is deleted. A guest that never reached a server confirms (after keeping the account
    /// once) and only the device resets; server_e2e.sh covers the server side. Run with --mobile-preview for the
    /// phone-sized canvas.
    /// </summary>
    private async Task ReviewAccountDeletion()
    {
        _output = ProjectSettings.GlobalizePath(OS.GetCmdlineUserArgs().Contains("--mobile-preview") ? "res://artifacts/account-deletion-phone" : "res://artifacts/account-deletion");
        System.IO.Directory.CreateDirectory(_output);
        var state = GameState.Instance;
        state.ResetProgress(); state.SetAnalyticsConsent(false); state.SetShowHints(false);
        state.SetSelectedStage(1); state.PrepareCampaignBattle(); state.ApplyVictory(1, 40, 2, 3);
        var profile = state.PlayerProfileId;
        Check(state.HighestUnlockedStage == 2 && state.PlayerAuthToken.Length == 0, "The review starts as an offline guest with some progress");

        await Open("MainMenu");
        await PressHint("Settings");
        var settings = Walk(GetTree().Root).OfType<SettingsMenu>().Single(page => !page.IsQueuedForDeletion());
        Button Named(string name) => Walk(settings).OfType<Button>().FirstOrDefault(button => button.IsVisibleInTree() && !button.IsQueuedForDeletion() && button.AccessibilityName == name);
        Button Labelled(string text) => Walk(settings).OfType<Button>().FirstOrDefault(button => button.IsVisibleInTree() && !button.IsQueuedForDeletion() && button.Text == text);
        await TapModal(Named("Account") ?? throw new InvalidOperationException("Settings has no Account tab"));
        var row = Named("Delete account") ?? throw new InvalidOperationException("The Account tab has no Delete account row");
        for (var parent = row.GetParent(); parent != null; parent = parent.GetParent())
            if (parent is ScrollContainer scroll) RevealInScroll(scroll, row);
        await Wait(.2);
        Check(GetViewport().GetVisibleRect().Encloses(row.GetGlobalRect()), "The Delete account row scrolls fully into view");
        AuditText("Settings / Account"); await Capture("settings-account");

        // Signed in, with no server to delete the account on.
        state.SetSignedInAccount("apple", "Apple account");
        await TapModal(row);
        Check(Walk(settings).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text.Contains("unlinks your Apple sign-in")),
            "A signed-in player is told their sign-in is unlinked");
        await Capture("delete-confirm-signed-in");
        await TapModal(Labelled("Delete account"));
        await Wait(1);
        Check(state.PlayerProfileId == profile && state.AccountProvider == "apple" && state.HighestUnlockedStage == 2 &&
            Walk(settings).OfType<RoyalLabel>().Any(label => label.IsVisibleInTree() && label.Text.Contains("server") &&
                GetViewport().GetVisibleRect().Encloses(label.GetGlobalRect())),
            "Without the server nothing is deleted, and Settings shows why on screen");
        AuditText("Settings / Delete account failed"); await Capture("delete-failed");
        state.ClearPlayerProfileSession();
        row = Named("Delete account");

        await TapModal(row);
        var confirm = Labelled("Delete account"); var keep = Labelled("Keep account");
        Check(Walk(settings).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text == "Delete your account?") && confirm != null && keep != null,
            "Delete account asks for confirmation, with Keep account beside it");
        Check(confirm != null && keep != null && GetViewport().GetVisibleRect().Encloses(confirm.GetGlobalRect()) && GetViewport().GetVisibleRect().Encloses(keep.GetGlobalRect()),
            "Both confirmation buttons fit on screen");
        AuditText("Settings / Delete account"); await Capture("delete-confirm");
        if (keep == null || confirm == null) return;

        await TapModal(keep);
        Check(Labelled("Delete account") == null && state.PlayerProfileId == profile && state.HighestUnlockedStage == 2, "Keep account closes the confirmation and changes nothing");
        await TapModal(Named("Delete account"));
        await TapModal(Labelled("Delete account"));
        await Wait(1);
        Check(state.PlayerProfileId != profile && state.HighestUnlockedStage == 1 && state.SkipAutoSignIn && state.AccountProvider.Length == 0,
            "Confirming starts the device again as a new guest that does not sign in by itself");
        Check(GetTree().CurrentScene is MainMenu && !((MapMenu)GetTree().CurrentScene).HasHomeModal, "Deleting returns to the camp");
        await Capture("after-delete");
    }
}
