using HarmonyLib;
using UnityEngine;

public class ServerPauseMod : IModApi
{
    public static bool IsPaused { get; private set; } = false;
    private static float _savedTimeScale = 1f;

    public void InitMod(Mod _modInstance)
    {
        var harmony = new Harmony("com.qol.serverpause");
        harmony.PatchAll();
        Debug.Log("[ServerPause] Mod loaded. Press ESC to pause/resume the server.");
    }

    public static void PauseServer()
    {
        if (IsPaused) return;
        IsPaused = true;
        _savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        Debug.Log("[ServerPause] Server paused.");
    }

    public static void ResumeServer()
    {
        if (!IsPaused) return;
        IsPaused = false;
        Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : 1f;
        Debug.Log("[ServerPause] Server resumed.");
    }
}

[HarmonyPatch(typeof(EntityAlive), "OnUpdateLive")]
public class Patch_EntityAlive_OnUpdateLive
{
    static bool Prefix() => !ServerPauseMod.IsPaused;
}

[HarmonyPatch(typeof(EntityPlayerLocal), "LateUpdate")]
public class Patch_EntityPlayerLocal_LateUpdate
{
    static bool Prefix() => !ServerPauseMod.IsPaused;
}

[HarmonyPatch(typeof(GameManager), "gmUpdate")]
public class Patch_GameManager_gmUpdate
{
    static bool Prefix() => !ServerPauseMod.IsPaused;
}

[HarmonyPatch(typeof(World), "TickWorldStep")]
public class Patch_World_TickWorldStep
{
    static bool Prefix() => !ServerPauseMod.IsPaused;
}
