using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.IO;
using System.Collections.Generic;

public class DropdownWithSprites : MonoBehaviour
{
    [Header("Dropdown Setup")]
    [SerializeField] private string csvFilePath = "dropdown_options.csv";
    [SerializeField] private TMP_Dropdown dropdown;
    [SerializeField] private float optionsFontSize = 12f;
    
    [Header("Sprite Setup")]
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite[] optionSprites;
    [SerializeField] private Sprite defaultSprite;
    [SerializeField] private bool matchSpritesByName = false;
    [SerializeField] private string spritePrefix = "Option_";
    
    private List<string> optionLabels = new List<string>();
    
    void Awake()
    {
        // Auto-find dropdown if not assigned
        if (dropdown == null)
        {
            dropdown = GetComponent<TMP_Dropdown>();
            if (dropdown == null)
            {
                dropdown = GetComponentInChildren<TMP_Dropdown>();
                if (dropdown == null)
                {
                    Debug.LogError("Dropdown reference is missing and could not be found automatically");
                    return;
                }
            }
        }
        
        // Auto-find image if not assigned
        if (targetImage == null)
        {
            // Try to find Image in siblings
            targetImage = transform.parent?.GetComponentInChildren<Image>(true);
            if (targetImage == null || targetImage.GetComponent<TMP_Dropdown>() != null)
            {
                Debug.LogWarning("Image component not assigned and could not be found automatically");
            }
            else
            {
                Debug.Log($"Automatically found image: {targetImage.name}");
            }
        }
    }
    
    void Start()
    {
        // Set font sizes
        SetDropdownFontSize();
        
        // Populate dropdown from CSV
        PopulateDropdownFromCSV();
        
        // Add listener for dropdown value change
        dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
        
        // Set initial sprite
        SetSpriteForSelectedOption(dropdown.value);
    }
    
    private void PopulateDropdownFromCSV()
    {
        string fullPath = Path.Combine(Application.streamingAssetsPath, csvFilePath);
        
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"CSV file not found at: {fullPath}");
            return;
        }
        
        try
        {
            string[] lines = File.ReadAllLines(fullPath);
            
            dropdown.ClearOptions();
            optionLabels.Clear();
            
            List<TMP_Dropdown.OptionData> dropdownOptions = new List<TMP_Dropdown.OptionData>();
            
            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                if (!string.IsNullOrEmpty(trimmedLine))
                {
                    dropdownOptions.Add(new TMP_Dropdown.OptionData(trimmedLine));
                    optionLabels.Add(trimmedLine);
                }
            }
            
            dropdown.AddOptions(dropdownOptions);
            
            Debug.Log($"Successfully populated dropdown with {dropdownOptions.Count} options from CSV");
            
            // Validate sprite array
            if (optionSprites != null && optionSprites.Length > 0)
            {
                if (optionSprites.Length < optionLabels.Count)
                {
                    Debug.LogWarning($"Not enough sprites ({optionSprites.Length}) for all dropdown options ({optionLabels.Count})");
                }
            }
            else
            {
                if (matchSpritesByName)
                {
                    Debug.Log("Will attempt to load sprites by name dynamically");
                }
                else
                {
                    Debug.LogWarning("No sprites assigned - will use default sprite only");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error reading CSV file: {ex.Message}");
        }
    }
    
    private void SetDropdownFontSize()
    {
        // Get the dropdown template
        Transform templateTransform = dropdown.template;
        
        if (templateTransform != null)
        {
            // Find the Item Text
            Transform itemTransform = templateTransform.Find("Viewport/Content/Item");
            TextMeshProUGUI itemText = itemTransform?.Find("Item Text")?.GetComponent<TextMeshProUGUI>();
            
            if (itemText != null)
            {
                itemText.fontSize = optionsFontSize;
            }
        }
        
        // Also adjust the font size of the main dropdown text
        if (dropdown.captionText != null)
        {
            dropdown.captionText.fontSize = optionsFontSize;
        }
    }
    
    private void OnDropdownValueChanged(int index)
    {
        Debug.Log($"Selected option: {dropdown.options[index].text} (index: {index})");
        SetSpriteForSelectedOption(index);
    }
    
    private void SetSpriteForSelectedOption(int index)
    {
        if (targetImage == null)
        {
            Debug.LogWarning("No target Image assigned for sprite change");
            return;
        }
        
        // Validate index
        if (index < 0 || index >= optionLabels.Count)
        {
            Debug.LogWarning($"Invalid dropdown index: {index}");
            targetImage.sprite = defaultSprite;
            return;
        }
        
        // Get sprite for selected option
        Sprite selectedSprite = null;
        
        // Method 1: Use pre-assigned sprite array
        if (optionSprites != null && optionSprites.Length > index && optionSprites[index] != null)
        {
            selectedSprite = optionSprites[index];
        }
        // Method 2: Try to load sprite by name
        else if (matchSpritesByName)
        {
            string spriteName = spritePrefix + optionLabels[index].Replace(" ", "_");
            selectedSprite = Resources.Load<Sprite>(spriteName);
            
            if (selectedSprite == null)
            {
                Debug.LogWarning($"Could not find sprite with name: {spriteName}");
            }
        }
        
        // Set sprite (or default if none found)
        targetImage.sprite = selectedSprite != null ? selectedSprite : defaultSprite;
        
        // Ensure the image is visible
        targetImage.enabled = true;
    }
}