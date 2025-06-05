using UnityEngine;
using System.Collections.Generic;

public class LineConnectionHandler_MRTK3 : MonoBehaviour
{
    [Header("Line Settings")]
    [Tooltip("Material for the connecting lines")]
    public Material lineMaterial;
    
    [Tooltip("Width of the connecting lines")]
    public float lineWidth = 0.01f;
    
    [Tooltip("Color of the connecting lines")]
    public Color lineColor = Color.blue;
    
    [Tooltip("Should lines be drawn automatically when dots are placed")]
    public bool autoDrawLines = true;
    
    [Tooltip("Parent object for all line objects")]
    public Transform linesParent;
    
    [Header("Debug")]
    public bool debugMode = true;
    
    // Private fields
    private DotPlacementHandler_MRTK3 dotHandler;
    private List<LineRenderer> connectionLines = new List<LineRenderer>();
    private List<GameObject> lineObjects = new List<GameObject>();
    
    // Store previous values for change detection
    private float previousLineWidth;
    private Color previousLineColor;
    
    void Start()
    {
        // Find the dot placement handler
        dotHandler = FindObjectOfType<DotPlacementHandler_MRTK3>();
        
        if (dotHandler == null)
        {
            Debug.LogError("LineConnectionHandler: Could not find DotPlacementHandler_MRTK3 script!");
            return;
        }
        
        // Create lines parent if not assigned
        if (linesParent == null)
        {
            GameObject linesParentObj = new GameObject("Connection Lines");
            linesParent = linesParentObj.transform;
        }
        
        // Create default line material if not assigned
        if (lineMaterial == null)
        {
            CreateDefaultLineMaterial();
        }
        
        // Initialize previous values
        previousLineWidth = lineWidth;
        previousLineColor = lineColor;
        
        if (debugMode)
            Debug.Log("LineConnectionHandler: Initialized successfully");
    }
    
    void CreateDefaultLineMaterial()
    {
        lineMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        lineMaterial.color = lineColor;
        lineMaterial.name = "DefaultLineMaterial";
    }
    
    void Update()
    {
        // Check for parameter changes
        CheckForParameterChanges();
        
        // Auto-update lines if enabled and dots have changed
        if (autoDrawLines && dotHandler != null)
        {
            UpdateConnectionLines();
        }
    }
    
    void CheckForParameterChanges()
    {
        bool needsUpdate = false;
        
        // Check if line width changed
        if (Mathf.Abs(lineWidth - previousLineWidth) > 0.0001f)
        {
            previousLineWidth = lineWidth;
            needsUpdate = true;
        }
        
        // Check if line color changed
        if (lineColor != previousLineColor)
        {
            previousLineColor = lineColor;
            needsUpdate = true;
        }
        
        // Update all lines if any parameter changed
        if (needsUpdate)
        {
            UpdateAllLineProperties();
        }
    }
    
    void UpdateAllLineProperties()
    {
        // Update material color
        if (lineMaterial != null)
        {
            lineMaterial.color = lineColor;
        }
        
        // Update all existing line renderers
        foreach (LineRenderer line in connectionLines)
        {
            if (line != null)
            {
                line.startWidth = lineWidth;
                line.endWidth = lineWidth;
                line.material = lineMaterial;
            }
        }
        
        if (debugMode)
            Debug.Log("LineConnectionHandler: Updated all line properties");
    }
    
    public void UpdateConnectionLines()
    {
        if (dotHandler == null) return;
        
        var dotPositions = dotHandler.GetAllDotPositions();
        int requiredLines = Mathf.Max(0, dotPositions.Count - 1);
        
        // Remove excess lines if we have too many
        while (connectionLines.Count > requiredLines)
        {
            RemoveLastLine();
        }
        
        // Add new lines if we need more
        while (connectionLines.Count < requiredLines)
        {
            CreateNewLine();
        }
        
        // Update all line positions
        for (int i = 0; i < connectionLines.Count && i < requiredLines; i++)
        {
            if (connectionLines[i] != null && i + 1 < dotPositions.Count)
            {
                UpdateLinePositions(connectionLines[i], dotPositions[i], dotPositions[i + 1]);
            }
        }
    }
    
    void CreateNewLine()
    {
        GameObject lineObj = new GameObject($"ConnectionLine_{connectionLines.Count + 1}");
        lineObj.transform.SetParent(linesParent);
        
        LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>();
        
        // Configure the line renderer
        lineRenderer.material = lineMaterial;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        
        // Disable shadows and lighting for better performance
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        
        connectionLines.Add(lineRenderer);
        lineObjects.Add(lineObj);
        
        if (debugMode)
            Debug.Log($"LineConnectionHandler: Created line #{connectionLines.Count}");
    }
    
    void RemoveLastLine()
    {
        if (connectionLines.Count > 0)
        {
            int lastIndex = connectionLines.Count - 1;
            
            if (connectionLines[lastIndex] != null)
            {
                Destroy(lineObjects[lastIndex]);
            }
            
            connectionLines.RemoveAt(lastIndex);
            lineObjects.RemoveAt(lastIndex);
            
            if (debugMode)
                Debug.Log("LineConnectionHandler: Removed last line");
        }
    }
    
    void UpdateLinePositions(LineRenderer line, Vector3 startPos, Vector3 endPos)
    {
        line.SetPosition(0, startPos);
        line.SetPosition(1, endPos);
    }
    
    public void ClearAllLines()
    {
        foreach (GameObject lineObj in lineObjects)
        {
            if (lineObj != null)
                Destroy(lineObj);
        }
        
        connectionLines.Clear();
        lineObjects.Clear();
        
        if (debugMode)
            Debug.Log("LineConnectionHandler: Cleared all lines");
    }
    
    public void ToggleLineVisibility()
    {
        if (linesParent != null)
        {
            bool isActive = linesParent.gameObject.activeSelf;
            linesParent.gameObject.SetActive(!isActive);
            
            if (debugMode)
                Debug.Log($"LineConnectionHandler: Lines are now {(!isActive ? "visible" : "hidden")}");
        }
    }
    
    public void SetAutoDrawLines(bool enabled)
    {
        autoDrawLines = enabled;
        
        if (debugMode)
            Debug.Log($"LineConnectionHandler: Auto draw lines set to {enabled}");
    }
    
    public void ForceUpdateLines()
    {
        UpdateConnectionLines();
        
        if (debugMode)
            Debug.Log("LineConnectionHandler: Forced line update");
    }
    
    public int GetLineCount()
    {
        return connectionLines.Count;
    }
    
    public List<Vector3[]> GetAllLineSegments()
    {
        List<Vector3[]> segments = new List<Vector3[]>();
        
        foreach (LineRenderer line in connectionLines)
        {
            if (line != null)
            {
                Vector3[] positions = new Vector3[line.positionCount];
                line.GetPositions(positions);
                segments.Add(positions);
            }
        }
        
        return segments;
    }
    
    void OnValidate()
    {
        // Ensure positive values
        lineWidth = Mathf.Max(0.001f, lineWidth);
    }
    
    void OnDestroy()
    {
        // Clean up
        ClearAllLines();
        
        if (lineMaterial != null && lineMaterial.name == "DefaultLineMaterial")
            Destroy(lineMaterial);
    }
}