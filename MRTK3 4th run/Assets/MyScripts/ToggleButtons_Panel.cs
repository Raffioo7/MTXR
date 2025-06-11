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
    
    [Tooltip("Reference to DotManagementExtension to clear loops when panel closes")]
    public DotManagementExtension_MRTK3 dotManagementExtension;
    
    [Tooltip("Reference to SimpleTextReader to save inspection data when panel closes")]
    public SimpleTextReader textReader;
    
    [Header("Close Button Behavior")]
    [Tooltip("Clear all dots and loops when panel is closed via close button")]
    public bool clearLoopsOnClose = true;
    
    [Tooltip("Save inspection data (including loops) when close button is pressed")]
    public bool saveInspectionOnClose = true;
    
    [Tooltip("Only save if there's actual content (text, input, or loops)")]
    public bool saveOnlyIfContentExists = true;
    
    [Header("Toggle Button Behavior")]
    [Tooltip("Clear all dots and loops when toggle button is pressed (independent of panel state)")]
    public bool clearLoopsOnToggle = true;
    
    [Tooltip("Save inspection data when toggle button clears loops")]
    public bool saveInspectionOnToggle = true;
    
    void Start()
    {
        // Find SimpleInspectionLoader if not assigned
        if (inspectionLoader == null)
            inspectionLoader = FindObjectOfType<SimpleInspectionLoader>();
        
        // Find DotManagementExtension if not assigned
        if (dotManagementExtension == null)
            dotManagementExtension = FindObjectOfType<DotManagementExtension_MRTK3>();
        
        // Find SimpleTextReader if not assigned
        if (textReader == null)
            textReader = FindObjectOfType<SimpleTextReader>();
        
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
        // Always handle loop clearing first when toggle button is pressed
        if (clearLoopsOnToggle)
        {
            HandleLoopClearingOnToggle("main toggle button");
        }
        
        if (panel.activeSelf)
        {
            // Panel is open
            if (lastButtonUsed == toggleButton)
            {
                // Toggle button was last used - close the panel
                HidePanelFromToggle();
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
        // Always handle loop clearing first when additional toggle button is pressed
        if (clearLoopsOnToggle)
        {
            HandleLoopClearingOnToggle($"additional toggle button ({clickedButton.name})");
        }
        
        if (panel.activeSelf)
        {
            // Panel is open
            if (lastButtonUsed == clickedButton)
            {
                // Same button clicked - close the panel
                HidePanelFromToggle();
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
        
        // Also restore default placeholders if textReader is available
        if (textReader != null)
        {
            textReader.RestoreDefaultPlaceholders();
        }
        
        Debug.Log("Panel contents reset to default state");
    }
    
    void HidePanel()
    {
        // Save inspection data with loop system if enabled (close button behavior)
        if (saveInspectionOnClose && textReader != null)
        {
            bool hasContent = CheckIfContentExists();
            
            if (!saveOnlyIfContentExists || hasContent)
            {
                SaveInspectionWithLoops();
                Debug.Log("Saved inspection data with loop system before closing panel");
            }
            else
            {
                Debug.Log("No content to save - skipping inspection save");
            }
        }
        
        panel.SetActive(false);
        if (additionalButton1 != null)
            additionalButton1.gameObject.SetActive(false);
        if (additionalButton2 != null)
            additionalButton2.gameObject.SetActive(false);
        if (additionalButton3 != null)
            additionalButton3.gameObject.SetActive(false);
        
        // Reset the last button used when panel is hidden
        lastButtonUsed = null;
        
        // Clear all dots and loops when panel is closed via close button (after saving)
        if (clearLoopsOnClose && dotManagementExtension != null)
        {
            dotManagementExtension.ClearAllDots();
            Debug.Log("Cleared all dots and loops after panel close (close button)");
        }
        
        // Refresh inspection loader buttons when panel is closed
        if (inspectionLoader != null)
        {
            inspectionLoader.RefreshInspections();
            Debug.Log("Refreshed inspection list after panel close");
        }
    }
    
    void HidePanelFromToggle()
    {
        // This is called when toggle buttons actually close the panel
        // Loop clearing was already handled in the toggle methods
        
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
            Debug.Log("Refreshed inspection list after panel close (toggle button)");
        }
    }
    
    void HandleLoopClearingOnToggle(string buttonName)
    {
        // Save inspection data before clearing loops if enabled
        if (saveInspectionOnToggle && textReader != null)
        {
            bool hasContent = CheckIfContentExists();
            
            if (!saveOnlyIfContentExists || hasContent)
            {
                SaveInspectionWithLoops();
                Debug.Log($"Saved inspection data before clearing loops ({buttonName})");
            }
            else
            {
                Debug.Log($"No content to save when {buttonName} pressed");
            }
        }
        
        // Clear loops regardless of panel state
        if (dotManagementExtension != null)
        {
            dotManagementExtension.ClearAllDots();
            Debug.Log($"Cleared all dots and loops when {buttonName} pressed");
        }
    }
    
    bool CheckIfContentExists()
    {
        if (textReader == null) return false;
        
        // Check text fields
        bool hasTextContent = false;
        if (textReader.textField1 != null && !string.IsNullOrEmpty(textReader.textField1.text) && 
            textReader.textField1.text != textReader.defaultTextField1Text)
        {
            hasTextContent = true;
        }
        if (textReader.textField2 != null && !string.IsNullOrEmpty(textReader.textField2.text) && 
            textReader.textField2.text != textReader.defaultTextField2Text)
        {
            hasTextContent = true;
        }
        if (textReader.inputField1 != null && !string.IsNullOrEmpty(textReader.inputField1.text) && 
            textReader.inputField1.text != textReader.defaultInputField1Text)
        {
            hasTextContent = true;
        }
        
        // Check loop content
        bool hasLoopContent = false;
        if (dotManagementExtension != null)
        {
            DotPlacementHandler_MRTK3 dotHandler = FindObjectOfType<DotPlacementHandler_MRTK3>();
            if (dotHandler != null)
            {
                int completedLoops = dotHandler.GetCompletedLoopCount();
                int currentLoopDots = dotHandler.GetCurrentLoopDotCount();
                hasLoopContent = completedLoops > 0 || currentLoopDots > 0;
            }
        }
        
        Debug.Log($"Content check - Text: {hasTextContent}, Loops: {hasLoopContent}");
        return hasTextContent || hasLoopContent;
    }
    
    void SaveInspectionWithLoops()
    {
        if (textReader == null)
        {
            Debug.LogWarning("Cannot save inspection - SimpleTextReader not found");
            return;
        }
        
        // Use the enhanced save method that includes loop data
        textReader.SaveAllTextWithLoopsToJSON();
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