using UnityEngine;
using MixedReality.Toolkit.UX;

public class SimpleOpacityController : MonoBehaviour
{
    public GameObject targetObject;
    public Slider opacitySlider;
    
    private Renderer[] renderers;
    
    void Start()
    {
        renderers = targetObject.GetComponentsInChildren<Renderer>();
        opacitySlider.OnValueUpdated.AddListener(OnSliderValueChanged);
    }
    
    void OnSliderValueChanged(SliderEventData eventData)
    {
        float alpha = eventData.NewValue;
        
        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.materials)
            {
                if (material.HasProperty("_Color"))
                {
                    Color color = material.color;
                    color.a = alpha;
                    material.color = color;
                }
            }
        }
    }
}