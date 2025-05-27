using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class ColumnSetting
{
    public string columnName;
    public bool enabled = true;
    [Tooltip("Preview of first data entry")]
    public string previewData = "";
    
    public ColumnSetting(string name)
    {
        columnName = name;
        enabled = true;
    }
    
    public ColumnSetting(string name, string preview)
    {
        columnName = name;
        enabled = true;
        previewData = preview;
    }
}

public class PropertyLoader : MonoBehaviour
{
    [Header("CSV Data")]
    public TextAsset csvFile;
    
    [Header("Column Settings")]
    [Tooltip("Column settings are automatically generated from CSV headers. First column (Family Type) is always used for matching.")]
    public List<ColumnSetting> columnSettings = new List<ColumnSetting>();
    
    [Header("Controls")]
    [Button("Refresh Column Settings")]
    public bool refreshColumns;
    
    [Header("Debug")]
    public bool showDebugLog = true;
    
    void Start()
    {
        LoadPropertiesFromCSV();
    }
    
    void OnValidate()
    {
        // Auto-refresh columns when CSV file changes
        if (csvFile != null && (columnSettings.Count == 0 || refreshColumns))
        {
            RefreshColumnSettings();
        }
    }
    
    [ContextMenu("Refresh Column Settings")]
    void RefreshColumnSettings()
    {
        if (csvFile == null)
        {
            Debug.LogWarning("No CSV file assigned!");
            return;
        }
        
        // Try to read with UTF-8 encoding to handle accents and special characters
        string csvText = System.Text.Encoding.UTF8.GetString(csvFile.bytes);
        string[] lines = csvText.Split('\n');
        
        if (showDebugLog)
        {
            Debug.Log($"Total lines in CSV: {lines.Length}");
            if (lines.Length > 0) Debug.Log($"First line (headers): '{lines[0]}'");
            if (lines.Length > 1) Debug.Log($"Second line (first data): '{lines[1]}'");
        }
        
        if (lines.Length < 2)
        {
            Debug.LogWarning("CSV file needs at least a header and one data row!");
            return;
        }
        
        // Get headers from first line
        string[] headers = lines[0].Split(',');
        
        // Find first non-empty data row
        string[] firstDataRow = null;
        for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            if (!string.IsNullOrEmpty(lines[lineIndex].Trim()))
            {
                firstDataRow = lines[lineIndex].Split(',');
                if (showDebugLog)
                {
                    Debug.Log($"Using data row {lineIndex}: '{lines[lineIndex]}'");
                }
                break;
            }
        }
        
        if (firstDataRow == null)
        {
            Debug.LogWarning("No valid data rows found!");
            return;
        }
        
        // Clear existing settings
        columnSettings.Clear();
        
        // Create settings for each column
        for (int i = 0; i < headers.Length; i++)
        {
            string cleanHeader = headers[i].Trim().Trim('"');
            if (!string.IsNullOrEmpty(cleanHeader))
            {
                // Get preview data for this column
                string previewData = "No data";
                if (i < firstDataRow.Length)
                {
                    string rawData = firstDataRow[i].Trim().Trim('"');
                    previewData = string.IsNullOrEmpty(rawData) ? "Empty" : rawData;
                    
                    // Limit preview length for readability
                    if (previewData.Length > 30)
                    {
                        previewData = previewData.Substring(0, 30) + "...";
                    }
                }
                
                if (showDebugLog)
                {
                    Debug.Log($"Column {i}: Header='{cleanHeader}', Preview='{previewData}'");
                }
                
                ColumnSetting setting = new ColumnSetting(cleanHeader, previewData);
                // First column is always enabled (family type for matching)
                if (i == 0) setting.enabled = true;
                columnSettings.Add(setting);
            }
        }
        
        if (showDebugLog)
        {
            Debug.Log($"Refreshed column settings. Found {columnSettings.Count} columns: {string.Join(", ", columnSettings.Select(c => c.columnName))}");
        }
    }
    
    void LoadPropertiesFromCSV()
    {
        if (csvFile == null)
        {
            Debug.LogError("CSV file not assigned!");
            return;
        }
        
        // Try to read with UTF-8 encoding to handle accents and special characters
        string csvText = System.Text.Encoding.UTF8.GetString(csvFile.bytes);
        string[] lines = csvText.Split('\n');
        
        if (lines.Length <= 1)
        {
            Debug.LogError("CSV has no data rows!");
            return;
        }
        
        // Ensure column settings are up to date
        if (columnSettings.Count == 0)
        {
            RefreshColumnSettings();
        }
        
        // Get headers
        string[] headers = lines[0].Split(',');
        
        // Skip header row, process data
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i].Trim())) continue;
            
            string[] values = lines[i].Split(',');
            
            if (values.Length < 1) continue;
            
            string familyType = values[0].Trim().Trim('"');
            
            if (string.IsNullOrEmpty(familyType)) continue;
            
            // Create properties dictionary based on enabled columns
            Dictionary<string, string> properties = new Dictionary<string, string>();
            
            // Process all columns based on settings
            for (int colIndex = 0; colIndex < columnSettings.Count && colIndex < values.Length; colIndex++)
            {
                ColumnSetting setting = columnSettings[colIndex];
                
                if (setting.enabled && colIndex < values.Length)
                {
                    string value = values[colIndex].Trim().Trim('"');
                    properties[setting.columnName] = value;
                }
            }
            
            AttachPropertiesToObjects(familyType, properties);
        }
        
        if (showDebugLog)
        {
            Debug.Log("Property loading complete!");
        }
    }
    
    void AttachPropertiesToObjects(string familyType, Dictionary<string, string> properties)
    {
        // Find the FullScaleModel object first
        GameObject fullScaleModel = GameObject.Find("FullScaleModel");
        if (fullScaleModel == null)
        {
            if (showDebugLog)
                Debug.LogWarning("FullScaleModel object not found! RevitData will not be attached.");
            return;
        }
        
        // Only search within FullScaleModel and its children
        Transform[] allChildren = fullScaleModel.GetComponentsInChildren<Transform>();
        
        foreach (Transform childTransform in allChildren)
        {
            GameObject obj = childTransform.gameObject;
            
            // Skip the FullScaleModel itself
            if (obj == fullScaleModel) continue;
            
            // Check if object name contains family type (with various naming patterns)
            string objectName = obj.name.ToLower();
            string searchName = familyType.ToLower().Replace(" ", "").Replace(":", "");
            
            if (objectName.Contains(searchName) || 
                objectName.Contains(familyType.ToLower()) ||
                objectName.Replace(" ", "").Replace("_", "").Contains(searchName))
            {
                RevitData data = obj.GetComponent<RevitData>();
                if (data == null)
                {
                    data = obj.AddComponent<RevitData>();
                }
                
                data.SetProperties(properties);
                
                if (showDebugLog)
                {
                    string propertiesString = "";
                    foreach (var prop in properties)
                    {
                        propertiesString += $"{prop.Key}: {prop.Value}, ";
                    }
                    Debug.Log($"Added properties to: {obj.name} - {propertiesString.TrimEnd(',', ' ')}");
                }
            }
        }
    }
}

// Custom attribute for button in inspector
public class ButtonAttribute : PropertyAttribute
{
    public string MethodName { get; }
    
    public ButtonAttribute(string methodName)
    {
        MethodName = methodName;
    }
}