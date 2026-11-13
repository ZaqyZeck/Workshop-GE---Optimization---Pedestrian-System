using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class PlayerAuthoring : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float stoppingDistance = 0.1f;

    public float MoveSpeed => moveSpeed;
    public float RotationSpeed => rotationSpeed;
    public float StoppingDistance => stoppingDistance;

    private class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new PlayerComponent
            {
                State = Player.PlayerState.Alive,
                MoveSpeed = authoring.moveSpeed,
                RotationSpeed = authoring.rotationSpeed,
                StoppingDistance = authoring.stoppingDistance,
                TargetPosition = float3.zero,
                HasTarget = false,
                IsMoving = false
            });

            AddComponent<PlayerPhysicsStabilizer>(entity);
        }
    }
}
