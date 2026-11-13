using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using UnityCollider = UnityEngine.Collider;
using UnityBoxCollider = UnityEngine.BoxCollider;
using UnitySphereCollider = UnityEngine.SphereCollider;
using UnityCapsuleCollider = UnityEngine.CapsuleCollider;
using UnityMeshCollider = UnityEngine.MeshCollider;
using PhysicsColliderBlob = Unity.Physics.Collider;
using PhysicsBoxCollider = Unity.Physics.BoxCollider;
using PhysicsSphereCollider = Unity.Physics.SphereCollider;
using PhysicsCapsuleCollider = Unity.Physics.CapsuleCollider;
using PhysicsMeshCollider = Unity.Physics.MeshCollider;
using PhysicsConvexCollider = Unity.Physics.ConvexCollider;
using PhysicsMaterial = Unity.Physics.Material;

namespace TMG.Town
{
    /// <summary>
    /// Tag component identifying an ECS static physics proxy entity created from a regular GameObject Collider.
    /// </summary>
    public struct EnvironmentColliderProxy : IComponentData {}

    /// <summary>
    /// Synchronizes regular GameObject Colliders in the main scene (anything tagged "Enviroment", plus placed
    /// structures/props) into the ECS Unity.Physics world as static PhysicsCollider entities so that ECS
    /// entities (such as the character in Town-EntityScene) physically collide with them instead of phasing through.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(PhysicsSystemGroup))]
    public partial class EnvironmentPhysicsBridgeSystem : SystemBase
    {
        private const string EnvironmentTag = "Enviroment";

        private struct TrackedCollider
        {
            public Entity Entity;
            public BlobAssetReference<PhysicsColliderBlob> Blob;
            public float3 LastPosition;
            public quaternion LastRotation;
        }

        private readonly Dictionary<int, TrackedCollider> _trackedColliders = new Dictionary<int, TrackedCollider>();
        private readonly List<int> _staleIds = new List<int>();
        private readonly List<UnityCollider> _colliderBuffer = new List<UnityCollider>();

        protected override void OnDestroy()
        {
            foreach (var kvp in _trackedColliders)
            {
                if (kvp.Value.Blob.IsCreated)
                {
                    kvp.Value.Blob.Dispose();
                }
            }
            _trackedColliders.Clear();
        }

        protected override void OnUpdate()
        {
            var activeIds = new HashSet<int>();

            // 1. Collect all colliders under GameObjects tagged "Enviroment" (Plane, Ground, Nature trees/rocks/bushes)
            GameObject[] envRoots = null;
            try
            {
                envRoots = GameObject.FindGameObjectsWithTag(EnvironmentTag);
            }
            catch (UnityException)
            {
                // Tag does not exist
            }

            if (envRoots != null)
            {
                for (int i = 0; i < envRoots.Length; i++)
                {
                    if (envRoots[i] == null || !envRoots[i].activeInHierarchy)
                        continue;

                    envRoots[i].GetComponentsInChildren(false, _colliderBuffer);
                    for (int j = 0; j < _colliderBuffer.Count; j++)
                    {
                        ProcessCollider(_colliderBuffer[j], activeIds);
                    }
                }
            }

            // 2. Also collect solid colliders on structures placed via PlacementManager (houses, shops, parks, etc.)
            if (PlacementManager.Instance != null)
            {
                PlacementManager.Instance.GetComponentsInChildren(false, _colliderBuffer);
                for (int j = 0; j < _colliderBuffer.Count; j++)
                {
                    ProcessCollider(_colliderBuffer[j], activeIds);
                }
            }

            // 3. Clean up proxy entities whose GameObject Collider was destroyed (e.g. cleared trees/rocks) or disabled
            _staleIds.Clear();
            foreach (var kvp in _trackedColliders)
            {
                if (!activeIds.Contains(kvp.Key))
                {
                    _staleIds.Add(kvp.Key);
                }
            }

            for (int i = 0; i < _staleIds.Count; i++)
            {
                int id = _staleIds[i];
                var tracked = _trackedColliders[id];
                if (EntityManager.Exists(tracked.Entity))
                {
                    EntityManager.DestroyEntity(tracked.Entity);
                }
                if (tracked.Blob.IsCreated)
                {
                    tracked.Blob.Dispose();
                }
                _trackedColliders.Remove(id);
            }
        }

