using Unity.Entities;
using UnityEngine;

namespace SimpleCity.AI.ECS
{
    public struct AgentPrefabElement : IBufferElementData
    {
        public Entity Prefab;
    }

    /// <summary>
    /// Put this on a GameObject inside a subscene. Each prefab needs AiAgentAuthoring.
    /// The index in this array is the agent type (same as the old pedestrianPrefabs array).
    /// </summary>
    public class AiAgentPrefabsAuthoring : MonoBehaviour
    {
        public GameObject[] pedestrianPrefabs;

        class Baker : Baker<AiAgentPrefabsAuthoring>
        {
            public override void Bake(AiAgentPrefabsAuthoring authoring)
            {
                var e = GetEntity(TransformUsageFlags.None);
                var buffer = AddBuffer<AgentPrefabElement>(e);

                if (authoring.pedestrianPrefabs == null)
                    return;

                foreach (var prefab in authoring.pedestrianPrefabs)
                {
                    buffer.Add(new AgentPrefabElement
                    {
                        Prefab = GetEntity(prefab, TransformUsageFlags.Dynamic)
                    });
                }
            }
        }
    }
}