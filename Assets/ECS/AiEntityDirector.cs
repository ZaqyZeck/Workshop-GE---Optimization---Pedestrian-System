using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using SimpleCity.AI.ECS;

namespace SimpleCity.AI
{
    public class AiEntityDirector : MonoBehaviour
    {
        public static AiEntityDirector Instance;
        //public PlacementManager placementManager;

        // Pedestrian prefabs now live in the subscene: see AiAgentPrefabsAuthoring.
        public GameObject carPrefab;

        [SerializeField] private int numberOfPedestrian = 100;

        [Tooltip("Distance kept from the park border when picking a random spawn point.")]
        [SerializeField, Range(0f, 0.5f)] private float parkSpawnEdgeMargin = 0.15f;

        AdjacencyGraph pedestrianGraph = new AdjacencyGraph();
        AdjacencyGraph carGraph = new AdjacencyGraph();

        // Pedestrians are entities now.
        private readonly List<Entity> spawnedPedestrians = new List<Entity>();
        private readonly List<GameObject> spawnedCars = new List<GameObject>();

        // One ECS entity per park (holds ParkData). Created lazily the first time a park is needed.
        private readonly Dictionary<StructureModel, Entity> parkEntities = new Dictionary<StructureModel, Entity>();

        private EntityQuery prefabQuery;
        private bool prefabQueryCreated;

        List<Vector3> carPath = new List<Vector3>();

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private static World EcsWorld => World.DefaultGameObjectInjectionWorld;
        private static EntityManager EM => World.DefaultGameObjectInjectionWorld.EntityManager;

        #region Pedestrians - spawning

        /// <summary>
        /// Spawns pedestrian entities at random points inside random parks. They stay in their
        /// park and wander with flocking behaviour (see AiAgentSystem).
        /// The agent type is the index of the prefab in AiAgentPrefabsAuthoring, so agents that
        /// look the same flock together and avoid the other looks.
        /// </summary>
        public void SpawnAllAagents()
        {
            RemoveAllPedestrians();

            if (EcsWorld == null || !EcsWorld.IsCreated)
            {
                Debug.LogWarning("Cannot spawn pedestrians: no ECS world.");
                return;
            }

            if (!TryGetPrefabEntities(out NativeArray<Entity> prefabs))
            {
                Debug.LogWarning("Cannot spawn pedestrians: no baked pedestrian prefabs found. " +
                                 "Add AiAgentPrefabsAuthoring to a GameObject in a subscene and wait for it to load.");
                return;
            }

            var parks = PlacementManager.Instance.GetAllParks()
                .Where(x => x != null)
                .ToList();

            if (parks.Count == 0)
            {
                prefabs.Dispose();
                Debug.LogWarning("Cannot spawn pedestrians: No parks available.");
                return;
            }

            PruneParkEntities();

            int spawnedCount = 0;

            for (int i = 0; i < numberOfPedestrian; i++)
            {
                StructureModel randomPark = parks[UnityEngine.Random.Range(0, parks.Count)];
                int agentType = UnityEngine.Random.Range(0, prefabs.Length);

                if (TrySpawningAnAgent(randomPark, agentType, prefabs[agentType]))
                {
                    spawnedCount++;
                }
            }

            prefabs.Dispose();

            Debug.Log($"Spawned {spawnedCount}/{numberOfPedestrian} pedestrians in {parks.Count} parks.");
        }

        private bool TrySpawningAnAgent(StructureModel park, int agentType, Entity prefab)
        {
            if (park == null)
                return false;

            var em = EM;

            Vector3 spawnPosition = GetRandomPointInPark(park, GetWalkingHeight(park));
            Quaternion spawnRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

            Entity agent = em.Instantiate(prefab);

            if (!em.HasComponent<AiAgentState>(agent))
            {
                em.DestroyEntity(agent);
                return false;
            }

            // Keep the prefab's scale.
            var xform = em.GetComponentData<LocalTransform>(agent);
            xform.Position = spawnPosition;
            xform.Rotation = spawnRotation;
            em.SetComponentData(agent, xform);

            AiAgentUtility.InitializeInPark(em, agent, agentType, GetOrCreateParkEntity(park));

            spawnedPedestrians.Add(agent);

            return true;
        }

        /// <summary>Reads the prefab entities baked by AiAgentPrefabsAuthoring (index == agent type).</summary>
        private bool TryGetPrefabEntities(out NativeArray<Entity> prefabs)
        {
            prefabs = default;

            if (!prefabQueryCreated)
            {
                prefabQuery = EM.CreateEntityQuery(ComponentType.ReadOnly<AgentPrefabElement>());
                prefabQueryCreated = true;
            }

            if (prefabQuery.CalculateEntityCount() == 0)
                return false;

            var buffer = prefabQuery.GetSingletonBuffer<AgentPrefabElement>(true);
            if (buffer.Length == 0)
                return false;

            prefabs = new NativeArray<Entity>(buffer.Length, Allocator.Temp);
            for (int i = 0; i < buffer.Length; i++)
                prefabs[i] = buffer[i].Prefab;

            return true;
        }

