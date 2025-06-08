using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Examples of how to connect different menu systems to the inspection manager
/// </summary>

// EXAMPLE 1: Connect a simple input field
public class BasicInfoConnector : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField inspectorNameField;
    public TMP_InputField bridgeIdField;
    public TMP_InputField locationField;
    
    [Header("Manager Reference")]
    public BridgeInspectionManager inspectionManager;
    
    void Start()
    {
        if (inspectionManager == null)
            inspectionManager = FindObjectOfType<BridgeInspectionManager>();
    }
    
    // Call this when user finishes entering basic info
    public void OnBasicInfoChanged()
    {
        if (inspectionManager != null)
        {
            inspectionManager.SetBasicInfo(
                inspectorNameField.text,
                bridgeIdField.text,
                locationField.text
            );
        }
    }
}

// EXAMPLE 2: Connect your tile menu system
public class TileMenuConnector : MonoBehaviour
{
    [Header("Manager Reference")]
    public BridgeInspectionManager inspectionManager;
    
    void Start()
    {
        if (inspectionManager == null)
            inspectionManager = FindObjectOfType<BridgeInspectionManager>();
    }
    
    // Call this when a tile is selected
    public void OnTileSelected(string category, string subcategory, string selectedValue)
    {
        if (inspectionManager != null)
        {
            inspectionManager.AddInspectionData(
                category,           // e.g., "Structural Assessment"
                subcategory,        // e.g., "Deck Condition"
                "Selected Option",  // Field name
                selectedValue,      // The tile that was selected
                "dropdown"          // Data type
            );
        }
    }
    
    // Example: Connect to your existing TileMenuManager
    public void ConnectToTileMenu(TileMenuManager tileMenu, string category, string subcategory)
    {
        // You would modify your TileMenuManager's OnTileClicked method to call this:
        // OnTileSelected(category, subcategory, selectedTile.column1);
    }
}

// EXAMPLE 3: Connect your dot placement system
public class DotPlacementConnector : MonoBehaviour
{
    [Header("Manager Reference")]
    public BridgeInspectionManager inspectionManager;
    
    [Header("Dot System Reference")]
    public DotPlacementHandler_MRTK3 dotHandler;
    
    void Start()
    {
        if (inspectionManager == null)
            inspectionManager = FindObjectOfType<BridgeInspectionManager>();
            
        // Subscribe to dot events if available
        if (dotHandler != null)
        {
            dotHandler.OnLoopClosed += OnDotLoopClosed;
        }
    }
    
    // Call this to save current dot positions
    public void SaveCurrentDotPositions(string category, string subcategory, string description)
    {
        if (inspectionManager != null && dotHandler != null)
        {
            var allLoops = dotHandler.GetAllLoopPositions();
            
            // Convert to simple coordinate list
            List<Vector3> allCoordinates = new List<Vector3>();
            foreach (var loop in allLoops)
            {
                allCoordinates.AddRange(loop);
            }
            
            inspectionManager.AddCoordinateData(
                category,       // e.g., "Measurements"
                subcategory,    // e.g., "Crack Mapping"
                description,    // e.g., "Crack Pattern 1"
                allCoordinates,
                $"Recorded {allLoops.Count} loops with {allCoordinates.Count} total points"
            );
        }
    }
    
    void OnDotLoopClosed(int loopIndex)
    {
        // Automatically save when a loop is closed
        SaveCurrentDotPositions("Measurements", "Crack Mapping", $"Auto-saved Loop {loopIndex + 1}");
    }
}

// EXAMPLE 4: Connect your camera system
public class CameraConnector : MonoBehaviour
{
    [Header("Manager Reference")]
    public BridgeInspectionManager inspectionManager;
    
    [Header("Camera Reference")]
    public HoloLensCameraCapture cameraCapture;
    
    void Start()
    {
        if (inspectionManager == null)
            inspectionManager = FindObjectOfType<BridgeInspectionManager>();
            
        // Subscribe to camera events
        if (cameraCapture != null)
        {
            cameraCapture.OnCaptureComplete += OnPhotoCaptured;
        }
    }
    
    // Call this to take a photo for a specific inspection purpose
    public void TakeInspectionPhoto(string category, string subcategory, string description)
    {
        // Store the context for when the photo is actually captured
        PhotoContext context = new PhotoContext
        {
            category = category,
            subcategory = subcategory,
            description = description
        };
        
        // You could store this context and use it in OnPhotoCaptured
        // For now, just trigger the capture
        if (cameraCapture != null)
        {
            cameraCapture.CaptureHologramInclusivePhoto();
        }
    }
    
    void OnPhotoCaptured(string imagePath)
    {
        if (inspectionManager != null)
        {
            // You might want to show a UI to let user categorize the photo
            // For now, just add it with a generic category
            inspectionManager.AddImageData(
                "Documentation",    // Category
                "Photos",          // Subcategory
                $"Photo {System.DateTime.Now:HHmmss}", // Field name
                imagePath,         // Image path
                "Photo captured during inspection"      // Notes
            );
        }
    }
    
    [System.Serializable]
    public class PhotoContext
    {
        public string category;
        public string subcategory;
        public string description;
    }
}

// EXAMPLE 5: Simple text input connector
public class TextInputConnector : MonoBehaviour
{
    [Header("Manager Reference")]
    public BridgeInspectionManager inspectionManager;
    
    void Start()
    {
        if (inspectionManager == null)
            inspectionManager = FindObjectOfType<BridgeInspectionManager>();
    }
    
    // Call this from any text input field
    public void OnTextChanged(string category, string subcategory, string fieldName, string value)
    {
        if (inspectionManager != null)
        {
            inspectionManager.UpdateInspectionData(category, subcategory, fieldName, value);
        }
    }
    
    // Example: Connect to a comment field
    public void OnCommentChanged(TMP_InputField commentField)
    {
        OnTextChanged("General", "Comments", "Inspector Notes", commentField.text);
    }
    
    // Example: Connect to a measurement field
    public void OnMeasurementChanged(TMP_InputField measurementField, string measurementType)
    {
        OnTextChanged("Measurements", "Manual", measurementType, measurementField.text);
    }
}

// EXAMPLE 6: Dropdown/Selection connector
public class DropdownConnector : MonoBehaviour
{
    [Header("Manager Reference")]
    public BridgeInspectionManager inspectionManager;
    
    void Start()
    {
        if (inspectionManager == null)
            inspectionManager = FindObjectOfType<BridgeInspectionManager>();
    }
    
    // Call this when any dropdown selection changes
    public void OnDropdownChanged(string category, string subcategory, string fieldName, int selectedIndex, string[] options)
    {
        if (inspectionManager != null && selectedIndex >= 0 && selectedIndex < options.Length)
        {
            inspectionManager.UpdateInspectionData(
                category,
                subcategory,
                fieldName,
                options[selectedIndex],
                $"Selected from {options.Length} options"
            );
        }
    }
}