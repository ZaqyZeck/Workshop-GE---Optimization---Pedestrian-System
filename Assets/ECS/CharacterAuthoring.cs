using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
//using Unity.Ph;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

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

    public class CameraTargetReference : IComponentData
    {
        public Transform target;
    }

    //public readonly partial struct CameraTargetAspect : IAspect
    //{
    //    //public Transform target;
    //}

    public class CharacterAuthoring : MonoBehaviour
    {
        public float MoveSpeed = 5;
        //public Transform cameraTargetTransform;

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
                AddComponentObject<CameraTargetReference>(entity, new CameraTargetReference
                {
                    target = null
                });
            }
        }
    }

    public partial struct CharacterMoveSystem : ISystem
    {

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (charVelocity, charDirection, charStat) in SystemAPI.Query<RefRW<PhysicsVelocity>, CharacterMoveDirection, CharacterStat>())
            {
                float2 moveStep = charDirection.value * charStat.speed;
                charVelocity.ValueRW.Linear = new float3(moveStep.x, 0f, moveStep.y);
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
            // Note: Managed components (classes) are queried directly without RefRW/RefRO
            foreach (var (camRef, transform) in SystemAPI.Query<CameraTargetReference, RefRO<LocalTransform>>())
            {
                if (camRef.target != null)
                {
                    camRef.target.position = new float3(transform.ValueRO.Position.x, camRef.target.position.y, transform.ValueRO.Position.z);
                }
            }
        }
    }
}