        #endregion

        #region Pedestrians - parks

        /// <summary>
        /// XZ area an agent may walk in while inside the park (Rect.y is world Z).
        /// The park's transform sits on the center of its origin cell and the
        /// footprint extends ParkWidth x ParkHeight cells in +X / +Z, each cell being 1 unit.
        /// </summary>
        public Rect GetParkBounds(StructureModel park)
        {
            Vector3 origin = park.transform.position;
            return Rect.MinMaxRect(
                origin.x - 0.5f + parkSpawnEdgeMargin,
                origin.z - 0.5f + parkSpawnEdgeMargin,
                origin.x + StructureManager.ParkWidth - 0.5f - parkSpawnEdgeMargin,
                origin.z + StructureManager.ParkHeight - 0.5f - parkSpawnEdgeMargin
            );
        }

        /// <summary>
        /// Returns a random point inside the park footprint.
        /// </summary>
        private Vector3 GetRandomPointInPark(StructureModel park, float height)
        {
            Rect bounds = GetParkBounds(park);
            return new Vector3(
                UnityEngine.Random.Range(bounds.xMin, bounds.xMax),
                height,
                UnityEngine.Random.Range(bounds.yMin, bounds.yMax)
            );
        }

        /// <summary>
        /// Pedestrians walk at the height of the sidewalk markers so that they keep
        /// the same height while wandering in a park and while walking on the road.
        /// </summary>
        private float GetWalkingHeight(StructureModel park)
        {
            var road = GetRoadAt(park.RoadPosition);
            if (road != null)
            {
                var marker = road.GetPedestrianSpawnMarker(park.transform.position);
                if (marker != null)
                    return marker.Position.y;
            }
            return park.transform.position.y;
        }

        private StructureModel GetRoadAt(Vector3Int position)
        {
            var structure = PlacementManager.Instance.GetStructureAt(position);
            return structure != null && structure.IsRoad ? structure : null;
        }

        /// <summary>Park entity for a StructureModel, created on first use.</summary>
        private Entity GetOrCreateParkEntity(StructureModel park)
        {
            var em = EM;

            if (parkEntities.TryGetValue(park, out Entity existing) && em.Exists(existing))
                return existing;

            Entity created = AiAgentUtility.CreatePark(em, park.ParkType, park.RoadPosition, GetParkBounds(park));
            parkEntities[park] = created;
            return created;
        }

        /// <summary>
        /// Call this from wherever a park is removed (e.g. PlacementManager). Agents that are
        /// still walking to it are destroyed when they arrive.
        /// </summary>
        public void NotifyParkRemoved(StructureModel park)
        {
            if (park == null || !parkEntities.TryGetValue(park, out Entity entity))
                return;

            parkEntities.Remove(park);
            AiAgentUtility.DestroyPark(EM, entity);
        }

        /// <summary>Drops entities of parks that no longer exist.</summary>
        private void PruneParkEntities()
        {
            var dead = parkEntities.Keys.Where(k => k == null).ToList();
            foreach (var key in dead)
            {
                AiAgentUtility.DestroyPark(EM, parkEntities[key]);
                parkEntities.Remove(key);
            }
        }

        /// <summary>
        /// Sends the agent along the sidewalks to a park whose type (Green / Red / Yellow)
        /// differs from the type of the park it is currently in.
        /// </summary>
        public bool TrySendAgentToDifferentPark(Entity agent)
        {
            var em = EM;

            if (!em.Exists(agent) || !em.HasComponent<AiAgentState>(agent))
                return false;

            var state = em.GetComponentData<AiAgentState>(agent);
            if (state.State != AgentStateType.Wandering)
                return false;

            ParkType currentType = state.CurrentParkType;

            var candidates = PlacementManager.Instance.GetAllParks()
                .Where(park => park != null && park.ParkType != currentType)
                .ToList();

            // Try random candidates until one is reachable over the road network.
            while (candidates.Count > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, candidates.Count);
                StructureModel targetPark = candidates[randomIndex];

                if (TryGetPathToPark(agent, targetPark, out List<Vector3> path))
                {
                    return AiAgentUtility.StartTravel(em, agent, GetOrCreateParkEntity(targetPark), path);
                }

                candidates.RemoveAt(randomIndex);
            }

            return false;
        }

