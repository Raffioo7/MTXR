using UnityEngine;
using TMPro;
using System.IO;
using System.Collections.Generic;

public class CSVColumnDropdownPopulator : MonoBehaviour
{
    [Tooltip("The path to the CSV file, relative to the StreamingAssets folder")]
    [SerializeField] private string csvFilePath = "dropdown_options.csv";
    
    [Tooltip("The dropdown to populate")]
    [SerializeField] private TMP_Dropdown dropdown;
    
    private char delimiter = ';';
    
    void Start()
    {
        if (dropdown == null)
        {
            Debug.LogError("Dropdown reference is missing");
            return;
        }
        
        // Load and populate dropdown options
        PopulateDropdownFromCSV();
    }
    
    private void PopulateDropdownFromCSV()
    {
        // Construct the full path to the CSV file
        string fullPath = Path.Combine(Application.streamingAssetsPath, csvFilePath);
        
        // Check if the file exists
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"CSV file not found at: {fullPath}");
            return;
        }
        
        try
        {
            // Read all lines from the CSV file
            string[] lines = File.ReadAllLines(fullPath);
            
            // Make sure there's at least one line
            if (lines.Length == 0)
            {
                Debug.LogError("CSV file is empty");
                return;
            }
            
            // Clear existing options in the dropdown
            dropdown.ClearOptions();
            
            // Create a list for new options
            List<TMP_Dropdown.OptionData> dropdownOptions = new List<TMP_Dropdown.OptionData>();
            
            // Extract the first column values from each row
            foreach (string line in lines)
            {
                string[] values = line.Split(delimiter);
                
                // Make sure there's at least one value in the row
                if (values.Length > 0)
                {
                    string firstColumnValue = values[0].Trim();
                    if (!string.IsNullOrEmpty(firstColumnValue))
                    {
                        dropdownOptions.Add(new TMP_Dropdown.OptionData(firstColumnValue));
                    }
                }
            }
            
            // Add the options to the dropdown
            dropdown.AddOptions(dropdownOptions);
            
            Debug.Log($"Successfully populated dropdown with {dropdownOptions.Count} options from the first column of CSV");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error reading CSV file: {ex.Message}");
        }
    }
    
    // Optional: Add an event handler for when an option is selected
    public void OnDropdownValueChanged(int index)
    {
        Debug.Log($"Selected option: {dropdown.options[index].text}");
        // Your custom logic here
    }
}