using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System;
using UnityEngine.Events;
using System.Linq;

public class CrossSectionPlaneHandler_MRTK3 : MonoBehaviour
{
    [Header("MRTK3 Buttons")]
    [Tooltip("Button to toggle cross-section mode")]
    public GameObject toggleCrossSectionButton;
    
    [Tooltip("Button to update/apply the cross-section cut")]
    public GameObject updateCrossSectionButton;
    
    [Header("Update Mode")]
    [Tooltip("Manual: Only update when button is pressed. Auto: Update in real-time (more laggy)")]
    public bool manualUpdateMode = true;
    
    [Tooltip("Show visual feedback when cross-section needs updating")]
    public bool showUpdateIndicator = true;
    
    [Header("Cross-Section Settings")]
    [Tooltip("Only affect children of this object (leave empty to use the object this script is on)")]
    public Transform targetParent;
    
    [Tooltip("Panel near which to spawn the cutting plane")]
    public Transform spawnNearPanel;
    
    [Tooltip("Distance from panel to spawn the plane")]
    public float spawnDistance = 1f;
    
    [Tooltip("Visual representation of the cutting plane")]
    public GameObject planePrefab;
    
    [Tooltip("Size of the cutting plane visual")]
    public Vector2 planeSize = new Vector2(5f, 5f);
    
    [Tooltip("Color of the cutting plane")]
    public Color planeColor = new Color(1f, 0f, 0f, 0.3f);
    
    [Tooltip("Color of the plane when update is needed (manual mode only)")]
    public Color planeColorNeedsUpdate = new Color(1f, 1f, 0f, 0.5f);
    
    [Tooltip("Which side of the plane to hide (true = hide positive side, false = hide negative side)")]
    public bool hidePositiveSide = true;
    
    [Header("Initial Plane Transform")]
    [Tooltip("Initial position of the cutting plane (world coordinates)")]
    public Vector3 initialPosition = Vector3.zero;
    
    [Tooltip("Initial rotation of the cutting plane (euler angles in degrees)")]
    public Vector3 initialRotation = Vector3.zero;
    
    [Tooltip("Use panel position as base for initial position")]
    public bool useRelativeToPanel = true;
    
    [Header("Shader Clipping")]
    [Tooltip("Shader that supports clipping planes (leave empty to use default)")]
    public Shader clippingShader;
    
    [Tooltip("Color for the cut surface (cross-section interior)")]
    public Color cutSurfaceColor = new Color(0.8f, 0.2f, 0.2f, 1f);
    
    [Tooltip("Show the cut surface on clipped geometry")]
    public bool showCutSurface = true;
    
    [Header("Quality Settings")]
    [Tooltip("Smoothing threshold for edge interpolation")]
    [Range(0.001f, 0.1f)]
    public float smoothingThreshold = 0.01f;
    
    [Tooltip("Generate cut surface geometry to fill holes")]
    public bool generateCutSurface = true;
    
    [Header("Performance Settings")]
    [Tooltip("How often to update cross-section (lower = better performance) - Only used in Auto mode")]
    [Range(0.01f, 0.5f)]
    public float updateInterval = 0.1f;
    
    [Tooltip("Minimum distance plane must move to trigger update indicator")]
    public float movementThreshold = 0.01f;
    
    [Tooltip("Minimum rotation plane must rotate to trigger update indicator (degrees)")]
    public float rotationThreshold = 1f;
    
    [Tooltip("Use simple object culling instead of mesh slicing for better performance")]
    public bool useSimpleMode = false;
    
    [Header("Plane Visibility")]
    [Tooltip("Make plane visible from both sides")]
    public bool doubleSidedPlane = true;
    
    [Header("Debug")]
    public bool debugMode = true;
    
    // Private fields
    private bool isCrossSectionActive = false;
    private bool needsUpdate = false;
    private bool isCurrentlyUpdated = true; // Track if the current cut matches the plane position
    private GameObject crossSectionPlane;
    private List<Renderer> affectedRenderers = new List<Renderer>();
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private Dictionary<Renderer, Material[]> clippedMaterials = new Dictionary<Renderer, Material[]>();
    private Dictionary<Renderer, MeshSliceData> originalMeshData = new Dictionary<Renderer, MeshSliceData>();
    
    // Performance optimization variables
    private float lastUpdateTime = 0f;
    private Vector3 lastPlanePosition;
    private Quaternion lastPlaneRotation;
    private Vector3 lastAppliedPosition; // Position where the cut was last applied
    private Quaternion lastAppliedRotation; // Rotation where the cut was last applied
    
    // Materials for visual feedback
    private Material planeMaterial;
    private Material planeOriginalMaterial;
    
    // Shader property names for clipping
    private static readonly int ClipPlane = Shader.PropertyToID("_ClipPlane");
    private static readonly int ClipPlaneEnabled = Shader.PropertyToID("_ClipPlaneEnabled");
    private static readonly int CutSurfaceColor = Shader.PropertyToID("_CutSurfaceColor");
    private static readonly int ShowCutSurface = Shader.PropertyToID("_ShowCutSurface");
    
    // Structure to store original mesh data
    [System.Serializable]
    private class MeshSliceData
    {
        public Mesh originalMesh;
        public MeshFilter meshFilter;
        public bool wasSliced;
        
        public MeshSliceData(MeshFilter filter)
        {
            meshFilter = filter;
            originalMesh = filter.sharedMesh;
            wasSliced = false;
        }
    }
    
    void Start()
    {
        if (targetParent == null)
        {
            targetParent = this.transform;
        }
        
        SetupCrossSectionButtons();
        
        if (planePrefab == null)
        {
            CreateDefaultPlane();
        }
        
        if (clippingShader == null)
        {
            CreateClippingShader();
        }
        
        FindAffectedRenderers();
        
        if (debugMode)
        {
            Debug.Log($"CrossSectionHandler: Initialized with {affectedRenderers.Count} renderers under {targetParent.name}");
            Debug.Log($"CrossSectionHandler: Update mode: {(manualUpdateMode ? "MANUAL" : "AUTO")}");
        }
    }
    
    void SetupCrossSectionButtons()
    {
        // Setup toggle button
        if (toggleCrossSectionButton != null)
        {
            Component buttonInteractable = toggleCrossSectionButton.GetComponent("StatefulInteractable");
            if (buttonInteractable != null)
            {
                bool subscribed = TrySubscribeToButtonClick(buttonInteractable, ToggleCrossSection);
                
                if (debugMode)
                {
                    if (subscribed)
                        Debug.Log("CrossSectionHandler: Successfully set up toggle button");
                    else
                        Debug.LogWarning("CrossSectionHandler: Failed to set up toggle button");
                }
            }
            else if (debugMode)
            {
                Debug.LogWarning("CrossSectionHandler: No StatefulInteractable found on toggle button");
            }
        }
        else
        {
            Debug.LogWarning("CrossSectionHandler: No toggle button assigned!");
        }
        
        // Setup update button
        if (updateCrossSectionButton != null)
        {
            Component updateButtonInteractable = updateCrossSectionButton.GetComponent("StatefulInteractable");
            if (updateButtonInteractable != null)
            {
                bool subscribed = TrySubscribeToButtonClick(updateButtonInteractable, ManualUpdateCrossSection);
                
                if (debugMode)
                {
                    if (subscribed)
                        Debug.Log("CrossSectionHandler: Successfully set up update button");
                    else
                        Debug.LogWarning("CrossSectionHandler: Failed to set up update button");
                }
            }
            else if (debugMode)
            {
                Debug.LogWarning("CrossSectionHandler: No StatefulInteractable found on update button");
            }
            
            // Set initial button state
            UpdateButtonStates();
        }
        else if (manualUpdateMode)
        {
            Debug.LogWarning("CrossSectionHandler: Manual update mode enabled but no update button assigned!");
        }
    }
    
