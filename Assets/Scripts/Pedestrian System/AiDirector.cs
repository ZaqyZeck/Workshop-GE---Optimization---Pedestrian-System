using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SimpleCity.AI
{
    public class AiDirector : MonoBehaviour
    {
        public static AiDirector Instance;
        //public PlacementManager placementManager;
        public GameObject[] pedestrianPrefabs;

        public GameObject carPrefab;

        [SerializeField] private int numberOfPedestrian = 100;

        [Tooltip("Distance kept from the park border when picking a random spawn point.")]
        [SerializeField, Range(0f, 0.5f)] private float parkSpawnEdgeMargin = 0.15f;

        AdjacencyGraph pedestrianGraph = new AdjacencyGraph();
        AdjacencyGraph carGraph = new AdjacencyGraph();

        private readonly List<GameObject> spawnedPedestrians = new List<GameObject>();
        private readonly List<GameObject> spawnedCars = new List<GameObject>();

        // Which agents are wandering in which park (neighbour lists for flocking).
        private readonly Dictionary<StructureModel, List<AiAgent>> agentsByPark = new Dictionary<StructureModel, List<AiAgent>>();
        private readonly Dictionary<AiAgent, StructureModel> parkByAgent = new Dictionary<AiAgent, StructureModel>();
        private static readonly List<AiAgent> EmptyAgentList = new List<AiAgent>();

        List<Vector3> carPath = new List<Vector3>();

        private void Awake()
        {
            Instance = this;
        }

        #region Pedestrians - spawning

        /// <summary>
        /// Spawns pedestrians at random points inside random parks. They stay in their
        /// park and wander around with flocking behaviour (see AiAgent).
        /// The agent type is the index of the pedestrian prefab, so agents that look
        /// the same flock together and avoid the other looks.
        /// </summary>
        public void SpawnAllAagents()
        {
            RemoveAllPedestrians();

            if (pedestrianPrefabs == null || pedestrianPrefabs.Length == 0)
            {
                Debug.LogWarning("Cannot spawn pedestrians: No pedestrian prefabs assigned.");
                return;
            }

            var parks = PlacementManager.Instance.GetAllParks()
                .Where(x => x != null)
                .ToList();

            if (parks.Count == 0)
            {
                Debug.LogWarning("Cannot spawn pedestrians: No parks available.");
                return;
            }

            int spawnedCount = 0;

            for (int i = 0; i < numberOfPedestrian; i++)
            {
                StructureModel randomPark = parks[UnityEngine.Random.Range(0, parks.Count)];
                int agentType = UnityEngine.Random.Range(0, pedestrianPrefabs.Length);

                if (TrySpawningAnAgent(randomPark, agentType))
                {
                    spawnedCount++;
                }
            }

            Debug.Log($"Spawned {spawnedCount}/{numberOfPedestrian} pedestrians in {parks.Count} parks.");
        }

        private bool TrySpawningAnAgent(StructureModel park, int agentType)
        {
            if (park == null)
                return false;

            Vector3 spawnPosition = GetRandomPointInPark(park, GetWalkingHeight(park));
            Quaternion spawnRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

            GameObject agent = Instantiate(
                pedestrianPrefabs[agentType],
                spawnPosition,
                spawnRotation
            );

            var aiAgent = agent.GetComponent<AiAgent>();

            if (aiAgent == null)
            {
                Destroy(agent);
                return false;
            }

            aiAgent.InitializeInPark(agentType, park);

            spawnedPedestrians.Add(agent);

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

        public void RegisterAgentInPark(AiAgent agent, StructureModel park)
        {
            UnregisterAgent(agent);

            if (!agentsByPark.TryGetValue(park, out var agents))
            {
                agents = new List<AiAgent>();
                agentsByPark.Add(park, agents);
            }

            agents.Add(agent);
            parkByAgent[agent] = park;
        }

        public void UnregisterAgent(AiAgent agent)
        {
            if (!parkByAgent.TryGetValue(agent, out var park))
                return;

            parkByAgent.Remove(agent);

            if (agentsByPark.TryGetValue(park, out var agents))
            {
                agents.Remove(agent);
                if (agents.Count == 0)
                    agentsByPark.Remove(park);
            }
        }

        /// <summary>
        /// All agents that are currently wandering in the same park as the given agent
        /// (including the agent itself). Used as the neighbour list for flocking.
        /// </summary>
        public IReadOnlyList<AiAgent> GetParkMates(AiAgent agent)
        {
            if (parkByAgent.TryGetValue(agent, out var park) && agentsByPark.TryGetValue(park, out var agents))
                return agents;

            return EmptyAgentList;
        }

        /// <summary>
        /// Sends the agent along the sidewalks to a park whose type (Green / Red / Yellow)
        /// differs from the type of the park it is currently in.
        /// </summary>
        public bool TrySendAgentToDifferentPark(AiAgent agent)
        {
            if (agent == null || agent.State != AiAgent.AgentState.Wandering)
                return false;

            ParkType currentType = agent.CurrentParkType;

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
                    agent.StartTravel(targetPark, path);
                    return true;
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

            foreach (GameObject pedestrian in spawnedPedestrians)
            {
                if (pedestrian == null)
                    continue;

                var aiAgent = pedestrian.GetComponent<AiAgent>();
                if (aiAgent == null)
                    continue;

                agentCount++;
                if (aiAgent.MovingToDifferentPark())
                    sentCount++;
            }

            Debug.Log($"Sent {sentCount}/{agentCount} pedestrians to a park of a different type.");
        }

        /// <summary>
        /// Path: agent position -> nearest sidewalk marker of the current park's road ->
        /// sidewalks -> marker nearest to the destination -> random point inside the target park.
        /// </summary>
        private bool TryGetPathToPark(AiAgent agent, StructureModel targetPark, out List<Vector3> agentPath)
        {
            agentPath = null;

            Vector3Int startRoadPosition = agent.CurrentParkRoadPosition;
            Vector3Int endRoadPosition = targetPark.RoadPosition;

            var startRoad = GetRoadAt(startRoadPosition);
            var endRoad = GetRoadAt(endRoadPosition);

            if (startRoad == null || endRoad == null)
                return false;

            Vector3 agentPosition = agent.transform.position;
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
            return AdjacencyGraph.AStarSearch(pedestrianGraph,startPosition,endPosition);
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

                    if(marker.OpenForconnections && i+1 < path.Count)
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
                if(limitDistance && tempDictionary.Count == 4)
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
                    if(marker.OpenForconnections && i + 1 < path.Count)
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
            foreach (GameObject pedestrian in spawnedPedestrians)
            {
                if (pedestrian != null)
                    Destroy(pedestrian);
            }

            spawnedPedestrians.Clear();
            agentsByPark.Clear();
            parkByAgent.Clear();

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

