using UnityEngine;
using UnityEngine.Rendering;
using MixedReality.Toolkit.UX;
using System.Collections.Generic;

public class URPOpacityController : MonoBehaviour
{
    [Header("Target Objects")]
    [SerializeField] private GameObject targetObject; // Parent object with multiple children
    [SerializeField] private Renderer[] targetRenderers; // Alternative: manually assign specific renderers
    
    [Header("Hierarchy Settings")]
    [SerializeField] private bool includeChildren = true;
    [SerializeField] private bool includeInactive = false;
    
    [Header("Debug Info")]
    [SerializeField] private bool showDebugInfo = true;
    
    private List<RendererData> rendererDataList = new List<RendererData>();
    
    [System.Serializable]
    private class RendererData
    {
        public Renderer renderer;
        public Material[] originalMaterials;
        public Material[] materialInstances;
        public Color[] originalColors;
        public bool[] wasTransparent;
    }
    
    void Start()
    {
        InitializeRenderers();
    }
    
    private void InitializeRenderers()
    {
        rendererDataList.Clear();
        
        List<Renderer> allRenderers = new List<Renderer>();
        
        // Get renderers from target object and children
        if (targetObject != null)
        {
            if (includeChildren)
            {
                allRenderers.AddRange(targetObject.GetComponentsInChildren<Renderer>(includeInactive));
            }
            else
            {
                Renderer singleRenderer = targetObject.GetComponent<Renderer>();
                if (singleRenderer != null)
                {
                    allRenderers.Add(singleRenderer);
                }
            }
        }
        
        // Add manually assigned renderers
        if (targetRenderers != null && targetRenderers.Length > 0)
        {
            foreach (var renderer in targetRenderers)
            {
                if (renderer != null && !allRenderers.Contains(renderer))
                {
                    allRenderers.Add(renderer);
                }
            }
        }
        
        // Process each renderer
        foreach (var renderer in allRenderers)
        {
            if (renderer == null) continue;
            
            RendererData data = new RendererData();
            data.renderer = renderer;
            data.originalMaterials = renderer.sharedMaterials;
            data.materialInstances = new Material[data.originalMaterials.Length];
            data.originalColors = new Color[data.originalMaterials.Length];
            data.wasTransparent = new bool[data.originalMaterials.Length];
            
            // Create material instances with full property preservation
            for (int i = 0; i < data.originalMaterials.Length; i++)
            {
                if (data.originalMaterials[i] != null)
                {
                    // Create a perfect copy of the material
                    data.materialInstances[i] = CreateMaterialCopy(data.originalMaterials[i]);
                    
                    // Store original values
                    if (data.materialInstances[i].HasProperty("_BaseColor"))
                    {
                        data.originalColors[i] = data.materialInstances[i].GetColor("_BaseColor");
                    }
                    else if (data.materialInstances[i].HasProperty("_Color"))
                    {
                        data.originalColors[i] = data.materialInstances[i].GetColor("_Color");
                    }
                    
                    // Check if it was already transparent
                    data.wasTransparent[i] = IsTransparent(data.materialInstances[i]);
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"Material {i} on {renderer.name}: {data.materialInstances[i].shader.name}");
                        Debug.Log($"Original color: {data.originalColors[i]}, Was transparent: {data.wasTransparent[i]}");
                    }
                }
            }
            
