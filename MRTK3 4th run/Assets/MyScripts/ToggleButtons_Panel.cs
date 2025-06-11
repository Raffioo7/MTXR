using UnityEngine;
using MixedReality.Toolkit.UX;

public class PanelToggleMRTKV2 : MonoBehaviour
{
    [SerializeField] private PressableButton toggleButton;
    [SerializeField] private PressableButton closeButton;
    [SerializeField] private GameObject panel;
    [SerializeField] private PressableButton additionalButton1;
    [SerializeField] private PressableButton additionalButton2;
    [SerializeField] private PressableButton additionalButton3;
    
    [Header("Additional Toggle Buttons")]
    [Tooltip("These buttons will also toggle the panel like the main toggle button")]
    [SerializeField] private PressableButton additionalToggleButton1;
    [SerializeField] private PressableButton additionalToggleButton2;
    [SerializeField] private PressableButton additionalToggleButton3;
    
    // Track which button opened the panel
    private PressableButton lastButtonUsed = null;
    
    [Header("Panel Content Reset")]
    [Tooltip("Method to call when resetting panel to default state")]
    public UnityEngine.Events.UnityEvent OnPanelReset;
    
    [Header("Connected Systems")]
    [Tooltip("Reference to SimpleInspectionLoader to refresh after close")]
    public SimpleInspectionLoader inspectionLoader;
    
    void Start()
    {
        // Find SimpleInspectionLoader if not assigned
        if (inspectionLoader == null)
            inspectionLoader = FindObjectOfType<SimpleInspectionLoader>();
        
        // Subscribe to MRTK button events
        toggleButton.OnClicked.AddListener(TogglePanel);
        closeButton.OnClicked.AddListener(HidePanel);
        
        // Subscribe additional toggle buttons with their own methods
        if (additionalToggleButton1 != null)
            additionalToggleButton1.OnClicked.AddListener(() => ConditionalToggle(additionalToggleButton1));
        if (additionalToggleButton2 != null)
            additionalToggleButton2.OnClicked.AddListener(() => ConditionalToggle(additionalToggleButton2));
        if (additionalToggleButton3 != null)
            additionalToggleButton3.OnClicked.AddListener(() => ConditionalToggle(additionalToggleButton3));
        
        // Start with panel and additional buttons hidden
        HidePanel();
    }
    
    void TogglePanel()
    {
        if (panel.activeSelf)
        {
            // Panel is open
            if (lastButtonUsed == toggleButton)
            {
                // Toggle button was last used - close the panel
                HidePanel();
            }
            else
            {
                // Different button was last used - reset panel contents but keep it open
                ResetPanelToDefault();
                lastButtonUsed = toggleButton;
            }
        }
        else
        {
            // Panel is closed - open it normally
            ShowPanel();
            lastButtonUsed = toggleButton; // Remember main toggle button opened it
        }
    }
    
    void ConditionalToggle(PressableButton clickedButton)
    {
        if (panel.activeSelf)
        {
            // Panel is open
            if (lastButtonUsed == clickedButton)
            {
                // Same button clicked - close the panel
                HidePanel();
            }
            else
            {
                // Different button clicked - switch ownership but keep panel open
                lastButtonUsed = clickedButton;
            }
        }
        else
        {
            // Panel is closed - open it and remember which button opened it
            ShowPanel();
            lastButtonUsed = clickedButton;
        }
    }
    
    void ShowPanel()
    {
        panel.SetActive(true);
        if (additionalButton1 != null)
            additionalButton1.gameObject.SetActive(true);
        if (additionalButton2 != null)
            additionalButton2.gameObject.SetActive(true);
        if (additionalButton3 != null)
            additionalButton3.gameObject.SetActive(true);
    }
    
    void ResetPanelToDefault()
    {
        // Invoke the reset event - this allows you to define what "default" means in the inspector
        OnPanelReset?.Invoke();
        
        Debug.Log("Panel contents reset to default state");
    }
    
    void HidePanel()
    {
        panel.SetActive(false);
        if (additionalButton1 != null)
            additionalButton1.gameObject.SetActive(false);
        if (additionalButton2 != null)
            additionalButton2.gameObject.SetActive(false);
        if (additionalButton3 != null)
            additionalButton3.gameObject.SetActive(false);
        
        // Reset the last button used when panel is hidden
        lastButtonUsed = null;
        
        // Refresh inspection loader buttons when panel is closed
        if (inspectionLoader != null)
        {
            inspectionLoader.RefreshInspections();
            Debug.Log("Refreshed inspection list after panel close");
        }
    }
    
    void OnDestroy()
    {
        // Clean up listeners
        if (toggleButton != null)
            toggleButton.OnClicked.RemoveListener(TogglePanel);
        if (closeButton != null)
            closeButton.OnClicked.RemoveListener(HidePanel);
        
        // Clean up additional toggle button listeners
        if (additionalToggleButton1 != null)
            additionalToggleButton1.OnClicked.RemoveAllListeners();
        if (additionalToggleButton2 != null)
            additionalToggleButton2.OnClicked.RemoveAllListeners();
        if (additionalToggleButton3 != null)
            additionalToggleButton3.OnClicked.RemoveAllListeners();
    }
}