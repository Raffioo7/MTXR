using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class HierarchySettings
{
    public bool showSubstructure = true;
    public int substructureOrder = -3;
    public bool showElement = true;
    public int elementOrder = -2;
    public bool showObjectID = true;
    public int objectIDOrder = -1;
}

[System.Serializable]
public class PropertyPair
{
    public string key;
    public string value;
    public int displayOrder;
    public bool isHierarchy = false;  // To distinguish hierarchy from CSV data
    
    public PropertyPair(string k, string v, int order = 0, bool hierarchy = false)
    {
        key = k;
        value = v;
        displayOrder = order;
        isHierarchy = hierarchy;
    }
}

public class RevitData : MonoBehaviour
{
    [Header("Object Properties")]
    public List<PropertyPair> properties = new List<PropertyPair>();
    
    [Header("Hierarchy Info")]
    public HierarchySettings hierarchySettings = new HierarchySettings();
    
    // Main method that accepts dictionary
    public void SetProperties(Dictionary<string, string> newProperties)
    {
        properties.Clear();
        
        foreach (var prop in newProperties)
        {
            properties.Add(new PropertyPair(prop.Key, prop.Value));
        }
        
        // Sort by display order (properties added by PropertyLoader will have correct order)
        properties = properties.OrderBy(p => p.displayOrder).ToList();
    }
    
    // Overload that accepts properties with display order and hierarchy settings
    public void SetProperties(Dictionary<string, string> newProperties, Dictionary<string, int> displayOrders, HierarchySettings hierarchySettings)
    {
        properties.Clear();
        this.hierarchySettings = hierarchySettings;
        
        foreach (var prop in newProperties)
        {
            int order = displayOrders.ContainsKey(prop.Key) ? displayOrders[prop.Key] : 999;
            properties.Add(new PropertyPair(prop.Key, prop.Value, order, false));
        }
        
        // Sort by display order
        properties = properties.OrderBy(p => p.displayOrder).ToList();
    }
    
    // Method to get all properties including hierarchy info for display
    public List<PropertyPair> GetDisplayProperties(GameObject gameObject)
    {
        List<PropertyPair> displayProps = new List<PropertyPair>();
        
        // Add hierarchy info based on settings
        if (hierarchySettings.showSubstructure)
        {
            string grandparentName = "None";
            if (gameObject.transform.parent?.parent != null)
                grandparentName = gameObject.transform.parent.parent.name;
            displayProps.Add(new PropertyPair("Substructure", grandparentName, hierarchySettings.substructureOrder, true));
        }
        
        if (hierarchySettings.showElement)
        {
            string parentName = "None";
            if (gameObject.transform.parent != null)
                parentName = gameObject.transform.parent.name;
            displayProps.Add(new PropertyPair("Element", parentName, hierarchySettings.elementOrder, true));
        }
        
        if (hierarchySettings.showObjectID)
        {
            displayProps.Add(new PropertyPair("Object ID", gameObject.name, hierarchySettings.objectIDOrder, true));
        }
        
        // Add CSV properties
        displayProps.AddRange(properties);
        
        // Sort everything by display order
        return displayProps.OrderBy(p => p.displayOrder).ToList();
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