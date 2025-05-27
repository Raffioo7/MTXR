using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class PropertyPair
{
    public string key;
    public string value;
}

public class RevitData : MonoBehaviour
{
    [Header("Object Properties")]
    public List<PropertyPair> properties = new List<PropertyPair>();
    
    // Main method that accepts dictionary
    public void SetProperties(Dictionary<string, string> newProperties)
    {
        properties.Clear();
        
        foreach (var prop in newProperties)
        {
            properties.Add(new PropertyPair
            {
                key = prop.Key,
                value = prop.Value
            });
        }
    }
    
    // Legacy method for backwards compatibility (if you still need it elsewhere)
    public void SetProperties(string familyType, string year)
    {
        Dictionary<string, string> props = new Dictionary<string, string>
        {
            {"FamilyType", familyType},
            {"Year", year}
        };
        SetProperties(props);
    }
    
    // Helper methods to get specific properties
    public string GetProperty(string key)
    {
        foreach (var prop in properties)
        {
            if (prop.key == key)
                return prop.value;
        }
        return "";
    }
    
    public string GetFamilyType()
    {
        return GetProperty("FamilyType");
    }
    
    // You can add more specific getters as needed
    public string GetYear()
    {
        return GetProperty("Year");
    }
    
    public string GetMaterial()
    {
        return GetProperty("Material");
    }
    
    public string GetCost()
    {
        return GetProperty("Cost");
    }
}