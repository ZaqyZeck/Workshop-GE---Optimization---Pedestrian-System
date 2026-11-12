using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SimpleCity.AI.ECS
{
    /// <summary>
    /// Main-thread entry points that replace the public methods of AiAgent
    /// (InitializeInPark, StartTravel, Stop) and the park registration in AiDirector.
    /// Call these from your (managed) AiDirector / spawner code.
    /// </summary>
    public static class AiAgentUtility
    {
        /// <summary>Call when a park structure is placed. Keep the returned Entity in your StructureModel / grid.</summary>
        public static Entity CreatePark(EntityManager em, ParkType type, UnityEngine.Vector3Int roadPosition, UnityEngine.Rect bounds)
        {
            var e = em.CreateEntity(typeof(ParkData));
            em.SetComponentData(e, new ParkData
            {
                Type = type,
                RoadPosition = new int3(roadPosition.x, roadPosition.y, roadPosition.z),
                // Rect.y is world Z
                Bounds = new float4(bounds.xMin, bounds.yMin, bounds.xMax, bounds.yMax)
            });
            return e;
        }

        /// <summary>Call when a park structure is removed. Agents walking there will be destroyed on arrival.</summary>
        public static void DestroyPark(EntityManager em, Entity park)
        {
            if (em.Exists(park))
                em.DestroyEntity(park);
        }

        /// <summary>Places the agent inside a park where it starts wandering.</summary>
        public static void InitializeInPark(EntityManager em, Entity agent, int agentType, Entity park)
        {
            var state = em.GetComponentData<AiAgentState>(agent);
            var cfg = em.GetComponentData<AiAgentConfig>(agent);
            var parkData = em.GetComponentData<ParkData>(park);
            var xform = em.GetComponentData<LocalTransform>(agent);

            state.AgentType = agentType;
            state.Random = Unity.Mathematics.Random.CreateFromIndex((uint)(agent.Index + 1) * 2654435761u);
            AiAgentLogic.EnterPark(ref state, in cfg, park, in parkData, xform.Forward());

            em.SetComponentData(agent, state);
        }

        /// <summary>
        /// Hand a path (found by your pathfinding) to an agent. Returns false if it can't start.
        /// Replaces AiAgent.StartTravel.
        /// </summary>
        public static bool StartTravel(EntityManager em, Entity agent, Entity targetPark, IReadOnlyList<UnityEngine.Vector3> path)
        {
            if (path == null || path.Count == 0)
                return false;

            var buffer = em.GetBuffer<PathPoint>(agent);
            buffer.Clear();
            for (int i = 0; i < path.Count; i++)
                buffer.Add(new PathPoint { Value = new float3(path[i].x, path[i].y, path[i].z) });

            var state = em.GetComponentData<AiAgentState>(agent);
            state.CurrentPark = Entity.Null; // leaves the park (no explicit unregister needed anymore)
            state.TargetPark = targetPark;
            state.PathIndex = 0;
            state.State = AgentStateType.Traveling;
            state.IsWalking = true;
            em.SetComponentData(agent, state);

            em.SetComponentEnabled<AgentStopped>(agent, false);
            return true;
        }

        /// <summary>Replaces the Stop property setter.</summary>
        public static void SetStopped(EntityManager em, Entity agent, bool stopped)
        {
            em.SetComponentEnabled<AgentStopped>(agent, stopped);

            var state = em.GetComponentData<AiAgentState>(agent);
            state.IsWalking = !stopped;
            if (stopped)
                state.Velocity = float3.zero;
            em.SetComponentData(agent, state);
        }
    }
}