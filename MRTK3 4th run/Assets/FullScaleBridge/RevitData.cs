using UnityEngine;

public class RevitData : MonoBehaviour
{
    [Header("Revit Properties")]
    public string familyType;
    public string year;
    
    public void SetProperties(string family, string yr)
    {
        familyType = family;
        year = yr;
    }
}