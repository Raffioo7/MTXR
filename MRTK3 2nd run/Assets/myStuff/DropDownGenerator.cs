using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using TMPro;

public class CSVDropdownGenerator : MonoBehaviour
{
    [SerializeField] private TextAsset csvFile; // Assign your CSV file in the inspector
    [SerializeField] private Transform dropdownContainer; // Container where dropdowns will be placed
    [SerializeField] private TMP_Dropdown dropdownPrefab; // Prefab for TMP_Dropdown
    [SerializeField] private float dropdownSpacing = 50f; // Vertical spacing between dropdowns
    
    private List<List<string>> columnData = new List<List<string>>();
    
    void Start()
    {
        if (csvFile != null)
        {
            ParseCSV();
            CreateDropdowns();
        }
        else
        {
            Debug.LogError("CSV file is not assigned!");
        }
    }
    
    private void ParseCSV()
    {
        // Split the CSV by lines
        string[] lines = csvFile.text.Split('\n');
        
        if (lines.Length == 0)
        {
            Debug.LogError("CSV file is empty!");
            return;
        }
        
        // Get the first line to determine how many columns we have
        string[] firstLineCols = lines[0].Split(';');
        int columnCount = firstLineCols.Length;
        
        // Initialize the lists for each column
        for (int i = 0; i < columnCount; i++)
        {
            columnData.Add(new List<string>());
        }
        
        // Parse each line
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            string[] cols = line.Split(';');
            
            // Add each column's data to the corresponding list
            for (int i = 0; i < cols.Length && i < columnCount; i++)
            {
                columnData[i].Add(cols[i].Trim());
            }
        }
    }
    
    private void CreateDropdowns()
    {
        // Make sure we have a container and prefab
        if (dropdownContainer == null || dropdownPrefab == null)
        {
            Debug.LogError("Dropdown container or prefab not assigned!");
            return;
        }
        
        // Clear any existing children in the container
        foreach (Transform child in dropdownContainer)
        {
            Destroy(child.gameObject);
        }
        
        // We'll no longer modify the container's RectTransform here
        // Instead, we'll position the dropdowns relative to the container's top
        
        // Create a dropdown for each column
        for (int colIndex = 0; colIndex < columnData.Count; colIndex++)
        {
            // Instantiate the dropdown
            TMP_Dropdown dropdown = Instantiate(dropdownPrefab, dropdownContainer);
            
            // Configure dropdown's RectTransform
            RectTransform rect = dropdown.GetComponent<RectTransform>();
            
            // Get container size for positioning reference
            RectTransform containerRect = dropdownContainer.GetComponent<RectTransform>();
            float containerHeight = containerRect.rect.height;
            
            // Set dropdown to stretch horizontally but have fixed height
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            
            // Set width and height
            float dropdownHeight = rect.sizeDelta.y;
            rect.sizeDelta = new Vector2(0, dropdownHeight); // Width of 0 means "use anchors for width"
            
            // Position from top with spacing
            rect.anchoredPosition = new Vector2(0, -colIndex * dropdownSpacing - 10); // Added padding at top
            
            // Populate the dropdown with options from this column
            List<string> columnOptions = columnData[colIndex];
            dropdown.ClearOptions();
            
            List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
            foreach (string option in columnOptions)
            {
                options.Add(new TMP_Dropdown.OptionData(option));
            }
            
            dropdown.AddOptions(options);
            
            // Name the dropdown based on column index
            dropdown.name = "Dropdown_Column_" + colIndex;
        }
    }
}

// Optional extension: Load CSV from a file at runtime instead of TextAsset
public class RuntimeCSVLoader : MonoBehaviour
{
    [SerializeField] private string csvFilePath; // Path relative to StreamingAssets folder
    [SerializeField] private CSVDropdownGenerator dropdownGenerator;
    
    void Start()
    {
        if (string.IsNullOrEmpty(csvFilePath) || dropdownGenerator == null)
        {
            Debug.LogError("CSV file path or dropdown generator not assigned!");
            return;
        }
        
        string fullPath = Path.Combine(Application.streamingAssetsPath, csvFilePath);
        
        if (File.Exists(fullPath))
        {
            string csvContent = File.ReadAllText(fullPath);
            TextAsset csvAsset = new TextAsset(csvContent);
            
            // Set the CSV file on the dropdown generator
            typeof(CSVDropdownGenerator)
                .GetField("csvFile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(dropdownGenerator, csvAsset);
                
            // Call the appropriate methods to parse and create dropdowns
            dropdownGenerator.SendMessage("ParseCSV");
            dropdownGenerator.SendMessage("CreateDropdowns");
        }
        else
        {
            Debug.LogError("CSV file not found at path: " + fullPath);
        }
    }
}