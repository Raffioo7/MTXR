using UnityEngine;
using TMPro;
using MixedReality.Toolkit.UX;
using System;
using System.IO;
using System.Linq;

/// <summary>
/// Reads text from multiple TMP fields and MRTK3 input field, saves to JSON, and can load back from JSON
/// </summary>
public class SimpleTextReader : MonoBehaviour
{
    [Header("Text Fields (Display Only)")]
    [Tooltip("Drag your first TextMeshProUGUI component here")]
    public TextMeshProUGUI textField1;
    
    [Tooltip("Drag your second TextMeshProUGUI component here")]
    public TextMeshProUGUI textField2;
    
    [Header("Input Fields (User Input)")]
    [Tooltip("Drag your MRTK3 TMP Input Field component here")]
    public TMP_InputField inputField1;
    
    [Header("Settings")]
    public string fileName = "bridge_inspection";
    
    [Header("Buttons")]
    [Tooltip("Drag your save button here")]
    public GameObject saveButtonObject;
    
    [Tooltip("Drag your load button here")]
    public GameObject loadButtonObject;
    
    [Header("Element Highlighting")]
    [Tooltip("Drag your PropertyClickHandler_MRTK3 component here")]
    public PropertyClickHandler_MRTK3 propertyHandler;
    
    [Header("Load Settings")]
    [Tooltip("Filename to load (without .json extension). Leave empty to load most recent file.")]
    public string fileToLoad = "";
    
    void Start()
    {
        // Find PropertyClickHandler if not assigned
        if (propertyHandler == null)
            propertyHandler = FindObjectOfType<PropertyClickHandler_MRTK3>();
        
        SetupButtons();
        LogAssignedFields();
    }
    
    void SetupButtons()
    {
        // Set up the save button
        if (saveButtonObject != null)
        {
            var saveButton = saveButtonObject.GetComponent<PressableButton>();
            if (saveButton == null)
                saveButton = saveButtonObject.GetComponentInChildren<PressableButton>();
            
            if (saveButton != null)
            {
                saveButton.OnClicked.AddListener(SaveAllTextToJSON);
                Debug.Log("Save button connected!");
            }
        }
        
        // Set up the load button
        if (loadButtonObject != null)
        {
            var loadButton = loadButtonObject.GetComponent<PressableButton>();
            if (loadButton == null)
                loadButton = loadButtonObject.GetComponentInChildren<PressableButton>();
            
            if (loadButton != null)
            {
                loadButton.OnClicked.AddListener(LoadFromJSON);
                Debug.Log("Load button connected!");
            }
        }
    }
    
    void LogAssignedFields()
    {
        Debug.Log("=== ASSIGNED FIELDS ===");
        if (textField1 != null)
            Debug.Log($"Text Field 1: '{textField1.gameObject.name}' - Current: '{textField1.text}'");
        if (textField2 != null)
            Debug.Log($"Text Field 2: '{textField2.gameObject.name}' - Current: '{textField2.text}'");
        if (inputField1 != null)
            Debug.Log($"Input Field 1: '{inputField1.gameObject.name}' - Current: '{inputField1.text}'");
        
        Debug.Log("Text reader ready. Save button captures all text. Load button restores from JSON.");
    }
    
    public void SaveAllTextToJSON()
    {
        Debug.Log("=== CAPTURING ALL TEXT FIELDS ===");
        
        string text1 = textField1 != null ? (textField1.text ?? "") : "";
        string text2 = textField2 != null ? (textField2.text ?? "") : "";
        string input1 = inputField1 != null ? (inputField1.text ?? "") : "";
        
        if (textField1 != null)
            Debug.Log($"Text Field 1 ('{textField1.gameObject.name}'): '{text1}'");
        if (textField2 != null)
            Debug.Log($"Text Field 2 ('{textField2.gameObject.name}'): '{text2}'");
        if (inputField1 != null)
            Debug.Log($"Input Field 1 ('{inputField1.gameObject.name}'): '{input1}'");
        
        // Get highlighted element info
        ElementInfo elementInfo = GetHighlightedElementInfo();
        
        SaveToJSON(text1, text2, input1, elementInfo);
    }
    
