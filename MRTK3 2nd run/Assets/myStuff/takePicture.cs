using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Windows.WebCam;

public class HoloLensCamera : MonoBehaviour
{
    [SerializeField] private AudioClip shutterSound; // Optional for audio feedback
    [SerializeField] private GameObject flashEffect; // Optional for visual feedback
    
    private UnityEngine.Windows.WebCam.PhotoCapture photoCaptureObject = null;
    private Resolution cameraResolution;
    private bool isCapturing = false;
    private AudioSource audioSource;

    void Start()
    {
        // Setup audio source for feedback
        audioSource = gameObject.AddComponent<AudioSource>();
        
        // Initialize camera
        StartCoroutine(InitializeCamera());
    }

    private IEnumerator InitializeCamera()
    {
        // Request camera permission
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        }

        if (Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Debug.Log("Camera permission granted, initializing camera");
            
            // Get available resolutions
            if (UnityEngine.Windows.WebCam.PhotoCapture.SupportedResolutions != null && 
                UnityEngine.Windows.WebCam.PhotoCapture.SupportedResolutions.Count() > 0)
            {
                // Select a suitable resolution - using middle resolution for balance
                var resolutions = UnityEngine.Windows.WebCam.PhotoCapture.SupportedResolutions
                    .OrderBy(res => res.width * res.height).ToList();
                
                if (resolutions.Count > 0)
                {
                    // Pick middle resolution for balance between quality and performance
                    int middleIndex = resolutions.Count / 2;
                    cameraResolution = resolutions[middleIndex];
                    
                    Debug.Log($"Selected camera resolution: {cameraResolution.width}x{cameraResolution.height}");
                    
                    // Create a PhotoCapture object
                    UnityEngine.Windows.WebCam.PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
                }
            }
            else
            {
                Debug.LogError("No camera resolutions found!");
            }
        }
        else
        {
            Debug.LogError("Camera permission denied!");
        }
    }

    private void OnPhotoCaptureCreated(UnityEngine.Windows.WebCam.PhotoCapture captureObject)
    {
        photoCaptureObject = captureObject;
        
        Debug.Log("Photo capture object created");
        
        // Set camera parameters
        CameraParameters cameraParams = new CameraParameters
        {
            hologramOpacity = 0.0f,  // 0 for fully transparent holograms, 1 for fully opaque
            cameraResolutionWidth = cameraResolution.width,
            cameraResolutionHeight = cameraResolution.height,
            pixelFormat = CapturePixelFormat.BGRA32
        };
        
        // Start the camera
        photoCaptureObject.StartPhotoModeAsync(cameraParams, OnPhotoModeStarted);
    }

    private void OnPhotoModeStarted(UnityEngine.Windows.WebCam.PhotoCapture.PhotoCaptureResult result)
    {
        if (result.success)
        {
            Debug.Log("Photo mode started successfully");
        }
        else
        {
            Debug.LogError($"Failed to start photo mode: {result.hResult}");
        }
    }

    // Public method to be called by the button connector
    public void TakePicture()
    {
        if (photoCaptureObject != null && !isCapturing)
        {
            isCapturing = true;
            
            // Play capture effects
            PlayCaptureEffect();
            
            // Generate a unique filename with timestamp
            string filename = $"HoloLensPhoto_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            string filePath = System.IO.Path.Combine(Application.persistentDataPath, filename);
            
            Debug.Log($"Taking photo and saving to: {filePath}");
            
            // Capture the photo
            photoCaptureObject.TakePhotoAsync(filePath, 
                PhotoCaptureFileOutputFormat.JPG, 
                OnCapturedPhotoToDisk);
        }
        else if (photoCaptureObject == null)
        {
            Debug.LogError("Cannot take picture - camera not initialized");
        }
    }

    private void OnCapturedPhotoToDisk(UnityEngine.Windows.WebCam.PhotoCapture.PhotoCaptureResult result)
    {
        isCapturing = false;
        
        if (result.success)
        {
            Debug.Log("Photo captured successfully!");
        }
        else
        {
            Debug.LogError($"Failed to capture photo: {result.hResult}");
        }
    }
    
    private void PlayCaptureEffect()
    {
        // Play shutter sound
        if (shutterSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(shutterSound);
        }
        
        // Show flash effect
        if (flashEffect != null)
        {
            flashEffect.SetActive(true);
            StartCoroutine(DisableFlashAfterDelay(0.2f));
        }
    }
    
    private System.Collections.IEnumerator DisableFlashAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        flashEffect.SetActive(false);
    }

    private void OnDestroy()
    {
        // Clean up
        if (photoCaptureObject != null)
        {
            photoCaptureObject.StopPhotoModeAsync(OnPhotoModeStopped);
        }
    }

    private void OnPhotoModeStopped(UnityEngine.Windows.WebCam.PhotoCapture.PhotoCaptureResult result)
    {
        photoCaptureObject.Dispose();
        photoCaptureObject = null;
        Debug.Log("Photo capture stopped and disposed");
    }
}