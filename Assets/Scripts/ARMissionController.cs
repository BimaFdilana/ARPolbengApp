using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

/// <summary>
/// Misi AR (pencarian gedung) dengan AR Foundation / ARCore, tanpa Vuforia.
/// - Target bawaan (Gedung1-4, Image1-7) ada di Reference Image Library.
/// - Target dari admin: gambar diunduh dari URL lalu ditambahkan ke library saat aplikasi berjalan.
/// Saat target ditemukan, kartu info (judul, deskripsi, tombol Selesai) muncul di atas gambar.
/// </summary>
public class ARMissionController : MonoBehaviour
{
    [Serializable]
    public class BuiltInTarget
    {
        public string name;
        public Sprite cover;
        public float width = 0.5f;
    }

    [Header("AR Foundation")]
    public GameObject arSessionObject;
    public GameObject xrOriginObject;
    public ARTrackedImageManager trackedImageManager;

    [Header("Target bawaan aplikasi")]
    public List<BuiltInTarget> builtInTargets = new List<BuiltInTarget>();

    [Header("Kartu info di atas gambar (diisi otomatis oleh menu migrasi)")]
    public GameObject cardTemplate;
    public Vector3 cardOffsetPerMeter = new Vector3(0f, 0.05f, 0f);
    public Quaternion cardRotation = Quaternion.Euler(90f, 0f, 0f);
    public Vector3 cardScalePerMeter = Vector3.one * 0.002f;

    [Header("UI Feedback")]
    public GameObject feedbackPanel;
    public TextMeshProUGUI feedbackText;
    public GameObject loadingSpinner;
    public GameObject successIcon;
    public GameObject failureIcon;

    [Header("UI Instruksi")]
    public GameObject instructionPanel;
    public Image targetThumbnail;
    public TextMeshProUGUI instructionText;

    private int currentMissionId;
    private string token;
    private Coroutine hideFeedbackCoroutine;

    private string targetName;     // nama gambar yang dicari di library
    private string missionTitle;
    private string missionDesc;
    private bool targetFound;
    private bool finishing;
    public bool XrStartedHere { get; private set; } // true bila XR dinyalakan oleh scene ini

    void Start()
    {
        if (feedbackPanel) feedbackPanel.SetActive(false);
        if (instructionPanel) instructionPanel.SetActive(false);
        if (cardTemplate) cardTemplate.SetActive(false);
        if (arSessionObject) arSessionObject.SetActive(false);
        if (xrOriginObject) xrOriginObject.SetActive(false);

        currentMissionId = PlayerPrefs.GetInt("active_mission_id", 0);
        token = PlayerPrefs.GetString("user_token");
        if (currentMissionId == 0)
        {
            ShowFeedback("Tidak ada misi aktif.", "failure");
            return;
        }
        StartCoroutine(Run());
    }

    void OnEnable() { if (trackedImageManager) trackedImageManager.trackablesChanged.AddListener(OnTrackablesChanged); }
    void OnDisable() { if (trackedImageManager) trackedImageManager.trackablesChanged.RemoveListener(OnTrackablesChanged); }
    void OnDestroy() { StopXR(); }

    IEnumerator Run()
    {
        ShowFeedback("Menyiapkan misi...", "loading");

        // 1. Ambil detail misi dari server
        var req = UnityWebRequest.Get(ApiConfig.BaseUrl + "/missions/" + currentMissionId);
        req.SetRequestHeader("Authorization", "Bearer " + token);
        req.SetRequestHeader("Accept", "application/json");
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowFeedback("Gagal memuat data misi", "failure");
            yield return new WaitForSeconds(2f);
            SceneManager.LoadScene("MissionScene");
            yield break;
        }

        JObject data = JObject.Parse(req.downloadHandler.text);
        string type = Str(data["type"]);
        missionTitle = Str(data["title"]);
        missionDesc = Str(data["description"]);
        string builtInName = Str(data["target_image_name"]);
        string targetUrl = Str(data["target_image_url"]);
        string coverUrl = Str(data["cover_image_url"]);
        float width = data["target_width"] != null && data["target_width"].Type != JTokenType.Null ? (float)data["target_width"] : 0.5f;

        // 2. Nyalakan AR
        yield return InitializeXR();
        if (trackedImageManager == null) { ShowFeedback("ARTrackedImageManager belum terpasang.", "failure"); yield break; }

