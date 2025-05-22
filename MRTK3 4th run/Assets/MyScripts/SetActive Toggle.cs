using UnityEngine;
using MixedReality.Toolkit.UX;

public class PanelToggleMRTK : MonoBehaviour
{
    [SerializeField] private PressableButton mrtkButton;
    [SerializeField] private GameObject panel;
    
    void Start()
    {
        // Subscribe to MRTK button events
        mrtkButton.OnClicked.AddListener(TogglePanel);
        
        // Start with panel hidden (optional)
        panel.SetActive(false);
    }
    
    void TogglePanel()
    {
        panel.SetActive(!panel.activeSelf);
    }
    
    void OnDestroy()
    {
        // Clean up listener
        if (mrtkButton != null)
            mrtkButton.OnClicked.RemoveListener(TogglePanel);
    }
}