using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

public class ServerPauseMod : IModApi
{
    public static bool IsPaused { get; private set; } = false;
    private static float _savedTimeScale = 1f;

    // Tracks which optional patches were successfully applied
    private static bool _patchedEntityAlive    = false;
    private static bool _patchedEntityPlayer   = false;
    private static bool _patchedGmUpdate       = false;
    private static bool _patchedTickWorldStep  = false;

    private static Harmony _harmony;

    public void InitMod(Mod _modInstance)
    {
        _harmony = new Harmony("com.qol.serverpause");

        // ESC handler — applied via attribute in EscPauseHandler.cs, very low risk
        _harmony.PatchAll(typeof(Patch_GameManager_Update_EscPause).Assembly);

        // Optional patches applied individually so a conflict with Rebirth or
        // another overhaul mod only disables that specific patch, not the whole mod.
        // Time.timeScale = 0 already freezes physics + FixedUpdate; these patches
        // additionally block Update/LateUpdate-based logic that ignores timeScale.
        TryPatchOptional(
            typeof(EntityAlive), "OnUpdateLive",
            prefix: nameof(Patch_EntityAlive_OnUpdateLive.Prefix),
            patchType: typeof(Patch_EntityAlive_OnUpdateLive),
            ref _patchedEntityAlive,
            "EntityAlive.OnUpdateLive"
        );

        TryPatchOptional(
            typeof(EntityPlayerLocal), "LateUpdate",
            prefix: nameof(Patch_EntityPlayerLocal_LateUpdate.Prefix),
            patchType: typeof(Patch_EntityPlayerLocal_LateUpdate),
            ref _patchedEntityPlayer,
            "EntityPlayerLocal.LateUpdate"
        );

        TryPatchOptional(
            typeof(GameManager), "gmUpdate",
            prefix: nameof(Patch_GameManager_gmUpdate.Prefix),
            patchType: typeof(Patch_GameManager_gmUpdate),
            ref _patchedGmUpdate,
            "GameManager.gmUpdate"
        );

        TryPatchOptional(
            typeof(World), "TickWorldStep",
            prefix: nameof(Patch_World_TickWorldStep.Prefix),
            patchType: typeof(Patch_World_TickWorldStep),
            ref _patchedTickWorldStep,
            "World.TickWorldStep"
        );

        Debug.Log($"[ServerPause] Loaded. ESC toggles pause. Optional patches: " +
                  $"EntityAlive={_patchedEntityAlive}, EntityPlayer={_patchedEntityPlayer}, " +
                  $"gmUpdate={_patchedGmUpdate}, TickWorldStep={_patchedTickWorldStep}");
    }

    private void TryPatchOptional(Type targetType, string methodName,
        string prefix, Type patchType, ref bool success, string label)
    {
        try
        {
            MethodInfo original = AccessTools.Method(targetType, methodName);
            if (original == null)
            {
                Debug.LogWarning($"[ServerPause] Method not found, skipping: {label}");
                return;
            }

            // Check if too many other mods already patched this method.
            // If another Prefix already blocks execution (returns false) we
            // skip to avoid double-blocking which can hide bugs in overhaul mods.
            var existingPatches = Harmony.GetPatchInfo(original);
            if (existingPatches != null && existingPatches.Prefixes.Count >= 3)
            {
                Debug.LogWarning($"[ServerPause] {label} has {existingPatches.Prefixes.Count} existing prefixes — skipping to avoid conflict. Time.timeScale=0 still active.");
                return;
            }

            HarmonyMethod harmonyPrefix = new HarmonyMethod(
                AccessTools.Method(patchType, prefix));

            _harmony.Patch(original, prefix: harmonyPrefix);
            success = true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ServerPause] Could not patch {label} (likely overhaul mod conflict). " +
                             $"Pause still works via Time.timeScale. Details: {ex.Message}");
        }
    }

    public static void PauseServer()
    {
        if (IsPaused) return;
        IsPaused = true;

        // Primary freeze: stops Unity physics, FixedUpdate, and animation ticks.
        // Even without optional patches this reliably halts the game world.
        _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;

        Debug.Log("[ServerPause] Paused." +
                  (_patchedEntityAlive ? "" : " (EntityAlive patch inactive — overhaul mode)"));
    }

    public static void ResumeServer()
    {
        if (!IsPaused) return;
        IsPaused = false;
        Time.timeScale = _savedTimeScale;
        Debug.Log("[ServerPause] Resumed.");
    }
}

// --- Optional patch classes (only applied if no conflict detected) ---

public class Patch_EntityAlive_OnUpdateLive
{
    public static bool Prefix() => !ServerPauseMod.IsPaused;
}

public class Patch_EntityPlayerLocal_LateUpdate
{
    public static bool Prefix() => !ServerPauseMod.IsPaused;
}

public class Patch_GameManager_gmUpdate
{
    public static bool Prefix() => !ServerPauseMod.IsPaused;
}

public class Patch_World_TickWorldStep
{
    public static bool Prefix() => !ServerPauseMod.IsPaused;
}
