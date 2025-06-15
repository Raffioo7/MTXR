using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class FBXMeshCombiner : EditorWindow
{
    [MenuItem("Tools/Combine FBX Meshes")]
    static void ShowWindow()
    {
        GetWindow<FBXMeshCombiner>("FBX Mesh Combiner");
    }

    GameObject selectedFBX;

    void OnGUI()
    {
        GUILayout.Label("FBX Mesh Combiner", EditorStyles.boldLabel);
        
        selectedFBX = (GameObject)EditorGUILayout.ObjectField("FBX Root", selectedFBX, typeof(GameObject), true);
        
        if (GUILayout.Button("Combine Meshes") && selectedFBX != null)
        {
            CombineFBXMeshes();
        }
    }

    void CombineFBXMeshes()
    {
        // Get all mesh filters from the FBX and its children
        MeshFilter[] meshFilters = selectedFBX.GetComponentsInChildren<MeshFilter>();
        
        if (meshFilters.Length == 0)
        {
            Debug.LogError("No mesh filters found in selected FBX!");
            return;
        }

        // Prepare combine instances
        List<CombineInstance> combineInstances = new List<CombineInstance>();
        
        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh != null)
            {
                CombineInstance ci = new CombineInstance();
                ci.mesh = meshFilter.sharedMesh;
                ci.transform = meshFilter.transform.localToWorldMatrix;
                combineInstances.Add(ci);
            }
        }

        // Create combined mesh
        Mesh combinedMesh = new Mesh();
        combinedMesh.name = selectedFBX.name + "_Combined";
        combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);
        
        // Optimize the mesh
        combinedMesh.Optimize();
        combinedMesh.RecalculateNormals();
        combinedMesh.RecalculateBounds();

        // Save as asset
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Combined Mesh", 
            selectedFBX.name + "_Combined", 
            "asset", 
            "Save combined mesh as asset");
            
        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(combinedMesh, path);
            AssetDatabase.SaveAssets();
            
            // Create a new GameObject with the combined mesh
            GameObject combinedObject = new GameObject(selectedFBX.name + "_Combined");
            combinedObject.AddComponent<MeshFilter>().sharedMesh = combinedMesh;
            
            // Get material from first mesh renderer
            MeshRenderer firstRenderer = selectedFBX.GetComponentInChildren<MeshRenderer>();
            if (firstRenderer != null)
            {
                MeshRenderer newRenderer = combinedObject.AddComponent<MeshRenderer>();
                newRenderer.material = firstRenderer.sharedMaterial;
            }
            
            Debug.Log($"Combined mesh saved to: {path}");
        }
    }
}