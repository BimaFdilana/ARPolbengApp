using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Newtonsoft.Json.Linq;

public class MissionManager : MonoBehaviour
{
    [Header("UI References")]
    public Transform missionContainer; 
    public GameObject missionPrefab;   
    public GameObject backButton;      

    [Header("Feedback Modal")]
    public GameObject feedbackPanel;
    public TextMeshProUGUI feedbackText;
    
    public GameObject loadingSpinner;
    public GameObject successIcon; 
    public GameObject failureIcon; 

    private string token;
    private Coroutine hideFeedbackCoroutine;

    // PASTIKAN NAMA SCENE INI BENAR DAN SUDAH ADA DI BUILD SETTINGS
    private const string SCENE_AR = "ARExploreScene";
    private const string SCENE_QUIZ = "QuizScene"; 

    void Start()
    {
        if (backButton != null)
        {
            Button btn = backButton.GetComponent<Button>();
            if (btn != null) 
            {
                btn.onClick.AddListener(() => SceneManager.LoadScene("MainMenuScene"));
            }
        }

        if(feedbackPanel) feedbackPanel.SetActive(false);

        token = PlayerPrefs.GetString("user_token");
        Debug.Log("[MissionManager] Token loaded: " + (string.IsNullOrEmpty(token) ? "NULL" : "OK"));
        
        StartCoroutine(LoadMissionData());
    }

    private IEnumerator LoadMissionData()
    {
        // ... (Bagian Load Data tidak berubah drastis, saya singkat agar fokus ke error Quiz) ...
        // ... (Gunakan kode LoadMissionData dari jawaban sebelumnya) ...
        
        // SAYA PERSINGKAT UNTUK FOKUS KE BAGIAN START CLICKED
        // Pastikan Anda tetap menggunakan LoadMissionData yang lengkap seperti sebelumnya
        
        ShowFeedback("Memuat Misi...", "loading");

        // 1. GET PROFILE
        UnityWebRequest reqProfile = UnityWebRequest.Get(ApiConfig.BaseUrl + "/profile");
        reqProfile.SetRequestHeader("Authorization", "Bearer " + token);
        reqProfile.SetRequestHeader("Accept", "application/json");
        yield return reqProfile.SendWebRequest();

        if (reqProfile.result != UnityWebRequest.Result.Success) {
            Debug.LogError("[MissionManager] Error Profile: " + reqProfile.error);
            ShowFeedback("Gagal Profil", "failure");
            yield break;
        }
        
        JObject profileData = JObject.Parse(reqProfile.downloadHandler.text);
        JArray userMissionsJson = null;
        if (profileData["missions_completed"] != null) userMissionsJson = (JArray)profileData["missions_completed"];
        else if (profileData["missions"] != null) userMissionsJson = (JArray)profileData["missions"];

        Dictionary<int, string> missionStatusMap = new Dictionary<int, string>();
        if (userMissionsJson != null) {
            foreach (var m in userMissionsJson) {
                int id = (int)m["id"];
                string status = m["pivot"]["status"].ToString(); 
                if (!missionStatusMap.ContainsKey(id)) missionStatusMap.Add(id, status);
            }
        }

        // 2. GET MISSIONS
        UnityWebRequest reqMissions = UnityWebRequest.Get(ApiConfig.BaseUrl + "/missions");
        reqMissions.SetRequestHeader("Authorization", "Bearer " + token);
        reqMissions.SetRequestHeader("Accept", "application/json");
        yield return reqMissions.SendWebRequest();

        if (reqMissions.result != UnityWebRequest.Result.Success) {
            Debug.LogError("[MissionManager] Error Missions: " + reqMissions.error);
            ShowFeedback("Gagal Misi", "failure");
            yield break;
        }

        ShowFeedback("", "none"); 
        JArray allMissions = JArray.Parse(reqMissions.downloadHandler.text);
        GenerateMissionList(allMissions, missionStatusMap);
    }

    void GenerateMissionList(JArray allMissions, Dictionary<int, string> missionStatusMap)
    {
        foreach (Transform child in missionContainer) Destroy(child.gameObject);
        bool isUnlocked = true; 

        foreach (var mission in allMissions)
        {
            int missionId = (int)mission["id"];
            string title = mission["title"].ToString();
            string type = mission["type"].ToString(); 
            string targetImageName = mission["target_image_name"] != null ? mission["target_image_name"].ToString() : "";
            
            string quizDataString = "";
            if (mission["quiz_data"] != null && mission["quiz_data"].Type != JTokenType.Null)
            {
                quizDataString = mission["quiz_data"].ToString(Newtonsoft.Json.Formatting.None);
            }

            GameObject itemObj = Instantiate(missionPrefab, missionContainer);
            Transform titleObj = itemObj.transform.Find("TitleText");
            if (titleObj != null) 
            {
                string typeLabel = (type == "quiz") ? "[KUIS] " : "[AR] ";
                titleObj.GetComponent<TextMeshProUGUI>().text = typeLabel + title;
            }

            Transform actionContainer = itemObj.transform.Find("ActionContainer");
            if (actionContainer != null)
            {
                Button actionBtn = actionContainer.Find("ActionButton").GetComponent<Button>();
                GameObject lockIcon = actionContainer.Find("LockIcon").gameObject;
                GameObject doneIcon = actionContainer.Find("DoneIcon").gameObject;
                TextMeshProUGUI btnText = actionBtn.GetComponentInChildren<TextMeshProUGUI>();

                string currentStatus = missionStatusMap.ContainsKey(missionId) ? missionStatusMap[missionId] : "locked";

                if (currentStatus == "completed") {
                    lockIcon.SetActive(false); doneIcon.SetActive(true); actionBtn.gameObject.SetActive(false); isUnlocked = true; 
                }
                else if (currentStatus == "in_progress") {
                    lockIcon.SetActive(false); doneIcon.SetActive(false); actionBtn.gameObject.SetActive(true);
                    if(btnText) btnText.text = "LANJUTKAN";
                    actionBtn.onClick.RemoveAllListeners();
                    actionBtn.onClick.AddListener(() => OnStartClicked(missionId, type, targetImageName, quizDataString));
                    isUnlocked = false; 
                }
                else {
                    if (isUnlocked) {
                        lockIcon.SetActive(false); doneIcon.SetActive(false); actionBtn.gameObject.SetActive(true); 
                        if(btnText) btnText.text = "MULAI";
                        actionBtn.onClick.RemoveAllListeners();
                        actionBtn.onClick.AddListener(() => OnStartClicked(missionId, type, targetImageName, quizDataString));
                        isUnlocked = false; 
                    } else {
                        lockIcon.SetActive(true); doneIcon.SetActive(false); actionBtn.gameObject.SetActive(false); isUnlocked = false;
                    }
                }
            }
        }
    }

