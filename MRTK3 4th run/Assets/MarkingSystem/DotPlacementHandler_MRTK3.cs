using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Reflection;
using System;

public class DotPlacementHandler_MRTK3 : MonoBehaviour
{
    [Header("MRTK3 Button")]
    [Tooltip("The MRTK3 button that will toggle dot placement mode")]
    public GameObject dotPlacementButton;
    
    [Header("Dot Settings")]
    [Tooltip("Prefab for the dot marker")]
    public GameObject dotPrefab;
    
    [Tooltip("Default dot color")]
    public Color dotColor = Color.red;
    
    [Tooltip("Color for the first dot in each loop")]
    public Color firstDotColor = Color.green;
    
    [Tooltip("Default dot size")]
    public float dotSize = 0.02f;
    
    [Tooltip("Size multiplier for the first dot in each loop")]
    public float firstDotSizeMultiplier = 1.5f;
    
    [Tooltip("Offset from surface to prevent z-fighting")]
    public float surfaceOffset = 0.001f;
    
    [Tooltip("Distance threshold for detecting clicks on existing dots")]
    public float dotClickThreshold = 0.05f;
    
    [Header("Label Settings")]
    [Tooltip("Vertical spacing of number label from dot")]
    public float labelSpacing = 0.04f;
    
    [Tooltip("Label font size")]
    public float labelFontSize = 1f;
    
    [Tooltip("Label color")]
    public Color labelColor = Color.white;
    
    [Header("Dot Management")]
    [Tooltip("Parent object for all placed dots")]
    public Transform dotsParent;
    
    [Header("Debug")]
    public bool debugMode = true;
    
    // Private fields
    private bool isPlacementModeActive = false;
    private PropertyClickHandler_MRTK3 propertyHandler;
    private List<List<GameObject>> dotLoops = new List<List<GameObject>>(); // Changed to support multiple loops
    private List<GameObject> currentLoop = new List<GameObject>(); // Current loop being drawn
    private GameObject currentHighlightedObject;
    
    // Store previous values for change detection
    private Color previousDotColor;
    private Color previousFirstDotColor;
    private float previousDotSize;
    private float previousFirstDotSizeMultiplier;
    private float previousLabelSpacing;
    private float previousLabelFontSize;
    private Color previousLabelColor;
    
    // Events for loop management
    public System.Action<int> OnLoopClosed;
    public System.Action<int> OnNewLoopStarted;
    
    void Start()
    {
        // Find the PropertyClickHandler_MRTK3 script
        propertyHandler = FindObjectOfType<PropertyClickHandler_MRTK3>();
        
        if (propertyHandler == null)
        {
            Debug.LogWarning("DotPlacementHandler: Could not find PropertyClickHandler_MRTK3 script!");
        }
        
        // Create dot prefab if not assigned
        if (dotPrefab == null)
        {
            CreateDefaultDotPrefab();
        }
        
        // Create dots parent if not assigned
        if (dotsParent == null)
        {
            GameObject dotsParentObj = new GameObject("Placed Dots");
            dotsParent = dotsParentObj.transform;
        }
        
        // Initialize previous values
        previousDotColor = dotColor;
        previousFirstDotColor = firstDotColor;
        previousDotSize = dotSize;
        previousFirstDotSizeMultiplier = firstDotSizeMultiplier;
        previousLabelSpacing = labelSpacing;
        previousLabelFontSize = labelFontSize;
        previousLabelColor = labelColor;
        
        // Hook into the button's functionality
        SetupDotPlacementButton();
    }
    
    void CreateDefaultDotPrefab()
    {
        // Create a simple sphere as the default dot
        dotPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dotPrefab.name = "DotPrefab";
        
        // Remove collider to prevent interference
        Destroy(dotPrefab.GetComponent<Collider>());
        
        // Set default size
        dotPrefab.transform.localScale = Vector3.one * dotSize;
        
        // Set default material and color
        Renderer renderer = dotPrefab.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            renderer.material.color = dotColor;
        }
        
