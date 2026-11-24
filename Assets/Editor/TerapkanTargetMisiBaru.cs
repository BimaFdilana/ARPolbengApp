using System.Collections.Generic;
using UnityEditor;
using UnityEditor.XR.ARSubsystems;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Menukar gambar target misi bawaan di MisiReferenceImageLibrary dengan versi crop yang skor ARCore-nya lebih baik.
/// Nama entri dan ukuran fisik TIDAK diubah, jadi misi di server (target_image_name) dan scene tetap cocok.
/// Menu: Tools > Navigasi Kampus > Terapkan Target Misi Baru
/// Terminal: Unity -batchmode -quit -projectPath . -executeMethod TerapkanTargetMisiBaru.DariCommandLine
/// </summary>
public static class TerapkanTargetMisiBaru
{
    const string LibraryPath = "Assets/ImageTracking/MisiReferenceImageLibrary.asset";

    // nama entri di library -> gambar pengganti
    static readonly Dictionary<string, string> Pengganti = new Dictionary<string, string>
    {
        { "Gedung1", "Assets/ImageTracking/TargetMisi/Gedung1_baru.png" },
    };

    [MenuItem("Tools/Navigasi Kampus/Terapkan Target Misi Baru")]
    public static void Terapkan() => Jalankan(true);

    public static void DariCommandLine() => EditorApplication.Exit(Jalankan(false) ? 0 : 1);

    static bool Jalankan(bool dialog)
    {
        var lib = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LibraryPath);
        if (lib == null) return Gagal("Library tidak ditemukan: " + LibraryPath, dialog);

        var log = new List<string>();
        foreach (var kv in Pengganti)
        {
            int idx = -1;
            for (int i = 0; i < lib.count; i++) if (lib[i].name == kv.Key) { idx = i; break; }
            if (idx < 0) return Gagal($"Entri '{kv.Key}' tidak ada di library.", dialog);

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(kv.Value);
            if (tex == null) return Gagal("Gambar pengganti tidak ditemukan/belum terimpor: " + kv.Value, dialog);

            Vector2 ukuran = lib[idx].size;          // simpan ukuran fisik lama
            bool specify = lib[idx].specifySize;
            lib.SetTexture(idx, tex, true);
            lib.SetSpecifySize(idx, specify);
            if (specify) lib.SetSize(idx, ukuran);   // pastikan tidak berubah
            log.Add($"{kv.Key} -> {kv.Value} (ukuran tetap {ukuran.x:F3} x {ukuran.y:F3} m)");
        }

        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        string msg = "Diterapkan: " + string.Join(" | ", log);
        Debug.Log("[TargetMisi] " + msg);
        if (dialog) EditorUtility.DisplayDialog("Target Misi", msg.Replace(" | ", "\n"), "OK");
        return true;
    }

    static bool Gagal(string pesan, bool dialog)
    {
        Debug.LogError("[TargetMisi] " + pesan);
        if (dialog) EditorUtility.DisplayDialog("Target Misi", pesan, "OK");
        return false;
    }
}