    // --- BAGIAN PENTING: DEBUGGING MULAI DARI SINI ---

    void OnStartClicked(int id, string type, string targetName, string quizJSON)
    {
        // LOG 1: Cek apakah tombol merespon dan datanya benar
        Debug.Log($"[MissionManager] OnStartClicked terpanggil! ID: {id}, Type: {type}");
        Debug.Log($"[MissionManager] Target: {targetName}");
        Debug.Log($"[MissionManager] Quiz Data: {quizJSON}");

        // Simpan Data
        PlayerPrefs.SetInt("active_mission_id", id);
        PlayerPrefs.SetString("active_mission_type", type);
        PlayerPrefs.SetString("active_mission_target", targetName);
        PlayerPrefs.SetString("active_mission_quiz_data", quizJSON);
        PlayerPrefs.Save();
        Debug.Log("[MissionManager] PlayerPrefs Saved.");

        StartCoroutine(PostStartMission(id, type));
    }

    IEnumerator PostStartMission(int id, string type)
    {
        ShowFeedback("Menyiapkan Misi...", "loading");
        Debug.Log($"[MissionManager] Sending API Request to /missions/{id}/start...");
        
        UnityWebRequest request = new UnityWebRequest(ApiConfig.BaseUrl + "/missions/" + id + "/start", "POST");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Authorization", "Bearer " + token);
        request.SetRequestHeader("Accept", "application/json"); 
        
        yield return request.SendWebRequest();

        Debug.Log($"[MissionManager] API Response Code: {request.responseCode}");
        Debug.Log($"[MissionManager] API Response Body: {request.downloadHandler.text}");

        bool isSuccess = (request.result == UnityWebRequest.Result.Success);
        
        if (!isSuccess)
        {
            string errorBody = request.downloadHandler.text.ToLower();
            if (errorBody.Contains("berlangsung") || errorBody.Contains("progress") || request.responseCode == 409)
            {
                Debug.LogWarning("[MissionManager] Misi sudah in_progress, mengizinkan masuk.");
                isSuccess = true;
            }
        }

        if (isSuccess)
        {
            ShowFeedback("", "none");
            
            // LOG 2: Cek logika perpindahan scene
            Debug.Log($"[MissionManager] Persiapan pindah scene. Type yang dicek: '{type}'");

            if (type.Trim().ToLower() == "quiz")
            {
                Debug.Log($"[MissionManager] Memuat Scene Quiz: {SCENE_QUIZ}");
                
                // Cek apakah scene valid di Build Settings
                if (Application.CanStreamedLevelBeLoaded(SCENE_QUIZ))
                {
                    SceneManager.LoadScene(SCENE_QUIZ);
                }
                else
                {
                    Debug.LogError($"[MissionManager] ERROR FATAL: Scene '{SCENE_QUIZ}' tidak ditemukan di Build Settings!");
                    ShowFeedback($"Scene '{SCENE_QUIZ}' hilang!", "failure");
                }
            }
            else
            {
                Debug.Log($"[MissionManager] Memuat Scene AR: {SCENE_AR}");
                SceneManager.LoadScene(SCENE_AR);
            }
        }
        else
        {
            string errorMsg = "Gagal memulai: " + request.error;
            try {
                JObject err = JObject.Parse(request.downloadHandler.text);
                if(err["message"] != null) errorMsg = err["message"].ToString();
            } catch {}

            Debug.LogError($"[MissionManager] Gagal Start: {errorMsg}");
            ShowFeedback(errorMsg, "failure"); 
            PlayerPrefs.DeleteKey("active_mission_id");
        }
    }

    void ShowFeedback(string msg, string state)
    {
        if(feedbackPanel == null) return;
        if (state == "none" || state == "false") { feedbackPanel.SetActive(false); return; }
        if (hideFeedbackCoroutine != null) StopCoroutine(hideFeedbackCoroutine);
        feedbackPanel.SetActive(true);
        if(feedbackText) feedbackText.text = msg;
        if(loadingSpinner) loadingSpinner.SetActive(false);
        if(successIcon) successIcon.SetActive(false);
        if(failureIcon) failureIcon.SetActive(false);
        if (state == "loading" && loadingSpinner) loadingSpinner.SetActive(true);
        if (state == "success" && successIcon) successIcon.SetActive(true);
        if (state == "failure" && failureIcon) failureIcon.SetActive(true);
        if (state == "failure" || state == "success") hideFeedbackCoroutine = StartCoroutine(HideFeedbackAfterDelay(2f));
    }

    IEnumerator HideFeedbackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if(feedbackPanel) feedbackPanel.SetActive(false);
    }
}