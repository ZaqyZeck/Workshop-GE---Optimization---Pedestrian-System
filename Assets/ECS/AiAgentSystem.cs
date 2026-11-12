using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SimpleCity.AI.ECS
{
    /// <summary>
    /// Wandering (boids inside a park) and Traveling (following a path to another park).
    /// Replaces AiAgent.Update + the park-mate lookups that AiDirector used to do.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct AiAgentSystem : ISystem
    {
        EntityQuery agentQuery;
        ComponentLookup<ParkData> parkLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            agentQuery = SystemAPI.QueryBuilder().WithAll<AiAgentState, LocalTransform>().Build();
            parkLookup = state.GetComponentLookup<ParkData>(true);

            state.RequireForUpdate(agentQuery);
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            // 1. Snapshot every wandering agent, grouped by park (replaces AiDirector.GetParkMates).
            //    Using a snapshot also makes the flocking independent of update order.
            var neighbours = new NativeParallelMultiHashMap<Entity, Neighbour>(
                agentQuery.CalculateEntityCount(), state.WorldUpdateAllocator);

            state.Dependency = new BuildNeighbourMapJob
            {
                Map = neighbours.AsParallelWriter()
            }.ScheduleParallel(state.Dependency);

            // 2. Boids.
            state.Dependency = new WanderJob
            {
                DeltaTime = dt,
                Neighbours = neighbours
            }.ScheduleParallel(state.Dependency);

            // 3. Traveling.
            parkLookup.Update(ref state);
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            state.Dependency = new TravelJob
            {
                DeltaTime = dt,
                Parks = parkLookup,
                Ecb = ecb.AsParallelWriter()
            }.ScheduleParallel(state.Dependency);
        }

        // ---------------------------------------------------------------------------------

        [BurstCompile]
        partial struct BuildNeighbourMapJob : IJobEntity
        {
            public NativeParallelMultiHashMap<Entity, Neighbour>.ParallelWriter Map;

            void Execute(Entity entity, in LocalTransform transform, in AiAgentState agent)
            {
                if (agent.State != AgentStateType.Wandering || agent.CurrentPark == Entity.Null)
                    return;

                Map.Add(agent.CurrentPark, new Neighbour
                {
                    Entity = entity,
                    Position = transform.Position,
                    Velocity = agent.Velocity,
                    AgentType = agent.AgentType
                });
            }
        }

        // ---------------------------------------------------------------------------------

        [BurstCompile]
        partial struct WanderJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public NativeParallelMultiHashMap<Entity, Neighbour> Neighbours;

            void Execute(Entity entity, ref LocalTransform transform, ref AiAgentState agent, in AiAgentConfig cfg)
            {
                if (agent.State != AgentStateType.Wandering)
                    return;

                float3 position = transform.Position;

                float3 steering =
                    AgentSteering.Flocking(entity, position, in agent, in cfg, Neighbours)
                    + AgentSteering.WanderForce(ref agent, in cfg, DeltaTime, transform.Forward()) * cfg.WanderWeight
                    + AgentSteering.BoundaryForce(position, in agent, in cfg) * cfg.BoundaryWeight;

                float3 velocity = agent.Velocity + steering * DeltaTime;
                velocity.y = 0f;

                // Keep the speed between min and max so the walk animation always matches.
                float currentSpeed = math.length(velocity);
                if (currentSpeed > cfg.MaxWanderSpeed)
                {
                    velocity = velocity / currentSpeed * cfg.MaxWanderSpeed;
                }
                else if (currentSpeed < cfg.MinWanderSpeed)
                {
                    float3 dir = currentSpeed > 0.0001f ? velocity / currentSpeed : transform.Forward();
                    velocity = dir * cfg.MinWanderSpeed;
                }

                // Hard limit: never leave the park while wandering.
                AgentSteering.KeepInsidePark(ref position, ref velocity, agent.ParkBounds);

                agent.Velocity = velocity;
                agent.IsWalking = true;

                // Rigidbody.linearVelocity -> integrate directly.
                transform.Position = position + velocity * DeltaTime;

                if (math.lengthsq(velocity) > 0.000001f)
                {
                    transform.Rotation = math.slerp(
                        transform.Rotation,
                        quaternion.LookRotationSafe(velocity, math.up()),
                        math.saturate(DeltaTime * cfg.RotationSpeed));
                }

                // autoChangePark was commented out in the original; the timer logic would go here.
                // Park selection needs pathfinding, so request it from a managed system instead.
            }
        }

        // ---------------------------------------------------------------------------------

        [BurstCompile]
        [WithDisabled(typeof(AgentStopped))] // Stop == true skips traveling, like before
        partial struct TravelJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public ComponentLookup<ParkData> Parks;
            public EntityCommandBuffer.ParallelWriter Ecb;

            void Execute(
                [ChunkIndexInQuery] int sortKey,
                Entity entity,
                ref LocalTransform transform,
                ref AiAgentState agent,
                in AiAgentConfig cfg,
                DynamicBuffer<PathPoint> path)
            {
                if (agent.State != AgentStateType.Traveling)
                    return;

                if (agent.PathIndex >= path.Length)
                {
                    Arrive(sortKey, entity, ref transform, ref agent, in cfg, path);
                    return;
                }

                float3 pos = transform.Position;
                float3 target = path[agent.PathIndex].Value;
                target.y = pos.y;

                // Same as before: move along the forward vector, steer toward the waypoint.
                float3 forward = transform.Forward();
                agent.Velocity = forward * cfg.Speed;
                agent.IsWalking = true;
                pos += agent.Velocity * DeltaTime;

                float3 look = target - transform.Position;
                if (math.lengthsq(look) > 0.000001f)
                {
                    transform.Rotation = math.slerp(
                        transform.Rotation,
                        quaternion.LookRotationSafe(look, math.up()),
                        math.saturate(DeltaTime * cfg.RotationSpeed));
                }

                transform.Position = pos;

                if (math.distance(pos, target) < 0.05f)
                {
                    agent.PathIndex++;
                    if (agent.PathIndex >= path.Length)
                        Arrive(sortKey, entity, ref transform, ref agent, in cfg, path);
                }
            }

            void Arrive(
                int sortKey,
                Entity entity,
                ref LocalTransform transform,
                ref AiAgentState agent,
                in AiAgentConfig cfg,
                DynamicBuffer<PathPoint> path)
            {
                // The target park was removed while walking towards it.
                if (!Parks.TryGetComponent(agent.TargetPark, out ParkData park))
                {
                    Ecb.DestroyEntity(sortKey, entity);
                    return;
                }

                path.Clear();
                AiAgentLogic.EnterPark(ref agent, in cfg, agent.TargetPark, in park, transform.Forward());
            }
        }
    }

    // =====================================================================================

    public static class AiAgentLogic
    {
        /// <summary>Shared by InitializeInPark and ArriveAtTargetPark.</summary>
        public static void EnterPark(
            ref AiAgentState agent,
            in AiAgentConfig cfg,
            Entity parkEntity,
            in ParkData park,
            float3 forward)
        {
            agent.Velocity = forward * cfg.MinWanderSpeed;

            agent.CurrentPark = parkEntity;
            agent.CurrentParkType = park.Type;
            agent.CurrentParkRoadPosition = park.RoadPosition;
            agent.ParkBounds = park.Bounds;
            agent.TargetPark = Entity.Null;
            agent.PathIndex = 0;
            agent.WanderAngle = 0f;
            agent.ParkStayTimer = agent.Random.NextFloat(cfg.ParkStayDuration.x, cfg.ParkStayDuration.y);

            agent.State = AgentStateType.Wandering;
            agent.IsWalking = true;
        }
    }

    [BurstCompile]
    public static class AgentSteering
    {
        /// <summary>Separation + alignment + cohesion with same-type agents, avoidance of other types.</summary>
        public static float3 Flocking(
            Entity self,
            float3 position,
            in AiAgentState agent,
            in AiAgentConfig cfg,
            in NativeParallelMultiHashMap<Entity, Neighbour> neighbours)
        {
            float neighbourRadiusSqr = cfg.NeighbourRadius * cfg.NeighbourRadius;
            float separationSqr = cfg.DesiredSeparation * cfg.DesiredSeparation;
            float avoidRadiusSqr = cfg.AvoidRadius * cfg.AvoidRadius;

            float3 separation = 0;
            float3 avoidance = 0;
            float3 alignment = 0;
            float3 center = 0;
            int flockCount = 0;

            foreach (var other in neighbours.GetValuesForKey(agent.CurrentPark))
            {
                if (other.Entity == self)
                    continue;

                float3 offset = position - other.Position;
                offset.y = 0f;
                float distanceSqr = math.lengthsq(offset);

                if (other.AgentType == agent.AgentType)
                {
                    if (distanceSqr > neighbourRadiusSqr)
                        continue;

                    flockCount++;
                    center += other.Position;
                    alignment += other.Velocity;

                    // Push away harder the closer a flock mate is than the desired distance.
                    if (distanceSqr < separationSqr && distanceSqr > 0.000001f)
                    {
                        float distance = math.sqrt(distanceSqr);
                        separation += offset / distance * (1f - distance / cfg.DesiredSeparation);
                    }
                }
                else if (distanceSqr < avoidRadiusSqr && distanceSqr > 0.000001f)
                {
                    float distance = math.sqrt(distanceSqr);
                    avoidance += offset / distance * (1f - distance / cfg.AvoidRadius);
                }
            }

            float3 force = 0;

            force += Steer(separation, math.saturate(math.length(separation)), agent.Velocity, in cfg) * cfg.SeparationWeight;
            force += Steer(avoidance, math.saturate(math.length(avoidance)), agent.Velocity, in cfg) * cfg.AvoidanceWeight;

            if (flockCount > 0)
            {
                force += Steer(alignment / flockCount, 1f, agent.Velocity, in cfg) * cfg.AlignmentWeight;

                // Only pull towards the group when farther away than the desired distance.
                float3 toCenter = center / flockCount - position;
                toCenter.y = 0f;
                float cohesionRange = math.max(0.0001f, cfg.NeighbourRadius - cfg.DesiredSeparation);
                float cohesionStrength = math.saturate((math.length(toCenter) - cfg.DesiredSeparation) / cohesionRange);
                force += Steer(toCenter, cohesionStrength, agent.Velocity, in cfg) * cfg.CohesionWeight;
            }

            return force;
        }

        /// <summary>Random meandering: steers toward the current heading rotated by a slowly drifting angle.</summary>
        public static float3 WanderForce(ref AiAgentState agent, in AiAgentConfig cfg, float deltaTime, float3 forward)
        {
            agent.WanderAngle += agent.Random.NextFloat(-1f, 1f) * cfg.WanderJitter * deltaTime;
            agent.WanderAngle = math.clamp(agent.WanderAngle, -cfg.MaxWanderAngle, cfg.MaxWanderAngle);

            float3 heading = math.lengthsq(agent.Velocity) > 0.000001f ? math.normalize(agent.Velocity) : forward;
            float3 wanderDirection = math.mul(quaternion.RotateY(math.radians(agent.WanderAngle)), heading);
            return Steer(wanderDirection, 1f, agent.Velocity, in cfg);
        }

        /// <summary>Soft force pushing the agent back inside when it gets close to the park border.</summary>
        public static float3 BoundaryForce(float3 position, in AiAgentState agent, in AiAgentConfig cfg)
        {
            float4 b = agent.ParkBounds; // x=minX, y=minZ, z=maxX, w=maxZ
            float margin = math.max(0.0001f, cfg.BoundaryMargin);
            float3 push = 0;

            if (position.x < b.x + margin)
                push.x = (b.x + margin - position.x) / margin;
            else if (position.x > b.z - margin)
                push.x = -(position.x - (b.z - margin)) / margin;

            if (position.z < b.y + margin)
                push.z = (b.y + margin - position.z) / margin;
            else if (position.z > b.w - margin)
                push.z = -(position.z - (b.w - margin)) / margin;

            return Steer(push, math.saturate(math.length(push)), agent.Velocity, in cfg);
        }

        /// <summary>Hard limit: clamp inside the park and reflect the velocity.</summary>
        public static void KeepInsidePark(ref float3 position, ref float3 velocity, float4 b)
        {
            if (position.x < b.x) { position.x = b.x; velocity.x = math.abs(velocity.x); }
            else if (position.x > b.z) { position.x = b.z; velocity.x = -math.abs(velocity.x); }

            if (position.z < b.y) { position.z = b.y; velocity.z = math.abs(velocity.z); }
            else if (position.z > b.w) { position.z = b.w; velocity.z = -math.abs(velocity.z); }
        }

        /// <summary>Reynolds steering: (desired velocity - current velocity), limited by the max steering force.</summary>
        public static float3 Steer(float3 direction, float strength, float3 velocity, in AiAgentConfig cfg)
        {
            direction.y = 0f;
            if (strength <= 0f || math.lengthsq(direction) < 0.00000001f)
                return 0;

            float3 desired = math.normalize(direction) * cfg.MaxWanderSpeed;
            float3 steer = desired - velocity;

            float lenSq = math.lengthsq(steer);
            float max = cfg.MaxSteeringForce;
            if (lenSq > max * max)
                steer = steer / math.sqrt(lenSq) * max;

            return steer * strength;
        }
    }
}