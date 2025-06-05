using UnityEngine;
using System.Reflection;
using System;

public class DotManagementExtension_MRTK3 : MonoBehaviour
{
    [Header("MRTK3 Buttons")]
    [Tooltip("Button to clear all dots")]
    public GameObject clearAllDotsButton;
    
    [Tooltip("Button to remove the last placed dot")]
    public GameObject removeLastDotButton;
    
    [Tooltip("Button to export dot positions")]
    public GameObject exportPositionsButton;
    
    [Tooltip("Button to toggle dot visibility")]
    public GameObject toggleVisibilityButton;
    
    [Header("Line Management Buttons")]
    [Tooltip("Button to toggle line visibility")]
    public GameObject toggleLinesButton;
    
    [Tooltip("Button to toggle auto-draw lines")]
    public GameObject toggleAutoDrawButton;
    
    [Tooltip("Button to manually update lines")]
    public GameObject updateLinesButton;
    
    [Header("Debug")]
    public bool debugMode = true;
    
    // Private fields
    private PropertyClickHandler_MRTK3 propertyHandler;
    private DotPlacementHandler_MRTK3 dotHandler;
    private LineConnectionHandler_MRTK3 lineHandler;
    private bool dotsVisible = true;
    private bool linesVisible = true;
    private bool autoDrawEnabled = true;
    
    void Start()
    {
        // Find required components
        propertyHandler = FindObjectOfType<PropertyClickHandler_MRTK3>();
        dotHandler = FindObjectOfType<DotPlacementHandler_MRTK3>();
        lineHandler = FindObjectOfType<LineConnectionHandler_MRTK3>();
        
        if (dotHandler == null)
        {
            Debug.LogError("DotManagementExtension: DotPlacementHandler_MRTK3 not found!");
            return;
        }
        
        if (lineHandler == null)
        {
            Debug.LogWarning("DotManagementExtension: LineConnectionHandler_MRTK3 not found! Line features will be disabled.");
        }
        
        // Set up all buttons
        SetupButtons();
    }
    
    void SetupButtons()
    {
        // Set up original buttons
        if (clearAllDotsButton != null)
        {
            SetupButton(clearAllDotsButton, ClearAllDots, "Clear All Dots");
        }
        else if (debugMode)
        {
            Debug.LogWarning("DotManagementExtension: Clear All Dots button not assigned");
        }
        
        if (removeLastDotButton != null)
        {
            SetupButton(removeLastDotButton, RemoveLastDot, "Remove Last Dot");
        }
        else if (debugMode)
        {
            Debug.LogWarning("DotManagementExtension: Remove Last Dot button not assigned");
        }
        
        if (exportPositionsButton != null)
        {
            SetupButton(exportPositionsButton, ExportDotPositions, "Export Positions");
        }
        else if (debugMode)
        {
            Debug.LogWarning("DotManagementExtension: Export Positions button not assigned");
        }
        
        if (toggleVisibilityButton != null)
        {
            SetupButton(toggleVisibilityButton, ToggleDotVisibility, "Toggle Visibility");
        }
        else if (debugMode)
        {
            Debug.LogWarning("DotManagementExtension: Toggle Visibility button not assigned");
        }
        
        // Set up new line management buttons
        if (toggleLinesButton != null)
        {
            SetupButton(toggleLinesButton, ToggleLineVisibility, "Toggle Lines");
        }
        else if (debugMode)
        {
            Debug.LogWarning("DotManagementExtension: Toggle Lines button not assigned");
        }
        
        if (toggleAutoDrawButton != null)
        {
            SetupButton(toggleAutoDrawButton, ToggleAutoDraw, "Toggle Auto Draw");
        }
        else if (debugMode)
        {
            Debug.LogWarning("DotManagementExtension: Toggle Auto Draw button not assigned");
        }
        
        if (updateLinesButton != null)
        {
            SetupButton(updateLinesButton, UpdateLines, "Update Lines");
        }
        else if (debugMode)
        {
            Debug.LogWarning("DotManagementExtension: Update Lines button not assigned");
        }
    }
    
