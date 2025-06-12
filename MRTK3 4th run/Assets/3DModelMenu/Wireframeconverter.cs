using System.Collections.Generic;
using UnityEngine;
using MixedReality.Toolkit.UX;

public class WireframeConverter : MonoBehaviour
{
    [Header("Target Object")]
    [SerializeField] private GameObject targetObject;
    
    [Header("MRTK3 Button")]
    [SerializeField] private PressableButton mrtkButton;
    
    [Header("Wireframe Settings")]
    [SerializeField] private Material wireframeMaterial;
    [SerializeField] private bool createWireframeMaterial = true;
    [SerializeField] private Color wireframeColor = Color.white;
    [SerializeField] private float wireframeWidth = 0.01f;
    
    private Dictionary<MeshRenderer, Material[]> originalMaterials = new Dictionary<MeshRenderer, Material[]>();
    private bool isWireframe = false;
    private Material generatedWireframeMaterial;

    private void Start()
    {
        // If no target object is specified, use this GameObject
        if (targetObject == null)
            targetObject = gameObject;
            
        // Subscribe to button press event
        if (mrtkButton != null)
        {
            mrtkButton.OnClicked.AddListener(ToggleWireframe);
        }
        
        // Create wireframe material if needed
        if (createWireframeMaterial && wireframeMaterial == null)
        {
            CreateWireframeMaterial();
        }
        
        // Store original materials
        StoreOriginalMaterials();
    }

    private void CreateWireframeMaterial()
    {
        // Create a simple wireframe material using Unity's built-in shader
        generatedWireframeMaterial = new Material(Shader.Find("Sprites/Default"));
        generatedWireframeMaterial.color = wireframeColor;
        generatedWireframeMaterial.SetInt("_ZWrite", 1);
        generatedWireframeMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
        
        // For better wireframe effect, you might want to use a custom shader
        // This is a basic implementation
        wireframeMaterial = generatedWireframeMaterial;
    }

    private void StoreOriginalMaterials()
    {
        // Get all MeshRenderers in the target object and its children
        MeshRenderer[] meshRenderers = targetObject.GetComponentsInChildren<MeshRenderer>();
        
        foreach (MeshRenderer renderer in meshRenderers)
        {
            // Store original materials
            originalMaterials[renderer] = renderer.materials;
        }
    }

    public void ToggleWireframe()
    {
        if (isWireframe)
        {
            RestoreOriginalMaterials();
        }
        else
        {
            ApplyWireframeMaterials();
        }
        
        isWireframe = !isWireframe;
    }

    private void ApplyWireframeMaterials()
    {
        if (wireframeMaterial == null)
        {
            Debug.LogWarning("No wireframe material assigned!");
            return;
        }

        foreach (var kvp in originalMaterials)
        {
            MeshRenderer renderer = kvp.Key;
            if (renderer != null)
            {
                // Create wireframe geometry
                CreateWireframeGeometry(renderer);
            }
        }
    }

    private void CreateWireframeGeometry(MeshRenderer meshRenderer)
    {
        MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.mesh == null) return;

        Mesh originalMesh = meshFilter.mesh;
        
        // Create wireframe child object
        GameObject wireframeObj = new GameObject(meshRenderer.name + "_Wireframe");
        wireframeObj.transform.SetParent(meshRenderer.transform);
        wireframeObj.transform.localPosition = Vector3.zero;
        wireframeObj.transform.localRotation = Quaternion.identity;
        wireframeObj.transform.localScale = Vector3.one;

        // Generate wireframe mesh
        Mesh wireframeMesh = GenerateWireframeMesh(originalMesh);
        
        // Setup wireframe renderer
        MeshFilter wireframeMeshFilter = wireframeObj.AddComponent<MeshFilter>();
        MeshRenderer wireframeMeshRenderer = wireframeObj.AddComponent<MeshRenderer>();
        
        wireframeMeshFilter.mesh = wireframeMesh;
        wireframeMeshRenderer.material = wireframeMaterial;
        
        // Hide original mesh
        meshRenderer.enabled = false;
        
        // Store wireframe object reference
        wireframeObj.tag = "WireframeTemp";
    }

    private Mesh GenerateWireframeMesh(Mesh originalMesh)
    {
        Vector3[] vertices = originalMesh.vertices;
        int[] triangles = originalMesh.triangles;
        
        List<Vector3> wireframeVertices = new List<Vector3>();
        List<int> wireframeIndices = new List<int>();
        
        // Generate lines from triangle edges
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v1 = triangles[i];
            int v2 = triangles[i + 1];
            int v3 = triangles[i + 2];
            
            // Add triangle edges as lines
            AddEdge(vertices[v1], vertices[v2], wireframeVertices, wireframeIndices);
            AddEdge(vertices[v2], vertices[v3], wireframeVertices, wireframeIndices);
            AddEdge(vertices[v3], vertices[v1], wireframeVertices, wireframeIndices);
        }
        
        // Create wireframe mesh
        Mesh wireframeMesh = new Mesh();
        wireframeMesh.vertices = wireframeVertices.ToArray();
        wireframeMesh.SetIndices(wireframeIndices.ToArray(), MeshTopology.Lines, 0);
        wireframeMesh.RecalculateBounds();
        
        return wireframeMesh;
    }

    private void AddEdge(Vector3 v1, Vector3 v2, List<Vector3> vertices, List<int> indices)
    {
        int startIndex = vertices.Count;
        vertices.Add(v1);
        vertices.Add(v2);
        indices.Add(startIndex);
        indices.Add(startIndex + 1);
    }

    private void RestoreOriginalMaterials()
    {
        // Remove wireframe objects
        GameObject[] wireframeObjects = GameObject.FindGameObjectsWithTag("WireframeTemp");
        foreach (GameObject obj in wireframeObjects)
        {
            if (obj.transform.IsChildOf(targetObject.transform))
            {
                DestroyImmediate(obj);
            }
        }
        
        // Restore original renderers
        foreach (var kvp in originalMaterials)
        {
            MeshRenderer renderer = kvp.Key;
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }
    }

    private void OnDestroy()
    {
        // Clean up generated materials
        if (generatedWireframeMaterial != null)
        {
            DestroyImmediate(generatedWireframeMaterial);
        }
        
        // Unsubscribe from button events
        if (mrtkButton != null)
        {
            mrtkButton.OnClicked.RemoveListener(ToggleWireframe);
        }
    }

    // Public methods for external control
    public void ShowWireframe()
    {
        if (!isWireframe)
            ToggleWireframe();
    }

    public void ShowOriginal()
    {
        if (isWireframe)
            ToggleWireframe();
    }
}