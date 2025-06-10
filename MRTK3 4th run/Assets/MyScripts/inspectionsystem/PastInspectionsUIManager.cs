using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MixedReality.Toolkit.UX;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Creates a scrollable list of past inspections that can be selected and loaded
/// </summary>
public class InspectionListUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Panel that contains the inspection list")]
    public GameObject listPanel;
    
    [Tooltip("Parent object where inspection list items will be created")]
    public Transform listContainer;
    
    [Tooltip("Prefab for each inspection list item (should have TMP_Text and PressableButton)")]
    public GameObject listItemPrefab;
    
    [Tooltip("Button to open/close the inspection list")]
    public GameObject toggleListButton;
    
    [Tooltip("Button to refresh the list")]
    public GameObject refreshListButton;
    
    [Header("Connected Systems")]
    [Tooltip("Reference to your SimpleTextReader script")]
    public SimpleTextReader textReader;
    
    [Header("List Settings")]
    [Tooltip("Maximum number of inspections to show")]
    public int maxInspectionsToShow = 10;
    
    [Tooltip("Show newest first")]
    public bool newestFirst = true;
    
    [Header("Layout Settings")]
    [Tooltip("Space between list items (XR-optimized)")]
    [Range(0f, 0.02f)]
    public float itemSpacing = 0f;
    
    [Tooltip("Padding around the list edges (XR-optimized)")]
    [Range(0f, 0.015f)]
    public float listPadding = 0f;
    
    [Tooltip("Height of each list item (XR-optimized)")]
    [Range(0.01f, 0.1f)]
    public float itemHeight = 0.015f;
    
    [Tooltip("Update layout in real-time during play")]
    public bool updateLayoutInRealTime = true;
    
    [Header("Clipping Settings")]
    [Tooltip("Enable/disable bounds checking for list items")]
    public bool enableBoundsClipping = true;
    
    [Tooltip("Clipping mode - how to determine the viewport bounds")]
    public ClippingMode clippingMode = ClippingMode.UseListPanel;
    
    [Tooltip("Reference object that defines the clipping area (only used in Manual mode)")]
    public Transform clippingBounds;
    
    [Tooltip("Size of clipping area (only used in Manual mode)")]
    public Vector3 clippingSize = new Vector3(1f, 1f, 1f);
    
    [Tooltip("Center offset for clipping area")]
    public Vector3 clippingCenter = Vector3.zero;
    
    public enum ClippingMode
    {
        UseListPanel,      // Use the listPanel's RectTransform as viewport
        UseListContainer,  // Use the listContainer's RectTransform as viewport
        UseScrollViewport, // Look for a parent ScrollRect's viewport
        Manual            // Use manual clippingBounds reference
    }
    
    private List<GameObject> listItems = new List<GameObject>();
    private bool isListVisible = false;
    private UnityEngine.UI.VerticalLayoutGroup layoutGroup;
    private UnityEngine.UI.ContentSizeFitter sizeFitter;
    
    // Store previous values to detect changes
    private float previousSpacing;
    private float previousPadding;
    private float previousHeight;
    
    void Start()
    {
        // Find SimpleTextReader if not assigned
        if (textReader == null)
            textReader = FindObjectOfType<SimpleTextReader>();
        
        // Hide list initially
        if (listPanel != null)
            listPanel.SetActive(false);
        
        // Setup layout for the container with fixed values
        SetupListLayout();
        
        SetupButtons();
        RefreshInspectionList();
        
        // Store initial values
        StorePreviousValues();
        
        // Force layout update to apply fixed values
        StartCoroutine(ForceLayoutUpdateNextFrame());
    }
    
    // Coroutine to force layout update on the next frame
    private System.Collections.IEnumerator ForceLayoutUpdateNextFrame()
    {
        yield return null; // Wait one frame
        UpdateLayout();
        Debug.Log("Initial layout update applied");
    }
    
    void Update()
    {
        // Check for layout changes during runtime
        if (updateLayoutInRealTime && HasLayoutChanged())
        {
            UpdateLayout();
            StorePreviousValues();
        }
        
        // Update item visibility based on bounds clipping
        if (enableBoundsClipping && listPanel != null && listPanel.activeSelf)
        {
            UpdateListItemVisibility();
        }
    }
    
    bool HasLayoutChanged()
    {
        float threshold = 0.001f;
        return Mathf.Abs(itemSpacing - previousSpacing) > threshold ||
               Mathf.Abs(listPadding - previousPadding) > threshold ||
               Mathf.Abs(itemHeight - previousHeight) > threshold;
    }
    
    void StorePreviousValues()
    {
        previousSpacing = itemSpacing;
        previousPadding = listPadding;
        previousHeight = itemHeight;
    }
    
    void UpdateLayout()
    {
        // Update the layout group settings
        if (layoutGroup != null)
        {
            layoutGroup.spacing = itemSpacing;
            // Convert XR units to pixels for padding
            int paddingPixels = Mathf.RoundToInt(listPadding * 1000f);
            layoutGroup.padding = new UnityEngine.RectOffset(paddingPixels, paddingPixels, paddingPixels, paddingPixels);
        }
        
        // Update all existing list items
        foreach (GameObject item in listItems)
        {
            if (item != null)
            {
                UpdateListItemSize(item);
            }
        }
        
        // Force the layout to rebuild immediately
        if (layoutGroup != null)
        {
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(layoutGroup.GetComponent<RectTransform>());
        }
        
        if (Application.isPlaying)
            Debug.Log($"XR Layout updated - Spacing: {itemSpacing:F4}, Padding: {listPadding:F4}, Height: {itemHeight:F4}");
    }
    
    void SetupListLayout()
    {
        if (listContainer != null)
        {
            // Add VerticalLayoutGroup if it doesn't exist
            layoutGroup = listContainer.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (layoutGroup == null)
            {
                layoutGroup = listContainer.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            }
            
            // Configure the layout with current settings (XR scale)
            layoutGroup.childControlHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.spacing = itemSpacing;
            
            // Convert to pixels for padding (assuming 1000 pixels per unit)
            int paddingPixels = Mathf.RoundToInt(listPadding * 1000f);
            layoutGroup.padding = new UnityEngine.RectOffset(paddingPixels, paddingPixels, paddingPixels, paddingPixels);
            
            // Add ContentSizeFitter if it doesn't exist
            sizeFitter = listContainer.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (sizeFitter == null)
            {
                sizeFitter = listContainer.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            }
            
            sizeFitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            sizeFitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
            
            Debug.Log($"XR List layout configured - Spacing: {itemSpacing}, Padding: {listPadding}, Item Height: {itemHeight}");
        }
    }
    
    void SetupButtons()
    {
        // Setup toggle button
        if (toggleListButton != null)
        {
            var toggleButton = toggleListButton.GetComponent<PressableButton>();
            if (toggleButton == null)
                toggleButton = toggleListButton.GetComponentInChildren<PressableButton>();
            
            if (toggleButton != null)
            {
                toggleButton.OnClicked.AddListener(ToggleInspectionList);
                Debug.Log("Toggle list button connected");
            }
        }
        
        // Setup refresh button
        if (refreshListButton != null)
        {
            var refreshButton = refreshListButton.GetComponent<PressableButton>();
            if (refreshButton == null)
                refreshButton = refreshListButton.GetComponentInChildren<PressableButton>();
            
            if (refreshButton != null)
            {
                refreshButton.OnClicked.AddListener(RefreshInspectionList);
                Debug.Log("Refresh list button connected");
            }
        }
    }
    
    public void ToggleInspectionList()
    {
        isListVisible = !isListVisible;
        
        if (listPanel != null)
        {
            listPanel.SetActive(isListVisible);
            
            if (isListVisible)
            {
                RefreshInspectionList();
                // Force layout update when showing the list
                StartCoroutine(ForceLayoutUpdateNextFrame());
                Debug.Log("Inspection list opened");
            }
            else
            {
                Debug.Log("Inspection list closed");
            }
        }
    }
    
    public void RefreshInspectionList()
    {
        Debug.Log("Refreshing inspection list...");
        
        // Clear existing list items
        ClearListItems();
        
        // Get all inspection files
        var inspectionFiles = GetInspectionFiles();
        
        if (inspectionFiles.Length == 0)
        {
            CreateNoInspectionsItem();
            return;
        }
        
        // Create list items for each inspection
        int itemCount = 0;
        foreach (var fileInfo in inspectionFiles)
        {
            if (itemCount >= maxInspectionsToShow)
                break;
                
            CreateInspectionListItem(fileInfo);
            itemCount++;
        }
        
        Debug.Log($"Created {itemCount} inspection list items");
        
        // Force layout update after creating items and update clipping
        StartCoroutine(ForceLayoutUpdateNextFrame());
        if (enableBoundsClipping)
        {
            UpdateListItemVisibility();
        }
    }
    
    InspectionFileInfo[] GetInspectionFiles()
    {
        try
        {
            string basePath = Application.persistentDataPath;
            string[] files = Directory.GetFiles(basePath, "bridge_inspection_*.json");
            
            var fileInfos = new List<InspectionFileInfo>();
            
            foreach (string filePath in files)
            {
                try
                {
                    var fileInfo = new InspectionFileInfo
                    {
                        filePath = filePath,
                        fileName = Path.GetFileName(filePath),
                        creationTime = File.GetCreationTime(filePath)
                    };
                    
                    // Try to read basic info from the file
                    string jsonContent = File.ReadAllText(filePath);
                    var inspectionData = JsonUtility.FromJson<MultiFieldInspectionData>(jsonContent);
                    
                    if (inspectionData != null)
                    {
                        fileInfo.timestamp = inspectionData.timestamp;
                        fileInfo.connectedElement = inspectionData.connectedElementName ?? "No element";
                        fileInfo.inspectionId = inspectionData.inspectionId;
                        
                        // Create summary of content
                        var contentParts = new List<string>();
                        if (!string.IsNullOrEmpty(inspectionData.textField1)) contentParts.Add("Text1");
                        if (!string.IsNullOrEmpty(inspectionData.textField2)) contentParts.Add("Text2");
                        if (!string.IsNullOrEmpty(inspectionData.inputField1)) contentParts.Add("Input1");
                        
                        fileInfo.contentSummary = contentParts.Count > 0 ? string.Join(", ", contentParts) : "Empty";
                    }
                    
                    fileInfos.Add(fileInfo);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Could not read inspection file {Path.GetFileName(filePath)}: {e.Message}");
                }
            }
            
            // Sort by creation time
            if (newestFirst)
                return fileInfos.OrderByDescending(f => f.creationTime).ToArray();
            else
                return fileInfos.OrderBy(f => f.creationTime).ToArray();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error getting inspection files: {e.Message}");
            return new InspectionFileInfo[0];
        }
    }
    
    void CreateInspectionListItem(InspectionFileInfo fileInfo)
    {
        if (listItemPrefab == null || listContainer == null)
        {
            Debug.LogError("List item prefab or container not assigned!");
            return;
        }
        
        // Create the list item
        GameObject listItem = Instantiate(listItemPrefab, listContainer);
        
        // Setup the size for this item
        UpdateListItemSize(listItem);
        
        // Find and setup the text component
        TextMeshProUGUI[] textComponents = listItem.GetComponentsInChildren<TextMeshProUGUI>();
        if (textComponents.Length > 0)
        {
            string displayText = CreateDisplayText(fileInfo);
            textComponents[0].text = displayText;
        }
        
        // Setup the button to load this inspection
        PressableButton button = listItem.GetComponent<PressableButton>();
        if (button == null)
            button = listItem.GetComponentInChildren<PressableButton>();
        
        if (button != null)
        {
            button.OnClicked.AddListener(() => LoadSelectedInspection(fileInfo));
        }
        
        listItems.Add(listItem);
        
        Debug.Log($"Created list item for {fileInfo.fileName} with height {itemHeight}");
    }
    
    void UpdateListItemSize(GameObject listItem)
    {
        // Ensure the list item has proper size
        RectTransform rectTransform = listItem.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            // Set the size based on current settings
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, itemHeight);
        }
        
        // Add/Update LayoutElement for better control
        var layoutElement = listItem.GetComponent<UnityEngine.UI.LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = listItem.AddComponent<UnityEngine.UI.LayoutElement>();
        }
        
        layoutElement.preferredHeight = itemHeight;
        layoutElement.flexibleHeight = 0f;
    }
    
    string CreateDisplayText(InspectionFileInfo fileInfo)
    {
        // Create a nice display format
        string timeStr = "";
        if (!string.IsNullOrEmpty(fileInfo.timestamp))
        {
            try
            {
                DateTime dt = DateTime.Parse(fileInfo.timestamp);
                timeStr = dt.ToString("MMM dd, HH:mm");
            }
            catch
            {
                timeStr = fileInfo.creationTime.ToString("MMM dd, HH:mm");
            }
        }
        else
        {
            timeStr = fileInfo.creationTime.ToString("MMM dd, HH:mm");
        }
        
        string elementStr = string.IsNullOrEmpty(fileInfo.connectedElement) ? "No element" : fileInfo.connectedElement;
        
        // Truncate long element names
        if (elementStr.Length > 20)
            elementStr = elementStr.Substring(0, 17) + "...";
        
        return $"{timeStr}\n{elementStr}\n{fileInfo.contentSummary}";
    }
    
    void CreateNoInspectionsItem()
    {
        if (listItemPrefab == null || listContainer == null)
            return;
        
        GameObject listItem = Instantiate(listItemPrefab, listContainer);
        
        // Find and setup the text component
        TextMeshProUGUI[] textComponents = listItem.GetComponentsInChildren<TextMeshProUGUI>();
        if (textComponents.Length > 0)
        {
            textComponents[0].text = "No inspections found\nSave an inspection to see it here";
        }
        
        // Disable the button
        PressableButton button = listItem.GetComponent<PressableButton>();
        if (button != null)
            button.enabled = false;
        
        listItems.Add(listItem);
    }
    
    void LoadSelectedInspection(InspectionFileInfo fileInfo)
    {
        Debug.Log($"Loading inspection: {fileInfo.fileName}");
        
        if (textReader != null)
        {
            // Set the file to load and trigger the load
            textReader.LoadSpecificFile(fileInfo.fileName);
            
            // Close the list after loading
            if (listPanel != null)
            {
                listPanel.SetActive(false);
                isListVisible = false;
            }
            
            Debug.Log($"Loaded inspection from {fileInfo.timestamp} connected to {fileInfo.connectedElement}");
        }
        else
        {
            Debug.LogError("SimpleTextReader not found!");
        }
    }
    
    void ClearListItems()
    {
        foreach (GameObject item in listItems)
        {
            if (item != null)
                Destroy(item);
        }
        listItems.Clear();
    }
    
    /// <summary>
    /// Force update layout manually - useful for buttons or other scripts
    /// </summary>
    public void ForceUpdateLayout()
    {
        UpdateLayout();
        Debug.Log("Layout force updated manually");
    }
    
    /// <summary>
    /// Set spacing value programmatically (XR scale)
    /// </summary>
    public void SetSpacing(float spacing)
    {
        itemSpacing = Mathf.Clamp(spacing, 0f, 0.02f);
        UpdateLayout();
    }
    
    /// <summary>
    /// Set padding value programmatically (XR scale)
    /// </summary>
    public void SetPadding(float padding)
    {
        listPadding = Mathf.Clamp(padding, 0f, 0.015f);
        UpdateLayout();
    }
    
    /// <summary>
    /// Set item height programmatically (XR scale)
    /// </summary>
    public void SetItemHeight(float height)
    {
        itemHeight = Mathf.Clamp(height, 0.01f, 0.1f);
        UpdateLayout();
    }
    
    /// <summary>
    /// Reset layout to XR-optimized defaults
    /// </summary>
    public void ResetLayoutToDefaults()
    {
        itemSpacing = 0f;
        listPadding = 0f;
        itemHeight = 0.015f;
        UpdateLayout();
        Debug.Log("Layout reset to XR defaults");
    }
    
    /// <summary>
    /// Public method to refresh list - can be called from other scripts
    /// </summary>
    public void UpdateList()
    {
        RefreshInspectionList();
    }
    
    /// <summary>
    /// Public method to hide the list
    /// </summary>
    public void HideList()
    {
        if (isListVisible)
            ToggleInspectionList();
    }
    
    void OnDestroy()
    {
        ClearListItems();
    }
    
    // Bounds clipping methods (adapted from TileMenuManager)
    void UpdateListItemVisibility()
    {
        if (!enableBoundsClipping) return;
        
        Bounds clipBounds = GetClippingBounds();
        
        foreach (GameObject item in listItems)
        {
            if (item != null)
            {
                bool shouldBeVisible = IsItemInBounds(item, clipBounds);
                
                // Only change visibility if it's different to avoid unnecessary calls
                if (item.activeSelf != shouldBeVisible)
                {
                    item.SetActive(shouldBeVisible);
                }
            }
        }
    }
    
    Bounds GetClippingBounds()
    {
        switch (clippingMode)
        {
            case ClippingMode.UseListPanel:
                return GetBoundsFromRectTransform(listPanel);
                
            case ClippingMode.UseListContainer:
                return GetBoundsFromRectTransform(listContainer.gameObject);
                
            case ClippingMode.UseScrollViewport:
                return GetScrollViewportBounds();
                
            case ClippingMode.Manual:
            default:
                return GetManualBounds();
        }
    }
    
    Bounds GetBoundsFromRectTransform(GameObject target)
    {
        if (target == null) return GetManualBounds();
        
        RectTransform rectTransform = target.GetComponent<RectTransform>();
        if (rectTransform == null) return GetManualBounds();
        
        // Get world corners of the RectTransform
        Vector3[] worldCorners = new Vector3[4];
        rectTransform.GetWorldCorners(worldCorners);
        
        // Calculate center and size from corners
        Vector3 min = worldCorners[0]; // Bottom-left
        Vector3 max = worldCorners[2]; // Top-right
        
        Vector3 center = (min + max) * 0.5f + clippingCenter;
        Vector3 size = max - min;
        
        return new Bounds(center, size);
    }
    
    Bounds GetScrollViewportBounds()
    {
        // Look for a ScrollRect in parents
        ScrollRect scrollRect = listContainer.GetComponentInParent<ScrollRect>();
        if (scrollRect != null && scrollRect.viewport != null)
        {
            return GetBoundsFromRectTransform(scrollRect.viewport.gameObject);
        }
        
        // Fallback to list panel
        return GetBoundsFromRectTransform(listPanel);
    }
    
    Bounds GetManualBounds()
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
            // Use manual settings relative to list container
            center = listContainer.position + clippingCenter;
            size = clippingSize;
        }
        
        return new Bounds(center, size);
    }
    
    bool IsItemInBounds(GameObject item, Bounds bounds)
    {
        // Get the item's world position
        Vector3 itemPosition = item.transform.position;
        
        // For more precise checking, you can use bounds intersection:
        Bounds itemBounds = GetItemBounds(item);
        return bounds.Intersects(itemBounds);
        
        // Simple point-in-bounds check (alternative)
        // return bounds.Contains(itemPosition);
    }
    
    // Get bounds for a list item
    Bounds GetItemBounds(GameObject item)
    {
        Bounds itemBounds = new Bounds(item.transform.position, Vector3.zero);
        
        // Get all renderers to calculate total bounds
        Renderer[] renderers = item.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            itemBounds.Encapsulate(renderer.bounds);
        }
        
        // If no renderers found, use the item height as default size
        if (renderers.Length == 0)
        {
            // For UI elements, use the item height and estimate width
            itemBounds.size = new Vector3(0.3f, itemHeight, 0.01f);
        }
        
        return itemBounds;
    }
    
    // Public method to manually update visibility (useful for scrolling)
    public void UpdateClipping()
    {
        if (enableBoundsClipping)
        {
            UpdateListItemVisibility();
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

/// <summary>
/// Information about an inspection file
/// </summary>
[System.Serializable]
public class InspectionFileInfo
{
    public string filePath;
    public string fileName;
    public DateTime creationTime;
    public string timestamp;
    public string connectedElement;
    public string inspectionId;
    public string contentSummary;
}