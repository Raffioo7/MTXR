using UnityEngine;
using System;

public class AddCollidersToModel : MonoBehaviour
{
    [Header("Auto-add colliders to all child objects")]
    public bool addCollidersOnStart = true;
    
    [Header("Also add Stateful Interactable components")]
    public bool addStatefulInteractable = true;
    
    [Header("Collider Settings")]
    public bool useConvexColliders = false; // Changed default to false for concave shapes
    
    void Start()
    {
        if (addCollidersOnStart)
        {
            AddCollidersToChildren();
        }
    }
    
    [ContextMenu("Add Colliders to All Children")]
    public void AddCollidersToChildren()
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        int collidersAdded = 0;
        int interactablesAdded = 0;
        
        foreach (MeshRenderer renderer in renderers)
        {
            GameObject obj = renderer.gameObject;
            
            // Add collider if missing
            if (obj.GetComponent<Collider>() == null)
            {
                MeshCollider collider = obj.AddComponent<MeshCollider>();
                collider.convex = useConvexColliders; // Now controlled by inspector setting
                
                // Ensure we have a mesh to use
                MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    collider.sharedMesh = meshFilter.sharedMesh;
                    collidersAdded++;
                    
                    if (useConvexColliders)
                    {
                        Debug.Log($"Added CONVEX mesh collider to {obj.name}");
                    }
                    else
                    {
                        Debug.Log($"Added NON-CONVEX mesh collider to {obj.name}");
                    }
                }
                else
                {
                    Debug.LogWarning($"No mesh found for {obj.name}, removing collider");
                    DestroyImmediate(collider);
                }
            }
            
            // Add Stateful Interactable if enabled and missing
            if (addStatefulInteractable)
            {
                // Method 1: Try string-based component addition (most reliable)
                if (obj.GetComponent("StatefulInteractable") == null)
                {
                    try
                    {
                        Component addedComponent = UnityEngineInternal.APIUpdaterRuntimeServices.AddComponent(obj, "Assets/FullScaleBridge/MeshColliderGenerator.cs (45,48)", "StatefulInteractable");
                        if (addedComponent != null)
                        {
                            interactablesAdded++;
                            Debug.Log($"Added StatefulInteractable to {obj.name}");
                        }
                        else
                        {
                            Debug.LogWarning($"Failed to add StatefulInteractable to {obj.name}. Component type not found.");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Exception adding StatefulInteractable to {obj.name}: {e.Message}");
                    }
                }
            }
        }
        
        string convexStatus = useConvexColliders ? "CONVEX" : "NON-CONVEX";
        Debug.Log($"Added {collidersAdded} {convexStatus} colliders and {interactablesAdded} Stateful Interactables to {renderers.Length} objects");
    }
    
    [ContextMenu("Remove All Colliders")]
    public void RemoveAllColliders()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        int removed = 0;
        
        foreach (Collider collider in colliders)
        {
            DestroyImmediate(collider);
            removed++;
        }
        
        Debug.Log($"Removed {removed} colliders");
    }
    
    [ContextMenu("Convert Existing Colliders to Non-Convex")]
    public void ConvertToNonConvex()
    {
        MeshCollider[] meshColliders = GetComponentsInChildren<MeshCollider>();
        int converted = 0;
        
        foreach (MeshCollider meshCol in meshColliders)
        {
            if (meshCol.convex)
            {
                meshCol.convex = false;
                converted++;
                Debug.Log($"Converted {meshCol.gameObject.name} to non-convex");
            }
        }
        
        Debug.Log($"Converted {converted} mesh colliders to non-convex");
    }
    
    [ContextMenu("Debug Collider Status")]
    public void DebugColliderStatus()
    {
        MeshCollider[] meshColliders = GetComponentsInChildren<MeshCollider>();
        int convexCount = 0;
        int nonConvexCount = 0;
        
        Debug.Log("=== COLLIDER STATUS REPORT ===");
        
        foreach (MeshCollider meshCol in meshColliders)
        {
            if (meshCol.convex)
            {
                convexCount++;
                Debug.Log($"CONVEX: {meshCol.gameObject.name}");
            }
            else
            {
                nonConvexCount++;
                Debug.Log($"NON-CONVEX: {meshCol.gameObject.name}");
            }
        }
        
        Debug.Log($"SUMMARY: {convexCount} convex, {nonConvexCount} non-convex colliders");
    }
}