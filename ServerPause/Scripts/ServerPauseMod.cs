using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

public class ServerPauseMod : IModApi
{
    public static bool IsPaused { get; private set; } = false;

    private static ServerPauseMod _instance;
    private static float _savedTimeScale = 1f;

    public void InitMod(Mod _modInstance)
    {
        _instance = this;
        var harmony = new Harmony("com.qol.serverpause");
        harmony.PatchAll();

        ModEvents.GameStartDone.RegisterHandler(OnGameStartDone);

        Debug.Log("[ServerPause] Mod loaded. Use 'serverpause' console command to toggle pause.");
    }

    private void OnGameStartDone()
    {
        Debug.Log("[ServerPause] Game ready. Admin command 'serverpause' available.");
    }

    public static void PauseServer(string reason = "")
    {
        if (IsPaused) return;

        IsPaused = true;
        _savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        string msg = string.IsNullOrEmpty(reason)
            ? "Der Server wurde von einem Admin pausiert."
            : $"Der Server wurde von einem Admin pausiert: {reason}";

        BroadcastMessage(msg);
        Debug.Log($"[ServerPause] Server paused. Reason: {(string.IsNullOrEmpty(reason) ? "none" : reason)}");
    }

    public static void ResumeServer()
    {
        if (!IsPaused) return;

        IsPaused = false;
        Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : 1f;

        BroadcastMessage("Der Server wurde fortgesetzt.");
        Debug.Log("[ServerPause] Server resumed.");
    }

    private static void BroadcastMessage(string message)
    {
        if (GameManager.Instance == null) return;

        GameManager.ShowTooltip(null, message);

        List<ClientInfo> clients = ConnectionManager.Instance.Clients.List;
        foreach (ClientInfo client in clients)
        {
            GameManager.Instance.ChatMessageServer(
                client,
                EChatType.Global,
                -1,
                message,
                "[Server]",
                false,
                new List<int>()
            );
        }
    }
}

// Freeze all entity movement and AI while paused
[HarmonyPatch(typeof(EntityAlive), "OnUpdateLive")]
public class Patch_EntityAlive_OnUpdateLive
{
    static bool Prefix()
    {
        return !ServerPauseMod.IsPaused;
    }
}

// Block player actions while paused (movement input)
[HarmonyPatch(typeof(EntityPlayerLocal), "LateUpdate")]
public class Patch_EntityPlayerLocal_LateUpdate
{
    static bool Prefix()
    {
        return !ServerPauseMod.IsPaused;
    }
}

// Freeze world ticking (day/night cycle, game time)
[HarmonyPatch(typeof(GameManager), "gmUpdate")]
public class Patch_GameManager_gmUpdate
{
    static bool Prefix()
    {
        return !ServerPauseMod.IsPaused;
    }
}

// Block chunk simulation while paused
[HarmonyPatch(typeof(World), "TickWorldStep")]
public class Patch_World_TickWorldStep
{
    static bool Prefix()
    {
        return !ServerPauseMod.IsPaused;
    }
}
