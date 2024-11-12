using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    // Variables de movimiento
    public float speed = 5f;
    public float jumpHeight = 2f;
    public float gravity = -9.81f;
    public float rotationSpeed = 10f; // Velocidad de rotaci�n para suavizar el giro

    // Variables internas
    private Vector3 velocity;
    private bool isGrounded;
    private CharacterController controller;

    void Start()
    {
        // Obtener el componente CharacterController
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Verificar si el jugador est� en el suelo
        isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0)
        {
            // Restablecer la velocidad de ca�da si estamos en el suelo
            velocity.y = -2f;
        }

        // Movimiento horizontal (relativo a la c�mara)
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        // Crear un vector de movimiento en relaci�n con el mundo (eje global)
        Vector3 move = new Vector3(horizontalInput, 0, verticalInput).normalized;

        // Mover al jugador (el tiempo delta asegura una velocidad constante en diferentes FPS)
        controller.Move(move * speed * Time.deltaTime);

        // Cambiar la rotaci�n del personaje para que mire en la direcci�n del movimiento
        if (move != Vector3.zero)
        {
            // Determinar la rotaci�n objetivo
            Quaternion targetRotation = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Saltar
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            // Calcular la velocidad de salto basada en la altura deseada
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Aplicar gravedad
        velocity.y += gravity * Time.deltaTime;

        // Aplicar movimiento vertical (gravedad y salto)
        controller.Move(velocity * Time.deltaTime);
    }
}
