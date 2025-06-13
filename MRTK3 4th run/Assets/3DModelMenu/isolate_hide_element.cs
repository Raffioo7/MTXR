using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Reflection;
using System;
using UnityEngine.Events;
using System.Linq;

public class MeshIsolationHandler_MRTK3 : MonoBehaviour
{
    [Header("MRTK3 Buttons")]
    [Tooltip("Button to activate isolation mode")]
    public GameObject isolateButton;
    
    [Tooltip("Button to activate hide mode")]
    public GameObject hideButton;
    
    [Tooltip("Button to show all hidden/isolated elements")]
    public GameObject showAllButton;
    
    [Header("Target Settings")]
    [Tooltip("Only affect children of this object (leave empty to use the object this script is on)")]
    public Transform targetParent;
    
    [Header("Debug")]
    public bool debugMode = true;
    
    // Mode enum
    public enum MeshInteractionMode
    {
        None,
        Isolate,
        Hide
    }
    
    // Private fields
    private MeshInteractionMode currentMeshMode = MeshInteractionMode.None;
    private PropertyClickHandler_MRTK3 meshPropertyHandler;
    private HashSet<GameObject> meshSubscribedObjects = new HashSet<GameObject>();
    
    // Storage for original states
    private Dictionary<GameObject, OriginalMeshRenderState> meshOriginalStates = new Dictionary<GameObject, OriginalMeshRenderState>();
    private HashSet<GameObject> meshHiddenObjects = new HashSet<GameObject>();
    private HashSet<GameObject> meshIsolatedObjects = new HashSet<GameObject>();
    
    // Hand interaction tracking
    private Component[] meshHandRayComponents;
    private float lastMeshHandRayUpdateTime = 0f;
    private float meshHandRayUpdateInterval = 0.1f;
    
    // Structure to store original render state
    [System.Serializable]
    public class OriginalMeshRenderState
    {
        public bool wasActive;
        public int layer;
        
        public OriginalMeshRenderState(GameObject gameObject)
        {
            wasActive = gameObject.activeSelf;
            layer = gameObject.layer;
        }
    }
    
    void Start()
    {
        // Set target parent if not assigned
        if (targetParent == null)
        {
            targetParent = this.transform;
        }
        
        // Find the PropertyClickHandler_MRTK3 script
        meshPropertyHandler = FindObjectOfType<PropertyClickHandler_MRTK3>();
        
        if (meshPropertyHandler == null)
        {
            Debug.LogWarning("MeshIsolationHandler: Could not find PropertyClickHandler_MRTK3 script!");
        }
        
        // Setup buttons
        SetupMeshButtons();
        
        // Setup hand interactions for RevitData objects under target parent
        SetupMeshHandInteractions();
        
        // Initialize hand ray components cache
        UpdateMeshHandRayComponentsCache();
    }
    
    void SetupMeshButtons()
    {
        if (isolateButton != null)
        {
            SetupSingleMeshButton(isolateButton, () => SetMeshMode(MeshInteractionMode.Isolate), "Isolate");
        }
        else
        {
            Debug.LogWarning("MeshIsolationHandler: No isolate button assigned!");
        }
        
        if (hideButton != null)
        {
            SetupSingleMeshButton(hideButton, () => SetMeshMode(MeshInteractionMode.Hide), "Hide");
        }
        else
        {
            Debug.LogWarning("MeshIsolationHandler: No hide button assigned!");
        }
        
        if (showAllButton != null)
        {
            SetupSingleMeshButton(showAllButton, ShowAllMeshes, "Show All");
        }
        else
        {
            Debug.LogWarning("MeshIsolationHandler: No show all button assigned!");
        }
    }
    
    void SetupSingleMeshButton(GameObject button, System.Action onClickAction, string buttonName)
    {
        Component buttonInteractable = button.GetComponent("StatefulInteractable");
        if (buttonInteractable != null)
        {
            bool subscribed = TrySubscribeToMeshButtonClick(buttonInteractable, onClickAction);
            
            if (debugMode)
            {
                if (subscribed)
                    Debug.Log($"MeshIsolationHandler: Successfully set up {buttonName} button");
                else
                    Debug.LogWarning($"MeshIsolationHandler: Failed to set up {buttonName} button");
            }
        }
        else if (debugMode)
        {
            Debug.LogWarning($"MeshIsolationHandler: No StatefulInteractable found on {buttonName} button");
        }
    }
    
