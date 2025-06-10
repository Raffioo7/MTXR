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
    
    [Header("Settings")]
    [Tooltip("Auto-refresh when panel opens")]
    public bool autoRefreshOnOpen = true;
    
    [Tooltip("Show newest first")]
    public bool newestFirst = true;
    
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
    
    void Start()
    {
        // Find SimpleTextReader if not assigned
        if (textReader == null)
            textReader = FindObjectOfType<SimpleTextReader>();
        
        // Hide panel initially
        if (inspectionPanel != null)
            inspectionPanel.SetActive(false);
        
        // Setup buttons
        SetupButtons();
        
        // Load initial data
        RefreshInspections();
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
        Debug.Log("Refreshing last three inspections...");
        
        // Get all inspection files
        lastThreeInspections = GetLastThreeInspections();
        
        // Update button displays
        UpdateButtonDisplays();
        
        Debug.Log($"Loaded {lastThreeInspections.Length} recent inspections");
    }
    
    InspectionFileInfo[] GetLastThreeInspections()
    {
        try
        {
            string basePath = Application.persistentDataPath;
            string[] files = Directory.GetFiles(basePath, "bridge_inspection_*.json");
            
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
                        
                        // Create summary of content
                        var contentParts = new List<string>();
                        if (!string.IsNullOrEmpty(inspectionData.textField1)) contentParts.Add("Text1");
                        if (!string.IsNullOrEmpty(inspectionData.textField2)) contentParts.Add("Text2");
                        if (!string.IsNullOrEmpty(inspectionData.inputField1)) contentParts.Add("Input1");
                        
                        fileInfo.contentSummary = contentParts.Count > 0 ? string.Join(", ", contentParts) : "Empty";
                    }
                    
                    fileInfos.Add(fileInfo);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Could not read inspection file {Path.GetFileName(filePath)}: {e.Message}");
                }
            }
            
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
                // No inspection available
                string noInspectionText = "No Inspection";
                
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