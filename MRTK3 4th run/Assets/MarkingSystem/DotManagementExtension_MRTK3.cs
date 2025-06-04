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
    
    [Header("Debug")]
    public bool debugMode = true;
    
    // Private fields
    private PropertyClickHandler_MRTK3 propertyHandler;
    private DotPlacementHandler_MRTK3 dotHandler;
    private bool dotsVisible = true;
    
    void Start()
    {
        // Find required components
        propertyHandler = FindObjectOfType<PropertyClickHandler_MRTK3>();
        dotHandler = FindObjectOfType<DotPlacementHandler_MRTK3>();
        
        if (dotHandler == null)
        {
            Debug.LogError("DotManagementExtension: DotPlacementHandler_MRTK3 not found!");
            return;
        }
        
        // Set up all buttons
        SetupButtons();
    }
    
    void SetupButtons()
    {
        // Set up Clear All Dots button
        if (clearAllDotsButton != null)
        {
            SetupButton(clearAllDotsButton, ClearAllDots, "Clear All Dots");
        }
        else if (debugMode)
        {
            Debug.LogWarning("DotManagementExtension: Clear All Dots button not assigned");
        }
        
        // Set up Remove Last Dot button
        if (removeLastDotButton != null)
        {
            SetupButton(removeLastDotButton, RemoveLastDot, "Remove Last Dot");
        }
        else if (debugMode)
        {
            Debug.LogWarning("DotManagementExtension: Remove Last Dot button not assigned");
        }
        
        // Set up Export Positions button
        if (exportPositionsButton != null)
        {
            SetupButton(exportPositionsButton, ExportDotPositions, "Export Positions");
        }
        else if (debugMode)
        {
            Debug.LogWarning("DotManagementExtension: Export Positions button not assigned");
        }
        
        // Set up Toggle Visibility button
        if (toggleVisibilityButton != null)
        {
            SetupButton(toggleVisibilityButton, ToggleDotVisibility, "Toggle Visibility");
        }
        else if (debugMode)
        {
            Debug.LogWarning("DotManagementExtension: Toggle Visibility button not assigned");
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
            
            // Make sure dots are visible after clearing
            if (!dotsVisible && dotHandler.dotsParent != null)
            {
                dotsVisible = true;
                dotHandler.dotsParent.gameObject.SetActive(true);
            }
            
            if (debugMode)
                Debug.Log("DotManagementExtension: Cleared all dots");
        }
    }
    
    public void RemoveLastDot()
    {
        if (dotHandler != null)
        {
            dotHandler.RemoveLastDot();
            if (debugMode)
                Debug.Log("DotManagementExtension: Removed last dot");
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
            
            string export = "Dot Positions Export:\n";
            export += "====================\n";
            export += $"Total Dots: {positions.Count}\n";
            export += "--------------------\n";
            
            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 pos = positions[i];
                export += $"Dot {i + 1}: X={pos.x:F3}, Y={pos.y:F3}, Z={pos.z:F3}\n";
            }
            
            export += "====================\n";
            export += $"Exported at: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            
            Debug.Log(export);
            
            // Copy to clipboard (Unity 2020.1+)
            GUIUtility.systemCopyBuffer = export;
            
            if (debugMode)
                Debug.Log("DotManagementExtension: Dot positions exported to console and clipboard");
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
    
    // Optional: Visual feedback for toggle button
    void UpdateToggleButtonVisual()
    {
        if (toggleVisibilityButton != null)
        {
            Renderer buttonRenderer = toggleVisibilityButton.GetComponentInChildren<Renderer>();
            if (buttonRenderer != null)
            {
                // Change color based on visibility state
                buttonRenderer.material.color = dotsVisible ? Color.white : Color.gray;
            }
        }
    }
    
    void Update()
    {
        // Update toggle button visual if needed
        UpdateToggleButtonVisual();
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