    ElementInfo GetHighlightedElementInfo()
    {
        var elementInfo = new ElementInfo();
        
        if (propertyHandler != null)
        {
            // Use reflection to get the private currentlySelected field
            var field = typeof(PropertyClickHandler_MRTK3).GetField("currentlySelected", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                GameObject highlightedElement = field.GetValue(propertyHandler) as GameObject;
                
                if (highlightedElement != null)
                {
                    elementInfo.elementName = highlightedElement.name;
                    
                    // Get RevitData if available
                    RevitData revitData = highlightedElement.GetComponent<RevitData>();
                    if (revitData != null)
                    {
                        elementInfo.elementID = GetRevitDataID(revitData);
                        elementInfo.elementProperties = GetRevitDataSummary(revitData, highlightedElement);
                    }
                    
                    Debug.Log($"Connected to highlighted element: {elementInfo.elementName}");
                }
                else
                {
                    Debug.Log("No element currently highlighted");
                }
            }
        }
        
        return elementInfo;
    }
    
    string GetRevitDataID(RevitData revitData)
    {
        // Try to get ID from RevitData - you might need to adjust this based on your RevitData structure
        try
        {
            var properties = revitData.GetDisplayProperties(revitData.gameObject);
            var idProperty = properties.FirstOrDefault(p => 
                p.key.ToLower().Contains("id") || 
                p.key.ToLower().Contains("elementid") ||
                p.key.ToLower().Contains("guid"));
            
            return idProperty?.value ?? revitData.gameObject.GetInstanceID().ToString();
        }
        catch
        {
            return revitData.gameObject.GetInstanceID().ToString();
        }
    }
    
    string GetRevitDataSummary(RevitData revitData, GameObject element)
    {
        try
        {
            var properties = revitData.GetDisplayProperties(element);
            var summary = "";
            
            // Get first few key properties
            foreach (var prop in properties.Take(5))
            {
                summary += $"{prop.key}: {prop.value}; ";
            }
            
            return summary.TrimEnd(' ', ';');
        }
        catch
        {
            return $"Element: {element.name}";
        }
    }
    
