using UnityEngine;

public class MinimapMarker : MonoBehaviour
{
    private Transform mainCameraTransform;

    void Start()
    {
        // Cari kamera utama (HP User) otomatis
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }

    void Update()
    {
        if (mainCameraTransform != null)
        {
            // 1. Ikuti Posisi X dan Z player (Posisi Y/Tinggi tetap di lantai peta)
            Vector3 newPos = mainCameraTransform.position;
            newPos.y = transform.position.y; // Jangan ikuti tinggi HP, tetap di lantai
            transform.position = newPos;

            // 2. Ikuti Rotasi Y (Arah hadap) agar panah ikut berputar
            Vector3 newRot = transform.eulerAngles;
            newRot.y = mainCameraTransform.eulerAngles.y;
            transform.rotation = Quaternion.Euler(newRot);
        }
    }
}