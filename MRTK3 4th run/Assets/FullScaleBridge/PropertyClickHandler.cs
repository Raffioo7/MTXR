using UnityEngine;
using TMPro;

public class PropertyClickHandler : MonoBehaviour
{
    [Header("UI References")]
    public GameObject propertyPanel;
    public TextMeshProUGUI propertyDisplay;
    
    [Header("Settings")]
    public LayerMask clickableLayerMask = -1; // All layers
    
    private Camera playerCamera;
    
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
        
        // Press Escape to hide panel
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HidePropertyPanel();
        }
    }
    
    void HandleClick()
    {
        // Cast ray from camera through mouse position
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, clickableLayerMask))
        {
            // Check if the clicked object has RevitData
            RevitData revitData = hit.collider.GetComponent<RevitData>();
            
            if (revitData != null)
            {
                ShowProperties(revitData, hit.collider.gameObject);
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
    
    void ShowProperties(RevitData data, GameObject clickedObject)
    {
        if (propertyPanel != null && propertyDisplay != null)
        {
            // Show the panel
            propertyPanel.SetActive(true);
            
            // Format the property text
            string displayText = $"<b>Object:</b> {clickedObject.name}\n\n";
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
}