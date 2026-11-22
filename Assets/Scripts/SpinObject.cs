using UnityEngine;

public class SpinObject : MonoBehaviour
{
    public float speed = 50.0f;

    void Update()
    {
        // Putar objek pada sumbu Y (Vertikal)
        transform.Rotate(Vector3.up, speed * Time.deltaTime, Space.World);
    }
}