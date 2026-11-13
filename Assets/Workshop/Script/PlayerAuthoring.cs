
using Unity.Entities;
using UnityEngine;

public class PlayerAuthoring : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float stoppingDistance = 0.1f;

    private class PlayerBaker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            Entity player = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(player, new PlayerComponentConfig
            {
                moveSpeed = authoring.moveSpeed,
                rotationSpeed = authoring.rotationSpeed,
                stoppingDistance = authoring.stoppingDistance
            });

            AddComponent(player, new PlayerComponentState());
        }
    }
}