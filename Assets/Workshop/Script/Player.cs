
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    public enum PlayerState
    {
        Alive, Dead
    }

    public PlayerState state { get; private set; }

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float stoppingDistance = 0.1f;

    [Header("Animator")]
    [SerializeField] private Animator playerAnimator;

    //private Rigidbody rb;
    private Vector3 targetPosition;
    private bool hasTarget;

    private void Update()
    {
        if (state != PlayerState.Alive) return;

        HandleMouseInput();
        HandleMovement();
        HandleAnimation();
    }

    private void HandleMouseInput()
    {
        if (!Mouse.current.rightButton.wasPressedThisFrame)
            return;

        Vector3? mousePosition = InputManager.Instance.GetMousePosition();

        if (mousePosition != null)
        {
            MoveToPosition((Vector3)mousePosition);
        }
    }

    private void MoveToPosition(Vector3 position)
    {
        targetPosition = new Vector3(position.x, transform.position.y, position.z);
        hasTarget = true;
    }

    private void HandleMovement()
    {
        if (!hasTarget) return;

        targetPosition.y = transform.position.y;
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.magnitude <= stoppingDistance)
        {
            StopMovement();
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime
        );

        Vector3 remaining = targetPosition - transform.position;
        remaining.y = 0f;
        if (remaining.magnitude <= stoppingDistance)
        {
            StopMovement();
        }
    }

    private void HandleAnimation()
    {
        if (playerAnimator == null) return;

        playerAnimator.SetBool("Walk", IsMoving());
    }

    public void StopMovement()
    {
        hasTarget = false;

    }

    public bool IsMoving()
    {
        if (!hasTarget) return false;

        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        return direction.magnitude > stoppingDistance;
    }

    public void Die()
    {
        StopMovement();
        ChangeState(PlayerState.Dead);
    }

    public void Revive()
    {
        ChangeState(PlayerState.Alive);
    }

    private void ChangeState(PlayerState state)
    {
        this.state = state;
    }
}
