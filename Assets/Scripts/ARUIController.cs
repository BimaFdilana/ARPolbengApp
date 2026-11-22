using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Tombol Kembali dan Senter untuk scene misi AR (AR Foundation, tanpa Vuforia).
/// Senter memakai API torch AR Foundation 6.1+. Di versi yang belum mendukung (6.0),
/// tombol senter otomatis dinonaktifkan tanpa error.
/// </summary>
public class ARUIController : MonoBehaviour
{
    [Header("UI References")]
    public Button backButton;
    public Button flashButton;
    public TextMeshProUGUI flashButtonText; // Opsional: untuk ganti teks ON/OFF

    private bool isFlashOn;
    private ARCameraManager cameraManager;

    void Start()
    {
        cameraManager = FindFirstObjectByType<ARCameraManager>(FindObjectsInactive.Include);
        if (backButton != null) backButton.onClick.AddListener(OnBackButtonClicked);
        if (flashButton != null) flashButton.onClick.AddListener(OnFlashButtonClicked);
    }

    void OnBackButtonClicked()
    {
        if (isFlashOn) SetFlashlight(false);
        var mission = FindFirstObjectByType<ARMissionController>();
        if (mission != null) mission.StopXR();
        SceneManager.LoadScene("MissionScene");
    }

    void OnFlashButtonClicked()
    {
        bool target = !isFlashOn;
        if (SetFlashlight(target))
        {
            isFlashOn = target;
            if (flashButtonText != null)
            {
                flashButtonText.text = isFlashOn ? "OFF" : "⚡";
                flashButtonText.color = isFlashOn ? Color.yellow : Color.white;
            }
        }
        else if (flashButton != null)
        {
            flashButton.interactable = false;
            Debug.LogWarning("Senter tidak didukung (butuh AR Foundation 6.1+ dan HP yang mendukung).");
        }
    }

    bool SetFlashlight(bool enable)
    {
        if (cameraManager == null) cameraManager = FindFirstObjectByType<ARCameraManager>();
        var subsystem = cameraManager != null ? cameraManager.subsystem : null;
        if (subsystem == null) return false;

        // requestedCameraTorchMode tersedia mulai AR Foundation 6.1 -> diakses lewat reflection supaya tetap ter-compile di 6.0
        var prop = subsystem.GetType().GetProperty("requestedCameraTorchMode");
        if (prop == null || !prop.CanWrite || !prop.PropertyType.IsEnum) return false;
        try
        {
            prop.SetValue(subsystem, Enum.Parse(prop.PropertyType, enable ? "On" : "Off"));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Gagal mengatur senter: " + e.Message);
            return false;
        }
    }
}
