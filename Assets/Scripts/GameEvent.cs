using System;
using UnityEngine;

public static partial class GameEvent
{
    public static Action OnRoadPlacement, OnHousePlacement, OnSpecialPlacement, OnParkStructurePlacement, OnExitBuildMode, OnRemoveStructure;
    public static Action OnSaveDataStructure, OnDeleteDataStructure;

    public static event Action<Ray> OnMouseClick, OnMouseHold;
    public static event Action OnMouseUp, OnEscape;

    public static Action OnPlayerDie, OnPlayerRevive;
}
