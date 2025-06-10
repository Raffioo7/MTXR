using UnityEngine;
using TMPro;
using UnityEngine.Events;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;

public class PropertyClickHandler_MRTK3 : MonoBehaviour
{
    [Header("UI References")]
    public GameObject propertyPanel;
    public TextMeshProUGUI propertyDisplay;
    public GameObject unhighlightButton; // Add reference to your MRTK3 button GameObject
    public GameObject secondButton; // Add reference to your second MRTK3 button GameObject
    public GameObject thirdButton; // Add reference to your third MRTK3 button GameObject
    public GameObject highlightDependentObject; // NEW: GameObject that's active only when something is highlighted
    public GameObject thirdButtonPanel; // NEW: Panel that controls third button visibility
    
    [Header("Settings")]
    public LayerMask clickableLayerMask = -1; // All layers
    
    [Header("Highlighting")]
    public Color highlightColor = Color.yellow;
    public bool highlightEmission = true;
    
    [Header("Debug")]
    public bool debugMode = true;
    
    private GameObject currentlySelected;
    private Material[] originalMaterials;
    private Material[] highlightMaterials;
    
    // Track the last state of the third button panel to avoid constant updates
    private bool lastThirdButtonPanelState = false;
    
    void Start()
    {
        // No need to find DotPlacementHandler anymore
        
        // Hide panel initially
        if (propertyPanel != null)
            propertyPanel.SetActive(false);
            
        // Hide unhighlight button initially and set up MRTK3 interaction
        if (unhighlightButton != null)
        {
            unhighlightButton.SetActive(false);
            // Set up MRTK3 button interaction
            SetupUnhighlightButton();
        }
        
        // Hide second button initially and set up MRTK3 interaction
        if (secondButton != null)
        {
            secondButton.SetActive(false);
            // Set up MRTK3 second button interaction
            SetupSecondButton();
        }
        
        // Hide third button initially and set up MRTK3 interaction
        if (thirdButton != null)
        {
            thirdButton.SetActive(false);
            // Set up MRTK3 third button interaction
            SetupThirdButton();
        }
        
        // NEW: Hide highlight-dependent object initially
        if (highlightDependentObject != null)
        {
            highlightDependentObject.SetActive(false);
            if (debugMode)
                Debug.Log("Highlight-dependent object hidden initially");
        }
            
        // Set up MRTK3 interactables on all objects with RevitData
        SetupInteractables();
    }
    
    // Helper method to check if third button panel is visible (blocks highlighting)
    private bool IsThirdButtonPanelVisible()
    {
        return thirdButtonPanel != null && thirdButtonPanel.activeInHierarchy;
    }
    
    // Helper method to check if highlighting should be blocked
    private bool IsHighlightingBlocked()
    {
        return IsThirdButtonPanelVisible();
    }
    
    // NEW: Helper method to check if something is currently highlighted
    private bool IsAnythingHighlighted()
    {
        return currentlySelected != null;
    }
    
    // NEW: Method to update third button visibility based on its panel
    private void UpdateThirdButtonVisibility()
    {
        if (thirdButton != null && thirdButtonPanel != null)
        {
            bool currentPanelState = thirdButtonPanel.activeInHierarchy;
            
            // Only update if the state has changed
            if (currentPanelState != lastThirdButtonPanelState)
            {
                thirdButton.SetActive(currentPanelState);
                lastThirdButtonPanelState = currentPanelState;
                
                if (debugMode)
                    Debug.Log($"Third button is now {(currentPanelState ? "ACTIVE" : "INACTIVE")} based on panel visibility");
            }
        }
    }
    
    void SetupUnhighlightButton()
    {
        if (unhighlightButton == null) return;
        
        // Get the MRTK3 StatefulInteractable component from the button
        Component buttonInteractable = unhighlightButton.GetComponent("StatefulInteractable");
        if (buttonInteractable != null)
        {
            bool subscribed = TrySubscribeToButtonClickEvent(buttonInteractable);
            
            if (debugMode)
            {
                if (subscribed)
                    Debug.Log("Successfully subscribed to unhighlight button click event");
                else
                    Debug.LogWarning("Failed to subscribe to unhighlight button click event");
            }
        }
        else if (debugMode)
        {
            Debug.LogWarning("No StatefulInteractable found on unhighlight button");
        }
    }
    
