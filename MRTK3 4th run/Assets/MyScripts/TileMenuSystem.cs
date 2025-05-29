using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MixedReality.Toolkit.UX;

[System.Serializable]
public class TileData
{
    public string id;
    public string displayName;
    public string description;
    // Add more fields as needed based on your CSV structure
}

public class TileMenuSystem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject tileMenuPanel;
    [SerializeField] private Transform tileContainer; // Parent for spawned tiles
    [SerializeField] private GameObject tilePrefab; // Prefab with MRTK3 PressableButton
    [SerializeField] private TextMeshProUGUI targetTextField; // Where selected text goes
    [SerializeField] private PressableButton openMenuButton; // MRTK3 button to open menu
    
    [Header("CSV Configuration")]
    [SerializeField] private string csvFileName = "tiledata.csv";
    [SerializeField] private bool csvInStreamingAssets = true;
    
    [Header("Layout Settings")]
    [SerializeField] private int columnsCount = 3;
    [SerializeField] private float tileSpacing = 10f;
    [SerializeField] private Vector2 tileSize = new Vector2(200f, 150f);
    [SerializeField] private bool useGridLayoutGroup = false; // Changed to false for testing
    [SerializeField] private bool maintainButtonScale = true; // Option to maintain scale
    
    private List<TileData> tileDataList = new List<TileData>();
    private List<GameObject> spawnedTiles = new List<GameObject>();
    
    void Start()
    {
        // Initially hide the panel
        if (tileMenuPanel != null)
            tileMenuPanel.SetActive(false);
            
        // Setup button listener
        if (openMenuButton != null)
        {
            openMenuButton.OnClicked.AddListener(OpenTileMenu);
        }
        
        // Setup GridLayoutGroup if needed
        if (useGridLayoutGroup)
        {
            SetupGridLayout();
        }
        
        // Load CSV data
        LoadCSVData();
    }
    
    void SetupGridLayout()
    {
        if (!useGridLayoutGroup || tileContainer == null) return;
        
        GridLayoutGroup gridLayout = tileContainer.GetComponent<GridLayoutGroup>();
        if (gridLayout == null)
        {
            gridLayout = tileContainer.gameObject.AddComponent<GridLayoutGroup>();
        }
        
        gridLayout.cellSize = tileSize;
        gridLayout.spacing = new Vector2(tileSpacing, tileSpacing);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = columnsCount;
        gridLayout.childAlignment = TextAnchor.UpperLeft;
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.padding = new RectOffset(10, 10, 10, 10); // Add some padding
    }
    
    void LoadCSVData()
    {
        string filePath = csvInStreamingAssets ? 
            Path.Combine(Application.streamingAssetsPath, csvFileName) : 
            csvFileName;
            
        if (!File.Exists(filePath))
        {
            Debug.LogError($"CSV file not found at: {filePath}");
            return;
        }
        
        string dataString = File.ReadAllText(filePath);
        ParseCSV(dataString);
    }
    
    void ParseCSV(string csvText)
    {
        tileDataList.Clear();
        
        string[] lines = csvText.Split('\n');
        
        // Skip header line (index 0)
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            
            // Simple CSV parsing - adjust based on your CSV structure
            string[] values = SplitCSVLine(line);
            
            if (values.Length >= 2) // Minimum expected columns
            {
                TileData tile = new TileData();
                tile.id = values[0].Trim();
                tile.displayName = values[1].Trim();
                
                // Add description if available
                if (values.Length >= 3)
                    tile.description = values[2].Trim();
                    
                tileDataList.Add(tile);
            }
        }
        
        Debug.Log($"Loaded {tileDataList.Count} tiles from CSV");
    }
    
    // Handle CSV lines with potential commas in quoted strings
    string[] SplitCSVLine(string line)
    {
        List<string> result = new List<string>();
        bool inQuotes = false;
        string currentField = "";
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(currentField);
                currentField = "";
            }
            else
            {
                currentField += c;
            }
        }
        
        result.Add(currentField); // Add last field
        return result.ToArray();
    }
    
    void OpenTileMenu()
    {
        if (tileMenuPanel == null) return;
        
        // Toggle panel visibility
        bool isActive = tileMenuPanel.activeSelf;
        tileMenuPanel.SetActive(!isActive);
        
        // Only generate tiles when opening
        if (!isActive)
        {
            GenerateTiles();
        }
    }
    
    void GenerateTiles()
    {
        // Clear existing tiles
        foreach (var tile in spawnedTiles)
        {
            Destroy(tile);
        }
        spawnedTiles.Clear();
        
        // Debug info
        Debug.Log($"Generating tiles. Total data entries: {tileDataList.Count}");
        Debug.Log($"Container has GridLayoutGroup: {tileContainer.GetComponent<GridLayoutGroup>() != null}");
        
        // Check container size
        RectTransform containerRect = tileContainer.GetComponent<RectTransform>();
        Debug.Log($"Container size: {containerRect.rect.width} x {containerRect.rect.height}");
        Debug.Log($"Container position: {containerRect.anchoredPosition}");
        
        // Generate all tiles
        for (int i = 0; i < tileDataList.Count; i++)
        {
            CreateTile(tileDataList[i], i);
        }
        
        // Force layout update
        Canvas.ForceUpdateCanvases();
        if (useGridLayoutGroup)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(tileContainer.GetComponent<RectTransform>());
        }
    }
    
    void CreateTile(TileData data, int index)
    {
        GameObject newTile = Instantiate(tilePrefab, tileContainer);
        spawnedTiles.Add(newTile);
        
        Debug.Log($"Created tile: {newTile.name}");
        
        // Ensure the tile is active
        newTile.SetActive(true);
        
        // Get RectTransform
        RectTransform rectTransform = newTile.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            // Method 1: Use SetParent with worldPositionStays = false to maintain local scale
            rectTransform.SetParent(tileContainer, false);
            
            // Method 2: If you still have scaling issues, force the scale
            // Check if the prefab has a specific component to maintain scale
            ScaleMaintainer scaleMaintainer = newTile.GetComponent<ScaleMaintainer>();
            if (scaleMaintainer == null && maintainButtonScale)
            {
                scaleMaintainer = newTile.AddComponent<ScaleMaintainer>();
                scaleMaintainer.targetScale = Vector3.one;
            }
            
            // Reset position for testing
            rectTransform.anchoredPosition = Vector2.zero;
            
            // Position tile in grid only if not using GridLayoutGroup
            if (!useGridLayoutGroup && tileContainer.GetComponent<GridLayoutGroup>() == null)
            {
                int row = index / columnsCount;
                int col = index % columnsCount;
                
                float xPos = col * (tileSize.x + tileSpacing) + (tileSize.x / 2);
                float yPos = -row * (tileSize.y + tileSpacing) - (tileSize.y / 2);
                rectTransform.anchoredPosition = new Vector2(xPos, yPos);
            }
            
            Debug.Log($"Button Local Scale: {rectTransform.localScale}, World Scale: {rectTransform.lossyScale}");
        }
        
        // Get the PressableButton component
        PressableButton button = newTile.GetComponent<PressableButton>();
        if (button != null)
        {
            // Try multiple ways to find the TextMeshPro component
            TextMeshProUGUI buttonText = null;
            
            // Method 1: Try the specific hierarchy you mentioned
            Transform compressableVisuals = button.transform.Find("CompressableButtonVisuals");
            if (compressableVisuals != null)
            {
                Transform iconAndText = compressableVisuals.Find("IconAndText");
                if (iconAndText != null)
                {
                    Transform textMeshProTransform = iconAndText.Find("TextMeshPro");
                    if (textMeshProTransform != null)
                    {
                        buttonText = textMeshProTransform.GetComponent<TextMeshProUGUI>();
                    }
                }
            }
            
            // Method 2: If not found, search recursively from button
            if (buttonText == null)
            {
                buttonText = button.GetComponentInChildren<TextMeshProUGUI>(true);
                Debug.Log($"Found TextMeshPro using recursive search: {buttonText != null}");
            }
            
            // Method 3: Debug - print all children to see actual hierarchy
            if (buttonText == null)
            {
                Debug.LogWarning("Could not find TextMeshPro. Button hierarchy:");
                PrintTransformHierarchy(button.transform, "");
            }
            
            if (buttonText != null)
            {
                buttonText.text = data.displayName;
                Debug.Log($"Set button text to: {data.displayName}");
            }
            else
            {
                Debug.LogWarning("TextMeshProUGUI not found anywhere in button hierarchy");
            }
            
            // Capture the data in local variable for the lambda
            TileData capturedData = data;
            button.OnClicked.RemoveAllListeners(); // Clear any existing listeners
            button.OnClicked.AddListener(() => OnTileSelected(capturedData));
        }
        else
        {
            Debug.LogWarning($"PressableButton component not found on tile prefab for: {data.displayName}");
        }
    }
    
    void OnTileSelected(TileData selectedData)
    {
        // Update the target text field
        if (targetTextField != null)
        {
            targetTextField.text = selectedData.displayName;
        }
        
        // Close the panel
        if (tileMenuPanel != null)
        {
            tileMenuPanel.SetActive(false);
        }
        
        // Optional: Trigger any additional events
        Debug.Log($"Selected tile: {selectedData.displayName}");
    }
    
    void OnDestroy()
    {
        // Clean up listeners
        if (openMenuButton != null)
        {
            openMenuButton.OnClicked.RemoveListener(OpenTileMenu);
        }
    }
    
    // Helper method to debug hierarchy
    void PrintTransformHierarchy(Transform transform, string indent)
    {
        Debug.Log($"{indent}{transform.name}");
        foreach (Transform child in transform)
        {
            PrintTransformHierarchy(child, indent + "  ");
        }
    }
    
    // Helper method to calculate cumulative scale
    float GetCumulativeScale(Transform transform)
    {
        float scale = 1f;
        Transform current = transform;
        
        while (current != null)
        {
            scale *= current.localScale.x; // Assuming uniform scaling
            current = current.parent;
            
            // Stop at Canvas to avoid going too far up
            if (current && current.GetComponent<Canvas>() != null)
                break;
        }
        
        return scale;
    }
}

// Simple component to maintain scale in UI
public class ScaleMaintainer : MonoBehaviour
{
    public Vector3 targetScale = Vector3.one;
    private RectTransform rectTransform;
    
    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
    }
    
    void LateUpdate()
    {
        if (rectTransform != null && transform.hasChanged)
        {
            rectTransform.localScale = targetScale;
            transform.hasChanged = false;
        }
    }
}