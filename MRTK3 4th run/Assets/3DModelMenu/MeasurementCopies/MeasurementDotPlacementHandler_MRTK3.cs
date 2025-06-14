using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Reflection;
using System;
using UnityEngine.Events;
using System.Linq;

public class MeasurementDotPlacementHandler_MRTK3 : MonoBehaviour
{
    [Header("MRTK3 Buttons")]
    [Tooltip("The first MRTK3 button that will toggle dot placement mode and panel")]
    public GameObject dotPlacementButton;
    
    [Tooltip("The second MRTK3 button that will toggle dot placement mode and panel")]
    public GameObject dotPlacementButton2;
    
    [Header("Panel")]
    [Tooltip("Panel to toggle when buttons are clicked")]
    public GameObject panel;
    
    [Header("Dot Settings")]
    [Tooltip("Prefab for the dot marker")]
    public GameObject dotPrefab;
    
    [Tooltip("Default dot color")]
    public Color dotColor = Color.green;
    
    [Tooltip("Color for the first dot in each loop")]
    public Color firstDotColor = Color.cyan;
    
    [Tooltip("Default dot size")]
    public float dotSize = 0.02f;
    
    [Tooltip("Size multiplier for the first dot in each loop")]
    public float firstDotSizeMultiplier = 1.5f;
    
    [Tooltip("Offset from surface to prevent z-fighting")]
    public float surfaceOffset = 0.001f;
    
    [Tooltip("Distance threshold for detecting clicks on existing dots")]
    public float dotClickThreshold = 0.05f;
    
    [Header("Dot Management")]
    [Tooltip("Parent object for all placed dots")]
    public Transform dotsParent;
    
    [Header("Debug")]
    public bool debugMode = true;
    
    // Private fields
    private bool isPlacementModeActive = false;
    private PropertyClickHandler_MRTK3 propertyHandler;
    private List<List<GameObject>> dotLoops = new List<List<GameObject>>();
    private List<GameObject> currentLoop = new List<GameObject>();
    private GameObject currentHighlightedObject;
    
    // Store previous values for change detection
    private Color previousDotColor;
    private Color previousFirstDotColor;
    private float previousDotSize;
    private float previousFirstDotSizeMultiplier; 
    
    // Events for loop management
    public System.Action<int> OnMeasurementLoopClosed;
    public System.Action<int> OnMeasurementNewLoopStarted;
    
    // Hand interaction tracking
    private HashSet<GameObject> subscribedObjects = new HashSet<GameObject>();
    
    void Start()
    {
        // Find the PropertyClickHandler_MRTK3 script
        propertyHandler = FindObjectOfType<PropertyClickHandler_MRTK3>();
        
        if (propertyHandler == null)
        {
            Debug.LogWarning("MeasurementDotPlacementHandler: Could not find PropertyClickHandler_MRTK3 script!");
        }
        
        // Create dot prefab if not assigned
        if (dotPrefab == null)
        {
            CreateDefaultDotPrefab();
        }
        
        // Create dots parent if not assigned
        if (dotsParent == null)
        {
            GameObject dotsParentObj = new GameObject("Measurement Placed Dots");
            dotsParent = dotsParentObj.transform;
        }
        
        // Initialize previous values
        previousDotColor = dotColor;
        previousFirstDotColor = firstDotColor;
        previousDotSize = dotSize;
        previousFirstDotSizeMultiplier = firstDotSizeMultiplier;
        
        // Hook into the buttons' functionality
        SetupDotPlacementButtons();
        
        // Setup hand interactions for all RevitData objects
        SetupHandInteractions();
        
        // Initialize panel state (start hidden)
        if (panel != null)
        {
            panel.SetActive(false);
        }
        
        if (debugMode)
            Debug.Log("MeasurementDotPlacementHandler: Initialized successfully");
    }
    
    void SetupHandInteractions()
    {
        // Find all objects with RevitData component
        RevitData[] revitObjects = FindObjectsOfType<RevitData>();
        
        if (debugMode)
            Debug.Log($"MeasurementDotPlacementHandler: Found {revitObjects.Length} RevitData objects for hand interaction");
        
        foreach (RevitData revitData in revitObjects)
        {
            GameObject obj = revitData.gameObject;
            
            // Get existing StatefulInteractable component
            Component interactable = obj.GetComponent("StatefulInteractable");
            if (interactable != null)
            {
                bool subscribed = TrySubscribeToDotPlacementEvent(interactable, obj);
                
                if (subscribed)
                {
                    subscribedObjects.Add(obj);
                    if (debugMode)
                        Debug.Log($"MeasurementDotPlacementHandler: Successfully subscribed to {obj.name}");
                }
            }
        }
    }
    
