using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Collections;
using UnityEngine;
using Microsoft.MixedReality.Toolkit;

#if UNITY_WSA && !UNITY_EDITOR
using UnityEngine.Windows.WebCam;
using Windows.Media.Capture;
using Windows.Storage;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
#endif

#if UNITY_WSA && !UNITY_EDITOR
using Windows.Media.Capture;
using Windows.Storage;
using System.Linq;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
#endif

public class HoloLensCameraCapture : MonoBehaviour
{
    [Header("Capture Settings")]
    [SerializeField] private int photoWidth = 1280;
    [SerializeField] private int photoHeight = 720;
    [SerializeField] private string saveFolder = "CapturedImages";
    
    [Header("UI References")]
    [SerializeField] private GameObject captureUI;
    
    // Events for UI feedback
    public event Action<string> OnCaptureComplete;
    public event Action<string> OnCaptureError;
    
#if UNITY_WSA && !UNITY_EDITOR
    private MediaCapture mediaCapture;
    private bool isInitialized = false;
#endif
    
    
#if UNITY_WSA && !UNITY_EDITOR
    private PhotoCapture photoCaptureObject = null;
    private Resolution cameraResolution;
#endif
    
    private void Start()
    {
        InitializeCamera();
    }
    
    private async void InitializeCamera()
    {
        try
        {
#if UNITY_WSA && !UNITY_EDITOR
            // Get available camera resolutions
            Resolution[] resolutions = PhotoCapture.SupportedResolutions.ToArray();
            if (resolutions.Length > 0)
            {
                // Use the highest resolution available
                cameraResolution = resolutions.OrderByDescending(res => res.width * res.height).First();
                Debug.Log($"Camera resolution set to: {cameraResolution.width}x{cameraResolution.height}");
            }
            
            // Initialize Windows MediaCapture for direct camera access
            await InitializeWindowsMediaCapture();
#else
            Debug.Log("PhotoCapture only available on HoloLens device");
#endif
            
            Debug.Log("Camera initialized successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to initialize camera: {ex.Message}");
            OnCaptureError?.Invoke($"Camera initialization failed: {ex.Message}");
        }
    }
    
#if UNITY_WSA && !UNITY_EDITOR
    private async Task InitializeWindowsMediaCapture()
    {
        if (mediaCapture == null)
        {
            mediaCapture = new MediaCapture();
            
            var settings = new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Video,
                PhotoCaptureSource = PhotoCaptureSource.VideoPreview
            };
            
            await mediaCapture.InitializeAsync(settings);
            isInitialized = true;
        }
    }
#endif
    
    /// <summary>
    /// Captures a photo with holograms included (Mixed Reality capture)
    /// </summary>
    public void CaptureHologramInclusivePhoto()
    {
        try
        {
            Debug.Log("Starting hologram-inclusive capture...");
            
#if UNITY_WSA && !UNITY_EDITOR
            if (PhotoCapture.SupportedResolutions != null && PhotoCapture.SupportedResolutions.Count() > 0)
            {
                StartMixedRealityCapture();
            }
            else
            {
                Debug.LogError("PhotoCapture not supported on this device");
                OnCaptureError?.Invoke("PhotoCapture not supported");
            }
#else
            // Fallback for editor - capture render texture
            StartCoroutine(CaptureManualMixedReality());
#endif
        }
        catch (Exception ex)
        {
            Debug.LogError($"Hologram capture failed: {ex.Message}");
            OnCaptureError?.Invoke($"Hologram capture failed: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Captures a photo of the real world only (no holograms)
    /// </summary>
    public async void CaptureRealWorldOnlyPhoto()
    {
        try
        {
            Debug.Log("Starting real-world only capture...");
            
#if UNITY_WSA && !UNITY_EDITOR
            if (isInitialized)
            {
                await CaptureRealWorldPhoto();
            }
            else
            {
                throw new InvalidOperationException("Camera not initialized");
            }
#else
            Debug.LogWarning("Real-world capture only works on HoloLens device");
            OnCaptureError?.Invoke("Real-world capture only available on device");
#endif
        }
        catch (Exception ex)
        {
            Debug.LogError($"Real-world capture failed: {ex.Message}");
            OnCaptureError?.Invoke($"Real-world capture failed: {ex.Message}");
        }
    }
    
#if UNITY_WSA && !UNITY_EDITOR
    private void StartMixedRealityCapture()
    {
        PhotoCapture.CreateAsync(false, OnPhotoCaptureMRCreated);
    }
    
    private void OnPhotoCaptureMRCreated(PhotoCapture captureObject)
    {
        photoCaptureObject = captureObject;
        
        CameraParameters cameraParameters = new CameraParameters();
        cameraParameters.hologramOpacity = 1.0f; // Include holograms
        cameraParameters.cameraResolutionWidth = cameraResolution.width;
        cameraParameters.cameraResolutionHeight = cameraResolution.height;
        cameraParameters.pixelFormat = CapturePixelFormat.BGRA32;
        
        captureObject.StartPhotoModeAsync(cameraParameters, OnPhotoModeMRStarted);
    }
    
    private void OnPhotoModeMRStarted(PhotoCapture.PhotoCaptureResult result)
    {
        if (result.success)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filename = $"MR_Capture_{timestamp}.jpg";
            var filepath = Path.Combine(Application.persistentDataPath, filename);
            
            photoCaptureObject.TakePhotoAsync(filepath, PhotoCaptureFileOutputFormat.JPG, OnMRPhotoCaptured);
        }
        else
        {
            Debug.LogError("Unable to start photo mode for MR capture");
            OnCaptureError?.Invoke("Failed to start mixed reality photo mode");
            StopMRPhotoCapture();
        }
    }
    
    private void OnMRPhotoCaptured(PhotoCapture.PhotoCaptureResult result)
    {
        if (result.success)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filename = $"MR_Capture_{timestamp}.jpg";
            Debug.Log($"Mixed Reality photo saved: {filename}");
            OnCaptureComplete?.Invoke($"Mixed Reality photo saved: {filename}");
        }
        else
        {
            Debug.LogError("Failed to save mixed reality photo");
            OnCaptureError?.Invoke("Failed to save mixed reality photo");
        }
        
        StopMRPhotoCapture();
    }
    
    private void StopMRPhotoCapture()
    {
        if (photoCaptureObject != null)
        {
            photoCaptureObject.StopPhotoModeAsync(OnPhotoModeMRStopped);
        }
    }
    
    private void OnPhotoModeMRStopped(PhotoCapture.PhotoCaptureResult result)
    {
        if (photoCaptureObject != null)
        {
            photoCaptureObject.Dispose();
            photoCaptureObject = null;
        }
    }
#endif
    
    private System.Collections.IEnumerator CaptureManualMixedReality()
    {
        // Alternative method using Unity's render texture approach for editor testing
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filename = $"MR_Manual_{timestamp}.png";
        
        // Wait for end of frame to ensure rendering is complete
        yield return new WaitForEndOfFrame();
        
        // Create render texture to capture the current view
        RenderTexture renderTexture = new RenderTexture(photoWidth, photoHeight, 24);
        Camera mainCamera = Camera.main;
        
        if (mainCamera != null)
        {
            RenderTexture currentRT = RenderTexture.active;
            mainCamera.targetTexture = renderTexture;
            mainCamera.Render();
            
            RenderTexture.active = renderTexture;
            Texture2D texture = new Texture2D(photoWidth, photoHeight, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, photoWidth, photoHeight), 0, 0);
            texture.Apply();
            
            mainCamera.targetTexture = null;
            RenderTexture.active = currentRT;
            
            // Save the texture
            byte[] data = texture.EncodeToPNG();
            string path = Path.Combine(Application.persistentDataPath, filename);
            File.WriteAllBytes(path, data);
            
            Destroy(texture);
            Destroy(renderTexture);
            
            Debug.Log($"Mixed Reality photo saved: {filename}");
            OnCaptureComplete?.Invoke($"Mixed Reality photo saved: {filename}");
        }
        else
        {
            OnCaptureError?.Invoke("No main camera found for capture");
        }
    }
    
