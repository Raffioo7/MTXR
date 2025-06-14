using UnityEngine;
using MixedReality.Toolkit.UX;

public class PanelToggleMRTKVariant : MonoBehaviour
{
    [SerializeField] private PressableButton mrtkButton;
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform positionReference;
    
    [Header("Position Settings")]
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [SerializeField] private bool matchRotation = false;
    [SerializeField] private bool updatePositionOnToggle = true;
    
    void Start()
    {
        // Subscribe to MRTK button events
        mrtkButton.OnClicked.AddListener(TogglePanel);
        
        // Start with panel hidden (optional)
        panel.SetActive(false);
    }
    
    void TogglePanel()
    {
        bool isActive = !panel.activeSelf;
        panel.SetActive(isActive);
        
        // Update position when showing the panel
        if (isActive && updatePositionOnToggle && positionReference != null)
        {
            UpdatePanelPosition();
        }
    }
    
    void UpdatePanelPosition()
    {
        if (positionReference == null || panel == null) return;
        
        // Set position with offset
        panel.transform.position = positionReference.position + positionOffset;
        
        // Optionally match rotation
        if (matchRotation)
        {
            panel.transform.rotation = positionReference.rotation;
        }
    }
    
    // Public method to manually update position
    public void SetPositionReference(Transform newReference)
    {
        positionReference = newReference;
        if (panel.activeSelf && updatePositionOnToggle)
        {
            UpdatePanelPosition();
        }
    }
    
    // Public method to update position at any time
    public void ForceUpdatePosition()
    {
        UpdatePanelPosition();
    }
    
    void OnDestroy()
    {
        // Clean up listener
        if (mrtkButton != null)
            mrtkButton.OnClicked.RemoveListener(TogglePanel);
    }
}