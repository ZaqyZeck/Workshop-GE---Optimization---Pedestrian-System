using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Bridging MonoBehaviour that lets main-scene GameObjects (InputManager, CameraMovement,
/// LevelManager, Animator, UI, etc.) interact with the ECS Player entity.
/// </summary>
public class PlayerBridge : MonoBehaviour
{
    public static PlayerBridge Instance { get; private set; }

    [Header("Bridge Settings")]
    [SerializeField] private bool handleMouseInput = true;
    [SerializeField] private bool listenToGameEvents = true;
    [SerializeField] private bool syncThisTransform = true;

    [Header("Optional Scene References")]
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Animator playerAnimator;

    private EntityQuery playerQuery;
    private bool queryCreated;

    public Player.PlayerState state
    {
        get
        {
            if (TryGetPlayerEntity(out Entity entity, out EntityManager em))
            {
                return em.GetComponentData<PlayerComponent>(entity).State;
            }
            return Player.PlayerState.Alive;
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        if (listenToGameEvents)
        {
            GameEvent.OnPlayerDie += Die;
            GameEvent.OnPlayerRevive += Revive;
        }
    }

    private void OnDisable()
    {
        if (listenToGameEvents)
        {
            GameEvent.OnPlayerDie -= Die;
            GameEvent.OnPlayerRevive -= Revive;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (!TryGetPlayerEntity(out Entity entity, out EntityManager em))
            return;

        PlayerComponent playerData = em.GetComponentData<PlayerComponent>(entity);

        if (handleMouseInput && playerData.State == Player.PlayerState.Alive)
        {
            HandleMouseInput();
        }

        SyncFromEntity(em, entity);
        HandleAnimation(em.GetComponentData<PlayerComponent>(entity).IsMoving);
    }

    private void HandleMouseInput()
    {
        if (Mouse.current == null || !Mouse.current.rightButton.wasPressedThisFrame)
            return;

        if (InputManager.Instance == null)
            return;

        Vector3? mousePosition = InputManager.Instance.GetMousePosition();
        if (mousePosition != null)
        {
            MoveToPosition(mousePosition.Value);
        }
    }

    private void SyncFromEntity(EntityManager em, Entity entity)
    {
        if (!em.HasComponent<LocalTransform>(entity))
            return;

        LocalTransform ecsTransform = em.GetComponentData<LocalTransform>(entity);

        if (syncThisTransform)
        {
            transform.SetPositionAndRotation(ecsTransform.Position, ecsTransform.Rotation);
        }

        if (cameraTarget != null && cameraTarget != transform)
        {
            cameraTarget.position = new Vector3(
                ecsTransform.Position.x,
                cameraTarget.position.y,
                ecsTransform.Position.z
            );
        }
    }

    private void HandleAnimation(bool isMoving)
    {
        if (playerAnimator == null)
            return;

        playerAnimator.SetBool("Walk", isMoving);
    }

    public void MoveToPosition(Vector3 position)
    {
        if (!TryGetPlayerEntity(out Entity entity, out EntityManager em))
            return;

        PlayerComponent playerData = em.GetComponentData<PlayerComponent>(entity);
        if (playerData.State != Player.PlayerState.Alive)
            return;

        float currentY = em.HasComponent<LocalTransform>(entity)
            ? em.GetComponentData<LocalTransform>(entity).Position.y
            : position.y;

        playerData.TargetPosition = new float3(position.x, currentY, position.z);
        playerData.HasTarget = true;
        em.SetComponentData(entity, playerData);
    }

    public void StopMovement()
    {
        if (!TryGetPlayerEntity(out Entity entity, out EntityManager em))
            return;

        PlayerComponent playerData = em.GetComponentData<PlayerComponent>(entity);
        playerData.HasTarget = false;
        playerData.IsMoving = false;
        em.SetComponentData(entity, playerData);

        if (em.HasComponent<PhysicsVelocity>(entity))
        {
            em.SetComponentData(entity, new PhysicsVelocity
            {
                Linear = float3.zero,
                Angular = float3.zero
            });
        }
    }

    public bool IsMoving()
    {
        if (!TryGetPlayerEntity(out Entity entity, out EntityManager em))
            return false;

        return em.GetComponentData<PlayerComponent>(entity).IsMoving;
    }

    public void Die()
    {
        if (!TryGetPlayerEntity(out Entity entity, out EntityManager em))
            return;

        StopMovement();

        PlayerComponent playerData = em.GetComponentData<PlayerComponent>(entity);
        playerData.State = Player.PlayerState.Dead;
        em.SetComponentData(entity, playerData);
    }

    public void Revive()
    {
        if (!TryGetPlayerEntity(out Entity entity, out EntityManager em))
            return;

        PlayerComponent playerData = em.GetComponentData<PlayerComponent>(entity);
        playerData.State = Player.PlayerState.Alive;
        em.SetComponentData(entity, playerData);
    }

    public bool TryGetPlayerEntity(out Entity entity, out EntityManager em)
    {
        entity = Entity.Null;
        em = default;

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
            return false;

        em = world.EntityManager;

        if (!queryCreated)
        {
            playerQuery = em.CreateEntityQuery(ComponentType.ReadWrite<PlayerComponent>());
            queryCreated = true;
        }

        if (playerQuery.IsEmptyIgnoreFilter)
            return false;

        if (playerQuery.HasSingleton<PlayerComponent>())
        {
            entity = playerQuery.GetSingletonEntity();
            return true;
        }

        using var entities = playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
        if (entities.Length > 0)
        {
            entity = entities[0];
            return true;
        }

        return false;
    }
}
