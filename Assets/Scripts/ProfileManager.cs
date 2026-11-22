using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using System.Collections;
using TMPro;
using Newtonsoft.Json.Linq;

public class ProfileManager : MonoBehaviour
{
    [Header("Navigasi")]
    public Button backButton;

    [Header("UI Info User")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI pointsText;
    public TextMeshProUGUI levelText;

    [Header("UI Lencana (Grid)")]
    public Transform badgeContainer; 
    public GameObject badgePrefab;   

    [Header("Feedback Modal")]
    public GameObject feedbackPanel;
    public TextMeshProUGUI feedbackText;
    public GameObject loadingSpinner;
    public GameObject successIcon; // <-- BARU
    public GameObject failureIcon; // <-- BARU

    private Coroutine hideFeedbackCoroutine;

    void Start()
    {
        if (backButton != null)
            backButton.onClick.AddListener(() => SceneManager.LoadScene("MainMenuScene"));
        
        if (feedbackPanel != null) feedbackPanel.SetActive(false);
        
        StartCoroutine(GetProfileData());
    }

    private IEnumerator GetProfileData()
    {
        ShowFeedbackModal("Memuat Profil...", "loading");
        string token = PlayerPrefs.GetString("user_token");

        // Gunakan ApiConfig jika sudah dibuat, atau hardcode URL
        // UnityWebRequest request = UnityWebRequest.Get("http://localhost:8000/api/profile");
        UnityWebRequest request = UnityWebRequest.Get(ApiConfig.BaseUrl + "/profile");
        request.SetRequestHeader("Authorization", "Bearer " + token);
        request.SetRequestHeader("Accept", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            ShowFeedbackModal("", "none"); // Tutup modal jika sukses
            
            string json = request.downloadHandler.text;
            JObject data = JObject.Parse(json);

            if (nameText) nameText.text = data["name"].ToString();
            if (pointsText) pointsText.text = "Total Poin: " + data["total_points"].ToString();
            
            if (data["level"] != null) 
            {
                if (levelText) levelText.text = "Level " + data["level"].ToString();
            }
            else 
            {
                if (levelText) levelText.text = "Level 1";
            }

            JArray badges = (JArray)data["badges_earned"]; 

            if (badgeContainer != null)
            {
                foreach (Transform child in badgeContainer) Destroy(child.gameObject);

                if (badges != null && badges.Count > 0)
                {
                    foreach (var badgeData in badges)
                    {
                        if (badgePrefab != null)
                        {
                            GameObject newBadge = Instantiate(badgePrefab, badgeContainer);
                            
                            TextMeshProUGUI textBadge = newBadge.GetComponentInChildren<TextMeshProUGUI>();
                            if(textBadge != null) textBadge.text = badgeData["name"].ToString();

                            Transform iconTransform = newBadge.transform.Find("IconImage"); 
                            if (iconTransform != null)
                            {
                                Image iconImage = iconTransform.GetComponent<Image>();
                                string urlGambar = badgeData["icon_url"].ToString();
                                StartCoroutine(DownloadImage(urlGambar, iconImage));
                            }
                        }
                    }
                }
            }
        }
        else
        {
            if (request.responseCode == 401) 
            {
                ShowFeedbackModal("Sesi habis", "failure");
                yield return new WaitForSeconds(2f);
                SceneManager.LoadScene("AuthScene");
            }
            else 
            {
                ShowFeedbackModal("Gagal: " + request.error, "failure");
            }
        }
    }

    IEnumerator DownloadImage(string url, Image targetImage)
    {
        if (string.IsNullOrEmpty(url)) yield break;

        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Texture2D texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            targetImage.sprite = sprite;
            targetImage.preserveAspect = true; 
        }
    }

    // --- HELPER MODAL YANG DIPERBAIKI ---
    void ShowFeedbackModal(string msg, string state)
    {
        if (feedbackPanel == null) return;

        if (state == "none" || state == "false") 
        {
            feedbackPanel.SetActive(false);
            return;
        }

        if (hideFeedbackCoroutine != null) StopCoroutine(hideFeedbackCoroutine);

        feedbackPanel.SetActive(true);
        if(feedbackText) feedbackText.text = msg;
        
        // Logika Ikon: Nyalakan yang sesuai state, matikan yang lain
        if(loadingSpinner) loadingSpinner.SetActive(state == "loading");
        if(successIcon) successIcon.SetActive(state == "success");
        if(failureIcon) failureIcon.SetActive(state == "failure");

        if (state == "failure" || state == "success")
        {
            hideFeedbackCoroutine = StartCoroutine(HideFeedbackAfterDelay(2f));
        }
    }

    IEnumerator HideFeedbackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if(feedbackPanel) feedbackPanel.SetActive(false);
    }
}