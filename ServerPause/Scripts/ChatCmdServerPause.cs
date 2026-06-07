using System.Collections.Generic;
using HarmonyLib;

// Intercepts incoming chat messages to handle /pause and /resume admin chat commands
[HarmonyPatch(typeof(GameManager), "ChatMessage")]
public class Patch_GameManager_ChatMessage
{
    static bool Prefix(ClientInfo _cInfo, EChatType _chatType, int _senderEntityId,
        string _msg, string _mainName, bool _localizeMain, List<int> _recipientEntityIds)
    {
        if (_cInfo == null || string.IsNullOrEmpty(_msg)) return true;
        if (!_msg.StartsWith("/")) return true;

        // Check admin permission (level 0 = full admin in 7DTD)
        int adminLevel = GameManager.Instance.adminTools.GetUserPermissionLevel(_cInfo);
        if (adminLevel > 0) return true;

        string cmd = _msg.Trim().ToLower();

        if (cmd == "/pause" || cmd.StartsWith("/pause "))
        {
            string reason = cmd.Length > 7 ? _msg.Substring(7).Trim() : "";
            ServerPauseMod.PauseServer(reason);
            return false; // Don't broadcast the command itself as chat
        }

        if (cmd == "/resume" || cmd == "/fortsetzen")
        {
            ServerPauseMod.ResumeServer();
            return false;
        }

        if (cmd == "/pausestatus")
        {
            string status = ServerPauseMod.IsPaused ? "PAUSIERT" : "Läuft normal";
            GameManager.Instance.ChatMessageServer(
                _cInfo,
                EChatType.Whisper,
                -1,
                $"[ServerPause] Status: {status}",
                "[Server]",
                false,
                new List<int> { _cInfo.entityId }
            );
            return false;
        }

        return true;
    }
}
