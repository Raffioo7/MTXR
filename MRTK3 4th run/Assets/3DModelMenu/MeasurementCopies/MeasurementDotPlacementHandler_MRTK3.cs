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
    
    // Cache for hand ray components
    private Component[] handRayComponents;
    private float lastHandRayUpdateTime = 0f;
    private float handRayUpdateInterval = 0.1f;
    
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
        
        // Initialize hand ray components cache
        UpdateHandRayComponentsCache();
        
        // Initialize panel state (start hidden)
        if (panel != null)
        {
            panel.SetActive(false);
        }
        
        if (debugMode)
            Debug.Log("MeasurementDotPlacementHandler: Initialized successfully");
    }
    
    void UpdateHandRayComponentsCache()
    {
        var allComponents = FindObjectsOfType<MonoBehaviour>();
        handRayComponents = allComponents.Where(mb => 
            mb.GetType().Name.Contains("HandRay") ||
            mb.GetType().Name.Contains("PokeRay") ||
            mb.GetType().Name.Contains("HandInteractor") ||
            mb.GetType().Name.Contains("RayInteractor") ||
            mb.GetType().Name.Contains("FarRay") ||
            mb.GetType().Name.Contains("NearRay") ||
            (mb.gameObject.name.ToLower().Contains("hand") && 
             (mb.GetType().Name.Contains("Ray") || mb.GetType().Name.Contains("Interactor")))
        ).ToArray();
        
        if (debugMode)
            Debug.Log($"MeasurementDotPlacementHandler: Found {handRayComponents.Length} potential hand ray components");
    }
    
    void SetupHandInteractions()
    {
        RevitData[] revitObjects = FindObjectsOfType<RevitData>();
        
        if (debugMode)
            Debug.Log($"MeasurementDotPlacementHandler: Found {revitObjects.Length} RevitData objects for hand interaction");
        
        int successfulSubscriptions = 0;
        
        // Check if we can piggyback on the existing PropertyClickHandler system
        if (propertyHandler != null)
        {
            if (debugMode)
                Debug.Log("MeasurementDotPlacementHandler: Found PropertyClickHandler - attempting to integrate with existing system");
            
            // The PropertyClickHandler already handles all the interaction setup
            // We just need to detect when objects are clicked through that system
            successfulSubscriptions = revitObjects.Length;
            
            foreach (RevitData revitData in revitObjects)
            {
                subscribedObjects.Add(revitData.gameObject);
            }
            
            if (debugMode)
                Debug.Log($"MeasurementDotPlacementHandler: Integrated with PropertyClickHandler for {successfulSubscriptions} objects");
        }
        else
        {
            // Fallback: Try to find StatefulInteractable on individual objects
            foreach (RevitData revitData in revitObjects)
            {
                GameObject obj = revitData.gameObject;
                
                // Get existing StatefulInteractable component
                Component interactable = obj.GetComponent("StatefulInteractable");
                if (interactable != null)
                {
                    if (debugMode)
                        Debug.Log($"MeasurementDotPlacementHandler: Found StatefulInteractable on {obj.name}");
                    
                    // Try to find and subscribe to the OnClicked event
                    bool subscribed = TrySubscribeToDotPlacementEvent(interactable, obj);
                    
                    if (subscribed)
                    {
                        subscribedObjects.Add(obj);
                        successfulSubscriptions++;
                        if (debugMode)
                            Debug.Log($"MeasurementDotPlacementHandler: Successfully subscribed to {obj.name}");
                    }
                }
                else if (debugMode)
                {
                    Debug.LogWarning($"MeasurementDotPlacementHandler: No StatefulInteractable found on {obj.name}");
                }
            }
        }
        
        if (debugMode)
        {
            Debug.Log($"MeasurementDotPlacementHandler: Successfully set up interaction for {successfulSubscriptions} out of {revitObjects.Length} RevitData objects");
        }
        
        if (successfulSubscriptions == 0)
        {
            Debug.LogError("MeasurementDotPlacementHandler: No objects were successfully set up for interaction!");
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
        if (!isPlacementModeActive)
        {
            if (debugMode)
                Debug.Log($"MeasurementDotPlacementHandler: Ignoring click on {clickedObject.name} - placement mode inactive");
            return;
        }
        
        if (debugMode)
            Debug.Log($"MeasurementDotPlacementHandler: Hand clicked on {clickedObject.name} for dot placement");
        
        currentHighlightedObject = clickedObject;
        
        Vector3 hitPoint;
        Vector3 hitNormal;
        
        if (GetCurrentHandRayHit(clickedObject, out hitPoint, out hitNormal))
        {
            GameObject clickedDot = GetClickedDotNearPosition(hitPoint);
            if (clickedDot != null)
            {
                HandleDotClick(clickedDot);
                return;
            }
            
            PlaceDotAtPosition(hitPoint, hitNormal);
        }
        else
        {
            PlaceDotOnObjectSmart(clickedObject);
        }
    }
    
    bool GetCurrentHandRayHit(GameObject targetObject, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        
        if (Time.time - lastHandRayUpdateTime > handRayUpdateInterval)
        {
            UpdateHandRayComponentsCache();
            lastHandRayUpdateTime = Time.time;
        }
        
        foreach (var handRayComponent in handRayComponents)
        {
            if (handRayComponent == null || !handRayComponent.gameObject.activeInHierarchy) continue;
            
            if (TryGetHandRayHitFromComponent(handRayComponent, targetObject, out hitPoint, out hitNormal))
            {
                if (debugMode)
                    Debug.Log($"MeasurementDotPlacementHandler: Got hit from {handRayComponent.GetType().Name}");
                return true;
            }
        }
        
        if (debugMode)
            Debug.LogWarning("MeasurementDotPlacementHandler: Could not get hand ray hit, using fallback");
        
        return false;
    }
    
    bool TryGetHandRayHitFromComponent(Component handRayComponent, GameObject targetObject, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        
        Type componentType = handRayComponent.GetType();
        string[] hitProperties = { 
            "raycastHit", "RaycastHit", "currentRaycastHit", "CurrentRaycastHit",
            "hitInfo", "HitInfo", "lastHit", "LastHit", "result", "Result"
        };
        
        foreach (string propName in hitProperties)
        {
            PropertyInfo prop = componentType.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null)
            {
                object value = prop.GetValue(handRayComponent);
                if (TryExtractHitInfo(value, targetObject, out hitPoint, out hitNormal))
                    return true;
            }
            
            FieldInfo field = componentType.GetField(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                object value = field.GetValue(handRayComponent);
                if (TryExtractHitInfo(value, targetObject, out hitPoint, out hitNormal))
                    return true;
            }
        }
        
        return false;
    }
    
    bool TryExtractHitInfo(object hitObject, GameObject targetObject, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        
        if (hitObject == null) return false;
        
        if (hitObject is RaycastHit)
        {
            RaycastHit hit = (RaycastHit)hitObject;
            if (hit.collider != null && hit.collider.gameObject == targetObject)
            {
                hitPoint = hit.point;
                hitNormal = hit.normal;
                return true;
            }
        }
        
        Type hitType = hitObject.GetType();
        Vector3? position = TryGetVector3FromObject(hitObject, hitType, new string[] { "point", "position", "hitPoint", "worldPosition" });
        Vector3? normal = TryGetVector3FromObject(hitObject, hitType, new string[] { "normal", "surfaceNormal", "hitNormal" });
        GameObject hitGameObject = TryGetGameObjectFromObject(hitObject, hitType);
        
        if (position.HasValue && hitGameObject == targetObject)
        {
            hitPoint = position.Value;
            hitNormal = normal ?? Vector3.up;
            return true;
        }
        
        return false;
    }
    
    Vector3? TryGetVector3FromObject(object obj, Type objType, string[] propertyNames)
    {
        foreach (string propName in propertyNames)
        {
            PropertyInfo prop = objType.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.PropertyType == typeof(Vector3))
            {
                return (Vector3)prop.GetValue(obj);
            }
            
            FieldInfo field = objType.GetField(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(Vector3))
            {
                return (Vector3)field.GetValue(obj);
            }
        }
        return null;
    }
    
    GameObject TryGetGameObjectFromObject(object obj, Type objType)
    {
        string[] gameObjectProperties = { "gameObject", "GameObject", "collider", "transform" };
        
        foreach (string propName in gameObjectProperties)
        {
            PropertyInfo prop = objType.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null)
            {
                object value = prop.GetValue(obj);
                if (value is GameObject) return (GameObject)value;
                if (value is Collider) return ((Collider)value).gameObject;
                if (value is Transform) return ((Transform)value).gameObject;
            }
            
            FieldInfo field = objType.GetField(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                object value = field.GetValue(obj);
                if (value is GameObject) return (GameObject)value;
                if (value is Collider) return ((Collider)value).gameObject;
                if (value is Transform) return ((Transform)value).gameObject;
            }
        }
        
        return null;
    }
    
    void PlaceDotOnObjectSmart(GameObject targetObject)
    {
        Vector3 surfacePosition;
        Vector3 surfaceNormal;
        
        if (GetSmartSurfacePointOnObject(targetObject, out surfacePosition, out surfaceNormal))
        {
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
            Renderer objRenderer = obj.GetComponent<Renderer>();
            if (objRenderer != null)
            {
                position = objRenderer.bounds.center;
                normal = Vector3.up;
                return true;
            }
            
            position = obj.transform.position;
            normal = Vector3.up;
            return true;
        }
        
        if (Camera.main != null)
        {
            Vector3 cameraPosition = Camera.main.transform.position;
            Vector3 closestPoint = objCollider.ClosestPoint(cameraPosition);
            
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
            
            position = closestPoint;
            normal = (closestPoint - obj.transform.position).normalized;
            if (normal.magnitude < 0.001f) normal = Vector3.up;
            return true;
        }
        
        Renderer objRenderer2 = obj.GetComponent<Renderer>();
        if (objRenderer2 != null)
        {
            position = objRenderer2.bounds.center;
            normal = Vector3.up;
            return true;
        }
        
        position = obj.transform.position;
        normal = Vector3.up;
        return true;
    }
    
    GameObject GetClickedDotNearPosition(Vector3 position)
    {
        foreach (GameObject dot in currentLoop)
        {
            if (dot != null && Vector3.Distance(dot.transform.position, position) <= dotClickThreshold)
            {
                return dot;
            }
        }
        
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
        
        dotLoops.Add(new List<GameObject>(currentLoop));
        int loopIndex = dotLoops.Count - 1;
        
        for (int i = 0; i < currentLoop.Count; i++)
        {
            currentLoop[i].name = $"MeasurementLoop{loopIndex + 1}_Dot{i + 1}";
        }
        
        if (debugMode)
            Debug.Log($"MeasurementDotPlacementHandler: Closed loop {loopIndex + 1} with {currentLoop.Count} dots");
        
        OnMeasurementLoopClosed?.Invoke(loopIndex);
        StartNewLoop();
    }
    
    void StartNewLoop()
    {
        currentLoop.Clear();
        int newLoopIndex = dotLoops.Count;
        
        if (debugMode)
            Debug.Log($"MeasurementDotPlacementHandler: Started new loop {newLoopIndex + 1}");
        
        OnMeasurementNewLoopStarted?.Invoke(newLoopIndex);
    }
    
    void PlaceDotAtPosition(Vector3 position, Vector3 normal)
    {
        GameObject newDot = Instantiate(dotPrefab, position + normal * surfaceOffset, Quaternion.identity, dotsParent);
        newDot.SetActive(true);
        
        bool isFirstDot = currentLoop.Count == 0;
        int dotNumber = currentLoop.Count + 1;
        
        newDot.name = $"MeasurementTempLoop_Dot{dotNumber}";
        newDot.transform.up = normal;
        
        currentLoop.Add(newDot);
        UpdateDotAppearance(newDot, isFirstDot);
        
        if (debugMode)
            Debug.Log($"MeasurementDotPlacementHandler: Placed dot #{dotNumber} at {position}");
    }
    
    void CreateDefaultDotPrefab()
    {
        dotPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dotPrefab.name = "MeasurementDotPrefab";
        
        Destroy(dotPrefab.GetComponent<Collider>());
        dotPrefab.transform.localScale = Vector3.one * dotSize;
        
        Renderer renderer = dotPrefab.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            renderer.material.color = dotColor;
        }
        
        dotPrefab.SetActive(false);
    }
    
    void SetupDotPlacementButtons()
    {
        if (dotPlacementButton != null)
        {
            SetupSingleButton(dotPlacementButton, "first");
        }
        
        if (dotPlacementButton2 != null)
        {
            SetupSingleButton(dotPlacementButton2, "second");
        }
    }
    
    void SetupSingleButton(GameObject button, string buttonName)
    {
        Component buttonInteractable = button.GetComponent("StatefulInteractable");
        if (buttonInteractable != null)
        {
            bool subscribed = TrySubscribeToButtonClick(buttonInteractable);
            
            if (debugMode)
            {
                if (subscribed)
                    Debug.Log($"MeasurementDotPlacementHandler: Successfully set up {buttonName} button");
                else
                    Debug.LogWarning($"MeasurementDotPlacementHandler: Failed to set up {buttonName} button");
            }
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
        isPlacementModeActive = !isPlacementModeActive;
        
        if (panel != null)
        {
            panel.SetActive(!panel.activeSelf);
        }
        
        if (debugMode)
            Debug.Log($"MeasurementDotPlacementHandler: Dot placement mode is now {(isPlacementModeActive ? "ACTIVE" : "INACTIVE")}");
        
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
        CheckForParameterChanges();
        currentHighlightedObject = GetCurrentlyHighlightedObject();
        
        // If we're using the PropertyClickHandler system, check for object clicks
        if (propertyHandler != null && isPlacementModeActive)
        {
            GameObject currentlySelected = propertyHandler.GetCurrentlySelected();
            if (currentlySelected != null && currentlySelected != currentHighlightedObject)
            {
                // An object was clicked through the PropertyClickHandler
                if (subscribedObjects.Contains(currentlySelected))
                {
                    OnObjectClickedForDotPlacement(currentlySelected);
                }
            }
        }
        
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
                    Debug.Log("MeasurementDotPlacementHandler: Exited dot placement mode");
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
        
        for (int i = 0; i < currentLoop.Count; i++)
        {
            GameObject dot = currentLoop[i];
            if (dot != null)
            {
                UpdateDotAppearance(dot, i == 0);
            }
        }
        
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
        float size = isFirstDot ? dotSize * firstDotSizeMultiplier : dotSize;
        dot.transform.localScale = Vector3.one * size;
        
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
        foreach (var loop in dotLoops)
        {
            foreach (GameObject dot in loop)
            {
                if (dot != null)
                    Destroy(dot);
            }
        }
        dotLoops.Clear();
        
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
        
        foreach (var loop in dotLoops)
        {
            foreach (GameObject dot in loop)
            {
                if (dot != null)
                    positions.Add(dot.transform.position);
            }
        }
        
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
    
    public void SubscribeToNewObject(GameObject obj)
    {
        if (obj.GetComponent<RevitData>() == null) return;
        if (subscribedObjects.Contains(obj)) return;
        
        // Get existing StatefulInteractable component (same as PropertyClickHandler)
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
        else if (debugMode)
        {
            Debug.LogWarning($"MeasurementDotPlacementHandler: No StatefulInteractable found on {obj.name}");
        }
    }
    
    void OnValidate()
    {
        dotSize = Mathf.Max(0.001f, dotSize);
        firstDotSizeMultiplier = Mathf.Max(0.1f, firstDotSizeMultiplier);
        surfaceOffset = Mathf.Max(0.0001f, surfaceOffset);
        dotClickThreshold = Mathf.Max(0.001f, dotClickThreshold);
    }
    
    void OnDestroy()
    {
        ClearAllDots();
        
        if (dotPrefab != null && dotPrefab.name == "MeasurementDotPrefab")
            Destroy(dotPrefab);
        
        subscribedObjects.Clear();
    }
}

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