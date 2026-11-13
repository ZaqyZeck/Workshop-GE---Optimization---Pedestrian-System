using System;
//using Unity.Physics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    public event Action<Ray> OnMouseClick, OnMouseHold;
    public event Action OnMouseUp, OnEscape;
    private Vector2 mouseMovementVector = Vector2.zero;
    public Vector2 CameraMovementVector { get => mouseMovementVector; }
    [SerializeField]
    Camera mainCamera;

    [Header("RaycastAttribut")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private string groundTag = "Ground";
    [SerializeField] private float raycastDistance = 500f;

    private void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        CheckClickDownEvent();
        CheckClickHoldEvent();
        CheckClickUpEvent();
        CheckArrowInput();
        CheckEscClick();
    }

    private void CheckClickHoldEvent()
    {
        if (Mouse.current.leftButton.isPressed && EventSystem.current.IsPointerOverGameObject() == false)
        {

            OnMouseHold?.Invoke(mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue()));
        }
    }

    private void CheckClickUpEvent()
    {
        if (Mouse.current.leftButton.wasReleasedThisFrame && EventSystem.current.IsPointerOverGameObject() == false)
        {
            OnMouseUp?.Invoke();
        }
    }

    private void CheckClickDownEvent()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame && EventSystem.current.IsPointerOverGameObject() == false)
        {
            OnMouseClick?.Invoke(mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue()));
        }
    }

    private void CheckEscClick()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            OnEscape.Invoke();
        }
    }

    private void CheckArrowInput()
    {
        mouseMovementVector = new Vector2(
            (Keyboard.current.dKey.isPressed ? 1 : 0) - (Keyboard.current.aKey.isPressed ? 1 : 0),
            (Keyboard.current.wKey.isPressed ? 1 : 0) - (Keyboard.current.sKey.isPressed ? 1 : 0)
        );
    }


    public Vector3? GetMousePosition()
    {
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.value);

        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            raycastDistance,
            groundLayer
        );

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.CompareTag(groundTag))
            {
                return hit.point;
            }
        }

        return null;
    }



    public void ClearEvents()
    {
        OnMouseClick = null;
        OnMouseHold = null;
        OnEscape = null;
        OnMouseUp = null;
    }
}
