using System.Collections.Generic;

public class ConsoleCmdServerPause : ConsoleCmdAbstract
{
    public override string getDescription()
    {
        return "Pausiert oder setzt den Server fort. Nur für Admins.";
    }

    public override string getHelp()
    {
        return "Verwendung:\n" +
               "  serverpause          - Pausiert/Setzt den Server fort (Toggle)\n" +
               "  serverpause pause    - Pausiert den Server\n" +
               "  serverpause pause <Grund> - Pausiert mit Nachricht an alle Spieler\n" +
               "  serverpause resume   - Setzt den Server fort\n" +
               "  serverpause status   - Zeigt aktuellen Pausenstatus";
    }

    public override string[] getCommands()
    {
        return new string[] { "serverpause", "sp" };
    }

    public override bool IsExecuteOnClient => false;

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        // Only admins may use this command
        if (_senderInfo.RemoteClientInfo != null)
        {
            int adminLevel = GameManager.Instance.adminTools.GetUserPermissionLevel(_senderInfo.RemoteClientInfo);
            int requiredLevel = GamePrefs.GetInt(EnumGamePrefs.ServerAdminSlots);

            // Lower permission level number = higher rank in 7DTD
            if (adminLevel > 0)
            {
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output(
                    "[ServerPause] Du hast keine Berechtigung für diesen Befehl.");
                return;
            }
        }

        if (_params.Count == 0 || _params[0].ToLower() == "toggle")
        {
            Toggle();
            return;
        }

        switch (_params[0].ToLower())
        {
            case "pause":
                string reason = _params.Count > 1 ? string.Join(" ", _params.GetRange(1, _params.Count - 1)) : "";
                ServerPauseMod.PauseServer(reason);
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output(
                    "[ServerPause] Server wurde pausiert.");
                break;

            case "resume":
            case "fortsetzen":
                ServerPauseMod.ResumeServer();
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output(
                    "[ServerPause] Server wurde fortgesetzt.");
                break;

            case "status":
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output(
                    $"[ServerPause] Status: {(ServerPauseMod.IsPaused ? "PAUSIERT" : "Läuft")}");
                break;

            default:
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output(getHelp());
                break;
        }
    }

    private void Toggle()
    {
        if (ServerPauseMod.IsPaused)
        {
            ServerPauseMod.ResumeServer();
            SingletonMonoBehaviour<SdtdConsole>.Instance.Output("[ServerPause] Server fortgesetzt.");
        }
        else
        {
            ServerPauseMod.PauseServer();
            SingletonMonoBehaviour<SdtdConsole>.Instance.Output("[ServerPause] Server pausiert.");
        }
    }
}
