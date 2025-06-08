using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Reflection;
using System;
using UnityEngine.Events;
using System.Linq;

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
    
    // Events for loop management
    public System.Action<int> OnLoopClosed;
    public System.Action<int> OnNewLoopStarted;
    
    // Hand interaction tracking
    private HashSet<GameObject> subscribedObjects = new HashSet<GameObject>();
    
    // Cache for hand ray components
    private Component[] handRayComponents;
    private float lastHandRayUpdateTime = 0f;
    private float handRayUpdateInterval = 0.1f; // Update every 100ms
    
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
        
        // Hook into the button's functionality
        SetupDotPlacementButton();
        
        // Setup hand interactions for all RevitData objects
        SetupHandInteractions();
        
        // Initialize hand ray components cache
        UpdateHandRayComponentsCache();
    }
    
    void UpdateHandRayComponentsCache()
    {
        // Cache hand ray components for better performance
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
            Debug.Log($"DotPlacementHandler: Found {handRayComponents.Length} potential hand ray components");
    }
    
    void SetupHandInteractions()
    {
        // Find all objects with RevitData component
        RevitData[] revitObjects = FindObjectsOfType<RevitData>();
        
        if (debugMode)
            Debug.Log($"DotPlacementHandler: Found {revitObjects.Length} RevitData objects for hand interaction");
        
        foreach (RevitData revitData in revitObjects)
        {
            GameObject obj = revitData.gameObject;
            
            // Get existing StatefulInteractable component
            Component interactable = obj.GetComponent("StatefulInteractable");
            if (interactable != null)
            {
                if (debugMode)
                    Debug.Log($"DotPlacementHandler: Found StatefulInteractable on {obj.name}");
                
                // Try to find and subscribe to the OnClicked event for dot placement
                bool subscribed = TrySubscribeToDotPlacementEvent(interactable, obj);
                
                if (subscribed)
                {
                    subscribedObjects.Add(obj);
                    if (debugMode)
                        Debug.Log($"DotPlacementHandler: Successfully subscribed to {obj.name}");
                }
                else if (debugMode)
                {
                    Debug.LogWarning($"DotPlacementHandler: Failed to subscribe to click event on {obj.name}");
                }
            }
            else if (debugMode)
            {
                Debug.LogWarning($"DotPlacementHandler: No StatefulInteractable found on {obj.name}");
            }
        }
    }
    
    bool TrySubscribeToDotPlacementEvent(Component interactable, GameObject targetObj)
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
                    eventValue.AddListener(() => OnObjectClickedForDotPlacement(targetObj));
                    if (debugMode)
                        Debug.Log($"DotPlacementHandler: Successfully subscribed to {eventName} field on {targetObj.name}");
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
                    eventValue.AddListener(() => OnObjectClickedForDotPlacement(targetObj));
                    if (debugMode)
                        Debug.Log($"DotPlacementHandler: Successfully subscribed to {eventName} property on {targetObj.name}");
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
                Debug.Log($"DotPlacementHandler: Ignoring click on {clickedObject.name} - placement mode inactive");
            return;
        }
        
        if (debugMode)
            Debug.Log($"DotPlacementHandler: Hand clicked on {clickedObject.name} for dot placement");
        
        // Update the currently highlighted object
        currentHighlightedObject = clickedObject;
        
        // Get the current hand ray hit point on this object
        Vector3 hitPoint;
        Vector3 hitNormal;
        
        if (GetCurrentHandRayHit(clickedObject, out hitPoint, out hitNormal))
        {
            // Check if we clicked on an existing dot first
            GameObject clickedDot = GetClickedDotNearPosition(hitPoint);
            if (clickedDot != null)
            {
                HandleDotClick(clickedDot);
                return;
            }
            
            // Place dot at the exact ray hit point
            PlaceDotAtPosition(hitPoint, hitNormal);
        }
        else
        {
            // Fallback: Place dot on object surface using a more sophisticated method
            PlaceDotOnObjectSmart(clickedObject);
        }
    }
    
    bool GetCurrentHandRayHit(GameObject targetObject, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        
        // Update hand ray cache if needed
        if (Time.time - lastHandRayUpdateTime > handRayUpdateInterval)
        {
            UpdateHandRayComponentsCache();
            lastHandRayUpdateTime = Time.time;
        }
        
        // Try to get hit info from cached hand ray components
        foreach (var handRayComponent in handRayComponents)
        {
            if (handRayComponent == null || !handRayComponent.gameObject.activeInHierarchy) continue;
            
            if (TryGetHandRayHitFromComponent(handRayComponent, targetObject, out hitPoint, out hitNormal))
            {
                if (debugMode)
                    Debug.Log($"DotPlacementHandler: Got hit from {handRayComponent.GetType().Name} on {handRayComponent.gameObject.name}");
                return true;
            }
        }
        
        // Try to find MRTK3 interaction manager and get current interaction
        if (TryGetHitFromInteractionManager(targetObject, out hitPoint, out hitNormal))
        {
            if (debugMode)
                Debug.Log("DotPlacementHandler: Got hit from interaction manager");
            return true;
        }
        
        // Try to get hit from XR ray interactor
        if (TryGetHitFromXRRayInteractor(targetObject, out hitPoint, out hitNormal))
        {
            if (debugMode)
                Debug.Log("DotPlacementHandler: Got hit from XR ray interactor");
            return true;
        }
        
        if (debugMode)
            Debug.LogWarning("DotPlacementHandler: Could not get hand ray hit, using fallback");
        
        return false;
    }
    
    bool TryGetHandRayHitFromComponent(Component handRayComponent, GameObject targetObject, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        
        Type componentType = handRayComponent.GetType();
        
        // Try to get raycast result or hit info
        string[] hitProperties = { 
            "raycastHit", "RaycastHit", "currentRaycastHit", "CurrentRaycastHit",
            "hitInfo", "HitInfo", "lastHit", "LastHit", "result", "Result",
            "raycastResult", "RaycastResult", "currentHit", "CurrentHit"
        };
        
        foreach (string propName in hitProperties)
        {
            // Try property first
            PropertyInfo prop = componentType.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null)
            {
                object value = prop.GetValue(handRayComponent);
                if (TryExtractHitInfo(value, targetObject, out hitPoint, out hitNormal))
                    return true;
            }
            
            // Try field
            FieldInfo field = componentType.GetField(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                object value = field.GetValue(handRayComponent);
                if (TryExtractHitInfo(value, targetObject, out hitPoint, out hitNormal))
                    return true;
            }
        }
        
        // Try to get ray origin and direction for manual raycast
        if (TryGetRayFromComponent(handRayComponent, out Ray ray))
        {
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, Mathf.Infinity))
            {
                if (hit.collider.gameObject == targetObject)
                {
                    hitPoint = hit.point;
                    hitNormal = hit.normal;
                    return true;
                }
            }
        }
        
        return false;
    }
    
    bool TryExtractHitInfo(object hitObject, GameObject targetObject, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        
        if (hitObject == null) return false;
        
        // Handle RaycastHit
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
        
        // Try to handle custom hit structures using reflection
        Type hitType = hitObject.GetType();
        
        // Look for point/position field/property
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
    
    bool TryGetRayFromComponent(Component component, out Ray ray)
    {
        ray = new Ray();
        Type componentType = component.GetType();
        
        // Try to get ray origin and direction
        Vector3? origin = TryGetVector3FromObject(component, componentType, new string[] { "origin", "rayOrigin", "startPoint", "position" });
        Vector3? direction = TryGetVector3FromObject(component, componentType, new string[] { "direction", "rayDirection", "forward" });
        
        if (origin.HasValue && direction.HasValue)
        {
            ray = new Ray(origin.Value, direction.Value);
            return true;
        }
        
        // Try to get from transform if it represents a ray
        if (component.transform != null)
        {
            ray = new Ray(component.transform.position, component.transform.forward);
            return true;
        }
        
        return false;
    }
    
    bool TryGetHitFromInteractionManager(GameObject targetObject, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        
        // Look for MRTK3 interaction manager
        var managers = FindObjectsOfType<MonoBehaviour>().Where(mb => 
            mb.GetType().Name.Contains("InteractionManager") ||
            mb.GetType().Name.Contains("XRInteraction")).ToArray();
        
        foreach (var manager in managers)
        {
            if (TryGetCurrentInteractionHit(manager, targetObject, out hitPoint, out hitNormal))
                return true;
        }
        
        return false;
    }
    
    bool TryGetCurrentInteractionHit(Component manager, GameObject targetObject, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        
        Type managerType = manager.GetType();
        
        // Try to get current interaction or active interactor
        string[] interactionProperties = { 
            "currentInteraction", "activeInteractor", "currentInteractor", 
            "focusedInteractor", "selectedInteractor" 
        };
        
        foreach (string propName in interactionProperties)
        {
            object interactor = TryGetValueFromObject(manager, managerType, propName);
            if (interactor != null)
            {
                if (TryGetHitFromInteractor(interactor, targetObject, out hitPoint, out hitNormal))
                    return true;
            }
        }
        
        return false;
    }
    
    object TryGetValueFromObject(object obj, Type objType, string propertyName)
    {
        PropertyInfo prop = objType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null)
        {
            return prop.GetValue(obj);
        }
        
        FieldInfo field = objType.GetField(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            return field.GetValue(obj);
        }
        
        return null;
    }
    
    bool TryGetHitFromInteractor(object interactor, GameObject targetObject, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        
        if (interactor == null) return false;
        
        Type interactorType = interactor.GetType();
        
        // Try to get hit info from interactor
        string[] hitProperties = { 
            "raycastHit", "currentRaycastHit", "lastHit", "hitInfo", "result",
            "currentHit", "raycastResult"
        };
        
        foreach (string propName in hitProperties)
        {
            object hitValue = TryGetValueFromObject(interactor, interactorType, propName);
            if (TryExtractHitInfo(hitValue, targetObject, out hitPoint, out hitNormal))
                return true;
        }
        
        return false;
    }
    
    bool TryGetHitFromXRRayInteractor(GameObject targetObject, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        
        // Look for XR Ray Interactor components
        var xrInteractors = FindObjectsOfType<MonoBehaviour>().Where(mb => 
            mb.GetType().Name.Contains("XRRayInteractor") ||
            mb.GetType().Name.Contains("XRDirectInteractor") ||
            mb.GetType().Name.Contains("RayInteractor")).ToArray();
        
        foreach (var interactor in xrInteractors)
        {
            if (interactor.gameObject.activeInHierarchy && 
                TryGetHitFromInteractor(interactor, targetObject, out hitPoint, out hitNormal))
            {
                return true;
            }
        }
        
        return false;
    }
    
    void PlaceDotOnObjectSmart(GameObject targetObject)
    {
        // Get the surface position and normal using multiple methods
        Vector3 surfacePosition;
        Vector3 surfaceNormal;
        
        if (GetSmartSurfacePointOnObject(targetObject, out surfacePosition, out surfaceNormal))
        {
            PlaceDotAtPosition(surfacePosition, surfaceNormal);
        }
        else if (debugMode)
        {
            Debug.LogWarning($"DotPlacementHandler: Could not find surface point on {targetObject.name}");
        }
    }
    
    bool GetSmartSurfacePointOnObject(GameObject obj, out Vector3 position, out Vector3 normal)
    {
        position = Vector3.zero;
        normal = Vector3.up;
        
        Collider objCollider = obj.GetComponent<Collider>();
        if (objCollider == null)
        {
            if (debugMode)
                Debug.LogWarning($"DotPlacementHandler: No collider found on {obj.name}");
            return false;
        }
        
        // Method 1: Try to use hand position if available
        Vector3 handPosition = GetCurrentHandPosition();
        if (handPosition != Vector3.zero)
        {
            Vector3 closestPoint = objCollider.ClosestPoint(handPosition);
            Vector3 directionFromCenter = (closestPoint - obj.transform.position).normalized;
            
            // Raycast from slightly inside the object outward to get the surface normal
            Vector3 rayStart = obj.transform.position + directionFromCenter * 0.001f;
            Vector3 rayDirection = directionFromCenter;
            
            RaycastHit hit;
            if (Physics.Raycast(rayStart, rayDirection, out hit, Mathf.Infinity))
            {
                if (hit.collider == objCollider)
                {
                    position = hit.point;
                    normal = hit.normal;
                    return true;
                }
            }
            
            // Fallback: use closest point and calculate normal
            position = closestPoint;
            normal = (closestPoint - obj.transform.position).normalized;
            return true;
        }
        
        // Method 2: Use camera position as reference
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
            return true;
        }
        
        // Method 3: Use object bounds center
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
    
    Vector3 GetCurrentHandPosition()
    {
        // Try to find hand tracking components
        var handComponents = FindObjectsOfType<MonoBehaviour>().Where(mb => 
            mb.gameObject.name.ToLower().Contains("hand") ||
            mb.GetType().Name.ToLower().Contains("hand")).ToArray();
        
        foreach (var handComponent in handComponents)
        {
            if (handComponent.gameObject.activeInHierarchy)
            {
                // Try to get position from the hand component
                Vector3? handPos = TryGetVector3FromObject(handComponent, handComponent.GetType(), 
                    new string[] { "position", "palmPosition", "handPosition", "jointPosition" });
                if (handPos.HasValue)
                {
                    return handPos.Value;
                }
                
                // Use transform position as fallback
                return handComponent.transform.position;
            }
        }
        
        return Vector3.zero;
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
        
        if (debugMode)
            Debug.Log($"DotPlacementHandler: Placed dot #{dotNumber} at {position} on {currentHighlightedObject?.name ?? "unknown object"}");
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
        
        // Unsubscribe from all object events
        UnsubscribeFromAllObjects();
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
    
    void UnsubscribeFromAllObjects()
    {
        foreach (GameObject obj in subscribedObjects)
        {
            if (obj != null)
            {
                Component interactable = obj.GetComponent("StatefulInteractable");
                if (interactable != null)
                {
                    UnsubscribeFromObjectClickEvent(interactable);
                }
            }
        }
        subscribedObjects.Clear();
    }
    
    void UnsubscribeFromObjectClickEvent(Component interactable)
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
                    // Note: We can't easily remove specific listeners, so we'll leave them
                    // The listeners will naturally be cleaned up when the object is destroyed
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
                    // Note: We can't easily remove specific listeners, so we'll leave them
                    // The listeners will naturally be cleaned up when the object is destroyed
                    return;
                }
            }
        }
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
                    Debug.Log($"DotPlacementHandler: Manually subscribed to {obj.name}");
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