using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Helper script for loading and restoring loop system data from saved inspections
/// This script can recreate dots, lines, and shaded areas from saved loop data
/// </summary>
public class LoopSystemLoader : MonoBehaviour
{
    [Header("Loop System Components")]
    [Tooltip("Reference to DotPlacementHandler")]
    public DotPlacementHandler_MRTK3 dotPlacementHandler;
    
    [Tooltip("Reference to LineConnectionHandler")]
    public LineConnectionHandler_MRTK3 lineConnectionHandler;
    
    [Tooltip("Reference to LoopAreaShader")]
    public LoopAreaShader_MRTK3 loopAreaShader;
    
    [Header("Loading Settings")]
    [Tooltip("Delay between placing dots to allow system to process")]
    public float dotPlacementDelay = 0.1f;
    
    [Tooltip("Delay between creating loops")]
    public float loopCreationDelay = 0.2f;
    
    [Tooltip("Enable debug logging")]
    public bool debugMode = true;
    
    void Start()
    {
        // Find components if not assigned
        if (dotPlacementHandler == null)
            dotPlacementHandler = FindObjectOfType<DotPlacementHandler_MRTK3>();
        if (lineConnectionHandler == null)
            lineConnectionHandler = FindObjectOfType<LineConnectionHandler_MRTK3>();
        if (loopAreaShader == null)
            loopAreaShader = FindObjectOfType<LoopAreaShader_MRTK3>();
        
        if (debugMode)
        {
            Debug.Log("=== LOOP SYSTEM LOADER INITIALIZED ===");
            Debug.Log($"DotPlacementHandler: {(dotPlacementHandler != null ? "Found" : "Missing")}");
            Debug.Log($"LineConnectionHandler: {(lineConnectionHandler != null ? "Found" : "Missing")}");
            Debug.Log($"LoopAreaShader: {(loopAreaShader != null ? "Found" : "Missing")}");
        }
    }
    
    /// <summary>
    /// Load and restore a complete loop system from saved data
    /// </summary>
    public void LoadLoopSystem(LoopSystemData loopSystemData)
    {
        if (loopSystemData == null || loopSystemData.loops == null)
        {
            Debug.LogWarning("LoopSystemLoader: No loop data to load");
            return;
        }
        
        if (debugMode)
        {
            Debug.Log("=== LOADING LOOP SYSTEM ===");
            Debug.Log($"Total loops to restore: {loopSystemData.loops.Count}");
            Debug.Log($"Completed loops: {loopSystemData.totalCompletedLoops}");
            Debug.Log($"Current loop dots: {loopSystemData.currentLoopDotCount}");
        }
        
        // Clear existing loop system first
        ClearCurrentLoopSystem();
        
        // Start the restoration process
        StartCoroutine(RestoreLoopsCoroutine(loopSystemData));
    }
    
    /// <summary>
    /// Clear the current loop system
    /// </summary>
    public void ClearCurrentLoopSystem()
    {
        if (dotPlacementHandler != null)
        {
            dotPlacementHandler.ClearAllDots();
        }
        
        if (lineConnectionHandler != null)
        {
            lineConnectionHandler.ClearAllLines();
        }
        
        if (loopAreaShader != null)
        {
            loopAreaShader.ClearAllShadedAreas();
        }
        
        if (debugMode)
            Debug.Log("LoopSystemLoader: Cleared existing loop system");
    }
    
    /// <summary>
    /// Coroutine to restore loops with proper timing
    /// </summary>
    IEnumerator RestoreLoopsCoroutine(LoopSystemData loopSystemData)
    {
        if (dotPlacementHandler == null)
        {
            Debug.LogError("LoopSystemLoader: DotPlacementHandler not found - cannot restore loops");
            yield break;
        }
        
        for (int loopIndex = 0; loopIndex < loopSystemData.loops.Count; loopIndex++)
        {
            LoopData loopData = loopSystemData.loops[loopIndex];
            
            if (debugMode)
                Debug.Log($"Restoring loop {loopIndex + 1}: {loopData.dotCount} dots, Completed: {loopData.isCompleted}");
            
            // Restore this loop
            yield return StartCoroutine(RestoreSingleLoop(loopData, loopIndex));
            
            // Wait between loops
            yield return new WaitForSeconds(loopCreationDelay);
        }
        
        // Final update to ensure everything is properly connected
        yield return new WaitForSeconds(0.1f);
        
        if (lineConnectionHandler != null)
        {
            lineConnectionHandler.ForceUpdateLines();
        }
        
        if (loopAreaShader != null)
        {
            loopAreaShader.ForceUpdateAllAreas();
        }
        
        if (debugMode)
            Debug.Log("=== LOOP SYSTEM RESTORATION COMPLETE ===");
    }
    
