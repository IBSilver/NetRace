using UnityEngine;

public class FollowPlayer : MonoBehaviour
{
    public Transform player; // Transform del jugador
    public float rotationSpeed = 100f; // Velocidad de rotaci�n de la c�mara
    public Vector3 offset = new Vector3(0, 4, -8); // Offset inicial de la c�mara

    private float xRot; // Rotaci�n horizontal de la c�mara

    void LateUpdate()
    {
        // Obtener entrada del rat�n para rotaci�n horizontal
        xRot += Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;

        // Rotar alrededor del jugador
        Quaternion rotation = Quaternion.Euler(0, xRot, 0);
        transform.position = player.position + rotation * offset;

        // La c�mara siempre mira al jugador
        transform.LookAt(player.position + Vector3.up * 1.5f); // Ajuste para mirar un poco arriba del centro
    }
}
