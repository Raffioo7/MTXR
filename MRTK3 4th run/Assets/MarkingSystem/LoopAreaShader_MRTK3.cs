using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class LoopAreaShader_MRTK3 : MonoBehaviour
{
    [Header("Shading Settings")]
    [Tooltip("Material for the shaded area")]
    public Material shadedAreaMaterial;
    
    [Tooltip("Color for the shaded area")]
    public Color shadedAreaColor = new Color(0.2f, 0.5f, 0.8f, 0.5f);
    
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
    private DotPlacementHandler_MRTK3 dotHandler;
    private List<GameObject> shadedAreas = new List<GameObject>();
    private List<MeshFilter> areaMeshFilters = new List<MeshFilter>();
    private List<MeshRenderer> areaMeshRenderers = new List<MeshRenderer>();
    
    // Store previous values for change detection
    private Color previousShadedAreaColor;
    private float previousSurfaceOffset;
    
    void Start()
    {
        // Find the dot placement handler
        dotHandler = FindObjectOfType<DotPlacementHandler_MRTK3>();
        
        if (dotHandler == null)
        {
            Debug.LogError("LoopAreaShader: Could not find DotPlacementHandler_MRTK3 script!");
            return;
        }
        
        // Subscribe to loop events
        dotHandler.OnLoopClosed += OnLoopClosed;
        
        // Create shaded areas parent if not assigned
        if (shadedAreasParent == null)
        {
            GameObject shadedAreasParentObj = new GameObject("Shaded Areas");
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
            Debug.Log("LoopAreaShader: Initialized successfully");
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
        shadedAreaMaterial.name = "DefaultShadedAreaMaterial";
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
            Debug.Log("LoopAreaShader: Updated all shaded area properties");
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
                Debug.LogWarning($"LoopAreaShader: Invalid loop index {loopIndex}");
            return;
        }
        
        if (loopIndex < allLoops.Count)
        {
            var loopPositions = allLoops[loopIndex];
            
            if (loopPositions.Count < 3)
            {
                if (debugMode)
                    Debug.LogWarning($"LoopAreaShader: Loop {loopIndex + 1} has less than 3 points, cannot create shaded area");
                return;
            }
            
            // Create the shaded area mesh
            CreateShadedAreaMesh(loopIndex, loopPositions);
        }
    }
    
    void CreateShadedAreaMesh(int loopIndex, List<Vector3> loopPositions)
    {
        // Create a new GameObject for the shaded area
        GameObject shadedAreaObj = new GameObject($"ShadedArea_Loop{loopIndex + 1}");
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
            Debug.Log($"LoopAreaShader: Created shaded area for loop {loopIndex + 1} with {loopPositions.Count} vertices");
    }
    
    Mesh GenerateLoopMesh(List<Vector3> loopPositions)
    {
        Mesh mesh = new Mesh();
        mesh.name = "LoopAreaMesh";
        
        // Calculate the center and normal of the loop
        Vector3 center = Vector3.zero;
        foreach (Vector3 pos in loopPositions)
        {
            center += pos;
        }
        center /= loopPositions.Count;
        
        // Calculate normal using Newell's method
        Vector3 normal = CalculateLoopNormal(loopPositions);
        
        // Create vertices with surface offset
        List<Vector3> vertices = new List<Vector3>();
        
        // Add the center vertex first (for fan triangulation)
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
            triangles.Add(i + 1); // Current vertex (offset by 1 because center is at 0)
            triangles.Add(nextIndex + 1); // Next vertex
        }
        
        // Create UVs
        Vector2[] uvs = new Vector2[vertices.Count];
        
        // UV for center
        uvs[0] = new Vector2(0.5f, 0.5f);
        
        // UVs for loop vertices
        for (int i = 1; i < vertices.Count; i++)
        {
            Vector3 localPos = vertices[i] - center;
            
            // Create a coordinate system on the plane
            Vector3 right = Vector3.Cross(normal, Vector3.up).normalized;
            if (right.magnitude < 0.001f)
            {
                right = Vector3.Cross(normal, Vector3.forward).normalized;
            }
            Vector3 forward = Vector3.Cross(right, normal).normalized;
            
            // Project onto the plane
            float u = Vector3.Dot(localPos, right) * 0.5f + 0.5f;
            float v = Vector3.Dot(localPos, forward) * 0.5f + 0.5f;
            
            uvs[i] = new Vector2(u, v);
        }
        
        // If double-sided, create both front and back faces
        if (doubleSided)
        {
            int originalVertexCount = vertices.Count;
            int originalTriangleCount = triangles.Count;
            
            // Duplicate vertices for back face
            for (int i = 0; i < originalVertexCount; i++)
            {
                vertices.Add(vertices[i]);
            }
            
            // Add back face triangles with reversed winding
            for (int i = 0; i < originalTriangleCount; i += 3)
            {
                triangles.Add(triangles[i] + originalVertexCount);
                triangles.Add(triangles[i + 2] + originalVertexCount);
                triangles.Add(triangles[i + 1] + originalVertexCount);
            }
            
            // Duplicate UVs
            List<Vector2> uvList = new List<Vector2>(uvs);
            uvList.AddRange(uvs);
            uvs = uvList.ToArray();
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
                Debug.LogWarning("LoopAreaShader: Could not calculate proper normal, using world up");
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
            Debug.Log("LoopAreaShader: Cleared all shaded areas");
    }
    
    public void ToggleShadedAreaVisibility()
    {
        if (shadedAreasParent != null)
        {
            bool isActive = shadedAreasParent.gameObject.activeSelf;
            shadedAreasParent.gameObject.SetActive(!isActive);
            
            if (debugMode)
                Debug.Log($"LoopAreaShader: Shaded areas are now {(!isActive ? "visible" : "hidden")}");
        }
    }
    
    public void SetAutoShading(bool enabled)
    {
        autoShadeOnLoopClose = enabled;
        
        if (debugMode)
            Debug.Log($"LoopAreaShader: Auto shading set to {enabled}");
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
            Debug.Log($"LoopAreaShader: Force updated {completedLoops} shaded areas");
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
    
    // Alternative mesh generation method using ear clipping (can be toggled)
    public Mesh GenerateLoopMeshEarClipping(List<Vector3> loopPositions)
    {
        Mesh mesh = new Mesh();
        mesh.name = "LoopAreaMesh_EarClip";
        
        // Calculate normal
        Vector3 normal = CalculateLoopNormal(loopPositions);
        
        // Create vertices with surface offset
        Vector3[] vertices = new Vector3[loopPositions.Count];
        for (int i = 0; i < loopPositions.Count; i++)
        {
            vertices[i] = loopPositions[i] + normal * surfaceOffset;
        }
        
        // Triangulate using ear clipping
        List<int> triangles = TriangulatePolygon(vertices, normal);
        
        // Create UVs
        Vector2[] uvs = GenerateUVs(vertices);
        
        // Assign to mesh
        mesh.vertices = vertices;
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs;
        
        // Calculate normals and bounds
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        return mesh;
    }
    
    List<int> TriangulatePolygon(Vector3[] vertices, Vector3 normal)
    {
        List<int> triangles = new List<int>();
        List<int> indices = Enumerable.Range(0, vertices.Length).ToList();
        
        int attempts = 0;
        int maxAttempts = vertices.Length * vertices.Length;
        
        while (indices.Count > 3 && attempts < maxAttempts)
        {
            attempts++;
            bool earFound = false;
            
            for (int i = 0; i < indices.Count; i++)
            {
                int prev = (i - 1 + indices.Count) % indices.Count;
                int next = (i + 1) % indices.Count;
                
                if (IsEar(vertices, indices, prev, i, next, normal))
                {
                    // Add triangle
                    triangles.Add(indices[prev]);
                    triangles.Add(indices[i]);
                    triangles.Add(indices[next]);
                    
                    // Remove current vertex
                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }
            }
            
            if (!earFound)
            {
                if (debugMode)
                    Debug.LogWarning("LoopAreaShader: No ear found, using fan triangulation fallback");
                
                // Fallback to fan triangulation
                triangles.Clear();
                for (int i = 1; i < vertices.Length - 1; i++)
                {
                    triangles.Add(0);
                    triangles.Add(i);
                    triangles.Add(i + 1);
                }
                break;
            }
        }
        
        // Add the last triangle
        if (indices.Count == 3)
        {
            triangles.Add(indices[0]);
            triangles.Add(indices[1]);
            triangles.Add(indices[2]);
        }
        
        return triangles;
    }
    
    bool IsEar(Vector3[] vertices, List<int> indices, int prevIdx, int currIdx, int nextIdx, Vector3 normal)
    {
        Vector3 a = vertices[indices[prevIdx]];
        Vector3 b = vertices[indices[currIdx]];
        Vector3 c = vertices[indices[nextIdx]];
        
        // Check if triangle is counter-clockwise
        Vector3 cross = Vector3.Cross(b - a, c - a);
        if (Vector3.Dot(cross, normal) < 0)
        {
            return false;
        }
        
        // Check if any other vertex is inside the triangle
        for (int i = 0; i < indices.Count; i++)
        {
            if (i == prevIdx || i == currIdx || i == nextIdx)
                continue;
            
            if (IsPointInTriangle(vertices[indices[i]], a, b, c))
            {
                return false;
            }
        }
        
        return true;
    }
    
    bool IsPointInTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        // Barycentric coordinates method
        Vector3 v0 = c - a;
        Vector3 v1 = b - a;
        Vector3 v2 = p - a;
        
        float dot00 = Vector3.Dot(v0, v0);
        float dot01 = Vector3.Dot(v0, v1);
        float dot02 = Vector3.Dot(v0, v2);
        float dot11 = Vector3.Dot(v1, v1);
        float dot12 = Vector3.Dot(v1, v2);
        
        float invDenom = 1 / (dot00 * dot11 - dot01 * dot01);
        float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
        float v = (dot00 * dot12 - dot01 * dot02) * invDenom;
        
        return (u >= -0.001f) && (v >= -0.001f) && (u + v <= 1.001f);
    }
    
    Vector2[] GenerateUVs(Vector3[] vertices)
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
            dotHandler.OnLoopClosed -= OnLoopClosed;
        }
        
        // Clean up
        ClearAllShadedAreas();
        
        if (shadedAreaMaterial != null && shadedAreaMaterial.name == "DefaultShadedAreaMaterial")
            Destroy(shadedAreaMaterial);
    }
}