using System.Collections;
using SimpleCity.AI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CommandController : MonoBehaviour
{
    [SerializeField] private Text debugUI;
    [SerializeField] private Text modeUI;
    [SerializeField] private TMP_InputField inputUI;

    private Coroutine debugCoroutine;

    private void Update()
    {
        if (Keyboard.current.enterKey.wasPressedThisFrame)
        {
            if (string.IsNullOrEmpty(inputUI.text)) return;
            Command(inputUI.text);
            inputUI.text = null;
        }
    }

    public void Command(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;

        command = command.Trim().ToLower();
        if (!command.StartsWith('/')) return;

        string cleanCommand = command.Substring(1).Trim();

        switch (cleanCommand)
        {
            case "destroy structure":
                DebugCommand("");
                GameEvent.OnRemoveStructure?.Invoke();
                DebugBuildMode("Build Mode: Destroy");
                return;

            case "switch park":
                AiEntityDirector.Instance.SendAllAgentsToDifferentParks();
                DebugCommand("pedestrian switching park");
                return;

            case "save structure data":
                GameEvent.OnSaveDataStructure?.Invoke();
                DebugCommand("structure data saved");
                return;

            case "delete save data":
                GameEvent.OnDeleteDataStructure?.Invoke();
                DebugCommand("structure data deleted");
                return;

            case "exit build mode":
                GameEvent.OnExitBuildMode?.Invoke();
                DebugCommand("");
                DebugBuildMode("Build Mode: Off");
                return;
            case "revive":
                GameEvent.OnPlayerRevive?.Invoke();
                DebugCommand("Player hidup lagi");
                break;
            case "die":
                GameEvent.OnPlayerDie?.Invoke();
                DebugCommand("Player mati");
                break;

        }

        int spaceIndex = cleanCommand.IndexOf(' ');
        if (spaceIndex == -1)
        {
            DebugCommand("wrong command");
            return;
        }

        string commandType = cleanCommand.Substring(0, spaceIndex);
        string commandLeft = cleanCommand.Substring(spaceIndex + 1).Trim();

        switch (commandType)
        {
            case "spawn":
                if (int.TryParse(commandLeft, out int number))
                {
                    AiEntityDirector.Instance.SetPedestrianNumber(number);
                    AiEntityDirector.Instance.SpawnAllAagents();

                    DebugCommand($"spawning {number} pedestrian");
                }
                else
                {
                    DebugCommand("Not a valid number.");
                }
                break;

            case "build":
                switch (commandLeft)
                {
                    case "road":
                        GameEvent.OnRoadPlacement?.Invoke();
                        DebugCommand("");
                        DebugBuildMode("Build Mode: Road");
                        break;

                    case "house":
                        DebugCommand("");
                        GameEvent.OnHousePlacement?.Invoke();
                        DebugBuildMode("Build Mode: House");
                        break;

                    case "store":
                        DebugCommand("");
                        GameEvent.OnSpecialPlacement?.Invoke();
                        DebugBuildMode("Build Mode: Store");
                        break;

                    case "park":
                        DebugCommand("");
                        DebugBuildMode("Build Mode: Park");
                        GameEvent.OnParkStructurePlacement?.Invoke();
                        break;

                    default:
                        DebugCommand("wrong build type");
                        break;
                }
                break;

            default:
                DebugCommand("wrong command");
                break;
        }
    }

    private void DebugCommand(string debugMessage)
    {
        if (debugCoroutine != null)
        {
            StopCoroutine(debugCoroutine);
        }

        debugUI.color = Color.white;
        debugUI.text = debugMessage;

        if (!string.IsNullOrEmpty(debugMessage))
        {
            debugCoroutine = StartCoroutine(DeleteMessageAfter(3f));
        }
    }

    private IEnumerator DeleteMessageAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        debugUI.color = new Color(1f, 1f, 1f, 0f);
        debugCoroutine = null;
    }

    private void DebugBuildMode(string buildMode)
    {
        modeUI.text = buildMode;
    }
}