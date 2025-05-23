using UnityEngine;

public class AddCollidersToModel : MonoBehaviour
{
    [Header("Auto-add colliders to all child objects")]
    public bool addCollidersOnStart = true;
    
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
        
        foreach (MeshRenderer renderer in renderers)
        {
            GameObject obj = renderer.gameObject;
            
            if (obj.GetComponent<Collider>() == null)
            {
                MeshCollider collider = obj.AddComponent<MeshCollider>();
                collider.convex = true;
            }
        }
        
        Debug.Log($"Added colliders to {renderers.Length} objects");
    }
}