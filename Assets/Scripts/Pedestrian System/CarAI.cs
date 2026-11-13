using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

public class CarAI : MonoBehaviour
{
    [SerializeField]
    private List<Vector3> path = null;
    [SerializeField]
    private float arriveDistance = .3f, lastPointArriveDistance = .1f;
    [SerializeField]
    private float turningAngleOffset = 5;
    [SerializeField]
    private Vector3 currentTargetPosition;

    [SerializeField]
    private GameObject raycastStartingPoint = null;
    [SerializeField]
    private float collisionRaycastLength = 0.1f;

    internal bool IsThisLastPathIndex()
    {
        return index >= path.Count - 1;
    }

    private int index = 0;

    private bool stop;
    private bool collisionStop = false;

    public bool Stop
    {
        get { return stop || collisionStop; }
        set { stop = value; }
    }

    [field: SerializeField]
    public UnityEvent<Vector2> OnDrive { get; set; }

    // =========================================================================
    // JOB SYSTEM BATCHING (No ECS - Pure MonoBehaviour + IJobParallelFor)
    // =========================================================================

    private static readonly List<CarAI> ActiveCars = new List<CarAI>();
    private static int LastProcessedFrame = -1;

    /// <summary>
    /// Toggleable in code or via inspector on any CarAI to compare Job System vs regular Update in the Profiler.
    /// </summary>
    [Header("Workshop Optimization")]
    [SerializeField]
    private bool useJobSystem = true;

    [BurstCompile]
    private struct CarSteeringAndArrivalJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<quaternion> Rotations;
        [ReadOnly] public NativeArray<float3> TargetPositions;
        [ReadOnly] public NativeArray<float> ArriveDistances;
        [ReadOnly] public NativeArray<float> TurningAngleOffsets;
        [ReadOnly] public NativeArray<byte> IsStopped;

        [WriteOnly] public NativeArray<float2> DriveOutputs;
        [WriteOnly] public NativeArray<byte> ReachedTargetOutputs;

