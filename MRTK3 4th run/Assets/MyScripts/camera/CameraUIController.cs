using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MixedReality.Toolkit.UX;

public class CameraUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private PressableButton mixedRealityButton;
    [SerializeField] private PressableButton realWorldButton;
    [SerializeField] private PressableButton toggleUIButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI lastCaptureText;
    [SerializeField] private GameObject capturePanel;
    
    [Header("Camera Reference")]
    [SerializeField] private HoloLensCameraCapture cameraCapture;
    
    private void Start()
    {
        SetupUI();
        SetupCameraEvents();
        UpdateStatusText();
    }
    
    private void SetupUI()
    {
        // Setup MRTK3 PressableButton listeners
        if (mixedRealityButton != null)
        {
            mixedRealityButton.OnClicked.AddListener(() => {
                cameraCapture?.CaptureHologramInclusivePhoto();
                ShowFeedback("Taking Mixed Reality photo...");
            });
        }
        
        if (realWorldButton != null)
        {
            realWorldButton.OnClicked.AddListener(() => {
                cameraCapture?.CaptureRealWorldOnlyPhoto();
                ShowFeedback("Taking Real World photo...");
            });
        }
        
        if (toggleUIButton != null)
        {
            toggleUIButton.OnClicked.AddListener(() => {
                cameraCapture?.ToggleCaptureUI();
            });
        }
        
        // Initially hide the capture panel
        if (capturePanel != null)
        {
            capturePanel.SetActive(false);
        }
    }
    
    private void SetupCameraEvents()
    {
        if (cameraCapture != null)
        {
            cameraCapture.OnCaptureComplete += OnCaptureSuccess;
            cameraCapture.OnCaptureError += OnCaptureError;
        }
    }
    
    private void OnCaptureSuccess(string filename)
    {
        ShowFeedback($"✓ Photo saved: {filename}", Color.green);
        if (lastCaptureText != null)
        {
            lastCaptureText.text = $"Last: {filename}";
        }
    }
    
    private void OnCaptureError(string error)
    {
        ShowFeedback($"✗ Error: {error}", Color.red);
    }
    
    private void ShowFeedback(string message, Color? color = null)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color ?? Color.white;
        }
        
        Debug.Log($"Camera UI: {message}");
        
        // Clear the message after 3 seconds
        Invoke(nameof(ClearFeedback), 3f);
    }
    
    private void ClearFeedback()
    {
        UpdateStatusText();
    }
    
    private void UpdateStatusText()
    {
        if (statusText != null && cameraCapture != null)
        {
            statusText.text = cameraCapture.GetCameraStatus();
            statusText.color = Color.white;
        }
    }
    
    // Public methods for external triggers (voice commands, gestures, etc.)
    public void TakeMixedRealityPhoto()
    {
        cameraCapture?.CaptureHologramInclusivePhoto();
        ShowFeedback("Taking Mixed Reality photo...");
    }
    
    public void TakeRealWorldPhoto()
    {
        cameraCapture?.CaptureRealWorldOnlyPhoto();
        ShowFeedback("Taking Real World photo...");
    }
    
    public void ToggleCapturePanel()
    {
        if (capturePanel != null)
        {
            capturePanel.SetActive(!capturePanel.activeSelf);
        }
    }
    
    // Update status periodically
    private void Update()
    {
        // Update status every 5 seconds
        if (Time.time % 5f < 0.1f)
        {
            UpdateStatusText();
        }
    }
    
}