            // Apply the instances to the renderer
            renderer.materials = data.materialInstances;
            rendererDataList.Add(data);
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"URPOpacityController initialized with {rendererDataList.Count} renderers, {GetTotalMaterialCount()} materials");
        }
    }
    
    /// <summary>
    /// Create a perfect copy of a material, preserving all properties and keywords
    /// </summary>
    private Material CreateMaterialCopy(Material original)
    {
        Material copy = new Material(original);
        
        // Copy all shader keywords
        string[] keywords = original.shaderKeywords;
        foreach (string keyword in keywords)
        {
            copy.EnableKeyword(keyword);
        }
        
        // Ensure render queue is preserved
        copy.renderQueue = original.renderQueue;
        
        // For URP materials, preserve critical properties
        if (original.shader.name.Contains("Universal Render Pipeline"))
        {
            // Copy URP-specific properties that might get lost
            if (original.HasProperty("_Surface"))
                copy.SetFloat("_Surface", original.GetFloat("_Surface"));
            if (original.HasProperty("_Blend"))
                copy.SetFloat("_Blend", original.GetFloat("_Blend"));
            if (original.HasProperty("_AlphaClip"))
                copy.SetFloat("_AlphaClip", original.GetFloat("_AlphaClip"));
            if (original.HasProperty("_SrcBlend"))
                copy.SetFloat("_SrcBlend", original.GetFloat("_SrcBlend"));
            if (original.HasProperty("_DstBlend"))
                copy.SetFloat("_DstBlend", original.GetFloat("_DstBlend"));
            if (original.HasProperty("_ZWrite"))
                copy.SetFloat("_ZWrite", original.GetFloat("_ZWrite"));
        }
        
        return copy;
    }
    
    private bool IsTransparent(Material material)
    {
        if (material.HasProperty("_Surface"))
        {
            return material.GetFloat("_Surface") == 1; // 1 = Transparent in URP
        }
        return false;
    }
    
    private int GetTotalMaterialCount()
    {
        int count = 0;
        foreach (var data in rendererDataList)
        {
            count += data.materialInstances.Length;
        }
        return count;
    }
    
    /// <summary>
    /// Called by the slider's OnValueUpdated event
    /// </summary>
    public void OnSliderValueChanged(SliderEventData eventData)
    {
        SetOpacity(eventData.NewValue);
    }
    
    /// <summary>
    /// Set opacity for all materials
    /// </summary>
    public void SetOpacity(float opacity)
    {
        opacity = Mathf.Clamp01(opacity);
        
        foreach (var data in rendererDataList)
        {
            if (data.renderer == null) continue;
            
            for (int i = 0; i < data.materialInstances.Length; i++)
            {
                Material material = data.materialInstances[i];
                if (material == null) continue;
                
                // Only modify opacity if it's different from 1.0, or if we need to restore to 1.0
                bool needsTransparency = opacity < 1.0f;
                bool currentlyTransparent = IsTransparent(material);
                
                // Set the opacity
                SetMaterialOpacity(material, data.originalColors[i], opacity);
                
                // Only change transparency mode if needed
                if (needsTransparency != data.wasTransparent[i])
                {
                    SetURPTransparencyMode(material, needsTransparency);
                }
                else if (needsTransparency && !currentlyTransparent)
                {
                    // Need to enable transparency but preserve original settings
                    SetURPTransparencyMode(material, true);
                }
                else if (!needsTransparency && currentlyTransparent && !data.wasTransparent[i])
                {
                    // Need to disable transparency and restore original settings
                    SetURPTransparencyMode(material, false);
                }
            }
        }
    }
    
    private void SetMaterialOpacity(Material material, Color originalColor, float opacity)
    {
        Color newColor = originalColor;
        newColor.a = opacity;
        
        // Try different property names for different shader types
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", newColor);
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", newColor);
        }
        else if (material.HasProperty("_MainTex"))
        {
            material.color = newColor;
        }
    }
    
    private void SetURPTransparencyMode(Material material, bool transparent)
    {
        if (material.shader.name.Contains("Universal Render Pipeline"))
        {
            if (transparent)
            {
                // Only change to transparent if not already transparent
                if (material.GetFloat("_Surface") != 1)
                {
                    material.SetFloat("_Surface", 1); // 1 = Transparent
                    material.SetFloat("_Blend", 0); // 0 = Alpha blend
                    
                    // Enable alpha blending
                    material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_ZWrite", 0);
                    
                    // Set render queue
                    material.renderQueue = (int)RenderQueue.Transparent;
                    
                    // Enable keywords
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.EnableKeyword("_ALPHABLEND_ON");
                    material.DisableKeyword("_ALPHATEST_ON");
                }
            }
            else
            {
                // Only change to opaque if currently transparent and wasn't originally transparent
                if (material.GetFloat("_Surface") != 0)
                {
                    material.SetFloat("_Surface", 0); // 0 = Opaque
                    
                    // Set opaque blending
                    material.SetInt("_SrcBlend", (int)BlendMode.One);
                    material.SetInt("_DstBlend", (int)BlendMode.Zero);
                    material.SetInt("_ZWrite", 1);
                    
                    // Set render queue back to geometry
                    material.renderQueue = (int)RenderQueue.Geometry;
                    
                    // Disable transparency keywords
                    material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.DisableKeyword("_ALPHABLEND_ON");
                    material.DisableKeyword("_ALPHATEST_ON");
                }
            }
        }
        else
        {
            // Fallback for non-URP materials (Standard shader, etc.)
            if (transparent)
            {
                if (material.HasProperty("_Mode") && material.GetFloat("_Mode") != 3)
                {
                    material.SetFloat("_Mode", 3); // Transparent
                    material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_ZWrite", 0);
                    material.EnableKeyword("_ALPHABLEND_ON");
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.renderQueue = 3000;
                }
            }
            else
            {
                if (material.HasProperty("_Mode") && material.GetFloat("_Mode") != 0)
                {
                    material.SetFloat("_Mode", 0); // Opaque
                    material.SetInt("_SrcBlend", (int)BlendMode.One);
                    material.SetInt("_DstBlend", (int)BlendMode.Zero);
                    material.SetInt("_ZWrite", 1);
                    material.DisableKeyword("_ALPHABLEND_ON");
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.renderQueue = -1;
                }
            }
        }
    }
    
    void OnDestroy()
    {
        // Clean up material instances
        foreach (var data in rendererDataList)
        {
            if (data.materialInstances != null)
            {
                foreach (var material in data.materialInstances)
                {
                    if (material != null)
                    {
                        DestroyImmediate(material);
                    }
                }
            }
        }
        
        rendererDataList.Clear();
    }
    
    /// <summary>
    /// Reset all materials to full opacity
    /// </summary>
    public void ResetOpacity()
    {
        SetOpacity(1.0f);
    }
    
    /// <summary>
    /// Refresh the renderer list (call if hierarchy changes at runtime)
    /// </summary>
    public void RefreshRenderers()
    {
        OnDestroy();
        InitializeRenderers();
    }
    
    /// <summary>
    /// Get debug information
    /// </summary>
    public string GetDebugInfo()
    {
        return $"URPOpacityController: {rendererDataList.Count} renderers, {GetTotalMaterialCount()} materials";
    }
}