    public void LoadFromJSON()
    {
        Debug.Log("=== LOADING FROM JSON ===");
        
        try
        {
            string filePath = GetFilePathToLoad();
            
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                Debug.LogError($"File not found: {filePath}");
                return;
            }
            
            string jsonContent = File.ReadAllText(filePath);
            Debug.Log($"Loading from: {filePath}");
            
            MultiFieldInspectionData loadedData = JsonUtility.FromJson<MultiFieldInspectionData>(jsonContent);
            
            if (loadedData == null)
            {
                Debug.LogError("Failed to parse JSON data");
                return;
            }
            
            PopulateFieldsFromData(loadedData);
            
            Debug.Log($"SUCCESS! Loaded inspection data from {Path.GetFileName(filePath)}");
            Debug.Log($"Inspection ID: {loadedData.inspectionId}");
            Debug.Log($"Timestamp: {loadedData.timestamp}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load JSON: {e.Message}");
        }
    }
    
    void PopulateFieldsFromData(MultiFieldInspectionData data)
    {
        Debug.Log("=== POPULATING FIELDS FROM LOADED DATA ===");
        
        if (textField1 != null)
        {
            textField1.text = data.textField1 ?? "";
            Debug.Log($"Loaded into Text Field 1: '{data.textField1}'");
        }
        
        if (textField2 != null)
        {
            textField2.text = data.textField2 ?? "";
            Debug.Log($"Loaded into Text Field 2: '{data.textField2}'");
        }
        
        if (inputField1 != null)
        {
            inputField1.text = data.inputField1 ?? "";
            Debug.Log($"Loaded into Input Field 1: '{data.inputField1}'");
        }
        
        Debug.Log("All fields populated with loaded data!");
    }
    
    string GetFilePathToLoad()
    {
        string basePath = Application.persistentDataPath;
        
        if (!string.IsNullOrEmpty(fileToLoad))
        {
            string specificFile = fileToLoad.EndsWith(".json") ? fileToLoad : fileToLoad + ".json";
            return Path.Combine(basePath, specificFile);
        }
        
        try
        {
            string[] files = Directory.GetFiles(basePath, $"{fileName}_*.json");
            if (files.Length == 0)
            {
                Debug.LogWarning($"No {fileName}_*.json files found in {basePath}");
                return "";
            }
            
            string mostRecentFile = files.OrderByDescending(f => File.GetCreationTime(f)).First();
            Debug.Log($"Found {files.Length} files, using most recent: {Path.GetFileName(mostRecentFile)}");
            return mostRecentFile;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error finding files: {e.Message}");
            return "";
        }
    }
    
    void SaveToJSON(string textField1Content, string textField2Content, string inputField1Content, ElementInfo elementInfo)
    {
        var inspectionData = new MultiFieldInspectionData
        {
            inspectionId = Guid.NewGuid().ToString(),
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            textField1 = textField1Content,
            textField2 = textField2Content,
            inputField1 = inputField1Content,
            connectedElementName = elementInfo.elementName,
            connectedElementID = elementInfo.elementID,
            elementProperties = elementInfo.elementProperties,
            captureInfo = $"Captured {DateTime.Now:HH:mm:ss} - Fields: {(string.IsNullOrEmpty(textField1Content) ? 0 : 1) + (string.IsNullOrEmpty(textField2Content) ? 0 : 1) + (string.IsNullOrEmpty(inputField1Content) ? 0 : 1)} with data, Element: {elementInfo.elementName}"
        };
        
        try
        {
            string json = JsonUtility.ToJson(inspectionData, true);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fullFileName = $"{fileName}_{timestamp}.json";
            string filePath = Path.Combine(Application.persistentDataPath, fullFileName);
            
            File.WriteAllText(filePath, json);
            
            Debug.Log($"SUCCESS! Saved all fields to: {filePath}");
            Debug.Log($"Text Field 1: '{textField1Content}'");
            Debug.Log($"Text Field 2: '{textField2Content}'");
            Debug.Log($"Input Field 1: '{inputField1Content}'");
            Debug.Log($"Connected Element: '{elementInfo.elementName}' (ID: {elementInfo.elementID})");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save: {e.Message}");
        }
    }
    
    public void LoadSpecificFile(string filename)
    {
        fileToLoad = filename;
        LoadFromJSON();
    }
    
    public string[] GetAvailableFiles()
    {
        try
        {
            string[] files = Directory.GetFiles(Application.persistentDataPath, $"{fileName}_*.json");
            return files.Select(f => Path.GetFileName(f)).ToArray();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error getting file list: {e.Message}");
            return new string[0];
        }
    }
    
    public void ClearAllFields()
    {
        if (textField1 != null) textField1.text = "";
        if (textField2 != null) textField2.text = "";
        if (inputField1 != null) inputField1.text = "";
        
        Debug.Log("All fields cleared!");
    }
    
    public void SaveCurrentText()
    {
        SaveAllTextToJSON();
    }
    
    public void LoadSavedText()
    {
        LoadFromJSON();
    }
    
    /// <summary>
    /// Find and highlight the element that was connected to a loaded inspection
    /// </summary>
    public void HighlightConnectedElement()
    {
        // This would be called after loading to highlight the saved element
        // Implementation depends on how you want to find the element again
    }
}

/// <summary>
/// Helper class for element information
/// </summary>
public class ElementInfo
{
    public string elementName = "";
    public string elementID = "";
    public string elementProperties = "";
}

[System.Serializable]
public class MultiFieldInspectionData
{
    public string inspectionId;
    public string timestamp;
    public string captureInfo;
    public string textField1;
    public string textField2;
    public string inputField1;
    
    [Header("Element Connection")]
    public string connectedElementName;    // Name of the highlighted GameObject
    public string connectedElementID;      // RevitData ID if available
    public string elementProperties;       // Store key properties as text
}