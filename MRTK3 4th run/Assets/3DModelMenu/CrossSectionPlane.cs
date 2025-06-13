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
    
    [Header("Debug")]
    public bool debugMode = true;
    
    // Private fields
    private bool isCrossSectionActive = false;
    private GameObject crossSectionPlane;
    private List<Renderer> affectedRenderers = new List<Renderer>();
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private Dictionary<Renderer, Material[]> clippedMaterials = new Dictionary<Renderer, Material[]>();
    private Dictionary<Renderer, MeshSliceData> originalMeshData = new Dictionary<Renderer, MeshSliceData>();
    
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
        // Set target parent if not assigned
        if (targetParent == null)
        {
            targetParent = this.transform;
        }
        
        // Setup button
        SetupCrossSectionButton();
        
        // Create plane if not assigned
        if (planePrefab == null)
        {
            CreateDefaultPlane();
        }
        
        // Create clipping shader if needed
        if (clippingShader == null)
        {
            CreateClippingShader();
        }
        
        // Find all affected renderers
        FindAffectedRenderers();
        
        if (debugMode)
            Debug.Log($"CrossSectionHandler: Initialized with {affectedRenderers.Count} renderers under {targetParent.name}");
    }
    
    void SetupCrossSectionButton()
    {
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
    
    void CreateDefaultPlane()
    {
        // Create a plane GameObject
        GameObject planeObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
        planeObj.name = "CrossSectionPlane";
        planeObj.transform.localScale = new Vector3(planeSize.x * 0.1f, 1f, planeSize.y * 0.1f);
        
        // Create transparent material
        Material planeMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        planeMaterial.color = planeColor;
        
        // Set to transparent mode
        planeMaterial.SetFloat("_Surface", 1); // Transparent
        planeMaterial.SetFloat("_Blend", 0); // Alpha blend
        planeMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        planeMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        planeMaterial.SetInt("_ZWrite", 0);
        planeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        planeMaterial.EnableKeyword("_ALPHABLEND_ON");
        planeMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        
        planeObj.GetComponent<Renderer>().material = planeMaterial;
        
        // Remove collider since we don't want it to interfere
        Destroy(planeObj.GetComponent<Collider>());
        
        // Add MRTK3 manipulation components
        SetupMRTK3Manipulation(planeObj);
        
        planePrefab = planeObj;
        planeObj.SetActive(false);
    }
    
    void SetupMRTK3Manipulation(GameObject planeObj)
    {
        // Try to add MRTK3 manipulation components using reflection
        // This allows the script to work even if MRTK3 isn't available
        
        // Add ObjectManipulator component
        Type objectManipulatorType = Type.GetType("MixedReality.Toolkit.UX.ObjectManipulator, MixedReality.Toolkit.UX");
        if (objectManipulatorType != null)
        {
            Component manipulator = planeObj.AddComponent(objectManipulatorType);
            
            // Enable manipulation
            var allowedManipulationsField = objectManipulatorType.GetField("allowedManipulations");
            if (allowedManipulationsField != null)
            {
                // Set to allow move and rotate (value 3 = Move | Rotate)
                allowedManipulationsField.SetValue(manipulator, 3);
            }
            
            if (debugMode)
                Debug.Log("CrossSectionHandler: Added ObjectManipulator to plane");
        }
        else if (debugMode)
        {
            Debug.LogWarning("CrossSectionHandler: Could not find MRTK3 ObjectManipulator. Please add manipulation component manually.");
        }
        
        // Add NearInteractionGrabbable component
        Type nearInteractionGrabbableType = Type.GetType("MixedReality.Toolkit.UX.NearInteractionGrabbable, MixedReality.Toolkit.UX");
        if (nearInteractionGrabbableType != null)
        {
            planeObj.AddComponent(nearInteractionGrabbableType);
            if (debugMode)
                Debug.Log("CrossSectionHandler: Added NearInteractionGrabbable to plane");
        }
        
        // Add StatefulInteractable component
        Type statefulInteractableType = Type.GetType("MixedReality.Toolkit.UX.StatefulInteractable, MixedReality.Toolkit.UX");
        if (statefulInteractableType != null)
        {
            planeObj.AddComponent(statefulInteractableType);
            if (debugMode)
                Debug.Log("CrossSectionHandler: Added StatefulInteractable to plane");
        }
    }
    
    void CreateClippingShader()
    {
        // Try to find an existing clipping shader first
        clippingShader = Shader.Find("Custom/ClippingPlane");
        
        if (clippingShader == null)
        {
            // Try to find URP Lit shader as fallback
            clippingShader = Shader.Find("Universal Render Pipeline/Lit");
            
            if (clippingShader == null)
            {
                // Final fallback to standard shader
                clippingShader = Shader.Find("Standard");
            }
            
            if (debugMode)
                Debug.LogWarning("CrossSectionHandler: Custom clipping shader not found. Using fallback shader. For best results, create a custom shader with clipping plane support.");
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
        
        // Find all renderers under the target parent
        Renderer[] renderers = targetParent.GetComponentsInChildren<Renderer>();
        
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null && renderer.gameObject != crossSectionPlane)
            {
                affectedRenderers.Add(renderer);
                originalMaterials[renderer] = renderer.materials;
                
                // Store mesh data for slicing
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
        
        UpdateButtonVisualFeedback();
        
        if (debugMode)
            Debug.Log($"CrossSectionHandler: Cross-section is now {(isCrossSectionActive ? "ACTIVE" : "INACTIVE")}");
    }
    
    void EnableCrossSection()
    {
        // Create cross-section plane instance
        if (crossSectionPlane == null)
        {
            crossSectionPlane = Instantiate(planePrefab);
            crossSectionPlane.name = "CrossSectionPlane_Instance";
            
            // Position it based on spawn settings
            Vector3 spawnPosition = GetSpawnPosition();
            crossSectionPlane.transform.position = spawnPosition;
            crossSectionPlane.transform.rotation = GetSpawnRotation();
        }
        
        crossSectionPlane.SetActive(true);
        
        // Create clipped materials for all affected renderers
        CreateClippedMaterials();
        
        // Apply clipped materials
        ApplyClippedMaterials();
    }
    
    void DisableCrossSection()
    {
        // Hide the plane
        if (crossSectionPlane != null)
        {
            crossSectionPlane.SetActive(false);
        }
        
        // Restore original meshes and materials
        RestoreOriginalMeshes();
        RestoreOriginalMaterials();
        
        // Make sure all renderers are enabled
        foreach (Renderer renderer in affectedRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }
        
        // Clear global shader properties
        Shader.SetGlobalFloat("_ClippingEnabled", 0f);
    }
    
    void RestoreOriginalMeshes()
    {
        foreach (var kvp in originalMeshData)
        {
            Renderer renderer = kvp.Key;
            MeshSliceData meshData = kvp.Value;
            
            if (meshData.wasSliced && meshData.meshFilter != null && meshData.originalMesh != null)
            {
                // Destroy the sliced mesh to prevent memory leaks
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
    
    Vector3 GetSpawnPosition()
    {
        if (useRelativeToPanel && spawnNearPanel != null)
        {
            // Use panel position as base and add initial position as offset
            Vector3 panelPosition = spawnNearPanel.position;
            Vector3 panelForward = spawnNearPanel.forward;
            Vector3 basePosition = panelPosition + panelForward * spawnDistance;
            return basePosition + initialPosition;
        }
        else if (initialPosition != Vector3.zero)
        {
            // Use absolute initial position
            return initialPosition;
        }
        else if (spawnNearPanel != null)
        {
            // Fallback to panel with spawn distance
            Vector3 panelPosition = spawnNearPanel.position;
            Vector3 panelForward = spawnNearPanel.forward;
            return panelPosition + panelForward * spawnDistance;
        }
        else
        {
            // Final fallback: use center of target bounds
            Bounds targetBounds = GetTargetBounds();
            return targetBounds.center;
        }
    }
    
    Quaternion GetSpawnRotation()
    {
        // Always use the initial rotation from inspector
        if (initialRotation != Vector3.zero)
        {
            return Quaternion.Euler(initialRotation);
        }
        else if (spawnNearPanel != null)
        {
            // Fallback: orient the plane to face the same direction as the panel
            return spawnNearPanel.rotation;
        }
        else
        {
            // Default orientation
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
                    // Create a material with clipping support
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
        // For now, let's use a simpler approach - just copy the original material
        // and we'll handle clipping through renderer culling
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
        // Update clipping plane if cross-section is active
        if (isCrossSectionActive && crossSectionPlane != null && crossSectionPlane.activeInHierarchy)
        {
            UpdateClippingPlane();
        }
    }
    
    void UpdateClippingPlane()
    {
        // Get plane position and normal
        Vector3 planePosition = crossSectionPlane.transform.position;
        Vector3 planeNormal = hidePositiveSide ? crossSectionPlane.transform.up : -crossSectionPlane.transform.up;
        
        // Perform mesh slicing for true cross-sectioning
        UpdateMeshSlicing(planePosition, planeNormal);
    }
    
    void UpdateMeshSlicing(Vector3 planePosition, Vector3 planeNormal)
    {
        foreach (Renderer renderer in affectedRenderers)
        {
            if (renderer == null || !originalMeshData.ContainsKey(renderer)) continue;
            
            MeshSliceData meshData = originalMeshData[renderer];
            if (meshData.meshFilter == null || meshData.originalMesh == null) continue;
            
            // Slice the mesh at the plane
            Mesh slicedMesh = SliceMesh(meshData.originalMesh, planePosition, planeNormal, renderer.transform);
            
            if (slicedMesh != null)
            {
                // Apply the sliced mesh
                meshData.meshFilter.mesh = slicedMesh;
                meshData.wasSliced = true;
                
                if (debugMode)
                    Debug.Log($"CrossSectionHandler: Sliced mesh for {renderer.gameObject.name}");
            }
            else
            {
                // If slicing failed or mesh is completely on one side, hide/show the renderer
                Vector3 rendererCenter = renderer.bounds.center;
                float distanceToPlane = Vector3.Dot(rendererCenter - planePosition, planeNormal);
                bool shouldBeVisible = hidePositiveSide ? (distanceToPlane < 0) : (distanceToPlane > 0);
                renderer.enabled = shouldBeVisible;
            }
        }
    }
    
    Mesh SliceMesh(Mesh originalMesh, Vector3 planePosition, Vector3 planeNormal, Transform objectTransform)
    {
        if (originalMesh == null) return null;
        
        // Check if mesh is readable
        if (!originalMesh.isReadable)
        {
            if (debugMode)
                Debug.LogWarning($"CrossSectionHandler: Mesh '{originalMesh.name}' is not readable. Enable 'Read/Write' in import settings for mesh slicing, or it will fall back to object-level hiding.");
            return null;
        }
        
        try
        {
            // Transform plane to local space
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
            
            // Process each triangle
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
                
                // Calculate distances from plane
                float d1 = Vector3.Dot(v1 - localPlanePosition, localPlaneNormal);
                float d2 = Vector3.Dot(v2 - localPlanePosition, localPlaneNormal);
                float d3 = Vector3.Dot(v3 - localPlanePosition, localPlaneNormal);
                
                // Determine which side of the plane each vertex is on
                bool keep1 = hidePositiveSide ? (d1 < 0) : (d1 > 0);
                bool keep2 = hidePositiveSide ? (d2 < 0) : (d2 > 0);
                bool keep3 = hidePositiveSide ? (d3 < 0) : (d3 > 0);
                
                int keepCount = (keep1 ? 1 : 0) + (keep2 ? 1 : 0) + (keep3 ? 1 : 0);
                
                if (keepCount == 3)
                {
                    // Keep entire triangle
                    AddTriangle(newVertices, newTriangles, newNormals, newUV, 
                               v1, v2, v3, n1, n2, n3, uv1, uv2, uv3);
                }
                else if (keepCount == 2)
                {
                    // Clip triangle - keep 2 vertices, create quad
                    ClipTriangleKeep2(newVertices, newTriangles, newNormals, newUV,
                                     v1, v2, v3, n1, n2, n3, uv1, uv2, uv3,
                                     keep1, keep2, keep3, d1, d2, d3,
                                     localPlanePosition, localPlaneNormal);
                }
                else if (keepCount == 1)
                {
                    // Clip triangle - keep 1 vertex, create smaller triangle
                    ClipTriangleKeep1(newVertices, newTriangles, newNormals, newUV,
                                     v1, v2, v3, n1, n2, n3, uv1, uv2, uv3,
                                     keep1, keep2, keep3, d1, d2, d3,
                                     localPlanePosition, localPlaneNormal);
                }
                // If keepCount == 0, discard triangle entirely
            }
            
            if (newVertices.Count == 0) return null;
            
            // Create new mesh
            Mesh slicedMesh = new Mesh();
            slicedMesh.name = originalMesh.name + "_Sliced";
            slicedMesh.vertices = newVertices.ToArray();
            slicedMesh.triangles = newTriangles.ToArray();
            slicedMesh.normals = newNormals.ToArray();
            if (newUV.Count > 0) slicedMesh.uv = newUV.ToArray();
            
            slicedMesh.RecalculateBounds();
            if (newNormals.Count == 0) slicedMesh.RecalculateNormals();
            
            return slicedMesh;
        }
        catch (System.Exception e)
        {
            if (debugMode)
                Debug.LogError($"CrossSectionHandler: Error slicing mesh '{originalMesh.name}': {e.Message}");
            return null;
        }
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
    
    void ClipTriangleKeep2(List<Vector3> vertices, List<int> triangles, List<Vector3> normals, List<Vector2> uv,
                          Vector3 v1, Vector3 v2, Vector3 v3, Vector3 n1, Vector3 n2, Vector3 n3,
                          Vector2 uv1, Vector2 uv2, Vector2 uv3,
                          bool keep1, bool keep2, bool keep3, float d1, float d2, float d3,
                          Vector3 planePos, Vector3 planeNormal)
    {
        // Find which vertex to discard and create intersection points
        Vector3[] keepVerts = new Vector3[2];
        Vector3[] keepNormals = new Vector3[2];
        Vector2[] keepUVs = new Vector2[2];
        Vector3 discardVert;
        Vector3 discardNormal;
        Vector2 discardUV;
        float discardDist;
        float[] keepDists = new float[2];
        
        int keepIndex = 0;
        if (keep1)
        {
            keepVerts[keepIndex] = v1;
            keepNormals[keepIndex] = n1;
            keepUVs[keepIndex] = uv1;
            keepDists[keepIndex] = d1;
            keepIndex++;
        }
        if (keep2)
        {
            keepVerts[keepIndex] = v2;
            keepNormals[keepIndex] = n2;
            keepUVs[keepIndex] = uv2;
            keepDists[keepIndex] = d2;
            keepIndex++;
        }
        if (keep3)
        {
            keepVerts[keepIndex] = v3;
            keepNormals[keepIndex] = n3;
            keepUVs[keepIndex] = uv3;
            keepDists[keepIndex] = d3;
        }
        
        // Set discard vertex
        if (!keep1) { discardVert = v1; discardNormal = n1; discardUV = uv1; discardDist = d1; }
        else if (!keep2) { discardVert = v2; discardNormal = n2; discardUV = uv2; discardDist = d2; }
        else { discardVert = v3; discardNormal = n3; discardUV = uv3; discardDist = d3; }
        
        // Create intersection points
        Vector3 intersect1 = GetIntersectionPoint(keepVerts[0], discardVert, keepDists[0], discardDist);
        Vector3 intersect2 = GetIntersectionPoint(keepVerts[1], discardVert, keepDists[1], discardDist);
        
        Vector3 intersectNormal1 = Vector3.Lerp(keepNormals[0], discardNormal, Mathf.Abs(keepDists[0]) / (Mathf.Abs(keepDists[0]) + Mathf.Abs(discardDist)));
        Vector3 intersectNormal2 = Vector3.Lerp(keepNormals[1], discardNormal, Mathf.Abs(keepDists[1]) / (Mathf.Abs(keepDists[1]) + Mathf.Abs(discardDist)));
        
        Vector2 intersectUV1 = Vector2.Lerp(keepUVs[0], discardUV, Mathf.Abs(keepDists[0]) / (Mathf.Abs(keepDists[0]) + Mathf.Abs(discardDist)));
        Vector2 intersectUV2 = Vector2.Lerp(keepUVs[1], discardUV, Mathf.Abs(keepDists[1]) / (Mathf.Abs(keepDists[1]) + Mathf.Abs(discardDist)));
        
        // Create quad from the clipped triangle
        AddTriangle(vertices, triangles, normals, uv,
                   keepVerts[0], keepVerts[1], intersect1,
                   keepNormals[0], keepNormals[1], intersectNormal1,
                   keepUVs[0], keepUVs[1], intersectUV1);
        
        AddTriangle(vertices, triangles, normals, uv,
                   keepVerts[1], intersect2, intersect1,
                   keepNormals[1], intersectNormal2, intersectNormal1,
                   keepUVs[1], intersectUV2, intersectUV1);
    }
    
    void ClipTriangleKeep1(List<Vector3> vertices, List<int> triangles, List<Vector3> normals, List<Vector2> uv,
                          Vector3 v1, Vector3 v2, Vector3 v3, Vector3 n1, Vector3 n2, Vector3 n3,
                          Vector2 uv1, Vector2 uv2, Vector2 uv3,
                          bool keep1, bool keep2, bool keep3, float d1, float d2, float d3,
                          Vector3 planePos, Vector3 planeNormal)
    {
        Vector3 keepVert, discard1, discard2;
        Vector3 keepNormal, discardNormal1, discardNormal2;
        Vector2 keepUV, discardUV1, discardUV2;
        float keepDist, discardDist1, discardDist2;
        
        if (keep1)
        {
            keepVert = v1; keepNormal = n1; keepUV = uv1; keepDist = d1;
            discard1 = v2; discardNormal1 = n2; discardUV1 = uv2; discardDist1 = d2;
            discard2 = v3; discardNormal2 = n3; discardUV2 = uv3; discardDist2 = d3;
        }
        else if (keep2)
        {
            keepVert = v2; keepNormal = n2; keepUV = uv2; keepDist = d2;
            discard1 = v1; discardNormal1 = n1; discardUV1 = uv1; discardDist1 = d1;
            discard2 = v3; discardNormal2 = n3; discardUV2 = uv3; discardDist2 = d3;
        }
        else
        {
            keepVert = v3; keepNormal = n3; keepUV = uv3; keepDist = d3;
            discard1 = v1; discardNormal1 = n1; discardUV1 = uv1; discardDist1 = d1;
            discard2 = v2; discardNormal2 = n2; discardUV2 = uv2; discardDist2 = d2;
        }
        
        // Create intersection points
        Vector3 intersect1 = GetIntersectionPoint(keepVert, discard1, keepDist, discardDist1);
        Vector3 intersect2 = GetIntersectionPoint(keepVert, discard2, keepDist, discardDist2);
        
        Vector3 intersectNormal1 = Vector3.Lerp(keepNormal, discardNormal1, Mathf.Abs(keepDist) / (Mathf.Abs(keepDist) + Mathf.Abs(discardDist1)));
        Vector3 intersectNormal2 = Vector3.Lerp(keepNormal, discardNormal2, Mathf.Abs(keepDist) / (Mathf.Abs(keepDist) + Mathf.Abs(discardDist2)));
        
        Vector2 intersectUV1 = Vector2.Lerp(keepUV, discardUV1, Mathf.Abs(keepDist) / (Mathf.Abs(keepDist) + Mathf.Abs(discardDist1)));
        Vector2 intersectUV2 = Vector2.Lerp(keepUV, discardUV2, Mathf.Abs(keepDist) / (Mathf.Abs(keepDist) + Mathf.Abs(discardDist2)));
        
        // Create smaller triangle
        AddTriangle(vertices, triangles, normals, uv,
                   keepVert, intersect1, intersect2,
                   keepNormal, intersectNormal1, intersectNormal2,
                   keepUV, intersectUV1, intersectUV2);
    }
    
    Vector3 GetIntersectionPoint(Vector3 v1, Vector3 v2, float d1, float d2)
    {
        float t = Mathf.Abs(d1) / (Mathf.Abs(d1) + Mathf.Abs(d2));
        return Vector3.Lerp(v1, v2, t);
    }
    
    // Manual control methods (for programmatic use only)
    public void MovePlane(float distance)
    {
        if (crossSectionPlane != null && isCrossSectionActive)
        {
            Vector3 moveDirection = crossSectionPlane.transform.forward;
            crossSectionPlane.transform.position += moveDirection * distance;
            
            if (debugMode)
                Debug.Log($"CrossSectionHandler: Moved plane by {distance} units");
        }
    }
    
    public void RotatePlane(Vector3 axis, float degrees)
    {
        if (crossSectionPlane != null && isCrossSectionActive)
        {
            crossSectionPlane.transform.Rotate(axis, degrees, Space.World);
            
            if (debugMode)
                Debug.Log($"CrossSectionHandler: Rotated plane {degrees}° around {axis}");
        }
    }
    
    // Public methods
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
        }
    }
    
    public void SetPlaneRotation(Quaternion rotation)
    {
        if (crossSectionPlane != null)
        {
            crossSectionPlane.transform.rotation = rotation;
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
    
    void OnValidate()
    {
        // Ensure plane size is positive
        planeSize.x = Mathf.Max(0.1f, planeSize.x);
        planeSize.y = Mathf.Max(0.1f, planeSize.y);
        
        // Ensure alpha is in valid range
        planeColor.a = Mathf.Clamp01(planeColor.a);
        cutSurfaceColor.a = Mathf.Clamp01(cutSurfaceColor.a);
        
        // Ensure positive spawn distance
        spawnDistance = Mathf.Max(0.1f, spawnDistance);
    }
    
    void OnDestroy()
    {
        // Clean up
        RestoreOriginalMeshes();
        RestoreOriginalMaterials();
        
        // Clear global shader properties
        Shader.SetGlobalFloat("_ClippingEnabled", 0f);
        
        // Destroy clipped materials
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
        
        // Destroy plane instance
        if (crossSectionPlane != null && crossSectionPlane != planePrefab)
        {
            DestroyImmediate(crossSectionPlane);
        }
        
        // Remove button listener
        if (toggleCrossSectionButton != null)
        {
            Component buttonInteractable = toggleCrossSectionButton.GetComponent("StatefulInteractable");
            if (buttonInteractable != null)
            {
                // Note: Can't easily remove specific listeners, they'll be cleaned up on destroy
            }
        }
    }
    
    // Method to refresh renderers if hierarchy changes
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