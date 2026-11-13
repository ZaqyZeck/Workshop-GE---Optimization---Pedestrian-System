//	Created by: Sunny Valley Studio 
//	https://svstudio.itch.io

using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SVS
{

    public class CameraMovement : MonoBehaviour
    {
        public static CameraMovement Instance;
        public Camera gameCamera;
        public float cameraMovementSpeed = 5;

        public float maxOrthographicSize = 5f, minOrthographicSize = 0.5f;
        public float sensitivity = 10;

        [SerializeField] private Transform CameraTarget;
        [SerializeField] private Vector3 CameraPosition;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            gameCamera = GetComponent<Camera>();
            CameraPosition = transform.position;
        }

        public void MoveCamera(Vector3 inputVector)
        {
            //var movementVector = Quaternion.Euler(0,30,0) * inputVector;
            gameCamera.transform.position = inputVector;
        }

        private void Update()
        {
            var scrollInput = Mouse.current.scroll.y.ReadValue() * sensitivity;
            gameCamera.orthographicSize = Mathf.Clamp(gameCamera.orthographicSize - scrollInput, minOrthographicSize, maxOrthographicSize);

            if(CameraTarget != null)MoveCamera(new Vector3(CameraTarget.position.x, 5, CameraTarget.position.z));
            else
            {
                MoveCamera(CameraPosition);
            }
        }

        public void SetCameraPosition(Vector3 position)
        {
            gameCamera.transform.position = position;
        }
    }
}