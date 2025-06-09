using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Central data structure for bridge inspection
/// </summary>
[System.Serializable]
public class BridgeInspectionRecord
{
    [Header("Basic Information")]
    public string inspectionId;
    public string inspectorName;
    public string bridgeId;
    public string location;
    public DateTime inspectionDate;
    
    [Header("Inspection Data")]
    public List<InspectionEntry> entries;
    
    public BridgeInspectionRecord()
    {
        inspectionId = System.Guid.NewGuid().ToString();
        inspectionDate = DateTime.Now;
        entries = new List<InspectionEntry>();
    }
}

/// <summary>
/// Individual data entry - flexible structure for any type of input
/// </summary>
[System.Serializable]
public class InspectionEntry
{
    public string category;        // e.g., "Structural", "Visual", "Measurements"
    public string subcategory;     // e.g., "Deck", "Supports", "Joints"
    public string fieldName;      // e.g., "Crack Length", "Material Condition"
    public string value;          // The actual data
    public string dataType;       // "text", "number", "coordinates", "image", "dropdown"
    public string notes;          // Additional comments
    public DateTime timestamp;    // When this entry was recorded
    
    public InspectionEntry(string cat, string subcat, string field, string val, string type = "text", string note = "")
    {
        category = cat;
        subcategory = subcat;
        fieldName = field;
        value = val;
        dataType = type;
        notes = note;
        timestamp = DateTime.Now;
    }
}

/// <summary>
/// Central manager that collects data from all your different menu systems
/// </summary>
public class BridgeInspectionManager : MonoBehaviour
{
    [Header("Current Inspection")]
    public BridgeInspectionRecord currentInspection;
    
    [Header("File Settings")]
    public string saveFileName = "bridge_inspection";
    public bool autoSaveEnabled = false;
    public float autoSaveInterval = 300f; // 5 minutes
    
    [Header("Debug")]
    public bool debugMode = true;
    
    private string savePath;
    private float lastAutoSave;
    
    void Start()
    {
        // Set up save path
        savePath = Application.persistentDataPath;
        
        // Start new inspection
        StartNewInspection();
        
        if (debugMode)
            Debug.Log($"Bridge Inspection Manager started. Save path: {savePath}");
    }
    
    void Update()
    {
        // Auto-save functionality
        if (autoSaveEnabled && Time.time - lastAutoSave > autoSaveInterval)
        {
            AutoSave();
        }
    }
    
    #region Public Methods - Call these from your different menu systems
    
    /// <summary>
    /// Start a completely new inspection record
    /// </summary>
    public void StartNewInspection()
    {
        currentInspection = new BridgeInspectionRecord();
        
        if (debugMode)
            Debug.Log($"Started new inspection: {currentInspection.inspectionId}");
    }
    
    /// <summary>
    /// Set basic inspection information
    /// </summary>
    public void SetBasicInfo(string inspectorName, string bridgeId, string location)
    {
        currentInspection.inspectorName = inspectorName;
        currentInspection.bridgeId = bridgeId;
        currentInspection.location = location;
        currentInspection.inspectionDate = DateTime.Now;
        
        if (debugMode)
            Debug.Log($"Set basic info - Inspector: {inspectorName}, Bridge: {bridgeId}");
    }
    
    /// <summary>
    /// Add any piece of data to the inspection record
    /// Use this from ALL your different menu systems
    /// </summary>
    public void AddInspectionData(string category, string subcategory, string fieldName, string value, string dataType = "text", string notes = "")
    {
        var entry = new InspectionEntry(category, subcategory, fieldName, value, dataType, notes);
        currentInspection.entries.Add(entry);
        
        if (debugMode)
            Debug.Log($"Added data: {category}/{subcategory}/{fieldName} = {value}");
    }
    
    /// <summary>
    /// Update existing data or add if it doesn't exist
    /// </summary>
    public void UpdateInspectionData(string category, string subcategory, string fieldName, string newValue, string notes = "")
    {
        // Find existing entry
        var existing = currentInspection.entries.Find(e => 
            e.category == category && 
            e.subcategory == subcategory && 
            e.fieldName == fieldName);
        
        if (existing != null)
        {
            existing.value = newValue;
            existing.notes = notes;
            existing.timestamp = DateTime.Now;
            
            if (debugMode)
                Debug.Log($"Updated data: {category}/{subcategory}/{fieldName} = {newValue}");
        }
        else
        {
            AddInspectionData(category, subcategory, fieldName, newValue, "text", notes);
        }
    }
    
    /// <summary>
    /// Add coordinate data (from your dot placement system)
    /// </summary>
    public void AddCoordinateData(string category, string subcategory, string fieldName, List<Vector3> coordinates, string notes = "")
    {
        string coordJson = JsonUtility.ToJson(new CoordinateList { coordinates = coordinates });
        AddInspectionData(category, subcategory, fieldName, coordJson, "coordinates", notes);
    }
    