        private void ProcessCollider(UnityCollider col, HashSet<int> activeIds)
        {
            if (col == null || !col.enabled || col.isTrigger || !col.gameObject.activeInHierarchy) return;

            if (col.gameObject.scene.isSubScene) return;

            if (col.attachedRigidbody != null && !col.attachedRigidbody.isKinematic) return;

            int instanceId = col.GetInstanceID();
            if (!activeIds.Add(instanceId))
                return;

            Transform t = col.transform;
            float3 pos = t.position;
            quaternion rot = t.rotation;

            if (_trackedColliders.TryGetValue(instanceId, out var existing))
            {
                // Update transform if the GameObject moved/rotated
                if (!math.all(pos == existing.LastPosition) || ! existing.LastRotation.Equals(rot))
                {
                    existing.LastPosition = pos;
                    existing.LastRotation = rot;
                    _trackedColliders[instanceId] = existing;

                    if (EntityManager.Exists(existing.Entity))
                    {
                        EntityManager.SetComponentData(existing.Entity, LocalTransform.FromPositionRotation(pos, rot));
                        EntityManager.SetComponentData(existing.Entity, new LocalToWorld
                        {
                            Value = float4x4.TRS(pos, rot, new float3(1f, 1f, 1f))
                        });
                    }
                }
                return;
            }

            BlobAssetReference<PhysicsColliderBlob> blob = CreateColliderBlob(col);
            if (!blob.IsCreated)
                return;

            Entity proxyEntity = EntityManager.CreateEntity();
            EntityManager.AddComponent<EnvironmentColliderProxy>(proxyEntity);
            EntityManager.AddComponentData(proxyEntity, LocalTransform.FromPositionRotation(pos, rot));
            EntityManager.AddComponentData(proxyEntity, new LocalToWorld
            {
                Value = float4x4.TRS(pos, rot, new float3(1f, 1f, 1f))
            });
            EntityManager.AddComponentData(proxyEntity, new PhysicsCollider { Value = blob });
            EntityManager.AddSharedComponent(proxyEntity, new PhysicsWorldIndex(0));

            _trackedColliders.Add(instanceId, new TrackedCollider
            {
                Entity = proxyEntity,
                Blob = blob,
                LastPosition = pos,
                LastRotation = rot
            });
        }

        private static BlobAssetReference<PhysicsColliderBlob> CreateColliderBlob(UnityCollider col)
        {
            float3 scale = math.abs((float3)col.transform.lossyScale);
            var filter = CollisionFilter.Default;
            var material = PhysicsMaterial.Default;

            if (col is UnityBoxCollider box)
            {
                float3 size = math.abs((float3)box.size) * scale;
                float3 center = (float3)box.center * scale;
                float bevel = math.min(ConvexHullGenerationParameters.Default.BevelRadius, math.cmin(size) * 0.1f);

                return PhysicsBoxCollider.Create(new BoxGeometry
                {
                    Center = center,
                    Orientation = quaternion.identity,
                    Size = size,
                    BevelRadius = math.max(0f, bevel)
                }, filter, material);
            }

            if (col is UnitySphereCollider sphere)
            {
                float radius = sphere.radius * math.cmax(scale);
                float3 center = (float3)sphere.center * scale;
                return PhysicsSphereCollider.Create(new SphereGeometry
                {
                    Center = center,
                    Radius = radius
                }, filter, material);
            }

            if (col is UnityCapsuleCollider capsule)
            {
                int dir = capsule.direction;
                float3 axis = float3.zero;
                axis[dir] = 1f;

                float3 orthoScale = scale;
                orthoScale[dir] = 0f;
                float radius = capsule.radius * math.cmax(orthoScale);
                float height = math.max(capsule.height * scale[dir], 2f * radius);
                float3 offset = axis * (0.5f * height - radius);
                float3 center = (float3)capsule.center * scale;

                return PhysicsCapsuleCollider.Create(new CapsuleGeometry
                {
                    Vertex0 = center + offset,
                    Vertex1 = center - offset,
                    Radius = radius
                }, filter, material);
            }

            if (col is UnityMeshCollider meshCol)
            {
                UnityEngine.Mesh sharedMesh = meshCol.sharedMesh;
                if (sharedMesh == null)
                    return default;

                // If the mesh is readable on CPU (e.g., Unity's built-in Plane mesh), build an exact Mesh or ConvexHull collider
                if (sharedMesh.isReadable)
                {
                    Vector3[] srcVerts = sharedMesh.vertices;
                    int[] srcTris = sharedMesh.triangles;
                    if (srcVerts != null && srcVerts.Length > 0 && srcTris != null && srcTris.Length >= 3)
                    {
                        using var vertices = new NativeArray<float3>(srcVerts.Length, Allocator.Temp);
                        for (int i = 0; i < srcVerts.Length; i++)
                        {
                            var __newvertices__ = vertices;
                            __newvertices__[i] = (float3)srcVerts[i] * scale;
                        }

                        if (meshCol.convex)
                        {
                            return PhysicsConvexCollider.Create(vertices, ConvexHullGenerationParameters.Default, filter, material);
                        }
                        else
                        {
                            int triCount = srcTris.Length / 3;
                            using var triangles = new NativeArray<int3>(triCount, Allocator.Temp);
                            for (int i = 0; i < triCount; i++)
                            {
                                var __newtriangles__ = triangles;
                                __newtriangles__[i] = new int3(srcTris[i * 3], srcTris[i * 3 + 1], srcTris[i * 3 + 2]);
                            }
                            return PhysicsMeshCollider.Create(vertices, triangles, filter, material);
                        }
                    }
                }

                // Fallback when FBX has Read/Write disabled (isReadable == false): approximate using local mesh bounds
                Bounds b = sharedMesh.bounds;
                float3 size = math.max((float3)b.size * scale, new float3(0.05f));
                float3 center = (float3)b.center * scale;
                float bevel = math.min(ConvexHullGenerationParameters.Default.BevelRadius, math.cmin(size) * 0.1f);

                return PhysicsBoxCollider.Create(new BoxGeometry
                {
                    Center = center,
                    Orientation = quaternion.identity,
                    Size = size,
                    BevelRadius = math.max(0f, bevel)
                }, filter, material);
            }

            return default;
        }
    }
}
