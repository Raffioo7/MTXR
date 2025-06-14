using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[System.Serializable]
public class FolderPanel
{
    [Header("Panel Configuration")]
    public string folderName;
    public string folderPath; // Relative to StreamingAssets or Resources
    public GameObject panelObject;
    public TMP_Dropdown dropdown;
    
    [Header("Display Components")]
    public RawImage imageDisplay; // For PNG/JPG files
    public GameObject pdfDisplay; // Custom PDF display object
    public TextMeshProUGUI statusText;
    
    [Header("File Types")]
    public bool includePDF = true;
    public bool includePNG = true;
    public bool includeJPG = true;
    
    [HideInInspector]
    public List<FileInfo> availableFiles = new List<FileInfo>();
}

[System.Serializable]
public class FileInfo
{
    public string fileName;
    public string filePath;
    public FileType fileType;
    
    public FileInfo(string name, string path, FileType type)
    {
        fileName = name;
        filePath = path;
        fileType = type;
    }
}

public enum FileType
{
    PDF,
    PNG,
    JPG
}

public class FilePanelManager : MonoBehaviour
{
    [Header("Folder Panels Configuration")]
    public List<FolderPanel> folderPanels = new List<FolderPanel>();
    
    [Header("Settings")]
    public bool useStreamingAssets = true; // If false, uses Resources folder
    public bool refreshOnStart = true;
    
    private Dictionary<TMP_Dropdown, FolderPanel> dropdownToPanel = new Dictionary<TMP_Dropdown, FolderPanel>();
    
    void Start()
    {
        if (refreshOnStart)
        {
            InitializePanels();
        }
    }
    
    public void InitializePanels()
    {
        dropdownToPanel.Clear();
        
        foreach (var panel in folderPanels)
        {
            SetupPanel(panel);
        }
    }
    
    void SetupPanel(FolderPanel panel)
    {
        if (panel.dropdown == null)
        {
            Debug.LogError($"Dropdown not assigned for panel: {panel.folderName}");
            return;
        }
        
        // Clear existing options
        panel.dropdown.ClearOptions();
        panel.availableFiles.Clear();
        
        // Add to dictionary for callback reference
        dropdownToPanel[panel.dropdown] = panel;
        
        // Load files from folder
        LoadFilesFromFolder(panel);
        
        // Populate dropdown
        PopulateDropdown(panel);
        
        // Setup dropdown callback
        panel.dropdown.onValueChanged.RemoveAllListeners();
        panel.dropdown.onValueChanged.AddListener((index) => OnDropdownValueChanged(panel.dropdown, index));
        
        // Update status
        UpdateStatusText(panel, $"Loaded {panel.availableFiles.Count} files");
    }
    
    void LoadFilesFromFolder(FolderPanel panel)
    {
        string fullPath;
        
        if (useStreamingAssets)
        {
            fullPath = Path.Combine(Application.streamingAssetsPath, panel.folderPath);
        }
        else
        {
            fullPath = Path.Combine(Application.dataPath, "Resources", panel.folderPath);
        }
        
        if (!Directory.Exists(fullPath))
        {
            Debug.LogWarning($"Directory does not exist: {fullPath}");
            UpdateStatusText(panel, "Folder not found");
            return;
        }
        
        // Get all files in directory
        string[] files = Directory.GetFiles(fullPath);
        
        foreach (string filePath in files)
        {
            string fileName = Path.GetFileName(filePath);
            string extension = Path.GetExtension(filePath).ToLower();
            
            FileType fileType;
            bool shouldInclude = false;
            
            switch (extension)
            {
                case ".pdf":
                    fileType = FileType.PDF;
                    shouldInclude = panel.includePDF;
                    break;
                case ".png":
                    fileType = FileType.PNG;
                    shouldInclude = panel.includePNG;
                    break;
                case ".jpg":
                case ".jpeg":
                    fileType = FileType.JPG;
                    shouldInclude = panel.includeJPG;
                    break;
                default:
                    continue; // Skip unknown file types
            }
            
            if (shouldInclude)
            {
                panel.availableFiles.Add(new FileInfo(fileName, filePath, fileType));
            }
        }
        
        // Sort files alphabetically
        panel.availableFiles.Sort((a, b) => a.fileName.CompareTo(b.fileName));
    }
    
