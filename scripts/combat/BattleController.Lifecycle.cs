using Godot;

public partial class BattleController
{
    private void OnBattleApplicationStateChanged()
    {
        if (AppLifecycleService.Instance?.IsInteractive != false || _battleEnded ||
            _battlePaused || _restartPending || _endlessCheckpointActive || IsLanRaceMode || IsOnlineRoomMode)
            return;

        // Returning to the app keeps the pause menu open until the player is ready.
        TogglePause();
        GameState.Instance.SaveNow();
    }
}
