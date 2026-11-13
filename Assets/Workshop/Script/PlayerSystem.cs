using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.VisualScripting;
using UnityEngine.InputSystem;
//using UnityEngine;

[BurstCompile]
public partial struct PlayerMovementSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerComponentConfig>();
        state.RequireForUpdate<PlayerComponentState>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRO<PlayerComponentConfig> playerConfig, RefRW<PlayerComponentState> playerState, RefRW <LocalTransform> playerTransform, Entity playerEntity) 
            in SystemAPI.Query<RefRO<PlayerComponentConfig>, RefRW<PlayerComponentState>, RefRW<LocalTransform>>().WithEntityAccess())
        {
            if (!playerState.ValueRO.HasTarget) return;

            playerState.ValueRW.TargetPosition.y = playerTransform.ValueRO.Position.y;
            float3 direction = playerState.ValueRO.TargetPosition - playerTransform.ValueRO.Position;
            direction.y = 0f;

            if (math.length(direction) <= playerConfig.ValueRO.stoppingDistance)
            {
                playerState.ValueRW.HasTarget = false;
                return;
            }

            //quaternion targetRotation = Quaternion.LookRotation(direction);

            //float3
            float3 forwardDirection = direction / math.length(direction);

            quaternion targetRotation = quaternion.LookRotation(forwardDirection, math.up());

            playerTransform.ValueRW.Rotation = RotateTowards(
                playerTransform.ValueRO.Rotation,
                targetRotation,
                playerConfig.ValueRO.rotationSpeed * deltaTime
            );

            float step = playerConfig.ValueRO.moveSpeed * deltaTime;
            playerTransform.ValueRW.Position += step * forwardDirection;

            float3 remaining = playerState.ValueRO.TargetPosition - playerTransform.ValueRO.Position;
            remaining.y = 0f;
            if (math.length(remaining) <= playerConfig.ValueRO.stoppingDistance)
            {
                playerState.ValueRW.HasTarget = false;
            }
        }

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

public partial struct InputSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerComponentState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!Mouse.current.rightButton.wasPressedThisFrame) return;

        foreach (RefRW<PlayerComponentState> playerState
            in SystemAPI.Query<RefRW<PlayerComponentState>>())
        {
            //bool isCorrect = false;
            float3 targetPosition = (float3) InputManager.Instance.GetMousePosition(out bool isCorrect);

            if(!isCorrect) return;

            playerState.ValueRW.HasTarget = true;
            playerState.ValueRW.TargetPosition = targetPosition;
        }

    }
}

public partial class PlayerCameraSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // How to get and save CameraMovement reference
    }
} 