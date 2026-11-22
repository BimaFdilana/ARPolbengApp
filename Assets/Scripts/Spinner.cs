using UnityEngine;

public class Spinner : MonoBehaviour
{
    // Kecepatan putaran, bisa diatur di Inspector
    public float speed = 200f;

    void Update()
    {
        // Putar object mengelilingi sumbu Z
        transform.Rotate(0, 0, -speed * Time.deltaTime);
    }
}