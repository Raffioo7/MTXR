using UnityEngine;
using TMPro;
using MixedReality.Toolkit.UX;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Enhanced SimpleTextReader that can save loop system data along with text fields
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
    
    [Header("Numbering System")]
    [Tooltip("Starting number for inspections (will auto-increment)")]
    public int startingNumber = 1;
    
    [Tooltip("Number format (e.g., '000' for 001, 002, 003)")]
    public string numberFormat = "000";
    
    [Tooltip("Reset numbering system")]
    public bool resetNumbering = false;
    
    [Header("Buttons")]
    [Tooltip("Drag your save button here")]
    public GameObject saveButtonObject;
    
    [Tooltip("Drag your load button here")]
    public GameObject loadButtonObject;
    
    [Header("Element Highlighting")]
    [Tooltip("Drag your PropertyClickHandler_MRTK3 component here")]
    public PropertyClickHandler_MRTK3 propertyHandler;
    
    [Header("Loop System Integration")]
    [Tooltip("Drag your DotPlacementHandler_MRTK3 component here")]
    public DotPlacementHandler_MRTK3 dotPlacementHandler;
    
    [Tooltip("Drag your LineConnectionHandler_MRTK3 component here")]
    public LineConnectionHandler_MRTK3 lineConnectionHandler;
    
    [Tooltip("Drag your LoopAreaShader_MRTK3 component here")]
    public LoopAreaShader_MRTK3 loopAreaShader;
    
    [Tooltip("Drag your LoopSystemLoader component here")]
    public LoopSystemLoader loopSystemLoader;
    
    [Header("Load Settings")]
    [Tooltip("Filename to load (without .json extension). Leave empty to load most recent file.")]
    public string fileToLoad = "";
    
    [Header("Default Placeholder Text")]
    [Tooltip("Default placeholder text for Text Field 1")]
    public string defaultTextField1Text = "Enter your text here...";

    [Tooltip("Default placeholder text for Text Field 2")]
    public string defaultTextField2Text = "Additional notes...";

    [Tooltip("Default placeholder text for Input Field 1")]
    public string defaultInputField1Text = "Type here...";
    
    // Private variables for numbering
    private int currentInspectionNumber;
    private string numberingDataFile = "inspection_numbering.json";
    
    void Start()
    {
        // Find PropertyClickHandler if not assigned
        if (propertyHandler == null)
            propertyHandler = FindObjectOfType<PropertyClickHandler_MRTK3>();
        
        // Find loop system components if not assigned
        if (dotPlacementHandler == null)
            dotPlacementHandler = FindObjectOfType<DotPlacementHandler_MRTK3>();
        if (lineConnectionHandler == null)
            lineConnectionHandler = FindObjectOfType<LineConnectionHandler_MRTK3>();
        if (loopAreaShader == null)
            loopAreaShader = FindObjectOfType<LoopAreaShader_MRTK3>();
        if (loopSystemLoader == null)
            loopSystemLoader = FindObjectOfType<LoopSystemLoader>();
        
        // Store initial placeholders automatically
        StoreInitialPlaceholders();
        
        // Initialize numbering system
        InitializeNumberingSystem();
        
        SetupButtons();
        LogAssignedFields();
    }
    
    public void RestoreDefaultPlaceholders()
    {
        if (textField1 != null) 
            textField1.text = defaultTextField1Text;
        if (textField2 != null) 
            textField2.text = defaultTextField2Text;
        if (inputField1 != null) 
            inputField1.text = defaultInputField1Text;
    
        Debug.Log("Restored default placeholder text!");
    }

    void StoreInitialPlaceholders()
    {
        // This would run in Start() to automatically capture whatever text is already there
        if (textField1 != null && string.IsNullOrEmpty(defaultTextField1Text))
            defaultTextField1Text = textField1.text;
        if (textField2 != null && string.IsNullOrEmpty(defaultTextField2Text))
            defaultTextField2Text = textField2.text;
        if (inputField1 != null && string.IsNullOrEmpty(defaultInputField1Text))
            defaultInputField1Text = inputField1.text;
    }
    
    void OnValidate()
    {
        // Reset numbering when checkbox is checked in inspector
        if (resetNumbering)
        {
            resetNumbering = false;
            if (Application.isPlaying)
            {
                ResetNumberingSystem();
            }
        }
    }
    
    void InitializeNumberingSystem()
    {
        try
        {
            string numberingPath = Path.Combine(Application.persistentDataPath, numberingDataFile);
            
            if (File.Exists(numberingPath))
            {
                string numberingJson = File.ReadAllText(numberingPath);
                NumberingData data = JsonUtility.FromJson<NumberingData>(numberingJson);
                currentInspectionNumber = data.nextNumber;
                Debug.Log($"Loaded numbering system - Next inspection number: {currentInspectionNumber.ToString(numberFormat)}");
            }
            else
            {
                // First time setup - scan existing files to determine next number
                currentInspectionNumber = GetNextNumberFromExistingFiles();
                SaveNumberingData();
                Debug.Log($"Initialized numbering system - Next inspection number: {currentInspectionNumber.ToString(numberFormat)}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error initializing numbering system: {e.Message}");
            currentInspectionNumber = startingNumber;
        }
    }
    
    int GetNextNumberFromExistingFiles()
    {
        try
        {
            string basePath = Application.persistentDataPath;
            string[] files = Directory.GetFiles(basePath, $"{fileName}_*.json");
            
            int highestNumber = startingNumber - 1;
            
            foreach (string file in files)
            {
                try
                {
                    string jsonContent = File.ReadAllText(file);
                    EnhancedInspectionData data = JsonUtility.FromJson<EnhancedInspectionData>(jsonContent);
                    
                    if (data != null && !string.IsNullOrEmpty(data.inspectionNumber))
                    {
                        if (int.TryParse(data.inspectionNumber, out int fileNumber))
                        {
                            if (fileNumber > highestNumber)
                            {
                                highestNumber = fileNumber;
                            }
                        }
                    }
                }
                catch
                {
                    // Skip files that can't be parsed
                    continue;
                }
            }
            
            int nextNumber = highestNumber + 1;
            Debug.Log($"Scanned {files.Length} existing files, highest number found: {highestNumber}, next will be: {nextNumber}");
            return nextNumber;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error scanning existing files: {e.Message}");
            return startingNumber;
        }
    }
    
    void SaveNumberingData()
    {
        try
        {
            NumberingData data = new NumberingData
            {
                nextNumber = currentInspectionNumber,
                lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            
            string json = JsonUtility.ToJson(data, true);
            string numberingPath = Path.Combine(Application.persistentDataPath, numberingDataFile);
            File.WriteAllText(numberingPath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error saving numbering data: {e.Message}");
        }
    }
    
    public void ResetNumberingSystem()
    {
        currentInspectionNumber = startingNumber;
        SaveNumberingData();
        Debug.Log($"Numbering system reset to {startingNumber}");
    }
    
    public int GetNextInspectionNumber()
    {
        return currentInspectionNumber;
    }
    
    public string GetNextInspectionNumberFormatted()
    {
        return currentInspectionNumber.ToString(numberFormat);
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
                saveButton.OnClicked.AddListener(SaveAllTextWithLoopsToJSON);
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
        
        // Log loop system connections
        Debug.Log("=== LOOP SYSTEM CONNECTIONS ===");
        Debug.Log($"DotPlacementHandler: {(dotPlacementHandler != null ? "Connected" : "Not Found")}");
        Debug.Log($"LineConnectionHandler: {(lineConnectionHandler != null ? "Connected" : "Not Found")}");
        Debug.Log($"LoopAreaShader: {(loopAreaShader != null ? "Connected" : "Not Found")}");
        Debug.Log($"LoopSystemLoader: {(loopSystemLoader != null ? "Connected" : "Not Found")}");
        
        Debug.Log($"Text reader ready. Next inspection number: {GetNextInspectionNumberFormatted()}");
    }
    
    // Original method - keeps for backward compatibility
    public void SaveAllTextToJSON()
    {
        SaveAllTextWithLoopsToJSON();
    }
    
    // Enhanced method that includes loop system data
    public void SaveAllTextWithLoopsToJSON()
    {
        Debug.Log("=== CAPTURING ALL TEXT FIELDS AND LOOP SYSTEM ===");
        
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
        
        // Get loop system data
        LoopSystemData loopData = GetLoopSystemData();
        
        SaveToEnhancedJSON(text1, text2, input1, elementInfo, loopData);
    }
    
    LoopSystemData GetLoopSystemData()
    {
        var loopData = new LoopSystemData();
        
        if (dotPlacementHandler != null)
        {
            // Get all loop positions
            var allLoops = dotPlacementHandler.GetAllLoopPositions();
            loopData.loops = new List<LoopData>();
            
            int completedLoops = dotPlacementHandler.GetCompletedLoopCount();
            
            for (int i = 0; i < allLoops.Count; i++)
            {
                var loopPositions = allLoops[i];
                var loopInfo = new LoopData
                {
                    loopIndex = i,
                    isCompleted = i < completedLoops,
                    dotCount = loopPositions.Count,
                    positions = loopPositions.ToArray()
                };
                
                loopData.loops.Add(loopInfo);
            }
            
            loopData.totalCompletedLoops = completedLoops;
            loopData.currentLoopDotCount = dotPlacementHandler.GetCurrentLoopDotCount();
            
            Debug.Log($"Captured {loopData.loops.Count} loops ({completedLoops} completed, {loopData.currentLoopDotCount} dots in current)");
        }
        
        // Get line information
        if (lineConnectionHandler != null)
        {
            loopData.totalLineCount = lineConnectionHandler.GetLineCount();
            var lineSegments = lineConnectionHandler.GetAllLineSegments();
            loopData.lineSegmentCount = lineSegments.Count;
            
            Debug.Log($"Captured {loopData.totalLineCount} lines with {loopData.lineSegmentCount} segments");
        }
        
        // Get shaded area information
        if (loopAreaShader != null)
        {
            loopData.shadedAreaCount = loopAreaShader.GetShadedAreaCount();
            
            Debug.Log($"Captured {loopData.shadedAreaCount} shaded areas");
        }
        
        loopData.captureTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        
        return loopData;
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
            
            // Try to load as enhanced data first, then fall back to legacy format
            EnhancedInspectionData loadedData = JsonUtility.FromJson<EnhancedInspectionData>(jsonContent);
            
            if (loadedData == null)
            {
                Debug.LogError("Failed to parse JSON data");
                return;
            }
            
            PopulateFieldsFromEnhancedData(loadedData);
            
            Debug.Log($"SUCCESS! Loaded enhanced inspection data from {Path.GetFileName(filePath)}");
            Debug.Log($"Inspection Number: {loadedData.inspectionNumber}");
            Debug.Log($"Inspection ID: {loadedData.inspectionId}");
            Debug.Log($"Timestamp: {loadedData.timestamp}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load JSON: {e.Message}");
        }
    }
    
    void PopulateFieldsFromEnhancedData(EnhancedInspectionData data)
    {
        Debug.Log("=== POPULATING FIELDS FROM ENHANCED DATA ===");
        
        // Populate text fields
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
        
        // Restore loop system if available
        if (data.loopSystemData != null && data.loopSystemData.loops != null && data.loopSystemData.loops.Count > 0)
        {
            Debug.Log($"Found {data.loopSystemData.loops.Count} loops in saved data");
            
            if (loopSystemLoader != null)
            {
                Debug.Log("Restoring loop system from saved data...");
                loopSystemLoader.LoadLoopSystem(data.loopSystemData);
            }
            else
            {
                Debug.LogWarning("LoopSystemLoader not found - cannot restore loops. Add LoopSystemLoader component to enable loop restoration.");
            }
        }
        else
        {
            Debug.Log("No loop data found in saved inspection");
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
    
    void SaveToEnhancedJSON(string textField1Content, string textField2Content, string inputField1Content, ElementInfo elementInfo, LoopSystemData loopSystemData)
    {
        // Get the current inspection number and increment for next time
        string inspectionNumber = currentInspectionNumber.ToString(numberFormat);
        
        var inspectionData = new EnhancedInspectionData
        {
            // Basic inspection info
            inspectionNumber = inspectionNumber,
            inspectionId = Guid.NewGuid().ToString(),
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            
            // Text field data
            textField1 = textField1Content,
            textField2 = textField2Content,
            inputField1 = inputField1Content,
            
            // Element connection data
            connectedElementName = elementInfo.elementName,
            connectedElementID = elementInfo.elementID,
            elementProperties = elementInfo.elementProperties,
            
            // Loop system data
            loopSystemData = loopSystemData,
            
            // Enhanced capture info
            captureInfo = $"Enhanced Inspection #{inspectionNumber} - Captured {DateTime.Now:HH:mm:ss} - Fields: {(string.IsNullOrEmpty(textField1Content) ? 0 : 1) + (string.IsNullOrEmpty(textField2Content) ? 0 : 1) + (string.IsNullOrEmpty(inputField1Content) ? 0 : 1)} with data, Element: {elementInfo.elementName}, Loops: {loopSystemData.loops?.Count ?? 0} ({loopSystemData.totalCompletedLoops} completed)"
        };
        
        try
        {
            string json = JsonUtility.ToJson(inspectionData, true);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fullFileName = $"{fileName}_{inspectionNumber}_{timestamp}.json";
            string filePath = Path.Combine(Application.persistentDataPath, fullFileName);
            
            File.WriteAllText(filePath, json);
            
            // Increment the number for next inspection
            currentInspectionNumber++;
            SaveNumberingData();
            
            Debug.Log($"SUCCESS! Saved enhanced inspection #{inspectionNumber} to: {filePath}");
            Debug.Log($"Text Field 1: '{textField1Content}'");
            Debug.Log($"Text Field 2: '{textField2Content}'");
            Debug.Log($"Input Field 1: '{inputField1Content}'");
            Debug.Log($"Connected Element: '{elementInfo.elementName}' (ID: {elementInfo.elementID})");
            Debug.Log($"Loop Data: {loopSystemData.loops?.Count ?? 0} loops, {loopSystemData.totalCompletedLoops} completed, {loopSystemData.totalLineCount} lines, {loopSystemData.shadedAreaCount} shaded areas");
            Debug.Log($"Next inspection will be: {currentInspectionNumber.ToString(numberFormat)}");
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
        SaveAllTextWithLoopsToJSON();
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

/// <summary>
/// Data structure for tracking inspection numbering
/// </summary>
[System.Serializable]
public class NumberingData
{
    public int nextNumber;
    public string lastUpdated;
}

/// <summary>
/// Enhanced data structure that includes loop system information
/// </summary>
[System.Serializable]
public class EnhancedInspectionData
{
    [Header("Inspection Info")]
    public string inspectionNumber;        // Auto-incrementing number (001, 002, etc.)
    public string inspectionId;            // Unique GUID
    public string timestamp;
    public string captureInfo;
    
    [Header("Field Data")]
    public string textField1;
    public string textField2;
    public string inputField1;
    
    [Header("Element Connection")]
    public string connectedElementName;    // Name of the highlighted GameObject
    public string connectedElementID;      // RevitData ID if available
    public string elementProperties;       // Store key properties as text
    
    [Header("Loop System Data")]
    public LoopSystemData loopSystemData;  // Complete loop system state
}

/// <summary>
/// Legacy data structure for backward compatibility
/// </summary>
[System.Serializable]
public class MultiFieldInspectionData
{
    [Header("Inspection Info")]
    public string inspectionNumber;
    public string inspectionId;
    public string timestamp;
    public string captureInfo;
    
    [Header("Field Data")]
    public string textField1;
    public string textField2;
    public string inputField1;
    
    [Header("Element Connection")]
    public string connectedElementName;
    public string connectedElementID;
    public string elementProperties;
}

/// <summary>
/// Complete loop system data structure
/// </summary>
[System.Serializable]
public class LoopSystemData
{
    [Header("Loop Information")]
    public List<LoopData> loops = new List<LoopData>();
    public int totalCompletedLoops;
    public int currentLoopDotCount;
    
    [Header("Line Information")]
    public int totalLineCount;
    public int lineSegmentCount;
    
    [Header("Shaded Area Information")]
    public int shadedAreaCount;
    
    [Header("Metadata")]
    public string captureTimestamp;
}

/// <summary>
/// Individual loop data structure
/// </summary>
[System.Serializable]
public class LoopData
{
    public int loopIndex;
    public bool isCompleted;
    public int dotCount;
    public Vector3[] positions;
    
    [Header("Loop Metadata")]
    public string loopName;
    public float totalLength;
    public float estimatedArea;
    
    public LoopData()
    {
        positions = new Vector3[0];
    }
}