    void PopulateDropdown(FolderPanel panel)
    {
        List<string> options = new List<string>();
        options.Add("Select a file..."); // Default option
        
        foreach (var file in panel.availableFiles)
        {
            options.Add(file.fileName);
        }
        
        panel.dropdown.AddOptions(options);
        panel.dropdown.value = 0; // Set to default
    }
    
    void OnDropdownValueChanged(TMP_Dropdown dropdown, int selectedIndex)
    {
        if (!dropdownToPanel.ContainsKey(dropdown))
            return;
            
        FolderPanel panel = dropdownToPanel[dropdown];
        
        // Index 0 is "Select a file...", so actual files start at index 1
        if (selectedIndex == 0)
        {
            HideAllDisplays(panel);
            UpdateStatusText(panel, "No file selected");
            return;
        }
        
        int fileIndex = selectedIndex - 1;
        if (fileIndex >= 0 && fileIndex < panel.availableFiles.Count)
        {
            DisplayFile(panel, panel.availableFiles[fileIndex]);
        }
    }
    
    void DisplayFile(FolderPanel panel, FileInfo fileInfo)
    {
        HideAllDisplays(panel);
        
        switch (fileInfo.fileType)
        {
            case FileType.PNG:
            case FileType.JPG:
                StartCoroutine(LoadAndDisplayImage(panel, fileInfo));
                break;
            case FileType.PDF:
                DisplayPDF(panel, fileInfo);
                break;
        }
        
        UpdateStatusText(panel, $"Displaying: {fileInfo.fileName}");
    }
    
    IEnumerator LoadAndDisplayImage(FolderPanel panel, FileInfo fileInfo)
    {
        if (panel.imageDisplay == null)
        {
            Debug.LogWarning($"Image display not assigned for panel: {panel.folderName}");
            yield break;
        }
        
        string filePath = "file://" + fileInfo.filePath;
        
        using (WWW www = new WWW(filePath))
        {
            yield return www;
            
            if (string.IsNullOrEmpty(www.error))
            {
                Texture2D texture = www.texture;
                panel.imageDisplay.texture = texture;
                panel.imageDisplay.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogError($"Failed to load image: {www.error}");
                UpdateStatusText(panel, "Failed to load image");
            }
        }
    }
    
    void DisplayPDF(FolderPanel panel, FileInfo fileInfo)
    {
        if (panel.pdfDisplay == null)
        {
            Debug.LogWarning($"PDF display not assigned for panel: {panel.folderName}");
            return;
        }
        
        // PDF display logic depends on your PDF solution
        // This is a placeholder - you'll need to integrate with your PDF viewer
        panel.pdfDisplay.SetActive(true);
        
        // Example: If using a PDF renderer component
        var pdfRenderer = panel.pdfDisplay.GetComponent<IPDFRenderer>();
        if (pdfRenderer != null)
        {
            pdfRenderer.LoadPDF(fileInfo.filePath);
        }
    }
    
    void HideAllDisplays(FolderPanel panel)
    {
        if (panel.imageDisplay != null)
            panel.imageDisplay.gameObject.SetActive(false);
            
        if (panel.pdfDisplay != null)
            panel.pdfDisplay.SetActive(false);
    }
    
    void UpdateStatusText(FolderPanel panel, string message)
    {
        if (panel.statusText != null)
        {
            panel.statusText.text = message;
        }
    }
    
    // Public methods for manual control
    public void RefreshPanel(int panelIndex)
    {
        if (panelIndex >= 0 && panelIndex < folderPanels.Count)
        {
            SetupPanel(folderPanels[panelIndex]);
        }
    }
    
    public void RefreshAllPanels()
    {
        InitializePanels();
    }
    
    public void SelectFileInPanel(int panelIndex, string fileName)
    {
        if (panelIndex >= 0 && panelIndex < folderPanels.Count)
        {
            var panel = folderPanels[panelIndex];
            for (int i = 0; i < panel.availableFiles.Count; i++)
            {
                if (panel.availableFiles[i].fileName == fileName)
                {
                    panel.dropdown.value = i + 1; // +1 because index 0 is "Select a file..."
                    break;
                }
            }
        }
    }
}

// Interface for PDF renderer - implement this based on your PDF solution
public interface IPDFRenderer
{
    void LoadPDF(string filePath);
}