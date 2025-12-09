using UnityEngine;

namespace FPS
{    
    public class MouseMovement : MonoBehaviour
    {
        [SerializeField] private float mouseSensivity = 100f;
        [SerializeField] private float bottomRotationLimit = 90f;
        [SerializeField] private float topRotationLimit = -90f;
        [SerializeField] private Camera bodyCam;

        private float xRotation = 0f; // Lưu trữ góc quay quanh trục X
        private float yRotation = 0f; // Lưu trữ góc quay quanh trục Y

        private void Update()
        {
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                Cursor.lockState = CursorLockMode.None;
                return;
            }
            else Cursor.lockState = CursorLockMode.Locked;
            // Get mouse input
            float mouseX = Input.GetAxis("Mouse X") * mouseSensivity * Time.deltaTime; // trục chuột quay ngang
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensivity * Time.deltaTime; // trục chuột quay dọc

            // Lỗi do eulerAngles dùng khoảng [0, 360), giả sử quay -30 -> 330, chuẩn hóa về 90 => sai

            xRotation -= mouseY; // Quay quanh trục ngang -> di chuyển theo trục dọc
            xRotation = Mathf.Clamp(xRotation, bottomRotationLimit, topRotationLimit);

            yRotation += mouseX; // Quay quanh trục dọc

            bodyCam.transform.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f); // Quay camera theo trục dọc
            transform.localRotation = Quaternion.Euler(0, yRotation, 0f);
        }
    }
}