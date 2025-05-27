using UnityEngine;

public class SimpleClickHighlighter : MonoBehaviour
{
    [Header("Highlight Settings")]
    public Color highlightColor = Color.yellow;
    public float highlightDuration = 1.0f;
    
    private Renderer objectRenderer;
    private Color originalColor;
    private Material originalMaterial;
    private bool isHighlighted = false;
    
    void Start()
    {
        // Get the renderer and store original color
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null)
        {
            originalMaterial = objectRenderer.material;
            originalColor = originalMaterial.color;
        }
    }
    
    // This method can be called from MRTK3 events or buttons
    public void OnObjectClicked()
    {
        if (!isHighlighted)
        {
            ActivateHighlight();
        }
        else
        {
            DeactivateHighlight();
        }
    }
    
    void ActivateHighlight()
    {
        if (objectRenderer != null)
        {
            objectRenderer.material.color = highlightColor;
            isHighlighted = true;
            
            // Auto-remove highlight after duration
            if (highlightDuration > 0)
            {
                Invoke(nameof(DeactivateHighlight), highlightDuration);
            }
        }
    }
    
    void DeactivateHighlight()
    {
        if (objectRenderer != null && isHighlighted)
        {
            objectRenderer.material.color = originalColor;
            isHighlighted = false;
            
            // Cancel any pending auto-stop
            CancelInvoke(nameof(DeactivateHighlight));
        }
    }
}