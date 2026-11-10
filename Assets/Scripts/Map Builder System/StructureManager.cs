using SVS;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class StructureManager : MonoBehaviour
{
    public static StructureManager Instance;

    // Footprint (in grid cells) of every park structure.
    public const int ParkWidth = 2;
    public const int ParkHeight = 2;

    public StructurePrefabWeighted[] housesPrefabe, specialPrefabs, parkStructuresPrefabs;
    //public PlacementManager placementManager;

    private float[] houseWeights, specialWeights, parkStructureWeights;

    private void Awake()
    {
        Instance = this;
    }
    private void Start()
    {
        houseWeights = housesPrefabe.Select(prefabStats => prefabStats.weight).ToArray();
        specialWeights = specialPrefabs.Select(prefabStats => prefabStats.weight).ToArray();
        parkStructureWeights = parkStructuresPrefabs.Select(prefabStats => prefabStats.weight).ToArray();
    }

    public void PlaceHouse(Vector3Int position)
    {
        if (CheckPositionBeforePlacement(position))
        {
            int randomIndex = GetRandomWeightedIndex(houseWeights);
            int id = 100 + randomIndex;
            PlacementManager.Instance.PlaceObjectOnTheMap(position, housesPrefabe[randomIndex].prefab, CellType.Structure, id);
            AudioPlayer.instance.PlayPlacementSound();
        }
    }

    internal void PlaceParkStructure(Vector3Int position)
    {
        int width = ParkWidth;
        int height = ParkHeight;
        if(CheckParkStructure(position, width , height))
        {
            int randomIndex = GetRandomWeightedIndex(parkStructureWeights);
            int id = 300 + randomIndex;
            PlacementManager.Instance.PlaceObjectOnTheMap(position, parkStructuresPrefabs[randomIndex].prefab, CellType.Structure, id, width, height);
            AudioPlayer.instance.PlayPlacementSound();
        }
    }

    private bool CheckParkStructure(Vector3Int position, int width, int height)
    {
        bool nearRoad = false;
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                var newPosition = position + new Vector3Int(x, 0, z);
                
                if (DefaultCheck(newPosition)==false)
                {
                    return false;
                }
                if (nearRoad == false)
                {
                    nearRoad = RoadCheck(newPosition);
                }
            }
        }
        return nearRoad;
    }

    public void PlaceSpecial(Vector3Int position)
    {
        if (CheckPositionBeforePlacement(position))
        {
            int randomIndex = GetRandomWeightedIndex(specialWeights);
            int id = 200 + randomIndex;
            PlacementManager.Instance.PlaceObjectOnTheMap(position, specialPrefabs[randomIndex].prefab, CellType.SpecialStructure, id);
            AudioPlayer.instance.PlayPlacementSound();
        }
    }

    private int GetRandomWeightedIndex(float[] weights)
    {
        float sum = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            sum += weights[i];
        }

        float randomValue = UnityEngine.Random.Range(0, sum);
        float tempSum = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            //0->weihg[0] weight[0]->weight[1]
            if(randomValue >= tempSum && randomValue < tempSum + weights[i])
            {
                return i;
            }
            tempSum += weights[i];
        }
        return 0;
    }

    private bool CheckPositionBeforePlacement(Vector3Int position)
    {
        if (DefaultCheck(position) == false)
        {
            return false;
        }

        if (RoadCheck(position) == false)
            return false;
        
        return true;
    }

    private bool RoadCheck(Vector3Int position)
    {
        if (PlacementManager.Instance.GetNeighboursOfTypeFor(position, CellType.Road).Count <= 0)
        {
            Debug.Log("Must be placed near a road");
            return false;
        }
        return true;
    }

    private bool DefaultCheck(Vector3Int position)
    {
        if (PlacementManager.Instance.CheckIfPositionInBound(position) == false)
        {
            Debug.Log("This position is out of bounds");
            return false;
        }
        if (PlacementManager.Instance.CheckIfPositionIsFree(position) == false)
        {
            Debug.Log("This position is not EMPTY");
            return false;
        }
        return true;
    }
}

[Serializable]
public struct StructurePrefabWeighted
{
    public GameObject prefab;
    [Range(0,1)]
    public float weight;
}