    bool TrySubscribeToButtonClickEvent(Component interactable)
    {
        Type interactableType = interactable.GetType();
        
        // Try different possible event names for MRTK3 buttons
        string[] possibleEventNames = { "OnClicked", "onClicked", "Clicked", "clicked" };
        
        foreach (string eventName in possibleEventNames)
        {
            // Try as field
            FieldInfo fieldInfo = interactableType.GetField(eventName);
            if (fieldInfo != null)
            {
                var eventValue = fieldInfo.GetValue(interactable) as UnityEvent;
                if (eventValue != null)
                {
                    eventValue.AddListener(UnhighlightSelected);
                    if (debugMode)
                        Debug.Log($"Successfully subscribed to button {eventName} field");
                    return true;
                }
            }
            
            // Try as property
            PropertyInfo propertyInfo = interactableType.GetProperty(eventName);
            if (propertyInfo != null)
            {
                var eventValue = propertyInfo.GetValue(interactable) as UnityEvent;
                if (eventValue != null)
                {
                    eventValue.AddListener(UnhighlightSelected);
                    if (debugMode)
                        Debug.Log($"Successfully subscribed to button {eventName} property");
                    return true;
                }
            }
        }
        
        return false;
    }
    
    void SetupSecondButton()
    {
        if (secondButton == null) return;
        
        // Get the MRTK3 StatefulInteractable component from the second button
        Component buttonInteractable = secondButton.GetComponent("StatefulInteractable");
        if (buttonInteractable != null)
        {
            bool subscribed = TrySubscribeToSecondButtonClickEvent(buttonInteractable);
            
            if (debugMode)
            {
                if (subscribed)
                    Debug.Log("Successfully subscribed to second button click event");
                else
                    Debug.LogWarning("Failed to subscribe to second button click event");
            }
        }
        else if (debugMode)
        {
            Debug.LogWarning("No StatefulInteractable found on second button");
        }
    }
    
    bool TrySubscribeToSecondButtonClickEvent(Component interactable)
    {
        Type interactableType = interactable.GetType();
        
        // Try different possible event names for MRTK3 buttons
        string[] possibleEventNames = { "OnClicked", "onClicked", "Clicked", "clicked" };
        
        foreach (string eventName in possibleEventNames)
        {
            // Try as field
            FieldInfo fieldInfo = interactableType.GetField(eventName);
            if (fieldInfo != null)
            {
                var eventValue = fieldInfo.GetValue(interactable) as UnityEvent;
                if (eventValue != null)
                {
                    eventValue.AddListener(OnSecondButtonClicked);
                    if (debugMode)
                        Debug.Log($"Successfully subscribed to second button {eventName} field");
                    return true;
                }
            }
            
            // Try as property
            PropertyInfo propertyInfo = interactableType.GetProperty(eventName);
            if (propertyInfo != null)
            {
                var eventValue = propertyInfo.GetValue(interactable) as UnityEvent;
                if (eventValue != null)
                {
                    eventValue.AddListener(OnSecondButtonClicked);
                    if (debugMode)
                        Debug.Log($"Successfully subscribed to second button {eventName} property");
                    return true;
                }
            }
        }
        
        return false;
    }
    
    void SetupThirdButton()
    {
        if (thirdButton == null) return;
        
        // Get the MRTK3 StatefulInteractable component from the third button
        Component buttonInteractable = thirdButton.GetComponent("StatefulInteractable");
        if (buttonInteractable != null)
        {
            bool subscribed = TrySubscribeToThirdButtonClickEvent(buttonInteractable);
            
            if (debugMode)
            {
                if (subscribed)
                    Debug.Log("Successfully subscribed to third button click event");
                else
                    Debug.LogWarning("Failed to subscribe to third button click event");
            }
        }
        else if (debugMode)
        {
            Debug.LogWarning("No StatefulInteractable found on third button");
        }
    }
    
