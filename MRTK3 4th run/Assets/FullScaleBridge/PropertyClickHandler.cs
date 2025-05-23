using UnityEngine;
using TMPro;

public class PropertyClickHandler : MonoBehaviour
{
    [Header("UI References")]
    public GameObject propertyPanel;
    public TextMeshProUGUI propertyDisplay;
    
    [Header("Settings")]
    public LayerMask clickableLayerMask = -1; // All layers
    
    [Header("Highlighting")]
    public Color highlightColor = Color.yellow;
    public bool highlightEmission = true;
    
    private Camera playerCamera;
    private GameObject currentlySelected;
    private Material[] originalMaterials;
    private Material[] highlightMaterials;
    
    void Start()
    {
        playerCamera = Camera.main;
        if (playerCamera == null)
            playerCamera = FindObjectOfType<Camera>();
            
        // Hide panel initially
        if (propertyPanel != null)
            propertyPanel.SetActive(false);
    }
    
    void Update()
    {
        // Left mouse click
        if (Input.GetMouseButtonDown(0))
        {
            HandleClick();
        }
        
        // Press Escape to hide panel and clear selection
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ClearSelection();
        }
    }
    
    void HandleClick()
    {
        // Clear previous selection first
        ClearSelection();
        
        // Cast ray from camera through mouse position
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, clickableLayerMask))
        {
            // Check if the clicked object has RevitData
            RevitData revitData = hit.collider.GetComponent<RevitData>();
            
            if (revitData != null)
            {
                SelectObject(hit.collider.gameObject, revitData);
            }
            else
            {
                HidePropertyPanel();
            }
        }
        else
        {
            HidePropertyPanel();
        }
    }
    
    void SelectObject(GameObject clickedObject, RevitData data)
    {
        currentlySelected = clickedObject;
        HighlightObject(clickedObject);
        ShowProperties(data, clickedObject);
    }
    
    void HighlightObject(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null) return;
        
        // Store original materials
        originalMaterials = renderer.materials;
        highlightMaterials = new Material[originalMaterials.Length];
        
        // Create highlight materials
        for (int i = 0; i < originalMaterials.Length; i++)
        {
            highlightMaterials[i] = new Material(originalMaterials[i]);
            highlightMaterials[i].color = highlightColor;
            
            if (highlightEmission)
            {
                highlightMaterials[i].EnableKeyword("_EMISSION");
                highlightMaterials[i].SetColor("_EmissionColor", highlightColor * 0.3f);
            }
        }
        
        // Apply highlight materials
        renderer.materials = highlightMaterials;
    }
    
    void ClearSelection()
    {
        if (currentlySelected != null)
        {
            // Restore original materials
            Renderer renderer = currentlySelected.GetComponent<Renderer>();
            if (renderer != null && originalMaterials != null)
            {
                renderer.materials = originalMaterials;
            }
            
            // Clean up highlight materials
            if (highlightMaterials != null)
            {
                foreach (Material mat in highlightMaterials)
                {
                    if (mat != null)
                        DestroyImmediate(mat);
                }
            }
            
            currentlySelected = null;
            originalMaterials = null;
            highlightMaterials = null;
        }
        
        HidePropertyPanel();
    }
    
    void ShowProperties(RevitData data, GameObject clickedObject)
    {
        if (propertyPanel != null && propertyDisplay != null)
        {
            // Show the panel
            propertyPanel.SetActive(true);
            
            // Get parent and grandparent names
            string parentName = "None";
            string grandparentName = "None";
            
            if (clickedObject.transform.parent != null)
            {
                parentName = clickedObject.transform.parent.name;
                
                if (clickedObject.transform.parent.parent != null)
                {
                    grandparentName = clickedObject.transform.parent.parent.name;
                }
            }
            
            // Format the property text with hierarchy info
            string displayText = $"<b>Object:</b> {clickedObject.name}\n\n";
            displayText += $"<b>Parent:</b> {parentName}\n\n";
            displayText += $"<b>Grandparent:</b> {grandparentName}\n\n";
            displayText += $"<b>Family Type:</b> {data.familyType}\n\n";
            displayText += $"<b>Year:</b> {data.year}";
            
            propertyDisplay.text = displayText;
        }
    }
    
    void HidePropertyPanel()
    {
        if (propertyPanel != null)
        {
            propertyPanel.SetActive(false);
        }
    }
    
    void OnDestroy()
    {
        // Clean up any remaining highlight materials
        ClearSelection();
    }
}