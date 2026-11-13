using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct PlayerSystem : ISystem
{
    private ComponentLookup<PhysicsVelocity> physicsVelocityLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerComponent>();
        physicsVelocityLookup = state.GetComponentLookup<PhysicsVelocity>(false);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        //// 1. One-time physics setup for any newly baked ECS Player entity with Unity.Physics components
        //var ecb = new EntityCommandBuffer(Allocator.Temp);
        //foreach (var (physicsMass, collider, entity) in SystemAPI
        //             .Query<RefRW<PhysicsMass>, RefRO<PhysicsCollider>>()
        //             .WithAll<PlayerPhysicsStabilizer>()
        //             .WithEntityAccess())
        //{
        //    physicsMass.ValueRW.InverseInertia = float3.zero;

        //    if (collider.ValueRO.IsValid)
        //    {
        //        ref var colBlob = ref collider.ValueRO.Value.Value;
        //        if (colBlob.GetCollisionResponse() == CollisionResponsePolicy.RaiseTriggerEvents)
        //        {
        //            colBlob.SetCollisionResponse(CollisionResponsePolicy.Collide);
        //        }
        //    }

        //    ecb.RemoveComponent<PlayerPhysicsStabilizer>(entity);
        //}
        //ecb.Playback(state.EntityManager);
        //ecb.Dispose();

        // 2. Movement & Rotation Update
        float dt = SystemAPI.Time.DeltaTime;
        physicsVelocityLookup.Update(ref state);

        foreach ((RefRW<PlayerComponent> playerData, RefRW<LocalTransform> transform, Entity player) 
            in SystemAPI.Query<RefRW<PlayerComponent>, RefRW<LocalTransform>>().WithEntityAccess())
        {

            // Prevent physics velocity from causing any unwanted gliding/drifting
            if (physicsVelocityLookup.HasComponent(player))
            {
                var velocity = physicsVelocityLookup[player];
                velocity.Linear = float3.zero;
                velocity.Angular = float3.zero;
                physicsVelocityLookup[player] = velocity;
            }

            if (playerData.ValueRO.State != Player.PlayerState.Alive)
            {
                playerData.ValueRW.HasTarget = false;
                playerData.ValueRW.IsMoving = false;
                continue;
            }

            if (!playerData.ValueRO.HasTarget)
            {
                playerData.ValueRW.IsMoving = false;
                continue;
            }

            float3 currentPos = transform.ValueRO.Position;
            float3 targetPos = new float3(
                playerData.ValueRO.TargetPosition.x,
                currentPos.y,
                playerData.ValueRO.TargetPosition.z
            );
            playerData.ValueRW.TargetPosition = targetPos;

            float3 direction = targetPos - currentPos;
            direction.y = 0f;

            float distance = math.length(direction);
            float stoppingDistance = playerData.ValueRO.StoppingDistance;

            if (distance <= stoppingDistance)
            {
                playerData.ValueRW.HasTarget = false;
                playerData.ValueRW.IsMoving = false;
                continue;
            }

            // Rotate towards movement direction (equivalent to Quaternion.RotateTowards)
            float3 forwardDir = direction / distance;
            quaternion targetRotation = quaternion.LookRotationSafe(forwardDir, math.up());
            transform.ValueRW.Rotation = RotateTowards(
                transform.ValueRO.Rotation,
                targetRotation,
                math.radians(playerData.ValueRO.RotationSpeed) * dt
            );

            // Move towards target position (equivalent to Vector3.MoveTowards)
            float step = playerData.ValueRO.MoveSpeed * dt;
            float3 newPos;
            if (distance <= step)
            {
                newPos = targetPos;
            }
            else
            {
                newPos = currentPos + forwardDir * step;
            }

            transform.ValueRW.Position = newPos;

            // Check remaining distance so we stop immediately on arrival without gliding
            float3 remaining = targetPos - newPos;
            remaining.y = 0f;
            if (math.length(remaining) <= stoppingDistance)
            {
                playerData.ValueRW.HasTarget = false;
                playerData.ValueRW.IsMoving = false;
            }
            else
            {
                playerData.ValueRW.IsMoving = true;
            }
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
    }

    private static quaternion RotateTowards(quaternion from, quaternion to, float maxRadiansDelta)
    {
        float dot = math.abs(math.dot(from.value, to.value));
        if (dot >= 0.999999f)
        {
            return to;
        }

        float angle = math.acos(math.clamp(dot, -1f, 1f)) * 2f;
        if (angle <= maxRadiansDelta || angle < 0.000001f)
        {
            return to;
        }

        float t = math.saturate(maxRadiansDelta / angle);
        return math.slerp(from, to, t);
    }
}
