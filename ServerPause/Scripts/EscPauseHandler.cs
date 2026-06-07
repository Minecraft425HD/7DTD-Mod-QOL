using HarmonyLib;
using UnityEngine;

// Intercepts ESC key for the server host to toggle server pause.
// Works whether players are online or not.
[HarmonyPatch(typeof(GameManager), "Update")]
public class Patch_GameManager_Update_EscPause
{
    private static bool _escWasDown = false;
    private static float _cooldown = 0f;

    static void Postfix()
    {
        // Only the local machine running as server host may use ESC to pause
        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;

        // Ignore if a UI window (like ESC-menu or inventory) is open
        LocalPlayerUI playerUI = LocalPlayerUI.GetUIForPrimaryPlayer();
        if (playerUI != null && playerUI.windowManager.IsAnyWindowOpen()) return;

        _cooldown -= Time.unscaledDeltaTime;
        bool escDown = Input.GetKeyDown(KeyCode.Escape);

        if (escDown && _cooldown <= 0f)
        {
            _cooldown = 0.3f; // debounce: prevent rapid toggling

            if (ServerPauseMod.IsPaused)
                ServerPauseMod.ResumeServer();
            else
                ServerPauseMod.PauseServer();
        }
    }
}
