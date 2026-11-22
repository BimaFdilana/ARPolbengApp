using UnityEngine;
using UnityEngine.SceneManagement; // Wajib untuk pindah scene
using UnityEngine.UI;
using UnityEngine.Networking; // vvv BARU vvv
using System.Collections;    // vvv BARU vvv
using TMPro;                 // vvv BARU vvv

public class MainMenuManager : MonoBehaviour
{
    // --- Variabel untuk dihubungkan di Inspector ---
    [Header("Tombol Navigasi")]
    public Button startMissionButton;
    public Button profileButton;
    public Button mapButton;
    public Button logoutButton;

    // vvv BARU: Variabel untuk Modal vvv
    [Header("Panel Feedback/Modal")]
    public GameObject feedbackPanel;
    public TextMeshProUGUI feedbackText;
    public GameObject loadingSpinner;
    public GameObject successIcon;
    public GameObject failureIcon;
    // ^^^ BARU ^^^

    // --- Scene Names ---
    private string missionSceneName = "MissionScene";
    private string profileSceneName = "ProfileScene";
    private string mapSceneName = "CampusMapScene";
    private string authSceneName = "AuthScene";
    
    // vvv BARU: URL API & Coroutine vvv
    private Coroutine hideFeedbackCoroutine;
    // ^^^ BARU ^^^

    void Start()
    {
        // Daftarkan fungsi ke listener tombol
        startMissionButton.onClick.AddListener(GoToMissionScene);
        profileButton.onClick.AddListener(GoToProfileScene);
        mapButton.onClick.AddListener(GoToMapScene);
        
        // --- DIUBAH ---
        // Tombol Logout sekarang memanggil fungsi baru
        if (feedbackPanel != null) feedbackPanel.SetActive(false);
        logoutButton.onClick.AddListener(OnLogoutButtonClick);
        // --------------
    }

    // --- FUNGSI NAVIGASI ---
    public void GoToMissionScene()
    {
        SceneManager.LoadScene(missionSceneName);
    }
    public void GoToProfileScene()
    {
        SceneManager.LoadScene(profileSceneName);
    }
    public void GoToMapScene()
    {
        SceneManager.LoadScene(mapSceneName);
    }

    // --- FUNGSI LOGOUT (DIUBAH TOTAL) ---

    // Fungsi ini dipanggil oleh tombol
    public void OnLogoutButtonClick()
    {
        StartCoroutine(LogoutRequest());
    }

    // Coroutine untuk proses logout asinkron
    // Coroutine untuk proses logout asinkron yang lebih rapi
    private IEnumerator LogoutRequest()
    {
        // 1. Tampilkan modal loading
        ShowFeedbackModal("Sedang logout...", "loading");

        // 2. Ambil token
        string userToken = PlayerPrefs.GetString("user_token", null);


        if (string.IsNullOrEmpty(userToken))
        {
            ShowFeedbackModal("Token hilang, logout lokal...", "failure");
        }
        else
        {
            // 3. Request ke API
            UnityWebRequest request = new UnityWebRequest(ApiConfig.BaseUrl + "/logout", "POST");
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Bearer " + userToken);
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                ShowFeedbackModal("Logout Berhasil! Sampai Jumpa.", "success");
            }
            else
            {
                // Tampilkan pesan error spesifik jika perlu, atau pesan umum
                ShowFeedbackModal("Gagal connect server. Logout paksa...", "failure");
            }
        }

        // 4. Hapus Data Lokal
        PlayerPrefs.DeleteKey("user_token");
        PlayerPrefs.Save();

        yield return new WaitForSeconds(2.5f); 

        SceneManager.LoadScene(authSceneName);
    }


    private IEnumerator GoToLoginAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        // 3. Kembali ke halaman login
        SceneManager.LoadScene(authSceneName);
    }

    // --- FUNGSI HELPER MODAL (Disalin dari AuthManager) ---

    private void ShowFeedbackModal(string message, string state)
    {
        if (hideFeedbackCoroutine != null)
        {
            StopCoroutine(hideFeedbackCoroutine);
        }

        feedbackPanel.SetActive(true);
        feedbackText.text = message;

        loadingSpinner.SetActive(state == "loading");
        successIcon.SetActive(state == "success");
        failureIcon.SetActive(state == "failure");

        // Auto-hide HANYA jika BUKAN loading
        if (state == "success" || state == "failure")
        {
            hideFeedbackCoroutine = StartCoroutine(HideFeedbackAfterDelay(3f));
        }
    }

    private IEnumerator HideFeedbackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        feedbackPanel.SetActive(false);
    }
}