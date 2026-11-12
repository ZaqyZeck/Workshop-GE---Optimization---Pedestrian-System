using Unity.Entities;
using Unity.Mathematics;

namespace SimpleCity.AI.ECS
{
    public enum AgentStateType : byte
    {
        Idle,       // Not initialized yet.
        Wandering,  // Moving around inside a park using flocking (boids).
        Traveling   // Following a sidewalk path towards another park.
    }

    /// <summary>
    /// Replaces StructureModel for the AI: one entity per park.
    /// Assumes ParkType is a plain (blittable) enum.
    /// </summary>
    public struct ParkData : IComponentData
    {
        public ParkType Type;
        public int3 RoadPosition;
        /// <summary>x = minX, y = minZ, z = maxX, w = maxZ (was Rect: Rect.y = world Z).</summary>
        public float4 Bounds;
    }

    /// <summary>Tuning values (the [SerializeField] fields). Baked per agent; same for all agents of a prefab.</summary>
    public struct AiAgentConfig : IComponentData
    {
        // Travel
        public float Speed;
        public float RotationSpeed;

        // Boids
        public float MinWanderSpeed;
        public float MaxWanderSpeed;
        public float MaxSteeringForce;
        public float NeighbourRadius;
        public float DesiredSeparation;
        public float AvoidRadius;
        public float BoundaryMargin;
        public float WanderJitter;
        public float MaxWanderAngle;

        // Weights
        public float SeparationWeight;
        public float AlignmentWeight;
        public float CohesionWeight;
        public float AvoidanceWeight;
        public float WanderWeight;
        public float BoundaryWeight;

        // Park switching
        public bool AutoChangePark;
        public float2 ParkStayDuration;
    }

    /// <summary>All mutable runtime state that used to live as fields on AiAgent.</summary>
    public struct AiAgentState : IComponentData
    {
        public AgentStateType State;

        /// <summary>Agents with the same type flock together, different types avoid each other.</summary>
        public int AgentType;

        public float3 Velocity;
        public float WanderAngle;
        public float ParkStayTimer;
        public int PathIndex;

        public Entity CurrentPark;
        public ParkType CurrentParkType;
        /// <summary>Cached so the agent can still leave if its park gets removed.</summary>
        public int3 CurrentParkRoadPosition;
        public float4 ParkBounds;

        public Entity TargetPark;

        /// <summary>Drive your animation from this (was animator "Walk" bool).</summary>
        public bool IsWalking;

        public Unity.Mathematics.Random Random;
    }

    /// <summary>The sidewalk path (was List&lt;Vector3&gt; pathToGo).</summary>
    [InternalBufferCapacity(0)]
    public struct PathPoint : IBufferElementData
    {
        public float3 Value;
    }

    /// <summary>Enableable replacement for the Stop property. Enabled = agent stops while traveling.</summary>
    public struct AgentStopped : IComponentData, IEnableableComponent { }

    /// <summary>Snapshot of an agent used for neighbour lookups inside a park.</summary>
    public struct Neighbour
    {
        public Entity Entity;
        public float3 Position;
        public float3 Velocity;
        public int AgentType;
    }
}