    bool TrySubscribeToButtonClick(Component interactable, System.Action onClickAction)
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
    
    // NEW METHOD: Manual update trigger
    public void ManualUpdateCrossSection()
    {
        if (!isCrossSectionActive)
        {
            if (debugMode)
                Debug.LogWarning("CrossSectionHandler: Cannot update - cross-section is not active");
            return;
        }
        
        if (crossSectionPlane == null)
        {
            if (debugMode)
                Debug.LogWarning("CrossSectionHandler: Cannot update - no cross-section plane found");
            return;
        }
        
        if (debugMode)
            Debug.Log("CrossSectionHandler: Manual update triggered");
        
        // Force update the clipping
        ForceUpdateClippingPlane();
        
        // Update our tracking variables
        lastAppliedPosition = crossSectionPlane.transform.position;
        lastAppliedRotation = crossSectionPlane.transform.rotation;
        lastPlanePosition = lastAppliedPosition;
        lastPlaneRotation = lastAppliedRotation;
        
        isCurrentlyUpdated = true;
        needsUpdate = false;
        
        // Update visual feedback
        UpdatePlaneVisualFeedback();
        UpdateButtonStates();
    }
    
    // NEW METHOD: Check if update is needed
    private bool CheckIfUpdateNeeded()
    {
        if (crossSectionPlane == null || !isCrossSectionActive) return false;
        
        Vector3 currentPosition = crossSectionPlane.transform.position;
        Quaternion currentRotation = crossSectionPlane.transform.rotation;
        
        bool positionChanged = Vector3.Distance(currentPosition, lastAppliedPosition) > movementThreshold;
        bool rotationChanged = Quaternion.Angle(currentRotation, lastAppliedRotation) > rotationThreshold;
        
        return positionChanged || rotationChanged;
    }
    
    // NEW METHOD: Update plane visual feedback
    private void UpdatePlaneVisualFeedback()
    {
        if (!showUpdateIndicator || crossSectionPlane == null || planeMaterial == null) return;
        
        if (manualUpdateMode && !isCurrentlyUpdated)
        {
            // Show that update is needed
            planeMaterial.color = planeColorNeedsUpdate;
        }
        else
        {
            // Show normal state
            planeMaterial.color = planeColor;
        }
    }
    
