using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class ButtonHoverText : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private string hoverMessage = "Button hover text";
    [SerializeField] private GameObject hoverTextPrefab; // Drag your prefab here
    [SerializeField] private Vector3 hoverTextOffset = new Vector3(0, 0.1f, 0); // Position relative to button
    
    private GameObject hoverTextInstance;
    private TextMeshPro hoverTextComponent;
    private Vector3 lastOffset; // Track the last offset to detect changes
    
    void Start()
    {
        // Instantiate the prefab for this button
        if (hoverTextPrefab != null)
        {
            hoverTextInstance = Instantiate(hoverTextPrefab, transform);
            hoverTextInstance.transform.localPosition = hoverTextOffset;
            
            // Get the TextMeshPro component
            hoverTextComponent = hoverTextInstance.GetComponent<TextMeshPro>();
            
            // Store the initial offset
            lastOffset = hoverTextOffset;
            
            // Initially hide it
            hoverTextInstance.SetActive(false);
        }
    }
    
    void Update()
    {
        // Check if the offset has changed during runtime
        if (hoverTextInstance != null && lastOffset != hoverTextOffset)
        {
            // Update the position with the new offset
            hoverTextInstance.transform.localPosition = hoverTextOffset;
            lastOffset = hoverTextOffset;
        }
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverTextComponent != null)
        {
            hoverTextComponent.text = hoverMessage;
            hoverTextInstance.SetActive(true);
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverTextInstance != null)
            hoverTextInstance.SetActive(false);
    }
    
    void OnDestroy()
    {
        // Clean up the instantiated prefab
        if (hoverTextInstance != null)
            Destroy(hoverTextInstance);
    }
}