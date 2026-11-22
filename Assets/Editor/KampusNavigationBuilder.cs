using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>
/// Menu: Tools > Navigasi Kampus
///  1. "Bangun Prefab Navigasi Kampus"  : membuat Assets/Navigasi/KampusNavigasi.prefab dari model + daftar ruangan.
///  2. "Pakai Prefab di Scene yang Terbuka": memasang prefab tersebut ke komponen NewIndoorNav di scene yang sedang dibuka.
/// Jalankan ulang menu 1 setiap kali model atau ruangan_kampus.json diperbarui.
/// </summary>
public static class KampusNavigationBuilder
{
    const string ModelPath = "Assets/Models/GedungBaru/KampusPolbeng.obj";
    const string JsonPath = "Assets/Models/GedungBaru/ruangan_kampus.json";
    const string OldPrefabPath = "Assets/Prefabs/TrackedImage.prefab";
    const string StarPath = "Assets/Models/estrellica.obj";
    const string OutDir = "Assets/Navigasi";
    const string PrefabPath = OutDir + "/KampusNavigasi.prefab";
    const string NavDataPath = OutDir + "/KampusNavMesh.asset";

    [Serializable] class Ruang { public string nama; public string gedung; public int lantai; public float[] posisi; }
    [Serializable] class DataRuang { public Ruang[] ruangan; }

    struct AnchorDef
    {
        public string image; public Vector3 pos; public float yaw; public bool lama; public string ket;
        public AnchorDef(string i, Vector3 p, float y, bool l, string k) { image = i; pos = p; yaw = y; lama = l; ket = k; }
    }

    // Posisi gambar penanda di model (meter). Sesuaikan dengan lokasi tempel yang sebenarnya.
    static readonly AnchorDef[] Anchors =
    {
        new AnchorDef("Target_Lobi", new Vector3(-70.81f, 0f, -45.86f), 0f, true,
            "Gambar lama di lobi utara Gedung TI. Memakai cara hitung versi lama (lantai = tinggi gambar)."),
        new AnchorDef("Target_GedungTI", new Vector3(-74f, 1.5f, -40.25f), 180f, false, "Dinding luar Gedung TI, di samping pintu utara (menghadap jalan utama)."),
        new AnchorDef("Target_GedungElektro", new Vector3(35f, 1.5f, -40.2f), 180f, false, "Dinding luar Gedung Teknik Elektro (termasuk Perpustakaan), sisi yang menghadap jalan utama, dekat pintu masuk."),
        new AnchorDef("Target_GKT4", new Vector3(-21.85f, 1.5f, 22.5f), -90f, false, "Dinding ujung timur GKT 4, di samping pintu utama."),
        new AnchorDef("Target_BAAK", new Vector3(3f, 1.5f, 24.85f), 0f, false, "Fasad depan BAAK, di samping pintu utama."),
        new AnchorDef("Target_Bahasa", new Vector3(21.85f, 1.5f, 22.5f), 90f, false, "Dinding ujung barat Gedung Bahasa, di samping pintu utama."),
        new AnchorDef("Target_GedungC", new Vector3(5f, 1.5f, 49.85f), 0f, false, "Dinding depan blok tengah Gedung C, di samping pintu lobi."),
        new AnchorDef("Target_Mesin", new Vector3(-105.85f, 1.5f, 24f), -90f, false, "Dinding timur Gedung Mesin, di samping pintu."),
    };

