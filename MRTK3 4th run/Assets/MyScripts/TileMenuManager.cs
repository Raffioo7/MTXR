using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;
using MixedReality.Toolkit.UX;
using MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit;
using UnityEngine.UI;

public class TileMenuManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject tileMenuPanel;
    [SerializeField] private GameObject tilePrefab; // MRTK3 button prefab
    [SerializeField] private Transform tileContainer; // Parent transform for tiles (inside ScrollView content)
    [SerializeField] private TextMeshProUGUI displayText; // Text to show selected tile
    
    [Header("Scroll View Settings")]
    [SerializeField] private ScrollRect scrollRect; // Unity ScrollRect component
    [SerializeField] private RectTransform scrollViewContent; // Content RectTransform inside ScrollView
    [SerializeField] private bool useScrollView = true;
    [SerializeField] private float scrollViewHeight = 0.4f; // Height of visible scroll area
    [SerializeField] private bool addScrollHandles = true; // Add MRTK3 scroll handles
    
    [Header("CSV Settings")]
    [SerializeField] private string csvFileName = "tiles.csv";
    [SerializeField] private bool loadFromResources = true;
    [SerializeField] private string csvPath = ""; // Optional custom path
    
    [Header("Layout Settings")]
    [SerializeField] private int tilesPerRow = 3;
    [SerializeField] private float tileSpacing = 0.02f; // Space between tiles
    [SerializeField] private Vector3 tileScale = Vector3.one;
    [SerializeField] private bool useAbsolutePositioning = false; // If true, use tileWidth/Height directly
    [SerializeField] private float tileWidth = 0.32f; // Manual tile width for absolute positioning
    [SerializeField] private float tileHeight = 0.08f; // Manual tile height for absolute positioning
    
    [Header("Runtime Preview")]
    [SerializeField] private bool autoRefresh = true; // Auto refresh when values change
    [SerializeField] private bool refreshNow = false; // Button to manually refresh
    
    private List<TileData> tileDataList = new List<TileData>();
    private List<GameObject> spawnedTiles = new List<GameObject>();
    private GameObject scrollViewObject;
    
    // Store previous values to detect changes
    private int prevTilesPerRow;
    private float prevTileSpacing;
    private Vector3 prevTileScale;
    private bool prevUseAbsolutePositioning;
    private float prevTileWidth;
    private float prevTileHeight;
    
    [System.Serializable]
    public class TileData
    {
        public string column1;
        public string column2;
        
        public TileData(string col1, string col2)
        {
            column1 = col1;
            column2 = col2;
        }
    }
    
    void Start()
    {
        // Initially hide the panel
        if (tileMenuPanel != null)
            tileMenuPanel.SetActive(false);
            
        // Setup scroll view if needed
        if (useScrollView)
            SetupScrollView();
            
        // Load CSV data
        LoadCSVData();
        
        // Store initial values
        StoreCurrentValues();
    }
    
    void Update()
    {
        // Check if we should refresh
        if (autoRefresh && tileMenuPanel != null && tileMenuPanel.activeSelf)
        {
            if (HasValuesChanged())
            {
                RefreshTileLayout();
                StoreCurrentValues();
            }
        }
    }
    
    void OnValidate()
    {
        // This is called when values change in the inspector
        if (Application.isPlaying && refreshNow)
        {
            refreshNow = false;
            RefreshTileLayout();
        }
    }
    
    void SetupScrollView()
    {
        // If scrollRect is not assigned, try to find or create one
        if (scrollRect == null && tileContainer != null)
        {
            scrollRect = tileContainer.GetComponentInParent<ScrollRect>();
            
            if (scrollRect == null)
            {
                Debug.LogWarning("No ScrollRect found. Please add a ScrollRect component to enable scrolling.");
                useScrollView = false;
                return;
            }
        }
        
        // Get the content RectTransform
        if (scrollRect != null && scrollViewContent == null)
        {
            scrollViewContent = scrollRect.content;
            if (scrollViewContent == null)
            {
                scrollViewContent = tileContainer.GetComponent<RectTransform>();
                scrollRect.content = scrollViewContent;
            }
        }
        
        // Setup MRTK3 scroll handling
        if (addScrollHandles && scrollRect != null)
        {
            SetupMRTKScrollHandles();
        }
    }
    
    void SetupMRTKScrollHandles()
    {
        // Add CanvasProxyInteractor if not present (for MRTK3 canvas interaction)
        CanvasProxyInteractor canvasProxy = scrollRect.GetComponent<CanvasProxyInteractor>();
        if (canvasProxy == null)
        {
            canvasProxy = scrollRect.gameObject.AddComponent<CanvasProxyInteractor>();
        }
        
        // Configure scroll settings for MRTK3
        scrollRect.horizontal = false; // Usually only want vertical scrolling
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 30f; // Adjust for comfort
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.135f;
        
        // Add StatefulInteractable for better MRTK3 integration
        Component interactable = scrollRect.GetComponent("StatefulInteractable");
        if (interactable == null)
        {
            interactable = UnityEngineInternal.APIUpdaterRuntimeServices.AddComponent(scrollRect.gameObject, "Assets/MyScripts/TileMenuManager.cs (161,28)", "StatefulInteractable");
        }
        
        // Make sure the canvas has proper settings for MRTK3
        Canvas canvas = scrollRect.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            // Ensure the canvas is set up for world space if needed
            if (canvas.renderMode != RenderMode.WorldSpace)
            {
                Debug.Log("Setting Canvas to WorldSpace for MRTK3 interaction");
                canvas.renderMode = RenderMode.WorldSpace;
            }
            
            // Add GraphicRaycaster if missing
            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }
    }
    
    bool HasValuesChanged()
    {
        return tilesPerRow != prevTilesPerRow ||
               tileSpacing != prevTileSpacing ||
               tileScale != prevTileScale ||
               useAbsolutePositioning != prevUseAbsolutePositioning ||
               tileWidth != prevTileWidth ||
               tileHeight != prevTileHeight;
    }
    
    void StoreCurrentValues()
    {
        prevTilesPerRow = tilesPerRow;
        prevTileSpacing = tileSpacing;
        prevTileScale = tileScale;
        prevUseAbsolutePositioning = useAbsolutePositioning;
        prevTileWidth = tileWidth;
        prevTileHeight = tileHeight;
    }
    
    void RefreshTileLayout()
    {
        if (tileMenuPanel != null && tileMenuPanel.activeSelf && tileDataList.Count > 0)
        {
            ClearTiles();
            GenerateTiles();
        }
    }
    
    void LoadCSVData()
    {
        string csvText = "";
        
        if (loadFromResources)
        {
            // Load from Resources folder
            TextAsset csvAsset = Resources.Load<TextAsset>(Path.GetFileNameWithoutExtension(csvFileName));
            if (csvAsset != null)
            {
                csvText = csvAsset.text;
            }
            else
            {
                Debug.LogError($"CSV file '{csvFileName}' not found in Resources folder!");
                return;
            }
        }
        else
        {
            // Load from custom path
            string fullPath = string.IsNullOrEmpty(csvPath) ? 
                Path.Combine(Application.dataPath, csvFileName) : 
                Path.Combine(csvPath, csvFileName);
                
            if (File.Exists(fullPath))
            {
                csvText = File.ReadAllText(fullPath);
            }
            else
            {
                Debug.LogError($"CSV file not found at: {fullPath}");
                return;
            }
        }
        
        ParseCSV(csvText);
    }
    
    void ParseCSV(string csvText)
    {
        tileDataList.Clear();
        
        string[] lines = csvText.Split('\n');
        
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            
            // Skip empty lines
            if (string.IsNullOrEmpty(line))
                continue;
                
            // Skip header row if it exists (optional)
            if (i == 0 && line.ToLower().Contains("column"))
                continue;
                
            string[] columns = line.Split(',');
            
            if (columns.Length >= 2)
            {
                TileData data = new TileData(
                    columns[0].Trim(),
                    columns[1].Trim()
                );
                tileDataList.Add(data);
            }
            else if (columns.Length == 1)
            {
                // If only one column, use it for both values
                TileData data = new TileData(
                    columns[0].Trim(),
                    columns[0].Trim()
                );
                tileDataList.Add(data);
            }
        }
        
        Debug.Log($"Loaded {tileDataList.Count} tiles from CSV");
    }
    
    public void OpenTileMenu()
    {
        if (tileMenuPanel == null || tilePrefab == null || tileContainer == null)
        {
            Debug.LogError("Missing references! Please assign all required components.");
            return;
        }
        
        // Clear existing tiles
        ClearTiles();
        
        // Generate new tiles
        GenerateTiles();
        
        // Show the panel
        tileMenuPanel.SetActive(true);
        
        // Reset scroll position
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f; // Scroll to top
        }
    }
    
    public void CloseTileMenu()
    {
        if (tileMenuPanel != null)
            tileMenuPanel.SetActive(false);
    }
    
    void ClearTiles()
    {
        foreach (GameObject tile in spawnedTiles)
        {
            if (tile != null)
                Destroy(tile);
        }
        spawnedTiles.Clear();
    }
    
    void GenerateTiles()
    {
        float totalTileWidth;
        float totalTileHeight;
        
        if (useAbsolutePositioning)
        {
            // Use manual values
            totalTileWidth = tileWidth + tileSpacing;
            totalTileHeight = tileHeight + tileSpacing;
        }
        else
        {
            // Try to automatically determine tile size
            RectTransform tileRect = tilePrefab.GetComponent<RectTransform>();
            
            if (tileRect != null)
            {
                // UI element
                totalTileWidth = tileRect.rect.width * tileScale.x + tileSpacing;
                totalTileHeight = tileRect.rect.height * tileScale.y + tileSpacing;
            }
            else
            {
                // 3D object - try to get bounds
                GameObject tempTile = Instantiate(tilePrefab);
                tempTile.transform.localScale = tileScale;
                
                Bounds bounds = GetObjectBounds(tempTile);
                totalTileWidth = bounds.size.x + tileSpacing;
                totalTileHeight = bounds.size.y + tileSpacing;
                
                // If bounds are zero, use default values
                if (totalTileWidth <= tileSpacing) totalTileWidth = 0.32f + tileSpacing;
                if (totalTileHeight <= tileSpacing) totalTileHeight = 0.08f + tileSpacing;
                
                DestroyImmediate(tempTile);
            }
        }
        
        // Calculate grid dimensions
        int totalRows = Mathf.CeilToInt((float)tileDataList.Count / tilesPerRow);
        float gridHeight = totalRows * totalTileHeight;
        float gridWidth = tilesPerRow * totalTileWidth;
        
        // Update scroll view content size if using scroll
        if (useScrollView && scrollViewContent != null)
        {
            scrollViewContent.sizeDelta = new Vector2(gridWidth, gridHeight);
            
            // Ensure the scroll view viewport is set correctly
            if (scrollRect != null && scrollRect.viewport != null)
            {
                RectTransform viewport = scrollRect.viewport;
                // Keep viewport height as configured
                viewport.sizeDelta = new Vector2(gridWidth + 0.05f, scrollViewHeight);
            }
        }
        
        // Center the grid horizontally
        float startX = -gridWidth / 2f + totalTileWidth / 2f;
        float startY = gridHeight / 2f - totalTileHeight / 2f;
        
        for (int i = 0; i < tileDataList.Count; i++)
        {
            // Instantiate tile
            GameObject newTile = Instantiate(tilePrefab, tileContainer);
            newTile.transform.localScale = tileScale;
            
            // Position tile in grid
            int row = i / tilesPerRow;
            int col = i % tilesPerRow;
            
            float xPos = startX + (col * totalTileWidth);
            float yPos = startY - (row * totalTileHeight);
            
            // For UI elements
            if (newTile.GetComponent<RectTransform>() != null)
            {
                RectTransform rt = newTile.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(xPos, yPos);
            }
            else
            {
                // For 3D objects
                newTile.transform.localPosition = new Vector3(xPos, yPos, 0);
            }
            
            // Set tile text
            TileData data = tileDataList[i];
            SetTileText(newTile, data);
            
            // Setup button click handler
            SetupTileButton(newTile, data);
            
            spawnedTiles.Add(newTile);
        }
    }
    
    Bounds GetObjectBounds(GameObject obj)
    {
        Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        
        foreach (Renderer renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }
        
        return bounds;
    }
    
    void SetTileText(GameObject tile, TileData data)
    {
        // Find TextMeshPro components in the tile
        TextMeshProUGUI[] textComponents = tile.GetComponentsInChildren<TextMeshProUGUI>();
        
        if (textComponents.Length > 0)
        {
            // Use both columns for display (customize as needed)
            textComponents[0].text = $"{data.column1}\n{data.column2}";
        }
        else
        {
            // Try TextMeshPro 3D
            TextMeshPro[] text3DComponents = tile.GetComponentsInChildren<TextMeshPro>();
            if (text3DComponents.Length > 0)
            {
                text3DComponents[0].text = $"{data.column1}\n{data.column2}";
            }
        }
    }
    
    void SetupTileButton(GameObject tile, TileData data)
    {
        // Get MRTK3 PressableButton component
        PressableButton button = tile.GetComponent<PressableButton>();
        
        if (button != null)
        {
            // Add click handler
            button.OnClicked.AddListener(() => OnTileClicked(data));
        }
        else
        {
            Debug.LogWarning("PressableButton component not found on tile prefab!");
        }
    }
    
    void OnTileClicked(TileData data)
    {
        // Update the display text
        if (displayText != null)
        {
            displayText.text = $"Selected: {data.column1} - {data.column2}";
        }
        
        // Close the menu
        CloseTileMenu();
        
        // Optional: Trigger any additional events
        Debug.Log($"Tile clicked: {data.column1}, {data.column2}");
    }
    
    // Optional: Method to reload CSV at runtime
    public void ReloadCSV()
    {
        LoadCSVData();
        if (tileMenuPanel.activeSelf)
        {
            ClearTiles();
            GenerateTiles();
        }
    }
    
    // Public method for manual refresh
    public void RefreshLayout()
    {
        RefreshTileLayout();
    }
    
    // Get current tile count for editor display
    public int GetTileCount()
    {
        return spawnedTiles.Count;
    }
}