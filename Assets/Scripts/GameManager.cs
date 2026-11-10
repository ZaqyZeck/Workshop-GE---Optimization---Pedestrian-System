using SimpleCity.AI;
using SVS;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    // Singleton Instance
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // Subscribe to GameEvent instead of UIController events
        GameEvent.OnRoadPlacement += RoadPlacementHandler;
        GameEvent.OnHousePlacement += HousePlacementHandler;
        GameEvent.OnSpecialPlacement += SpecialPlacementHandler;
        GameEvent.OnParkStructurePlacement += ParkStructurePlacement;
        GameEvent.OnEscape += HandleEscape;
        GameEvent.OnExitBuildMode += HandleEscape;
        GameEvent.OnRemoveStructure += RemoveStructureHandler;
    }

    private void HandleEscape()
    {
        ClearInputActions();
        UIController.Instance.ResetButtonColor();
        PathVisualizer.Instance.ResetPath();
        // Subscribe to GameEvent instead of InputManager instance events
GameEvent.OnMouseClick += TrySelectingAgent;
    }

    private void TrySelectingAgent(Ray ray)
    {
        GameObject hitObject = ObjectDetector.Instance.RaycastAll(ray);
        if (hitObject != null)
        {
            var agentScript = hitObject.GetComponent<AiAgent>();
            agentScript?.ShowPath();
        }
    }

    private void ParkStructurePlacement()
    {
        ClearInputActions();
        InputManager.Instance.OnMouseClick += (pos) =>
        {
            ProcessInputAndCall(StructureManager.Instance.PlaceParkStructure, pos);
        };
        GameEvent.OnEscape += HandleEscape;
    }

    private void SpecialPlacementHandler()
    {
        ClearInputActions();
        InputManager.Instance.OnMouseClick += (pos) =>
        {
            ProcessInputAndCall(StructureManager.Instance.PlaceSpecial, pos);
        };
        GameEvent.OnEscape += HandleEscape;
    }

    private void HousePlacementHandler()
    {
        ClearInputActions();
        InputManager.Instance.OnMouseClick += (pos) =>
        {
            ProcessInputAndCall(StructureManager.Instance.PlaceHouse, pos);
        };
        GameEvent.OnEscape += HandleEscape;
    }

    private void RoadPlacementHandler()
    {
        ClearInputActions();
        InputManager.Instance.OnMouseClick += (pos) =>
        {
            ProcessInputAndCall(RoadManager.Instance.PlaceRoad, pos);
        };
        InputManager.Instance.OnMouseUp += RoadManager.Instance.FinishPlacingRoad;
        InputManager.Instance.OnMouseHold += (pos) =>
        {
            ProcessInputAndCall(RoadManager.Instance.PlaceRoad, pos);
        };
        GameEvent.OnEscape += HandleEscape;
    }

    private void RemoveStructureHandler()
    {
        ClearInputActions();
        InputManager.Instance.OnMouseClick += (pos) =>
        {
            ProcessInputAndCall(PlacementManager.Instance.RemoveStructureAt, pos);
        };
        GameEvent.OnEscape += HandleEscape;
    }

    private void ClearInputActions()
    {
        InputManager.Instance.ClearEvents();
    }

    private void ProcessInputAndCall(Action<Vector3Int> callback, Ray ray)
    {
        Vector3Int? result = ObjectDetector.Instance.RaycastGround(ray);
        if (result.HasValue)
            callback.Invoke(result.Value);
    }

    private void Update()
    {
        CameraMovement.Instance.MoveCamera(new Vector3(InputManager.Instance.CameraMovementVector.x, 0, InputManager.Instance.CameraMovementVector.y));
    }
}