        // 3. Siapkan target
        if (!string.IsNullOrEmpty(targetUrl))
        {
            targetName = "misi_" + currentMissionId;
            bool ok = false;
            yield return AddRuntimeTarget(targetUrl, targetName, width, r => ok = r);
            if (!ok) yield break;
        }
        else
        {
            targetName = builtInName;
            if (string.IsNullOrEmpty(targetName) || FindBuiltIn(targetName) == null)
                Debug.LogWarning($"[ARMission] Target bawaan '{targetName}' tidak ada di daftar builtInTargets. Pastikan namanya sama dengan Reference Image Library.");
        }

        // 4. Panel instruksi
        Sprite cover = FindBuiltIn(targetName)?.cover;
        if (!string.IsNullOrEmpty(coverUrl))
        {
            var texReq = UnityWebRequestTexture.GetTexture(coverUrl);
            yield return texReq.SendWebRequest();
            if (texReq.result == UnityWebRequest.Result.Success)
            {
                var t = DownloadHandlerTexture.GetContent(texReq);
                cover = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
            }
        }
        ShowInstruction(type, cover);
        ShowFeedback("Arahkan kamera ke penanda gedung", "success");
    }

    // ---------------- XR ----------------
    IEnumerator InitializeXR()
    {
        yield return null;
        var mgr = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (mgr != null)
        {
            if (mgr.activeLoader == null) { yield return mgr.InitializeLoader(); XrStartedHere = true; }
            if (mgr.activeLoader != null) mgr.StartSubsystems();
            else { ShowFeedback("Gagal memuat ARCore.", "failure"); yield break; }
        }
        yield return null;
        if (xrOriginObject) xrOriginObject.SetActive(true);
        yield return null;
        if (arSessionObject) arSessionObject.SetActive(true);
    }

    public void StopXR()
    {
        if (arSessionObject) arSessionObject.SetActive(false);
        if (xrOriginObject) xrOriginObject.SetActive(false);
        var mgr = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (mgr != null && mgr.isInitializationComplete)
        {
            mgr.StopSubsystems();
            mgr.DeinitializeLoader();
        }
    }

    // ---------------- Target dari admin ----------------
    IEnumerator AddRuntimeTarget(string url, string name, float width, Action<bool> done)
    {
        ShowFeedback("Mengunduh gambar target...", "loading");
        var texReq = UnityWebRequestTexture.GetTexture(url, false); // false = tekstur bisa dibaca CPU
        yield return texReq.SendWebRequest();
        if (texReq.result != UnityWebRequest.Result.Success)
        {
            ShowFeedback("Gagal mengunduh gambar target.", "failure");
            done(false); yield break;
        }
        Texture2D tex = DownloadHandlerTexture.GetContent(texReq);

        // gambar hanya bisa ditambahkan setelah sesi AR berjalan
        float timeout = Time.time + 15f;
        while (ARSession.state < ARSessionState.SessionInitializing && Time.time < timeout) yield return null;

        if (!(trackedImageManager.referenceLibrary is MutableRuntimeReferenceImageLibrary))
            trackedImageManager.referenceLibrary = trackedImageManager.CreateRuntimeLibrary(trackedImageManager.referenceLibrary as XRReferenceImageLibrary);

        if (!(trackedImageManager.referenceLibrary is MutableRuntimeReferenceImageLibrary lib))
        {
            ShowFeedback("HP ini tidak mendukung penambahan target.", "failure");
            done(false); yield break;
        }

        if (!lib.IsTextureFormatSupported(tex.format))
        {
            var conv = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            conv.SetPixels32(tex.GetPixels32());
            conv.Apply();
            tex = conv;
        }

        AddReferenceImageJobState job = lib.ScheduleAddImageWithValidationJob(tex, name, width);
        while (!job.jobHandle.IsCompleted) yield return null;
        job.jobHandle.Complete();

        if (job.status == AddReferenceImageJobStatus.Success)
        {
            done(true);
        }
        else
        {
            Debug.LogWarning("[ARMission] Gagal menambah target: " + job.status);
            ShowFeedback("Gambar target kurang detail untuk dipindai.\nHubungi admin untuk mengganti gambar.", "failure");
            done(false);
        }
    }

    // ---------------- Deteksi ----------------
    void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
    {
        foreach (var img in args.added) Check(img);
        foreach (var img in args.updated) Check(img);
    }

    void Check(ARTrackedImage img)
    {
        if (targetFound || string.IsNullOrEmpty(targetName)) return;
        if (img.trackingState != TrackingState.Tracking) return;
        if (!string.Equals(img.referenceImage.name, targetName, StringComparison.OrdinalIgnoreCase)) return;

        targetFound = true;
        if (instructionPanel) instructionPanel.SetActive(false);

        if (cardTemplate != null)
        {
            float w = img.size.x > 0.01f ? img.size.x : (FindBuiltIn(targetName)?.width ?? 0.5f);
            var card = Instantiate(cardTemplate, img.transform);
            card.SetActive(true);
            card.transform.localPosition = cardOffsetPerMeter * w;
            card.transform.localRotation = cardRotation;
            card.transform.localScale = cardScalePerMeter * w;
            var canvas = card.GetComponentInChildren<Canvas>(true);
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace) canvas.worldCamera = Camera.main;
            foreach (var txt in card.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (txt.gameObject.name == "DynamicTitle") txt.text = missionTitle;
                if (txt.gameObject.name == "DynamicDesc") txt.text = missionDesc;
            }
            ShowFeedback("Ditemukan! Tekan tombol Selesai.", "success");
        }
        else
        {
            ShowFeedback("Ditemukan!", "success");
            OnFinishMissionClicked();
        }
    }

    // ---------------- Selesai ----------------
    public void OnFinishMissionClicked()
    {
        if (finishing) return;
        finishing = true;
        StartCoroutine(FinishMissionRequest());
    }

    IEnumerator FinishMissionRequest()
    {
        ShowFeedback("Verifikasi...", "loading");
        var request = new UnityWebRequest(ApiConfig.BaseUrl + "/missions/" + currentMissionId + "/finish", "POST");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Authorization", "Bearer " + token);
        request.SetRequestHeader("Accept", "application/json");
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            JObject data = JObject.Parse(request.downloadHandler.text);
            int points = data["points_awarded"] != null ? (int)data["points_awarded"] : 0;
            bool isLevelUp = data["is_level_up"] != null && (bool)data["is_level_up"];
            string msg = "MISI SELESAI!\n+" + points + " Poin";
            if (isLevelUp) msg += "\nLEVEL UP!";
            ShowFeedback(msg, "success");
            yield return new WaitForSeconds(3f);
            StopXR();
            SceneManager.LoadScene("MissionScene");
        }
        else
        {
            finishing = false;
            ShowFeedback("Gagal: " + request.error, "failure");
        }
    }

    // ---------------- UI ----------------
    void ShowInstruction(string type, Sprite cover)
    {
        if (instructionPanel == null) return;
        instructionPanel.SetActive(true);
        string label = type == "book" ? "Cari Buku:" : "Cari Gedung:";
        if (instructionText) instructionText.text = label + "\n<size=120%><b>" + missionTitle + "</b></size>";
        if (targetThumbnail)
        {
            targetThumbnail.gameObject.SetActive(cover != null);
            if (cover != null) { targetThumbnail.sprite = cover; targetThumbnail.color = Color.white; targetThumbnail.preserveAspect = true; }
        }
    }

    BuiltInTarget FindBuiltIn(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return builtInTargets.Find(t => string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase));
    }

    static string Str(JToken t) => t == null || t.Type == JTokenType.Null ? "" : t.ToString();

    void ShowFeedback(string msg, string state)
    {
        if (feedbackPanel == null) return;
        if (state == "none") { feedbackPanel.SetActive(false); return; }
        if (hideFeedbackCoroutine != null) StopCoroutine(hideFeedbackCoroutine);
        feedbackPanel.SetActive(true);
        if (feedbackText) feedbackText.text = msg;
        if (loadingSpinner) loadingSpinner.SetActive(state == "loading");
        if (successIcon) successIcon.SetActive(state == "success");
        if (failureIcon) failureIcon.SetActive(state == "failure");
        if (state == "failure" || state == "success") hideFeedbackCoroutine = StartCoroutine(HideFeedbackAfterDelay(2.5f));
    }

    IEnumerator HideFeedbackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (feedbackPanel) feedbackPanel.SetActive(false);
    }
}