        public void Execute(int i)
        {
            if (IsStopped[i] != 0)
            {
                DriveOutputs[i] = float2.zero;
                ReachedTargetOutputs[i] = 0;
                return;
            }

            float3 pos = Positions[i];
            float3 target = TargetPositions[i];

            // 1. Check if the car arrived at its current waypoint
            float distSq = math.distancesq(target, pos);
            float arriveDist = ArriveDistances[i];
            ReachedTargetOutputs[i] = (distSq < arriveDist * arriveDist) ? (byte)1 : (byte)0;

            // 2. Compute local target point (equivalent to transform.InverseTransformPoint(currentTargetPosition))
            float3 localTarget = math.rotate(math.inverse(Rotations[i]), target - pos);
            float angle = math.degrees(math.atan2(localTarget.x, localTarget.z));

            float offset = TurningAngleOffsets[i];
            float rotateCar = 0f;
            if (angle > offset)
            {
                rotateCar = 1f;
            }
            else if (angle < -offset)
            {
                rotateCar = -1f;
            }

            DriveOutputs[i] = new float2(rotateCar, 1f);
        }
    }

    private void OnEnable()
    {
        if (!ActiveCars.Contains(this))
            ActiveCars.Add(this);
    }

    private void OnDisable()
    {
        ActiveCars.Remove(this);
    }

    private void Start()
    {
        if (path == null || path.Count == 0)
        {
            Stop = true;
        }
        else
        {
            currentTargetPosition = path[index];
        }
    }

    public void SetPath(List<Vector3> path)
    {
        if (path.Count == 0)
        {
            Destroy(gameObject);
            return;
        }
        this.path = path;
        index = 0;
        currentTargetPosition = this.path[index];

        Vector3 relativepoint = transform.InverseTransformPoint(this.path[index + 1]);

        float angle = Mathf.Atan2(relativepoint.x, relativepoint.z) * Mathf.Rad2Deg;

        transform.rotation = Quaternion.Euler(0, angle, 0);
        Stop = false;
    }

    private void Update()
    {
        if (!useJobSystem)
        {
            CheckIfArrived();
            Drive();
            CheckForCollisions();
            return;
        }

        // Run a single batched Job + RaycastCommand pass once per frame for all active cars
        if (LastProcessedFrame != Time.frameCount)
        {
            LastProcessedFrame = Time.frameCount;
            ProcessAllCarsWithJobSystem();
        }
    }

    private static void ProcessAllCarsWithJobSystem()
    {
        int count = ActiveCars.Count;
        if (count == 0)
            return;

        // Allocate temporary NativeArrays for the frame
        var raycastCommands = new NativeArray<RaycastCommand>(count, Allocator.TempJob);
        var raycastHits = new NativeArray<RaycastHit>(count, Allocator.TempJob);

        var positions = new NativeArray<float3>(count, Allocator.TempJob);
        var rotations = new NativeArray<quaternion>(count, Allocator.TempJob);
        var targetPositions = new NativeArray<float3>(count, Allocator.TempJob);
        var arriveDistances = new NativeArray<float>(count, Allocator.TempJob);
        var turningOffsets = new NativeArray<float>(count, Allocator.TempJob);
        var isStopped = new NativeArray<byte>(count, Allocator.TempJob);

        var driveOutputs = new NativeArray<float2>(count, Allocator.TempJob);
        var reachedTargetOutputs = new NativeArray<byte>(count, Allocator.TempJob);

        // Step 1: Gather main-thread Transform & Raycast parameters into contiguous NativeArrays
        for (int i = 0; i < count; i++)
        {
            CarAI car = ActiveCars[i];
            Transform carTransform = car.transform;

            positions[i] = carTransform.position;
            rotations[i] = carTransform.rotation;
            targetPositions[i] = car.currentTargetPosition;
            arriveDistances[i] = (car.path != null && car.index == car.path.Count - 1)
                ? car.lastPointArriveDistance
                : car.arriveDistance;
            turningOffsets[i] = car.turningAngleOffset;

            Vector3 rayOrigin = car.raycastStartingPoint != null
                ? car.raycastStartingPoint.transform.position
                : carTransform.position;

            var queryParams = new QueryParameters(1 << car.gameObject.layer, false, QueryTriggerInteraction.UseGlobal, false);
            raycastCommands[i] = new RaycastCommand(rayOrigin, carTransform.forward, queryParams, car.collisionRaycastLength);
        }

        // Step 2: Schedule batched Physics Raycast job across worker threads
        JobHandle raycastJobHandle = RaycastCommand.ScheduleBatch(raycastCommands, raycastHits, 16);
        raycastJobHandle.Complete();

        // Update collisionStop state from batched raycast results before steering calculation
        for (int i = 0; i < count; i++)
        {
            CarAI car = ActiveCars[i];
            car.collisionStop = raycastHits[i].colliderInstanceID != 0;
            isStopped[i] = car.Stop ? (byte)1 : (byte)0;
        }

        // Step 3: Schedule Burst-compiled IJobParallelFor for arrival distance and steering angle math
        CarSteeringAndArrivalJob steeringJob = new CarSteeringAndArrivalJob
        {
            Positions = positions,
            Rotations = rotations,
            TargetPositions = targetPositions,
            ArriveDistances = arriveDistances,
            TurningAngleOffsets = turningOffsets,
            IsStopped = isStopped,
            DriveOutputs = driveOutputs,
            ReachedTargetOutputs = reachedTargetOutputs
        };

        JobHandle steeringJobHandle = steeringJob.Schedule(count, 32);
        steeringJobHandle.Complete();

        // Step 4: Apply results back to each CarAI (iterate backwards in case a car reaches the end of its path and is destroyed)
        for (int i = count - 1; i >= 0; i--)
        {
            CarAI car = ActiveCars[i];
            if (car == null)
                continue;

            float2 drive = driveOutputs[i];
            car.OnDrive?.Invoke(new Vector2(drive.x, drive.y));

            if (reachedTargetOutputs[i] != 0)
            {
                car.SetNextTargetIndex();
            }
        }

        // Step 5: Dispose temporary NativeArrays
        raycastCommands.Dispose();
        raycastHits.Dispose();
        positions.Dispose();
        rotations.Dispose();
        targetPositions.Dispose();
        arriveDistances.Dispose();
        turningOffsets.Dispose();
        isStopped.Dispose();
        driveOutputs.Dispose();
        reachedTargetOutputs.Dispose();
    }

    private void CheckForCollisions()
    {
        if (Physics.Raycast(raycastStartingPoint.transform.position, transform.forward, collisionRaycastLength, 1 << gameObject.layer))
        {
            collisionStop = true;
        }
        else
        {
            collisionStop = false;
        }
    }

    private void Drive()
    {
        if (Stop)
        {
            OnDrive?.Invoke(Vector2.zero);
        }
        else
        {
            Vector3 relativepoint = transform.InverseTransformPoint(currentTargetPosition);
            float angle = Mathf.Atan2(relativepoint.x, relativepoint.z) * Mathf.Rad2Deg;
            var rotateCar = 0;
            if (angle > turningAngleOffset)
            {
                rotateCar = 1;
            }
            else if (angle < -turningAngleOffset)
            {
                rotateCar = -1;
            }
            OnDrive?.Invoke(new Vector2(rotateCar, 1));
        }
    }

    private void CheckIfArrived()
    {
        if (Stop == false)
        {
            var distanceToCheck = arriveDistance;
            if (index == path.Count - 1)
            {
                distanceToCheck = lastPointArriveDistance;
            }
            if (Vector3.Distance(currentTargetPosition, transform.position) < distanceToCheck)
            {
                SetNextTargetIndex();
            }
        }
    }

    private void SetNextTargetIndex()
    {
        index++;
        if (index >= path.Count)
        {
            Stop = true;
            Destroy(gameObject);
        }
        else
        {
            currentTargetPosition = path[index];
        }
    }
}
