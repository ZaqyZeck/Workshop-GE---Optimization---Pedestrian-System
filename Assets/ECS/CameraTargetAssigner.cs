using UnityEngine;
using Unity.Entities;

namespace TMG.Town
{
    public class CameraTargetAssigner : MonoBehaviour
    {
        //[SerializeField] private Transform mainSceneCameraTarget;

        private void Start()
        {
            AssignTarget();
        }

        public void AssignTarget()
        {
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            // Query the spawned entity with CameraTargetReference
            var query = entityManager.CreateEntityQuery(typeof(CameraTargetReference));

            if (query.HasSingleton<CameraTargetReference>())
            {
                var targetRef = query.GetSingleton<CameraTargetReference>();
                targetRef.target = transform;
            }
        }
    }
}
