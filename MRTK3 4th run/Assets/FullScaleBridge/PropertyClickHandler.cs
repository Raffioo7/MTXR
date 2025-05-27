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
    
    void Start()
    {
        // Hide panel initially
        if (propertyPanel != null)
            propertyPanel.SetActive(false);
            
        // Set up MRTK3 interactables on all objects with RevitData
        SetupInteractables();
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
    
    void Update()
    {
        // Press Escape to hide panel and clear selection
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ClearSelection();
        }
    }
    
    public void OnObjectClicked(GameObject clickedObject)
    {
        if (debugMode)
            Debug.Log($"OnObjectClicked called for: {clickedObject.name}");
        
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
    
    void OnDestroy()
    {
        // Clean up any remaining highlight materials
        ClearSelection();
        
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