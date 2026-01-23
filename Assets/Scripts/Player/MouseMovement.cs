using UnityEngine;

namespace FPS
{    
    public class MouseMovement : MonoBehaviour
    {
        public static MouseMovement Instance { get; private set; }
        
        [Header("Sensitivity")]
        [SerializeField] private float mouseSensitivity = 100f;
        [SerializeField] private float minSensitivity = 10f;
        [SerializeField] private float maxSensitivity = 500f;
        
        [Header("Rotation Limits")]
        [SerializeField] private float bottomRotationLimit = 90f;
        [SerializeField] private float topRotationLimit = -90f;
        
        [Header("References")]
        [SerializeField] private Camera bodyCam;

        private float xRotation = 0f;
        private float yRotation = 0f;

        public float Sensitivity => mouseSensitivity;
        public float MinSensitivity => minSensitivity;
        public float MaxSensitivity => maxSensitivity;

        private void Awake()
        {
            Instance = this;
            
            if (PlayerPrefs.HasKey("MouseSensitivity"))
            {
                mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity");
            }
        }

        private void Update()
        {
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                Cursor.lockState = CursorLockMode.None;
                return;
            }
            else Cursor.lockState = CursorLockMode.Locked;
            
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, bottomRotationLimit, topRotationLimit);

            yRotation += mouseX;

            bodyCam.transform.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.localRotation = Quaternion.Euler(0, yRotation, 0f);
        }

        public void SetSensitivity(float newSensitivity)
        {
            mouseSensitivity = Mathf.Clamp(newSensitivity, minSensitivity, maxSensitivity);
            PlayerPrefs.SetFloat("MouseSensitivity", mouseSensitivity);
            PlayerPrefs.Save();
        }

        public void SetSensitivityNormalized(float normalized01)
        {
            float newSens = Mathf.Lerp(minSensitivity, maxSensitivity, normalized01);
            SetSensitivity(newSens);
        }

        public float GetSensitivityNormalized()
        {
            return Mathf.InverseLerp(minSensitivity, maxSensitivity, mouseSensitivity);
        }
    }
}