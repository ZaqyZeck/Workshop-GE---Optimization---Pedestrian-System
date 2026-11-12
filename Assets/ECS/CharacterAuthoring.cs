
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Collections;

namespace TMG.Town
{
    public struct CharacterMoveDirection : IComponentData
    {
        public float2 value;
    }

    public struct CharacterStat : IComponentData
    {
        public float speed;
    }

    public struct CharacterPhysicsStabilizer : IComponentData { }

    public class CameraTargetReference : IComponentData
    {
        public Transform target;
    }

    public class CharacterAuthoring : MonoBehaviour
    {
        public float MoveSpeed = 5;

        private class Baker : Baker<CharacterAuthoring>
        {
            public override void Bake(CharacterAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<CharacterMoveDirection>(entity);

                AddComponent<CharacterStat>(entity, new CharacterStat
                {
                    speed = authoring.MoveSpeed
                });

                AddComponent<CharacterPhysicsStabilizer>(entity);

                AddComponentObject<CameraTargetReference>(entity, new CameraTargetReference
                {
                    target = null
                });
            }
        }
    }

    public partial struct CharacterPhysicsSetupSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (physicsMass, collider, entity) in SystemAPI
                         .Query<RefRW<PhysicsMass>, RefRO<PhysicsCollider>>()
                         .WithAll<CharacterPhysicsStabilizer>()
                         .WithEntityAccess())
            {
                // Lock X, Y, Z rotation.
                physicsMass.ValueRW.InverseInertia = float3.zero;

                // Ensure the collider is not a trigger.
                if (collider.ValueRO.IsValid)
                {
                    ref var colBlob = ref collider.ValueRO.Value.Value;

                    if (colBlob.GetCollisionResponse() == CollisionResponsePolicy.RaiseTriggerEvents)
                    {
                        colBlob.SetCollisionResponse(CollisionResponsePolicy.Collide);
                    }
                }

                // Defer component removal until after query iteration.
                ecb.RemoveComponent<CharacterPhysicsStabilizer>(entity);
            }

            // Apply structural changes after iteration is complete.
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    public partial struct CharacterMoveSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (charVelocity, charDirection, charStat, charTransform) in
                     SystemAPI.Query<RefRW<PhysicsVelocity>, CharacterMoveDirection, CharacterStat, RefRW<LocalTransform>>())
            {
                float2 moveStep = charDirection.value * charStat.speed;

                // Preserve vertical velocity for gravity and ground collisions.
                float currentYVelocity = charVelocity.ValueRO.Linear.y;

                charVelocity.ValueRW.Linear = new float3(
                    moveStep.x,
                    currentYVelocity,
                    moveStep.y
                );

                charVelocity.ValueRW.Angular = float3.zero;

                // Rotate the character toward the movement direction.
                if (math.lengthsq(charDirection.value) > 0.0001f)
                {
                    float3 forward = new float3(
                        charDirection.value.x,
                        0f,
                        charDirection.value.y
                    );

                    charTransform.ValueRW.Rotation = quaternion.LookRotationSafe(
                        forward,
                        math.up()
                    );
                }
            }
        }
    }

    [UpdateBefore(typeof(CharacterMoveSystem))]
    public partial struct CharacterInputSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (Keyboard.current == null)
                return;

            float2 input = float2.zero;

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                input.y += 1;

            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                input.y -= 1;

            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                input.x += 1;

            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                input.x -= 1;

            input = math.normalizesafe(input);

            foreach (var direction in SystemAPI.Query<RefRW<CharacterMoveDirection>>())
            {
                direction.ValueRW.value = input;
            }
        }
    }

    public partial struct ControlCameraSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // Managed components are queried directly.
            foreach (var (camRef, transform) in
                     SystemAPI.Query<CameraTargetReference, RefRO<LocalTransform>>())
            {
                if (camRef.target != null)
                {
                    camRef.target.position = new float3(
                        transform.ValueRO.Position.x,
                        camRef.target.position.y,
                        transform.ValueRO.Position.z
                    );
                }
            }
        }
    }
}
