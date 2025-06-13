using UnityEngine;
using MixedReality.Toolkit.UX;
using System.Collections.Generic;
using System.Linq;

public class SimpleMeshCutter : MonoBehaviour
{
    [Header("Target Object")]
    public GameObject targetObject;
    
    [Header("Cutting Plane")]
    public GameObject cuttingPlane;
    
    [Header("MRTK Button")]
    public PressableButton cutButton;
    
    [Header("Cut Settings")]
    public bool destroyOriginal = true;
    [Tooltip("Which side of the plane to hide")]
    public bool hidePositiveSide = true;
    [Tooltip("Threshold for edge smoothing")]
    [Range(0.001f, 0.1f)]
    public float edgeSmoothingThreshold = 0.01f;
    [Tooltip("Subdivide triangles that span across the cutting plane")]
    public bool subdivideIntersectingTriangles = true;
    [Tooltip("Number of subdivision levels (1=4 triangles, 2=16 triangles, 3=64 triangles, etc.)")]
    [Range(1, 4)]
    public int subdivisionLevels = 2;
    [Tooltip("Distance threshold for triangle subdivision")]
    [Range(0.01f, 1.0f)]
    public float subdivisionThreshold = 0.1f;
    [Tooltip("Fill holes in cut surfaces")]
    public bool fillCutHoles = true;
    
    private struct OriginalMeshData
    {
        public GameObject gameObject;
        public Mesh originalMesh;
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
    }
    
    private struct Triangle
    {
        public Vector3 v1, v2, v3;
        public Vector3 n1, n2, n3;
        public Vector2 uv1, uv2, uv3;
        
        public Triangle(Vector3 vertex1, Vector3 vertex2, Vector3 vertex3,
                       Vector3 normal1, Vector3 normal2, Vector3 normal3,
                       Vector2 uv1, Vector2 uv2, Vector2 uv3)
        {
            this.v1 = vertex1; this.v2 = vertex2; this.v3 = vertex3;
            this.n1 = normal1; this.n2 = normal2; this.n3 = normal3;
            this.uv1 = uv1; this.uv2 = uv2; this.uv3 = uv3;
        }
    }
    
    private List<OriginalMeshData> originalMeshes = new List<OriginalMeshData>();
    private List<GameObject> cutObjects = new List<GameObject>();
    
    void Start()
    {
        if (cutButton != null)
        {
            cutButton.OnClicked.AddListener(PerformCut);
        }
        
        StoreOriginalMeshes();
    }
    
