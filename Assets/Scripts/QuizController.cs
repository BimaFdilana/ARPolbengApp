using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json.Linq;
using System.Collections;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class QuizController : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI questionText;
    public Transform answerContainer;
    public GameObject answerButtonPrefab;
    public Button submitButton; // Boleh dikosongkan/diabaikan

    [Header("Feedback")]
    public GameObject feedbackPanel;     
    public TextMeshProUGUI feedbackMessage; 
    public GameObject loadingSpinner;
    public GameObject successIcon;
    public GameObject failureIcon;

    private int correctAnswerIndex;
    private int missionId;
    private string token;

    public void InitializeQuiz(string jsonString, int id)
    {
        missionId = id;
        token = PlayerPrefs.GetString("user_token");

        if(feedbackPanel) feedbackPanel.SetActive(false);
        // Bersihkan jawaban lama
        foreach (Transform child in answerContainer) Destroy(child.gameObject);

        try 
        {
            JToken token = JToken.Parse(jsonString);
            if (token.Type == JTokenType.String) token = JToken.Parse(token.ToString());
            JObject quizData = (JObject)token;

            string question = quizData["question"].ToString();
            JArray answers = null;
            if (quizData["options"] != null) answers = (JArray)quizData["options"];
            else if (quizData["answers"] != null) answers = (JArray)quizData["answers"];
            
            correctAnswerIndex = (int)quizData["correct_index"];

            questionText.text = question;

            // --- PERBAIKAN VISUAL DI SINI ---
            for (int i = 0; i < answers.Count; i++)
            {
                int idx = i; 
                GameObject btnObj = Instantiate(answerButtonPrefab, answerContainer);
                
                // 1. Reset Transform agar tidak gepeng/hilang
                btnObj.transform.localScale = Vector3.one; 
                btnObj.transform.localPosition = Vector3.zero;
                
                // 2. Paksa Warna & Ukuran Teks (Anti-Invisible)
                TextMeshProUGUI btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                {
                    btnText.text = answers[i].ToString();
                    btnText.color = Color.black; // Paksa Hitam biar kontras
                    btnText.fontSize = 40;       // Paksa ukuran font besar
                }

                // 3. Paksa Tombol Aktif
                btnObj.SetActive(true);

                btnObj.GetComponent<Button>().onClick.AddListener(() => OnAnswerClicked(idx));
            }
            
            // 4. Paksa Layout Refresh (Penting!)
            LayoutRebuilder.ForceRebuildLayoutImmediate(answerContainer.GetComponent<RectTransform>());
        }
        catch (System.Exception e) { ShowFeedback("Error JSON: " + e.Message, "failure"); }
    }

    // --- LOGIKA KLIK ---
    void OnAnswerClicked(int index)
    {
        if (index == correctAnswerIndex)
        {
            // BENAR -> LANGSUNG KIRIM REQUEST
            Debug.Log("JAWABAN BENAR! MENGIRIM DATA...");
            ShowFeedback("Benar! Menyimpan...", "success");
            StartCoroutine(PostFinishMission());
        }
        else
        {
            ShowFeedback("Salah, coba lagi.", "failure");
            StartCoroutine(HideFeedback(1.5f));
        }
    }

    IEnumerator PostFinishMission()
    {
        Debug.Log($"SENDING FINISH REQUEST: /missions/{missionId}/finish");

        UnityWebRequest request = new UnityWebRequest(ApiConfig.BaseUrl + "/missions/" + missionId + "/finish", "POST");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Authorization", "Bearer " + token);
        request.SetRequestHeader("Accept", "application/json");

        yield return request.SendWebRequest();

        Debug.Log("RESPONSE: " + request.downloadHandler.text);

        if (request.result == UnityWebRequest.Result.Success)
        {
            ShowFeedback("MISI SELESAI!", "success");
            yield return new WaitForSeconds(2f);
            SceneManager.LoadScene("MissionScene");
        }
        else
        {
            ShowFeedback("Gagal Koneksi: " + request.error, "failure");
            yield return new WaitForSeconds(2f);
            if(feedbackPanel) feedbackPanel.SetActive(false);
        }
    }

    void ShowFeedback(string msg, string state)
    {
        if(feedbackPanel == null) return;
        feedbackPanel.SetActive(true);
        if(feedbackMessage) feedbackMessage.text = msg;
        
        if(loadingSpinner) loadingSpinner.SetActive(state == "loading");
        if(successIcon) successIcon.SetActive(state == "success");
        if(failureIcon) failureIcon.SetActive(state == "failure");
    }

    IEnumerator HideFeedback(float delay)
    {
        yield return new WaitForSeconds(delay);
        if(feedbackPanel) feedbackPanel.SetActive(false);
    }
}