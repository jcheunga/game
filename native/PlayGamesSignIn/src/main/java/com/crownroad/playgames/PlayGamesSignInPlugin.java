package com.crownroad.playgames;

import android.app.Activity;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageManager;
import android.view.View;

import androidx.annotation.NonNull;

import com.google.android.gms.games.GamesSignInClient;
import com.google.android.gms.games.PlayGames;
import com.google.android.gms.games.PlayGamesSdk;

import org.godotengine.godot.Godot;
import org.godotengine.godot.plugin.GodotPlugin;
import org.godotengine.godot.plugin.SignalInfo;
import org.godotengine.godot.plugin.UsedByGodot;

import java.util.HashSet;
import java.util.Set;

/**
 * Signs the player in to Google Play Games and emits a one-time server auth code. The game
 * sends that code to its server, which exchanges it with Google to learn the player ID.
 */
public class PlayGamesSignInPlugin extends GodotPlugin {
    private static final String APP_ID = "com.google.android.gms.games.APP_ID";
    private boolean configured;

    public PlayGamesSignInPlugin(Godot godot) {
        super(godot);
    }

    @NonNull
    @Override
    public String getPluginName() {
        return "PlayGamesSignIn";
    }

    @NonNull
    @Override
    public Set<SignalInfo> getPluginSignals() {
        Set<SignalInfo> signals = new HashSet<>();
        signals.add(new SignalInfo("server_access_granted", String.class));
        signals.add(new SignalInfo("sign_in_failed", String.class));
        return signals;
    }

    @Override
    public View onMainCreate(Activity activity) {
        // The SDK cannot start without the Play Games project ID that the export adds to the
        // manifest; without it the game simply keeps its other sign-in options.
        configured = hasAppId(activity);
        if (configured) PlayGamesSdk.initialize(activity);
        return null;
    }

    @UsedByGodot
    public boolean isConfigured() {
        return configured;
    }

    /** Uses the existing Play Games sign-in, or shows Google's sign-in when interactive. */
    @UsedByGodot
    public void requestServerAccess(String serverClientId, boolean interactive) {
        Activity activity = getActivity();
        if (!configured || activity == null) {
            emitSignal("sign_in_failed", "Google Play Games is not set up in this build.");
            return;
        }
        GamesSignInClient client = PlayGames.getGamesSignInClient(activity);
        client.isAuthenticated().addOnCompleteListener(check -> {
            if (check.isSuccessful() && check.getResult().isAuthenticated()) {
                requestCode(client, serverClientId);
            } else if (!interactive) {
                emitSignal("sign_in_failed", "Not signed in to Google Play Games.");
            } else {
                client.signIn().addOnCompleteListener(signIn -> {
                    if (signIn.isSuccessful() && signIn.getResult().isAuthenticated()) requestCode(client, serverClientId);
                    else emitSignal("sign_in_failed", "Google Play Games sign-in was cancelled.");
                });
            }
        });
    }

    private void requestCode(GamesSignInClient client, String serverClientId) {
        client.requestServerSideAccess(serverClientId, false).addOnCompleteListener(task -> {
            if (task.isSuccessful() && task.getResult() != null) emitSignal("server_access_granted", task.getResult());
            else emitSignal("sign_in_failed", "Google Play Games could not confirm this player.");
        });
    }

    private static boolean hasAppId(Activity activity) {
        try {
            ApplicationInfo info = activity.getPackageManager().getApplicationInfo(activity.getPackageName(), PackageManager.GET_META_DATA);
            return info.metaData != null && info.metaData.get(APP_ID) != null;
        } catch (PackageManager.NameNotFoundException e) {
            return false;
        }
    }
}
