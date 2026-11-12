// UIController now uses centralized GameEvent for UI actions
using System;
using System.Collections.Generic;
using SimpleCity.AI;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    // Singleton Instance
    public static UIController Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    public Button placeRoadButton, placeHouseButton, placeSpecialButton, placeParkStructureButton,
        destroyStructureButton, enterBuildModeButton, exitBuildModeButton, saveStructureButton, deleteDataButton,
        spawnPedestrainButton, spawnCarButton;

    [Tooltip("Optional: sends every pedestrian to a park of a different type.")]
    public Button changeParkButton;

    public GameObject buildButtonGroup;

    public Color outlineColor;
    private List<Button> buttonList;

    private void Start()
    {
        buttonList = new List<Button> { placeHouseButton, placeRoadButton, placeSpecialButton, placeParkStructureButton };

        placeRoadButton.onClick.AddListener(() =>
        {
            ResetButtonColor();
            ModifyOutline(placeRoadButton);
            GameEvent.OnRoadPlacement?.Invoke();
        });
        placeHouseButton.onClick.AddListener(() =>
        {
            ResetButtonColor();
            ModifyOutline(placeHouseButton);
            GameEvent.OnHousePlacement?.Invoke();
        });
        placeSpecialButton.onClick.AddListener(() =>
        {
            ResetButtonColor();
            ModifyOutline(placeSpecialButton);
            GameEvent.OnSpecialPlacement?.Invoke();
        });
        placeParkStructureButton.onClick.AddListener(() =>
        {
            ResetButtonColor();
            ModifyOutline(placeParkStructureButton);
            GameEvent.OnParkStructurePlacement?.Invoke();
        });
        destroyStructureButton.onClick.AddListener(() =>
        {
            ResetButtonColor();
            ModifyOutline(destroyStructureButton);
            GameEvent.OnRemoveStructure?.Invoke();
        });
        enterBuildModeButton.onClick.AddListener(() =>
        {
            buildButtonGroup.SetActive(true);
        });
        exitBuildModeButton.onClick.AddListener(() =>
        {
            ResetButtonColor();
            GameEvent.OnExitBuildMode?.Invoke();
        });
        if (saveStructureButton != null) saveStructureButton.onClick.AddListener(() =>
        {
            GameEvent.OnSaveDataStructure?.Invoke();
        }
        );
        if (deleteDataButton != null) deleteDataButton.onClick.AddListener(() =>
        {
            GameEvent.OnDeleteDataStructure?.Invoke();
        });
        spawnCarButton.onClick.AddListener(() =>
        {
            //AiDirector.Instance.SpawnACar();
            AiEntityDirector.Instance.SpawnACar(); 
        });
        spawnPedestrainButton.onClick.AddListener(() =>
        {
            //AiDirector.Instance.SpawnAllAagents();
            AiEntityDirector.Instance.SpawnAllAagents();
        });
        if (changeParkButton != null) changeParkButton.onClick.AddListener(() =>
        {
            //AiDirector.Instance.SendAllAgentsToDifferentParks();
            AiEntityDirector.Instance.SendAllAgentsToDifferentParks();
        });
    }

    private void ModifyOutline(Button button)
    {
        var outline = button.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = outlineColor;
            outline.enabled = true;
        }
    }

    public void ResetButtonColor()
    {
        foreach (Button button in buttonList)
        {
            var outline = button.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
        }
    }
}
