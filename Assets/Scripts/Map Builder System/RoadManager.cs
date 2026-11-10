using SVS;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RoadManager : MonoBehaviour
{
    public static RoadManager Instance;
    //public PlacementManager placementManager;

    public GameObject deadEnd, roadStraight, corner, threeWay, fourWay;

    public List<Vector3Int> temporaryPlacementPositions = new List<Vector3Int>();
    public List<Vector3Int> roadPositionsToRecheck = new List<Vector3Int>();

    private Vector3Int startPosition;
    private bool placementMode = false;

    //public RoadFixer roadFixer;

    private void Awake()
    {
        Instance = this;
    }

    //private void Start()
    //{
    //    roadFixer = GetComponent<RoadFixer>();
    //}

    public void PlaceRoad(Vector3Int position)
    {
        if (PlacementManager.Instance.CheckIfPositionInBound(position) == false)
            return;
        if (PlacementManager.Instance.CheckIfPositionIsFree(position) == false)
            return;
        if (placementMode == false)
        {
            temporaryPlacementPositions.Clear();
            roadPositionsToRecheck.Clear();

            placementMode = true;
            startPosition = position;

            temporaryPlacementPositions.Add(position);
            PlacementManager.Instance.PlaceTemporaryStructure(position, deadEnd, CellType.Road);

        }
        else
        {
            PlacementManager.Instance.RemoveAllTemporaryStructures();
            temporaryPlacementPositions.Clear();

            foreach (var positionsToFix in roadPositionsToRecheck)
            {
                FixRoadAtPosition(positionsToFix);
            }

            roadPositionsToRecheck.Clear();

            temporaryPlacementPositions = PlacementManager.Instance.GetPathBetween(startPosition, position);

            foreach (var temporaryPosition in temporaryPlacementPositions)
            {
                if (PlacementManager.Instance.CheckIfPositionIsFree(temporaryPosition) == false)
                {
                    roadPositionsToRecheck.Add(temporaryPosition);
                    continue;
                }  
                PlacementManager.Instance.PlaceTemporaryStructure(temporaryPosition, deadEnd, CellType.Road);
            }
        }

        FixRoadPrefabs();

    }

    private void FixRoadPrefabs()
    {
        foreach (var temporaryPosition in temporaryPlacementPositions)
        {
            FixRoadAtPosition(temporaryPosition);
            var neighbours = PlacementManager.Instance.GetNeighboursOfTypeFor(temporaryPosition, CellType.Road);
            foreach (var roadposition in neighbours)
            {
                if (roadPositionsToRecheck.Contains(roadposition)==false)
                {
                    roadPositionsToRecheck.Add(roadposition);
                }
            }
        }
        foreach (var positionToFix in roadPositionsToRecheck)
        {
            FixRoadAtPosition(positionToFix);
        }
    }

    public void FinishPlacingRoad()
    {
        placementMode = false;
        PlacementManager.Instance.AddtemporaryStructuresToStructureDictionary();
        if (temporaryPlacementPositions.Count > 0)
        {
            AudioPlayer.instance.PlayPlacementSound();
        }
        temporaryPlacementPositions.Clear();
        startPosition = Vector3Int.zero;
    }

    //Road fixer

    public void FixRoadAtPosition(Vector3Int temporaryPosition)
    {
        //[right, up, left, down]
        var result = PlacementManager.Instance.GetNeighbourTypesFor(temporaryPosition);
        int roadCount = 0;
        roadCount = result.Where(x => x == CellType.Road).Count();

        if (roadCount == 0 || roadCount == 1)
        {
            CreateDeadEnd(result, temporaryPosition);
        }
        else if (roadCount == 2)
        {
            if (CreateStraightRoad(result, temporaryPosition))
                return;
            CreateCorner(result, temporaryPosition);
        }
        else if (roadCount == 3)
        {
            Create3Way(result, temporaryPosition);
        }
        else
        {
            Create4Way(result, temporaryPosition);
        }
    }

    private void Create4Way(CellType[] result, Vector3Int temporaryPosition)
    {
        PlacementManager.Instance.ModifyStructureModel(temporaryPosition, fourWay, Quaternion.identity);
    }

    //[left, up, right, down]
    private void Create3Way(CellType[] result, Vector3Int temporaryPosition)
    {
        if (result[1] == CellType.Road && result[2] == CellType.Road && result[3] == CellType.Road)
        {
            PlacementManager.Instance.ModifyStructureModel(temporaryPosition, threeWay, Quaternion.identity);
        }
        else if (result[2] == CellType.Road && result[3] == CellType.Road && result[0] == CellType.Road)
        {
            PlacementManager.Instance.ModifyStructureModel(temporaryPosition, threeWay, Quaternion.Euler(0, 90, 0));
        }
        else if (result[3] == CellType.Road && result[0] == CellType.Road && result[1] == CellType.Road)
        {
            PlacementManager.Instance.ModifyStructureModel(temporaryPosition, threeWay, Quaternion.Euler(0, 180, 0));
        }
        else if (result[0] == CellType.Road && result[1] == CellType.Road && result[2] == CellType.Road)
        {
            PlacementManager.Instance.ModifyStructureModel(temporaryPosition, threeWay, Quaternion.Euler(0, 270, 0));
        }

    }

    //[left, up, right, down]
    private void CreateCorner(CellType[] result, Vector3Int temporaryPosition)
    {
        if (result[1] == CellType.Road && result[2] == CellType.Road)
        {
            PlacementManager.Instance.ModifyStructureModel(temporaryPosition, corner, Quaternion.Euler(0, 90, 0));
        }
        else if (result[2] == CellType.Road && result[3] == CellType.Road)
        {
            PlacementManager.Instance.ModifyStructureModel(temporaryPosition, corner, Quaternion.Euler(0, 180, 0));
        }
        else if (result[3] == CellType.Road && result[0] == CellType.Road)
        {
            PlacementManager.Instance.ModifyStructureModel(temporaryPosition, corner, Quaternion.Euler(0, 270, 0));
        }
        else if (result[0] == CellType.Road && result[1] == CellType.Road)
        {
            PlacementManager.Instance.ModifyStructureModel(temporaryPosition, corner, Quaternion.identity);
        }
    }

    //[left, up, right, down]
    private bool CreateStraightRoad(CellType[] result, Vector3Int temporaryPosition)
    {
        if (result[0] == CellType.Road && result[2] == CellType.Road)
        {
            PlacementManager.Instance.ModifyStructureModel(temporaryPosition, roadStraight, Quaternion.identity);
            return true;
        }
        else if (result[1] == CellType.Road && result[3] == CellType.Road)
        {
            PlacementManager.Instance.ModifyStructureModel(temporaryPosition, roadStraight, Quaternion.Euler(0, 90, 0));
            return true;
        }
        return false;
    }

    //[left, up, right, down]
    private void CreateDeadEnd(CellType[] result, Vector3Int temporaryPosition)
    {
        if (result[1] == CellType.Road)
        {
            PlacementManager.Instance.ModifyStructureModel(temporaryPosition, deadEnd, Quaternion.Euler(0, 270, 0));
        }
        else if (result[2] == CellType.Road)
        {
            PlacementManager.Instance.ModifyStructureModel(temporaryPosition, deadEnd, Quaternion.identity);
        }
        else if (result[3] == CellType.Road)
        {
            PlacementManager.Instance.ModifyStructureModel(temporaryPosition, deadEnd, Quaternion.Euler(0, 90, 0));
        }
        else if (result[0] == CellType.Road)
        {
            PlacementManager.Instance.ModifyStructureModel(temporaryPosition, deadEnd, Quaternion.Euler(0, 180, 0));
        }
    }
}
