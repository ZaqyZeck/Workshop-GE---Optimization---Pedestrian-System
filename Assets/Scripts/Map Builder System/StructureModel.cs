using SimpleCity.AI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Park variant, matching the index inside StructureManager.parkStructuresPrefabs.
/// </summary>
public enum ParkType
{
    None = -1,
    Green = 0,   // parkStructuresPrefabs[0]
    Red = 1,     // parkStructuresPrefabs[1]
    Yellow = 2   // parkStructuresPrefabs[2]
}

public class StructureModel : MonoBehaviour, INeedingRoad
{
    float yHeight = 0;

    public Vector3Int RoadPosition { get; set; }

    // awalan id = 0, road structure
    // awalan id = 1, house structure
    // awalan id = 2, special structure
    // awalan id = 3, park structure

    public int structureId;

    public bool IsRoad => structureId / 100 == 0;
    public bool IsPark => structureId / 100 == 3;

    /// <summary>
    /// Park variant of this structure (prefab index = id % 100), or ParkType.None if it is not a park.
    /// </summary>
    public ParkType ParkType => IsPark ? (ParkType)(structureId % 100) : ParkType.None;

    public void CreateModel(GameObject model, int id)
    {
        var structure = Instantiate(model, transform);
        yHeight = structure.transform.position.y;
        structureId = id;
    }

    public void SwapModel(GameObject model, Quaternion rotation)
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
        var structure = Instantiate(model, transform);
        structure.transform.localPosition = new Vector3(0, yHeight, 0);
        structure.transform.localRotation = rotation;
    }

    public Vector3 GetNearestPedestrianMarkerTo(Vector3 position)
    {
        return transform.GetChild(0).GetComponent<RoadHelper>().GetClosestPedestrainPosition(position);
    }

    public Marker GetPedestrianSpawnMarker(Vector3 position)
    {
        return transform.GetChild(0).GetComponent<RoadHelper>().GetpositioForPedestrianToSpwan(position);
    }

    public List<Marker> GetPedestrianMarkers()
    {
        return transform.GetChild(0).GetComponent<RoadHelper>().GetAllPedestrianMarkers();
    }

    internal List<Marker> GetCarMarkers()
    {
        return transform.GetChild(0).GetComponent<RoadHelper>().GetAllCarMarkers();
    }

    public Vector3 GetNearestCarMarkerTo(Vector3 position)
    {
        return transform.GetChild(0).GetComponent<RoadHelper>().GetClosestCarMarkerPosition(position);
    }

    public Marker GetCarSpawnMarker(Vector3Int nextPathPosition)
    {
        return transform.GetChild(0).GetComponent<RoadHelper>().GetPositioForCarToSpawn(nextPathPosition);
    }

    public Marker GetCarEndMarker(Vector3Int previousPathPosition)
    {
        return transform.GetChild(0).GetComponent<RoadHelper>().GetPositioForCarToEnd(previousPathPosition);
    }
}