    void StoreOriginalMeshes()
    {
        MeshFilter[] meshFilters = targetObject.GetComponentsInChildren<MeshFilter>();
        
        foreach (var meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh != null)
            {
                var meshData = new OriginalMeshData
                {
                    gameObject = meshFilter.gameObject,
                    originalMesh = meshFilter.sharedMesh,
                    meshFilter = meshFilter,
                    meshRenderer = meshFilter.GetComponent<MeshRenderer>()
                };
                originalMeshes.Add(meshData);
            }
        }
    }
    
    public void PerformCut()
    {
        if (cuttingPlane == null)
        {
            Debug.LogError("Cutting plane is not assigned!");
            return;
        }
        
        ClearCutObjects();
        
        Vector3 planePosition = cuttingPlane.transform.position;
        Vector3 planeNormal = hidePositiveSide ? cuttingPlane.transform.up : -cuttingPlane.transform.up;
        
        foreach (var meshData in originalMeshes)
        {
            if (meshData.originalMesh == null) continue;
            
            Mesh slicedMesh = SliceMesh(meshData.originalMesh, planePosition, planeNormal, meshData.gameObject.transform);
            
            if (slicedMesh != null)
            {
                CreateCutObject(slicedMesh, meshData, "_Cut");
                
                if (destroyOriginal)
                {
                    meshData.gameObject.SetActive(false);
                }
            }
        }
        
        Debug.Log($"Mesh cutting completed with subdivision level {subdivisionLevels}!");
    }
    
    Mesh SliceMesh(Mesh originalMesh, Vector3 planePosition, Vector3 planeNormal, Transform objectTransform)
    {
        if (originalMesh == null || !originalMesh.isReadable)
        {
            Debug.LogWarning($"Mesh '{originalMesh.name}' is not readable.");
            return null;
        }
        
        // Transform plane to local space
        Vector3 localPlanePosition = objectTransform.InverseTransformPoint(planePosition);
        Vector3 localPlaneNormal = objectTransform.InverseTransformDirection(planeNormal).normalized;
        
        Vector3[] vertices = originalMesh.vertices;
        int[] triangles = originalMesh.triangles;
        Vector3[] normals = originalMesh.normals;
        Vector2[] uvs = originalMesh.uv;
        
        List<Vector3> newVertices = new List<Vector3>();
        List<int> newTriangles = new List<int>();
        List<Vector3> newNormals = new List<Vector3>();
        List<Vector2> newUVs = new List<Vector2>();
        
        // Track cut edge vertices for hole filling
        List<Vector3> cutEdgeVertices = new List<Vector3>();
        
        // Process each triangle
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v1 = vertices[triangles[i]];
            Vector3 v2 = vertices[triangles[i + 1]];
            Vector3 v3 = vertices[triangles[i + 2]];
            
            Vector3 n1 = normals.Length > triangles[i] ? normals[triangles[i]] : Vector3.up;
            Vector3 n2 = normals.Length > triangles[i + 1] ? normals[triangles[i + 1]] : Vector3.up;
            Vector3 n3 = normals.Length > triangles[i + 2] ? normals[triangles[i + 2]] : Vector3.up;
            
            Vector2 uv1 = uvs.Length > triangles[i] ? uvs[triangles[i]] : Vector2.zero;
            Vector2 uv2 = uvs.Length > triangles[i + 1] ? uvs[triangles[i + 1]] : Vector2.zero;
            Vector2 uv3 = uvs.Length > triangles[i + 2] ? uvs[triangles[i + 2]] : Vector2.zero;
            
            Triangle originalTriangle = new Triangle(v1, v2, v3, n1, n2, n3, uv1, uv2, uv3);
            
            // Check if triangle needs subdivision before cutting
            if (subdivideIntersectingTriangles && TriangleNeedsSubdivision(v1, v2, v3, localPlanePosition, localPlaneNormal))
            {
                // Subdivide the triangle and process each sub-triangle
                List<Triangle> subdivided = SubdivideTriangleRecursive(originalTriangle, subdivisionLevels);
                
                foreach (Triangle subTri in subdivided)
                {
                    ProcessTriangle(newVertices, newTriangles, newNormals, newUVs, cutEdgeVertices,
                                  subTri.v1, subTri.v2, subTri.v3, subTri.n1, subTri.n2, subTri.n3,
                                  subTri.uv1, subTri.uv2, subTri.uv3,
                                  localPlanePosition, localPlaneNormal);
                }
            }
            else
            {
                // Process triangle normally
                ProcessTriangle(newVertices, newTriangles, newNormals, newUVs, cutEdgeVertices,
                              v1, v2, v3, n1, n2, n3, uv1, uv2, uv3,
                              localPlanePosition, localPlaneNormal);
            }
        }
        
        // Fill holes in the cut surface
        if (fillCutHoles && cutEdgeVertices.Count >= 3)
        {
            FillCutHoles(newVertices, newTriangles, newNormals, newUVs, cutEdgeVertices, localPlaneNormal);
        }
        
        if (newVertices.Count == 0) return null;
        
        // Create new mesh
        Mesh slicedMesh = new Mesh();
        slicedMesh.name = originalMesh.name + "_Sliced";
        slicedMesh.vertices = newVertices.ToArray();
        slicedMesh.triangles = newTriangles.ToArray();
        
        if (newNormals.Count == newVertices.Count)
            slicedMesh.normals = newNormals.ToArray();
        else
            slicedMesh.RecalculateNormals();
        
        if (newUVs.Count == newVertices.Count)
            slicedMesh.uv = newUVs.ToArray();
        
        slicedMesh.RecalculateBounds();
        
        return slicedMesh;
    }
    
    bool TriangleNeedsSubdivision(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 planePosition, Vector3 planeNormal)
    {
        // Calculate distances from vertices to plane
        float d1 = Vector3.Dot(v1 - planePosition, planeNormal);
        float d2 = Vector3.Dot(v2 - planePosition, planeNormal);
        float d3 = Vector3.Dot(v3 - planePosition, planeNormal);
        
        // Check if triangle spans across the plane with a significant size
        float minDist = Mathf.Min(d1, d2, d3);
        float maxDist = Mathf.Max(d1, d2, d3);
        
        // Triangle spans the plane if min and max are on opposite sides
        bool spansPlane = (minDist < 0 && maxDist > 0);
        
        // Also check if the triangle is large enough to warrant subdivision
        float triangleSize = Vector3.Cross(v2 - v1, v3 - v1).magnitude * 0.5f;
        bool isLargeTriangle = triangleSize > subdivisionThreshold;
        
        return spansPlane && isLargeTriangle;
    }
    
    List<Triangle> SubdivideTriangleRecursive(Triangle triangle, int levels)
    {
        if (levels <= 0)
        {
            return new List<Triangle> { triangle };
        }
        
        // Create one level of subdivision (split into 4 triangles)
        List<Triangle> currentLevel = SubdivideTriangleOnce(triangle);
        
        if (levels == 1)
        {
            return currentLevel;
        }
        
        // Recursively subdivide each triangle
        List<Triangle> result = new List<Triangle>();
        foreach (Triangle subTriangle in currentLevel)
        {
            result.AddRange(SubdivideTriangleRecursive(subTriangle, levels - 1));
        }
        
        return result;
    }
    
    List<Triangle> SubdivideTriangleOnce(Triangle triangle)
    {
        // Calculate midpoints
        Vector3 mid12 = (triangle.v1 + triangle.v2) * 0.5f;
        Vector3 mid23 = (triangle.v2 + triangle.v3) * 0.5f;
        Vector3 mid31 = (triangle.v3 + triangle.v1) * 0.5f;
        
        // Interpolate normals
        Vector3 norm12 = ((triangle.n1 + triangle.n2) * 0.5f).normalized;
        Vector3 norm23 = ((triangle.n2 + triangle.n3) * 0.5f).normalized;
        Vector3 norm31 = ((triangle.n3 + triangle.n1) * 0.5f).normalized;
        
        // Interpolate UVs
        Vector2 uv12 = (triangle.uv1 + triangle.uv2) * 0.5f;
        Vector2 uv23 = (triangle.uv2 + triangle.uv3) * 0.5f;
        Vector2 uv31 = (triangle.uv3 + triangle.uv1) * 0.5f;
        
        // Create 4 sub-triangles
        List<Triangle> result = new List<Triangle>
        {
            // Corner triangles
            new Triangle(triangle.v1, mid12, mid31, triangle.n1, norm12, norm31, triangle.uv1, uv12, uv31),
            new Triangle(mid12, triangle.v2, mid23, norm12, triangle.n2, norm23, uv12, triangle.uv2, uv23),
            new Triangle(mid31, mid23, triangle.v3, norm31, norm23, triangle.n3, uv31, uv23, triangle.uv3),
            
            // Center triangle
            new Triangle(mid12, mid23, mid31, norm12, norm23, norm31, uv12, uv23, uv31)
        };
        
        return result;
    }
    
    void ProcessTriangle(List<Vector3> vertices, List<int> triangles, List<Vector3> normals, List<Vector2> uvs,
                        List<Vector3> cutEdgeVertices,
                        Vector3 v1, Vector3 v2, Vector3 v3, Vector3 n1, Vector3 n2, Vector3 n3,
                        Vector2 uv1, Vector2 uv2, Vector2 uv3,
                        Vector3 planePosition, Vector3 planeNormal)
    {
        // Check which side of the plane each vertex is on
        float d1 = Vector3.Dot(v1 - planePosition, planeNormal);
        float d2 = Vector3.Dot(v2 - planePosition, planeNormal);
        float d3 = Vector3.Dot(v3 - planePosition, planeNormal);
        
        // Apply smoothing threshold to reduce jagged edges
        if (Mathf.Abs(d1) < edgeSmoothingThreshold) d1 = 0f;
        if (Mathf.Abs(d2) < edgeSmoothingThreshold) d2 = 0f;
        if (Mathf.Abs(d3) < edgeSmoothingThreshold) d3 = 0f;
        
        bool keep1 = hidePositiveSide ? (d1 <= 0) : (d1 >= 0);
        bool keep2 = hidePositiveSide ? (d2 <= 0) : (d2 >= 0);
        bool keep3 = hidePositiveSide ? (d3 <= 0) : (d3 >= 0);
        
        int keepCount = (keep1 ? 1 : 0) + (keep2 ? 1 : 0) + (keep3 ? 1 : 0);
        
        if (keepCount == 3)
        {
            // Keep entire triangle
            AddTriangle(vertices, triangles, normals, uvs,
                       v1, v2, v3, n1, n2, n3, uv1, uv2, uv3);
        }
        else if (keepCount == 2)
        {
            // Cut triangle - keep 2 vertices
            CutTriangleKeep2(vertices, triangles, normals, uvs, cutEdgeVertices,
                            v1, v2, v3, n1, n2, n3, uv1, uv2, uv3,
                            keep1, keep2, keep3, d1, d2, d3);
        }
        else if (keepCount == 1)
        {
            // Cut triangle - keep 1 vertex
            CutTriangleKeep1(vertices, triangles, normals, uvs, cutEdgeVertices,
                            v1, v2, v3, n1, n2, n3, uv1, uv2, uv3,
                            keep1, keep2, keep3, d1, d2, d3);
        }
        // If keepCount == 0, discard entire triangle
    }
    
    void CutTriangleKeep2(List<Vector3> vertices, List<int> triangles, List<Vector3> normals, List<Vector2> uvs,
                         List<Vector3> cutEdgeVertices,
                         Vector3 v1, Vector3 v2, Vector3 v3, Vector3 n1, Vector3 n2, Vector3 n3,
                         Vector2 uv1, Vector2 uv2, Vector2 uv3,
                         bool keep1, bool keep2, bool keep3, float d1, float d2, float d3)
    {
        // Find which vertices to keep and which to discard
        Vector3 keepA, keepB, discard;
        Vector3 normA, normB, normDiscard;
        Vector2 uvA, uvB, uvDiscard;
        float distA, distB, distDiscard;
        
        if (!keep1) // v1 is discarded
        {
            keepA = v2; keepB = v3; discard = v1;
            normA = n2; normB = n3; normDiscard = n1;
            uvA = uv2; uvB = uv3; uvDiscard = uv1;
            distA = d2; distB = d3; distDiscard = d1;
        }
        else if (!keep2) // v2 is discarded
        {
            keepA = v1; keepB = v3; discard = v2;
            normA = n1; normB = n3; normDiscard = n2;
            uvA = uv1; uvB = uv3; uvDiscard = uv2;
            distA = d1; distB = d3; distDiscard = d2;
        }
        else // v3 is discarded
        {
            keepA = v1; keepB = v2; discard = v3;
            normA = n1; normB = n2; normDiscard = n3;
            uvA = uv1; uvB = uv2; uvDiscard = uv3;
            distA = d1; distB = d2; distDiscard = d3;
        }
        
        // Calculate intersection points with better precision
        Vector3 intersectA = CalculateIntersection(keepA, discard, distA, distDiscard);
        Vector3 intersectB = CalculateIntersection(keepB, discard, distB, distDiscard);
        
        // Store cut edge vertices for hole filling
        cutEdgeVertices.Add(intersectA);
        cutEdgeVertices.Add(intersectB);
        
        // Interpolate normals and UVs
        float tA = Mathf.Abs(distA) / (Mathf.Abs(distA) + Mathf.Abs(distDiscard) + 0.0001f);
        float tB = Mathf.Abs(distB) / (Mathf.Abs(distB) + Mathf.Abs(distDiscard) + 0.0001f);
        
        Vector3 intersectNormA = Vector3.Lerp(normA, normDiscard, tA).normalized;
        Vector3 intersectNormB = Vector3.Lerp(normB, normDiscard, tB).normalized;
        
        Vector2 intersectUVA = Vector2.Lerp(uvA, uvDiscard, tA);
        Vector2 intersectUVB = Vector2.Lerp(uvB, uvDiscard, tB);
        
        // Create two triangles to form a quad
        AddTriangle(vertices, triangles, normals, uvs,
                   keepA, keepB, intersectA,
                   normA, normB, intersectNormA,
                   uvA, uvB, intersectUVA);
        
        AddTriangle(vertices, triangles, normals, uvs,
                   keepB, intersectB, intersectA,
                   normB, intersectNormB, intersectNormA,
                   uvB, intersectUVB, intersectUVA);
    }
    
    void CutTriangleKeep1(List<Vector3> vertices, List<int> triangles, List<Vector3> normals, List<Vector2> uvs,
                         List<Vector3> cutEdgeVertices,
                         Vector3 v1, Vector3 v2, Vector3 v3, Vector3 n1, Vector3 n2, Vector3 n3,
                         Vector2 uv1, Vector2 uv2, Vector2 uv3,
                         bool keep1, bool keep2, bool keep3, float d1, float d2, float d3)
    {
        // Find which vertex to keep
        Vector3 keep, discardA, discardB;
        Vector3 normKeep, normDiscardA, normDiscardB;
        Vector2 uvKeep, uvDiscardA, uvDiscardB;
        float distKeep, distDiscardA, distDiscardB;
        
        if (keep1)
        {
            keep = v1; discardA = v2; discardB = v3;
            normKeep = n1; normDiscardA = n2; normDiscardB = n3;
            uvKeep = uv1; uvDiscardA = uv2; uvDiscardB = uv3;
            distKeep = d1; distDiscardA = d2; distDiscardB = d3;
        }
        else if (keep2)
        {
            keep = v2; discardA = v1; discardB = v3;
            normKeep = n2; normDiscardA = n1; normDiscardB = n3;
            uvKeep = uv2; uvDiscardA = uv1; uvDiscardB = uv3;
            distKeep = d2; distDiscardA = d1; distDiscardB = d3;
        }
        else
        {
            keep = v3; discardA = v1; discardB = v2;
            normKeep = n3; normDiscardA = n1; normDiscardB = n2;
            uvKeep = uv3; uvDiscardA = uv1; uvDiscardB = uv2;
            distKeep = d3; distDiscardA = d1; distDiscardB = d2;
        }
        
        // Calculate intersection points
        Vector3 intersectA = CalculateIntersection(keep, discardA, distKeep, distDiscardA);
        Vector3 intersectB = CalculateIntersection(keep, discardB, distKeep, distDiscardB);
        
        // Store cut edge vertices for hole filling
        cutEdgeVertices.Add(intersectA);
        cutEdgeVertices.Add(intersectB);
        
        // Interpolate normals and UVs
        float tA = Mathf.Abs(distKeep) / (Mathf.Abs(distKeep) + Mathf.Abs(distDiscardA) + 0.0001f);
        float tB = Mathf.Abs(distKeep) / (Mathf.Abs(distKeep) + Mathf.Abs(distDiscardB) + 0.0001f);
        
        Vector3 intersectNormA = Vector3.Lerp(normKeep, normDiscardA, tA).normalized;
        Vector3 intersectNormB = Vector3.Lerp(normKeep, normDiscardB, tB).normalized;
        
        Vector2 intersectUVA = Vector2.Lerp(uvKeep, uvDiscardA, tA);
        Vector2 intersectUVB = Vector2.Lerp(uvKeep, uvDiscardB, tB);
        
        // Create one triangle
        AddTriangle(vertices, triangles, normals, uvs,
                   keep, intersectA, intersectB,
                   normKeep, intersectNormA, intersectNormB,
                   uvKeep, intersectUVA, intersectUVB);
    }
    
    void FillCutHoles(List<Vector3> vertices, List<int> triangles, List<Vector3> normals, List<Vector2> uvs,
                     List<Vector3> cutEdgeVertices, Vector3 planeNormal)
    {
        if (cutEdgeVertices.Count < 3) return;
        
        // Remove duplicate vertices that are very close to each other
        List<Vector3> cleanedVertices = new List<Vector3>();
        float mergeThreshold = edgeSmoothingThreshold;
        
        foreach (Vector3 vertex in cutEdgeVertices)
        {
            bool isDuplicate = false;
            foreach (Vector3 existing in cleanedVertices)
            {
                if (Vector3.Distance(vertex, existing) < mergeThreshold)
                {
                    isDuplicate = true;
                    break;
                }
            }
            if (!isDuplicate)
            {
                cleanedVertices.Add(vertex);
            }
        }
        
        if (cleanedVertices.Count < 3) return;
        
        // Calculate center point
        Vector3 center = Vector3.zero;
        foreach (Vector3 vertex in cleanedVertices)
        {
            center += vertex;
        }
        center /= cleanedVertices.Count;
        
        // Sort vertices around center for proper winding
        List<Vector3> sortedVertices = SortVerticesAroundCenter(cleanedVertices, center, planeNormal);
        
        // Create triangles from center to each edge
        Vector3 normal = hidePositiveSide ? -planeNormal : planeNormal;
        
        for (int i = 0; i < sortedVertices.Count; i++)
        {
            int next = (i + 1) % sortedVertices.Count;
            
            Vector3 v1 = center;
            Vector3 v2 = sortedVertices[i];
            Vector3 v3 = sortedVertices[next];
            
            // Check winding order and flip if necessary
            Vector3 calculatedNormal = Vector3.Cross(v2 - v1, v3 - v1).normalized;
            if (Vector3.Dot(calculatedNormal, normal) < 0)
            {
                Vector3 temp = v2;
                v2 = v3;
                v3 = temp;
            }
            
            AddTriangle(vertices, triangles, normals, uvs,
                       v1, v2, v3,
                       normal, normal, normal,
                       Vector2.zero, Vector2.zero, Vector2.zero);
        }
    }
    
    List<Vector3> SortVerticesAroundCenter(List<Vector3> vertices, Vector3 center, Vector3 normal)
    {
        Vector3 reference = Vector3.up;
        if (Vector3.Dot(reference, normal) > 0.9f)
        {
            reference = Vector3.right;
        }
        
        Vector3 planeReference = Vector3.ProjectOnPlane(reference, normal).normalized;
        
        List<Vector3> sortedVertices = new List<Vector3>(vertices);
        sortedVertices.Sort((a, b) =>
        {
            Vector3 dirA = Vector3.ProjectOnPlane((a - center).normalized, normal);
            Vector3 dirB = Vector3.ProjectOnPlane((b - center).normalized, normal);
            
            float angleA = Mathf.Atan2(Vector3.Dot(Vector3.Cross(planeReference, dirA), normal), Vector3.Dot(planeReference, dirA));
            float angleB = Mathf.Atan2(Vector3.Dot(Vector3.Cross(planeReference, dirB), normal), Vector3.Dot(planeReference, dirB));
            
            if (angleA < 0) angleA += 2 * Mathf.PI;
            if (angleB < 0) angleB += 2 * Mathf.PI;
            
            return angleA.CompareTo(angleB);
        });
        
        return sortedVertices;
    }
    
    Vector3 CalculateIntersection(Vector3 v1, Vector3 v2, float d1, float d2)
    {
        float denominator = Mathf.Abs(d1) + Mathf.Abs(d2);
        if (denominator < 0.0001f) return v1;
        
        float t = Mathf.Abs(d1) / denominator;
        return Vector3.Lerp(v1, v2, t);
    }
    
    void AddTriangle(List<Vector3> vertices, List<int> triangles, List<Vector3> normals, List<Vector2> uvs,
                    Vector3 v1, Vector3 v2, Vector3 v3, Vector3 n1, Vector3 n2, Vector3 n3,
                    Vector2 uv1, Vector2 uv2, Vector2 uv3)
    {
        int startIndex = vertices.Count;
        vertices.AddRange(new Vector3[] { v1, v2, v3 });
        triangles.AddRange(new int[] { startIndex, startIndex + 1, startIndex + 2 });
        normals.AddRange(new Vector3[] { n1, n2, n3 });
        uvs.AddRange(new Vector2[] { uv1, uv2, uv3 });
    }
    
    void CreateCutObject(Mesh cutMesh, OriginalMeshData originalData, string suffix)
    {
        GameObject cutObject = new GameObject(originalData.gameObject.name + suffix);
        cutObjects.Add(cutObject);
        
        cutObject.transform.SetParent(originalData.gameObject.transform.parent);
        cutObject.transform.localPosition = originalData.gameObject.transform.localPosition;
        cutObject.transform.localRotation = originalData.gameObject.transform.localRotation;
        cutObject.transform.localScale = originalData.gameObject.transform.localScale;
        
        MeshFilter meshFilter = cutObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = cutObject.AddComponent<MeshRenderer>();
        
        meshFilter.mesh = cutMesh;
        meshRenderer.materials = originalData.meshRenderer.materials;
        
        if (originalData.gameObject.GetComponent<MeshCollider>())
        {
            MeshCollider collider = cutObject.AddComponent<MeshCollider>();
            collider.sharedMesh = cutMesh;
            collider.convex = true;
        }
    }
    
    void ClearCutObjects()
    {
        foreach (var cutObject in cutObjects)
        {
            if (cutObject != null)
            {
                DestroyImmediate(cutObject);
            }
        }
        cutObjects.Clear();
        
        if (destroyOriginal)
        {
            foreach (var meshData in originalMeshes)
            {
                if (meshData.gameObject != null)
                {
                    meshData.gameObject.SetActive(true);
                }
            }
        }
    }
   
    public void ResetCut()
    {
        ClearCutObjects();
    }
}