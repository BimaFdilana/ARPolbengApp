using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.XR.ARSubsystems;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Menu: Tools > Navigasi Kampus > Daftarkan Penanda ke Library
/// Memasukkan semua gambar di Assets/ImageTracking/PenandaNavigasi ke ReferenceImageLibrary
/// yang dipakai CampusMapScene. Nama gambar = nama file = KampusAnchor.imageName di prefab.
/// Aman dijalankan berulang: gambar yang sudah terdaftar dilewati, entri lama (mis. Target_Lobi) tidak disentuh.
/// </summary>
public static class DaftarkanPenandaNavigasi
{
    const string LibraryPath = "Assets/ImageTracking/ReferenceImageLibrary.asset";
    const string ImageFolder = "Assets/ImageTracking/PenandaNavigasi";

    // Ukuran cetak penanda (meter). Harus sama dengan ukuran kertas di PenandaCetak/Penanda_Navigasi_Polbeng.pdf.
    const float SizeMeters = 0.18f;

    [MenuItem("Tools/Navigasi Kampus/Daftarkan Penanda ke Library")]
    public static void Daftarkan() => Jalankan(true);

    /// <summary>Untuk terminal: Unity -batchmode -quit -projectPath . -executeMethod DaftarkanPenandaNavigasi.DaftarkanDariCommandLine</summary>
    public static void DaftarkanDariCommandLine()
    {
        EditorApplication.Exit(Jalankan(false) ? 0 : 1);
    }

    static bool Jalankan(bool tampilkanDialog)
    {
        var library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LibraryPath);
        if (library == null)
        {
            Debug.LogError("[Penanda] Library tidak ditemukan: " + LibraryPath);
            if (tampilkanDialog) EditorUtility.DisplayDialog("Daftarkan Penanda", "Library tidak ditemukan:\n" + LibraryPath, "OK");
            return false;
        }

        var sudahAda = new HashSet<string>();
        for (int i = 0; i < library.count; i++) sudahAda.Add(library[i].name);

        var ditambah = new List<string>();
        var dilewati = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ImageFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string nama = Path.GetFileNameWithoutExtension(path);
            if (sudahAda.Contains(nama)) { dilewati.Add(nama); continue; }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) continue;

            library.Add();
            int idx = library.count - 1;
            library.SetName(idx, nama);
            library.SetTexture(idx, tex, true);
            library.SetSpecifySize(idx, true);
            library.SetSize(idx, new Vector2(SizeMeters, SizeMeters));
            ditambah.Add(nama);
        }

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();

        string msg = $"Ditambahkan: {ditambah.Count}\n{string.Join("\n", ditambah)}" +
                     $"\n\nSudah ada (dilewati): {dilewati.Count}" +
                     $"\n\nTotal gambar di library: {library.count}" +
                     $"\nUkuran terdaftar: {SizeMeters:F2} x {SizeMeters:F2} m";
        Debug.Log("[Penanda] " + msg.Replace("\n", " | "));
        if (tampilkanDialog) EditorUtility.DisplayDialog("Daftarkan Penanda", msg, "OK");
        return true;
    }
}
