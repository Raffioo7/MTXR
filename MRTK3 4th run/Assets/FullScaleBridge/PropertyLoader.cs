using UnityEngine;
using System.Collections.Generic;

public class PropertyLoader : MonoBehaviour
{
    [Header("CSV Data")]
    public TextAsset csvFile;
    
    [Header("Debug")]
    public bool showDebugLog = true;
    
    void Start()
    {
        LoadPropertiesFromCSV();
    }
    
    void LoadPropertiesFromCSV()
    {
        if (csvFile == null)
        {
            Debug.LogError("CSV file not assigned!");
            return;
        }
        
        string[] lines = csvFile.text.Split('\n');
        
        if (lines.Length <= 1)
        {
            Debug.LogError("CSV has no data rows!");
            return;
        }
        
        // Skip header row, process data
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i].Trim())) continue;
            
            string[] values = lines[i].Split(',');
            
            if (values.Length < 2) continue;
            
            string familyType = values[0].Trim().Trim('"');
            string year = values[1].Trim().Trim('"');
            
            if (string.IsNullOrEmpty(familyType)) continue;
            
            AttachPropertiesToObjects(familyType, year);
        }
        
        if (showDebugLog)
        {
            Debug.Log("Property loading complete!");
        }
    }
    
    void AttachPropertiesToObjects(string familyType, string year)
    {
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        
        foreach (GameObject obj in allObjects)
        {
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
                
                data.SetProperties(familyType, year);
                
                if (showDebugLog)
                {
                    Debug.Log($"Added properties to: {obj.name} - {familyType}, {year}");
                }
            }
        }
    }
}