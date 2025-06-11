using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MixedReality.Toolkit.UX;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Simple inspection loader with three fixed buttons for the last three inspections
/// Now filters by currently highlighted element
/// </summary>
public class SimpleInspectionLoader : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Panel that contains the inspection buttons")]
    public GameObject inspectionPanel;
    
    [Tooltip("Button for the most recent inspection")]
    public GameObject button1;
    
    [Tooltip("Button for the second most recent inspection")]
    public GameObject button2;
    
    [Tooltip("Button for the third most recent inspection")]
    public GameObject button3;
    
    [Tooltip("Button to toggle the inspection panel")]
    public GameObject togglePanelButton;
    
    [Header("Connected Systems")]
    [Tooltip("Reference to your SimpleTextReader script")]
    public SimpleTextReader textReader;
    
    [Tooltip("Reference to your PropertyClickHandler_MRTK3 script")]
    public PropertyClickHandler_MRTK3 propertyClickHandler;
    
    [Header("Settings")]
    [Tooltip("Auto-refresh when panel opens")]
    public bool autoRefreshOnOpen = true;
    
    [Tooltip("Show newest first")]
    public bool newestFirst = true;
    
    [Tooltip("Enable debug logging")]
    public bool debugMode = true;
    
    [Header("Display Settings")]
    [Tooltip("Date format for button display")]
    public DateFormat dateFormat = DateFormat.MonthDayYear;
    
    [Tooltip("Show inspection ID if available, otherwise use filename number")]
    public bool preferInspectionId = true;
    
    public enum DateFormat
    {
        MonthDayYear,     // Dec 15, 2024
        DayMonthYear,     // 15 Dec 2024  
        ShortDate,        // 12/15/24
        YearMonthDay,     // 2024-12-15
        MonthDay          // Dec 15
    }
    
    private InspectionFileInfo[] lastThreeInspections;
    private bool isPanelVisible = false;
    private string currentHighlightedElementName = "";
    
    void Start()
    {
        // Find SimpleTextReader if not assigned
        if (textReader == null)
            textReader = FindObjectOfType<SimpleTextReader>();
        
        // Find PropertyClickHandler if not assigned
        if (propertyClickHandler == null)
            propertyClickHandler = FindObjectOfType<PropertyClickHandler_MRTK3>();
        
        // Hide panel initially
        if (inspectionPanel != null)
            inspectionPanel.SetActive(false);
        
        // Setup buttons
        SetupButtons();
        
        // Load initial data (will show "No element selected" state)
        RefreshInspections();
    }
    
    void Update()
    {
        // Check if highlighted element has changed
        string newHighlightedElement = GetCurrentHighlightedElementName();
        
        if (newHighlightedElement != currentHighlightedElementName)
        {
            if (debugMode)
            {
                Debug.Log($"=== ELEMENT SELECTION CHANGED ===");
                Debug.Log($"Previous: '{currentHighlightedElementName}'");
                Debug.Log($"New: '{newHighlightedElement}'");
            }
            
            currentHighlightedElementName = newHighlightedElement;
            
            // Refresh inspections for the new element
            RefreshInspections();
        }
    }
    
    /// <summary>
    /// Gets the name of the currently highlighted element from PropertyClickHandler
    /// </summary>
    string GetCurrentHighlightedElementName()
    {
        if (propertyClickHandler == null)
            return "";
        
        // Get the currently selected object from PropertyClickHandler
        GameObject currentlySelected = GetCurrentlySelectedObject();
        
        if (currentlySelected == null)
            return "";
        
        // Get the RevitData component to find the element name
        RevitData revitData = currentlySelected.GetComponent<RevitData>();
        if (revitData != null)
        {
            // Try to get a meaningful element name from RevitData
            // You might need to adjust this based on your RevitData structure
            string elementName = GetElementNameFromRevitData(revitData);
            return elementName;
        }
        
        // Fallback to GameObject name
        return currentlySelected.name;
    }
    
    /// <summary>
    /// Uses reflection to get the currently selected object from PropertyClickHandler
    /// </summary>
    GameObject GetCurrentlySelectedObject()
    {
        if (propertyClickHandler == null)
            return null;
        
        // Use reflection to access the private currentlySelected field
        var field = typeof(PropertyClickHandler_MRTK3).GetField("currentlySelected", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (field != null)
        {
            return field.GetValue(propertyClickHandler) as GameObject;
        }
        
        return null;
    }
    
    /// <summary>
    /// Extracts a meaningful element name from RevitData
    /// Adjust this method based on your RevitData properties
    /// </summary>
    string GetElementNameFromRevitData(RevitData revitData)
    {
        // Try to get element name from various possible properties
        // You'll need to adjust these based on your actual RevitData structure
        
        // Method 1: Try to get from display properties
        var displayProperties = revitData.GetDisplayProperties(revitData.gameObject);
        
        if (debugMode)
        {
            Debug.Log("=== DEBUGGING ELEMENT NAME EXTRACTION ===");
            Debug.Log($"GameObject: {revitData.gameObject.name}");
            Debug.Log($"Found {displayProperties.Count} display properties:");
            foreach (var prop in displayProperties)
            {
                Debug.Log($"  - {prop.key}: '{prop.value}'");
            }
        }
        
        // Look for common element identifier properties
        foreach (var prop in displayProperties)
        {
            if (prop.key.ToLower().Contains("name") || 
                prop.key.ToLower().Contains("element") ||
                prop.key.ToLower().Contains("id"))
            {
                if (!string.IsNullOrEmpty(prop.value) && prop.value != "null")
                {
                    if (debugMode)
                        Debug.Log($"Selected element name from property '{prop.key}': '{prop.value}'");
                    return prop.value;
                }
            }
        }
        
        // Method 2: Fallback to GameObject name
        if (debugMode)
            Debug.Log($"Using GameObject name as fallback: '{revitData.gameObject.name}'");
        return revitData.gameObject.name;
    }
    
    void SetupButtons()
    {
        // Setup toggle button
        if (togglePanelButton != null)
        {
            var toggleButton = togglePanelButton.GetComponent<PressableButton>();
            if (toggleButton == null)
                toggleButton = togglePanelButton.GetComponentInChildren<PressableButton>();
            
            if (toggleButton != null)
            {
                toggleButton.OnClicked.AddListener(TogglePanel);
                Debug.Log("Toggle panel button connected");
            }
        }
        
        // Setup inspection buttons
        SetupInspectionButton(button1, 0);
        SetupInspectionButton(button2, 1);
        SetupInspectionButton(button3, 2);
    }
    
    void SetupInspectionButton(GameObject buttonObj, int index)
    {
        if (buttonObj == null) return;
        
        var button = buttonObj.GetComponent<PressableButton>();
        if (button == null)
            button = buttonObj.GetComponentInChildren<PressableButton>();
        
        if (button != null)
        {
            button.OnClicked.AddListener(() => LoadInspection(index));
            Debug.Log($"Inspection button {index + 1} connected");
        }
    }
    
    public void TogglePanel()
    {
        isPanelVisible = !isPanelVisible;
        
        if (inspectionPanel != null)
        {
            inspectionPanel.SetActive(isPanelVisible);
            
            if (isPanelVisible)
            {
                if (autoRefreshOnOpen)
                    RefreshInspections();
                Debug.Log("Inspection panel opened");
            }
            else
            {
                Debug.Log("Inspection panel closed");
            }
        }
    }
    
    public void RefreshInspections()
    {
        Debug.Log($"Refreshing inspections for element: {currentHighlightedElementName}");
        
        // Get inspections for the currently highlighted element
        lastThreeInspections = GetLastThreeInspectionsForElement(currentHighlightedElementName);
        
        // Update button displays
        UpdateButtonDisplays();
        
        Debug.Log($"Loaded {lastThreeInspections.Length} recent inspections for {currentHighlightedElementName}");
    }
    
    InspectionFileInfo[] GetLastThreeInspectionsForElement(string elementName)
    {
        if (debugMode)
        {
            Debug.Log("=== DEBUGGING INSPECTION FILTERING ===");
            Debug.Log($"Looking for inspections for element: '{elementName}'");
        }
        
        try
        {
            string basePath = Application.persistentDataPath;
            string[] files = Directory.GetFiles(basePath, "bridge_inspection_*.json");
            
            if (debugMode)
                Debug.Log($"Found {files.Length} inspection files in total");
            
            var fileInfos = new List<InspectionFileInfo>();
            
            foreach (string filePath in files)
            {
                try
                {
                    var fileInfo = new InspectionFileInfo
                    {
                        filePath = filePath,
                        fileName = Path.GetFileName(filePath),
                        creationTime = File.GetCreationTime(filePath)
                    };
                    
                    // Try to read basic info from the file
                    string jsonContent = File.ReadAllText(filePath);
                    var inspectionData = JsonUtility.FromJson<MultiFieldInspectionData>(jsonContent);
                    
                    if (inspectionData != null)
                    {
                        fileInfo.timestamp = inspectionData.timestamp;
                        fileInfo.connectedElement = inspectionData.connectedElementName ?? "No element";
                        fileInfo.inspectionId = inspectionData.inspectionId;
                        
                        if (debugMode)
                        {
                            Debug.Log($"File: {fileInfo.fileName}");
                            Debug.Log($"  Connected Element: '{fileInfo.connectedElement}'");
                            Debug.Log($"  Timestamp: {fileInfo.timestamp}");
                            Debug.Log($"  Inspection ID: {fileInfo.inspectionId}");
                        }
                        
                        // Create summary of content
                        var contentParts = new List<string>();
                        if (!string.IsNullOrEmpty(inspectionData.textField1)) contentParts.Add("Text1");
                        if (!string.IsNullOrEmpty(inspectionData.textField2)) contentParts.Add("Text2");
                        if (!string.IsNullOrEmpty(inspectionData.inputField1)) contentParts.Add("Input1");
                        
                        fileInfo.contentSummary = contentParts.Count > 0 ? string.Join(", ", contentParts) : "Empty";
                        
                        // FILTER: Only include inspections for the specified element
                        if (string.IsNullOrEmpty(elementName))
                        {
                            if (debugMode)
                                Debug.Log($"  SKIPPED: No element name provided");
                            continue;
                        }
                        
                        // Try different matching strategies
                        bool isMatch = false;
                        
                        // Strategy 1: Exact match (case insensitive)
                        if (fileInfo.connectedElement.Equals(elementName, StringComparison.OrdinalIgnoreCase))
                        {
                            isMatch = true;
                            if (debugMode)
                                Debug.Log($"  MATCHED: Exact match");
                        }
                        // Strategy 2: Contains match (in case of partial names)
                        else if (fileInfo.connectedElement.ToLower().Contains(elementName.ToLower()) ||
                                elementName.ToLower().Contains(fileInfo.connectedElement.ToLower()))
                        {
                            isMatch = true;
                            if (debugMode)
                                Debug.Log($"  MATCHED: Contains match");
                        }
                        
                        if (isMatch)
                        {
                            fileInfos.Add(fileInfo);
                            if (debugMode)
                                Debug.Log($"  ADDED to results");
                        }
                        else
                        {
                            if (debugMode)
                                Debug.Log($"  SKIPPED: No match ('{fileInfo.connectedElement}' != '{elementName}')");
                        }
                    }
                    else
                    {
                        if (debugMode)
                            Debug.LogWarning($"Could not parse JSON in file {Path.GetFileName(filePath)}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Could not read inspection file {Path.GetFileName(filePath)}: {e.Message}");
                }
            }
            
            if (debugMode)
                Debug.Log($"Found {fileInfos.Count} matching inspections for element '{elementName}'");
            
            // Sort by creation time and take the last 3
            if (newestFirst)
                return fileInfos.OrderByDescending(f => f.creationTime).Take(3).ToArray();
            else
                return fileInfos.OrderBy(f => f.creationTime).TakeLast(3).ToArray();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error getting inspection files: {e.Message}");
            return new InspectionFileInfo[0];
        }
    }
    
    void UpdateButtonDisplays()
    {
        UpdateButtonDisplay(button1, 0);
        UpdateButtonDisplay(button2, 1);
        UpdateButtonDisplay(button3, 2);
    }
    
    void UpdateButtonDisplay(GameObject buttonObj, int index)
    {
        if (buttonObj == null) return;
        
        // Find TextMeshPro component - try both UI and 3D versions
        TextMeshProUGUI textComponentUI = null;
        TMPro.TextMeshPro textComponent3D = null;
        
        // Method 1: Search for UI version
        TextMeshProUGUI[] allTextComponentsUI = buttonObj.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (allTextComponentsUI.Length > 0)
        {
            textComponentUI = allTextComponentsUI[0];
            Debug.Log($"Found TextMeshProUGUI at: {GetGameObjectPath(textComponentUI.gameObject)}");
        }
        
        // Method 2: Search for 3D version (which is what MRTK3 is using)
        TMPro.TextMeshPro[] allTextComponents3D = buttonObj.GetComponentsInChildren<TMPro.TextMeshPro>(true);
        if (allTextComponents3D.Length > 0)
        {
            textComponent3D = allTextComponents3D[0];
            Debug.Log($"Found TextMeshPro 3D at: {GetGameObjectPath(textComponent3D.gameObject)}");
        }
        
        // Update the text - prioritize 3D version since that's what MRTK3 uses
        if (textComponent3D != null || textComponentUI != null)
        {
            if (index < lastThreeInspections.Length)
            {
                // Display inspection info in concise format
                var fileInfo = lastThreeInspections[index];
                string displayText = CreateConciseDisplayText(fileInfo);
                
                // Set text on whichever component we found
                if (textComponent3D != null)
                {
                    textComponent3D.text = displayText;
                }
                else if (textComponentUI != null)
                {
                    textComponentUI.text = displayText;
                }
                
                // Enable the button
                var button = buttonObj.GetComponent<PressableButton>();
                if (button != null) button.enabled = true;
                
                Debug.Log($"Updated button {index + 1} text: {displayText}");
            }
            else
            {
                // No inspection available for this element
                string noInspectionText = string.IsNullOrEmpty(currentHighlightedElementName) 
                    ? "Select Element" 
                    : "No Inspections";
                
                if (textComponent3D != null)
                {
                    textComponent3D.text = noInspectionText;
                }
                else if (textComponentUI != null)
                {
                    textComponentUI.text = noInspectionText;
                }
                
                // Disable the button
                var button = buttonObj.GetComponent<PressableButton>();
                if (button != null) button.enabled = false;
            }
        }
        else
        {
            Debug.LogWarning($"Could not find any TextMeshPro component in button {index + 1}");
            Debug.Log($"Button {index + 1} full structure:\n{GetFullButtonStructure(buttonObj)}");
        }
    }
    
    // Create concise display format: "Insp. ID 004 - Jun 10, 2025"
    string CreateConciseDisplayText(InspectionFileInfo fileInfo)
    {
        string inspectionNumber = ExtractInspectionNumber(fileInfo);
        string dateStr = FormatDate(fileInfo);
        
        return $"Insp. ID {inspectionNumber} - {dateStr}";
    }
    
    // Helper to get full path of a GameObject
    string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        
        return path;
    }
    
    // More detailed structure debug
    string GetFullButtonStructure(GameObject buttonObj, int depth = 0, int maxDepth = 5)
    {
        if (depth > maxDepth) return "";
        
        string indent = new string(' ', depth * 2);
        string structure = $"{indent}{buttonObj.name}";
        
        // Add component info
        Component[] components = buttonObj.GetComponents<Component>();
        foreach (Component comp in components)
        {
            if (comp is TextMeshProUGUI || comp is TMPro.TextMeshPro || comp is PressableButton)
            {
                structure += $" [{comp.GetType().Name}]";
            }
        }
        
        structure += "\n";
        
        // Add children
        for (int i = 0; i < buttonObj.transform.childCount && depth < maxDepth; i++)
        {
            structure += GetFullButtonStructure(buttonObj.transform.GetChild(i).gameObject, depth + 1, maxDepth);
        }
        
        return structure;
    }
    
    string CreateDisplayText(InspectionFileInfo fileInfo, int buttonNumber)
    {
        // Extract inspection number from filename or use a counter
        string inspectionNumber = ExtractInspectionNumber(fileInfo);
        
        // Create a nice date format based on settings
        string dateStr = FormatDate(fileInfo);
        
        return $"Inspection #{inspectionNumber}\n{dateStr}";
    }
    
    string FormatDate(InspectionFileInfo fileInfo)
    {
        DateTime dateToFormat;
        
        // Try to use timestamp first, then creation time
        if (!string.IsNullOrEmpty(fileInfo.timestamp))
        {
            try
            {
                dateToFormat = DateTime.Parse(fileInfo.timestamp);
            }
            catch
            {
                dateToFormat = fileInfo.creationTime;
            }
        }
        else
        {
            dateToFormat = fileInfo.creationTime;
        }
        
        // Format based on selected format
        switch (dateFormat)
        {
            case DateFormat.MonthDayYear:
                return dateToFormat.ToString("MMM dd, yyyy");
            case DateFormat.DayMonthYear:
                return dateToFormat.ToString("dd MMM yyyy");
            case DateFormat.ShortDate:
                return dateToFormat.ToString("MM/dd/yy");
            case DateFormat.YearMonthDay:
                return dateToFormat.ToString("yyyy-MM-dd");
            case DateFormat.MonthDay:
                return dateToFormat.ToString("MMM dd");
            default:
                return dateToFormat.ToString("MMM dd, yyyy");
        }
    }
    
    string ExtractInspectionNumber(InspectionFileInfo fileInfo)
    {
        // Method 1: Try to get inspection number from the JSON data if preferred
        if (preferInspectionId && !string.IsNullOrEmpty(fileInfo.inspectionId))
        {
            return fileInfo.inspectionId;
        }
        
        // Method 2: Extract number from filename - handle new format with number_timestamp
        // Example: "bridge_inspection_004_20250610_180910.json" -> "004"
        string fileName = fileInfo.fileName;
        if (fileName.Contains("bridge_inspection_"))
        {
            string afterPrefix = fileName.Replace("bridge_inspection_", "").Replace(".json", "");
            
            // Split by underscores and take the first part (the inspection number)
            string[] parts = afterPrefix.Split('_');
            if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]))
            {
                return parts[0]; // This should be the inspection number like "004"
            }
        }
        
        // Method 3: Use inspection ID as fallback
        if (!string.IsNullOrEmpty(fileInfo.inspectionId))
        {
            return fileInfo.inspectionId;
        }
        
        // Method 4: Use timestamp as unique identifier
        if (!string.IsNullOrEmpty(fileInfo.timestamp))
        {
            try
            {
                DateTime dt = DateTime.Parse(fileInfo.timestamp);
                return dt.ToString("yyyyMMdd-HHmm");
            }
            catch
            {
                // Fallback to creation time
                return fileInfo.creationTime.ToString("yyyyMMdd-HHmm");
            }
        }
        
        // Method 5: Fallback to creation time
        return fileInfo.creationTime.ToString("yyyyMMdd-HHmm");
    }
    
    void LoadInspection(int index)
    {
        if (index >= lastThreeInspections.Length)
        {
            Debug.LogWarning($"No inspection available for button {index + 1}");
            return;
        }
        
        var fileInfo = lastThreeInspections[index];
        Debug.Log($"Loading inspection {index + 1}: {fileInfo.fileName}");
        
        if (textReader != null)
        {
            // Set the file to load and trigger the load
            textReader.LoadSpecificFile(fileInfo.fileName);
            
            // Close the panel after loading
            if (inspectionPanel != null)
            {
                inspectionPanel.SetActive(false);
                isPanelVisible = false;
            }
            
            Debug.Log($"Loaded inspection from {fileInfo.timestamp} connected to {fileInfo.connectedElement}");
        }
        else
        {
            Debug.LogError("SimpleTextReader not found!");
        }
    }
    
    /// <summary>
    /// Public method to refresh inspections - can be called from other scripts
    /// </summary>
    public void UpdateInspections()
    {
        RefreshInspections();
    }
    
    /// <summary>
    /// Public method to show the panel
    /// </summary>
    public void ShowPanel()
    {
        if (!isPanelVisible)
            TogglePanel();
    }
    
    /// <summary>
    /// Public method to hide the panel
    /// </summary>
    public void HidePanel()
    {
        if (isPanelVisible)
            TogglePanel();
    }
    
    /// <summary>
    /// Get info about a specific inspection (0-2)
    /// </summary>
    public InspectionFileInfo GetInspectionInfo(int index)
    {
        if (index >= 0 && index < lastThreeInspections.Length)
            return lastThreeInspections[index];
        return null;
    }
    
    /// <summary>
    /// Check if an inspection is available at the given index
    /// </summary>
    public bool HasInspectionAt(int index)
    {
        return index >= 0 && index < lastThreeInspections.Length;
    }
    
    /// <summary>
    /// Get the currently highlighted element name (for debugging)
    /// </summary>
    public string GetCurrentElementName()
    {
        return currentHighlightedElementName;
    }
}

/// <summary>
/// Information about an inspection file
/// </summary>
[System.Serializable]
public class InspectionFileInfo
{
    public string filePath;
    public string fileName;
    public DateTime creationTime;
    public string timestamp;
    public string connectedElement;
    public string inspectionId;
    public string contentSummary;
}