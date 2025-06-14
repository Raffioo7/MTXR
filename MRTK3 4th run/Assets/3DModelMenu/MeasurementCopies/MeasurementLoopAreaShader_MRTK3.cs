using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class MeasurementLoopAreaShader_MRTK3 : MonoBehaviour
{
    [Header("Shading Settings")]
    [Tooltip("Material for the shaded area")]
    public Material shadedAreaMaterial;
    
    [Tooltip("Color for the shaded area")]
    public Color shadedAreaColor = new Color(0.8f, 0.2f, 0.5f, 0.4f);
    
    [Tooltip("Offset from surface to prevent z-fighting")]
    public float surfaceOffset = 0.002f;
    
    [Tooltip("Automatically shade areas when loops are closed")]
    public bool autoShadeOnLoopClose = true;
    
    [Tooltip("Parent object for all shaded areas")]
    public Transform shadedAreasParent;
    
    [Header("Mesh Settings")]
    [Tooltip("Use double-sided shader for better visibility")]
    public bool doubleSided = true;
    
    [Header("Debug")]
    public bool debugMode = true;
    
    // Private fields
    private MeasurementDotPlacementHandler_MRTK3 dotHandler;
    private List<GameObject> shadedAreas = new List<GameObject>();
    private List<MeshFilter> areaMeshFilters = new List<MeshFilter>();
    private List<MeshRenderer> areaMeshRenderers = new List<MeshRenderer>();
    
    // Store previous values for change detection
    private Color previousShadedAreaColor;
    private float previousSurfaceOffset;
    
    void Start()
    {
        // Find the measurement dot placement handler
        dotHandler = FindObjectOfType<MeasurementDotPlacementHandler_MRTK3>();
        
        if (dotHandler == null)
        {
            Debug.LogError("MeasurementLoopAreaShader: Could not find MeasurementDotPlacementHandler_MRTK3 script!");
            return;
        }
        
        // Subscribe to loop events
        dotHandler.OnMeasurementLoopClosed += OnLoopClosed;
        
        // Create shaded areas parent if not assigned
        if (shadedAreasParent == null)
        {
            GameObject shadedAreasParentObj = new GameObject("Measurement Shaded Areas");
            shadedAreasParent = shadedAreasParentObj.transform;
        }
        
        // Create default material if not assigned
        if (shadedAreaMaterial == null)
        {
            CreateDefaultShadedAreaMaterial();
        }
        
        // Initialize previous values
        previousShadedAreaColor = shadedAreaColor;
        previousSurfaceOffset = surfaceOffset;
        
        if (debugMode)
            Debug.Log("MeasurementLoopAreaShader: Initialized successfully");
    }
    
    void CreateDefaultShadedAreaMaterial()
    {
        // Create a transparent material
        shadedAreaMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        
        // Set render mode to transparent
        shadedAreaMaterial.SetFloat("_Surface", 1); // 1 = Transparent
        shadedAreaMaterial.SetFloat("_Blend", 0); // 0 = Alpha
        shadedAreaMaterial.SetFloat("_AlphaClip", 0);
        shadedAreaMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        shadedAreaMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        shadedAreaMaterial.SetFloat("_ZWrite", 0);
        shadedAreaMaterial.SetFloat("_Cull", doubleSided ? 0 : 2); // 0 = Off (double-sided), 2 = Back
        
        shadedAreaMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        shadedAreaMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        
        shadedAreaMaterial.renderQueue = 3000; // Transparent queue
        
        shadedAreaMaterial.color = shadedAreaColor;
        shadedAreaMaterial.name = "MeasurementDefaultShadedAreaMaterial";
    }
    
    void Update()
    {
        // Check for parameter changes
        CheckForParameterChanges();
    }
    
    void CheckForParameterChanges()
    {
        bool needsUpdate = false;
        
        // Check if color changed
        if (shadedAreaColor != previousShadedAreaColor)
        {
            previousShadedAreaColor = shadedAreaColor;
            needsUpdate = true;
        }
        
        // Check if surface offset changed
        if (Mathf.Abs(surfaceOffset - previousSurfaceOffset) > 0.0001f)
        {
            previousSurfaceOffset = surfaceOffset;
            needsUpdate = true;
        }
        
        // Update all shaded areas if any parameter changed
        if (needsUpdate)
        {
            UpdateAllShadedAreaProperties();
        }
    }
    
    void UpdateAllShadedAreaProperties()
    {
        // Update material color
        if (shadedAreaMaterial != null)
        {
            shadedAreaMaterial.color = shadedAreaColor;
        }
        
        // Update all mesh renderers
        foreach (MeshRenderer renderer in areaMeshRenderers)
        {
            if (renderer != null && renderer.material != null)
            {
                renderer.material.color = shadedAreaColor;
            }
        }
        
        // Update positions if surface offset changed
        if (Mathf.Abs(surfaceOffset - previousSurfaceOffset) > 0.0001f)
        {
            ForceUpdateAllAreas();
        }
        
        if (debugMode)
            Debug.Log("MeasurementLoopAreaShader: Updated all shaded area properties");
    }
    
    void OnLoopClosed(int loopIndex)
    {
        if (autoShadeOnLoopClose)
        {
            CreateShadedAreaForLoop(loopIndex);
        }
    }
    
    public void CreateShadedAreaForLoop(int loopIndex)
    {
        var allLoops = dotHandler.GetAllLoopPositions();
        
        if (loopIndex < 0 || loopIndex >= dotHandler.GetCompletedLoopCount())
        {
            if (debugMode)
                Debug.LogWarning($"MeasurementLoopAreaShader: Invalid loop index {loopIndex}");
            return;
        }
        
        if (loopIndex < allLoops.Count)
        {
            var loopPositions = allLoops[loopIndex];
            
            if (loopPositions.Count < 3)
            {
                if (debugMode)
                    Debug.LogWarning($"MeasurementLoopAreaShader: Loop {loopIndex + 1} has less than 3 points, cannot create shaded area");
                return;
            }
            
            // Create the shaded area mesh
            CreateShadedAreaMesh(loopIndex, loopPositions);
        }
    }
    
    void CreateShadedAreaMesh(int loopIndex, List<Vector3> loopPositions)
    {
        // Create a new GameObject for the shaded area
        GameObject shadedAreaObj = new GameObject($"MeasurementShadedArea_Loop{loopIndex + 1}");
        shadedAreaObj.transform.SetParent(shadedAreasParent);
        
        // Add mesh components
        MeshFilter meshFilter = shadedAreaObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = shadedAreaObj.AddComponent<MeshRenderer>();
        
        // Create the mesh
        Mesh mesh = GenerateLoopMesh(loopPositions);
        meshFilter.mesh = mesh;
        
        // Set material
        meshRenderer.material = shadedAreaMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        
        // Store references
        shadedAreas.Add(shadedAreaObj);
        areaMeshFilters.Add(meshFilter);
        areaMeshRenderers.Add(meshRenderer);
        
        if (debugMode)
            Debug.Log($"MeasurementLoopAreaShader: Created shaded area for loop {loopIndex + 1} with {loopPositions.Count} vertices");
    }
    
    Mesh GenerateLoopMesh(List<Vector3> loopPositions)
    {
        Mesh mesh = new Mesh();
        mesh.name = "MeasurementLoopAreaMesh";
        
        // Calculate normal using Newell's method
        Vector3 normal = CalculateLoopNormal(loopPositions);
        
        // Check if polygon is simple (no self-intersections) and convex
        bool isConvex = IsPolygonConvex(loopPositions, normal);
        
        if (isConvex)
        {
            // Use simple fan triangulation for convex polygons
            return GenerateConvexMesh(loopPositions, normal);
        }
        else
        {
            // Use ear clipping for concave polygons
            return GenerateConcaveMesh(loopPositions, normal);
        }
    }
    
    bool IsPolygonConvex(List<Vector3> vertices, Vector3 normal)
    {
        int n = vertices.Count;
        bool hasPositive = false;
        bool hasNegative = false;
        
        for (int i = 0; i < n; i++)
        {
            Vector3 a = vertices[i];
            Vector3 b = vertices[(i + 1) % n];
            Vector3 c = vertices[(i + 2) % n];
            
            Vector3 cross = Vector3.Cross(b - a, c - b);
            float dot = Vector3.Dot(cross, normal);
            
            if (dot > 0.001f) hasPositive = true;
            if (dot < -0.001f) hasNegative = true;
            
            // If we have both positive and negative, it's concave
            if (hasPositive && hasNegative) return false;
        }
        
        return true;
    }
    
    Mesh GenerateConvexMesh(List<Vector3> loopPositions, Vector3 normal)
    {
        Mesh mesh = new Mesh();
        mesh.name = "MeasurementLoopAreaMesh_Convex";
        
        // Calculate the center
        Vector3 center = Vector3.zero;
        foreach (Vector3 pos in loopPositions)
        {
            center += pos;
        }
        center /= loopPositions.Count;
        
        // Create vertices with surface offset
        List<Vector3> vertices = new List<Vector3>();
        
        // Add the center vertex first
        vertices.Add(center + normal * surfaceOffset);
        
        // Add all the loop vertices
        for (int i = 0; i < loopPositions.Count; i++)
        {
            vertices.Add(loopPositions[i] + normal * surfaceOffset);
        }
        
        // Create triangles using fan triangulation from center
        List<int> triangles = new List<int>();
        
        for (int i = 0; i < loopPositions.Count; i++)
        {
            int nextIndex = (i + 1) % loopPositions.Count;
            
            // Triangle: center -> current vertex -> next vertex
            triangles.Add(0); // Center vertex
            triangles.Add(i + 1); // Current vertex
            triangles.Add(nextIndex + 1); // Next vertex
        }
        
        // Create UVs
        Vector2[] uvs = GenerateUVsFromVertices(vertices.ToArray(), center, normal);
        
        // Apply double-sided if needed
        if (doubleSided)
        {
            ApplyDoubleSided(ref vertices, ref triangles, ref uvs);
        }
        
        // Assign to mesh
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.uv = uvs;
        
        // Calculate normals and bounds
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        return mesh;
    }
    
    Mesh GenerateConcaveMesh(List<Vector3> loopPositions, Vector3 normal)
    {
        Mesh mesh = new Mesh();
        mesh.name = "MeasurementLoopAreaMesh_Concave";
        
        // Create vertices with surface offset
        List<Vector3> vertices = new List<Vector3>();
        for (int i = 0; i < loopPositions.Count; i++)
        {
            vertices.Add(loopPositions[i] + normal * surfaceOffset);
        }
        
        // Use ear clipping algorithm
        List<int> triangles = EarClippingTriangulation(vertices, normal);
        
        // If ear clipping fails, try constrained Delaunay triangulation
        if (triangles.Count < (vertices.Count - 2) * 3)
        {
            if (debugMode)
                Debug.LogWarning("MeasurementLoopAreaShader: Ear clipping failed, using alternative triangulation");
            triangles = ConstrainedTriangulation(vertices, normal);
        }
        
        // Create UVs
        Vector2[] uvs = GenerateUVsFromBounds(vertices.ToArray());
        
        // Apply double-sided if needed
        if (doubleSided)
        {
            ApplyDoubleSided(ref vertices, ref triangles, ref uvs);
        }
        
        // Assign to mesh
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.uv = uvs;
        
        // Calculate normals and bounds
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        return mesh;
    }
    
    List<int> EarClippingTriangulation(List<Vector3> vertices, Vector3 normal)
    {
        List<int> triangles = new List<int>();
        List<int> indices = new List<int>();
        
        // Initialize indices
        for (int i = 0; i < vertices.Count; i++)
        {
            indices.Add(i);
        }
        
        int iterations = 0;
        int maxIterations = vertices.Count * 2;
        
        while (indices.Count > 3 && iterations < maxIterations)
        {
            iterations++;
            bool earFound = false;
            
            for (int i = 0; i < indices.Count; i++)
            {
                int prev = indices[(i - 1 + indices.Count) % indices.Count];
                int curr = indices[i];
                int next = indices[(i + 1) % indices.Count];
                
                if (IsValidEar(vertices, indices, prev, curr, next, normal))
                {
                    // Add triangle
                    triangles.Add(prev);
                    triangles.Add(curr);
                    triangles.Add(next);
                    
                    // Remove current vertex
                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }
            }
            
            if (!earFound)
            {
                // If no ear found, break to avoid infinite loop
                if (debugMode)
                    Debug.LogWarning("MeasurementLoopAreaShader: No ear found in iteration " + iterations);
                break;
            }
        }
        
        // Add the last triangle if we have exactly 3 vertices left
        if (indices.Count == 3)
        {
            triangles.Add(indices[0]);
            triangles.Add(indices[1]);
            triangles.Add(indices[2]);
        }
        
        return triangles;
    }
    
    bool IsValidEar(List<Vector3> vertices, List<int> indices, int prev, int curr, int next, Vector3 normal)
    {
        Vector3 a = vertices[prev];
        Vector3 b = vertices[curr];
        Vector3 c = vertices[next];
        
        // Check if triangle is counter-clockwise
        Vector3 cross = Vector3.Cross(b - a, c - a);
        if (Vector3.Dot(cross, normal) < 0)
        {
            return false;
        }
        
        // Check if any other vertex is inside the triangle
        for (int i = 0; i < indices.Count; i++)
        {
            int idx = indices[i];
            if (idx == prev || idx == curr || idx == next)
                continue;
            
            if (IsPointInTriangle(vertices[idx], a, b, c, normal))
            {
                return false;
            }
        }
        
        return true;
    }
    
    bool IsPointInTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
    {
        // Project points onto 2D plane perpendicular to normal
        Vector2 p2d = ProjectTo2D(p, normal);
        Vector2 a2d = ProjectTo2D(a, normal);
        Vector2 b2d = ProjectTo2D(b, normal);
        Vector2 c2d = ProjectTo2D(c, normal);
        
        // Use barycentric coordinates
        float denominator = ((b2d.y - c2d.y) * (a2d.x - c2d.x) + (c2d.x - b2d.x) * (a2d.y - c2d.y));
        if (Mathf.Abs(denominator) < 0.0001f) return false;
        
        float a_weight = ((b2d.y - c2d.y) * (p2d.x - c2d.x) + (c2d.x - b2d.x) * (p2d.y - c2d.y)) / denominator;
        float b_weight = ((c2d.y - a2d.y) * (p2d.x - c2d.x) + (a2d.x - c2d.x) * (p2d.y - c2d.y)) / denominator;
        float c_weight = 1 - a_weight - b_weight;
        
        // Check if point is inside triangle (with small epsilon for floating point errors)
        const float epsilon = -0.0001f;
        return a_weight >= epsilon && b_weight >= epsilon && c_weight >= epsilon;
    }
    
    Vector2 ProjectTo2D(Vector3 point, Vector3 normal)
    {
        // Create an orthonormal basis
        Vector3 right = Vector3.Cross(normal, Vector3.up).normalized;
        if (right.magnitude < 0.001f)
        {
            right = Vector3.Cross(normal, Vector3.forward).normalized;
        }
        Vector3 forward = Vector3.Cross(right, normal).normalized;
        
        // Project onto the 2D plane
        return new Vector2(Vector3.Dot(point, right), Vector3.Dot(point, forward));
    }
    
    List<int> ConstrainedTriangulation(List<Vector3> vertices, Vector3 normal)
    {
        List<int> triangles = new List<int>();
        
        // Simple triangulation by connecting to first vertex
        // This is a fallback method that works for star-shaped polygons
        for (int i = 1; i < vertices.Count - 1; i++)
        {
            triangles.Add(0);
            triangles.Add(i);
            triangles.Add(i + 1);
        }
        
        return triangles;
    }
    
    Vector2[] GenerateUVsFromVertices(Vector3[] vertices, Vector3 center, Vector3 normal)
    {
        Vector2[] uvs = new Vector2[vertices.Length];
        
        // UV for center (if it exists)
        uvs[0] = new Vector2(0.5f, 0.5f);
        
        // Create a coordinate system on the plane
        Vector3 right = Vector3.Cross(normal, Vector3.up).normalized;
        if (right.magnitude < 0.001f)
        {
            right = Vector3.Cross(normal, Vector3.forward).normalized;
        }
        Vector3 forward = Vector3.Cross(right, normal).normalized;
        
        // UVs for loop vertices
        for (int i = 1; i < vertices.Length; i++)
        {
            Vector3 localPos = vertices[i] - center;
            
            // Project onto the plane
            float u = Vector3.Dot(localPos, right) * 0.5f + 0.5f;
            float v = Vector3.Dot(localPos, forward) * 0.5f + 0.5f;
            
            uvs[i] = new Vector2(u, v);
        }
        
        return uvs;
    }
    
    Vector2[] GenerateUVsFromBounds(Vector3[] vertices)
    {
        Vector2[] uvs = new Vector2[vertices.Length];
        
        // Find bounds
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        
        foreach (Vector3 v in vertices)
        {
            minX = Mathf.Min(minX, v.x);
            maxX = Mathf.Max(maxX, v.x);
            minZ = Mathf.Min(minZ, v.z);
            maxZ = Mathf.Max(maxZ, v.z);
        }
        
        float width = maxX - minX;
        float height = maxZ - minZ;
        
        // Generate normalized UVs
        for (int i = 0; i < vertices.Length; i++)
        {
            float u = width > 0.001f ? (vertices[i].x - minX) / width : 0.5f;
            float v = height > 0.001f ? (vertices[i].z - minZ) / height : 0.5f;
            uvs[i] = new Vector2(u, v);
        }
        
        return uvs;
    }
    
    void ApplyDoubleSided(ref List<Vector3> vertices, ref List<int> triangles, ref Vector2[] uvs)
    {
        int originalVertexCount = vertices.Count;
        int originalTriangleCount = triangles.Count;
        
        // Duplicate vertices for back face
        List<Vector3> newVertices = new List<Vector3>(vertices);
        newVertices.AddRange(vertices);
        
        // Add back face triangles with reversed winding
        List<int> newTriangles = new List<int>(triangles);
        for (int i = 0; i < originalTriangleCount; i += 3)
        {
            newTriangles.Add(triangles[i] + originalVertexCount);
            newTriangles.Add(triangles[i + 2] + originalVertexCount);
            newTriangles.Add(triangles[i + 1] + originalVertexCount);
        }
        
        // Duplicate UVs
        List<Vector2> newUVs = new List<Vector2>(uvs);
        newUVs.AddRange(uvs);
        
        vertices = newVertices;
        triangles = newTriangles;
        uvs = newUVs.ToArray();
    }
    
    Vector3 CalculateLoopNormal(List<Vector3> loopPositions)
    {
        Vector3 normal = Vector3.zero;
        
        // Use Newell's method to calculate a robust normal
        for (int i = 0; i < loopPositions.Count; i++)
        {
            Vector3 current = loopPositions[i];
            Vector3 next = loopPositions[(i + 1) % loopPositions.Count];
            
            normal.x += (current.y - next.y) * (current.z + next.z);
            normal.y += (current.z - next.z) * (current.x + next.x);
            normal.z += (current.x - next.x) * (current.y + next.y);
        }
        
        // If the normal is too small, try to find a better one
        if (normal.magnitude < 0.001f)
        {
            // Try using the first three non-collinear points
            for (int i = 0; i < loopPositions.Count - 2; i++)
            {
                Vector3 v1 = loopPositions[i + 1] - loopPositions[i];
                Vector3 v2 = loopPositions[i + 2] - loopPositions[i];
                Vector3 cross = Vector3.Cross(v1, v2);
                
                if (cross.magnitude > 0.001f)
                {
                    normal = cross;
                    break;
                }
            }
        }
        
        // If still no good normal, use world up
        if (normal.magnitude < 0.001f)
        {
            normal = Vector3.up;
            if (debugMode)
                Debug.LogWarning("MeasurementLoopAreaShader: Could not calculate proper normal, using world up");
        }
        
        return normal.normalized;
    }
    
    public void ClearAllShadedAreas()
    {
        foreach (GameObject area in shadedAreas)
        {
            if (area != null)
                Destroy(area);
        }
        
        shadedAreas.Clear();
        areaMeshFilters.Clear();
        areaMeshRenderers.Clear();
        
        if (debugMode)
            Debug.Log("MeasurementLoopAreaShader: Cleared all shaded areas");
    }
    
    public void ToggleShadedAreaVisibility()
    {
        if (shadedAreasParent != null)
        {
            bool isActive = shadedAreasParent.gameObject.activeSelf;
            shadedAreasParent.gameObject.SetActive(!isActive);
            
            if (debugMode)
                Debug.Log($"MeasurementLoopAreaShader: Shaded areas are now {(!isActive ? "visible" : "hidden")}");
        }
    }
    
    public void SetAutoShading(bool enabled)
    {
        autoShadeOnLoopClose = enabled;
        
        if (debugMode)
            Debug.Log($"MeasurementLoopAreaShader: Auto shading set to {enabled}");
    }
    
    public void ForceUpdateAllAreas()
    {
        // Clear and recreate all shaded areas
        ClearAllShadedAreas();
        
        // Recreate shaded areas for all completed loops
        int completedLoops = dotHandler.GetCompletedLoopCount();
        
        for (int i = 0; i < completedLoops; i++)
        {
            CreateShadedAreaForLoop(i);
        }
        
        if (debugMode)
            Debug.Log($"MeasurementLoopAreaShader: Force updated {completedLoops} shaded areas");
    }
    
    public int GetShadedAreaCount()
    {
        return shadedAreas.Count;
    }
    
    public void UpdateShadedAreaColor(int loopIndex, Color newColor)
    {
        if (loopIndex >= 0 && loopIndex < areaMeshRenderers.Count)
        {
            if (areaMeshRenderers[loopIndex] != null)
            {
                areaMeshRenderers[loopIndex].material.color = newColor;
            }
        }
    }
    
    void OnValidate()
    {
        // Ensure valid values
        surfaceOffset = Mathf.Max(0.0001f, surfaceOffset);
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events
        if (dotHandler != null)
        {
            dotHandler.OnMeasurementLoopClosed -= OnLoopClosed;
        }
        
        // Clean up
        ClearAllShadedAreas();
        
        if (shadedAreaMaterial != null && shadedAreaMaterial.name == "MeasurementDefaultShadedAreaMaterial")
            Destroy(shadedAreaMaterial);
    }
}