    bool TrySubscribeToThirdButtonClickEvent(Component interactable)
    {
        Type interactableType = interactable.GetType();
        
        // Try different possible event names for MRTK3 buttons
        string[] possibleEventNames = { "OnClicked", "onClicked", "Clicked", "clicked" };
        
        foreach (string eventName in possibleEventNames)
        {
            // Try as field
            FieldInfo fieldInfo = interactableType.GetField(eventName);
            if (fieldInfo != null)
            {
                var eventValue = fieldInfo.GetValue(interactable) as UnityEvent;
                if (eventValue != null)
                {
                    eventValue.AddListener(OnThirdButtonClicked);
                    if (debugMode)
                        Debug.Log($"Successfully subscribed to third button {eventName} field");
                    return true;
                }
            }
            
            // Try as property
            PropertyInfo propertyInfo = interactableType.GetProperty(eventName);
            if (propertyInfo != null)
            {
                var eventValue = propertyInfo.GetValue(interactable) as UnityEvent;
                if (eventValue != null)
                {
                    eventValue.AddListener(OnThirdButtonClicked);
                    if (debugMode)
                        Debug.Log($"Successfully subscribed to third button {eventName} property");
                    return true;
                }
            }
        }
        
        return false;
    }
    
    void SetupInteractables()
    {
        // Find all objects with RevitData component
        RevitData[] revitObjects = FindObjectsOfType<RevitData>();
        
        if (debugMode)
            Debug.Log($"PropertyClickHandler: Found {revitObjects.Length} RevitData objects");
        
        foreach (RevitData revitData in revitObjects)
        {
            GameObject obj = revitData.gameObject;
            
            // Check if object is on the clickable layer
            if (((1 << obj.layer) & clickableLayerMask) == 0)
                continue;
            
            // Get existing StatefulInteractable component
            Component interactable = obj.GetComponent("StatefulInteractable");
            if (interactable != null)
            {
                if (debugMode)
                    Debug.Log($"Found StatefulInteractable on {obj.name}");
                
                // Try to find and subscribe to the OnClicked event
                bool subscribed = TrySubscribeToClickEvent(interactable, obj);
                
                if (debugMode && !subscribed)
                    Debug.LogWarning($"Failed to subscribe to click event on {obj.name}");
            }
            else if (debugMode)
            {
                Debug.LogWarning($"No StatefulInteractable found on {obj.name}");
            }
        }
    }
    
    bool TrySubscribeToClickEvent(Component interactable, GameObject targetObj)
    {
        Type interactableType = interactable.GetType();
        
        // Try different possible event names
        string[] possibleEventNames = { "OnClicked", "onClicked", "Clicked", "clicked" };
        
        foreach (string eventName in possibleEventNames)
        {
            // Try as field
            FieldInfo fieldInfo = interactableType.GetField(eventName);
            if (fieldInfo != null)
            {
                var eventValue = fieldInfo.GetValue(interactable) as UnityEvent;
                if (eventValue != null)
                {
                    eventValue.AddListener(() => OnObjectClicked(targetObj));
                    if (debugMode)
                        Debug.Log($"Successfully subscribed to {eventName} field on {targetObj.name}");
                    return true;
                }
            }
            
            // Try as property
            PropertyInfo propertyInfo = interactableType.GetProperty(eventName);
            if (propertyInfo != null)
            {
                var eventValue = propertyInfo.GetValue(interactable) as UnityEvent;
                if (eventValue != null)
                {
                    eventValue.AddListener(() => OnObjectClicked(targetObj));
                    if (debugMode)
                        Debug.Log($"Successfully subscribed to {eventName} property on {targetObj.name}");
                    return true;
                }
            }
        }
        
        // If standard approaches fail, try to find any UnityEvent in the type
        if (debugMode)
        {
            Debug.Log($"Looking for any UnityEvent in {interactableType.Name}...");
            
            // List all fields
            foreach (var field in interactableType.GetFields())
            {
                if (field.FieldType == typeof(UnityEvent) || field.FieldType.IsSubclassOf(typeof(UnityEvent)))
                {
                    Debug.Log($"Found UnityEvent field: {field.Name}");
                }
            }
            
            // List all properties
            foreach (var prop in interactableType.GetProperties())
            {
                if (prop.PropertyType == typeof(UnityEvent) || prop.PropertyType.IsSubclassOf(typeof(UnityEvent)))
                {
                    Debug.Log($"Found UnityEvent property: {prop.Name}");
                }
            }
        }
        
        return false;
    }
    
