// Home and campaign share the same atlas; pending challenge links open here.
public partial class MainMenu : MapMenu
{
    public override void _Ready()
    {
        base._Ready();
        TryHandleDeepLink();
    }

    private void TryHandleDeepLink()
    {
        if (DeepLinkHandler.Instance == null || !DeepLinkHandler.Instance.HasPendingChallenge())
            return;

        var code = DeepLinkHandler.Instance.ConsumePendingChallenge();
        if (string.IsNullOrWhiteSpace(code)) return;

        GameState.Instance?.TrySetSelectedAsyncChallengeCode(code, out _);
        SceneRouter.Instance?.GoToMultiplayer();
    }
}
