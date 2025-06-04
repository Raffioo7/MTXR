using UnityEngine;
using System.Reflection;
using System;

public class DotManagementExtension_MRTK3 : MonoBehaviour
{
    public enum ThirdButtonMode
    {
        ClearAllDots,
        RemoveLastDot,
        ExportDotPositions,
        ToggleDotVisibility
    }
    
    [Header("MRTK3 Button")]
    [Tooltip("The MRTK3 button that will trigger the dot management action")]
    public GameObject dotManagementButton;
    
    [Header("Button Settings")]
    public ThirdButtonMode buttonFunction = ThirdButtonMode.ClearAllDots;
    
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
        
        if (propertyHandler == null || dotHandler == null)
        {
            Debug.LogError("DotManagementExtension: Missing required components!");
            return;
        }
        
        // Hook into the button
        SetupDotManagementButton();
    }
    
    void SetupDotManagementButton()
    {
        if (dotManagementButton == null)
        {
            Debug.LogWarning("DotManagementExtension: No dot management button assigned!");
            return;
        }
        
        Component buttonInteractable = dotManagementButton.GetComponent("StatefulInteractable");
        if (buttonInteractable != null)
        {
            bool subscribed = TrySubscribeToButtonClick(buttonInteractable);
            
            if (debugMode)
            {
                if (subscribed)
                    Debug.Log("DotManagementExtension: Successfully set up dot management button");
                else
                    Debug.LogWarning("DotManagementExtension: Failed to set up dot management button");
            }
        }
        else if (debugMode)
        {
            Debug.LogWarning("DotManagementExtension: No StatefulInteractable found on dot management button");
        }
    }
    
    bool TrySubscribeToButtonClick(Component interactable)
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
                    eventValue.AddListener(OnDotManagementButtonClick);
                    return true;
                }
            }
            
            PropertyInfo propertyInfo = interactableType.GetProperty(eventName);
            if (propertyInfo != null)
            {
                var eventValue = propertyInfo.GetValue(interactable) as UnityEngine.Events.UnityEvent;
                if (eventValue != null)
                {
                    eventValue.AddListener(OnDotManagementButtonClick);
                    return true;
                }
            }
        }
        
        return false;
    }
    
    public void OnDotManagementButtonClick()
    {
        switch (buttonFunction)
        {
            case ThirdButtonMode.ClearAllDots:
                ClearAllDots();
                break;
                
            case ThirdButtonMode.RemoveLastDot:
                RemoveLastDot();
                break;
                
            case ThirdButtonMode.ExportDotPositions:
                ExportDotPositions();
                break;
                
            case ThirdButtonMode.ToggleDotVisibility:
                ToggleDotVisibility();
                break;
        }
    }
    
    void ClearAllDots()
    {
        if (dotHandler != null)
        {
            dotHandler.ClearAllDots();
            if (debugMode)
                Debug.Log("DotManagementExtension: Cleared all dots");
        }
    }
    
    void RemoveLastDot()
    {
        if (dotHandler != null)
        {
            dotHandler.RemoveLastDot();
            if (debugMode)
                Debug.Log("DotManagementExtension: Removed last dot");
        }
    }
    
    void ExportDotPositions()
    {
        if (dotHandler != null)
        {
            var positions = dotHandler.GetAllDotPositions();
            
            string export = "Dot Positions Export:\n";
            export += "====================\n";
            
            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 pos = positions[i];
                export += $"Dot {i + 1}: X={pos.x:F3}, Y={pos.y:F3}, Z={pos.z:F3}\n";
            }
            
            Debug.Log(export);
            
            // Optional: Copy to clipboard (Unity 2020.1+)
            GUIUtility.systemCopyBuffer = export;
            
            if (debugMode)
                Debug.Log("DotManagementExtension: Dot positions exported to console and clipboard");
        }
    }
    
    void ToggleDotVisibility()
    {
        if (dotHandler != null && dotHandler.dotsParent != null)
        {
            dotsVisible = !dotsVisible;
            dotHandler.dotsParent.gameObject.SetActive(dotsVisible);
            
            if (debugMode)
                Debug.Log($"DotManagementExtension: Dots are now {(dotsVisible ? "visible" : "hidden")}");
        }
    }
    
    // Public method to change button function at runtime
    public void SetButtonFunction(ThirdButtonMode mode)
    {
        buttonFunction = mode;
        if (debugMode)
            Debug.Log($"DotManagementExtension: Button function set to {mode}");
    }
    
    void OnDestroy()
    {
        // Remove button listener if it exists
        if (dotManagementButton != null)
        {
            Component buttonInteractable = dotManagementButton.GetComponent("StatefulInteractable");
            if (buttonInteractable != null)
            {
                UnsubscribeFromButtonClickEvent(buttonInteractable);
            }
        }
    }
    
    void UnsubscribeFromButtonClickEvent(Component interactable)
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
                    eventValue.RemoveListener(OnDotManagementButtonClick);
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
                    eventValue.RemoveListener(OnDotManagementButtonClick);
                    return;
                }
            }
        }
    }
}