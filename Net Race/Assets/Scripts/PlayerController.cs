using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float speed = 5f; // Velocidad de movimiento
    public float jumpHeight = 2f; // Altura del salto
    public float gravity = -9.81f; // Gravedad
    public float rotationSpeed = 10f; // Suavizado al rotar el jugador

    private Vector3 velocity; // Velocidad vertical
    private CharacterController controller; // Componente CharacterController
    private Transform cameraTransform; // Transform de la cámara principal
    private bool isGrounded; // Estado de contacto con el suelo
    private float groundCheckOffset = 0.1f; // Offset para detección de suelo

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cameraTransform = Camera.main.transform; // Obtener la cámara principal
    }

    void Update()
    {
        // Verificar si está en el suelo usando la propiedad del CharacterController
        isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Mantener al jugador pegado al suelo
        }

        // Obtener entrada del jugador
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // Calcular la dirección de movimiento relativa a la cámara
        Vector3 move = Vector3.zero; // Inicializar el movimiento

        if (horizontal != 0 || vertical != 0)
        {
            move = cameraTransform.right * horizontal + cameraTransform.forward * vertical;
            move.y = 0; // Ignorar el eje Y
            move = move.normalized; // Normalizar para evitar que diagonales sean más rápidas

            // Mover al jugador
            controller.Move(move * speed * Time.deltaTime);

            // Rotar al jugador hacia la dirección de movimiento
            Quaternion targetRotation = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Saltar
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Aplicar gravedad
        velocity.y += gravity * Time.deltaTime;

        // Aplicar movimiento vertical
        controller.Move(velocity * Time.deltaTime);

        // Ajustar isGrounded manualmente para pendientes y superficies no perfectamente planas
        if (!isGrounded && controller.velocity.magnitude < 0.1f)
        {
            Vector3 groundCheckPos = transform.position + Vector3.down * groundCheckOffset;
            if (Physics.Raycast(groundCheckPos, Vector3.down, groundCheckOffset + 0.1f))
            {
                isGrounded = true;
            }
        }
    }

    // Visualización del rango de detección del suelo (opcional)
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position + Vector3.down * groundCheckOffset, Vector3.down * 0.1f);
    }
}
