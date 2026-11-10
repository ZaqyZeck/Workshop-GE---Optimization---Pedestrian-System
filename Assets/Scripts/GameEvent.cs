using System;
using UnityEngine;

public static partial class GameEvent
{
    // UIController
    public static Action OnRoadPlacement, OnHousePlacement, OnSpecialPlacement, OnParkStructurePlacement, OnExitBuildMode, OnRemoveStructure;
    public static Action OnSaveDataStructure, OnDeleteDataStructure;

    // InputManager
    public static event Action<Ray> OnMouseClick, OnMouseHold;
    public static event Action OnMouseUp, OnEscape;
}