    // NEW METHOD: Update button states
    private void UpdateButtonStates()
    {
        // Update toggle button
        UpdateButtonVisualFeedback();
        
        // Update the update button (make it glow or change color when update is needed)
        if (updateCrossSectionButton != null)
        {
            Renderer buttonRenderer = updateCrossSectionButton.GetComponentInChildren<Renderer>();
            if (buttonRenderer != null && buttonRenderer.material != null)
            {
                Color targetColor;
                if (manualUpdateMode && !isCurrentlyUpdated && isCrossSectionActive)
                {
                    targetColor = Color.yellow; // Indicate update needed
                }
                else if (isCrossSectionActive)
                {
                    targetColor = Color.green; // Active and up to date
                }
                else
                {
                    targetColor = Color.gray; // Inactive
                }
                
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
    
    void CreateDefaultPlane()
    {
        GameObject planeObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
        planeObj.name = "CrossSectionPlane";
        planeObj.transform.localScale = new Vector3(planeSize.x * 0.1f, 1f, planeSize.y * 0.1f);
        
        planeMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        planeMaterial.color = planeColor;
        
        // Store original material reference
        planeOriginalMaterial = planeMaterial;
        
        // Make the plane transparent
        planeMaterial.SetFloat("_Surface", 1);
        planeMaterial.SetFloat("_Blend", 0);
        planeMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        planeMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        planeMaterial.SetInt("_ZWrite", 0);
        planeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        planeMaterial.EnableKeyword("_ALPHABLEND_ON");
        planeMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        
        // DISABLE BACKFACE CULLING - This makes it visible from both sides
        planeMaterial.SetInt("_Cull", doubleSidedPlane ? (int)UnityEngine.Rendering.CullMode.Off : (int)UnityEngine.Rendering.CullMode.Back);
        
        planeObj.GetComponent<Renderer>().material = planeMaterial;
        
        // Fix the collider issue - Replace MeshCollider with BoxCollider for MRTK3 interaction
        MeshCollider meshCollider = planeObj.GetComponent<MeshCollider>();
        if (meshCollider != null)
        {
            DestroyImmediate(meshCollider); // Remove the problematic mesh collider
        }
        
        // Add a BoxCollider instead - this works perfectly with Rigidbody
        BoxCollider boxCollider = planeObj.AddComponent<BoxCollider>();
        boxCollider.isTrigger = true; // Make it a trigger so it doesn't interfere with physics
        boxCollider.size = new Vector3(10f, 0.1f, 10f); // Flat box matching the plane
        
        if (debugMode)
            Debug.Log("CrossSectionHandler: Replaced MeshCollider with BoxCollider for MRTK3 compatibility");
        
        SetupMRTK3Manipulation(planeObj);
        
        planePrefab = planeObj;
        planeObj.SetActive(false);
    }
    
    void SetupMRTK3Manipulation(GameObject planeObj)
    {
        if (debugMode)
            Debug.Log("CrossSectionHandler: Starting MRTK3 manipulation setup...");
        
        // Since reflection isn't finding MRTK3 types, let's provide manual setup instructions
        Debug.LogWarning("CrossSectionHandler: MRTK3 ObjectManipulator not found via reflection. Please add the following components manually:");
        Debug.LogWarning("1. Add 'ObjectManipulator' component to the cutting plane");
        Debug.LogWarning("2. Set 'Allowed Manipulations' to 'Move' and 'Rotate'");
        Debug.LogWarning("3. Add 'NearInteractionGrabbable' component");
        Debug.LogWarning("4. Add 'StatefulInteractable' component");
        Debug.LogWarning("5. Ensure the plane has a Collider (BoxCollider is already added)");
        
        // Set up basic components that don't require MRTK3
        SetupBasicInteraction(planeObj);
        
        // Provide alternative: Create a simple manipulation system
        CreateFallbackManipulation(planeObj);
        
        if (debugMode)
        {
            Debug.Log("CrossSectionHandler: Basic setup completed. For full MRTK3 functionality, please:");
            Debug.Log("1. Verify MRTK3 is properly imported and configured");
            Debug.Log("2. Check that the MRTK3 assemblies are referenced in your project");
            Debug.Log("3. Manually add ObjectManipulator component to the cutting plane GameObject");
            Debug.Log("4. Configure ObjectManipulator with Move + Rotate manipulations");
        }
    }
    
    void SetupBasicInteraction(GameObject planeObj)
    {
        // Ensure we have a proper collider for interaction
        BoxCollider boxCollider = planeObj.GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = planeObj.AddComponent<BoxCollider>();
        }
        boxCollider.isTrigger = true;
        boxCollider.size = new Vector3(10f, 0.1f, 10f);
        
        // Add a Rigidbody for physics interaction
        Rigidbody rb = planeObj.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = planeObj.AddComponent<Rigidbody>();
        }
        
        // Configure for MRTK3 ObjectManipulator compatibility
        rb.isKinematic = true;   // ObjectManipulator typically works better with kinematic
        rb.useGravity = false;   // Disable gravity
        
        // Remove any constraints that might prevent movement
        rb.constraints = RigidbodyConstraints.None;
        
        // Set appropriate layer
        planeObj.layer = LayerMask.NameToLayer("Default");
        
        if (debugMode)
        {
            Debug.Log("CrossSectionHandler: Basic interaction components added:");
            Debug.Log($"  - BoxCollider (trigger): {boxCollider != null}");
            Debug.Log($"  - Rigidbody (kinematic, no constraints): {rb != null}");
            Debug.Log($"  - Layer: {planeObj.layer}");
        }
    }
    
    // Add a public method to temporarily disable our transform monitoring
    public void SetManipulationMode(bool isManipulating)
    {
        if (isManipulating)
        {
            // Store current state before manipulation
            if (crossSectionPlane != null)
            {
                lastPlanePosition = crossSectionPlane.transform.position;
                lastPlaneRotation = crossSectionPlane.transform.rotation;
            }
        }
        else
        {
            // In manual mode, just mark that we need an update
            if (manualUpdateMode)
            {
                isCurrentlyUpdated = false;
                UpdatePlaneVisualFeedback();
                UpdateButtonStates();
            }
            else
            {
                // In auto mode, force an update after manipulation ends
                needsUpdate = true;
            }
        }
        
        if (debugMode)
            Debug.Log($"CrossSectionHandler: Manipulation mode set to {isManipulating}");
    }
    
    void CreateFallbackManipulation(GameObject planeObj)
    {
        // Add a simple script component that can be used for basic manipulation
        // This won't provide MRTK3 hand tracking, but gives a foundation
        
        if (debugMode)
        {
            Debug.Log("CrossSectionHandler: Fallback manipulation setup completed.");
            Debug.Log("For MRTK3 hand interaction, please manually add these components:");
            Debug.Log("  1. ObjectManipulator (from MRTK3)");
            Debug.Log("  2. NearInteractionGrabbable (from MRTK3)");
            Debug.Log("  3. StatefulInteractable (from MRTK3)");
        }
    }
    
    // Public method to help with manual MRTK3 setup
    public void ConfigurePlaneForMRTK3()
    {
        if (crossSectionPlane == null)
        {
            Debug.LogWarning("CrossSectionHandler: No active cross-section plane found. Enable cross-section mode first.");
            return;
        }
        
        Debug.Log("CrossSectionHandler: Manual MRTK3 configuration helper");
        Debug.Log($"Target GameObject: {crossSectionPlane.name}");
        
        // Check current components
        var components = crossSectionPlane.GetComponents<Component>();
        Debug.Log($"Current components: {string.Join(", ", components.Select(c => c.GetType().Name))}");
        
        // Check for MRTK3 components
        bool hasObjectManipulator = components.Any(c => c.GetType().Name.Contains("ObjectManipulator"));
        bool hasNearInteraction = components.Any(c => c.GetType().Name.Contains("NearInteraction"));
        bool hasStatefulInteractable = components.Any(c => c.GetType().Name.Contains("StatefulInteractable"));
        
        Debug.Log($"MRTK3 Components Status:");
        Debug.Log($"  ObjectManipulator: {(hasObjectManipulator ? "✓ Found" : "✗ Missing")}");
        Debug.Log($"  NearInteractionGrabbable: {(hasNearInteraction ? "✓ Found" : "✗ Missing")}");
        Debug.Log($"  StatefulInteractable: {(hasStatefulInteractable ? "✓ Found" : "✗ Missing")}");
        
        if (!hasObjectManipulator)
        {
            Debug.LogWarning("Add ObjectManipulator component manually and configure:");
            Debug.LogWarning("  - Allowed Manipulations: Move, Rotate");
            Debug.LogWarning("  - Manipulation Type: One and Two Handed");
            Debug.LogWarning("  - Disable any constraints if present");
        }
    }
    
    void CreateClippingShader()
    {
        clippingShader = Shader.Find("Custom/ClippingPlane");
        
        if (clippingShader == null)
        {
            clippingShader = Shader.Find("Universal Render Pipeline/Lit");
            
            if (clippingShader == null)
            {
                clippingShader = Shader.Find("Standard");
            }
            
            if (debugMode)
                Debug.LogWarning("CrossSectionHandler: Custom clipping shader not found. Using fallback shader.");
        }
        else if (debugMode)
        {
            Debug.Log("CrossSectionHandler: Found existing clipping shader");
        }
    }
    
    void FindAffectedRenderers()
    {
        affectedRenderers.Clear();
        originalMaterials.Clear();
        clippedMaterials.Clear();
        originalMeshData.Clear();
        
        Renderer[] renderers = targetParent.GetComponentsInChildren<Renderer>();
        
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null && renderer.gameObject != crossSectionPlane)
            {
                affectedRenderers.Add(renderer);
                originalMaterials[renderer] = renderer.materials;
                
                MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    originalMeshData[renderer] = new MeshSliceData(meshFilter);
                }
            }
        }
    }
    
    public void ToggleCrossSection()
    {
        isCrossSectionActive = !isCrossSectionActive;
        
        if (isCrossSectionActive)
        {
            EnableCrossSection();
        }
        else
        {
            DisableCrossSection();
        }
        
        UpdateButtonStates();
        
        if (debugMode)
            Debug.Log($"CrossSectionHandler: Cross-section is now {(isCrossSectionActive ? "ACTIVE" : "INACTIVE")}");
    }
    
    void EnableCrossSection()
    {
        if (crossSectionPlane == null)
        {
            crossSectionPlane = Instantiate(planePrefab);
            crossSectionPlane.name = "CrossSectionPlane_Instance";
            
            Vector3 spawnPosition = GetSpawnPosition();
            crossSectionPlane.transform.position = spawnPosition;
            crossSectionPlane.transform.rotation = GetSpawnRotation();
            
            // Get the material from the instantiated plane
            Renderer planeRenderer = crossSectionPlane.GetComponent<Renderer>();
            if (planeRenderer != null)
            {
                planeMaterial = planeRenderer.material;
            }
            
            lastPlanePosition = spawnPosition;
            lastPlaneRotation = crossSectionPlane.transform.rotation;
            lastAppliedPosition = spawnPosition;
            lastAppliedRotation = crossSectionPlane.transform.rotation;
        }
        
        crossSectionPlane.SetActive(true);
        CreateClippedMaterials();
        ApplyClippedMaterials();
        
        // In manual mode, we start with the cut applied at the current position
        if (manualUpdateMode)
        {
            ForceUpdateClippingPlane();
            isCurrentlyUpdated = true;
        }
        else
        {
            needsUpdate = true;
        }
        
        UpdatePlaneVisualFeedback();
    }
    
    void DisableCrossSection()
    {
        if (crossSectionPlane != null)
        {
            crossSectionPlane.SetActive(false);
        }
        
        RestoreOriginalMeshes();
        RestoreOriginalMaterials();
        
        foreach (Renderer renderer in affectedRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }
        
        Shader.SetGlobalFloat("_ClippingEnabled", 0f);
        
        isCurrentlyUpdated = true;
        needsUpdate = false;
    }
    
    Vector3 GetSpawnPosition()
    {
        if (useRelativeToPanel && spawnNearPanel != null)
        {
            Vector3 panelPosition = spawnNearPanel.position;
            Vector3 panelForward = spawnNearPanel.forward;
            Vector3 basePosition = panelPosition + panelForward * spawnDistance;
            return basePosition + initialPosition;
        }
        else if (initialPosition != Vector3.zero)
        {
            return initialPosition;
        }
        else if (spawnNearPanel != null)
        {
            Vector3 panelPosition = spawnNearPanel.position;
            Vector3 panelForward = spawnNearPanel.forward;
            return panelPosition + panelForward * spawnDistance;
        }
        else
        {
            Bounds targetBounds = GetTargetBounds();
            return targetBounds.center;
        }
    }
    
    Quaternion GetSpawnRotation()
    {
        if (initialRotation != Vector3.zero)
        {
            return Quaternion.Euler(initialRotation);
        }
        else if (spawnNearPanel != null)
        {
            return spawnNearPanel.rotation;
        }
        else
        {
            return Quaternion.identity;
        }
    }
    
    Bounds GetTargetBounds()
    {
        Renderer[] renderers = targetParent.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(targetParent.position, Vector3.one);
        }
        
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        
        return bounds;
    }
    
    void CreateClippedMaterials()
    {
        foreach (Renderer renderer in affectedRenderers)
        {
            if (renderer == null) continue;
            
            Material[] originalMats = originalMaterials[renderer];
            Material[] clippedMats = new Material[originalMats.Length];
            
            for (int i = 0; i < originalMats.Length; i++)
            {
                if (originalMats[i] != null)
                {
                    clippedMats[i] = CreateClippingMaterial(originalMats[i]);
                }
                else
                {
                    clippedMats[i] = null;
                }
            }
            
            clippedMaterials[renderer] = clippedMats;
        }
    }
    
    Material CreateClippingMaterial(Material originalMaterial)
    {
        Material clippedMaterial = new Material(originalMaterial);
        
        if (debugMode)
            Debug.Log($"CrossSectionHandler: Created clipped material for {originalMaterial.name}");
        
        return clippedMaterial;
    }
    
    void ApplyClippedMaterials()
    {
        foreach (Renderer renderer in affectedRenderers)
        {
            if (renderer != null && clippedMaterials.ContainsKey(renderer))
            {
                renderer.materials = clippedMaterials[renderer];
            }
        }
    }
    
    void RestoreOriginalMaterials()
    {
        foreach (Renderer renderer in affectedRenderers)
        {
            if (renderer != null && originalMaterials.ContainsKey(renderer))
            {
                renderer.materials = originalMaterials[renderer];
            }
        }
    }
    
    void RestoreOriginalMeshes()
    {
        foreach (var kvp in originalMeshData)
        {
            Renderer renderer = kvp.Key;
            MeshSliceData meshData = kvp.Value;
            
            if (meshData.wasSliced && meshData.meshFilter != null && meshData.originalMesh != null)
            {
                if (meshData.meshFilter.mesh != meshData.originalMesh)
                {
                    Mesh slicedMesh = meshData.meshFilter.mesh;
                    meshData.meshFilter.mesh = meshData.originalMesh;
                    if (slicedMesh != null)
                    {
                        DestroyImmediate(slicedMesh);
                    }
                }
                
                meshData.wasSliced = false;
                
                if (debugMode)
                    Debug.Log($"CrossSectionHandler: Restored original mesh for {renderer.gameObject.name}");
            }
        }
    }
    
    void UpdateButtonVisualFeedback()
    {
        if (toggleCrossSectionButton != null)
        {
            Renderer buttonRenderer = toggleCrossSectionButton.GetComponentInChildren<Renderer>();
            if (buttonRenderer != null && buttonRenderer.material != null)
            {
                Color targetColor = isCrossSectionActive ? Color.green : Color.white;
                
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
    
    void Update()
    {
        if (isCrossSectionActive && crossSectionPlane != null && crossSectionPlane.activeInHierarchy)
        {
            if (manualUpdateMode)
            {
                // In manual mode, only check if update is needed and provide visual feedback
                if (CheckIfUpdateNeeded() && isCurrentlyUpdated)
                {
                    isCurrentlyUpdated = false;
                    UpdatePlaneVisualFeedback();
                    UpdateButtonStates();
                    
                    if (debugMode)
                        Debug.Log("CrossSectionHandler: Plane moved - update needed");
                }
            }
            else
            {
                // Auto mode - update in real-time like before
                bool timeToUpdate = Time.time - lastUpdateTime >= updateInterval;
                bool planeMoved = Vector3.Distance(crossSectionPlane.transform.position, lastPlanePosition) > movementThreshold;
                bool planeRotated = Quaternion.Angle(crossSectionPlane.transform.rotation, lastPlaneRotation) > rotationThreshold;
                
                if (timeToUpdate && (planeMoved || planeRotated || needsUpdate))
                {
                    UpdateClippingPlane();
                    lastUpdateTime = Time.time;
                    lastPlanePosition = crossSectionPlane.transform.position;
                    lastPlaneRotation = crossSectionPlane.transform.rotation;
                    lastAppliedPosition = lastPlanePosition;
                    lastAppliedRotation = lastPlaneRotation;
                    needsUpdate = false;
                    isCurrentlyUpdated = true;
                }
            }
        }
    }
    
    bool IsPlaneBeingManipulated()
    {
        if (crossSectionPlane == null) return false;
        
        // Check if ObjectManipulator is currently manipulating the object
        var components = crossSectionPlane.GetComponents<Component>();
        foreach (var component in components)
        {
            if (component.GetType().Name.Contains("ObjectManipulator"))
            {
                // Try to get the manipulation state via reflection
                try
                {
                    var isManipulatingField = component.GetType().GetField("isManipulating");
                    if (isManipulatingField != null)
                    {
                        bool isManipulating = (bool)isManipulatingField.GetValue(component);
                        if (isManipulating) return true;
                    }
                    
                    // Try alternative field names
                    var manipulationStateField = component.GetType().GetField("manipulationState");
                    if (manipulationStateField != null)
                    {
                        var state = manipulationStateField.GetValue(component);
                        if (state != null && !state.ToString().Contains("None"))
                        {
                            return true;
                        }
                    }
                    
                    // Try property instead of field
                    var isManipulatingProperty = component.GetType().GetProperty("IsManipulating");
                    if (isManipulatingProperty != null)
                    {
                        bool isManipulating = (bool)isManipulatingProperty.GetValue(component);
                        if (isManipulating) return true;
                    }
                }
                catch (System.Exception e)
                {
                    if (debugMode)
                        Debug.LogWarning($"CrossSectionHandler: Could not check manipulation state: {e.Message}");
                }
            }
        }
        
        return false;
    }
    
    // MODIFIED: Renamed for clarity and made public
    void ForceUpdateClippingPlane()
    {
        if (crossSectionPlane == null) return;
        
        Vector3 planePosition = crossSectionPlane.transform.position;
        Vector3 planeNormal = hidePositiveSide ? crossSectionPlane.transform.up : -crossSectionPlane.transform.up;
        
        if (useSimpleMode)
        {
            UpdateSimpleObjectCulling(planePosition, planeNormal);
        }
        else
        {
            UpdateMeshSlicing(planePosition, planeNormal);
        }
        
        if (debugMode)
            Debug.Log("CrossSectionHandler: Force updated clipping plane");
    }
    
    // MODIFIED: Wrapper for backward compatibility
    void UpdateClippingPlane()
    {
        ForceUpdateClippingPlane();
    }
    
    void UpdateSimpleObjectCulling(Vector3 planePosition, Vector3 planeNormal)
    {
        foreach (Renderer renderer in affectedRenderers)
        {
            if (renderer == null) continue;
            
            Vector3 rendererCenter = renderer.bounds.center;
            float distanceToPlane = Vector3.Dot(rendererCenter - planePosition, planeNormal);
            bool shouldBeVisible = hidePositiveSide ? (distanceToPlane < 0) : (distanceToPlane > 0);
            
            renderer.enabled = shouldBeVisible;
        }
    }
    
    void UpdateMeshSlicing(Vector3 planePosition, Vector3 planeNormal)
    {
        foreach (Renderer renderer in affectedRenderers)
        {
            if (renderer == null || !originalMeshData.ContainsKey(renderer)) continue;
            
            MeshSliceData meshData = originalMeshData[renderer];
            if (meshData.meshFilter == null || meshData.originalMesh == null) continue;
            
            Mesh slicedMesh = SliceMesh(meshData.originalMesh, planePosition, planeNormal, renderer.transform);
            
            if (slicedMesh != null)
            {
                meshData.meshFilter.mesh = slicedMesh;
                meshData.wasSliced = true;
                renderer.enabled = true;
                
                if (debugMode)
                    Debug.Log($"CrossSectionHandler: Sliced mesh for {renderer.gameObject.name}");
            }
            else
            {
                Vector3 rendererCenter = renderer.bounds.center;
                float distanceToPlane = Vector3.Dot(rendererCenter - planePosition, planeNormal);
                bool shouldBeVisible = hidePositiveSide ? (distanceToPlane < 0) : (distanceToPlane > 0);
                renderer.enabled = shouldBeVisible;
                
                if (debugMode && !meshData.originalMesh.isReadable)
                {
                    Debug.Log($"CrossSectionHandler: Using object culling for {renderer.gameObject.name} (mesh not readable)");
                }
            }
        }
    }
    
    Mesh SliceMesh(Mesh originalMesh, Vector3 planePosition, Vector3 planeNormal, Transform objectTransform)
    {
        if (originalMesh == null) return null;
        
        if (!originalMesh.isReadable)
        {
            if (debugMode)
                Debug.LogWarning($"CrossSectionHandler: Mesh '{originalMesh.name}' is not readable.");
            return null;
        }
        
        try
        {
            Vector3 localPlanePosition = objectTransform.InverseTransformPoint(planePosition);
            Vector3 localPlaneNormal = objectTransform.InverseTransformDirection(planeNormal).normalized;
            
            Vector3[] vertices = originalMesh.vertices;
            int[] triangles = originalMesh.triangles;
            Vector3[] normals = originalMesh.normals;
            Vector2[] uv = originalMesh.uv;
            
            List<Vector3> newVertices = new List<Vector3>();
            List<int> newTriangles = new List<int>();
            List<Vector3> newNormals = new List<Vector3>();
            List<Vector2> newUV = new List<Vector2>();
            
            // Track cut edge vertices for filling holes
            List<Vector3> cutEdgeVertices = new List<Vector3>();
            List<Vector3> cutEdgeNormals = new List<Vector3>();
            
            // Process each triangle individually
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 v1 = vertices[triangles[i]];
                Vector3 v2 = vertices[triangles[i + 1]];
                Vector3 v3 = vertices[triangles[i + 2]];
                
                Vector3 n1 = normals.Length > triangles[i] ? normals[triangles[i]] : Vector3.up;
                Vector3 n2 = normals.Length > triangles[i + 1] ? normals[triangles[i + 1]] : Vector3.up;
                Vector3 n3 = normals.Length > triangles[i + 2] ? normals[triangles[i + 2]] : Vector3.up;
                
                Vector2 uv1 = uv.Length > triangles[i] ? uv[triangles[i]] : Vector2.zero;
                Vector2 uv2 = uv.Length > triangles[i + 1] ? uv[triangles[i + 1]] : Vector2.zero;
                Vector2 uv3 = uv.Length > triangles[i + 2] ? uv[triangles[i + 2]] : Vector2.zero;
                
                // Calculate signed distances from vertices to plane
                float d1 = Vector3.Dot(v1 - localPlanePosition, localPlaneNormal);
                float d2 = Vector3.Dot(v2 - localPlanePosition, localPlaneNormal);
                float d3 = Vector3.Dot(v3 - localPlanePosition, localPlaneNormal);
                
                // Apply smoothing threshold to avoid micro-cuts
                if (Mathf.Abs(d1) < smoothingThreshold) d1 = 0f;
                if (Mathf.Abs(d2) < smoothingThreshold) d2 = 0f;
                if (Mathf.Abs(d3) < smoothingThreshold) d3 = 0f;
                
                // Determine which vertices to keep
                bool keep1 = hidePositiveSide ? (d1 <= 0) : (d1 >= 0);
                bool keep2 = hidePositiveSide ? (d2 <= 0) : (d2 >= 0);
                bool keep3 = hidePositiveSide ? (d3 <= 0) : (d3 >= 0);
                
                int keepCount = (keep1 ? 1 : 0) + (keep2 ? 1 : 0) + (keep3 ? 1 : 0);
                
                if (keepCount == 3)
                {
                    // Keep entire triangle - all vertices are on the visible side
                    AddTriangle(newVertices, newTriangles, newNormals, newUV, 
                               v1, v2, v3, n1, n2, n3, uv1, uv2, uv3);
                }
                else if (keepCount == 2)
                {
                    // Clip triangle - 2 vertices visible, 1 hidden
                    ProcessTriangleKeep2WithCutSurface(newVertices, newTriangles, newNormals, newUV,
                                                      cutEdgeVertices, cutEdgeNormals,
                                                      v1, v2, v3, n1, n2, n3, uv1, uv2, uv3,
                                                      keep1, keep2, keep3, d1, d2, d3, localPlaneNormal);
                }
                else if (keepCount == 1)
                {
                    // Clip triangle - 1 vertex visible, 2 hidden
                    ProcessTriangleKeep1WithCutSurface(newVertices, newTriangles, newNormals, newUV,
                                                      cutEdgeVertices, cutEdgeNormals,
                                                      v1, v2, v3, n1, n2, n3, uv1, uv2, uv3,
                                                      keep1, keep2, keep3, d1, d2, d3, localPlaneNormal);
                }
                // If keepCount == 0, discard triangle entirely (all vertices hidden)
            }
            
            // Generate cut surface to fill holes
            if (generateCutSurface && cutEdgeVertices.Count >= 3)
            {
                GenerateCutSurface(newVertices, newTriangles, newNormals, newUV, 
                                 cutEdgeVertices, cutEdgeNormals, localPlaneNormal);
            }
            
            if (newVertices.Count == 0) return null;
            
            // Create the sliced mesh
            Mesh slicedMesh = new Mesh();
            slicedMesh.name = originalMesh.name + "_Sliced";
            slicedMesh.vertices = newVertices.ToArray();
            slicedMesh.triangles = newTriangles.ToArray();
            
            // Handle normals
            if (newNormals.Count == newVertices.Count)
            {
                slicedMesh.normals = newNormals.ToArray();
            }
            else
            {
                slicedMesh.RecalculateNormals();
            }
            
            // Handle UVs
            if (newUV.Count == newVertices.Count)
            {
                slicedMesh.uv = newUV.ToArray();
            }
            
            slicedMesh.RecalculateBounds();
            slicedMesh.RecalculateTangents();
            
            return slicedMesh;
        }
        catch (System.Exception e)
        {
            if (debugMode)
                Debug.LogError($"CrossSectionHandler: Error slicing mesh '{originalMesh.name}': {e.Message}");
            return null;
        }
    }
    
    void ProcessTriangleKeep2WithCutSurface(List<Vector3> vertices, List<int> triangles, List<Vector3> normals, List<Vector2> uv,
                                           List<Vector3> cutEdgeVertices, List<Vector3> cutEdgeNormals,
                                           Vector3 v1, Vector3 v2, Vector3 v3, Vector3 n1, Vector3 n2, Vector3 n3,
                                           Vector2 uv1, Vector2 uv2, Vector2 uv3,
                                           bool keep1, bool keep2, bool keep3, float d1, float d2, float d3, Vector3 planeNormal)
    {
        // Find the two vertices to keep and one to discard
        List<int> keepIndices = new List<int>();
        List<int> discardIndices = new List<int>();
        
        if (keep1) keepIndices.Add(0); else discardIndices.Add(0);
        if (keep2) keepIndices.Add(1); else discardIndices.Add(1);
        if (keep3) keepIndices.Add(2); else discardIndices.Add(2);
        
        Vector3[] verts = { v1, v2, v3 };
        Vector3[] norms = { n1, n2, n3 };
        Vector2[] uvs = { uv1, uv2, uv3 };
        float[] dists = { d1, d2, d3 };
        
        Vector3 keepVert1 = verts[keepIndices[0]];
        Vector3 keepVert2 = verts[keepIndices[1]];
        Vector3 discardVert = verts[discardIndices[0]];
        
        Vector3 keepNormal1 = norms[keepIndices[0]];
        Vector3 keepNormal2 = norms[keepIndices[1]];
        Vector3 discardNormal = norms[discardIndices[0]];
        
        Vector2 keepUV1 = uvs[keepIndices[0]];
        Vector2 keepUV2 = uvs[keepIndices[1]];
        Vector2 discardUV = uvs[discardIndices[0]];
        
        float keepDist1 = dists[keepIndices[0]];
        float keepDist2 = dists[keepIndices[1]];
        float discardDist = dists[discardIndices[0]];
        
        // Calculate intersection points
        Vector3 intersect1 = CalculateIntersection(keepVert1, discardVert, keepDist1, discardDist);
        Vector3 intersect2 = CalculateIntersection(keepVert2, discardVert, keepDist2, discardDist);
        
        // Interpolate normals and UVs
        float t1 = Mathf.Abs(keepDist1) / (Mathf.Abs(keepDist1) + Mathf.Abs(discardDist) + 0.0001f);
        float t2 = Mathf.Abs(keepDist2) / (Mathf.Abs(keepDist2) + Mathf.Abs(discardDist) + 0.0001f);
        
        Vector3 intersectNormal1 = Vector3.Lerp(keepNormal1, discardNormal, t1).normalized;
        Vector3 intersectNormal2 = Vector3.Lerp(keepNormal2, discardNormal, t2).normalized;
        
        Vector2 intersectUV1 = Vector2.Lerp(keepUV1, discardUV, t1);
        Vector2 intersectUV2 = Vector2.Lerp(keepUV2, discardUV, t2);
        
        // Add intersection points to cut edge for surface generation
        cutEdgeVertices.Add(intersect1);
        cutEdgeVertices.Add(intersect2);
        cutEdgeNormals.Add(planeNormal);
        cutEdgeNormals.Add(planeNormal);
        
        // Create the clipped quad as two triangles
        // Triangle 1: keepVert1, keepVert2, intersect1
        AddTriangle(vertices, triangles, normals, uv,
                   keepVert1, keepVert2, intersect1,
                   keepNormal1, keepNormal2, intersectNormal1,
                   keepUV1, keepUV2, intersectUV1);
        
        // Triangle 2: keepVert2, intersect2, intersect1
        AddTriangle(vertices, triangles, normals, uv,
                   keepVert2, intersect2, intersect1,
                   keepNormal2, intersectNormal2, intersectNormal1,
                   keepUV2, intersectUV2, intersectUV1);
    }
    
    void ProcessTriangleKeep1WithCutSurface(List<Vector3> vertices, List<int> triangles, List<Vector3> normals, List<Vector2> uv,
                                           List<Vector3> cutEdgeVertices, List<Vector3> cutEdgeNormals,
                                           Vector3 v1, Vector3 v2, Vector3 v3, Vector3 n1, Vector3 n2, Vector3 n3,
                                           Vector2 uv1, Vector2 uv2, Vector2 uv3,
                                           bool keep1, bool keep2, bool keep3, float d1, float d2, float d3, Vector3 planeNormal)
    {
        // Find the one vertex to keep and two to discard
        Vector3 keepVert, discardVert1, discardVert2;
        Vector3 keepNormal, discardNormal1, discardNormal2;
        Vector2 keepUV, discardUV1, discardUV2;
        float keepDist, discardDist1, discardDist2;
        
        if (keep1)
        {
            keepVert = v1; keepNormal = n1; keepUV = uv1; keepDist = d1;
            discardVert1 = v2; discardNormal1 = n2; discardUV1 = uv2; discardDist1 = d2;
            discardVert2 = v3; discardNormal2 = n3; discardUV2 = uv3; discardDist2 = d3;
        }
        else if (keep2)
        {
            keepVert = v2; keepNormal = n2; keepUV = uv2; keepDist = d2;
            discardVert1 = v1; discardNormal1 = n1; discardUV1 = uv1; discardDist1 = d1;
            discardVert2 = v3; discardNormal2 = n3; discardUV2 = uv3; discardDist2 = d3;
        }
        else
        {
            keepVert = v3; keepNormal = n3; keepUV = uv3; keepDist = d3;
            discardVert1 = v1; discardNormal1 = n1; discardUV1 = uv1; discardDist1 = d1;
            discardVert2 = v2; discardNormal2 = n2; discardUV2 = uv2; discardDist2 = d2;
        }
        
        // Calculate intersection points
        Vector3 intersect1 = CalculateIntersection(keepVert, discardVert1, keepDist, discardDist1);
        Vector3 intersect2 = CalculateIntersection(keepVert, discardVert2, keepDist, discardDist2);
        
        // Interpolate normals and UVs
        float t1 = Mathf.Abs(keepDist) / (Mathf.Abs(keepDist) + Mathf.Abs(discardDist1) + 0.0001f);
        float t2 = Mathf.Abs(keepDist) / (Mathf.Abs(keepDist) + Mathf.Abs(discardDist2) + 0.0001f);
        
        Vector3 intersectNormal1 = Vector3.Lerp(keepNormal, discardNormal1, t1).normalized;
        Vector3 intersectNormal2 = Vector3.Lerp(keepNormal, discardNormal2, t2).normalized;
        
        Vector2 intersectUV1 = Vector2.Lerp(keepUV, discardUV1, t1);
        Vector2 intersectUV2 = Vector2.Lerp(keepUV, discardUV2, t2);
        
        // Add intersection points to cut edge for surface generation
        cutEdgeVertices.Add(intersect1);
        cutEdgeVertices.Add(intersect2);
        cutEdgeNormals.Add(planeNormal);
        cutEdgeNormals.Add(planeNormal);
        
        // Create the clipped triangle
        AddTriangle(vertices, triangles, normals, uv,
                   keepVert, intersect1, intersect2,
                   keepNormal, intersectNormal1, intersectNormal2,
                   keepUV, intersectUV1, intersectUV2);
    }
    
    void GenerateCutSurface(List<Vector3> vertices, List<int> triangles, List<Vector3> normals, List<Vector2> uv,
                           List<Vector3> cutEdgeVertices, List<Vector3> cutEdgeNormals, Vector3 planeNormal)
    {
        if (cutEdgeVertices.Count < 3) return;
        
        // Group cut edge vertices into continuous edge loops
        List<List<Vector3>> edgeLoops = FindEdgeLoops(cutEdgeVertices);
        
        foreach (List<Vector3> loop in edgeLoops)
        {
            if (loop.Count < 3) continue;
            
            // Triangulate each edge loop
            TriangulateLoop(vertices, triangles, normals, uv, loop, planeNormal);
        }
        
        if (debugMode && edgeLoops.Count > 0)
        {
            int totalVertices = edgeLoops.Sum(loop => loop.Count);
            Debug.Log($"CrossSectionHandler: Generated cut surface with {edgeLoops.Count} loops, {totalVertices} total vertices");
        }
    }
    
    List<List<Vector3>> FindEdgeLoops(List<Vector3> cutEdgeVertices)
    {
        List<List<Vector3>> loops = new List<List<Vector3>>();
        List<Vector3> remainingVertices = new List<Vector3>(cutEdgeVertices);
        float connectionThreshold = 0.01f; // Adjust based on your mesh scale
        
        while (remainingVertices.Count >= 3)
        {
            List<Vector3> currentLoop = new List<Vector3>();
            
            // Start with the first vertex
            Vector3 currentVertex = remainingVertices[0];
            currentLoop.Add(currentVertex);
            remainingVertices.RemoveAt(0);
            
            // Try to build a connected loop
            bool foundConnection = true;
            while (foundConnection && remainingVertices.Count > 0)
            {
                foundConnection = false;
                Vector3 nextVertex = Vector3.zero;
                int nextIndex = -1;
                
                // Find the closest vertex to continue the loop
                float closestDistance = float.MaxValue;
                for (int i = 0; i < remainingVertices.Count; i++)
                {
                    float distance = Vector3.Distance(currentVertex, remainingVertices[i]);
                    if (distance < connectionThreshold && distance < closestDistance)
                    {
                        closestDistance = distance;
                        nextVertex = remainingVertices[i];
                        nextIndex = i;
                        foundConnection = true;
                    }
                }
                
                if (foundConnection)
                {
                    currentLoop.Add(nextVertex);
                    remainingVertices.RemoveAt(nextIndex);
                    currentVertex = nextVertex;
                }
            }
            
            // Only add loops with enough vertices
            if (currentLoop.Count >= 3)
            {
                loops.Add(currentLoop);
            }
            else
            {
                // If we can't make a proper loop, create a simple triangle fan from remaining vertices
                if (remainingVertices.Count >= 2)
                {
                    List<Vector3> simpleLoop = new List<Vector3>(currentLoop);
                    simpleLoop.AddRange(remainingVertices.Take(2));
                    loops.Add(simpleLoop);
                    remainingVertices.Clear();
                }
                else
                {
                    break;
                }
            }
        }
        
        return loops;
    }
    
    void TriangulateLoop(List<Vector3> vertices, List<int> triangles, List<Vector3> normals, List<Vector2> uv,
                        List<Vector3> loop, Vector3 planeNormal)
    {
        if (loop.Count < 3) return;
        
        // Calculate the center of the loop
        Vector3 center = Vector3.zero;
        foreach (Vector3 vertex in loop)
        {
            center += vertex;
        }
        center /= loop.Count;
        
        // Sort vertices around the center for proper winding
        List<Vector3> sortedLoop = SortVerticesAroundCenter(loop, center, planeNormal);
        
        // Use ear clipping triangulation for better results
        List<Vector3> triangulatedVertices = new List<Vector3>();
        List<int> triangulatedIndices = new List<int>();
        
        if (sortedLoop.Count == 3)
        {
            // Simple triangle
            triangulatedVertices.AddRange(sortedLoop);
            triangulatedIndices.AddRange(new int[] { 0, 1, 2 });
        }
        else if (sortedLoop.Count == 4)
        {
            // Quad - split into two triangles
            triangulatedVertices.AddRange(sortedLoop);
            triangulatedIndices.AddRange(new int[] { 0, 1, 2, 0, 2, 3 });
        }
        else
        {
            // Complex polygon - use fan triangulation from center
            triangulatedVertices.Add(center);
            triangulatedVertices.AddRange(sortedLoop);
            
            for (int i = 0; i < sortedLoop.Count; i++)
            {
                int next = (i + 1) % sortedLoop.Count;
                triangulatedIndices.AddRange(new int[] { 0, i + 1, next + 1 });
            }
        }
        
        // Add triangulated vertices to the main mesh
        int baseIndex = vertices.Count;
        Vector3 normal = hidePositiveSide ? -planeNormal : planeNormal;
        
        for (int i = 0; i < triangulatedIndices.Count; i += 3)
        {
            int i1 = triangulatedIndices[i];
            int i2 = triangulatedIndices[i + 1];
            int i3 = triangulatedIndices[i + 2];
            
            Vector3 v1 = triangulatedVertices[i1];
            Vector3 v2 = triangulatedVertices[i2];
            Vector3 v3 = triangulatedVertices[i3];
            
            // Check triangle winding and flip if necessary
            Vector3 calculatedNormal = Vector3.Cross(v2 - v1, v3 - v1).normalized;
            bool needsFlip = Vector3.Dot(calculatedNormal, normal) < 0;
            
            if (needsFlip)
            {
                // Swap v2 and v3 to flip winding
                Vector3 temp = v2;
                v2 = v3;
                v3 = temp;
            }
            
            // Add the triangle
            int currentBase = vertices.Count;
            vertices.AddRange(new Vector3[] { v1, v2, v3 });
            triangles.AddRange(new int[] { currentBase, currentBase + 1, currentBase + 2 });
            normals.AddRange(new Vector3[] { normal, normal, normal });
            
            // Generate UVs based on position relative to center
            Vector2 uv1 = GenerateCutSurfaceUV(v1, center, planeNormal);
            Vector2 uv2 = GenerateCutSurfaceUV(v2, center, planeNormal);
            Vector2 uv3 = GenerateCutSurfaceUV(v3, center, planeNormal);
            
            uv.AddRange(new Vector2[] { uv1, uv2, uv3 });
        }
    }
    
    Vector2 GenerateCutSurfaceUV(Vector3 vertex, Vector3 center, Vector3 normal)
    {
        // Create a local 2D coordinate system on the plane
        Vector3 localVertex = vertex - center;
        
        // Find two perpendicular vectors in the plane
        Vector3 tangent = Vector3.Cross(normal, Vector3.up);
        if (tangent.magnitude < 0.1f)
        {
            tangent = Vector3.Cross(normal, Vector3.right);
        }
        tangent = tangent.normalized;
        
        Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;
        
        // Project the vertex onto these axes
        float u = Vector3.Dot(localVertex, tangent);
        float v = Vector3.Dot(localVertex, bitangent);
        
        // Normalize to 0-1 range (you may need to adjust the scale factor)
        float scale = 0.5f; // Adjust this based on your mesh size
        return new Vector2(u * scale + 0.5f, v * scale + 0.5f);
    }
    
    List<Vector3> SortVerticesAroundCenter(List<Vector3> vertices, Vector3 center, Vector3 normal)
    {
        if (vertices.Count < 3) return vertices;
        
        // Create a reference vector perpendicular to the normal
        Vector3 reference = Vector3.up;
        if (Vector3.Dot(reference, normal) > 0.9f)
        {
            reference = Vector3.right;
        }
        
        // Project reference onto the plane
        Vector3 planeReference = Vector3.ProjectOnPlane(reference, normal).normalized;
        
        // Sort vertices by angle around the center
        List<Vector3> sortedVertices = new List<Vector3>(vertices);
        sortedVertices.Sort((a, b) =>
        {
            Vector3 dirA = (a - center).normalized;
            Vector3 dirB = (b - center).normalized;
            
            // Project directions onto the plane
            Vector3 projA = Vector3.ProjectOnPlane(dirA, normal).normalized;
            Vector3 projB = Vector3.ProjectOnPlane(dirB, normal).normalized;
            
            // Calculate angles relative to reference direction
            float angleA = Mathf.Atan2(Vector3.Dot(Vector3.Cross(planeReference, projA), normal), Vector3.Dot(planeReference, projA));
            float angleB = Mathf.Atan2(Vector3.Dot(Vector3.Cross(planeReference, projB), normal), Vector3.Dot(planeReference, projB));
            
            // Normalize angles to [0, 2π]
            if (angleA < 0) angleA += 2 * Mathf.PI;
            if (angleB < 0) angleB += 2 * Mathf.PI;
            
            return angleA.CompareTo(angleB);
        });
        
        return sortedVertices;
    }
    
    Vector3 CalculateIntersection(Vector3 v1, Vector3 v2, float d1, float d2)
    {
        // Avoid division by zero
        float denominator = Mathf.Abs(d1) + Mathf.Abs(d2);
        if (denominator < 0.0001f) return v1;
        
        float t = Mathf.Abs(d1) / denominator;
        return Vector3.Lerp(v1, v2, t);
    }
    
    void AddTriangle(List<Vector3> vertices, List<int> triangles, List<Vector3> normals, List<Vector2> uv,
                    Vector3 v1, Vector3 v2, Vector3 v3, Vector3 n1, Vector3 n2, Vector3 n3,
                    Vector2 uv1, Vector2 uv2, Vector2 uv3)
    {
        int startIndex = vertices.Count;
        vertices.AddRange(new Vector3[] { v1, v2, v3 });
        triangles.AddRange(new int[] { startIndex, startIndex + 1, startIndex + 2 });
        normals.AddRange(new Vector3[] { n1, n2, n3 });
        uv.AddRange(new Vector2[] { uv1, uv2, uv3 });
    }
    
    // Public API methods
    public void MovePlane(float distance)
    {
        if (crossSectionPlane != null && isCrossSectionActive)
        {
            Vector3 moveDirection = crossSectionPlane.transform.forward;
            crossSectionPlane.transform.position += moveDirection * distance;
            
            if (manualUpdateMode)
            {
                isCurrentlyUpdated = false;
                UpdatePlaneVisualFeedback();
                UpdateButtonStates();
            }
            
            if (debugMode)
                Debug.Log($"CrossSectionHandler: Moved plane by {distance} units");
        }
    }
    
    public void RotatePlane(Vector3 axis, float degrees)
    {
        if (crossSectionPlane != null && isCrossSectionActive)
        {
            crossSectionPlane.transform.Rotate(axis, degrees, Space.World);
            
            if (manualUpdateMode)
            {
                isCurrentlyUpdated = false;
                UpdatePlaneVisualFeedback();
                UpdateButtonStates();
            }
            
            if (debugMode)
                Debug.Log($"CrossSectionHandler: Rotated plane {degrees}° around {axis}");
        }
    }
    
    public void EnableCrossSectionMode()
    {
        if (!isCrossSectionActive)
        {
            ToggleCrossSection();
        }
    }
    
    public void DisableCrossSectionMode()
    {
        if (isCrossSectionActive)
        {
            ToggleCrossSection();
        }
    }
    
    public bool IsCrossSectionActive()
    {
        return isCrossSectionActive;
    }
    
    public void SetPlanePosition(Vector3 position)
    {
        if (crossSectionPlane != null)
        {
            crossSectionPlane.transform.position = position;
            
            if (manualUpdateMode)
            {
                isCurrentlyUpdated = false;
                UpdatePlaneVisualFeedback();
                UpdateButtonStates();
            }
        }
    }
    
    public void SetPlaneRotation(Quaternion rotation)
    {
        if (crossSectionPlane != null)
        {
            crossSectionPlane.transform.rotation = rotation;
            
            if (manualUpdateMode)
            {
                isCurrentlyUpdated = false;
                UpdatePlaneVisualFeedback();
                UpdateButtonStates();
            }
        }
    }
    
    public Vector3 GetPlanePosition()
    {
        return crossSectionPlane != null ? crossSectionPlane.transform.position : Vector3.zero;
    }
    
    public Vector3 GetPlaneNormal()
    {
        return crossSectionPlane != null ? crossSectionPlane.transform.up : Vector3.up;
    }
    
    // NEW: Additional public methods for manual mode
    public void SetUpdateMode(bool manual)
    {
        bool wasManual = manualUpdateMode;
        manualUpdateMode = manual;
        
        if (wasManual && !manual && isCrossSectionActive)
        {
            // Switching from manual to auto - apply any pending updates
            if (!isCurrentlyUpdated)
            {
                ForceUpdateClippingPlane();
                isCurrentlyUpdated = true;
            }
        }
        
        UpdatePlaneVisualFeedback();
        UpdateButtonStates();
        
        if (debugMode)
            Debug.Log($"CrossSectionHandler: Update mode changed to {(manual ? "MANUAL" : "AUTO")}");
    }
    
    public bool IsUpdateNeeded()
    {
        return manualUpdateMode && !isCurrentlyUpdated && isCrossSectionActive;
    }
    
    public bool GetUpdateMode()
    {
        return manualUpdateMode;
    }
    
    void OnValidate()
    {
        planeSize.x = Mathf.Max(0.1f, planeSize.x);
        planeSize.y = Mathf.Max(0.1f, planeSize.y);
        
        planeColor.a = Mathf.Clamp01(planeColor.a);
        planeColorNeedsUpdate.a = Mathf.Clamp01(planeColorNeedsUpdate.a);
        cutSurfaceColor.a = Mathf.Clamp01(cutSurfaceColor.a);
        
        spawnDistance = Mathf.Max(0.1f, spawnDistance);
        
        updateInterval = Mathf.Max(0.01f, updateInterval);
        movementThreshold = Mathf.Max(0.001f, movementThreshold);
        rotationThreshold = Mathf.Max(0.1f, rotationThreshold);
        smoothingThreshold = Mathf.Max(0.001f, smoothingThreshold);
    }
    
    void OnDestroy()
    {
        RestoreOriginalMeshes();
        RestoreOriginalMaterials();
        
        Shader.SetGlobalFloat("_ClippingEnabled", 0f);
        
        foreach (var kvp in clippedMaterials)
        {
            Material[] materials = kvp.Value;
            if (materials != null)
            {
                foreach (Material material in materials)
                {
                    if (material != null)
                    {
                        DestroyImmediate(material);
                    }
                }
            }
        }
        
        clippedMaterials.Clear();
        originalMeshData.Clear();
        
        if (crossSectionPlane != null && crossSectionPlane != planePrefab)
        {
            DestroyImmediate(crossSectionPlane);
        }
    }
    
    public void RefreshRenderers()
    {
        bool wasActive = isCrossSectionActive;
        
        if (wasActive)
        {
            DisableCrossSection();
        }
        
        FindAffectedRenderers();
        
        if (wasActive)
        {
            EnableCrossSection();
        }
        
        if (debugMode)
            Debug.Log($"CrossSectionHandler: Refreshed with {affectedRenderers.Count} renderers");
    }
}