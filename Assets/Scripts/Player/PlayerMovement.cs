using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private CharacterController controller;
    [SerializeField] private float speed = 5f;
    [SerializeField] private float walkMultiplier = 2f;
    [SerializeField] private float jumpHeight = 0.5f;
    [SerializeField] private float gravityScale = 1f;
    [SerializeField] private Animator characterAnimation;

    private float gravity = -9.81f;
    private Vector3 velocity;
    private bool isGrounded;
    private Transform groundCheck;

    bool isStop = false;

    private void Start()
    {
        characterAnimation.SetFloat("Speed", speed);

        groundCheck = new GameObject("GroundCheck").transform;
        groundCheck.SetParent(transform);
        groundCheck.localPosition = Vector3.down * 1f;
    }

    private void Update()
    {
        isGrounded = Physics.CheckSphere(transform.position, 0.4f, LayerMask.GetMask("Ground"));
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
            currentSpeed /= walkMultiplier;
        }

        Vector3 move = transform.right * moveX + transform.forward * moveZ;

        controller.Move(move * currentSpeed * Time.deltaTime);

        float moveMagnitude = new Vector2(moveX, moveZ).magnitude;
        characterAnimation.SetFloat("Speed", moveMagnitude * currentSpeed);

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            characterAnimation.SetBool("Jump", true);
        }

        velocity.y += gravity * Time.deltaTime * gravityScale;
        controller.Move(velocity * Time.deltaTime);

        if (!isGrounded && velocity.y < -2f)
        {
            characterAnimation.SetBool("FreeFall", true);
        }
    }
}
