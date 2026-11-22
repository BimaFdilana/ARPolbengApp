using UnityEngine;

/// <summary>
/// Titik tujuan navigasi. Field tambahan bersifat opsional, jadi prefab lama (TrackedImage) tetap berfungsi.
/// </summary>
public class NavigationTarget : MonoBehaviour
{
    [Tooltip("Nama yang tampil di dropdown. Kosongkan untuk memakai nama GameObject.")]
    public string displayName;

    [Tooltip("Nama gedung, misalnya 'Gedung C'.")]
    public string gedung;

    [Tooltip("Lantai ruangan (1, 2, ...).")]
    public int lantai = 1;

    public string Label => string.IsNullOrEmpty(displayName) ? name : displayName;
}
