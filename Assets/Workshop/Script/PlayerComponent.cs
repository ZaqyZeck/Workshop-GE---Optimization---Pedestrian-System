using Unity.Entities;
using Unity.Mathematics;

public struct PlayerComponentConfig : IComponentData
{
    // stat yang sudah ditetapkan
    public float moveSpeed ;
    public float rotationSpeed;
    public float stoppingDistance;
}
public struct PlayerComponentState : IComponentData
{
    // attribut yang akan berubah
    public float3 TargetPosition;
    public bool HasTarget;
    public bool IsMoving;
}
