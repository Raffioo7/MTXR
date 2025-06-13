using UnityEngine;
using MixedReality.Toolkit.UX;

public class URPOpacityController : MonoBehaviour
{
    [Header("Target Object")]
    public GameObject targetObject;
    
    [Header("MRTK Slider")]
    public Slider opacitySlider;
    
    private Renderer[] allRenderers;
    private Material[] originalMaterials;
    private bool[] wasTransparent;
    
    void Start()
    {
        // Get all renderers from the target object and its children
        allRenderers = targetObject.GetComponentsInChildren<Renderer>();
        
        // Store original materials and their transparency state
        StoreOriginalMaterials();
        
        // Set up slider event
        if (opacitySlider != null)
        {
            opacitySlider.OnValueUpdated.AddListener(OnSliderValueChanged);
            // Set initial opacity using the slider's current value
            SetOpacity(opacitySlider.Value);
        }
    }
    
    void StoreOriginalMaterials()
    {
        int totalMaterials = 0;
        foreach (var renderer in allRenderers)
        {
            totalMaterials += renderer.materials.Length;
        }
        
        originalMaterials = new Material[totalMaterials];
        wasTransparent = new bool[totalMaterials];
        
        int index = 0;
        foreach (var renderer in allRenderers)
        {
            foreach (var material in renderer.materials)
            {
                originalMaterials[index] = material;
                // Check if material was already transparent
                wasTransparent[index] = material.HasProperty("_Surface") && material.GetFloat("_Surface") == 1;
                index++;
            }
        }
    }
    
    void OnSliderValueChanged(SliderEventData eventData)
    {
        SetOpacity(eventData.NewValue);
    }
    
    void SetOpacity(float opacity)
    {
        int materialIndex = 0;
        foreach (var renderer in allRenderers)
        {
            foreach (var material in renderer.materials)
            {
                SetMaterialOpacity(material, opacity, wasTransparent[materialIndex]);
                materialIndex++;
            }
        }
    }
    
    void SetMaterialOpacity(Material material, float opacity, bool wasOriginallyTransparent)
    {
        // For URP Lit shader
        if (material.shader.name.Contains("Universal Render Pipeline/Lit"))
        {
            if (opacity < 1.0f || wasOriginallyTransparent)
            {
                // Set to Transparent mode
                material.SetFloat("_Surface", 1); // 0 = Opaque, 1 = Transparent
                material.SetFloat("_Blend", 0); // 0 = Alpha, 1 = Premultiply, 2 = Additive, 3 = Multiply
                
                // Enable transparency keywords
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                
                // Set render queue
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                
                // Set blend mode
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
            }
            else if (!wasOriginallyTransparent)
            {
                // Set back to Opaque mode
                material.SetFloat("_Surface", 0);
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
            }
            
            // Set the alpha value
            if (material.HasProperty("_BaseColor"))
            {
                Color baseColor = material.GetColor("_BaseColor");
                baseColor.a = opacity;
                material.SetColor("_BaseColor", baseColor);
            }
        }
        // Fallback for other shaders
        else if (material.HasProperty("_Color"))
        {
            Color color = material.color;
            color.a = opacity;
            material.color = color;
        }
    }
}