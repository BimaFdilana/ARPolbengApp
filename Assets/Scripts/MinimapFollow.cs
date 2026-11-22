using UnityEngine;

/// <summary>
/// Membuat kamera minimap selalu berada tepat di atas pengguna, karena peta kampus jauh lebih besar dari layar minimap.
/// </summary>
public class MinimapFollow : MonoBehaviour
{
    [Tooltip("Kosongkan: otomatis memakai kamera AR (Camera.main).")]
    public Transform target;

    [Tooltip("Ketinggian kamera minimap di atas pengguna (meter).")]
    public float height = 40f;

    [Tooltip("Putar peta mengikuti arah hadap pengguna.")]
    public bool ikutiArahHadap = false;

    void LateUpdate()
    {
        if (target == null && Camera.main != null) target = Camera.main.transform;
        if (target == null) return;

        Vector3 p = target.position;
        transform.position = new Vector3(p.x, p.y + height, p.z);
        if (ikutiArahHadap)
            transform.rotation = Quaternion.Euler(90f, target.eulerAngles.y, 0f);
    }
}
