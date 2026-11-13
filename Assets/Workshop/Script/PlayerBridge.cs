using SVS;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBridge : MonoBehaviour
{
    [SerializeField] private CameraMovement cameraMovement;

    private bool isCameraAssigned;

    private void Awake()
    {
        if (cameraMovement == null)
        {
            cameraMovement = GetComponent<CameraMovement>();
        }
    }

    private void Start()
    {
        TryAssignCameraReference();
    }

    private void Update()
    {
        if (!isCameraAssigned) TryAssignCameraReference();
        HandleMouseInput();
    }

    public void TryAssignCameraReference()
    {
        if (cameraMovement == null)
        {
            cameraMovement = CameraMovement.Instance;
        }

        if (cameraMovement == null || World.DefaultGameObjectInjectionWorld == null)
            return;

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(typeof(PlayerCameraReference));

        if (query.HasSingleton<PlayerCameraReference>())
        {
            PlayerCameraReference cameraRef = query.GetSingleton<PlayerCameraReference>();
            cameraRef.cameraMovement = cameraMovement;
            isCameraAssigned = true;
        }
    }

    private void HandleMouseInput()
    {
        if (!Mouse.current.rightButton.wasPressedThisFrame)
            return;

        Vector3? mousePosition = InputManager.Instance.GetMousePosition();

        if (mousePosition != null)
        {
            MovePlayer((Vector3)mousePosition);
        }
    }

    private void MovePlayer(Vector3 targetPosition)
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery playerQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<PlayerComponentState>());

        Entity playerEntity = playerQuery.GetSingletonEntity();
        PlayerComponentState playerState = entityManager.GetComponentData<PlayerComponentState>(playerEntity);

        playerState.TargetPosition =(float3) targetPosition;
        playerState.HasTarget = true;

        entityManager.SetComponentData(playerEntity, playerState);
    }
}