        /// <summary>
        /// Sends every wandering pedestrian to a park of a different type.
        /// </summary>
        public void SendAllAgentsToDifferentParks()
        {
            int sentCount = 0;
            int agentCount = 0;

            var em = EM;

            foreach (Entity pedestrian in spawnedPedestrians)
            {
                // Agents can be destroyed by the ECS side (target park removed).
                if (!em.Exists(pedestrian))
                    continue;

                agentCount++;
                if (TrySendAgentToDifferentPark(pedestrian))
                    sentCount++;
            }

            Debug.Log($"Sent {sentCount}/{agentCount} pedestrians to a park of a different type.");
        }

        /// <summary>
        /// Path: agent position -> nearest sidewalk marker of the current park's road ->
        /// sidewalks -> marker nearest to the destination -> random point inside the target park.
        /// </summary>
        private bool TryGetPathToPark(Entity agent, StructureModel targetPark, out List<Vector3> agentPath)
        {
            agentPath = null;

            var em = EM;
            var state = em.GetComponentData<AiAgentState>(agent);

            var p = state.CurrentParkRoadPosition;
            Vector3Int startRoadPosition = new Vector3Int(p.x, p.y, p.z);
            Vector3Int endRoadPosition = targetPark.RoadPosition;

            var startRoad = GetRoadAt(startRoadPosition);
            var endRoad = GetRoadAt(endRoadPosition);

            if (startRoad == null || endRoad == null)
                return false;

            Vector3 agentPosition = em.GetComponentData<LocalTransform>(agent).Position;
            Vector3 destination = GetRandomPointInPark(targetPark, agentPosition.y);

            var startMarker = startRoad.GetPedestrianSpawnMarker(agentPosition);
            var endMarker = endRoad.GetPedestrianSpawnMarker(destination);

            if (startMarker == null || endMarker == null)
                return false;

            var roadPath = PlacementManager.Instance.GetPathBetween(startRoadPosition, endRoadPosition, true);

            if (roadPath == null || roadPath.Count == 0)
                return false;

            roadPath.Reverse();

            List<Vector3> markerPath = GetPedestrianPath(
                roadPath,
                startMarker.Position,
                endMarker.Position
            );

            if (markerPath == null || markerPath.Count == 0)
                return false;

            // Finish the route at a random point inside the target park.
            markerPath.Add(destination);

            agentPath = markerPath;
            return true;
        }

        #endregion

        public void SpawnACar()
        {
            foreach (var house in PlacementManager.Instance.GetAllHouses())
            {
                TrySpawninACar(house, PlacementManager.Instance.GetRandomSpecialStrucutre());
            }
        }

        private void TrySpawninACar(StructureModel startStructure, StructureModel endStructure)
        {
            if (startStructure == null || endStructure == null)
                return;

            var startRoadPosition = ((INeedingRoad)startStructure).RoadPosition;
            var endRoadPosition = ((INeedingRoad)endStructure).RoadPosition;

            var path = PlacementManager.Instance.GetPathBetween(
                startRoadPosition,
                endRoadPosition,
                true
            );

            if (path == null || path.Count < 3)
                return;

            path.Reverse();

            var startRoad = PlacementManager.Instance.GetStructureAt(startRoadPosition);
            var endRoad = PlacementManager.Instance.GetStructureAt(endRoadPosition);

            if (startRoad == null || endRoad == null)
                return;

            var startMarkerPosition = startRoad.GetCarSpawnMarker(path[1]);

            var endMarkerPosition = endRoad.GetCarEndMarker(path[path.Count - 2]);

            carPath = GetCarPath(
                path,
                startMarkerPosition.Position,
                endMarkerPosition.Position
            );

            if (carPath == null || carPath.Count == 0)
                return;

            GameObject car = Instantiate(
                carPrefab,
                startMarkerPosition.Position,
                Quaternion.identity
            );

            var carAI = car.GetComponent<CarAI>();

            if (carAI == null)
            {
                Destroy(car);
                return;
            }

            carAI.SetPath(carPath);

            spawnedCars.Add(car);
        }

        private List<Vector3> GetPedestrianPath(List<Vector3Int> path, Vector3 startPosition, Vector3 endPosition)
        {
            pedestrianGraph.ClearGraph();
            CreatAPedestrianGraph(path);
            return AdjacencyGraph.AStarSearch(pedestrianGraph, startPosition, endPosition);
        }


