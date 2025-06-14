using UnityEngine;
using System.Collections.Generic;

public class MeasurementLineConnectionHandler_MRTK3 : MonoBehaviour
{
    [Header("Line Settings")]
    [Tooltip("Material for the connecting lines")]
    public Material lineMaterial;
    
    [Tooltip("Material for closed loop lines")]
    public Material closedLoopLineMaterial;
    
    [Tooltip("Width of the connecting lines")]
    public float lineWidth = 0.01f;
    
    [Tooltip("Color of the connecting lines")]
    public Color lineColor = Color.cyan;
    
    [Tooltip("Color of closed loop lines")]
    public Color closedLoopColor = Color.magenta;
    
    [Tooltip("Should lines be drawn automatically when dots are placed")]
    public bool autoDrawLines = true;
    
    [Tooltip("Parent object for all line objects")]
    public Transform linesParent;
    
    [Header("Debug")]
    public bool debugMode = true;
    
    // Private fields
    private MeasurementDotPlacementHandler_MRTK3 dotHandler;
    private List<List<LineRenderer>> loopLines = new List<List<LineRenderer>>(); // Lines for each completed loop
    private List<List<GameObject>> loopLineObjects = new List<List<GameObject>>(); // GameObjects for each completed loop
    private List<LineRenderer> currentLoopLines = new List<LineRenderer>(); // Lines for current loop
    private List<GameObject> currentLoopLineObjects = new List<GameObject>(); // GameObjects for current loop
    
    // Store previous values for change detection
    private float previousLineWidth;
    private Color previousLineColor;
    private Color previousClosedLoopColor;
    
    void Start()
    {
        // Find the measurement dot placement handler
        dotHandler = FindObjectOfType<MeasurementDotPlacementHandler_MRTK3>();
        
        if (dotHandler == null)
        {
            Debug.LogError("MeasurementLineConnectionHandler: Could not find MeasurementDotPlacementHandler_MRTK3 script!");
            return;
        }
        
        // Subscribe to loop events
        dotHandler.OnMeasurementLoopClosed += OnLoopClosed;
        dotHandler.OnMeasurementNewLoopStarted += OnNewLoopStarted;
        
        // Create lines parent if not assigned
        if (linesParent == null)
        {
            GameObject linesParentObj = new GameObject("Measurement Connection Lines");
            linesParent = linesParentObj.transform;
        }
        
        // Create default line materials if not assigned
        if (lineMaterial == null)
        {
            CreateDefaultLineMaterial();
        }
        
        if (closedLoopLineMaterial == null)
        {
            CreateDefaultClosedLoopLineMaterial();
        }
        
        // Initialize previous values
        previousLineWidth = lineWidth;
        previousLineColor = lineColor;
        previousClosedLoopColor = closedLoopColor;
        
        if (debugMode)
            Debug.Log("MeasurementLineConnectionHandler: Initialized successfully with loop support");
    }
    
    void CreateDefaultLineMaterial()
    {
        lineMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        lineMaterial.color = lineColor;
        lineMaterial.name = "MeasurementDefaultLineMaterial";
    }
    
    void CreateDefaultClosedLoopLineMaterial()
    {
        closedLoopLineMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        closedLoopLineMaterial.color = closedLoopColor;
        closedLoopLineMaterial.name = "MeasurementDefaultClosedLoopLineMaterial";
    }
    
