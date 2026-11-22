using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;
using Newtonsoft.Json.Linq;

public class AuthManager : MonoBehaviour
{
    // --- URL API DARI LARAVEL ---
    // Gunakan ApiConfig agar konsisten
    private string apiUrl = ApiConfig.BaseUrl; 

    // --- Variabel Publik (Slot di Inspector) ---
    [Header("Panel UI")]
    public GameObject loginPanel;
    public GameObject registerPanel;
    public GameObject feedbackPanel;

    [Header("Login Panel Elements")]
    public TMP_InputField emailInputLogin; 
    public TMP_InputField passwordInputLogin;
    public Button loginButton;
    public Button goToRegisterButton;

    [Header("Register Panel Elements")]
    public TMP_InputField nameInputRegister;
    public TMP_InputField emailInputRegister; 
    public TMP_InputField passwordInputRegister;
    public TMP_InputField passwordConfirmationInputRegister; 
    public Button registerButton;
    public Button goToLoginButton;

    [Header("Feedback Elements")]
    public TextMeshProUGUI feedbackText;
    public GameObject loadingSpinner;
    public GameObject successIcon;
    public GameObject failureIcon;

    private Coroutine hideFeedbackCoroutine;

    // Fungsi ini dipanggil saat skrip pertama kali aktif
    void Start()
    {
        // --- LOGIKA AUTO LOGIN (PENYIMPANAN SESI) ---
        
        // 1. Cek apakah ada token tersimpan di PlayerPrefs
        if (PlayerPrefs.HasKey("user_token"))
        {
            string savedToken = PlayerPrefs.GetString("user_token");
            
            // 2. Jika token tidak kosong, langsung pindah ke Menu Utama
            if (!string.IsNullOrEmpty(savedToken))
            {
                // Opsional: Tampilkan loading sebentar
                // ShowFeedbackModal("Memuat Sesi...", "loading"); 
                
                // Langsung pindah scene tanpa login lagi
                SceneManager.LoadScene("MainMenuScene");
                return; // Hentikan eksekusi kode di bawah agar halaman login tidak muncul
            }
        }
        // --------------------------------------------

        // Jika tidak ada token, baru tampilkan halaman login
        ShowLoginPage();

        // Mendaftarkan fungsi ke tombol (menambahkan listener)
        if(loginButton) loginButton.onClick.AddListener(OnLoginButtonClick);
        if(registerButton) registerButton.onClick.AddListener(OnRegisterButtonClick);
        if(goToRegisterButton) goToRegisterButton.onClick.AddListener(ShowRegisterPage);
        if(goToLoginButton) goToLoginButton.onClick.AddListener(ShowLoginPage);
        
        // Matikan modal saat awal
        if(feedbackPanel) feedbackPanel.SetActive(false);
    }

    // --- FUNGSI UNTUK PINDAH PANEL ---
    public void ShowLoginPage()
    {
        if(loginPanel) loginPanel.SetActive(true);
        if(registerPanel) registerPanel.SetActive(false);
        if(feedbackPanel) feedbackPanel.SetActive(false);
    }

    public void ShowRegisterPage()
    {
        if(loginPanel) loginPanel.SetActive(false);
        if(registerPanel) registerPanel.SetActive(true);
        if(feedbackPanel) feedbackPanel.SetActive(false);
    }
    
    public void OnLoginButtonClick()
    {
        StartCoroutine(LoginRequest());
    }

    public void OnRegisterButtonClick()
    {
        StartCoroutine(RegisterRequest());
    }

    // --- FUNGSI KOMUNIKASI DENGAN SERVER ---

    private IEnumerator LoginRequest()
    {
        WWWForm form = new WWWForm();
        form.AddField("email", emailInputLogin.text);
        form.AddField("password", passwordInputLogin.text);

        // Gunakan ApiConfig
        UnityWebRequest request = UnityWebRequest.Post(apiUrl + "/login", form);
        request.SetRequestHeader("Accept", "application/json");
        
        ShowFeedbackModal("Mencoba masuk...", "loading");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            if (request.responseCode == 401)
            {
                ShowFeedbackModal("Email atau Password salah!", "failure");
            }
            else
            {
                ShowFeedbackModal("Error Jaringan: " + request.error, "failure");
            }
        }
        else
        {
            string jsonResponse = request.downloadHandler.text;
            var response = JObject.Parse(jsonResponse);

            if (response["access_token"] != null)
            {
                ShowFeedbackModal("Login Berhasil!", "success");
                
                // SIMPAN TOKEN DI SINI (PENTING UNTUK SESI)
                PlayerPrefs.SetString("user_token", response["access_token"].ToString());
                PlayerPrefs.Save(); // Memastikan tersimpan ke disk

                yield return new WaitForSeconds(1.5f);
                SceneManager.LoadScene("MainMenuScene"); 
            }
            else
            {
                ShowFeedbackModal(response["message"]?.ToString() ?? "Login Gagal!", "failure");
            }
        }
    }

    private IEnumerator RegisterRequest()
    {
        WWWForm form = new WWWForm();
        form.AddField("name", nameInputRegister.text);
        form.AddField("email", emailInputRegister.text);
        form.AddField("password", passwordInputRegister.text);
        form.AddField("password_confirmation", passwordConfirmationInputRegister.text);

        UnityWebRequest request = UnityWebRequest.Post(apiUrl + "/register", form);
        request.SetRequestHeader("Accept", "application/json");
        
        ShowFeedbackModal("Mendaftarkan akun...", "loading");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            if (request.responseCode == 400)
            {
                string jsonResponse = request.downloadHandler.text;
                var response = JObject.Parse(jsonResponse);
                var firstError = response.First.First.ToString();
                ShowFeedbackModal(firstError, "failure");
            }
            else
            {
                ShowFeedbackModal("Error: " + request.error, "failure");
            }
        }
        else
        {
            string jsonResponse = request.downloadHandler.text;
            var response = JObject.Parse(jsonResponse);

            if (response["access_token"] != null)
            {
                ShowFeedbackModal("Registrasi Berhasil!", "success");
                
                // SIMPAN TOKEN JUGA SAAT REGISTER (Auto Login)
                PlayerPrefs.SetString("user_token", response["access_token"].ToString());
                PlayerPrefs.Save();
                
                yield return new WaitForSeconds(1.5f);
                SceneManager.LoadScene("MainMenuScene"); // Langsung masuk
            }
            else
            {
                ShowFeedbackModal(response["message"]?.ToString() ?? "Registrasi Gagal!", "failure");
            }
        }
    }

    // --- HELPER MODAL ---
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