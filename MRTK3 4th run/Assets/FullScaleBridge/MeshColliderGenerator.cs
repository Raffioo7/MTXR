using UnityEngine;
using System;

public class AddCollidersToModel : MonoBehaviour
{
    [Header("Auto-add colliders to all child objects")]
    public bool addCollidersOnStart = true;
    
    [Header("Also add Stateful Interactable components")]
    public bool addStatefulInteractable = true;
    
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
                collider.convex = true;
                collidersAdded++;
            }
            
            // Add Stateful Interactable if enabled and missing
            if (addStatefulInteractable)
            {
                // Method 1: Try string-based component addition (most reliable)
                if (obj.GetComponent("StatefulInteractable") == null)
                {
                    Component addedComponent = UnityEngineInternal.APIUpdaterRuntimeServices.AddComponent(obj, "Assets/FullScaleBridge/MeshColliderGenerator.cs (45,48)", "StatefulInteractable");
                    if (addedComponent != null)
                    {
                        interactablesAdded++;
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to add StatefulInteractable to {obj.name}. Component type not found.");
                    }
                }
            }
        }
        
        Debug.Log($"Added {collidersAdded} colliders and {interactablesAdded} Stateful Interactables to {renderers.Length} objects");
    }
}