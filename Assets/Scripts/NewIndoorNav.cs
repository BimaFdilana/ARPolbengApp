using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

/// <summary>
/// Navigasi indoor AR.
/// - Mendukung banyak gambar penanda: setiap gambar dipasangkan ke titik KampusAnchor di prefab (nama harus sama).
/// - Posisi peta dikunci setelah gambar terlacak stabil, supaya garis tidak goyang. Scan gambar lain untuk kalibrasi ulang.
/// - Tetap kompatibel dengan prefab lama (tanpa KampusAnchor): pusat prefab = posisi gambar.
/// </summary>
public class NewIndoorNav : MonoBehaviour
{
    [Header("AR System Control (Wajib Diisi)")]
    [SerializeField] private GameObject arSessionObject; // Drag object 'AR Session'
    [SerializeField] private GameObject xrOriginObject;  // Drag object 'XR Origin'

    [Header("Main Components")]
    [SerializeField] private Transform player;
    [SerializeField] private Button backButton;
    [SerializeField] private ARTrackedImageManager m_TrackedImageManager;
    [SerializeField] private GameObject trackedImagePrefab;
    [SerializeField] private LineRenderer line;
    [SerializeField] private LineRenderer minimapLine;

    [Header("UI")]
    [SerializeField] private TMP_Dropdown targetSelector;
    [SerializeField] private TextMeshProUGUI infoText;
    [SerializeField] private GameObject infoPanel;

    [Header("Visual Settings")]
    [SerializeField] private float textureScrollSpeed = 0.5f; // Kecepatan gerak panah
    [SerializeField] private float arrivalDistance = 3.0f;    // Jarak target muncul (meter)

    [Header("Pelacakan & Jalur")]
    [Tooltip("Tinggi kamera HP dari lantai (meter), dipakai untuk mencari lantai di bawah pengguna.")]
    [SerializeField] private float eyeHeight = 1.4f;
    [Tooltip("Setelah gambar terlacak stabil selama ini (detik), posisi peta dikunci.")]
    [SerializeField] private float lockAfterSeconds = 2.0f;
    [Tooltip("Seberapa cepat peta mengikuti gambar sebelum dikunci (0-1).")]
    [Range(0.05f, 1f)] [SerializeField] private float smoothing = 0.35f;
    [Tooltip("Interval hitung ulang jalur (detik).")]
    [SerializeField] private float pathInterval = 0.2f;
    [Tooltip("Tinggi garis di atas lantai supaya tidak tenggelam.")]
    [SerializeField] private float lineHeight = 0.1f;
    [Header("Garis di Kamera AR")]
    [Tooltip("Lebar garis navigasi di layar kamera (meter).")]
    [SerializeField] private float arLineWidth = 0.3f;
    [Tooltip("Tinggi garis AR di atas perkiraan lantai tempat pengguna berdiri (meter).")]
    [SerializeField] private float arLineHeight = 0.05f;
    [Tooltip("Garis AR digeser agar titik awalnya selalu di kaki pengguna, sehingga tetap terlihat walau tinggi gambar penanda tidak persis.")]
    [SerializeField] private bool garisMengikutiLantaiPengguna = true;

    [Header("Mode Uji Editor (hanya berjalan di Unity Editor, tidak ikut ke HP)")]
    [Tooltip("Saat Play di Editor: tekan T untuk pura-pura memindai gambar, WASD untuk berjalan, klik kanan + geser untuk menoleh, Shift untuk cepat.")]
    [SerializeField] private bool modeUjiEditor = true;
    [Tooltip("Nama gambar penanda yang dipakai saat menekan T.")]
    [SerializeField] private string gambarUjiEditor = "Target_Lobi";
    [SerializeField] private float kecepatanJalanUji = 1.5f;

    private readonly List<NavigationTarget> navigationTargets = new List<NavigationTarget>();
    private NavigationTarget currentTarget;

