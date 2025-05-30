using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;
using MixedReality.Toolkit.UX;

public class TileMenuManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject tileMenuPanel;
    [SerializeField] private GameObject tilePrefab; // MRTK3 button prefab
    [SerializeField] private Transform tileContainer; // Parent transform for tiles
    [SerializeField] private TextMeshProUGUI displayText; // Text to show selected tile
    
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
    [SerializeField] private float verticalStartOffset = 0f; // Vertical offset for where tiles start generating
    
    [Header("Clipping Settings")]
    [SerializeField] private bool enableBoundsClipping = true; // Enable/disable bounds checking
    [SerializeField] private Transform clippingBounds; // Reference object that defines the clipping area
    [SerializeField] private Vector3 clippingSize = new Vector3(1f, 1f, 1f); // Size of clipping area if no reference object
    [SerializeField] private Vector3 clippingCenter = Vector3.zero; // Center offset for clipping area
    
    [Header("Runtime Preview")]
    [SerializeField] private bool autoRefresh = true; // Auto refresh when values change
    [SerializeField] private bool refreshNow = false; // Button to manually refresh
    
    private List<TileData> tileDataList = new List<TileData>();
    private List<GameObject> spawnedTiles = new List<GameObject>();
    
    // Store previous values to detect changes
    private int prevTilesPerRow;
    private float prevTileSpacing;
    private Vector3 prevTileScale;
    private bool prevUseAbsolutePositioning;
    private float prevTileWidth;
    private float prevTileHeight;
    private float prevVerticalStartOffset;
    private bool prevEnableBoundsClipping;
    private Vector3 prevClippingSize;
    private Vector3 prevClippingCenter;
    
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
        {
            tileMenuPanel.SetActive(false);
            isMenuOpen = false;
        }
            
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
        
        // Update tile visibility based on bounds clipping
        if (enableBoundsClipping && tileMenuPanel != null && tileMenuPanel.activeSelf)
        {
            UpdateTileVisibility();
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
    
    bool HasValuesChanged()
    {
        return tilesPerRow != prevTilesPerRow ||
               tileSpacing != prevTileSpacing ||
               tileScale != prevTileScale ||
               useAbsolutePositioning != prevUseAbsolutePositioning ||
               tileWidth != prevTileWidth ||
               tileHeight != prevTileHeight ||
               verticalStartOffset != prevVerticalStartOffset ||
               enableBoundsClipping != prevEnableBoundsClipping ||
               clippingSize != prevClippingSize ||
               clippingCenter != prevClippingCenter;
    }
    
    void StoreCurrentValues()
    {
        prevTilesPerRow = tilesPerRow;
        prevTileSpacing = tileSpacing;
        prevTileScale = tileScale;
        prevUseAbsolutePositioning = useAbsolutePositioning;
        prevTileWidth = tileWidth;
        prevTileHeight = tileHeight;
        prevVerticalStartOffset = verticalStartOffset;
        prevEnableBoundsClipping = enableBoundsClipping;
        prevClippingSize = clippingSize;
        prevClippingCenter = clippingCenter;
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
    
    // Track menu state explicitly
    private bool isMenuOpen = false;
    
    // New toggle method that replaces OpenTileMenu
    public void ToggleTileMenu()
    {
        if (tileMenuPanel == null)
        {
            Debug.LogError("TileMenuPanel is not assigned!");
            return;
        }
        
        Debug.Log($"ToggleTileMenu called. Current state - isMenuOpen: {isMenuOpen}, activeSelf: {tileMenuPanel.activeSelf}");
        
        // Toggle the panel state using our tracked variable
        if (isMenuOpen)
        {
            Debug.Log("Closing tile menu...");
            CloseTileMenu();
        }
        else
        {
            Debug.Log("Opening tile menu...");
            OpenTileMenu();
        }
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
        
        // Show the panel and update state
        tileMenuPanel.SetActive(true);
        isMenuOpen = true;
        
        Debug.Log("Tile menu opened");
    }
    
    public void CloseTileMenu()
    {
        if (tileMenuPanel != null)
        {
            tileMenuPanel.SetActive(false);
            isMenuOpen = false;
            Debug.Log("Tile menu closed");
        }
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
        
        // Center the grid
        float gridWidth = (tilesPerRow - 1) * totalTileWidth;
        float startX = -gridWidth / 2f;
        
        for (int i = 0; i < tileDataList.Count; i++)
        {
            // Instantiate tile
            GameObject newTile = Instantiate(tilePrefab, tileContainer);
            
            // Store original transform values before modifying
            Vector3 originalLocalPosition = newTile.transform.localPosition;
            Quaternion originalLocalRotation = newTile.transform.localRotation;
            Vector3 originalLocalScale = newTile.transform.localScale;
            
            // Apply scale (only if it's different from the prefab's scale)
            if (tileScale != Vector3.one)
            {
                newTile.transform.localScale = Vector3.Scale(originalLocalScale, tileScale);
            }
            
            // Position tile in grid
            int row = i / tilesPerRow;
            int col = i % tilesPerRow;
            
            float xPos = startX + (col * totalTileWidth);
            float yPos = verticalStartOffset + (-row * totalTileHeight);
            
            // For UI elements (RectTransform)
            RectTransform rt = newTile.GetComponent<RectTransform>();
            if (rt != null)
            {
                // Preserve original anchors and pivot
                rt.anchoredPosition = new Vector2(xPos, yPos);
                // Keep the original rotation
                rt.localRotation = originalLocalRotation;
            }
            else
            {
                // For 3D objects
                newTile.transform.localPosition = new Vector3(
                    originalLocalPosition.x + xPos, 
                    originalLocalPosition.y + yPos, 
                    originalLocalPosition.z
                );
                // Keep the original rotation
                newTile.transform.localRotation = originalLocalRotation;
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
    
    // Bounds clipping methods
    void UpdateTileVisibility()
    {
        if (!enableBoundsClipping) return;
        
        Bounds clipBounds = GetClippingBounds();
        
        foreach (GameObject tile in spawnedTiles)
        {
            if (tile != null)
            {
                bool shouldBeVisible = IsTileInBounds(tile, clipBounds);
                
                // Only change visibility if it's different to avoid unnecessary calls
                if (tile.activeSelf != shouldBeVisible)
                {
                    tile.SetActive(shouldBeVisible);
                }
            }
        }
    }
    
    Bounds GetClippingBounds()
    {
        Vector3 center;
        Vector3 size;
        
        if (clippingBounds != null)
        {
            // Use the referenced transform's bounds
            center = clippingBounds.position + clippingCenter;
            
            // Try to get bounds from a collider first
            Collider boundsCollider = clippingBounds.GetComponent<Collider>();
            if (boundsCollider != null)
            {
                size = boundsCollider.bounds.size;
            }
            else
            {
                // Use the transform's scale or lossyScale as size
                size = Vector3.Scale(clippingSize, clippingBounds.lossyScale);
            }
        }
        else
        {
            // Use manual settings relative to tile container
            center = tileContainer.position + clippingCenter;
            size = clippingSize;
        }
        
        return new Bounds(center, size);
    }
    
    bool IsTileInBounds(GameObject tile, Bounds bounds)
    {
        // Get the tile's world position
        Vector3 tilePosition = tile.transform.position;
        
        // You can also check if the tile's bounds overlap with clipping bounds
        // For more precise checking, uncomment below:
        /*
        Bounds tileBounds = GetTileBounds(tile);
        return bounds.Intersects(tileBounds);
        */
        
        // Simple point-in-bounds check
        return bounds.Contains(tilePosition);
    }
    
    // Optional: More precise bounds checking for tiles
    Bounds GetTileBounds(GameObject tile)
    {
        Bounds tileBounds = new Bounds(tile.transform.position, Vector3.zero);
        
        // Get all renderers to calculate total bounds
        Renderer[] renderers = tile.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            tileBounds.Encapsulate(renderer.bounds);
        }
        
        // If no renderers found, use a default size
        if (renderers.Length == 0)
        {
            tileBounds.size = new Vector3(0.1f, 0.1f, 0.1f);
        }
        
        return tileBounds;
    }
    
    // Public method to manually update visibility (useful for scrolling)
    public void UpdateClipping()
    {
        if (enableBoundsClipping)
        {
            UpdateTileVisibility();
        }
    }
    
    // Debug method to visualize clipping bounds in Scene view
    void OnDrawGizmosSelected()
    {
        if (enableBoundsClipping && Application.isPlaying)
        {
            Bounds clipBounds = GetClippingBounds();
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(clipBounds.center, clipBounds.size);
            
            Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
            Gizmos.DrawCube(clipBounds.center, clipBounds.size);
        }
    }
}