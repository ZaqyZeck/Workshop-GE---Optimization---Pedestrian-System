using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SimpleCity.AI.ECS
{
    /// <summary>
    /// Put this on the agent prefab (inside a subscene) instead of AiAgent.
    /// Same inspector fields as before.
    /// </summary>
    public class AiAgentAuthoring : MonoBehaviour
    {
        public int agentType;

        [Header("Travel")]
        public float speed = 0.2f;
        public float rotationSpeed = 10f;

        [Header("Park wandering (boids)")]
        public float minWanderSpeed = 0.05f;
        public float maxWanderSpeed = 0.15f;
        public float maxSteeringForce = 0.3f;
        public float neighbourRadius = 0.6f;
        public float desiredSeparation = 0.15f;
        public float avoidRadius = 0.35f;
        public float boundaryMargin = 0.2f;
        public float wanderJitter = 180f;
        public float maxWanderAngle = 60f;

        [Header("Behaviour weights")]
        public float separationWeight = 1.5f;
        public float alignmentWeight = 0.5f;
        public float cohesionWeight = 1f;
        public float avoidanceWeight = 2.5f;
        public float wanderWeight = 0.6f;
        public float boundaryWeight = 3f;

        [Header("Park switching")]
        public bool autoChangePark = false;
        public Vector2 parkStayDuration = new Vector2(15f, 30f);

        class Baker : Baker<AiAgentAuthoring>
        {
            public override void Bake(AiAgentAuthoring a)
            {
                var e = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(e, new AiAgentConfig
                {
                    Speed = a.speed,
                    RotationSpeed = a.rotationSpeed,
                    MinWanderSpeed = a.minWanderSpeed,
                    MaxWanderSpeed = a.maxWanderSpeed,
                    MaxSteeringForce = a.maxSteeringForce,
                    NeighbourRadius = a.neighbourRadius,
                    DesiredSeparation = a.desiredSeparation,
                    AvoidRadius = a.avoidRadius,
                    BoundaryMargin = a.boundaryMargin,
                    WanderJitter = a.wanderJitter,
                    MaxWanderAngle = a.maxWanderAngle,
                    SeparationWeight = a.separationWeight,
                    AlignmentWeight = a.alignmentWeight,
                    CohesionWeight = a.cohesionWeight,
                    AvoidanceWeight = a.avoidanceWeight,
                    WanderWeight = a.wanderWeight,
                    BoundaryWeight = a.boundaryWeight,
                    AutoChangePark = a.autoChangePark,
                    ParkStayDuration = new float2(a.parkStayDuration.x, a.parkStayDuration.y),
                });

                AddComponent(e, new AiAgentState
                {
                    State = AgentStateType.Idle,
                    AgentType = a.agentType,
                    Random = Unity.Mathematics.Random.CreateFromIndex(1u), // re-seeded in InitializeInPark
                });

                AddBuffer<PathPoint>(e);

                AddComponent<AgentStopped>(e);
                SetComponentEnabled<AgentStopped>(e, false);
            }
        }
    }
}