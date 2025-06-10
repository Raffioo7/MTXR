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
        
        // Find text component in the button
        TextMeshProUGUI[] textComponents = buttonObj.GetComponentsInChildren<TextMeshProUGUI>();
        
        if (textComponents.Length > 0)
        {
            if (index < lastThreeInspections.Length)
            {
                // Display inspection info
                var fileInfo = lastThreeInspections[index];
                string displayText = CreateDisplayText(fileInfo, index + 1);
                textComponents[0].text = displayText;
                
                // Enable the button
                var button = buttonObj.GetComponent<PressableButton>();
                if (button != null) button.enabled = true;
            }
            else
            {
                // No inspection available
                textComponents[0].text = $"Button {index + 1}\nNo inspection";
                
                // Disable the button
                var button = buttonObj.GetComponent<PressableButton>();
                if (button != null) button.enabled = false;
            }
        }
    }
    
    string CreateDisplayText(InspectionFileInfo fileInfo, int buttonNumber)
    {
        // Create a nice display format
        string timeStr = "";
        if (!string.IsNullOrEmpty(fileInfo.timestamp))
        {
            try
            {
                DateTime dt = DateTime.Parse(fileInfo.timestamp);
                timeStr = dt.ToString("MMM dd, HH:mm");
            }
            catch
            {
                timeStr = fileInfo.creationTime.ToString("MMM dd, HH:mm");
            }
        }
        else
        {
            timeStr = fileInfo.creationTime.ToString("MMM dd, HH:mm");
        }
        
        string elementStr = string.IsNullOrEmpty(fileInfo.connectedElement) ? "No element" : fileInfo.connectedElement;
        
        // Truncate long element names
        if (elementStr.Length > 15)
            elementStr = elementStr.Substring(0, 12) + "...";
        
        return $"#{buttonNumber}\n{timeStr}\n{elementStr}";
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