        private void CreatAPedestrianGraph(List<Vector3Int> path)
        {
            Dictionary<Marker, Vector3> tempDictionary = new Dictionary<Marker, Vector3>();

            for (int i = 0; i < path.Count; i++)
            {
                var currentPosition = path[i];
                var roadStructure = PlacementManager.Instance.GetStructureAt(currentPosition);
                var markersList = roadStructure.GetPedestrianMarkers();
                bool limitDistance = markersList.Count == 4;
                tempDictionary.Clear();
                foreach (var marker in markersList)
                {
                    pedestrianGraph.AddVertex(marker.Position);
                    foreach (var markerNeighbourPosition in marker.GetAdjacentPositions())
                    {
                        pedestrianGraph.AddEdge(marker.Position, markerNeighbourPosition);
                    }

                    if (marker.OpenForconnections && i + 1 < path.Count)
                    {
                        var nextRoadStructure = PlacementManager.Instance.GetStructureAt(path[i + 1]);
                        if (limitDistance)
                        {
                            tempDictionary.Add(marker, nextRoadStructure.GetNearestPedestrianMarkerTo(marker.Position));
                        }
                        else
                        {
                            pedestrianGraph.AddEdge(marker.Position, nextRoadStructure.GetNearestPedestrianMarkerTo(marker.Position));
                        }
                    }
                }
                if (limitDistance && tempDictionary.Count == 4)
                {
                    var distanceSortedMarkers = tempDictionary.OrderBy(x => Vector3.Distance(x.Key.Position, x.Value)).ToList();
                    for (int j = 0; j < 2; j++)
                    {
                        pedestrianGraph.AddEdge(distanceSortedMarkers[j].Key.Position, distanceSortedMarkers[j].Value);
                    }
                }
            }
        }

        private List<Vector3> GetCarPath(List<Vector3Int> path, Vector3 startPosition, Vector3 endPosition)
        {
            carGraph.ClearGraph();
            CreatACarGraph(path);
            Debug.Log(carGraph);
            return AdjacencyGraph.AStarSearch(carGraph, startPosition, endPosition);
        }

        private void CreatACarGraph(List<Vector3Int> path)
        {
            Dictionary<Marker, Vector3> tempDictionary = new Dictionary<Marker, Vector3>();
            for (int i = 0; i < path.Count; i++)
            {
                var currentPosition = path[i];
                var roadStructure = PlacementManager.Instance.GetStructureAt(currentPosition);
                var markersList = roadStructure.GetCarMarkers();
                var limitDistance = markersList.Count > 3;
                tempDictionary.Clear();

                foreach (var marker in markersList)
                {
                    carGraph.AddVertex(marker.Position);
                    foreach (var markerNeighbour in marker.adjacentMarkers)
                    {
                        carGraph.AddEdge(marker.Position, markerNeighbour.Position);
                    }
                    if (marker.OpenForconnections && i + 1 < path.Count)
                    {
                        var nextRoadPosition = PlacementManager.Instance.GetStructureAt(path[i + 1]);
                        if (limitDistance)
                        {
                            tempDictionary.Add(marker, nextRoadPosition.GetNearestCarMarkerTo(marker.Position));
                        }
                        else
                        {
                            carGraph.AddEdge(marker.Position, nextRoadPosition.GetNearestCarMarkerTo(marker.Position));
                        }
                    }
                }
                if (limitDistance && tempDictionary.Count > 2)
                {
                    var distanceSortedMarkers = tempDictionary.OrderBy(x => Vector3.Distance(x.Key.Position, x.Value)).ToList();
                    foreach (var item in distanceSortedMarkers)
                    {
                        Debug.Log(Vector3.Distance(item.Key.Position, item.Value));
                    }
                    for (int j = 0; j < 2; j++)
                    {
                        carGraph.AddEdge(distanceSortedMarkers[j].Key.Position, distanceSortedMarkers[j].Value);
                    }
                }
            }
        }

        public void RemoveAllPedestrians()
        {
            if (EcsWorld != null && EcsWorld.IsCreated)
            {
                var em = EM;
                var alive = new NativeList<Entity>(spawnedPedestrians.Count, Allocator.Temp);
                foreach (Entity pedestrian in spawnedPedestrians)
                {
                    if (em.Exists(pedestrian))
                        alive.Add(pedestrian);
                }
                em.DestroyEntity(alive.AsArray());
                alive.Dispose();
            }

            spawnedPedestrians.Clear();

            Debug.Log("All pedestrians removed.");
        }

        public void RemoveAllCars()
        {
            foreach (GameObject car in spawnedCars)
            {
                if (car != null)
                    Destroy(car);
            }

            spawnedCars.Clear();
            carPath.Clear();

            Debug.Log("All cars removed.");
        }

        private void Update()
        {
            //DrawGraph(carGraph);
            for (int i = 1; i < carPath.Count; i++)
            {
                Debug.DrawLine(carPath[i - 1] + Vector3.up, carPath[i] + Vector3.up, Color.magenta);
            }
        }

        private void DrawGraph(AdjacencyGraph graph)
        {
            foreach (var vertex in graph.GetVertices())
            {
                foreach (var vertexNeighbour in graph.GetConnectedVerticesTo(vertex))
                {
                    Debug.DrawLine(vertex.Position + Vector3.up, vertexNeighbour.Position + Vector3.up, Color.red);
                }
            }
        }


    }
}