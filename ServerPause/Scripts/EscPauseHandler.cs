using System;
using HarmonyLib;
using UnityEngine;

// Applied via PatchAll in ServerPauseMod.InitMod.
// Uses Postfix so Rebirth's own GameManager.Update logic runs first —
// we only append our ESC check after, which avoids execution-order conflicts.
[HarmonyPatch(typeof(GameManager), "Update")]
public class Patch_GameManager_Update_EscPause
{
    private static float _cooldown = 0f;

    static void Postfix()
    {
        try
        {
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;

            _cooldown -= Time.unscaledDeltaTime;

            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (_cooldown > 0f) return;

            // Skip if any UI window is open (inventory, ESC-menu, etc.)
            LocalPlayerUI playerUI = LocalPlayerUI.GetUIForPrimaryPlayer();
            if (playerUI != null && playerUI.windowManager.IsAnyWindowOpen()) return;

            _cooldown = 0.3f;

            if (ServerPauseMod.IsPaused)
                ServerPauseMod.ResumeServer();
            else
                ServerPauseMod.PauseServer();
        }
        catch (Exception ex)
        {
            // Never let our code bubble exceptions into GameManager.Update
            Debug.LogError($"[ServerPause] EscPauseHandler error: {ex.Message}");
        }
    }
}
