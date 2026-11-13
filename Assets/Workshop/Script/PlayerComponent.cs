using Unity.Entities;
using Unity.Mathematics;

public struct PlayerComponent : IComponentData
{
    public Player.PlayerState State;

    public float MoveSpeed;
    public float RotationSpeed;
    public float StoppingDistance;

    public float3 TargetPosition;
    public bool HasTarget;
    public bool IsMoving;
}

public struct PlayerPhysicsStabilizer : IComponentData { }