    void Update()
    {
        // Check for parameter changes
        CheckForParameterChanges();
        
        // Auto-update lines if enabled and dots have changed
        if (autoDrawLines && dotHandler != null)
        {
            UpdateCurrentLoopLines();
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
        
        // Check if closed loop color changed
        if (closedLoopColor != previousClosedLoopColor)
        {
            previousClosedLoopColor = closedLoopColor;
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
        // Update material colors
        if (lineMaterial != null)
        {
            lineMaterial.color = lineColor;
        }
        
        if (closedLoopLineMaterial != null)
        {
            closedLoopLineMaterial.color = closedLoopColor;
        }
        
        // Update all current loop line renderers
        foreach (LineRenderer line in currentLoopLines)
        {
            if (line != null)
            {
                line.startWidth = lineWidth;
                line.endWidth = lineWidth;
                line.material = lineMaterial;
            }
        }
        
        // Update all completed loop line renderers
        foreach (var loopLineList in loopLines)
        {
            foreach (LineRenderer line in loopLineList)
            {
                if (line != null)
                {
                    line.startWidth = lineWidth;
                    line.endWidth = lineWidth;
                    line.material = closedLoopLineMaterial;
                }
            }
        }
        
        if (debugMode)
            Debug.Log("MeasurementLineConnectionHandler: Updated all line properties");
    }
    
    void OnLoopClosed(int loopIndex)
    {
        if (debugMode)
            Debug.Log($"MeasurementLineConnectionHandler: Loop {loopIndex + 1} closed, creating closed loop lines");
        
        // Move current loop lines to completed loops
        loopLines.Add(new List<LineRenderer>(currentLoopLines));
        loopLineObjects.Add(new List<GameObject>(currentLoopLineObjects));
        
        // Get the loop positions
        var allLoops = dotHandler.GetAllLoopPositions();
        if (loopIndex < allLoops.Count)
        {
            var closedLoopPositions = allLoops[loopIndex];
            
            // Add closing line from last dot back to first dot
            if (closedLoopPositions.Count > 2)
            {
                CreateClosingLine(loopIndex, closedLoopPositions[closedLoopPositions.Count - 1], closedLoopPositions[0]);
            }
            
            // Update all lines in this loop to use closed loop material
            UpdateLoopLinesToClosedStyle(loopIndex);
        }
        
        // Clear current loop lines for the new loop
        currentLoopLines.Clear();
        currentLoopLineObjects.Clear();
    }
    
    void OnNewLoopStarted(int loopIndex)
    {
        if (debugMode)
            Debug.Log($"MeasurementLineConnectionHandler: New loop {loopIndex + 1} started");
    }
    
    void CreateClosingLine(int loopIndex, Vector3 startPos, Vector3 endPos)
    {
        GameObject lineObj = new GameObject($"MeasurementLoop{loopIndex + 1}_ClosingLine");
        lineObj.transform.SetParent(linesParent);
        
        LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>();
        
        // Configure the line renderer
        lineRenderer.material = closedLoopLineMaterial;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        
        // Disable shadows and lighting for better performance
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        
        // Set positions
        lineRenderer.SetPosition(0, startPos);
        lineRenderer.SetPosition(1, endPos);
        
        // Add to the appropriate loop
        if (loopIndex < loopLines.Count)
        {
            loopLines[loopIndex].Add(lineRenderer);
            loopLineObjects[loopIndex].Add(lineObj);
        }
        
        if (debugMode)
            Debug.Log($"MeasurementLineConnectionHandler: Created closing line for loop {loopIndex + 1}");
    }
    
    void UpdateLoopLinesToClosedStyle(int loopIndex)
    {
        if (loopIndex < loopLines.Count)
        {
            foreach (LineRenderer line in loopLines[loopIndex])
            {
                if (line != null)
                {
                    line.material = closedLoopLineMaterial;
                }
            }
        }
    }
    
    void UpdateCurrentLoopLines()
    {
        if (dotHandler == null) return;
        
        // Get current loop dot count
        int currentLoopDotCount = dotHandler.GetCurrentLoopDotCount();
        int requiredLines = Mathf.Max(0, currentLoopDotCount - 1);
        
        // Remove excess lines if we have too many
        while (currentLoopLines.Count > requiredLines)
        {
            RemoveLastCurrentLoopLine();
        }
        
        // Add new lines if we need more
        while (currentLoopLines.Count < requiredLines)
        {
            CreateNewCurrentLoopLine();
        }
        
        // Update all current loop line positions
        var allLoops = dotHandler.GetAllLoopPositions();
        if (allLoops.Count > 0)
        {
            var currentLoopPositions = allLoops[allLoops.Count - 1]; // Last loop is current
            
            for (int i = 0; i < currentLoopLines.Count && i < requiredLines && i + 1 < currentLoopPositions.Count; i++)
            {
                if (currentLoopLines[i] != null)
                {
                    UpdateLinePositions(currentLoopLines[i], currentLoopPositions[i], currentLoopPositions[i + 1]);
                }
            }
        }
    }
    
    public void UpdateConnectionLines()
    {
        // This method now updates both current and completed loops
        UpdateCurrentLoopLines();
        UpdateAllCompletedLoops();
    }
    
    void UpdateAllCompletedLoops()
    {
        var allLoops = dotHandler.GetAllLoopPositions();
        int completedLoopCount = dotHandler.GetCompletedLoopCount();
        
        for (int loopIndex = 0; loopIndex < completedLoopCount && loopIndex < allLoops.Count; loopIndex++)
        {
            var loopPositions = allLoops[loopIndex];
            
            if (loopIndex < loopLines.Count)
            {
                var loopLineList = loopLines[loopIndex];
                
                // Update sequential lines (not including closing line)
                int sequentialLines = loopPositions.Count - 1;
                for (int lineIndex = 0; lineIndex < sequentialLines && lineIndex < loopLineList.Count - 1; lineIndex++)
                {
                    if (loopLineList[lineIndex] != null)
                    {
                        UpdateLinePositions(loopLineList[lineIndex], loopPositions[lineIndex], loopPositions[lineIndex + 1]);
                    }
                }
                
                // Update closing line (last line in the list)
                if (loopLineList.Count > 0 && loopPositions.Count > 2)
                {
                    var closingLine = loopLineList[loopLineList.Count - 1];
                    if (closingLine != null)
                    {
                        UpdateLinePositions(closingLine, loopPositions[loopPositions.Count - 1], loopPositions[0]);
                    }
                }
            }
        }
    }
    
    void CreateNewCurrentLoopLine()
    {
        GameObject lineObj = new GameObject($"MeasurementCurrentLoop_Line_{currentLoopLines.Count + 1}");
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
        
        currentLoopLines.Add(lineRenderer);
        currentLoopLineObjects.Add(lineObj);
        
        if (debugMode)
            Debug.Log($"MeasurementLineConnectionHandler: Created current loop line #{currentLoopLines.Count}");
    }
    
    void RemoveLastCurrentLoopLine()
    {
        if (currentLoopLines.Count > 0)
        {
            int lastIndex = currentLoopLines.Count - 1;
            
            if (currentLoopLineObjects[lastIndex] != null)
            {
                Destroy(currentLoopLineObjects[lastIndex]);
            }
            
            currentLoopLines.RemoveAt(lastIndex);
            currentLoopLineObjects.RemoveAt(lastIndex);
            
            if (debugMode)
                Debug.Log("MeasurementLineConnectionHandler: Removed last current loop line");
        }
    }
    
    void UpdateLinePositions(LineRenderer line, Vector3 startPos, Vector3 endPos)
    {
        line.SetPosition(0, startPos);
        line.SetPosition(1, endPos);
    }
    
    public void ClearAllLines()
    {
        // Clear current loop lines
        foreach (GameObject lineObj in currentLoopLineObjects)
        {
            if (lineObj != null)
                Destroy(lineObj);
        }
        currentLoopLines.Clear();
        currentLoopLineObjects.Clear();
        
        // Clear completed loop lines
        foreach (var loopLineObjList in loopLineObjects)
        {
            foreach (GameObject lineObj in loopLineObjList)
            {
                if (lineObj != null)
                    Destroy(lineObj);
            }
        }
        loopLines.Clear();
        loopLineObjects.Clear();
        
        if (debugMode)
            Debug.Log("MeasurementLineConnectionHandler: Cleared all lines and loops");
    }
    
    public void ToggleLineVisibility()
    {
        if (linesParent != null)
        {
            bool isActive = linesParent.gameObject.activeSelf;
            linesParent.gameObject.SetActive(!isActive);
            
            if (debugMode)
                Debug.Log($"MeasurementLineConnectionHandler: Lines are now {(!isActive ? "visible" : "hidden")}");
        }
    }
    
    public void SetAutoDrawLines(bool enabled)
    {
        autoDrawLines = enabled;
        
        if (debugMode)
            Debug.Log($"MeasurementLineConnectionHandler: Auto draw lines set to {enabled}");
    }
    
    public void ForceUpdateLines()
    {
        UpdateConnectionLines();
        
        if (debugMode)
            Debug.Log("MeasurementLineConnectionHandler: Forced line update for all loops");
    }
    
    public int GetLineCount()
    {
        int totalLines = currentLoopLines.Count;
        
        foreach (var loopLineList in loopLines)
        {
            totalLines += loopLineList.Count;
        }
        
        return totalLines;
    }
    
    public int GetCompletedLoopCount()
    {
        return loopLines.Count;
    }
    
    public List<Vector3[]> GetAllLineSegments()
    {
        List<Vector3[]> segments = new List<Vector3[]>();
        
        // Add current loop line segments
        foreach (LineRenderer line in currentLoopLines)
        {
            if (line != null)
            {
                Vector3[] positions = new Vector3[line.positionCount];
                line.GetPositions(positions);
                segments.Add(positions);
            }
        }
        
        // Add completed loop line segments
        foreach (var loopLineList in loopLines)
        {
            foreach (LineRenderer line in loopLineList)
            {
                if (line != null)
                {
                    Vector3[] positions = new Vector3[line.positionCount];
                    line.GetPositions(positions);
                    segments.Add(positions);
                }
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
        // Unsubscribe from events
        if (dotHandler != null)
        {
            dotHandler.OnMeasurementLoopClosed -= OnLoopClosed;
            dotHandler.OnMeasurementNewLoopStarted -= OnNewLoopStarted;
        }
        
        // Clean up
        ClearAllLines();
        
        if (lineMaterial != null && lineMaterial.name == "MeasurementDefaultLineMaterial")
            Destroy(lineMaterial);
            
        if (closedLoopLineMaterial != null && closedLoopLineMaterial.name == "MeasurementDefaultClosedLoopLineMaterial")
            Destroy(closedLoopLineMaterial);
    }
}