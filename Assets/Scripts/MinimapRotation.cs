using UnityEngine;

public class MinimapRotation : MonoBehaviour
{
    [Header("Status (Otomatis Terisi)")]
    public Transform mainCamera; // Tidak perlu diisi manual di Prefab

    [Header("Pengaturan")]
    [Tooltip("Putar slider ini jika arah panah tidak pas dengan garis jalan")]
    [Range(0, 360)]
    public float rotationOffset = 0f; 

    void OnEnable()
    {
        // 1. Coba cari kamera utama via Tag "MainCamera"
        if (Camera.main != null)
        {
            mainCamera = Camera.main.transform;
        }
        // 2. Jika tidak ketemu, cari objek bernama "AR Camera" (Backup)
        else
        {
            GameObject arCam = GameObject.Find("AR Camera");
            if (arCam != null)
            {
                mainCamera = arCam.transform;
            }
            else
            {
                Debug.LogWarning("MinimapRotation: AR Camera tidak ditemukan! Pastikan Tag camera adalah MainCamera.");
            }
        }
    }

    void Update()
    {
        if (mainCamera != null)
        {
            // Ambil rotasi Y kamera + Offset buatan kita
            // Kita gunakan eulerAngles.y agar icon berputar kiri-kanan saja
            float currentY = mainCamera.eulerAngles.y + rotationOffset;
            
            // Terapkan rotasi (X dan Z tetap 0 atau 90 sesuai setup awal Anda)
            // Asumsi: Icon/VisionCone Anda diletakkan mendatar di X=90
            Vector3 currentEuler = transform.rotation.eulerAngles;
            
            // Kita kunci X pada rotasi saat ini (agar tidak tegak berdiri jika aslinya tidur)
            transform.rotation = Quaternion.Euler(currentEuler.x, currentY, 0);
        }
    }
}