    bool TrySubscribeToMeshButtonClick(Component interactable, System.Action onClickAction)
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
                    eventValue.AddListener(() => onClickAction.Invoke());
                    return true;
                }
            }
            
            PropertyInfo propertyInfo = interactableType.GetProperty(eventName);
            if (propertyInfo != null)
            {
                var eventValue = propertyInfo.GetValue(interactable) as UnityEngine.Events.UnityEvent;
                if (eventValue != null)
                {
                    eventValue.AddListener(() => onClickAction.Invoke());
                    return true;
                }
            }
        }
        
        return false;
    }
    
    void SetMeshMode(MeshInteractionMode mode)
    {
        currentMeshMode = mode;
        UpdateMeshButtonVisualFeedback();
        
        if (debugMode)
            Debug.Log($"MeshIsolationHandler: Mode set to {mode}");
    }
    
    void UpdateMeshButtonVisualFeedback()
    {
        UpdateSingleMeshButtonFeedback(isolateButton, currentMeshMode == MeshInteractionMode.Isolate);
        UpdateSingleMeshButtonFeedback(hideButton, currentMeshMode == MeshInteractionMode.Hide);
        
        // Show All button is always available, so we can give it a different color when there are hidden/isolated objects
        bool hasHiddenOrIsolated = meshHiddenObjects.Count > 0 || meshIsolatedObjects.Count > 0;
        UpdateSingleMeshButtonFeedback(showAllButton, hasHiddenOrIsolated);
    }
    
    void UpdateSingleMeshButtonFeedback(GameObject button, bool isActive)
    {
        if (button != null)
        {
            Renderer buttonRenderer = button.GetComponentInChildren<Renderer>();
            if (buttonRenderer != null && buttonRenderer.material != null)
            {
                Color targetColor = isActive ? Color.green : Color.white;
                
                if (buttonRenderer.material.HasProperty("_Color"))
                {
                    buttonRenderer.material.color = targetColor;
                }
                else if (buttonRenderer.material.HasProperty("_BaseColor"))
                {
                    buttonRenderer.material.SetColor("_BaseColor", targetColor);
                }
            }
        }
    }
    
    #region Hand Ray Detection
    
    void UpdateMeshHandRayComponentsCache()
    {
        var allComponents = FindObjectsOfType<MonoBehaviour>();
        meshHandRayComponents = allComponents.Where(mb => 
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
            Debug.Log($"MeshIsolationHandler: Found {meshHandRayComponents.Length} potential hand ray components");
    }
    
    void SetupMeshHandInteractions()
    {
        // Find RevitData objects only under the target parent
        RevitData[] revitObjects = targetParent.GetComponentsInChildren<RevitData>();
        
        if (debugMode)
            Debug.Log($"MeshIsolationHandler: Found {revitObjects.Length} RevitData objects under {targetParent.name} for hand interaction");
        
        foreach (RevitData revitData in revitObjects)
        {
            GameObject obj = revitData.gameObject;
            
            Component interactable = obj.GetComponent("StatefulInteractable");
            if (interactable != null)
            {
                if (debugMode)
                    Debug.Log($"MeshIsolationHandler: Found StatefulInteractable on {obj.name}");
                
                bool subscribed = TrySubscribeToMeshObjectClickEvent(interactable, obj);
                
                if (subscribed)
                {
                    meshSubscribedObjects.Add(obj);
                    if (debugMode)
                        Debug.Log($"MeshIsolationHandler: Successfully subscribed to {obj.name}");
                }
                else if (debugMode)
                {
                    Debug.LogWarning($"MeshIsolationHandler: Failed to subscribe to click event on {obj.name}");
                }
            }
            else if (debugMode)
            {
                Debug.LogWarning($"MeshIsolationHandler: No StatefulInteractable found on {obj.name}");
            }
        }
    }
    
    bool TrySubscribeToMeshObjectClickEvent(Component interactable, GameObject targetObj)
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
                    eventValue.AddListener(() => OnMeshObjectClickedForIsolation(targetObj));
                    if (debugMode)
                        Debug.Log($"MeshIsolationHandler: Successfully subscribed to {eventName} field on {targetObj.name}");
                    return true;
                }
            }
            
            PropertyInfo propertyInfo = interactableType.GetProperty(eventName);
            if (propertyInfo != null)
            {
                var eventValue = propertyInfo.GetValue(interactable) as UnityEvent;
                if (eventValue != null)
                {
                    eventValue.AddListener(() => OnMeshObjectClickedForIsolation(targetObj));
                    if (debugMode)
                        Debug.Log($"MeshIsolationHandler: Successfully subscribed to {eventName} property on {targetObj.name}");
                    return true;
                }
            }
        }
        
        return false;
    }
    
    public void OnMeshObjectClickedForIsolation(GameObject clickedObject)
    {
        if (currentMeshMode == MeshInteractionMode.None)
        {
            if (debugMode)
                Debug.Log($"MeshIsolationHandler: Ignoring click on {clickedObject.name} - no mode active");
            return;
        }
        
        if (debugMode)
            Debug.Log($"MeshIsolationHandler: Hand clicked on {clickedObject.name} for {currentMeshMode}");
        
        // Get the specific mesh object that was clicked
        GameObject clickedMeshObject = GetClickedMeshObject(clickedObject);
        
        if (clickedMeshObject != null)
        {
            if (currentMeshMode == MeshInteractionMode.Isolate)
            {
                IsolateMeshObject(clickedMeshObject);
            }
            else if (currentMeshMode == MeshInteractionMode.Hide)
            {
                HideMeshObject(clickedMeshObject);
            }
        }
        else if (debugMode)
        {
            Debug.LogWarning($"MeshIsolationHandler: Could not find clicked object {clickedObject.name}");
        }
    }
    
    GameObject GetClickedMeshObject(GameObject clickedObject)
    {
        // For now, we'll use the clicked object itself
        // You could make this more sophisticated if needed
        return clickedObject;
    }
    
    bool GetCurrentMeshHandRayHit(GameObject targetObject, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        
        if (Time.time - lastMeshHandRayUpdateTime > meshHandRayUpdateInterval)
        {
            UpdateMeshHandRayComponentsCache();
            lastMeshHandRayUpdateTime = Time.time;
        }
        
        foreach (var handRayComponent in meshHandRayComponents)
        {
            if (handRayComponent == null || !handRayComponent.gameObject.activeInHierarchy) continue;
            
            if (TryGetMeshHandRayHitFromComponent(handRayComponent, targetObject, out hitPoint, out hitNormal))
            {
                if (debugMode)
                    Debug.Log($"MeshIsolationHandler: Got hit from {handRayComponent.GetType().Name} on {handRayComponent.gameObject.name}");
                return true;
            }
        }
        
        if (TryGetMeshHitFromInteractionManager(targetObject, out hitPoint, out hitNormal))
        {
            if (debugMode)
                Debug.Log("MeshIsolationHandler: Got hit from interaction manager");
            return true;
        }
        
        if (TryGetMeshHitFromXRRayInteractor(targetObject, out hitPoint, out hitNormal))
        {
            if (debugMode)
                Debug.Log("MeshIsolationHandler: Got hit from XR ray interactor");
            return true;
        }
        
        if (debugMode)
            Debug.LogWarning("MeshIsolationHandler: Could not get hand ray hit, using fallback");
        
        return false;
    }
    
    bool TryGetMeshHandRayHitFromComponent(Component handRayComponent, GameObject targetObject, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        
        Type componentType = handRayComponent.GetType();
        
        string[] hitProperties = { 
            "raycastHit", "RaycastHit", "currentRaycastHit", "CurrentRaycastHit",
            "hitInfo", "HitInfo", "lastHit", "LastHit", "result", "Result",
            "raycastResult", "RaycastResult", "currentHit", "CurrentHit"
        };
        
        foreach (string propName in hitProperties)
        {
            PropertyInfo prop = componentType.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null)
            {
                object value = prop.GetValue(handRayComponent);
                if (TryExtractMeshHitInfo(value, targetObject, out hitPoint, out hitNormal))
                    return true;
            }
            
            FieldInfo field = componentType.GetField(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                object value = field.GetValue(handRayComponent);
                if (TryExtractMeshHitInfo(value, targetObject, out hitPoint, out hitNormal))
                    return true;
            }
        }
        
        if (TryGetMeshRayFromComponent(handRayComponent, out Ray ray))
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
    
    bool TryExtractMeshHitInfo(object hitObject, GameObject targetObject, out Vector3 hitPoint, out Vector3 hitNormal)
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
        Vector3? position = TryGetMeshVector3FromObject(hitObject, hitType, new string[] { "point", "position", "hitPoint", "worldPosition" });
        Vector3? normal = TryGetMeshVector3FromObject(hitObject, hitType, new string[] { "normal", "surfaceNormal", "hitNormal" });
        GameObject hitGameObject = TryGetMeshGameObjectFromObject(hitObject, hitType);
        
        if (position.HasValue && hitGameObject == targetObject)
        {
            hitPoint = position.Value;
            hitNormal = normal ?? Vector3.up;
            return true;
        }
        
        return false;
    }
    
    Vector3? TryGetMeshVector3FromObject(object obj, Type objType, string[] propertyNames)
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
    
    GameObject TryGetMeshGameObjectFromObject(object obj, Type objType)
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
    
    bool TryGetMeshRayFromComponent(Component component, out Ray ray)
    {
        ray = new Ray();
        Type componentType = component.GetType();
        
        Vector3? origin = TryGetMeshVector3FromObject(component, componentType, new string[] { "origin", "rayOrigin", "startPoint", "position" });
        Vector3? direction = TryGetMeshVector3FromObject(component, componentType, new string[] { "direction", "rayDirection", "forward" });
        
        if (origin.HasValue && direction.HasValue)
        {
            ray = new Ray(origin.Value, direction.Value);
            return true;
        }
        
        if (component.transform != null)
        {
            ray = new Ray(component.transform.position, component.transform.forward);
            return true;
        }
        
        return false;
    }
    
    bool TryGetMeshHitFromInteractionManager(GameObject targetObject, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        
        var managers = FindObjectsOfType<MonoBehaviour>().Where(mb => 
            mb.GetType().Name.Contains("InteractionManager") ||
            mb.GetType().Name.Contains("XRInteraction")).ToArray();
        
        foreach (var manager in managers)
        {
            if (TryGetCurrentMeshInteractionHit(manager, targetObject, out hitPoint, out hitNormal))
                return true;
        }
        
        return false;
    }
    
    bool TryGetCurrentMeshInteractionHit(Component manager, GameObject targetObject, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        
        Type managerType = manager.GetType();
        string[] interactionProperties = { 
            "currentInteraction", "activeInteractor", "currentInteractor", 
            "focusedInteractor", "selectedInteractor" 
        };
        
        foreach (string propName in interactionProperties)
        {
            object interactor = TryGetMeshValueFromObject(manager, managerType, propName);
            if (interactor != null)
            {
                if (TryGetMeshHitFromInteractor(interactor, targetObject, out hitPoint, out hitNormal))
                    return true;
            }
        }
        
        return false;
    }
    
    object TryGetMeshValueFromObject(object obj, Type objType, string propertyName)
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
    
    bool TryGetMeshHitFromInteractor(object interactor, GameObject targetObject, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        
        if (interactor == null) return false;
        
        Type interactorType = interactor.GetType();
        string[] hitProperties = { 
            "raycastHit", "currentRaycastHit", "lastHit", "hitInfo", "result",
            "currentHit", "raycastResult"
        };
        
        foreach (string propName in hitProperties)
        {
            object hitValue = TryGetMeshValueFromObject(interactor, interactorType, propName);
            if (TryExtractMeshHitInfo(hitValue, targetObject, out hitPoint, out hitNormal))
                return true;
        }
        
        return false;
    }
    
    bool TryGetMeshHitFromXRRayInteractor(GameObject targetObject, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        
        var xrInteractors = FindObjectsOfType<MonoBehaviour>().Where(mb => 
            mb.GetType().Name.Contains("XRRayInteractor") ||
            mb.GetType().Name.Contains("XRDirectInteractor") ||
            mb.GetType().Name.Contains("RayInteractor")).ToArray();
        
        foreach (var interactor in xrInteractors)
        {
            if (interactor.gameObject.activeInHierarchy && 
                TryGetMeshHitFromInteractor(interactor, targetObject, out hitPoint, out hitNormal))
            {
                return true;
            }
        }
        
        return false;
    }
    
    #endregion
    
    #region Isolation and Hiding Logic
    
    void IsolateMeshObject(GameObject meshObject)
    {
        if (debugMode)
            Debug.Log($"MeshIsolationHandler: Isolating object {meshObject.name}");
        
        // Store original state if not already stored
        if (!meshOriginalStates.ContainsKey(meshObject))
        {
            meshOriginalStates[meshObject] = new OriginalMeshRenderState(meshObject);
        }
        
        // Clear any existing isolation first
        meshIsolatedObjects.Clear();
        
        // If this object was hidden, remove it from hidden list
        if (meshHiddenObjects.Contains(meshObject))
        {
            meshHiddenObjects.Remove(meshObject);
        }
        
        // Add to isolated list
        meshIsolatedObjects.Add(meshObject);
        
        // Hide all other objects in the scene except this isolated one
        UpdateAllMeshObjectsVisibility();
        
        UpdateMeshButtonVisualFeedback();
        
        if (debugMode)
            Debug.Log($"MeshIsolationHandler: Isolated {meshObject.name}, hiding all other objects");
    }
    
    void HideMeshObject(GameObject meshObject)
    {
        if (debugMode)
            Debug.Log($"MeshIsolationHandler: Hiding object {meshObject.name}");
        
        // Store original state if not already stored
        if (!meshOriginalStates.ContainsKey(meshObject))
        {
            meshOriginalStates[meshObject] = new OriginalMeshRenderState(meshObject);
        }
        
        // If this object was isolated, remove it from isolated list
        if (meshIsolatedObjects.Contains(meshObject))
        {
            meshIsolatedObjects.Remove(meshObject);
        }
        
        // Add to hidden list
        meshHiddenObjects.Add(meshObject);
        
        // Hide the object
        meshObject.SetActive(false);
        
        UpdateMeshButtonVisualFeedback();
    }
    
    void UpdateAllMeshObjectsVisibility()
    {
        if (meshIsolatedObjects.Count == 0) return;
        
        // Get all GameObjects with renderers under the target parent only
        Renderer[] childRenderers = targetParent.GetComponentsInChildren<Renderer>();
        HashSet<GameObject> childObjectsWithRenderers = new HashSet<GameObject>();
        
        foreach (Renderer renderer in childRenderers)
        {
            if (renderer != null && renderer.gameObject != null)
            {
                childObjectsWithRenderers.Add(renderer.gameObject);
            }
        }
        
        if (debugMode)
            Debug.Log($"MeshIsolationHandler: Found {childObjectsWithRenderers.Count} child objects with renderers under {targetParent.name}");
        
        foreach (GameObject obj in childObjectsWithRenderers)
        {
            // Store original state if not already stored
            if (!meshOriginalStates.ContainsKey(obj))
            {
                meshOriginalStates[obj] = new OriginalMeshRenderState(obj);
            }
            
            if (meshIsolatedObjects.Contains(obj))
            {
                // Keep isolated objects active
                obj.SetActive(true);
                if (debugMode)
                    Debug.Log($"MeshIsolationHandler: Keeping {obj.name} active (isolated)");
            }
            else
            {
                // Hide all other child objects
                obj.SetActive(false);
                if (debugMode)
                    Debug.Log($"MeshIsolationHandler: Hiding {obj.name}");
            }
        }
        
        if (debugMode)
            Debug.Log($"MeshIsolationHandler: Isolation complete - {meshIsolatedObjects.Count} isolated, {childObjectsWithRenderers.Count - meshIsolatedObjects.Count} hidden");
    }
    
    void ShowAllMeshes()
    {
        if (debugMode)
            Debug.Log("MeshIsolationHandler: Showing all objects");
        
        // Restore all objects to their original state
        foreach (var kvp in meshOriginalStates)
        {
            GameObject obj = kvp.Key;
            OriginalMeshRenderState originalState = kvp.Value;
            
            if (obj != null)
            {
                // Restore original active state
                obj.SetActive(originalState.wasActive);
                
                // Restore original layer
                obj.layer = originalState.layer;
            }
        }
        
        // Clear all tracking lists
        meshHiddenObjects.Clear();
        meshIsolatedObjects.Clear();
        meshOriginalStates.Clear();
        
        // Reset mode
        currentMeshMode = MeshInteractionMode.None;
        
        UpdateMeshButtonVisualFeedback();
    }
    
    #endregion
    
    #region Public Methods
    
    public void SetMeshIsolateMode()
    {
        SetMeshMode(MeshInteractionMode.Isolate);
    }
    
    public void SetMeshHideMode()
    {
        SetMeshMode(MeshInteractionMode.Hide);
    }
    
    public void ClearMeshMode()
    {
        currentMeshMode = MeshInteractionMode.None;
        UpdateMeshButtonVisualFeedback();
        
        if (debugMode)
            Debug.Log("MeshIsolationHandler: Mode cleared");
    }
    
    public bool IsMeshIsolateMode()
    {
        return currentMeshMode == MeshInteractionMode.Isolate;
    }
    
    public bool IsMeshHideMode()
    {
        return currentMeshMode == MeshInteractionMode.Hide;
    }
    
    public int GetMeshHiddenCount()
    {
        return meshHiddenObjects.Count;
    }
    
    public int GetMeshIsolatedCount()
    {
        return meshIsolatedObjects.Count;
    }
    
    public List<GameObject> GetMeshHiddenObjects()
    {
        return meshHiddenObjects.ToList();
    }
    
    public List<GameObject> GetMeshIsolatedObjects()
    {
        return meshIsolatedObjects.ToList();
    }
    
    #endregion
    
    void Update()
    {
        // Optional: Exit isolation/hide mode with Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (currentMeshMode != MeshInteractionMode.None)
            {
                ClearMeshMode();
                if (debugMode)
                    Debug.Log("MeshIsolationHandler: Exited mode with Escape key");
            }
        }
        
        // Optional: Show all with a key combination
        if (Input.GetKeyDown(KeyCode.R) && Input.GetKey(KeyCode.LeftControl))
        {
            ShowAllMeshes();
            if (debugMode)
                Debug.Log("MeshIsolationHandler: Showed all with Ctrl+R");
        }
    }
    
    void OnValidate()
    {
        // No validation needed anymore since we removed hiddenAlpha
    }
    
    void OnDestroy()
    {
        // Clean up - restore all objects to original state
        ShowAllMeshes();
        
        // Remove button listeners
        UnsubscribeFromAllMeshButtons();
        
        // Unsubscribe from all object events
        UnsubscribeFromAllMeshObjects();
    }
    
    void UnsubscribeFromAllMeshButtons()
    {
        if (isolateButton != null)
        {
            Component buttonInteractable = isolateButton.GetComponent("StatefulInteractable");
            if (buttonInteractable != null)
            {
                UnsubscribeFromMeshButtonClickEvent(buttonInteractable);
            }
        }
        
        if (hideButton != null)
        {
            Component buttonInteractable = hideButton.GetComponent("StatefulInteractable");
            if (buttonInteractable != null)
            {
                UnsubscribeFromMeshButtonClickEvent(buttonInteractable);
            }
        }
        
        if (showAllButton != null)
        {
            Component buttonInteractable = showAllButton.GetComponent("StatefulInteractable");
            if (buttonInteractable != null)
            {
                UnsubscribeFromMeshButtonClickEvent(buttonInteractable);
            }
        }
    }
    
    void UnsubscribeFromMeshButtonClickEvent(Component interactable)
    {
        Type interactableType = interactable.GetType();
        string[] possibleEventNames = { "OnClicked", "onClicked", "Clicked", "clicked" };
        
        foreach (string eventName in possibleEventNames)
        {
            // Note: Unity events don't have easy removal of specific listeners
            // The listeners will be cleaned up when the object is destroyed
            FieldInfo fieldInfo = interactableType.GetField(eventName);
            if (fieldInfo != null)
            {
                var eventValue = fieldInfo.GetValue(interactable) as UnityEngine.Events.UnityEvent;
                if (eventValue != null)
                {
                    // Can't easily remove specific listeners, they'll be cleaned up on destroy
                    return;
                }
            }
            
            PropertyInfo propertyInfo = interactableType.GetProperty(eventName);
            if (propertyInfo != null)
            {
                var eventValue = propertyInfo.GetValue(interactable) as UnityEngine.Events.UnityEvent;
                if (eventValue != null)
                {
                    // Can't easily remove specific listeners, they'll be cleaned up on destroy
                    return;
                }
            }
        }
    }
    
    void UnsubscribeFromAllMeshObjects()
    {
        foreach (GameObject obj in meshSubscribedObjects)
        {
            if (obj != null)
            {
                Component interactable = obj.GetComponent("StatefulInteractable");
                if (interactable != null)
                {
                    UnsubscribeFromMeshObjectClickEvent(interactable);
                }
            }
        }
        meshSubscribedObjects.Clear();
    }
    
    void UnsubscribeFromMeshObjectClickEvent(Component interactable)
    {
        Type interactableType = interactable.GetType();
        string[] possibleEventNames = { "OnClicked", "onClicked", "Clicked", "clicked" };
        
        foreach (string eventName in possibleEventNames)
        {
            // Note: We can't easily remove specific listeners, so we'll leave them
            // The listeners will naturally be cleaned up when the object is destroyed
            FieldInfo fieldInfo = interactableType.GetField(eventName);
            if (fieldInfo != null)
            {
                var eventValue = fieldInfo.GetValue(interactable) as UnityEvent;
                if (eventValue != null)
                {
                    return;
                }
            }
            
            PropertyInfo propertyInfo = interactableType.GetProperty(eventName);
            if (propertyInfo != null)
            {
                var eventValue = propertyInfo.GetValue(interactable) as UnityEvent;
                if (eventValue != null)
                {
                    return;
                }
            }
        }
    }
    
    // Public method to manually subscribe to a new object if needed
    public void SubscribeToNewMeshObject(GameObject obj)
    {
        if (obj.GetComponent<RevitData>() == null) return;
        if (meshSubscribedObjects.Contains(obj)) return;
        
        Component interactable = obj.GetComponent("StatefulInteractable");
        if (interactable != null)
        {
            bool subscribed = TrySubscribeToMeshObjectClickEvent(interactable, obj);
            if (subscribed)
            {
                meshSubscribedObjects.Add(obj);
                if (debugMode)
                    Debug.Log($"MeshIsolationHandler: Manually subscribed to {obj.name}");
            }
        }
    }
    
    // Public method to restore a specific object
    public void RestoreMeshObject(GameObject obj)
    {
        if (meshOriginalStates.ContainsKey(obj))
        {
            OriginalMeshRenderState originalState = meshOriginalStates[obj];
            
            // Restore original state
            obj.SetActive(originalState.wasActive);
            obj.layer = originalState.layer;
            
            // Remove from tracking lists
            meshHiddenObjects.Remove(obj);
            meshIsolatedObjects.Remove(obj);
            meshOriginalStates.Remove(obj);
            
            // Update visibility for remaining isolated objects
            if (meshIsolatedObjects.Count > 0)
            {
                UpdateAllMeshObjectsVisibility();
            }
            
            UpdateMeshButtonVisualFeedback();
            
            if (debugMode)
                Debug.Log($"MeshIsolationHandler: Restored object {obj.name}");
        }
    }
    
    // Public method to check if an object is hidden
    public bool IsMeshObjectHidden(GameObject obj)
    {
        return meshHiddenObjects.Contains(obj);
    }
    
    // Public method to check if an object is isolated
    public bool IsMeshObjectIsolated(GameObject obj)
    {
        return meshIsolatedObjects.Contains(obj);
    }
    
    // Public method to get current mode as string
    public string GetCurrentMeshModeString()
    {
        return currentMeshMode.ToString();
    }
}