    void SetupButton(GameObject buttonObj, UnityEngine.Events.UnityAction action, string buttonName)
    {
        Component buttonInteractable = buttonObj.GetComponent("StatefulInteractable");
        if (buttonInteractable != null)
        {
            bool subscribed = TrySubscribeToButtonClick(buttonInteractable, action);
            
            if (debugMode)
            {
                if (subscribed)
                    Debug.Log($"DotManagementExtension: Successfully set up {buttonName} button");
                else
                    Debug.LogWarning($"DotManagementExtension: Failed to set up {buttonName} button");
            }
        }
        else if (debugMode)
        {
            Debug.LogWarning($"DotManagementExtension: No StatefulInteractable found on {buttonName} button");
        }
    }
    
    bool TrySubscribeToButtonClick(Component interactable, UnityEngine.Events.UnityAction action)
    {
        Type interactableType = interactable.GetType();
        string[] possibleEventNames = { "OnClicked", "onClicked", "Clicked", "clicked" };
        
        foreach (string eventName in possibleEventNames)
        {
            FieldInfo fieldInfo = interactableType.GetField(eventName);
            if (fieldInfo != null)
            {
                var eventValue = fieldInfo.GetValue(interactable) as UnityEngine.Events.UnityEvent;
                if (eventValue != null)
                {
                    eventValue.AddListener(action);
                    return true;
                }
            }
            
            PropertyInfo propertyInfo = interactableType.GetProperty(eventName);
            if (propertyInfo != null)
            {
                var eventValue = propertyInfo.GetValue(interactable) as UnityEngine.Events.UnityEvent;
                if (eventValue != null)
                {
                    eventValue.AddListener(action);
                    return true;
                }
            }
        }
        
        return false;
    }
    
    public void ClearAllDots()
    {
        if (dotHandler != null)
        {
            dotHandler.ClearAllDots();
            
            // Clear lines too
            if (lineHandler != null)
            {
                lineHandler.ClearAllLines();
            }
            
            // Make sure dots are visible after clearing
            if (!dotsVisible && dotHandler.dotsParent != null)
            {
                dotsVisible = true;
                dotHandler.dotsParent.gameObject.SetActive(true);
            }
            
            if (debugMode)
                Debug.Log("DotManagementExtension: Cleared all dots and lines");
        }
    }
    
    public void RemoveLastDot()
    {
        if (dotHandler != null)
        {
            dotHandler.RemoveLastDot();
            
            // Update lines after removing dot
            if (lineHandler != null)
            {
                lineHandler.UpdateConnectionLines();
            }
            
            if (debugMode)
                Debug.Log("DotManagementExtension: Removed last dot and updated lines");
        }
    }
    