    bool TrySubscribeToDotPlacementEvent(Component interactable, GameObject targetObj)
    {
        Type interactableType = interactable.GetType();
        string[] possibleEventNames = { "OnClicked", "onClicked", "Clicked", "clicked" };
        
        foreach (string eventName in possibleEventNames)
        {
            FieldInfo fieldInfo = interactableType.GetField(eventName);
            if (fieldInfo != null)
            {
                var eventValue = fieldInfo.GetValue(interactable) as UnityEvent;
                if (eventValue != null)
                {
                    eventValue.AddListener(() => OnObjectClickedForDotPlacement(targetObj));
                    return true;
                }
            }
            
            PropertyInfo propertyInfo = interactableType.GetProperty(eventName);
            if (propertyInfo != null)
            {
                var eventValue = propertyInfo.GetValue(interactable) as UnityEvent;
                if (eventValue != null)
                {
                    eventValue.AddListener(() => OnObjectClickedForDotPlacement(targetObj));
                    return true;
                }
            }
        }
        
        return false;
    }
    
    public void OnObjectClickedForDotPlacement(GameObject clickedObject)
    {
        // Only process if dot placement mode is active
        if (!isPlacementModeActive)
        {
            if (debugMode)
                Debug.Log($"MeasurementDotPlacementHandler: Ignoring click on {clickedObject.name} - placement mode inactive");
            return;
        }
        
        if (debugMode)
            Debug.Log($"MeasurementDotPlacementHandler: Hand clicked on {clickedObject.name} for dot placement");
        
        // Update the currently highlighted object
        currentHighlightedObject = clickedObject;
        
        // For simplicity, place dot at object center with upward normal
        PlaceDotOnObjectSmart(clickedObject);
    }
    
    void PlaceDotOnObjectSmart(GameObject targetObject)
    {
        Vector3 surfacePosition;
        Vector3 surfaceNormal;
        
        if (GetSmartSurfacePointOnObject(targetObject, out surfacePosition, out surfaceNormal))
        {
            // Check if we clicked on an existing dot first
            GameObject clickedDot = GetClickedDotNearPosition(surfacePosition);
            if (clickedDot != null)
            {
                HandleDotClick(clickedDot);
                return;
            }
            
            PlaceDotAtPosition(surfacePosition, surfaceNormal);
        }
        else if (debugMode)
        {
            Debug.LogWarning($"MeasurementDotPlacementHandler: Could not find surface point on {targetObject.name}");
        }
    }
    
    bool GetSmartSurfacePointOnObject(GameObject obj, out Vector3 position, out Vector3 normal)
    {
        position = Vector3.zero;
        normal = Vector3.up;
        
        Collider objCollider = obj.GetComponent<Collider>();
        if (objCollider == null)
        {
            // Use renderer bounds as fallback
            Renderer objRenderer = obj.GetComponent<Renderer>();
            if (objRenderer != null)
            {
                position = objRenderer.bounds.center;
                normal = Vector3.up;
                return true;
            }
            
            // Last resort: use transform position
            position = obj.transform.position;
            normal = Vector3.up;
            return true;
        }
        
        // Use camera position as reference
        if (Camera.main != null)
        {
            Vector3 cameraPosition = Camera.main.transform.position;
            Vector3 closestPoint = objCollider.ClosestPoint(cameraPosition);
            
            // Raycast from camera to the surface
            Vector3 direction = (closestPoint - cameraPosition).normalized;
            RaycastHit hit;
            if (Physics.Raycast(cameraPosition, direction, out hit, Mathf.Infinity))
            {
                if (hit.collider == objCollider)
                {
                    position = hit.point;
                    normal = hit.normal;
                    return true;
                }
            }
            
            // Fallback: use closest point
            position = closestPoint;
            normal = (closestPoint - obj.transform.position).normalized;
            if (normal.magnitude < 0.001f) normal = Vector3.up;
            return true;
        }
        
        // Method 3: Use object bounds center
        Renderer objRenderer2 = obj.GetComponent<Renderer>();
        if (objRenderer2 != null)
        {
            position = objRenderer2.bounds.center;
            normal = Vector3.up;
            return true;
        }
        
        // Last resort: use transform position
        position = obj.transform.position;
        normal = Vector3.up;
        return true;
    }
    
