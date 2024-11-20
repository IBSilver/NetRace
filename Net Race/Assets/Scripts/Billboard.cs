using UnityEngine;

public class Billboard : MonoBehaviour
{
    void LateUpdate()
    {
        // Make the UI face the camera
        transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
                         Camera.main.transform.rotation * Vector3.up);
    }
}
