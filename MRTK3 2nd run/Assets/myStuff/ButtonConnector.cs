using UnityEngine;

#if USING_MRTK3
using Microsoft.MixedReality.Toolkit.UX;
#endif

public class ButtonConnector : MonoBehaviour
{
    [SerializeField] private HoloLensCamera cameraController;
    
    // The Unity Inspector will show this field only if MRTK3 is available
    #if USING_MRTK3
    [SerializeField] private PressableButton mrtkButton;
    #endif
    
    void Start()
    {
        #if USING_MRTK3
        // Connect the MRTK button if available
        if (mrtkButton != null)
        {
            mrtkButton.OnClicked.AddListener(OnButtonPressed);
            Debug.Log("MRTK button connected successfully");
        }
        else
        {
            // Try to find the button on this GameObject
            mrtkButton = GetComponent<PressableButton>();
            if (mrtkButton != null)
            {
                mrtkButton.OnClicked.AddListener(OnButtonPressed);
                Debug.Log("MRTK button found and connected");
            }
            else
            {
                Debug.LogWarning("No MRTK button found, button functionality will be limited");
            }
        }
        #else
        Debug.Log("MRTK3 not detected, please connect button manually via Unity Events");
        #endif
        
        // If no camera controller is assigned, try to find one in the scene
        if (cameraController == null)
        {
            cameraController = FindObjectOfType<HoloLensCamera>();
            if (cameraController == null)
            {
                Debug.LogError("No HoloLensCamera found in the scene!");
            }
        }
    }
    
    // This method can be called by the button through the Unity Event system
    public void OnButtonPressed()
    {
        if (cameraController != null)
        {
            Debug.Log("Button pressed, taking picture");
            cameraController.TakePicture();
        }
        else
        {
            Debug.LogError("Cannot take picture - camera controller not found");
        }
    }
}