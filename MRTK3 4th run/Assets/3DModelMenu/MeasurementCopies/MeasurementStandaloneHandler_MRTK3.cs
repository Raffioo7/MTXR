using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class MeasurementStandaloneHandler_MRTK3 : MonoBehaviour
{
    [Header("Measurement Settings")]
    [Tooltip("Display measurements in metric (m/m²) or imperial (ft/ft²)")]
    public bool useMetricUnits = true;
    
    [Tooltip("Show measurements as floating text")]
    public bool showMeasurementLabels = true;
    
    [Tooltip("Update measurements in real-time as dots are placed")]
    public bool realTimeMeasurements = true;
    
    [Header("Label Settings")]
    [Tooltip("Prefab for measurement labels (TextMeshPro recommended)")]
    public GameObject labelPrefab;
    
    [Tooltip("Color for distance labels")]
    public Color distanceColor = Color.white;
    
    [Tooltip("Color for area labels")]
    public Color areaColor = Color.yellow;
    
    [Tooltip("Font size for labels")]
    public float fontSize = 0.1f;
    
    [Tooltip("Distance from surface to position labels for better readability")]
    public float labelSpacing = 0.1f;
    
    [Tooltip("Parent object for all measurement labels")]
    public Transform labelsParent;
    
    [Header("Debug")]
    public bool debugMode = true;
    
    // Private fields
    private MeasurementDotPlacementHandler_MRTK3 dotHandler;
    private List<List<GameObject>> loopDistanceLabels = new List<List<GameObject>>(); // Distance labels for each completed loop
    private List<GameObject> loopAreaLabels = new List<GameObject>(); // Area labels for each completed loop
    private List<GameObject> currentLoopDistanceLabels = new List<GameObject>(); // Distance labels for current loop
    
    // Cached measurements
    private List<List<float>> loopDistances = new List<List<float>>(); // Distances for each completed loop
    private List<float> loopAreas = new List<float>(); // Areas for each completed loop
    private List<float> currentLoopDistances = new List<float>(); // Distances for current loop
    
    void Start()
    {
        // Find the measurement dot placement handler
        dotHandler = FindObjectOfType<MeasurementDotPlacementHandler_MRTK3>();
        
        if (dotHandler == null)
        {
            Debug.LogError("MeasurementStandaloneHandler: Could not find MeasurementDotPlacementHandler_MRTK3 script!");
            return;
        }
        
        // Subscribe to loop events
        dotHandler.OnMeasurementLoopClosed += OnLoopClosed;
        dotHandler.OnMeasurementNewLoopStarted += OnNewLoopStarted;
        
        // Create labels parent if not assigned
        if (labelsParent == null)
        {
            GameObject labelsParentObj = new GameObject("Measurement Labels");
            labelsParent = labelsParentObj.transform;
        }
        
        // Create default label prefab if not assigned
        if (labelPrefab == null)
        {
            CreateDefaultLabelPrefab();
        }
        
        if (debugMode)
            Debug.Log("MeasurementStandaloneHandler: Initialized successfully");
    }
    
    void CreateDefaultLabelPrefab()
    {
        // Create a simple text label
        labelPrefab = new GameObject("MeasurementStandaloneLabel");
        
        // Add Canvas for world space text
        Canvas canvas = labelPrefab.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        
        // Add TextMeshPro component
        TextMeshProUGUI text = labelPrefab.AddComponent<TextMeshProUGUI>();
        text.text = "0cm";
        text.fontSize = fontSize * 100; // TextMeshPro uses different scale
        text.color = distanceColor;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        
        // Set canvas size
        RectTransform rectTransform = labelPrefab.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(2, 0.5f);
        
        // Add FaceCamera component
        labelPrefab.AddComponent<FaceCamera>();
        
        // Deactivate the prefab
        labelPrefab.SetActive(false);
    }
    
    void Update()
    {
        if (realTimeMeasurements && dotHandler != null)
        {
            UpdateCurrentLoopMeasurements();
        }
    }
    
    void OnLoopClosed(int loopIndex)
    {
        if (debugMode)
            Debug.Log($"MeasurementStandaloneHandler: Loop {loopIndex + 1} closed, calculating final measurements");
        
        // Move current measurements to completed loops
        loopDistances.Add(new List<float>(currentLoopDistances));
        loopDistanceLabels.Add(new List<GameObject>(currentLoopDistanceLabels));
        
        // Calculate and store area
        float area = CalculateCurrentLoopArea();
        loopAreas.Add(area);
        
        // Create area label
        CreateAreaLabel(loopIndex, area);
        
        // Update distance label colors to indicate completed loop
        UpdateCompletedLoopLabelColors(loopIndex);
        
        // Clear current loop data
        currentLoopDistances.Clear();
        currentLoopDistanceLabels.Clear();
        
        // Log measurements
        if (debugMode)
        {
            float totalDistance = GetLoopTotalDistance(loopIndex);
            Debug.Log($"MeasurementStandaloneHandler: Loop {loopIndex + 1} - Total Distance: {FormatDistance(totalDistance)}, Area: {FormatArea(area)}");
        }
    }
    
    void OnNewLoopStarted(int loopIndex)
    {
        if (debugMode)
            Debug.Log($"MeasurementStandaloneHandler: New loop {loopIndex + 1} started");
    }
    
    void UpdateCurrentLoopMeasurements()
    {
        var allLoops = dotHandler.GetAllLoopPositions();
        if (allLoops.Count == 0) return;
        
        var currentLoopPositions = allLoops[allLoops.Count - 1]; // Last loop is current
        int requiredDistances = Mathf.Max(0, currentLoopPositions.Count - 1);
        
        // Remove excess labels and distances
        while (currentLoopDistanceLabels.Count > requiredDistances)
        {
            RemoveLastCurrentDistanceLabel();
        }
        
        while (currentLoopDistances.Count > requiredDistances)
        {
            currentLoopDistances.RemoveAt(currentLoopDistances.Count - 1);
        }
        
        // Add new labels and calculate distances
        while (currentLoopDistanceLabels.Count < requiredDistances)
        {
            CreateCurrentDistanceLabel();
        }
        
        while (currentLoopDistances.Count < requiredDistances)
        {
            currentLoopDistances.Add(0f);
        }
        
        // Update distances and label positions
        for (int i = 0; i < requiredDistances && i + 1 < currentLoopPositions.Count; i++)
        {
            float distance = Vector3.Distance(currentLoopPositions[i], currentLoopPositions[i + 1]);
            currentLoopDistances[i] = distance;
            
            if (currentLoopDistanceLabels[i] != null)
            {
                UpdateDistanceLabel(currentLoopDistanceLabels[i], currentLoopPositions[i], currentLoopPositions[i + 1], distance);
            }
        }
    }
    
    void CreateCurrentDistanceLabel()
    {
        GameObject label = Instantiate(labelPrefab, labelsParent);
        label.SetActive(true);
        label.name = $"MeasurementCurrentLoop_Distance_{currentLoopDistanceLabels.Count + 1}";
        
        // Set color for current loop
        TextMeshProUGUI text = label.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.color = distanceColor;
        }
        
        currentLoopDistanceLabels.Add(label);
    }
    
    void RemoveLastCurrentDistanceLabel()
    {
        if (currentLoopDistanceLabels.Count > 0)
        {
            int lastIndex = currentLoopDistanceLabels.Count - 1;
            
            if (currentLoopDistanceLabels[lastIndex] != null)
            {
                Destroy(currentLoopDistanceLabels[lastIndex]);
            }
            
            currentLoopDistanceLabels.RemoveAt(lastIndex);
        }
    }
    
    void UpdateDistanceLabel(GameObject label, Vector3 startPos, Vector3 endPos, float distance)
    {
        // Position label at midpoint
        Vector3 midpoint = (startPos + endPos) * 0.5f;
        
        // Get surface normal at midpoint
        Vector3 surfaceNormal = GetSurfaceNormalAtPoint(midpoint);
        
        // Apply spacing perpendicular to surface
        label.transform.position = midpoint + surfaceNormal * labelSpacing;
        
        // Update text
        TextMeshProUGUI text = label.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = FormatDistance(distance);
        }
    }
    
    void CreateAreaLabel(int loopIndex, float area)
    {
        var allLoops = dotHandler.GetAllLoopPositions();
        if (loopIndex >= allLoops.Count) return;
        
        var loopPositions = allLoops[loopIndex];
        
        // Calculate centroid for label position
        Vector3 centroid = CalculateCentroid(loopPositions);
        
        // Get surface normal at centroid (same as distance labels)
        Vector3 surfaceNormal = GetSurfaceNormalAtPoint(centroid);
        
        GameObject areaLabel = Instantiate(labelPrefab, labelsParent);
        areaLabel.SetActive(true);
        areaLabel.name = $"MeasurementLoop{loopIndex + 1}_Area";
        
        // Apply spacing perpendicular to surface (same as distance labels)
        areaLabel.transform.position = centroid + surfaceNormal * labelSpacing;
        
        // Set area label properties
        TextMeshProUGUI text = areaLabel.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = FormatArea(area);
            text.color = areaColor;
            text.fontSize *= 1.2f; // Make area labels slightly larger
        }
        
        loopAreaLabels.Add(areaLabel);
    }
    
    void UpdateCompletedLoopLabelColors(int loopIndex)
    {
        if (loopIndex < loopDistanceLabels.Count)
        {
            foreach (GameObject label in loopDistanceLabels[loopIndex])
            {
                if (label != null)
                {
                    TextMeshProUGUI text = label.GetComponentInChildren<TextMeshProUGUI>();
                    if (text != null)
                    {
                        // Darken the color to indicate completed loop
                        text.color = distanceColor * 0.7f;
                    }
                }
            }
        }
    }
    
    Vector3 GetSurfaceNormalAtPoint(Vector3 point)
    {
        // Raycast downward to find surface normal
        RaycastHit hit;
        if (Physics.Raycast(point + Vector3.up * 0.1f, Vector3.down, out hit, 0.2f))
        {
            return hit.normal;
        }
        
        // Raycast upward to find surface normal
        if (Physics.Raycast(point + Vector3.down * 0.1f, Vector3.up, out hit, 0.2f))
        {
            return hit.normal;
        }
        
        // Try raycasting from multiple directions to find surface
        Vector3[] directions = {
            Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
            Vector3.up, Vector3.down
        };
        
        foreach (Vector3 direction in directions)
        {
            if (Physics.Raycast(point - direction * 0.05f, direction, out hit, 0.1f))
            {
                return hit.normal;
            }
        }
        
        // Fallback: use world up
        return Vector3.up;
    }
    
    float CalculateCurrentLoopArea()
    {
        var allLoops = dotHandler.GetAllLoopPositions();
        if (allLoops.Count == 0) return 0f;
        
        var positions = allLoops[allLoops.Count - 1];
        return CalculatePolygonArea(positions);
    }
    
    float CalculatePolygonArea(List<Vector3> positions)
    {
        if (positions.Count < 3) return 0f;
        
        // Calculate the normal of the polygon using Newell's method
        Vector3 normal = Vector3.zero;
        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 current = positions[i];
            Vector3 next = positions[(i + 1) % positions.Count];
            
            normal.x += (current.y - next.y) * (current.z + next.z);
            normal.y += (current.z - next.z) * (current.x + next.x);
            normal.z += (current.x - next.x) * (current.y + next.y);
        }
        normal = normal.normalized;
        
        // Project vertices onto a 2D plane
        Vector3 right = Vector3.Cross(normal, Vector3.up).normalized;
        if (right.magnitude < 0.001f)
        {
            right = Vector3.Cross(normal, Vector3.forward).normalized;
        }
        Vector3 forward = Vector3.Cross(right, normal).normalized;
        
        // Convert to 2D coordinates
        Vector2[] points2D = new Vector2[positions.Count];
        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 localPos = positions[i];
            points2D[i] = new Vector2(
                Vector3.Dot(localPos, right),
                Vector3.Dot(localPos, forward)
            );
        }
        
        // Calculate area using shoelace formula
        float area = 0f;
        for (int i = 0; i < points2D.Length; i++)
        {
            int j = (i + 1) % points2D.Length;
            area += points2D[i].x * points2D[j].y;
            area -= points2D[j].x * points2D[i].y;
        }
        
        return Mathf.Abs(area) * 0.5f;
    }
    
    Vector3 CalculateCentroid(List<Vector3> positions)
    {
        Vector3 centroid = Vector3.zero;
        foreach (Vector3 pos in positions)
        {
            centroid += pos;
        }
        return centroid / positions.Count;
    }
    
    string FormatDistance(float distance)
    {
        if (useMetricUnits)
        {
            // Always show in cm with no decimals
            return $"{(distance * 100f):F0}cm";
        }
        else
        {
            float feet = distance * 3.28084f;
            if (feet < 1f)
            {
                float inches = feet * 12f;
                return $"{inches:F1}in";
            }
            else
            {
                return $"{feet:F2}ft";
            }
        }
    }
    
    string FormatArea(float area)
    {
        if (useMetricUnits)
        {
            // Always show in m² with 1 decimal place
            return $"{area:F1}m²";
        }
        else
        {
            float sqFeet = area * 10.7639f;
            return $"{sqFeet:F2}ft²";
        }
    }
    
    // Public methods for getting measurements
    public float GetLoopTotalDistance(int loopIndex)
    {
        if (loopIndex < 0 || loopIndex >= loopDistances.Count) return 0f;
        
        float total = 0f;
        foreach (float distance in loopDistances[loopIndex])
        {
            total += distance;
        }
        
        return total;
    }
    
    public float GetLoopArea(int loopIndex)
    {
        if (loopIndex < 0 || loopIndex >= loopAreas.Count) return 0f;
        return loopAreas[loopIndex];
    }
    
    public float GetCurrentLoopTotalDistance()
    {
        float total = 0f;
        foreach (float distance in currentLoopDistances)
        {
            total += distance;
        }
        return total;
    }
    
    public float GetCurrentLoopArea()
    {
        return CalculateCurrentLoopArea();
    }
    
    public void ToggleMeasurementLabels()
    {
        if (labelsParent != null)
        {
            bool isActive = labelsParent.gameObject.activeSelf;
            labelsParent.gameObject.SetActive(!isActive);
            
            if (debugMode)
                Debug.Log($"MeasurementStandaloneHandler: Labels are now {(!isActive ? "visible" : "hidden")}");
        }
    }
    
    public void ToggleUnits()
    {
        useMetricUnits = !useMetricUnits;
        
        // Update all existing labels
        UpdateAllLabelTexts();
        
        if (debugMode)
            Debug.Log($"MeasurementStandaloneHandler: Switched to {(useMetricUnits ? "metric" : "imperial")} units");
    }
    
    void UpdateAllLabelTexts()
    {
        // Update current loop distance labels
        for (int i = 0; i < currentLoopDistanceLabels.Count && i < currentLoopDistances.Count; i++)
        {
            if (currentLoopDistanceLabels[i] != null)
            {
                TextMeshProUGUI text = currentLoopDistanceLabels[i].GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    text.text = FormatDistance(currentLoopDistances[i]);
                }
            }
        }
        
        // Update completed loop distance labels
        for (int loopIndex = 0; loopIndex < loopDistanceLabels.Count && loopIndex < loopDistances.Count; loopIndex++)
        {
            var labelList = loopDistanceLabels[loopIndex];
            var distanceList = loopDistances[loopIndex];
            
            for (int i = 0; i < labelList.Count && i < distanceList.Count; i++)
            {
                if (labelList[i] != null)
                {
                    TextMeshProUGUI text = labelList[i].GetComponentInChildren<TextMeshProUGUI>();
                    if (text != null)
                    {
                        text.text = FormatDistance(distanceList[i]);
                    }
                }
            }
        }
        
        // Update area labels
        for (int i = 0; i < loopAreaLabels.Count && i < loopAreas.Count; i++)
        {
            if (loopAreaLabels[i] != null)
            {
                TextMeshProUGUI text = loopAreaLabels[i].GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    text.text = FormatArea(loopAreas[i]);
                }
            }
        }
    }
    
    public void ClearAllMeasurements()
    {
        // Clear current loop labels
        foreach (GameObject label in currentLoopDistanceLabels)
        {
            if (label != null) Destroy(label);
        }
        currentLoopDistanceLabels.Clear();
        currentLoopDistances.Clear();
        
        // Clear completed loop labels
        foreach (var labelList in loopDistanceLabels)
        {
            foreach (GameObject label in labelList)
            {
                if (label != null) Destroy(label);
            }
        }
        loopDistanceLabels.Clear();
        loopDistances.Clear();
        
        // Clear area labels
        foreach (GameObject label in loopAreaLabels)
        {
            if (label != null) Destroy(label);
        }
        loopAreaLabels.Clear();
        loopAreas.Clear();
        
        if (debugMode)
            Debug.Log("MeasurementStandaloneHandler: Cleared all measurements");
    }
    
    public void PrintMeasurementSummary()
    {
        Debug.Log("=== MEASUREMENT SUMMARY ===");
        
        for (int i = 0; i < loopDistances.Count; i++)
        {
            float totalDistance = GetLoopTotalDistance(i);
            float area = GetLoopArea(i);
            Debug.Log($"Loop {i + 1}: Distance = {FormatDistance(totalDistance)}, Area = {FormatArea(area)}");
        }
        
        if (currentLoopDistances.Count > 0)
        {
            float currentDistance = GetCurrentLoopTotalDistance();
            float currentArea = GetCurrentLoopArea();
            Debug.Log($"Current Loop: Distance = {FormatDistance(currentDistance)}, Area = {FormatArea(currentArea)}");
        }
        
        Debug.Log("=========================");
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events
        if (dotHandler != null)
        {
            dotHandler.OnMeasurementLoopClosed -= OnLoopClosed;
            dotHandler.OnMeasurementNewLoopStarted -= OnNewLoopStarted;
        }
        
        // Clean up
        ClearAllMeasurements();
        
        if (labelPrefab != null && labelPrefab.name == "MeasurementStandaloneLabel")
        {
            Destroy(labelPrefab);
        }
    }
}