    /// <summary>
    /// Restore a single loop from saved data
    /// </summary>
    IEnumerator RestoreSingleLoop(LoopData loopData, int loopIndex)
    {
        if (loopData.positions == null || loopData.positions.Length == 0)
        {
            Debug.LogWarning($"LoopSystemLoader: Loop {loopIndex} has no position data");
            yield break;
        }
        
        // Place dots at each saved position
        for (int dotIndex = 0; dotIndex < loopData.positions.Length; dotIndex++)
        {
            Vector3 position = loopData.positions[dotIndex];
            
            // Calculate a surface normal (you might want to improve this based on your needs)
            Vector3 normal = Vector3.up;
            
            // Place the dot using the dot placement handler's method
            PlaceDotAtPosition(position, normal);
            
            if (debugMode)
                Debug.Log($"Placed dot {dotIndex + 1} at {position} for loop {loopIndex + 1}");
            
            // Wait between dot placements
            yield return new WaitForSeconds(dotPlacementDelay);
        }
        
        // If this was a completed loop, close it
        if (loopData.isCompleted && loopData.positions.Length >= 3)
        {
            yield return new WaitForSeconds(dotPlacementDelay);
            
            // Force close the current loop
            ForceCloseCurrentLoop();
            
            if (debugMode)
                Debug.Log($"Closed loop {loopIndex + 1}");
        }
    }
    
    /// <summary>
    /// Place a dot at a specific position using reflection to access private methods
    /// </summary>
    void PlaceDotAtPosition(Vector3 position, Vector3 normal)
    {
        if (dotPlacementHandler == null) return;
        
        try
        {
            // Use reflection to call the private PlaceDotAtPosition method
            var method = typeof(DotPlacementHandler_MRTK3).GetMethod("PlaceDotAtPosition", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                method.Invoke(dotPlacementHandler, new object[] { position, normal });
            }
            else
            {
                Debug.LogError("LoopSystemLoader: Could not find PlaceDotAtPosition method");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"LoopSystemLoader: Error placing dot at {position}: {e.Message}");
        }
    }
    
    /// <summary>
    /// Force close the current loop using reflection
    /// </summary>
    void ForceCloseCurrentLoop()
    {
        if (dotPlacementHandler == null) return;
        
        try
        {
            // Use reflection to call the private CloseCurrentLoop method
            var method = typeof(DotPlacementHandler_MRTK3).GetMethod("CloseCurrentLoop", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                method.Invoke(dotPlacementHandler, null);
            }
            else
            {
                Debug.LogError("LoopSystemLoader: Could not find CloseCurrentLoop method");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"LoopSystemLoader: Error closing loop: {e.Message}");
        }
    }
    
    /// <summary>
    /// Public method to test loop restoration with sample data
    /// </summary>
    [ContextMenu("Test Loop Restoration")]
    public void TestLoopRestoration()
    {
        // Create sample loop data for testing
        var testLoopData = new LoopSystemData
        {
            loops = new List<LoopData>
            {
                new LoopData
                {
                    loopIndex = 0,
                    isCompleted = true,
                    dotCount = 4,
                    positions = new Vector3[]
                    {
                        new Vector3(0, 0, 0),
                        new Vector3(1, 0, 0),
                        new Vector3(1, 0, 1),
                        new Vector3(0, 0, 1)
                    }
                }
            },
            totalCompletedLoops = 1,
            currentLoopDotCount = 0
        };
        
        LoadLoopSystem(testLoopData);
    }
    
    /// <summary>
    /// Get current loop system state for comparison
    /// </summary>
    public LoopSystemData GetCurrentLoopSystemState()
    {
        var currentState = new LoopSystemData();
        
        if (dotPlacementHandler != null)
        {
            var allLoops = dotPlacementHandler.GetAllLoopPositions();
            currentState.loops = new List<LoopData>();
            
            int completedLoops = dotPlacementHandler.GetCompletedLoopCount();
            
            for (int i = 0; i < allLoops.Count; i++)
            {
                var loopPositions = allLoops[i];
                var loopInfo = new LoopData
                {
                    loopIndex = i,
                    isCompleted = i < completedLoops,
                    dotCount = loopPositions.Count,
                    positions = loopPositions.ToArray()
                };
                
                currentState.loops.Add(loopInfo);
            }
            
            currentState.totalCompletedLoops = completedLoops;
            currentState.currentLoopDotCount = dotPlacementHandler.GetCurrentLoopDotCount();
        }
        
        if (lineConnectionHandler != null)
        {
            currentState.totalLineCount = lineConnectionHandler.GetLineCount();
        }
        
        if (loopAreaShader != null)
        {
            currentState.shadedAreaCount = loopAreaShader.GetShadedAreaCount();
        }
        
        return currentState;
    }
    
    /// <summary>
    /// Compare two loop system states for debugging
    /// </summary>
    public void CompareLoopStates(LoopSystemData savedState, LoopSystemData currentState)
    {
        Debug.Log("=== LOOP STATE COMPARISON ===");
        Debug.Log($"Saved loops: {savedState.loops?.Count ?? 0}, Current loops: {currentState.loops?.Count ?? 0}");
        Debug.Log($"Saved completed: {savedState.totalCompletedLoops}, Current completed: {currentState.totalCompletedLoops}");
        Debug.Log($"Saved current dots: {savedState.currentLoopDotCount}, Current dots: {currentState.currentLoopDotCount}");
        Debug.Log($"Saved lines: {savedState.totalLineCount}, Current lines: {currentState.totalLineCount}");
        Debug.Log($"Saved areas: {savedState.shadedAreaCount}, Current areas: {currentState.shadedAreaCount}");
    }
}