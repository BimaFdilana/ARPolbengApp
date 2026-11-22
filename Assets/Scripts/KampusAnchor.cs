using UnityEngine;

/// <summary>
/// Menandai letak satu gambar penanda (image target) di dalam model kampus.
/// Saat gambar dengan nama yang sama terdeteksi kamera, model kampus diposisikan
/// sehingga titik ini tepat berada di posisi gambar di dunia nyata.
///
/// Aturan arah (sumbu biru / forward):
/// - Gambar ditempel di DINDING : forward menunjuk MASUK ke dinding (searah pandangan orang yang menghadap gambar).
/// - Gambar diletakkan MENDATAR  : forward menunjuk ke sisi ATAS gambar.
/// Tinggi (Y) = tinggi pusat gambar dari lantai, dalam meter.
/// </summary>
public class KampusAnchor : MonoBehaviour
{
    [Tooltip("Harus sama persis dengan nama gambar di Reference Image Library.")]
    public string imageName;

    [Tooltip("Centang untuk gambar lama (Target_Lobi) yang memakai cara hitung arah versi lama.")]
    public bool modeLama;

    [TextArea] public string keterangan;

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.8f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.6f, 0.6f, 0.02f));
        Gizmos.DrawLine(Vector3.zero, Vector3.forward * 1.5f);
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawLine(transform.position, new Vector3(transform.position.x, transform.position.y - transform.localPosition.y, transform.position.z));
    }
}
