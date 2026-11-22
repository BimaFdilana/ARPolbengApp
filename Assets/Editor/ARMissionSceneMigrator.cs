using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARSubsystems;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Menu: Tools > Misi AR > Pindahkan ARExploreScene ke AR Foundation
/// Mengganti target Vuforia di ARExploreScene dengan AR Foundation (ARCore):
///  - membuat Reference Image Library dari gambar target lama (Gedung1-4, Image1-7),
///  - menyalin XR Origin + AR Session dari CampusMapScene,
///  - memakai kartu info lama (InfoCanvas) sebagai template,
///  - menghapus objek Vuforia (ARCamera, ImageTarget) dan komponen yang hilang.
/// Aman dijalankan ulang. Scene lama dicadangkan dulu ke Assets/Scenes/Backup.
/// </summary>
public static class ARMissionSceneMigrator
{
    const string ScenePath = "Assets/Scenes/ARExploreScene.unity";
    const string SourceScenePath = "Assets/Scenes/CampusMapScene.unity";
    const string LibraryPath = "Assets/ImageTracking/MisiReferenceImageLibrary.asset";
    const string BackupDir = "Assets/Scenes/Backup";

    static readonly string[] TargetNames =
        { "Gedung1", "Gedung2", "Gedung3", "Gedung4", "Image1", "Image2", "Image3", "Image4", "Image5", "Image6", "Image7" };

    static float WidthFor(string n) => n.StartsWith("Gedung") ? 0.5f : 0.15f;

    [MenuItem("Tools/Misi AR/Pindahkan ARExploreScene ke AR Foundation")]
    public static void Migrate()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // cadangkan scene
        if (!AssetDatabase.IsValidFolder(BackupDir)) AssetDatabase.CreateFolder("Assets/Scenes", "Backup");
        string backup = $"{BackupDir}/ARExploreScene_vuforia.unity";
        if (!System.IO.File.Exists(backup)) AssetDatabase.CopyAsset(ScenePath, backup);

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var roots = scene.GetRootGameObjects().ToList();

        var manager = Object.FindFirstObjectByType<ARMissionController>(FindObjectsInactive.Include);
        if (manager == null) { Fail("ARMissionController tidak ditemukan di ARExploreScene."); return; }

        // ---- 1. kumpulkan target lama
        var builtIns = new List<ARMissionController.BuiltInTarget>();
        var textures = new List<(string name, Texture2D tex, float w)>();
        GameObject cardSource = null; Transform cardTarget = null;
        foreach (var n in TargetNames)
        {
            var go = roots.FirstOrDefault(r => r.name == n);
            Sprite cover = null;
            if (go != null)
            {
                var info = go.GetComponent<BookInfo>();
                if (info != null) cover = info.coverImage;
                if (cardSource == null)
                {
                    var card = go.transform.Find("InfoCanvas");
                    if (card != null && n.StartsWith("Gedung")) { cardSource = card.gameObject; cardTarget = go.transform; }
                }
            }
            Texture2D tex = FindTexture(n) ?? (cover != null ? cover.texture : null);
            if (tex == null) { Debug.LogWarning($"[Misi AR] Gambar untuk {n} tidak ditemukan, dilewati."); continue; }
            builtIns.Add(new ARMissionController.BuiltInTarget { name = n, cover = cover, width = WidthFor(n) });
            textures.Add((n, tex, WidthFor(n)));
        }
        if (manager.builtInTargets != null && manager.builtInTargets.Count > 0 && textures.Count == 0)
        {
            builtIns = manager.builtInTargets; // sudah pernah dimigrasi
        }

        // ---- 2. Reference Image Library
        var lib = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LibraryPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<XRReferenceImageLibrary>();
            AssetDatabase.CreateAsset(lib, LibraryPath);
        }
        if (textures.Count > 0)
        {
            while (lib.count > 0) lib.RemoveAt(0);
            foreach (var (name, tex, w) in textures)
            {
                lib.Add();
                int i = lib.count - 1;
                lib.SetTexture(i, tex, true);
                lib.SetName(i, name);
                lib.SetSpecifySize(i, true);
                float h = w * tex.height / Mathf.Max(1f, tex.width);
                lib.SetSize(i, new Vector2(w, h));
            }
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
        }

        // ---- 3. template kartu info
        if (cardSource != null)
        {
            float w = WidthFor(cardTarget.name);
            Quaternion inv = Quaternion.Inverse(cardTarget.rotation);
            manager.cardOffsetPerMeter = inv * (cardSource.transform.position - cardTarget.position) / w;
            manager.cardRotation = inv * cardSource.transform.rotation;
            manager.cardScalePerMeter = cardSource.transform.lossyScale / w;

            var old = manager.transform.Find("KartuInfoTemplate");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var tpl = Object.Instantiate(cardSource, manager.transform);
            tpl.name = "KartuInfoTemplate";
            tpl.transform.localPosition = Vector3.zero;
            tpl.transform.localRotation = Quaternion.identity;
            tpl.transform.localScale = Vector3.one;
            tpl.SetActive(false);
            manager.cardTemplate = tpl;
        }

        // ---- 4. hapus objek Vuforia
        foreach (var n in TargetNames.Concat(new[] { "ARCamera" }))
        {
            var go = scene.GetRootGameObjects().FirstOrDefault(r => r.name == n);
            if (go != null) Object.DestroyImmediate(go);
        }
        foreach (var r in scene.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

        // ---- 5. XR Origin + AR Session dari CampusMapScene
        GameObject origin = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "XR Origin");
        GameObject session = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "AR Session");
        if (origin == null || session == null)
        {
            var src = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            foreach (var srcRoot in src.GetRootGameObjects())
            {
                if (srcRoot.name == "XR Origin" && origin == null) { origin = Object.Instantiate(srcRoot); origin.name = "XR Origin"; }
                if (srcRoot.name == "AR Session" && session == null) { session = Object.Instantiate(srcRoot); session.name = "AR Session"; }
            }
            if (origin != null) SceneManager.MoveGameObjectToScene(origin, scene);
            if (session != null) SceneManager.MoveGameObjectToScene(session, scene);
            EditorSceneManager.CloseScene(src, true);
        }
        if (origin == null || session == null) { Fail("XR Origin / AR Session tidak ditemukan di CampusMapScene."); return; }
        origin.SetActive(false); session.SetActive(false);

        var tim = origin.GetComponentInChildren<ARTrackedImageManager>(true) ?? origin.AddComponent<ARTrackedImageManager>();
        tim.referenceLibrary = lib;
        tim.trackedImagePrefab = null;
        tim.requestedMaxNumberOfMovingImages = 1;
        EditorUtility.SetDirty(tim);

        manager.arSessionObject = session;
        manager.xrOriginObject = origin;
        manager.trackedImageManager = tim;
        manager.builtInTargets = builtIns;
        EditorUtility.SetDirty(manager);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("Misi AR",
            $"ARExploreScene sudah memakai AR Foundation.\n\nTarget bawaan: {builtIns.Count}\nLibrary: {LibraryPath}\nCadangan scene lama: {backup}\n\nSilakan build ke HP dan coba misi pencarian gedung.", "OK");
    }

    static Texture2D FindTexture(string name)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Image" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.Equals(System.IO.Path.GetFileNameWithoutExtension(path), name, System.StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        return null;
    }

    static void Fail(string msg)
    {
        Debug.LogError("[Misi AR] " + msg);
        EditorUtility.DisplayDialog("Misi AR", msg, "OK");
    }
}