        // Deactivate the prefab
        dotPrefab.SetActive(false);
    }
    
    void SetupDotPlacementButton()
    {
        if (dotPlacementButton == null)
        {
            Debug.LogWarning("DotPlacementHandler: No dot placement button assigned!");
            return;
        }
        
        // Get the MRTK3 StatefulInteractable component
        Component buttonInteractable = dotPlacementButton.GetComponent("StatefulInteractable");
        if (buttonInteractable != null)
        {
            bool subscribed = TrySubscribeToButtonClick(buttonInteractable);
            
            if (debugMode)
            {
                if (subscribed)
                    Debug.Log("DotPlacementHandler: Successfully set up dot placement button");
                else
                    Debug.LogWarning("DotPlacementHandler: Failed to set up dot placement button");
            }
        }
        else if (debugMode)
        {
            Debug.LogWarning("DotPlacementHandler: No StatefulInteractable found on dot placement button");
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
                    eventValue.AddListener(ToggleDotPlacementMode);
                    return true;
                }
            }
            
            PropertyInfo propertyInfo = interactableType.GetProperty(eventName);
            if (propertyInfo != null)
            {
                var eventValue = propertyInfo.GetValue(interactable) as UnityEngine.Events.UnityEvent;
                if (eventValue != null)
                {
                    eventValue.AddListener(ToggleDotPlacementMode);
                    return true;
                }
            }
        }
        
        return false;
    }
    
    public void ToggleDotPlacementMode()
    {
        isPlacementModeActive = !isPlacementModeActive;
        
        if (debugMode)
            Debug.Log($"DotPlacementHandler: Dot placement mode is now {(isPlacementModeActive ? "ACTIVE" : "INACTIVE")}");
        
        // Update button visual feedback if needed
        UpdateButtonVisualFeedback();
    }
    
    void UpdateButtonVisualFeedback()
    {
        // Optional: Change button appearance to indicate active mode
        if (dotPlacementButton != null)
        {
            // You can add visual feedback here, like changing button color
            // For example, if using MRTK3 button visuals
            Renderer buttonRenderer = dotPlacementButton.GetComponentInChildren<Renderer>();
            if (buttonRenderer != null)
            {
                // Store original color if not stored
                if (!buttonRenderer.material.HasProperty("_OriginalColor"))
                {
                    buttonRenderer.material.SetColor("_OriginalColor", buttonRenderer.material.color);
                }
                
                // Change color based on mode
                if (isPlacementModeActive)
                {
                    buttonRenderer.material.color = Color.green; // Active mode color
                }
                else
                {
                    buttonRenderer.material.color = buttonRenderer.material.GetColor("_OriginalColor");
                }
            }
        }
    }
    
    void Update()
    {
        // Check for runtime parameter changes
        CheckForParameterChanges();
        
        // Get the currently highlighted object from PropertyClickHandler
        currentHighlightedObject = GetCurrentlyHighlightedObject();
        
        // Only process clicks if in placement mode and an object is highlighted
        if (isPlacementModeActive && currentHighlightedObject != null)
        {
            // Handle mouse/touch input
            if (Input.GetMouseButtonDown(0))
            {
                HandleDotPlacement();
            }
        }
        
        // Optional: Exit placement mode with Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPlacementModeActive)
            {
                isPlacementModeActive = false;
                UpdateButtonVisualFeedback();
                if (debugMode)
                    Debug.Log("DotPlacementHandler: Exited dot placement mode");
            }
        }
    }
    
    void CheckForParameterChanges()
    {
        bool needsUpdate = false;
        
        // Check if any parameters changed
        if (dotColor != previousDotColor)
        {
            previousDotColor = dotColor;
            needsUpdate = true;
        }
        
        if (firstDotColor != previousFirstDotColor)
        {
            previousFirstDotColor = firstDotColor;
            needsUpdate = true;
        }
        
        if (Mathf.Abs(dotSize - previousDotSize) > 0.0001f)
        {
            previousDotSize = dotSize;
            needsUpdate = true;
        }
        
        if (Mathf.Abs(firstDotSizeMultiplier - previousFirstDotSizeMultiplier) > 0.0001f)
        {
            previousFirstDotSizeMultiplier = firstDotSizeMultiplier;
            needsUpdate = true;
        }
        
        if (Mathf.Abs(labelSpacing - previousLabelSpacing) > 0.0001f)
        {
            previousLabelSpacing = labelSpacing;
            needsUpdate = true;
        }
        
        if (Mathf.Abs(labelFontSize - previousLabelFontSize) > 0.0001f)
        {
            previousLabelFontSize = labelFontSize;
            needsUpdate = true;
        }
        
        if (labelColor != previousLabelColor)
        {
            previousLabelColor = labelColor;
            needsUpdate = true;
        }
        
        // Update all existing dots if any parameter changed
        if (needsUpdate)
        {
            UpdateAllDots();
        }
    }
    
    void UpdateAllDots()
    {
        for (int loopIndex = 0; loopIndex < dotLoops.Count; loopIndex++)
        {
            var loop = dotLoops[loopIndex];
            for (int dotIndex = 0; dotIndex < loop.Count; dotIndex++)
            {
                GameObject dot = loop[dotIndex];
                if (dot != null)
                {
                    UpdateDotAppearance(dot, dotIndex == 0);
                }
            }
        }
        
        // Update current loop
        for (int i = 0; i < currentLoop.Count; i++)
        {
            GameObject dot = currentLoop[i];
            if (dot != null)
            {
                UpdateDotAppearance(dot, i == 0);
            }
        }
        
        // Update the prefab if it exists
        if (dotPrefab != null && dotPrefab.name == "DotPrefab")
        {
            dotPrefab.transform.localScale = Vector3.one * dotSize;
            Renderer prefabRenderer = dotPrefab.GetComponent<Renderer>();
            if (prefabRenderer != null && prefabRenderer.material != null)
            {
                prefabRenderer.material.color = dotColor;
            }
        }
    }
    
    void UpdateDotAppearance(GameObject dot, bool isFirstDot)
    {
        // Update dot size
        float size = isFirstDot ? dotSize * firstDotSizeMultiplier : dotSize;
        dot.transform.localScale = Vector3.one * size;
        
        // Update dot color
        Renderer renderer = dot.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
        {
            renderer.material.color = isFirstDot ? firstDotColor : dotColor;
        }
        
        // Update label
        TextMeshPro label = dot.GetComponentInChildren<TextMeshPro>();
        if (label != null)
        {
            label.transform.localPosition = Vector3.up * labelSpacing;
            label.fontSize = labelFontSize;
            label.color = labelColor;
        }
    }
    
    GameObject GetCurrentlyHighlightedObject()
    {
        // Use reflection to access the private currentlySelected field
        if (propertyHandler != null)
        {
            Type handlerType = propertyHandler.GetType();
            FieldInfo fieldInfo = handlerType.GetField("currentlySelected", BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (fieldInfo != null)
            {
                return fieldInfo.GetValue(propertyHandler) as GameObject;
            }
        }
        
        return null;
    }
    
    void HandleDotPlacement()
    {
        // Cast a ray from the camera through the mouse position
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        // Check if we clicked on an existing dot first
        GameObject clickedDot = GetClickedDot(ray);
        if (clickedDot != null)
        {
            HandleDotClick(clickedDot);
            return;
        }
        
        // Only raycast against the currently highlighted object for new dot placement
        if (Physics.Raycast(ray, out hit))
        {
            // Check if we hit the currently highlighted object
            if (hit.collider.gameObject == currentHighlightedObject)
            {
                PlaceDotAtPosition(hit.point, hit.normal);
            }
        }
    }
    
    GameObject GetClickedDot(Ray ray)
    {
        // Check current loop first
        foreach (GameObject dot in currentLoop)
        {
            if (dot != null && IsRayNearDot(ray, dot.transform.position))
            {
                return dot;
            }
        }
        
        // Check completed loops
        foreach (var loop in dotLoops)
        {
            foreach (GameObject dot in loop)
            {
                if (dot != null && IsRayNearDot(ray, dot.transform.position))
                {
                    return dot;
                }
            }
        }
        
        return null;
    }
    
    bool IsRayNearDot(Ray ray, Vector3 dotPosition)
    {
        Vector3 closestPoint = ray.origin + Vector3.Project(dotPosition - ray.origin, ray.direction);
        float distance = Vector3.Distance(closestPoint, dotPosition);
        return distance <= dotClickThreshold;
    }
    
    void HandleDotClick(GameObject clickedDot)
    {
        // Check if it's the first dot of the current loop (to close the loop)
        if (currentLoop.Count > 2 && clickedDot == currentLoop[0])
        {
            CloseCurrentLoop();
        }
        else if (debugMode)
        {
            Debug.Log($"DotPlacementHandler: Clicked on dot {clickedDot.name}");
        }
    }
    
    void CloseCurrentLoop()
    {
        if (currentLoop.Count < 3)
        {
            if (debugMode)
                Debug.LogWarning("DotPlacementHandler: Need at least 3 dots to close a loop");
            return;
        }
        
        // Add current loop to completed loops
        dotLoops.Add(new List<GameObject>(currentLoop));
        int loopIndex = dotLoops.Count - 1;
        
        // Rename dots to include loop information
        for (int i = 0; i < currentLoop.Count; i++)
        {
            currentLoop[i].name = $"Loop{loopIndex + 1}_Dot{i + 1}";
        }
        
        if (debugMode)
            Debug.Log($"DotPlacementHandler: Closed loop {loopIndex + 1} with {currentLoop.Count} dots");
        
        // Trigger event
        OnLoopClosed?.Invoke(loopIndex);
        
        // Start a new loop
        StartNewLoop();
    }
    
    void StartNewLoop()
    {
        currentLoop.Clear();
        int newLoopIndex = dotLoops.Count;
        
        if (debugMode)
            Debug.Log($"DotPlacementHandler: Started new loop {newLoopIndex + 1}");
        
        // Trigger event
        OnNewLoopStarted?.Invoke(newLoopIndex);
    }
    
    void PlaceDotAtPosition(Vector3 position, Vector3 normal)
    {
        // Create a new dot instance
        GameObject newDot = Instantiate(dotPrefab, position + normal * surfaceOffset, Quaternion.identity, dotsParent);
        newDot.SetActive(true);
        
        bool isFirstDot = currentLoop.Count == 0;
        int dotNumber = currentLoop.Count + 1;
        
        newDot.name = $"TempLoop_Dot{dotNumber}";
        
        // Optional: Make the dot face along the surface normal
        newDot.transform.up = normal;
        
        // Add to current loop
        currentLoop.Add(newDot);
        
        // Update appearance based on whether it's the first dot
        UpdateDotAppearance(newDot, isFirstDot);
        
        // Add a label showing dot number
        AddDotLabel(newDot, dotNumber);
        
        if (debugMode)
            Debug.Log($"DotPlacementHandler: Placed dot #{dotNumber} at {position} on {currentHighlightedObject.name}");
    }
    
    void AddDotLabel(GameObject dot, int dotNumber)
    {
        // Create a text label for the dot
        GameObject labelObj = new GameObject($"Label_{dotNumber}");
        labelObj.transform.SetParent(dot.transform);
        labelObj.transform.localPosition = Vector3.up * labelSpacing;
        
        // Add TextMeshPro component
        TextMeshPro tmpText = labelObj.AddComponent<TextMeshPro>();
        tmpText.text = dotNumber.ToString();
        tmpText.fontSize = labelFontSize;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = labelColor;
        
        // Make the label always face the camera
        labelObj.AddComponent<FaceCamera>();
    }
    
    // Public methods for managing dots
    public void ClearAllDots()
    {
        // Clear completed loops
        foreach (var loop in dotLoops)
        {
            foreach (GameObject dot in loop)
            {
                if (dot != null)
                    Destroy(dot);
            }
        }
        dotLoops.Clear();
        
        // Clear current loop
        foreach (GameObject dot in currentLoop)
        {
            if (dot != null)
                Destroy(dot);
        }
        currentLoop.Clear();
        
        if (debugMode)
            Debug.Log("DotPlacementHandler: Cleared all dots and loops");
    }
    
    public void RemoveLastDot()
    {
        if (currentLoop.Count > 0)
        {
            GameObject lastDot = currentLoop[currentLoop.Count - 1];
            currentLoop.RemoveAt(currentLoop.Count - 1);
            
            if (lastDot != null)
                Destroy(lastDot);
            
            if (debugMode)
                Debug.Log("DotPlacementHandler: Removed last dot from current loop");
        }
        else if (dotLoops.Count > 0)
        {
            // If no dots in current loop, remove the last completed loop
            var lastLoop = dotLoops[dotLoops.Count - 1];
            foreach (GameObject dot in lastLoop)
            {
                if (dot != null)
                    Destroy(dot);
            }
            dotLoops.RemoveAt(dotLoops.Count - 1);
            
            if (debugMode)
                Debug.Log("DotPlacementHandler: Removed last completed loop");
        }
    }
    
    public List<Vector3> GetAllDotPositions()
    {
        List<Vector3> positions = new List<Vector3>();
        
        // Add positions from completed loops
        foreach (var loop in dotLoops)
        {
            foreach (GameObject dot in loop)
            {
                if (dot != null)
                    positions.Add(dot.transform.position);
            }
        }
        
        // Add positions from current loop
        foreach (GameObject dot in currentLoop)
        {
            if (dot != null)
                positions.Add(dot.transform.position);
        }
        
        return positions;
    }
    
    public List<List<Vector3>> GetAllLoopPositions()
    {
        List<List<Vector3>> allLoops = new List<List<Vector3>>();
        
        // Add completed loops
        foreach (var loop in dotLoops)
        {
            List<Vector3> loopPositions = new List<Vector3>();
            foreach (GameObject dot in loop)
            {
                if (dot != null)
                    loopPositions.Add(dot.transform.position);
            }
            if (loopPositions.Count > 0)
                allLoops.Add(loopPositions);
        }
        
        // Add current loop if it has dots
        if (currentLoop.Count > 0)
        {
            List<Vector3> currentLoopPositions = new List<Vector3>();
            foreach (GameObject dot in currentLoop)
            {
                if (dot != null)
                    currentLoopPositions.Add(dot.transform.position);
            }
            allLoops.Add(currentLoopPositions);
        }
        
        return allLoops;
    }
    
    public int GetCompletedLoopCount()
    {
        return dotLoops.Count;
    }
    
    public int GetCurrentLoopDotCount()
    {
        return currentLoop.Count;
    }
    
    void OnValidate()
    {
        // Ensure positive values
        dotSize = Mathf.Max(0.001f, dotSize);
        firstDotSizeMultiplier = Mathf.Max(0.1f, firstDotSizeMultiplier);
        surfaceOffset = Mathf.Max(0.0001f, surfaceOffset);
        labelSpacing = Mathf.Max(0.001f, labelSpacing);
        labelFontSize = Mathf.Max(0.1f, labelFontSize);
        dotClickThreshold = Mathf.Max(0.001f, dotClickThreshold);
    }
    
    void OnDestroy()
    {
        // Clean up
        ClearAllDots();
        
        if (dotPrefab != null && dotPrefab.name == "DotPrefab")
            Destroy(dotPrefab);
        
        // Remove button listener if it exists
        if (dotPlacementButton != null)
        {
            Component buttonInteractable = dotPlacementButton.GetComponent("StatefulInteractable");
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
                    eventValue.RemoveListener(ToggleDotPlacementMode);
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
                    eventValue.RemoveListener(ToggleDotPlacementMode);
                    return;
                }
            }
        }
    }
}

// Simple component to make labels face the camera
public class FaceCamera : MonoBehaviour
{
    void LateUpdate()
    {
        if (Camera.main != null)
        {
            transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
                            Camera.main.transform.rotation * Vector3.up);
        }
    }
}