    private NavMeshSurface navMeshSurface;
    private NavMeshPath navMeshPath;
    private GameObject navigationBase;
    private readonly Dictionary<string, KampusAnchor> anchors = new Dictionary<string, KampusAnchor>();

    // status kunci posisi
    private string activeImage;
    private float activeSince;
    private bool locked;
    private bool placedOnce;
    private float lastNavRefresh;
    private Vector3 lastNavPos;
    private Quaternion lastNavRot;
    private float nextPathTime;
    private Vector3[] corners = new Vector3[0];
    private Vector3[] arCorners = new Vector3[0];

    void Start()
    {
        navMeshPath = new NavMeshPath();
        SiapkanGarisAR();
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        if (arSessionObject != null) arSessionObject.SetActive(false);
        if (xrOriginObject != null) xrOriginObject.SetActive(false);
#if UNITY_EDITOR
        if (modeUjiEditor) { StartEditorTest(); }
        else
#endif
        StartCoroutine(InitializeXR());

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(() =>
            {
                StopXR();
                SceneManager.LoadScene("MainMenuScene");
            });
        }

        if (line != null) line.positionCount = 0;
        if (minimapLine != null) minimapLine.positionCount = 0;
        if (infoPanel != null) infoPanel.SetActive(true);
#if UNITY_EDITOR
        if (modeUjiEditor) { UpdateInfoText(EditorHelp); return; }
#endif
        UpdateInfoText("Memuat Sistem AR...");
    }

    IEnumerator InitializeXR()
    {
        yield return null;
        if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null)
        {
            if (XRGeneralSettings.Instance.Manager.activeLoader == null)
                yield return XRGeneralSettings.Instance.Manager.InitializeLoader();

            if (XRGeneralSettings.Instance.Manager.activeLoader != null)
            {
                XRGeneralSettings.Instance.Manager.StartSubsystems();
                yield return null;
                if (xrOriginObject != null) xrOriginObject.SetActive(true);
                yield return null;
                if (arSessionObject != null) arSessionObject.SetActive(true);
                UpdateInfoText("Siap. Arahkan kamera ke gambar penanda...");
            }
            else UpdateInfoText("Gagal memuat XR Loader.");
        }
    }

    void StopXR()
    {
        if (arSessionObject != null) arSessionObject.SetActive(false);
        if (xrOriginObject != null) xrOriginObject.SetActive(false);
        if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null &&
            XRGeneralSettings.Instance.Manager.isInitializationComplete)
        {
            XRGeneralSettings.Instance.Manager.StopSubsystems();
            XRGeneralSettings.Instance.Manager.DeinitializeLoader();
        }
    }

    /// <summary>Lepas kunci posisi, peta akan menyesuaikan lagi dengan gambar berikutnya yang terlihat. Bisa dipasang ke tombol.</summary>
    public void KalibrasiUlang()
    {
        locked = false;
        activeImage = null;
        UpdateInfoText("Kalibrasi ulang: arahkan kamera ke gambar penanda...");
    }

    void Update()
    {
        AnimateLine(line);
        AnimateLine(minimapLine);
#if UNITY_EDITOR
        if (modeUjiEditor) EditorTestUpdate();
#endif

        if (navigationBase == null) return;
        if (currentTarget == null) { UpdateInfoText("Pilih tujuan."); return; }
        if (Time.time < nextPathTime) return;
        nextPathTime = Time.time + pathInterval;

        Vector3 origin = player.position + Vector3.down * eyeHeight;
        if (!NavMesh.SamplePosition(origin, out NavMeshHit hit, 2.0f, NavMesh.AllAreas) &&
            !NavMesh.SamplePosition(player.position, out hit, 5.0f, NavMesh.AllAreas))
        {
            ClearLines();
            UpdateInfoText("Posisi Anda belum terbaca di peta.\nArahkan kamera ke gambar penanda terdekat.");
            UpdateTargetVisibility(false);
            return;
        }

        NavMesh.CalculatePath(hit.position, currentTarget.transform.position, NavMesh.AllAreas, navMeshPath);
        if (navMeshPath.status == NavMeshPathStatus.PathInvalid || navMeshPath.corners.Length < 2)
        {
            ClearLines();
            UpdateInfoText("Jalur ke tujuan tidak ditemukan.");
            UpdateTargetVisibility(false);
            return;
        }

        if (corners.Length != navMeshPath.corners.Length) corners = new Vector3[navMeshPath.corners.Length];
        float pathLength = 0f;
        for (int i = 0; i < corners.Length; i++)
        {
            corners[i] = navMeshPath.corners[i] + Vector3.up * lineHeight;
            if (i > 0) pathLength += Vector3.Distance(navMeshPath.corners[i - 1], navMeshPath.corners[i]);
        }
        SetLine(minimapLine, corners);

        // Garis di kamera: digeser naik/turun supaya titik awalnya di lantai tempat pengguna berdiri
        if (arCorners.Length != corners.Length) arCorners = new Vector3[corners.Length];
        float geser = 0f;
        if (garisMengikutiLantaiPengguna && navMeshPath.corners.Length > 0)
            geser = (player.position.y - eyeHeight + arLineHeight) - navMeshPath.corners[0].y;
        for (int i = 0; i < arCorners.Length; i++)
            arCorners[i] = garisMengikutiLantaiPengguna ? navMeshPath.corners[i] + Vector3.up * geser : corners[i];
        SetLine(line, arCorners);

        int lantaiSaatIni = FloorAt(hit.position);
        string info = $"Tujuan: {currentTarget.Label}";
        if (!string.IsNullOrEmpty(currentTarget.gedung)) info += $"\n{currentTarget.gedung}, lantai {currentTarget.lantai}";
        info += $"\nJarak: {pathLength:F0} m";
        if (currentTarget.lantai > lantaiSaatIni) info += $"\nNaik tangga ke lantai {currentTarget.lantai}";
        else if (currentTarget.lantai < lantaiSaatIni) info += $"\nTurun tangga ke lantai {currentTarget.lantai}";
        if (navMeshPath.status == NavMeshPathStatus.PathPartial) info += "\n(Jalur belum lengkap)";
        UpdateInfoText(info);

        Vector3 flat = player.position - currentTarget.transform.position; flat.y = 0f;
        bool sameFloor = currentTarget.lantai == lantaiSaatIni || string.IsNullOrEmpty(currentTarget.gedung);
        UpdateTargetVisibility(sameFloor && flat.magnitude <= arrivalDistance);
    }

    void SiapkanGarisAR()
    {
        if (line == null) return;
        line.enabled = true;
        line.gameObject.SetActive(true);
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.widthMultiplier = arLineWidth;
        line.numCornerVertices = 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        Material m = line.material; // salinan material khusus garis ini
        if (m != null)
        {
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f); // tampil dari dua sisi
            m.renderQueue = 3100; // digambar setelah latar kamera AR
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.1f, 0.85f, 1f, 1f));
        }
        // pastikan layer garis ikut dirender kamera AR
        Camera cam = Camera.main;
        if (cam != null && (cam.cullingMask & (1 << line.gameObject.layer)) == 0) line.gameObject.layer = 0;
    }

    int FloorAt(Vector3 worldPos)
    {
        if (navigationBase == null) return 1;
        float y = navigationBase.transform.InverseTransformPoint(worldPos).y;
        return Mathf.Max(1, Mathf.FloorToInt((y + 1.0f) / 3.6f) + 1);
    }

    void AnimateLine(LineRenderer lr)
    {
        if (lr == null || lr.material == null) return;
        lr.material.SetTextureOffset("_BaseMap", new Vector2(Time.time * -textureScrollSpeed, 0));
    }

    static void SetLine(LineRenderer lr, Vector3[] pts)
    {
        if (lr == null) return;
        lr.positionCount = pts.Length;
        lr.SetPositions(pts);
    }

    void ClearLines()
    {
        if (line != null) line.positionCount = 0;
        if (minimapLine != null) minimapLine.positionCount = 0;
    }

    private void UpdateTargetVisibility(bool isVisible)
    {
        if (currentTarget == null) return;
        foreach (Renderer r in currentTarget.GetComponentsInChildren<Renderer>()) r.enabled = isVisible;
    }

    private void UpdateInfoText(string message)
    {
        if (infoText != null) infoText.text = message;
    }

    private void OnEnable() { if (m_TrackedImageManager != null) m_TrackedImageManager.trackablesChanged.AddListener(OnChanged); }
    private void OnDisable() { if (m_TrackedImageManager != null) m_TrackedImageManager.trackablesChanged.RemoveListener(OnChanged); }

    private void OnChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        foreach (var img in eventArgs.added) HandleImage(img);
        foreach (var img in eventArgs.updated) HandleImage(img);
    }

    void HandleImage(ARTrackedImage img)
    {
        if (img.trackingState != TrackingState.Tracking) return;
        string imgName = img.referenceImage.name;
        if (string.IsNullOrEmpty(imgName)) return; // gambar tanpa nama (mis. dari XR Simulation)

        if (navigationBase == null) CreateBase();
        if (navigationBase == null) return;

        anchors.TryGetValue(imgName, out KampusAnchor anchor);
        if (anchors.Count > 0 && anchor == null)
        {
            UpdateInfoText($"Gambar '{imgName}' belum terdaftar di peta.");
            return;
        }

        // gambar berbeda dari sebelumnya = kalibrasi ulang
        if (imgName != activeImage)
        {
            activeImage = imgName;
            activeSince = Time.time;
            locked = false;
            UpdateInfoText("Gambar terdeteksi, tahan sebentar...");
        }
        if (locked) return;

        ComputeBasePose(img, anchor, out Vector3 pos, out Quaternion rot);
        ApplyBasePose(pos, rot);
    }

    void ApplyBasePose(Vector3 pos, Quaternion rot)
    {
        Transform t = navigationBase.transform;
        if (!placedOnce) { t.SetPositionAndRotation(pos, rot); placedOnce = true; }
        else t.SetPositionAndRotation(Vector3.Lerp(t.position, pos, smoothing), Quaternion.Slerp(t.rotation, rot, smoothing));
        RefreshNavMesh(false);

        if (Time.time - activeSince >= lockAfterSeconds)
        {
            t.SetPositionAndRotation(pos, rot);
            RefreshNavMesh(true);
            locked = true;
            if (currentTarget == null) UpdateInfoText("Posisi terkunci. Pilih tujuan.");
        }
    }

    void ComputeBasePose(ARTrackedImage img, KampusAnchor anchor, out Vector3 pos, out Quaternion rot)
    {
        Vector3 imgPos = img.transform.position;
        Quaternion imgYaw;
        if (anchor == null || anchor.modeLama)
        {
            imgYaw = Quaternion.Euler(0f, img.transform.rotation.eulerAngles.y, 0f);
        }
        else
        {
            Vector3 normal = img.transform.up;
            Vector3 dir = Mathf.Abs(normal.y) < 0.7f ? -normal          // gambar di dinding: arah masuk ke dinding
                                                       : img.transform.forward; // gambar mendatar: sisi atas gambar
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            imgYaw = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        PoseFromAnchor(anchor, imgPos, imgYaw, out pos, out rot);
    }

    void PoseFromAnchor(KampusAnchor anchor, Vector3 imgPos, Quaternion imgYaw, out Vector3 pos, out Quaternion rot)
    {
        if (anchor == null) { pos = imgPos; rot = imgYaw; return; }

        // posisi & arah anchor relatif terhadap akar prefab (dihitung dari transform lokal berantai)
        Transform root = navigationBase.transform;
        Vector3 aLocalPos = root.InverseTransformPoint(anchor.transform.position);
        float aLocalYaw = (Quaternion.Inverse(root.rotation) * anchor.transform.rotation).eulerAngles.y;

        rot = imgYaw * Quaternion.Inverse(Quaternion.Euler(0f, aLocalYaw, 0f));
        pos = imgPos - rot * aLocalPos;
    }

    void RefreshNavMesh(bool force)
    {
        if (navMeshSurface == null || navMeshSurface.navMeshData == null) return;
        Transform t = navigationBase.transform;
        bool moved = Vector3.Distance(t.position, lastNavPos) > 0.05f || Quaternion.Angle(t.rotation, lastNavRot) > 0.5f;
        if (!force && (!moved || Time.time - lastNavRefresh < 0.25f)) return;
        navMeshSurface.RemoveData();
        navMeshSurface.AddData();
        lastNavRefresh = Time.time; lastNavPos = t.position; lastNavRot = t.rotation;
    }

    void CreateBase()
    {
        if (trackedImagePrefab == null) { UpdateInfoText("Prefab peta belum diisi."); return; }
        navigationBase = Instantiate(trackedImagePrefab);
        navMeshSurface = navigationBase.GetComponentInChildren<NavMeshSurface>();
        if (navMeshSurface != null && navMeshSurface.navMeshData == null) navMeshSurface.BuildNavMesh(); // prefab lama

        anchors.Clear();
        foreach (var a in navigationBase.GetComponentsInChildren<KampusAnchor>(true))
            if (!string.IsNullOrEmpty(a.imageName)) anchors[a.imageName] = a;

        navigationTargets.Clear();
        navigationTargets.AddRange(navigationBase.GetComponentsInChildren<NavigationTarget>(true));
        navigationTargets.Sort((a, b) =>
        {
            int c = string.Compare(a.gedung ?? "", b.gedung ?? "", System.StringComparison.Ordinal);
            if (c != 0) return c;
            c = a.lantai.CompareTo(b.lantai);
            return c != 0 ? c : NaturalCompare(a.Label, b.Label);
        });
        foreach (var t in navigationTargets)
            foreach (var r in t.GetComponentsInChildren<Renderer>()) r.enabled = false;

        if (targetSelector != null)
        {
            targetSelector.ClearOptions();
            targetSelector.AddOptions(navigationTargets.Select(LabelFor).ToList());
            targetSelector.onValueChanged.RemoveAllListeners();
            targetSelector.onValueChanged.AddListener(index =>
            {
                UpdateTargetVisibility(false);
                if (index >= 0 && index < navigationTargets.Count) currentTarget = navigationTargets[index];
                nextPathTime = 0f;
            });
        }
        if (navigationTargets.Count > 0) currentTarget = navigationTargets[0];
    }

#if UNITY_EDITOR
    // ======================= MODE UJI EDITOR =======================
    const string EditorHelp = "MODE UJI EDITOR\nT = pindai gambar | WASD = jalan | Klik kanan + geser = menoleh | Shift = cepat";
    private float yawUji, pitchUji;

    void StartEditorTest()
    {
        // Kamera dipakai tanpa ARCore: aktifkan XR Origin, matikan komponen yang butuh perangkat AR.
        if (xrOriginObject != null) xrOriginObject.SetActive(true);
        if (m_TrackedImageManager != null) m_TrackedImageManager.enabled = false; // "pindai" diganti tombol T
        if (player != null)
        {
            foreach (var b in player.GetComponents<Behaviour>())
            {
                string n = b.GetType().Name;
                if (n == "TrackedPoseDriver" || n == "ARCameraBackground" || n == "ARCameraManager") b.enabled = false;
            }
            var cam = player.GetComponent<Camera>();
            int minimapLayer = LayerMask.NameToLayer("Minimap");
            if (cam != null && minimapLayer >= 0) cam.cullingMask |= 1 << minimapLayer; // gedung terlihat di Game view
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.7f, 0.85f); }
            player.position = new Vector3(0f, eyeHeight, 0f);
            player.rotation = Quaternion.identity;
            yawUji = 0f; pitchUji = 0f;
        }
    }

    void EditorTestUpdate()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (kb == null || player == null) return;

        if (kb.tKey.wasPressedThisFrame) SimulateScan();

        // menoleh
        if (mouse != null && mouse.rightButton.isPressed)
        {
            Vector2 d = mouse.delta.ReadValue() * 0.15f;
            yawUji += d.x; pitchUji = Mathf.Clamp(pitchUji - d.y, -80f, 80f);
        }
        player.rotation = Quaternion.Euler(pitchUji, yawUji, 0f);

        // berjalan (dibatasi NavMesh supaya tidak menembus dinding, naik tangga otomatis)
        Vector3 input = Vector3.zero;
        if (kb.wKey.isPressed) input.z += 1; if (kb.sKey.isPressed) input.z -= 1;
        if (kb.dKey.isPressed) input.x += 1; if (kb.aKey.isPressed) input.x -= 1;
        if (input == Vector3.zero) return;
        float speed = kecepatanJalanUji * (kb.leftShiftKey.isPressed ? 4f : 1f);
        Vector3 dir = Quaternion.Euler(0f, yawUji, 0f) * input.normalized;
        Vector3 floor = player.position + Vector3.down * eyeHeight;
        Vector3 next = floor + dir * speed * Time.deltaTime;

        if (navigationBase != null && NavMesh.SamplePosition(floor, out NavMeshHit here, 1.0f, NavMesh.AllAreas))
        {
            if (NavMesh.Raycast(here.position, next, out NavMeshHit block, NavMesh.AllAreas)) next = block.position;
            if (NavMesh.SamplePosition(next, out NavMeshHit there, 1.0f, NavMesh.AllAreas)) next = there.position;
        }
        player.position = next + Vector3.up * eyeHeight;
    }

    void SimulateScan()
    {
        if (navigationBase == null) CreateBase();
        if (navigationBase == null) return;
        anchors.TryGetValue(gambarUjiEditor, out KampusAnchor anchor);
        if (anchors.Count > 0 && anchor == null) { UpdateInfoText($"Gambar uji '{gambarUjiEditor}' tidak ada di peta."); return; }

        // Pengguna berdiri di depan gambar: gambar 1 m di depan kamera.
        Quaternion yaw = Quaternion.Euler(0f, yawUji, 0f);
        Vector3 floor = player.position + Vector3.down * eyeHeight;
        float tinggiGambar = (anchor != null && !anchor.modeLama) ? anchor.transform.localPosition.y : 0f;
        Vector3 imgPos = floor + yaw * Vector3.forward * 1.0f + Vector3.up * tinggiGambar;

        navigationBase.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        PoseFromAnchor(anchor, imgPos, yaw, out Vector3 pos, out Quaternion rot);
        navigationBase.transform.SetPositionAndRotation(pos, rot);
        placedOnce = true; locked = true; activeImage = gambarUjiEditor;
        RefreshNavMesh(true);
        nextPathTime = 0f;
        Debug.Log($"[Mode Uji] Peta ditempatkan dari '{gambarUjiEditor}'.");
    }
#endif

    static string LabelFor(NavigationTarget t)
    {
        if (string.IsNullOrEmpty(t.gedung)) return t.Label;
        bool namaSudahAdaGedung = t.Label.Contains(t.gedung);
        string s = namaSudahAdaGedung ? t.Label : $"{t.Label} - {t.gedung}";
        return t.lantai > 1 ? $"{s} (Lt {t.lantai})" : s;
    }

    // urutan alami: "Ruang C2" sebelum "Ruang C10"
    static int NaturalCompare(string a, string b)
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int si = i, sj = j;
                while (i < a.Length && char.IsDigit(a[i])) i++;
                while (j < b.Length && char.IsDigit(b[j])) j++;
                int c = int.Parse(a.Substring(si, i - si)).CompareTo(int.Parse(b.Substring(sj, j - sj)));
                if (c != 0) return c;
            }
            else
            {
                int c = a[i].CompareTo(b[j]);
                if (c != 0) return c;
                i++; j++;
            }
        }
        return a.Length.CompareTo(b.Length);
    }
}