    GameObject GetClickedDotNearPosition(Vector3 position)
    {
        // Check current loop first
        foreach (GameObject dot in currentLoop)
        {
            if (dot != null && Vector3.Distance(dot.transform.position, position) <= dotClickThreshold)
            {
                return dot;
            }
        }
        
        // Check completed loops
        foreach (var loop in dotLoops)
        {
            foreach (GameObject dot in loop)
            {
                if (dot != null && Vector3.Distance(dot.transform.position, position) <= dotClickThreshold)
                {
                    return dot;
                }
            }
        }
        
        return null;
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
            Debug.Log($"MeasurementDotPlacementHandler: Clicked on dot {clickedDot.name}");
        }
    }
    
    void CloseCurrentLoop()
    {
        if (currentLoop.Count < 3)
        {
            if (debugMode)
                Debug.LogWarning("MeasurementDotPlacementHandler: Need at least 3 dots to close a loop");
            return;
        }
        
        // Add current loop to completed loops
        dotLoops.Add(new List<GameObject>(currentLoop));
        int loopIndex = dotLoops.Count - 1;
        
        // Rename dots to include loop information
        for (int i = 0; i < currentLoop.Count; i++)
        {
            currentLoop[i].name = $"MeasurementLoop{loopIndex + 1}_Dot{i + 1}";
        }
        
        if (debugMode)
            Debug.Log($"MeasurementDotPlacementHandler: Closed loop {loopIndex + 1} with {currentLoop.Count} dots");
        
        // Trigger event
        OnMeasurementLoopClosed?.Invoke(loopIndex);
        
        // Start a new loop
        StartNewLoop();
    }
    
    void StartNewLoop()
    {
        currentLoop.Clear();
        int newLoopIndex = dotLoops.Count;
        
        if (debugMode)
            Debug.Log($"MeasurementDotPlacementHandler: Started new loop {newLoopIndex + 1}");
        
        // Trigger event
        OnMeasurementNewLoopStarted?.Invoke(newLoopIndex);
    }
    
    void PlaceDotAtPosition(Vector3 position, Vector3 normal)
    {
        // Create a new dot instance
        GameObject newDot = Instantiate(dotPrefab, position + normal * surfaceOffset, Quaternion.identity, dotsParent);
        newDot.SetActive(true);
        
        bool isFirstDot = currentLoop.Count == 0;
        int dotNumber = currentLoop.Count + 1;
        
        newDot.name = $"MeasurementTempLoop_Dot{dotNumber}";
        
        // Optional: Make the dot face along the surface normal
        newDot.transform.up = normal;
        
        // Add to current loop
        currentLoop.Add(newDot);
        
        // Update appearance based on whether it's the first dot
        UpdateDotAppearance(newDot, isFirstDot);
        
        if (debugMode)
            Debug.Log($"MeasurementDotPlacementHandler: Placed dot #{dotNumber} at {position} on {currentHighlightedObject?.name ?? "unknown object"}");
    }
    
    void CreateDefaultDotPrefab()
    {
        // Create a simple sphere as the default dot
        dotPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dotPrefab.name = "MeasurementDotPrefab";
        
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
    
    void SetupDotPlacementButtons()
    {
        // Setup first button
        if (dotPlacementButton != null)
        {
            SetupSingleButton(dotPlacementButton, "first");
        }
        else
        {
            Debug.LogWarning("MeasurementDotPlacementHandler: No first dot placement button assigned!");
        }
        
        // Setup second button
        if (dotPlacementButton2 != null)
        {
            SetupSingleButton(dotPlacementButton2, "second");
        }
    }
    
    void SetupSingleButton(GameObject button, string buttonName)
    {
        // Get the MRTK3 StatefulInteractable component
        Component buttonInteractable = button.GetComponent("StatefulInteractable");
        if (buttonInteractable != null)
        {
            bool subscribed = TrySubscribeToButtonClick(buttonInteractable);
            
            if (debugMode)
            {
                if (subscribed)
                    Debug.Log($"MeasurementDotPlacementHandler: Successfully set up {buttonName} dot placement button");
                else
                    Debug.LogWarning($"MeasurementDotPlacementHandler: Failed to set up {buttonName} dot placement button");
            }
        }
        else if (debugMode)
        {
            Debug.LogWarning($"MeasurementDotPlacementHandler: No StatefulInteractable found on {buttonName} dot placement button");
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
                    eventValue.AddListener(ToggleDotPlacementModeAndPanel);
                    return true;
                }
            }
            
            PropertyInfo propertyInfo = interactableType.GetProperty(eventName);
            if (propertyInfo != null)
            {
                var eventValue = propertyInfo.GetValue(interactable) as UnityEngine.Events.UnityEvent;
                if (eventValue != null)
                {
                    eventValue.AddListener(ToggleDotPlacementModeAndPanel);
                    return true;
                }
            }
        }
        
        return false;
    }
    
    public void ToggleDotPlacementModeAndPanel()
    {
        // Toggle dot placement mode
        isPlacementModeActive = !isPlacementModeActive;
        
        // Toggle panel
        if (panel != null)
        {
            panel.SetActive(!panel.activeSelf);
        }
        
        if (debugMode)
            Debug.Log($"MeasurementDotPlacementHandler: Dot placement mode is now {(isPlacementModeActive ? "ACTIVE" : "INACTIVE")}, Panel is now {(panel != null && panel.activeSelf ? "VISIBLE" : "HIDDEN")}");
        
        // Update button visual feedback if needed
        UpdateButtonVisualFeedback();
    }
    
    public void ToggleDotPlacementMode()
    {
        isPlacementModeActive = !isPlacementModeActive;
        
        if (debugMode)
            Debug.Log($"MeasurementDotPlacementHandler: Dot placement mode is now {(isPlacementModeActive ? "ACTIVE" : "INACTIVE")}");
        
        UpdateButtonVisualFeedback();
    }
    
    void UpdateButtonVisualFeedback()
    {
        UpdateSingleButtonVisualFeedback(dotPlacementButton);
        UpdateSingleButtonVisualFeedback(dotPlacementButton2);
    }
    
    void UpdateSingleButtonVisualFeedback(GameObject button)
    {
        if (button != null)
        {
            Renderer buttonRenderer = button.GetComponentInChildren<Renderer>();
            if (buttonRenderer != null && buttonRenderer.material != null)
            {
                if (buttonRenderer.material.HasProperty("_Color"))
                {
                    buttonRenderer.material.color = isPlacementModeActive ? Color.green : Color.white;
                }
                else if (buttonRenderer.material.HasProperty("_BaseColor"))
                {
                    buttonRenderer.material.SetColor("_BaseColor", isPlacementModeActive ? Color.green : Color.white);
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
        
        // Optional: Exit placement mode with Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPlacementModeActive)
            {
                isPlacementModeActive = false;
                
                if (panel != null)
                {
                    panel.SetActive(false);
                }
                
                UpdateButtonVisualFeedback();
                if (debugMode)
                    Debug.Log("MeasurementDotPlacementHandler: Exited dot placement mode and hid panel");
            }
        }
    }
    
    void CheckForParameterChanges()
    {
        bool needsUpdate = false;
        
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
        if (dotPrefab != null && dotPrefab.name == "MeasurementDotPrefab")
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
    }
    
    GameObject GetCurrentlyHighlightedObject()
    {
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
            Debug.Log("MeasurementDotPlacementHandler: Cleared all dots and loops");
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
                Debug.Log("MeasurementDotPlacementHandler: Removed last dot from current loop");
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
                Debug.Log("MeasurementDotPlacementHandler: Removed last completed loop");
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
        dotClickThreshold = Mathf.Max(0.001f, dotClickThreshold);
    }
    
    void OnDestroy()
    {
        // Clean up
        ClearAllDots();
        
        if (dotPrefab != null && dotPrefab.name == "MeasurementDotPrefab")
            Destroy(dotPrefab);
        
        // Unsubscribe from all object events
        UnsubscribeFromAllObjects();
    }
    
    void UnsubscribeFromAllObjects()
    {
        foreach (GameObject obj in subscribedObjects)
        {
            if (obj != null)
            {
                Component interactable = obj.GetComponent("StatefulInteractable");
                if (interactable != null)
                {
                    // Note: We can't easily remove specific listeners, so we'll leave them
                    // The listeners will naturally be cleaned up when the object is destroyed
                }
            }
        }
        subscribedObjects.Clear();
    }
    
    // Public method to manually subscribe to a new object if needed
    public void SubscribeToNewObject(GameObject obj)
    {
        if (obj.GetComponent<RevitData>() == null) return;
        if (subscribedObjects.Contains(obj)) return;
        
        Component interactable = obj.GetComponent("StatefulInteractable");
        if (interactable != null)
        {
            bool subscribed = TrySubscribeToDotPlacementEvent(interactable, obj);
            if (subscribed)
            {
                subscribedObjects.Add(obj);
                if (debugMode)
                    Debug.Log($"MeasurementDotPlacementHandler: Manually subscribed to {obj.name}");
            }
        }
    }
}

// Simple component to make labels face the camera
public class MeasurementFaceCamera : MonoBehaviour
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