    /// <summary>
    /// Add image data (from your camera system)
    /// </summary>
    public void AddImageData(string category, string subcategory, string fieldName, string imagePath, string notes = "")
    {
        AddInspectionData(category, subcategory, fieldName, imagePath, "image", notes);
    }
    
    /// <summary>
    /// Get data from the current inspection
    /// </summary>
    public string GetInspectionData(string category, string subcategory, string fieldName)
    {
        var entry = currentInspection.entries.Find(e => 
            e.category == category && 
            e.subcategory == subcategory && 
            e.fieldName == fieldName);
        
        return entry?.value ?? "";
    }
    
    /// <summary>
    /// Get all entries for a specific category
    /// </summary>
    public List<InspectionEntry> GetCategoryData(string category)
    {
        return currentInspection.entries.FindAll(e => e.category == category);
    }
    
    #endregion
    
    #region Save/Load Methods
    
    /// <summary>
    /// Save current inspection to JSON file
    /// </summary>
    public void SaveInspection()
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"{saveFileName}_{timestamp}.json";
            string fullPath = Path.Combine(savePath, fileName);
            
            string json = JsonUtility.ToJson(currentInspection, true);
            File.WriteAllText(fullPath, json);
            
            Debug.Log($"Inspection saved: {fullPath}");
            Debug.Log($"Total entries: {currentInspection.entries.Count}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save inspection: {e.Message}");
        }
    }
    
    /// <summary>
    /// Load inspection from JSON file
    /// </summary>
    public void LoadInspection(string fileName)
    {
        try
        {
            string fullPath = Path.Combine(savePath, fileName);
            
            if (File.Exists(fullPath))
            {
                string json = File.ReadAllText(fullPath);
                currentInspection = JsonUtility.FromJson<BridgeInspectionRecord>(json);
                
                Debug.Log($"Inspection loaded: {fileName}");
                Debug.Log($"Total entries: {currentInspection.entries.Count}");
            }
            else
            {
                Debug.LogWarning($"File not found: {fullPath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load inspection: {e.Message}");
        }
    }
    
    /// <summary>
    /// Get list of all saved inspection files
    /// </summary>
    public List<string> GetSavedInspections()
    {
        List<string> files = new List<string>();
        
        try
        {
            string[] jsonFiles = Directory.GetFiles(savePath, $"{saveFileName}_*.json");
            
            foreach (string file in jsonFiles)
            {
                files.Add(Path.GetFileName(file));
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to get saved inspections: {e.Message}");
        }
        
        return files;
    }
    
    void AutoSave()
    {
        if (currentInspection.entries.Count > 0)
        {
            SaveInspection();
            lastAutoSave = Time.time;
            
            if (debugMode)
                Debug.Log("Auto-saved inspection");
        }
    }
    
    #endregion
    
    #region Debug and Utility Methods
    
    /// <summary>
    /// Test method - add sample data to verify your structure works
    /// Call this from a button to test your categories
    /// </summary>
    public void AddSampleData()
    {
        // Test with just one simple text entry
        AddInspectionData("General", "Notes", "Inspector Comments", "This is a test comment from the inspector");
        
        Debug.Log("Sample data added - check PrintInspectionSummary()");
    }
    
    /// <summary>
    /// Print current inspection data to console
    /// </summary>
    public void PrintInspectionSummary()
    {
        Debug.Log("=== BRIDGE INSPECTION SUMMARY ===");
        Debug.Log($"ID: {currentInspection.inspectionId}");
        Debug.Log($"Inspector: {currentInspection.inspectorName}");
        Debug.Log($"Bridge: {currentInspection.bridgeId}");
        Debug.Log($"Location: {currentInspection.location}");
        Debug.Log($"Date: {currentInspection.inspectionDate}");
        Debug.Log($"Total Entries: {currentInspection.entries.Count}");
        
        // Group by category
        var categories = new Dictionary<string, int>();
        foreach (var entry in currentInspection.entries)
        {
            if (!categories.ContainsKey(entry.category))
                categories[entry.category] = 0;
            categories[entry.category]++;
        }
        
        Debug.Log("--- Categories ---");
        foreach (var cat in categories)
        {
            Debug.Log($"{cat.Key}: {cat.Value} entries");
        }
        
        Debug.Log("--- All Entries ---");
        foreach (var entry in currentInspection.entries)
        {
            Debug.Log($"{entry.category} > {entry.subcategory} > {entry.fieldName}: {entry.value}");
        }
    }
    
    /// <summary>
    /// Clear all inspection data
    /// </summary>
    public void ClearInspection()
    {
        currentInspection.entries.Clear();
        
        if (debugMode)
            Debug.Log("Cleared all inspection data");
    }
    
    /// <summary>
    /// Get total number of entries
    /// </summary>
    public int GetEntryCount()
    {
        return currentInspection.entries.Count;
    }
    
    #endregion
}

/// <summary>
/// Helper class for coordinate serialization
/// </summary>
[System.Serializable]
public class CoordinateList
{
    public List<Vector3> coordinates;
}