    public void ExportDotPositions()
    {
        if (dotHandler != null)
        {
            var positions = dotHandler.GetAllDotPositions();
            
            if (positions.Count == 0)
            {
                Debug.LogWarning("DotManagementExtension: No dots to export!");
                return;
            }
            
            string export = "Dot Positions and Line Connections Export:\n";
            export += "========================================\n";
            export += $"Total Dots: {positions.Count}\n";
            
            // Add line information if available
            if (lineHandler != null)
            {
                int lineCount = lineHandler.GetLineCount();
                export += $"Total Lines: {lineCount}\n";
            }
            
            export += "----------------------------------------\n";
            export += "Dot Positions:\n";
            
            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 pos = positions[i];
                export += $"Dot {i + 1}: X={pos.x:F3}, Y={pos.y:F3}, Z={pos.z:F3}\n";
            }
            
            // Add line connections
            if (lineHandler != null && positions.Count > 1)
            {
                export += "----------------------------------------\n";
                export += "Line Connections:\n";
                
                for (int i = 0; i < positions.Count - 1; i++)
                {
                    export += $"Line {i + 1}: Dot {i + 1} -> Dot {i + 2}\n";
                }
            }
            
            export += "========================================\n";
            export += $"Exported at: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            
            Debug.Log(export);
            
            // Copy to clipboard (Unity 2020.1+)
            GUIUtility.systemCopyBuffer = export;
            
            if (debugMode)
                Debug.Log("DotManagementExtension: Dot positions and line data exported to console and clipboard");
        }
    }
    
    public void ToggleDotVisibility()
    {
        if (dotHandler != null && dotHandler.dotsParent != null)
        {
            dotsVisible = !dotsVisible;
            dotHandler.dotsParent.gameObject.SetActive(dotsVisible);
            
            if (debugMode)
                Debug.Log($"DotManagementExtension: Dots are now {(dotsVisible ? "visible" : "hidden")}");
        }
    }
    
    public void ToggleLineVisibility()
    {
        if (lineHandler != null)
        {
            lineHandler.ToggleLineVisibility();
            linesVisible = !linesVisible;
            
            if (debugMode)
                Debug.Log($"DotManagementExtension: Lines are now {(linesVisible ? "visible" : "hidden")}");
        }
    }
    
    public void ToggleAutoDraw()
    {
        if (lineHandler != null)
        {
            autoDrawEnabled = !autoDrawEnabled;
            lineHandler.SetAutoDrawLines(autoDrawEnabled);
            
            if (debugMode)
                Debug.Log($"DotManagementExtension: Auto-draw lines is now {(autoDrawEnabled ? "enabled" : "disabled")}");
        }
    }
    
    public void UpdateLines()
    {
        if (lineHandler != null)
        {
            lineHandler.ForceUpdateLines();
            
            if (debugMode)
                Debug.Log("DotManagementExtension: Manually updated lines");
        }
    }
    
    // Visual feedback for buttons
    void UpdateButtonVisuals()
    {
        UpdateToggleButtonVisual(toggleVisibilityButton, dotsVisible);
        UpdateToggleButtonVisual(toggleLinesButton, linesVisible);
        UpdateToggleButtonVisual(toggleAutoDrawButton, autoDrawEnabled);
    }
    
    void UpdateToggleButtonVisual(GameObject button, bool isActive)
    {
        if (button != null)
        {
            Renderer buttonRenderer = button.GetComponentInChildren<Renderer>();
            if (buttonRenderer != null)
            {
                buttonRenderer.material.color = isActive ? Color.white : Color.gray;
            }
        }
    }
    
    void Update()
    {
        // Update button visuals
        UpdateButtonVisuals();
    }
    
    void OnDestroy()
    {
        // Remove all button listeners
        UnsubscribeFromAllButtons();
    }
    
    void UnsubscribeFromAllButtons()
    {
        if (clearAllDotsButton != null)
            UnsubscribeFromButton(clearAllDotsButton, ClearAllDots);
            
        if (removeLastDotButton != null)
            UnsubscribeFromButton(removeLastDotButton, RemoveLastDot);
            
        if (exportPositionsButton != null)
            UnsubscribeFromButton(exportPositionsButton, ExportDotPositions);
            
        if (toggleVisibilityButton != null)
            UnsubscribeFromButton(toggleVisibilityButton, ToggleDotVisibility);
            
        if (toggleLinesButton != null)
            UnsubscribeFromButton(toggleLinesButton, ToggleLineVisibility);
            
        if (toggleAutoDrawButton != null)
            UnsubscribeFromButton(toggleAutoDrawButton, ToggleAutoDraw);
            
        if (updateLinesButton != null)
            UnsubscribeFromButton(updateLinesButton, UpdateLines);
    }
    
    void UnsubscribeFromButton(GameObject buttonObj, UnityEngine.Events.UnityAction action)
    {
        Component buttonInteractable = buttonObj.GetComponent("StatefulInteractable");
        if (buttonInteractable != null)
        {
            UnsubscribeFromButtonClickEvent(buttonInteractable, action);
        }
    }
    
    void UnsubscribeFromButtonClickEvent(Component interactable, UnityEngine.Events.UnityAction action)
    {
        Type interactableType = interactable.GetType();
        string[] possibleEventNames = { "OnClicked", "onClicked", "Clicked", "clicked" };
        
        foreach (string eventName in possibleEventNames)
        {
            // Try as field
            FieldInfo fieldInfo = interactableType.GetField(eventName);
            if (fieldInfo != null)
            {
                var eventValue = fieldInfo.GetValue(interactable) as UnityEngine.Events.UnityEvent;
                if (eventValue != null)
                {
                    eventValue.RemoveListener(action);
                    return;
                }
            }
            
            // Try as property
            PropertyInfo propertyInfo = interactableType.GetProperty(eventName);
            if (propertyInfo != null)
            {
                var eventValue = propertyInfo.GetValue(interactable) as UnityEngine.Events.UnityEvent;
                if (eventValue != null)
                {
                    eventValue.RemoveListener(action);
                    return;
                }
            }
        }
    }
}