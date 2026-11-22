using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class KeyboardSizer : MonoBehaviour
{
    [Header("Target Panel")]
    public RectTransform contentPanel; // Panel yang mau digeser (Login/Register Panel)
    
    [Header("Settings")]
    public float shiftAmount = 500f; // Berapa jauh digeser ke atas (pixel)
    public bool useAutoDetect = true; // Coba deteksi tinggi keyboard otomatis

    private float originalY;
    private bool isShifted = false;

    void Start()
    {
        if (contentPanel != null)
        {
            originalY = contentPanel.anchoredPosition.y;
        }
    }

    void Update()
    {
        // Cek apakah ada InputField yang sedang aktif (ditekan)
        GameObject selectedObj = EventSystem.current.currentSelectedGameObject;
        
        bool inputFieldActive = false;
        if (selectedObj != null)
        {
            // Cek apakah object yang dipilih adalah InputField (TMP atau Legacy)
            if (selectedObj.GetComponent<TMPro.TMP_InputField>() != null || selectedObj.GetComponent<InputField>() != null)
            {
                inputFieldActive = true;
            }
        }

        // Logika Geser
        // Jika keyboard muncul ATAU input field sedang aktif
        if ((TouchScreenKeyboard.visible || inputFieldActive) && !isShifted)
        {
            ShiftUp();
        }
        else if (!TouchScreenKeyboard.visible && !inputFieldActive && isShifted)
        {
            ShiftDown();
        }
    }

    void ShiftUp()
    {
        isShifted = true;
        float moveY = shiftAmount;

        // Coba deteksi tinggi keyboard asli jika auto detect nyala
        if (useAutoDetect && TouchScreenKeyboard.area.height > 0)
        {
            moveY = TouchScreenKeyboard.area.height;
        }

        // Geser panel
        contentPanel.anchoredPosition = new Vector2(contentPanel.anchoredPosition.x, originalY + moveY);
    }

    void ShiftDown()
    {
        isShifted = false;
        // Kembalikan ke posisi awal
        contentPanel.anchoredPosition = new Vector2(contentPanel.anchoredPosition.x, originalY);
    }
}