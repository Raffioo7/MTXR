using UnityEngine;

public class TransparentDoubleSidedPanel : MonoBehaviour
{
    [Header("Panel Appearance")]
    [Tooltip("Color of the panel (alpha controls transparency)")]
    public Color panelColor = new Color(1f, 0f, 0f, 0.3f); // Red with 30% opacity
    
    [Tooltip("Make panel visible from both sides")]
    public bool doubleSided = true;
    
    [Tooltip("Use URP/HDRP shader (if false, uses Standard shader)")]
    public bool useURPShader = true;
    
    void Start()
    {
        SetupTransparentDoubleSidedMaterial();
        SetupDoubleSidedColliders();
    }
    
    void SetupTransparentDoubleSidedMaterial()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogError("TransparentDoubleSidedPanel: No Renderer found!");
            return;
        }
        
        Material material;
        
        if (useURPShader)
        {
            // Use URP/HDRP Lit shader
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        }
        else
        {
            // Use Standard shader
            material = new Material(Shader.Find("Standard"));
        }
        
        // Set the base color
        material.color = panelColor;
        
        if (useURPShader)
        {
            // Configure URP shader for transparency
            SetupURPTransparency(material);
        }
        else
        {
            // Configure Standard shader for transparency
            SetupStandardTransparency(material);
        }
        
        // DISABLE BACKFACE CULLING - Makes it visible from both sides
        if (doubleSided)
        {
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        }
        else
        {
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Back);
        }
        
        // Apply the material
        renderer.material = material;
        
        Debug.Log($"TransparentDoubleSidedPanel: Applied {(useURPShader ? "URP" : "Standard")} transparent material");
    }
    
    void SetupURPTransparency(Material material)
    {
        // Set Surface Type to Transparent
        material.SetFloat("_Surface", 1); // 0 = Opaque, 1 = Transparent
        
        // Set Blending Mode to Alpha
        material.SetFloat("_Blend", 0); // 0 = Alpha, 1 = Premultiply, 2 = Additive, 3 = Multiply
        
        // Configure blend modes for transparency
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        
        // Disable depth writing for transparency
        material.SetInt("_ZWrite", 0);
        
        // Enable transparency keywords
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        
        // Set render queue to transparent
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
    
    void SetupStandardTransparency(Material material)
    {
        // Set Rendering Mode to Transparent
        material.SetFloat("_Mode", 3); // 0=Opaque, 1=Cutout, 2=Fade, 3=Transparent
        
        // Configure blend modes
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        
        // Enable transparency keyword
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        
        // Set render queue
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
    
    void SetupDoubleSidedColliders()
    {
        // For MRTK3 interaction from both sides, we need to ensure proper collider setup
        Collider mainCollider = GetComponent<Collider>();
        
        if (mainCollider != null && doubleSided)
        {
            // Method 1: Use a BoxCollider instead of MeshCollider for better double-sided interaction
            MeshCollider meshCollider = mainCollider as MeshCollider;
            if (meshCollider != null)
            {
                // Replace MeshCollider with BoxCollider for better double-sided interaction
                DestroyImmediate(meshCollider);
                
                BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
                boxCollider.isTrigger = false; // MRTK3 ObjectManipulator usually works better with non-trigger colliders
                
                // Size the box to match the visual bounds
                Renderer renderer = GetComponent<Renderer>();
                if (renderer != null)
                {
                    Bounds bounds = renderer.bounds;
                    boxCollider.size = transform.InverseTransformVector(bounds.size);
                    boxCollider.center = transform.InverseTransformPoint(bounds.center) - transform.InverseTransformPoint(transform.position);
                }
                else
                {
                    // Default size for a plane-like object
                    boxCollider.size = new Vector3(1f, 0.01f, 1f); // Very thin box
                }
                
                Debug.Log("TransparentDoubleSidedPanel: Replaced MeshCollider with BoxCollider for double-sided interaction");
            }
            else
            {
                // If it's already a BoxCollider or other primitive, make sure it's configured properly
                BoxCollider boxCollider = mainCollider as BoxCollider;
                if (boxCollider != null)
                {
                    boxCollider.isTrigger = false;
                    
                    // Ensure the box is thick enough to catch rays from both sides
                    Vector3 size = boxCollider.size;
                    if (size.y < 0.01f) // If it's too thin
                    {
                        size.y = 0.01f; // Make it slightly thicker
                        boxCollider.size = size;
                    }
                }
            }
            
            // Method 2: Add additional interaction components for MRTK3
            EnsureMRTK3Components();
            
            Debug.Log("TransparentDoubleSidedPanel: Configured collider for double-sided interaction");
        }
    }
    
    void EnsureMRTK3Components()
    {
        // Make sure we have the right MRTK3 components for interaction
        
        // Check for ObjectManipulator
        Component objectManipulator = null;
        Component[] components = GetComponents<Component>();
        
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i].GetType().Name.Contains("ObjectManipulator"))
            {
                objectManipulator = components[i];
                break;
            }
        }
            
        if (objectManipulator != null)
        {
            // Try to configure ObjectManipulator for better double-sided interaction
            try
            {
                var manipulatorType = objectManipulator.GetType();
                
                // Look for manipulation settings
                var allowedManipulationsField = manipulatorType.GetField("allowedManipulations");
                if (allowedManipulationsField != null)
                {
                    allowedManipulationsField.SetValue(objectManipulator, 3); // Move + Rotate
                }
                
                // Ensure it allows both near and far interaction
                var manipulationTypeField = manipulatorType.GetField("manipulationType");
                if (manipulationTypeField != null)
                {
                    manipulationTypeField.SetValue(objectManipulator, 2); // OneAndTwoHanded
                }
                
                Debug.Log("TransparentDoubleSidedPanel: Configured ObjectManipulator for double-sided interaction");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"TransparentDoubleSidedPanel: Could not configure ObjectManipulator: {e.Message}");
            }
        }
        
        // Ensure we have StatefulInteractable for better interaction detection
        bool hasStatefulInteractable = false;
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i].GetType().Name.Contains("StatefulInteractable"))
            {
                hasStatefulInteractable = true;
                break;
            }
        }
        
        if (!hasStatefulInteractable)
        {
            // Try to add StatefulInteractable if not present
            try
            {
                var statefulInteractableType = System.Type.GetType("MixedReality.Toolkit.UX.StatefulInteractable, MixedReality.Toolkit.UX");
                if (statefulInteractableType != null)
                {
                    gameObject.AddComponent(statefulInteractableType);
                    Debug.Log("TransparentDoubleSidedPanel: Added StatefulInteractable for better interaction");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"TransparentDoubleSidedPanel: Could not add StatefulInteractable: {e.Message}");
            }
        }
    }
    
    // Method to force double-sided interaction setup
    public void ForceDoubleSidedInteraction()
    {
        // Method 3: Create invisible interaction zones on both sides
        CreateDoubleSidedInteractionZones();
    }
    
    void CreateDoubleSidedInteractionZones()
    {
        // Create two thin interaction zones slightly offset from each side of the panel
        float offset = 0.005f; // 5mm offset
        
        // Front side interaction zone
        GameObject frontZone = new GameObject("FrontInteractionZone");
        frontZone.transform.SetParent(this.transform);
        frontZone.transform.localPosition = Vector3.forward * offset;
        frontZone.transform.localRotation = Quaternion.identity;
        frontZone.transform.localScale = Vector3.one;
        
        BoxCollider frontCollider = frontZone.AddComponent<BoxCollider>();
        frontCollider.size = new Vector3(1f, 1f, 0.001f); // Very thin
        frontCollider.isTrigger = false;
        
        // Back side interaction zone
        GameObject backZone = new GameObject("BackInteractionZone");
        backZone.transform.SetParent(this.transform);
        backZone.transform.localPosition = Vector3.back * offset;
        backZone.transform.localRotation = Quaternion.identity;
        backZone.transform.localScale = Vector3.one;
        
        BoxCollider backCollider = backZone.AddComponent<BoxCollider>();
        backCollider.size = new Vector3(1f, 1f, 0.001f); // Very thin
        backCollider.isTrigger = false;
        
        Debug.Log("TransparentDoubleSidedPanel: Created double-sided interaction zones");
    }
    
    // Public method to change color at runtime
    public void SetPanelColor(Color newColor)
    {
        panelColor = newColor;
        
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
        {
            renderer.material.color = newColor;
            
            // Update base color property for URP
            if (renderer.material.HasProperty("_BaseColor"))
            {
                renderer.material.SetColor("_BaseColor", newColor);
            }
        }
    }
    
    // Public method to change transparency
    public void SetTransparency(float alpha)
    {
        Color newColor = panelColor;
        newColor.a = Mathf.Clamp01(alpha);
        SetPanelColor(newColor);
    }
    
    // Public method to toggle double-sided rendering
    public void SetDoubleSided(bool enabled)
    {
        doubleSided = enabled;
        
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
        {
            if (doubleSided)
            {
                renderer.material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            }
            else
            {
                renderer.material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Back);
            }
        }
    }
    
    void OnValidate()
    {
        // Update material when values change in inspector
        if (Application.isPlaying)
        {
            SetupTransparentDoubleSidedMaterial();
        }
    }
}