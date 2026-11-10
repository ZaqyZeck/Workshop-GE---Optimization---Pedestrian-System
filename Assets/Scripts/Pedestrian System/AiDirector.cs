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

        AdjacencyGraph pedestrianGraph = new AdjacencyGraph();
        AdjacencyGraph carGraph = new AdjacencyGraph();

        private readonly List<GameObject> spawnedPedestrians = new List<GameObject>();
        private readonly List<GameObject> spawnedCars = new List<GameObject>();

        List<Vector3> carPath = new List<Vector3>();

        private void Awake()
        {
            Instance = this;
        }

        public void SpawnAllAagents()
        {
            RemoveAllPedestrians();

            var houses = PlacementManager.Instance.GetAllHouses().ToList();
            var specialStructures = PlacementManager.Instance.GetAllSpecialStructures().ToList();

            if (houses.Count == 0 || specialStructures.Count == 0)
            {
                Debug.LogWarning("Cannot spawn pedestrians: No houses or special structures available.");
                return;
            }

            int spawnedCount = 0;

            StructureModel validStart = null;
            StructureModel validEnd = null;

            // Try different house-special structure combinations first.
            var combinations = new List<(StructureModel start, StructureModel end)>();

            foreach (var house in houses)
            {
                foreach (var special in specialStructures)
                {
                    combinations.Add((house, special));
                }
            }

            // Shuffle combinations.
            for (int i = combinations.Count - 1; i > 0; i--)
            {
                int randomIndex = UnityEngine.Random.Range(0, i + 1);
                (combinations[i], combinations[randomIndex]) =
                    (combinations[randomIndex], combinations[i]);
            }

            // Try spawning using different combinations.
            foreach (var combination in combinations)
            {
                if (spawnedCount >= numberOfPedestrian) break;

                if (TrySpawningAnAgent(combination.start, combination.end))
                {
                    spawnedCount++;

                    // Remember a valid route for additional pedestrians.
                    validStart = combination.start;
                    validEnd = combination.end;
                }
            }

            // Reuse a valid route if the requested number has not been reached.
            if (validStart != null && validEnd != null)
            {
                while (spawnedCount < numberOfPedestrian)
                {
                    if (TrySpawningAnAgent(validStart, validEnd))
                    {
                        spawnedCount++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            Debug.Log($"Spawned {spawnedCount}/{numberOfPedestrian} pedestrians.");
        }

        private bool TrySpawningAnAgent(StructureModel startStructure, StructureModel endStructure)
        {
            if (startStructure == null || endStructure == null)
                return false;

            var startPosition = ((INeedingRoad)startStructure).RoadPosition;
            var endPosition = ((INeedingRoad)endStructure).RoadPosition;

            var startRoad = PlacementManager.Instance.GetStructureAt(startPosition);
            var endRoad = PlacementManager.Instance.GetStructureAt(endPosition);

            if (startRoad == null || endRoad == null)
                return false;

            var startMarkerPosition = startRoad.GetPedestrianSpawnMarker(startStructure.transform.position);
            var endMarkerPosition = endRoad.GetNearestPedestrianMarkerTo(endStructure.transform.position);

            var path = PlacementManager.Instance.GetPathBetween(startPosition, endPosition, true);

            if (path == null || path.Count == 0)
                return false;

            path.Reverse();

            List<Vector3> agentPath = GetPedestrianPath(
                path,
                startMarkerPosition.Position,
                endMarkerPosition
            );

            if (agentPath == null || agentPath.Count == 0)
                return false;

            GameObject agent = Instantiate(
                GetRandomPedestrian(),
                startMarkerPosition.Position,
                Quaternion.identity
            );

            var aiAgent = agent.GetComponent<AiAgent>();

            if (aiAgent == null)
            {
                Destroy(agent);
                return false;
            }

            aiAgent.Initialize(agentPath);

            spawnedPedestrians.Add(agent);

            return true;
        }

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
            Debug.Log(pedestrianGraph);
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

        private GameObject GetRandomPedestrian()
        {
            return pedestrianPrefabs[UnityEngine.Random.Range(0, pedestrianPrefabs.Length)];
        }

        public void RemoveAllPedestrians()
        {
            foreach (GameObject pedestrian in spawnedPedestrians)
            {
                if (pedestrian != null)
                    Destroy(pedestrian);
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