    [MenuItem("Tools/Navigasi Kampus/1. Bangun Prefab Navigasi Kampus")]
    public static void Build()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Navigasi Kampus", "Menyiapkan model...", 0.1f);
            if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets", "Navigasi");

            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) { Fail("Model tidak ditemukan: " + ModelPath); return; }
            bool changed = false;
            if (!importer.isReadable) { importer.isReadable = true; changed = true; }
            if (importer.globalScale != 1f) { importer.globalScale = 1f; changed = true; }
            if (changed) importer.SaveAndReimport();

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
            if (modelAsset == null || json == null) { Fail("Model atau ruangan_kampus.json tidak ditemukan di Assets/Models/GedungBaru."); return; }
            var data = JsonUtility.FromJson<DataRuang>(json.text);

            var root = new GameObject("KampusNavigasi");
            var model = (GameObject)Object.Instantiate(modelAsset, root.transform);
            model.name = "Model";

            // --- cek skala & arah impor (Gedung C: tangga di X ~ +10.75, Z ~ +61)
            float scale = 1f;
            var tangga = model.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name.Contains("GedungC_Tangga"));
            var all = model.GetComponentsInChildren<Renderer>();
            Bounds b = all[0].bounds; foreach (var r in all) b.Encapsulate(r.bounds);
            if (b.size.x < 25f) scale = 100f; else if (b.size.x > 2500f) scale = 0.01f;
            model.transform.localScale = Vector3.one * scale;
            if (tangga != null)
            {
                Vector3 c = tangga.bounds.center;
                if (c.x < 0f)
                    Debug.LogError("[Navigasi Kampus] Model terimpor terbalik kiri-kanan (tangga Gedung C ada di X negatif). Laporkan ini supaya file model diperbaiki.");
            }

            // --- layer Minimap: model hanya terlihat di minimap, tidak menutupi kamera AR
            int minimapLayer = LayerMask.NameToLayer("Minimap");
            if (minimapLayer >= 0) foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = minimapLayer;

            // --- aturan NavMesh per objek
            foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
            {
                if (child == model.transform) continue;
                string n = child.name;
                if (n.Contains("Atap"))
                    child.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                else if (n.Contains("Dinding") || n.Contains("Sekat"))
                {
                    var m = child.gameObject.AddComponent<NavMeshModifier>();
                    m.overrideArea = true; m.area = 1; // Not Walkable: puncak dinding tidak bisa dilalui
                }
            }

            EditorUtility.DisplayProgressBar("Navigasi Kampus", "Membangun NavMesh (bisa beberapa detik)...", 0.4f);
            var surface = root.AddComponent<NavMeshSurface>();
            surface.agentTypeID = 0;
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.layerMask = ~0;
            surface.BuildNavMesh();
            if (surface.navMeshData == null) { Object.DestroyImmediate(root); Fail("NavMesh gagal dibangun."); return; }
            AssetDatabase.DeleteAsset(NavDataPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavDataPath);

            // --- titik tujuan
            EditorUtility.DisplayProgressBar("Navigasi Kampus", "Membuat titik tujuan...", 0.7f);
            var tujuanRoot = new GameObject("Tujuan").transform; tujuanRoot.SetParent(root.transform, false);
            var star = AssetDatabase.LoadAssetAtPath<GameObject>(StarPath);
            int ok = 0, tidakTerjangkau = 0;
            foreach (var r in data.ruangan)
            {
                if (r.nama.StartsWith("Selasar")) continue;
                var go = new GameObject(r.nama);
                go.transform.SetParent(tujuanRoot, false);
                Vector3 p = new Vector3(r.posisi[0], r.posisi[1], r.posisi[2]) * scale;
                if (NavMesh.SamplePosition(root.transform.TransformPoint(p), out var hit, 3f, NavMesh.AllAreas))
                { p = root.transform.InverseTransformPoint(hit.position); ok++; }
                else { tidakTerjangkau++; Debug.LogWarning($"[Navigasi Kampus] '{r.nama}' tidak berada di atas NavMesh."); }
                go.transform.localPosition = p;
                var nt = go.AddComponent<NavigationTarget>();
                nt.displayName = r.nama; nt.gedung = r.gedung; nt.lantai = r.lantai;

                GameObject vis;
                if (star != null)
                {
                    vis = (GameObject)Object.Instantiate(star, go.transform);
                    var rs = vis.GetComponentsInChildren<Renderer>();
                    if (rs.Length > 0)
                    {
                        Bounds sb = rs[0].bounds; foreach (var rr in rs) sb.Encapsulate(rr.bounds);
                        float s = 0.6f / Mathf.Max(0.001f, Mathf.Max(sb.size.x, sb.size.y, sb.size.z));
                        vis.transform.localScale = Vector3.one * s;
                    }
                }
                else
                {
                    vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.DestroyImmediate(vis.GetComponent<Collider>());
                    vis.transform.SetParent(go.transform, false);
                    vis.transform.localScale = Vector3.one * 0.4f;
                }
                vis.name = "Penanda";
                vis.transform.localPosition = new Vector3(0f, 1.3f, 0f);
                vis.AddComponent<SpinObject>();
            }

            // --- titik penanda gambar
            var anchorRoot = new GameObject("PenandaGambar").transform; anchorRoot.SetParent(root.transform, false);
            foreach (var a in Anchors)
            {
                var go = new GameObject("Anchor_" + a.image);
                go.transform.SetParent(anchorRoot, false);
                go.transform.localPosition = new Vector3(a.pos.x * scale, a.pos.y, a.pos.z * scale);
                go.transform.localRotation = Quaternion.Euler(0f, a.yaw, 0f);
                var ka = go.AddComponent<KampusAnchor>();
                ka.imageName = a.image; ka.modeLama = a.lama; ka.keterangan = a.ket;
            }

            // --- minimap: ambil kamera minimap & ikon pemain dari prefab lama
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(OldPrefabPath);
            if (old != null)
            {
                var tmp = (GameObject)Object.Instantiate(old);
                foreach (var nm in new[] { "MinimapCamera", "PlayerIcon" })
                {
                    var t = tmp.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == nm);
                    if (t == null) continue;
                    t.SetParent(root.transform, false);
                    if (nm == "MinimapCamera")
                    {
                        var cam = t.GetComponent<Camera>();
                        if (cam != null)
                        {
                            cam.orthographic = true; cam.orthographicSize = 20f;
                            cam.farClipPlane = 200f;
                            if (minimapLayer >= 0) cam.cullingMask |= 1 << minimapLayer;
                        }
                        t.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        if (t.GetComponent<MinimapFollow>() == null) t.gameObject.AddComponent<MinimapFollow>();
                    }
                }
                Object.DestroyImmediate(tmp);
            }

            EditorUtility.DisplayProgressBar("Navigasi Kampus", "Menyimpan prefab...", 0.9f);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            EditorUtility.ClearProgressBar();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            string msg = $"Prefab dibuat: {PrefabPath}\nTitik tujuan: {ok} (tidak di atas NavMesh: {tidakTerjangkau})\nPenanda gambar: {Anchors.Length}\nSkala model: {scale}";
            Debug.Log("[Navigasi Kampus] " + msg.Replace("\n", " | "));
            EditorUtility.DisplayDialog("Navigasi Kampus", msg + "\n\nLangkah berikutnya: buka CampusMapScene, lalu Tools > Navigasi Kampus > 2. Pakai Prefab di Scene yang Terbuka.", "OK");
        }
        catch (Exception e)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Navigasi Kampus", "Terjadi error: " + e.Message + "\nLihat Console untuk detail.", "OK");
        }
    }

    [MenuItem("Tools/Navigasi Kampus/2. Pakai Prefab di Scene yang Terbuka")]
    public static void AssignToScene()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Fail("Jalankan dulu menu 1 (Bangun Prefab Navigasi Kampus)."); return; }
        var navs = Object.FindObjectsByType<NewIndoorNav>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (navs.Length == 0) { Fail("Tidak ada komponen NewIndoorNav di scene ini. Buka CampusMapScene dulu."); return; }
        foreach (var nav in navs)
        {
            var so = new SerializedObject(nav);
            so.FindProperty("trackedImagePrefab").objectReferenceValue = prefab;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(nav);
        }
        EditorSceneManager.MarkSceneDirty(navs[0].gameObject.scene);
        EditorSceneManager.SaveScene(navs[0].gameObject.scene);
        EditorUtility.DisplayDialog("Navigasi Kampus", $"Prefab kampus dipasang ke {navs.Length} NewIndoorNav dan scene disimpan.\n\nPrefab lama (TrackedImage) tidak dihapus, jadi bisa dikembalikan kapan saja.", "OK");
    }

    static void Fail(string msg)
    {
        EditorUtility.ClearProgressBar();
        Debug.LogError("[Navigasi Kampus] " + msg);
        EditorUtility.DisplayDialog("Navigasi Kampus", msg, "OK");
    }
}
