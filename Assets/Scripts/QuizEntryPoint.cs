using UnityEngine;

public class QuizEntryPoint : MonoBehaviour
{
    [Header("References")]
    public QuizController quizController; // Drag QuizPanel ke sini

    void Start()
    {
        string quizJson = PlayerPrefs.GetString("active_mission_quiz_data", "");
        int missionId = PlayerPrefs.GetInt("active_mission_id", 0);

        // --- TAMBAHKAN LOG INI ---
        Debug.Log("--------------------------------------------------");
        Debug.Log($"[DEBUG DATA] ID: {missionId}");
        Debug.Log($"[DEBUG JSON RAW]: {quizJson}"); // <--- Kita mau lihat ini
        Debug.Log("--------------------------------------------------");

        if (string.IsNullOrEmpty(quizJson) || quizJson == "null" || quizJson == "{}")
        {
            Debug.LogError("Data Kosong!");
            return;
        }

        if (quizController != null)
        {
            quizController.InitializeQuiz(quizJson, missionId);
        }
    }
}