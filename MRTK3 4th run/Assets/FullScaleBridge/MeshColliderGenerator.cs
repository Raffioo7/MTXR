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
                // Check if StatefulInteractable component already exists
                if (obj.GetComponent("StatefulInteractable") == null)
                {
                    try
                    {
                        // Try to find StatefulInteractable in all loaded assemblies
                        Type statefulInteractableType = null;
                        
                        // Search through all assemblies for the type
                        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                        {
                            statefulInteractableType = assembly.GetType("Microsoft.MixedReality.Toolkit.StatefulInteractable");
                            if (statefulInteractableType != null) break;
                            
                            statefulInteractableType = assembly.GetType("MixedReality.Toolkit.StatefulInteractable");
                            if (statefulInteractableType != null) break;
                            
                            statefulInteractableType = assembly.GetType("StatefulInteractable");
                            if (statefulInteractableType != null) break;
                        }
                        
                        if (statefulInteractableType != null)
                        {
                            Component addedComponent = obj.AddComponent(statefulInteractableType);
                            if (addedComponent != null)
                            {
                                interactablesAdded++;
                                Debug.Log($"Added StatefulInteractable to {obj.name}");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"StatefulInteractable component type not found. MRTK3 packages need to be installed first.");
                            Debug.LogWarning("Install MRTK3 using Mixed Reality Feature Tool or Package Manager, then try again.");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Exception adding StatefulInteractable to {obj.name}: {e.Message}");
                        Debug.LogWarning("This is likely because MRTK3 is not properly installed or imported.");
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