#if UNITY_WSA && !UNITY_EDITOR
    private async Task CaptureRealWorldPhoto()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filename = $"RealWorld_{timestamp}.jpg";
        
        // Create file in Pictures library
        var picturesLibrary = KnownFolders.PicturesLibrary;
        var file = await picturesLibrary.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
        
        // Configure photo settings
        var imageProperties = ImageEncodingProperties.CreateJpeg();
        imageProperties.Width = (uint)photoWidth;
        imageProperties.Height = (uint)photoHeight;
        
        // Capture photo to file
        await mediaCapture.CapturePhotoToStorageFileAsync(imageProperties, file);
        
        Debug.Log($"Real-world photo saved to: {file.Path}");
        OnCaptureComplete?.Invoke($"Real-world photo saved: {filename}");
    }
#endif
    
    private async Task SaveTextureToFile(Texture2D texture, string filename)
    {
        byte[] data = texture.EncodeToPNG();
        
#if UNITY_WSA && !UNITY_EDITOR
        try
        {
            var picturesLibrary = KnownFolders.PicturesLibrary;
            var file = await picturesLibrary.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
            
            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(data);
                    await writer.StoreAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save file: {ex.Message}");
            throw;
        }
#else
        // For editor testing
        string path = Path.Combine(Application.persistentDataPath, filename);
        File.WriteAllBytes(path, data);
        Debug.Log($"File saved to: {path}");
#endif
    }
    
    /// <summary>
    /// Toggle capture UI visibility
    /// </summary>
    public void ToggleCaptureUI()
    {
        if (captureUI != null)
        {
            captureUI.SetActive(!captureUI.activeSelf);
        }
    }
    
    /// <summary>
    /// Get camera status for UI display
    /// </summary>
    public string GetCameraStatus()
    {
#if UNITY_WSA && !UNITY_EDITOR
        if (isInitialized)
        {
            return "Camera Ready - Both capture modes available";
        }
        else
        {
            return "Camera not initialized";
        }
#else
        return "Editor mode - Mixed Reality capture available via render texture";
#endif
    }
    
    private void OnDestroy()
    {
#if UNITY_WSA && !UNITY_EDITOR
        if (photoCaptureObject != null)
        {
            photoCaptureObject.Dispose();
            photoCaptureObject = null;
        }
        
        if (mediaCapture != null)
        {
            mediaCapture.Dispose();
            mediaCapture = null;
        }
#endif
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
#if UNITY_WSA && !UNITY_EDITOR
            if (photoCaptureObject != null)
            {
                photoCaptureObject.Dispose();
                photoCaptureObject = null;
            }
            
            if (mediaCapture != null)
            {
                mediaCapture.Dispose();
                mediaCapture = null;
                isInitialized = false;
            }
#endif
        }
        else
        {
            // Re-initialize when app resumes
            InitializeCamera();
        }
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            Debug.Log("Testing MR capture with keyboard...");
            CaptureHologramInclusivePhoto();
        }
    }
}