    // NEW: Method to update highlight-dependent object visibility
    private void UpdateHighlightDependentObject()
    {
        if (highlightDependentObject != null)
        {
            bool shouldBeActive = IsAnythingHighlighted();
            highlightDependentObject.SetActive(shouldBeActive);
            
            if (debugMode)
                Debug.Log($"Highlight-dependent object is now {(shouldBeActive ? "ACTIVE" : "INACTIVE")}");
        }
    }
    
    void Update()
    {
        // Press Escape to hide panel and clear selection
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ClearSelection();
        }
        
        // Check third button panel visibility every frame
        UpdateThirdButtonVisibility();
    }
    
    public void OnObjectClicked(GameObject clickedObject)
    {
        if (debugMode)
            Debug.Log($"OnObjectClicked called for: {clickedObject.name}");
        
        // Check if highlighting is blocked - prevent highlighting changes
        if (IsHighlightingBlocked())
        {
            if (debugMode)
                Debug.Log("PropertyClickHandler: Highlighting blocked - third button panel is visible, ignoring object selection");
            return;
        }
        
        // Clear previous selection first
        ClearSelection();
        
        // Get RevitData component
        RevitData revitData = clickedObject.GetComponent<RevitData>();
        
        if (revitData != null)
        {
            SelectObject(clickedObject, revitData);
        }
        else
        {
            if (debugMode)
                Debug.LogWarning($"No RevitData found on clicked object: {clickedObject.name}");
            HidePropertyPanel();
        }
    }
    
    void SelectObject(GameObject clickedObject, RevitData data)
    {
        currentlySelected = clickedObject;
        HighlightObject(clickedObject);
        ShowProperties(data, clickedObject);
        ShowUnhighlightButton(); // Show the button when an object is selected
        ShowSecondButton(); // Show the second button when an object is selected
        // Third button visibility is controlled by thirdButtonPanel in Update()
        
        // NEW: Update highlight-dependent object visibility
        UpdateHighlightDependentObject();
    }
    
    void HighlightObject(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null)
        {
            if (debugMode)
                Debug.LogWarning($"No Renderer found on {obj.name}");
            return;
        }
        
        // Store original materials
        originalMaterials = renderer.materials;
        highlightMaterials = new Material[originalMaterials.Length];
        
        // Create highlight materials
        for (int i = 0; i < originalMaterials.Length; i++)
        {
            highlightMaterials[i] = new Material(originalMaterials[i]);
            highlightMaterials[i].color = highlightColor;
            
            if (highlightEmission)
            {
                highlightMaterials[i].EnableKeyword("_EMISSION");
                highlightMaterials[i].SetColor("_EmissionColor", highlightColor * 0.3f);
            }
        }
        
        // Apply highlight materials
        renderer.materials = highlightMaterials;
        
        if (debugMode)
            Debug.Log($"Highlighted {obj.name}");
    }
    
    void ClearSelection()
    {
        if (currentlySelected != null)
        {
            // Restore original materials
            Renderer renderer = currentlySelected.GetComponent<Renderer>();
            if (renderer != null && originalMaterials != null)
            {
                renderer.materials = originalMaterials;
            }
            
            // Clean up highlight materials
            if (highlightMaterials != null)
            {
                foreach (Material mat in highlightMaterials)
                {
                    if (mat != null)
                        DestroyImmediate(mat);
                }
            }
            
            currentlySelected = null;
            originalMaterials = null;
            highlightMaterials = null;
        }
        
        HidePropertyPanel();
        HideUnhighlightButton(); // Hide the button when selection is cleared
        HideSecondButton(); // Hide the second button when selection is cleared
        // Third button visibility is controlled by thirdButtonPanel in Update()
        
        // NEW: Update highlight-dependent object visibility
        UpdateHighlightDependentObject();
    }
    
    void ShowProperties(RevitData data, GameObject clickedObject)
    {
        if (propertyPanel != null && propertyDisplay != null)
        {
            // Show the panel
            propertyPanel.SetActive(true);
        
            // Get all properties in display order (including hierarchy info)
            List<PropertyPair> displayProperties = data.GetDisplayProperties(clickedObject);
        
            // Build the display text
            string displayText = "";
            foreach (var property in displayProperties)
            {
                displayText += $"<b>{property.key}:</b> {property.value}\n";
            }
        
            // Remove the last newline
            displayText = displayText.TrimEnd('\n');
        
            propertyDisplay.text = displayText;
        
            if (debugMode)
                Debug.Log($"Showing properties for {clickedObject.name}");
        }
    }
    
    void HidePropertyPanel()
    {
        if (propertyPanel != null)
        {
            propertyPanel.SetActive(false);
        }
    }
    
    void ShowUnhighlightButton()
    {
        if (unhighlightButton != null)
        {
            unhighlightButton.SetActive(true);
            
            if (debugMode)
                Debug.Log("Unhighlight button shown");
        }
    }
    
    void HideUnhighlightButton()
    {
        if (unhighlightButton != null)
        {
            unhighlightButton.SetActive(false);
            
            if (debugMode)
                Debug.Log("Unhighlight button hidden");
        }
    }
    
    void ShowSecondButton()
    {
        if (secondButton != null)
        {
            secondButton.SetActive(true);
            
            if (debugMode)
                Debug.Log("Second button shown");
        }
    }
    
    void HideSecondButton()
    {
        if (secondButton != null)
        {
            secondButton.SetActive(false);
            
            if (debugMode)
                Debug.Log("Second button hidden");
        }
    }
    
    void ShowThirdButton()
    {
        if (thirdButton != null)
        {
            thirdButton.SetActive(true);
            
            if (debugMode)
                Debug.Log("Third button shown");
        }
    }
    
    void HideThirdButton()
    {
        if (thirdButton != null)
        {
            thirdButton.SetActive(false);
            
            if (debugMode)
                Debug.Log("Third button hidden");
        }
    }
    
    // Public method that will be called by the unhighlight button
    public void UnhighlightSelected()
    {
        // Check if highlighting is blocked - prevent unhighlighting
        if (IsHighlightingBlocked())
        {
            if (debugMode)
                Debug.Log("PropertyClickHandler: Cannot unhighlight - third button panel is visible");
            return;
        }
        
        if (debugMode)
            Debug.Log("Unhighlight button pressed");
            
        ClearSelection();
    }
    
    // Public method that will be called by the second button
    public void OnSecondButtonClicked()
    {
        if (debugMode)
            Debug.Log("Second button pressed");
            
        // Add your custom functionality here
    }
    
    // Public method that will be called by the third button
    public void OnThirdButtonClicked()
    {
        if (debugMode)
            Debug.Log("Third button pressed");
            
        // Add your custom functionality here
    }
    
    void OnDestroy()
    {
        // Clean up any remaining highlight materials
        ClearSelection();
        
        // Remove button listener if it exists
        if (unhighlightButton != null)
        {
            Component buttonInteractable = unhighlightButton.GetComponent("StatefulInteractable");
            if (buttonInteractable != null)
            {
                UnsubscribeFromButtonClickEvent(buttonInteractable);
            }
        }
        
        // Remove second button listener if it exists
        if (secondButton != null)
        {
            Component buttonInteractable = secondButton.GetComponent("StatefulInteractable");
            if (buttonInteractable != null)
            {
                UnsubscribeFromSecondButtonClickEvent(buttonInteractable);
            }
        }
        
        // Remove third button listener if it exists
        if (thirdButton != null)
        {
            Component buttonInteractable = thirdButton.GetComponent("StatefulInteractable");
            if (buttonInteractable != null)
            {
                UnsubscribeFromThirdButtonClickEvent(buttonInteractable);
            }
        }
        
        // Unsubscribe from events
        RevitData[] revitObjects = FindObjectsOfType<RevitData>();
        foreach (RevitData revitData in revitObjects)
        {
            Component interactable = revitData.GetComponent("StatefulInteractable");
            if (interactable != null)
            {
                UnsubscribeFromClickEvent(interactable);
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
                var eventValue = fieldInfo.GetValue(interactable) as UnityEvent;
                if (eventValue != null)
                {
                    eventValue.RemoveListener(UnhighlightSelected);
                    return;
                }
            }
            
            // Try as property
            PropertyInfo propertyInfo = interactableType.GetProperty(eventName);
            if (propertyInfo != null)
            {
                var eventValue = propertyInfo.GetValue(interactable) as UnityEvent;
                if (eventValue != null)
                {
                    eventValue.RemoveListener(UnhighlightSelected);
                    return;
                }
            }
        }
    }
    
    void UnsubscribeFromSecondButtonClickEvent(Component interactable)
    {
        Type interactableType = interactable.GetType();
        string[] possibleEventNames = { "OnClicked", "onClicked", "Clicked", "clicked" };
        
        foreach (string eventName in possibleEventNames)
        {
            // Try as field
            FieldInfo fieldInfo = interactableType.GetField(eventName);
            if (fieldInfo != null)
            {
                var eventValue = fieldInfo.GetValue(interactable) as UnityEvent;
                if (eventValue != null)
                {
                    eventValue.RemoveListener(OnSecondButtonClicked);
                    return;
                }
            }
            
            // Try as property
            PropertyInfo propertyInfo = interactableType.GetProperty(eventName);
            if (propertyInfo != null)
            {
                var eventValue = propertyInfo.GetValue(interactable) as UnityEvent;
                if (eventValue != null)
                {
                    eventValue.RemoveListener(OnSecondButtonClicked);
                    return;
                }
            }
        }
    }
    
    void UnsubscribeFromThirdButtonClickEvent(Component interactable)
    {
        Type interactableType = interactable.GetType();
        string[] possibleEventNames = { "OnClicked", "onClicked", "Clicked", "clicked" };
        
        foreach (string eventName in possibleEventNames)
        {
            // Try as field
            FieldInfo fieldInfo = interactableType.GetField(eventName);
            if (fieldInfo != null)
            {
                var eventValue = fieldInfo.GetValue(interactable) as UnityEvent;
                if (eventValue != null)
                {
                    eventValue.RemoveListener(OnThirdButtonClicked);
                    return;
                }
            }
            
            // Try as property
            PropertyInfo propertyInfo = interactableType.GetProperty(eventName);
            if (propertyInfo != null)
            {
                var eventValue = propertyInfo.GetValue(interactable) as UnityEvent;
                if (eventValue != null)
                {
                    eventValue.RemoveListener(OnThirdButtonClicked);
                    return;
                }
            }
        }
    }
    
    void UnsubscribeFromClickEvent(Component interactable)
    {
        Type interactableType = interactable.GetType();
        string[] possibleEventNames = { "OnClicked", "onClicked", "Clicked", "clicked" };
        
        foreach (string eventName in possibleEventNames)
        {
            // Try as field
            FieldInfo fieldInfo = interactableType.GetField(eventName);
            if (fieldInfo != null)
            {
                var eventValue = fieldInfo.GetValue(interactable) as UnityEvent;
                if (eventValue != null)
                {
                    eventValue.RemoveAllListeners();
                    return;
                }
            }
            
            // Try as property
            PropertyInfo propertyInfo = interactableType.GetProperty(eventName);
            if (propertyInfo != null)
            {
                var eventValue = propertyInfo.GetValue(interactable) as UnityEvent;
                if (eventValue != null)
                {
                    eventValue.RemoveAllListeners();
                    return;
                }
            }
        }
    }
    
    // Optional: Method to manually subscribe to an existing interactable at runtime
    public void SubscribeToInteractable(GameObject obj)
    {
        if (obj.GetComponent<RevitData>() == null) return;
        
        // Get existing StatefulInteractable component
        Component interactable = obj.GetComponent("StatefulInteractable");
        if (interactable != null)
        {
            TrySubscribeToClickEvent(interactable, obj);
        }
    }
    
    // Alternative: Manual method that can be called from Unity Events
    public void ManualObjectClick(GameObject clickedObject)
    {
        OnObjectClicked(clickedObject);
    }
}