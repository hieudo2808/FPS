using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private CharacterController controller;
    [SerializeField] private float speed = 5f; // Tốc độ di chuyển của người chơi
    [SerializeField] private float walkMultiplier = 2f; // Hệ số giảm tốc độ khi nhấn Shift
    [SerializeField] private float jumpHeight = 0.5f; // Chiều cao nhảy của người chơi
    [SerializeField] private float gravityScale = 1f; // Tỷ lệ trọng lực, có thể điều chỉnh để tăng giảm ảnh hưởng của trọng lực
    [SerializeField] private Animator characterAnimation;

    private float gravity = -9.81f; // Lực hấp dẫn
    private Vector3 velocity; // Véc tơ vận tốc của người chơi
    private bool isGrounded; // Kiểm tra xem người chơi có đang đứng trên mặt đất hay không
    private Transform groundCheck;

    bool isStop = false;

    private void Start()
    {
        characterAnimation.SetFloat("Speed", speed);

        // tạo 1 điểm check chạm đất (dưới chân player)
        groundCheck = new GameObject("GroundCheck").transform;
        groundCheck.SetParent(transform);
        groundCheck.localPosition = Vector3.down * 1f;
    }

    private void Update()
    {
        //isGrounded = controller.isGrounded; // Sử dụng CharacterController để kiểm tra mặt đất
        isGrounded = Physics.CheckSphere(transform.position, 0.4f, LayerMask.GetMask("Ground")); // Kiểm tra mặt đất bằng CheckSphere
        characterAnimation.SetBool("Grounded", isGrounded);

        if (isStop)
        {
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = 1f;
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            bool temp = isStop;
            isStop = !temp;
        }

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
            characterAnimation.SetBool("FreeFall", false);
            characterAnimation.SetBool("Jump", false);
        }

        // Get input from WASD keys
        float moveX = Input.GetAxis("Horizontal"); // A/D keys
        float moveZ = Input.GetAxis("Vertical"); // W/S keys

        float currentSpeed = speed;
        if (Input.GetKey(KeyCode.LeftShift))
        {
            currentSpeed /= walkMultiplier; // Giảm tốc độ khi nhấn Shift
        }

        // Create a movement vector based on input
        Vector3 move = transform.right * moveX + transform.forward * moveZ;

        // Move the player using CharacterController
        controller.Move(move * currentSpeed * Time.deltaTime);

        // Gán giá trị Speed cho Blend Tree (Idle, Walk, Run)
        float moveMagnitude = new Vector2(moveX, moveZ).magnitude;
        characterAnimation.SetFloat("Speed", moveMagnitude * currentSpeed);

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity); // Tính vận tốc nhảy dựa trên chiều cao nhảy
            characterAnimation.SetBool("Jump", true);
        }

        // Apply gravity to the player
        velocity.y += gravity * Time.deltaTime * gravityScale;
        controller.Move(velocity * Time.deltaTime); // Cập nhật vận tốc của người chơi

        // Nếu rơi tự do
        if (!isGrounded && velocity.y < -2f)
        {
            characterAnimation.SetBool("FreeFall", true);
        }
    }
}
