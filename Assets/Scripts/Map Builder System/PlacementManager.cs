using System.Collections.Generic;
using UnityEngine;

public class PlacementManager : MonoBehaviour
{
   public static PlacementManager Instance;

    public int width, height;
    Grid placementGrid;

    private Dictionary<Vector3Int, StructureModel> temporaryRoadobjects = new Dictionary<Vector3Int, StructureModel>();
    private Dictionary<Vector3Int, StructureModel> structureDictionary = new Dictionary<Vector3Int, StructureModel>();

    [SerializeField] private List<StructureSaveData> structuresLoadedData;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        placementGrid = new Grid(width, height);

        // Load saved structures if a save file exists
        if (SaveSystem.HasSaveFile())
            LoadStructureData();
    }

    private void OnEnable()
    {
        GameEvent.OnSaveDataStructure += SaveStructureData;
        GameEvent.OnDeleteDataStructure += DeleteStructureSaveData;
        //GameEvent.OnLoadDataStructure += LoadStructureData;
    }

    private void OnDisable()
    {
        GameEvent.OnSaveDataStructure -= SaveStructureData;
        GameEvent.OnDeleteDataStructure -= DeleteStructureSaveData;
        //GameEvent.OnLoadDataStructure -= LoadStructureData;
    }

    internal CellType[] GetNeighbourTypesFor(Vector3Int position)
    {
        return placementGrid.GetAllAdjacentCellTypes(position.x, position.z);
    }

    internal bool CheckIfPositionInBound(Vector3Int position)
    {
        if (position.x >= 0 && position.x < width && position.z >= 0 && position.z < height)
        {
            return true;
        }
        return false;
    }

    internal void PlaceObjectOnTheMap(Vector3Int position, GameObject structurePrefab, CellType type, int id, int width = 1, int height = 1)
    {
        StructureModel structure = CreateANewStructureModel(position, structurePrefab, type, id);

        var structureNeedingRoad = structure.GetComponent<INeedingRoad>();
        if (structureNeedingRoad != null)
        {
            structureNeedingRoad.RoadPosition = GetNearestRoad(position, width, height).Value;
            Debug.Log("My nearest road position is: " + structureNeedingRoad.RoadPosition);
        }

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                var newPosition = position + new Vector3Int(x, 0, z);
                placementGrid[newPosition.x, newPosition.z] = type;
                structureDictionary.Add(newPosition, structure);
                DestroyNatureAt(newPosition);
            }
        }

    }

    private Vector3Int? GetNearestRoad(Vector3Int position, int width, int height)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var newPosition = position + new Vector3Int(x, 0, y);
                var roads = GetNeighboursOfTypeFor(newPosition, CellType.Road);
                if (roads.Count > 0)
                {
                    return roads[0];
                }
            }
        }
        return null;
    }

    private void DestroyNatureAt(Vector3Int position)
    {
        RaycastHit[] hits = Physics.BoxCastAll(position + new Vector3(0, 0.5f, 0), new Vector3(0.5f, 0.5f, 0.5f), transform.up, Quaternion.identity, 1f, 1 << LayerMask.NameToLayer("Nature"));
        foreach (var item in hits)
        {
            Destroy(item.collider.gameObject);
        }
    }

    internal bool CheckIfPositionIsFree(Vector3Int position)
    {
        return CheckIfPositionIsOfType(position, CellType.Empty);
    }

    private bool CheckIfPositionIsOfType(Vector3Int position, CellType type)
    {
        return placementGrid[position.x, position.z] == type;
    }

    internal void PlaceTemporaryStructure(Vector3Int position, GameObject structurePrefab, CellType type)
    {
        placementGrid[position.x, position.z] = type;
        StructureModel structure = CreateANewStructureModel(position, structurePrefab, type, 0);
        temporaryRoadobjects.Add(position, structure);
    }

    internal List<Vector3Int> GetNeighboursOfTypeFor(Vector3Int position, CellType type)
    {
        var neighbourVertices = placementGrid.GetAdjacentCellsOfType(position.x, position.z, type);
        List<Vector3Int> neighbours = new List<Vector3Int>();
        foreach (var point in neighbourVertices)
        {
            neighbours.Add(new Vector3Int(point.X, 0, point.Y));
        }
        return neighbours;
    }

    private StructureModel CreateANewStructureModel(Vector3Int position, GameObject structurePrefab, CellType type, int id)
    {
        GameObject structure = new GameObject(type.ToString());
        structure.transform.SetParent(transform);
        structure.transform.localPosition = position;

        StructureModel structureModel = structure.AddComponent<StructureModel>();
        structureModel.CreateModel(structurePrefab, id);
        return structureModel;
    }

    internal List<Vector3Int> GetPathBetween(Vector3Int startPosition, Vector3Int endPosition, bool isAgent = false)
    {
        var resultPath = GridSearch.AStarSearch(placementGrid, new Point(startPosition.x, startPosition.z), new Point(endPosition.x, endPosition.z), isAgent);
        List<Vector3Int> path = new List<Vector3Int>();
        foreach (Point point in resultPath)
        {
            path.Add(new Vector3Int(point.X, 0, point.Y));
        }
        return path;
    }

    internal void RemoveAllTemporaryStructures()
    {
        foreach (var structure in temporaryRoadobjects.Values)
        {
            var position = Vector3Int.RoundToInt(structure.transform.position);
            placementGrid[position.x, position.z] = CellType.Empty;
            Destroy(structure.gameObject);
        }
        temporaryRoadobjects.Clear();
    }

    public void RemoveStructureAt(Vector3Int position)
    {
        if (placementGrid[position.x, position.z] == CellType.Empty)
        {
            Debug.Log("already empty");
            return;
        }
        if (!structureDictionary.ContainsKey(position))
        {
            Debug.Log("dictionary doesn't have position");
            return;
        }

        StructureModel removeModel = GetStructureAt(position);

        // Collect ALL dictionary keys that share this StructureModel instance.
        // This handles multi-cell structures (e.g. 2x2 parks) where multiple
        // grid positions all point to the same StructureModel object.
        List<Vector3Int> positionsToRemove = new List<Vector3Int>();
        foreach (var kvp in structureDictionary)
        {
            if (kvp.Value == removeModel)
                positionsToRemove.Add(kvp.Key);
        }

        // Clear every occupied cell and remove from dictionary
        foreach (var pos in positionsToRemove)
        {
            placementGrid[pos.x, pos.z] = CellType.Empty;
            structureDictionary.Remove(pos);
        }

        Destroy(removeModel.gameObject);
    }

    internal void AddtemporaryStructuresToStructureDictionary()
    {
        foreach (var structure in temporaryRoadobjects)
        {
            structureDictionary.Add(structure.Key, structure.Value);
            DestroyNatureAt(structure.Key);
        }
        temporaryRoadobjects.Clear();
    }

    public void ModifyStructureModel(Vector3Int position, GameObject newModel, Quaternion rotation)
    {
        if (temporaryRoadobjects.ContainsKey(position))
            temporaryRoadobjects[position].SwapModel(newModel, rotation);
        else if (structureDictionary.ContainsKey(position))
            structureDictionary[position].SwapModel(newModel, rotation);
    }

    public StructureModel GetRandomRoad()
    {
        var point = placementGrid.GetRandomRoadPoint();
        return GetStructureAt(point);
    }

    public StructureModel GetRandomSpecialStrucutre()
    {
        var point = placementGrid.GetRandomSpecialStructurePoint();
        return GetStructureAt(point);
    }

    public StructureModel GetRandomHouseStructure()
    {
        var point = placementGrid.GetRandomHouseStructurePoint();
        return GetStructureAt(point);
    }

    public List<StructureModel> GetAllHouses()
    {
        List<StructureModel> returnList = new List<StructureModel>();
        var housePositions = placementGrid.GetAllHouses();
        foreach (var point in housePositions)
        {
            returnList.Add(structureDictionary[new Vector3Int(point.X, 0, point.Y)]);
        }
        return returnList;
    }

    internal List<StructureModel> GetAllSpecialStructures()
    {
        List<StructureModel> returnList = new List<StructureModel>();
        var housePositions = placementGrid.GetAllSpecialStructure();
        foreach (var point in housePositions)
        {
            returnList.Add(structureDictionary[new Vector3Int(point.X, 0, point.Y)]);
        }
        return returnList;
    }


    private StructureModel GetStructureAt(Point point)
    {
        if (point != null)
        {
            return structureDictionary[new Vector3Int(point.X, 0, point.Y)];
        }
        return null;
    }

    public StructureModel GetStructureAt(Vector3Int position)
    {
        if (structureDictionary.ContainsKey(position))
        {
            return structureDictionary[position];
        }
        return null;
    }

    private void SaveStructureData()
    {
        GameSaveData gameSaveData = new GameSaveData();

        foreach (KeyValuePair<Vector3Int, StructureModel> structure in structureDictionary)
        {
            Vector3Int position = structure.Key;
            StructureModel model = structure.Value;

            // Save position and structure ID here\
            

            StructureSaveData data = new StructureSaveData(position, model.RoadPosition, model.structureId);
            gameSaveData.structures.Add(data);
        }

        SaveSystem.Save(gameSaveData.structures);
    }

    private void DeleteStructureSaveData()
    {
        SaveSystem.DeleteSave();
    }

    private void LoadStructureData()
    {
        GameSaveData saveData = SaveSystem.Load();
        if (saveData == null || saveData.structures == null || saveData.structures.Count == 0)
        {
            Debug.LogWarning("LoadStructureData: No save data found.");
            return;
        }

        structuresLoadedData = saveData.structures;

        // --- Pass 1: Separate roads from non-road structures ---
        // structureId encoding (from StructureModel comments):
        //   0xx = Road
        //   1xx = House       (prefab index = id % 100)
        //   2xx = Special     (prefab index = id % 100)
        //   3xx = Park (2x2)  (prefab index = id % 100)
        List<StructureSaveData> roads = new List<StructureSaveData>();
        List<StructureSaveData> nonRoads = new List<StructureSaveData>();

        foreach (var data in saveData.structures)
        {
            if (data.structureId / 100 == 0)
                roads.Add(data);
            else
                nonRoads.Add(data);
        }

        // --- Pass 2: Place all road tiles into the grid first ---
        // We use deadEnd as the placeholder; FixRoadAtPosition will swap to the correct prefab afterwards.
        foreach (var data in roads)
        {
            if (!CheckIfPositionInBound(data.position)) continue;
            if (!CheckIfPositionIsFree(data.position)) continue;

            placementGrid[data.position.x, data.position.z] = CellType.Road;
            StructureModel structure = CreateANewStructureModel(data.position, RoadManager.Instance.deadEnd, CellType.Road, data.structureId);
            structureDictionary.Add(data.position, structure);
            DestroyNatureAt(data.position);
        }

        // --- Pass 3: Fix every road tile and its road neighbours now that all roads are in the grid ---
        foreach (var data in roads)
        {
            RoadManager.Instance.FixRoadAtPosition(data.position);

            var neighbours = GetNeighboursOfTypeFor(data.position, CellType.Road);
            foreach (var neighbour in neighbours)
                RoadManager.Instance.FixRoadAtPosition(neighbour);
        }

        // --- Pass 4: Place non-road structures (houses, specials, parks) ---
        foreach (var data in nonRoads)
        {
            if (!CheckIfPositionInBound(data.position)) continue;
            if (!CheckIfPositionIsFree(data.position)) continue;

            int typePrefix  = data.structureId / 100;
            int prefabIndex = data.structureId % 100;

            switch (typePrefix)
            {
                case 1: // House
                {
                    var prefabs = StructureManager.Instance.housesPrefabe;
                    if (prefabIndex >= prefabs.Length) break;
                    PlaceObjectOnTheMap(data.position, prefabs[prefabIndex].prefab, CellType.Structure, data.structureId);
                    break;
                }
                case 2: // Special structure
                {
                    var prefabs = StructureManager.Instance.specialPrefabs;
                    if (prefabIndex >= prefabs.Length) break;
                    PlaceObjectOnTheMap(data.position, prefabs[prefabIndex].prefab, CellType.SpecialStructure, data.structureId);
                    break;
                }
                case 3: // Park structure (2x2 footprint)
                {
                    var prefabs = StructureManager.Instance.parkStructuresPrefabs;
                    if (prefabIndex >= prefabs.Length) break;
                    PlaceObjectOnTheMap(data.position, prefabs[prefabIndex].prefab, CellType.Structure, data.structureId, 2, 2);
                    break;
                }
                default:
                    Debug.LogWarning($"LoadStructureData: Unknown type prefix {typePrefix} for structureId {data.structureId}");
                    break;
            }
        }

        Debug.Log($"LoadStructureData: Loaded {saveData.structures.Count} structures successfully.");
    }
}
