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
    /// Restore a single loop from saved data - FIXED FOR FIRST LOOP ISSUE
    /// </summary>
    IEnumerator RestoreSingleLoop(LoopData loopData, int loopIndex)
    {
        if (loopData.positions == null || loopData.positions.Length == 0)
        {
            Debug.LogWarning($"LoopSystemLoader: Loop {loopIndex} has no position data");
            yield break;
        }
        
        if (debugMode)
        {
            Debug.Log($"=== RESTORING LOOP {loopIndex + 1} ===");
            Debug.Log($"Expected dots: {loopData.dotCount}");
            Debug.Log($"Position data count: {loopData.positions.Length}");
            Debug.Log($"Is completed: {loopData.isCompleted}");
        }
        
        // SPECIAL HANDLING FOR FIRST LOOP (loopIndex == 0)
        if (loopIndex == 0)
        {
            // Give extra time for system initialization on first loop
            yield return new WaitForSeconds(dotPlacementDelay * 2);
            
            if (debugMode)
                Debug.Log("First loop detected - using enhanced restoration logic");
        }
        
        // Place all dots first
        for (int dotIndex = 0; dotIndex < loopData.positions.Length; dotIndex++)
        {
            Vector3 position = loopData.positions[dotIndex];
            Vector3 normal = Vector3.up;
            
            PlaceDotAtPosition(position, normal);
            
            if (debugMode)
                Debug.Log($"Placed dot {dotIndex + 1}/{loopData.positions.Length} at {position} for loop {loopIndex + 1}");
            
            // Extra delay for first loop dots to ensure proper processing
            float delayTime = (loopIndex == 0) ? dotPlacementDelay * 1.5f : dotPlacementDelay;
            yield return new WaitForSeconds(delayTime);
        }
        
        // If this was a completed loop, close it
        if (loopData.isCompleted && loopData.positions.Length >= 3)
        {
            // Extra delay before closing, especially for first loop
            float preCloseDelay = (loopIndex == 0) ? dotPlacementDelay * 3 : dotPlacementDelay;
            yield return new WaitForSeconds(preCloseDelay);
            
            if (debugMode)
            {
                int currentDots = dotPlacementHandler.GetCurrentLoopDotCount();
                Debug.Log($"Before closing - Current loop dot count: {currentDots}");
                
                // For first loop, do extra validation
                if (loopIndex == 0 && currentDots != loopData.dotCount)
                {
                    Debug.LogWarning($"FIRST LOOP DOT COUNT MISMATCH before closing! Expected: {loopData.dotCount}, Got: {currentDots}");
                }
            }
            
            // Use different closing strategy for first loop
            if (loopIndex == 0)
            {
                ForceCloseFirstLoopSafely(loopData);
            }
            else
            {
                ForceCloseCurrentLoopSafely();
            }
            
            if (debugMode)
            {
                Debug.Log($"After closing - Completed loops: {dotPlacementHandler.GetCompletedLoopCount()}");
                Debug.Log($"After closing - Current loop dots: {dotPlacementHandler.GetCurrentLoopDotCount()}");
                
                // Validate first loop closure
                if (loopIndex == 0)
                {
                    var allLoopsAfter = dotPlacementHandler.GetAllLoopPositions();
                    if (allLoopsAfter.Count > 0)
                    {
                        var firstLoop = allLoopsAfter[0];
                        if (firstLoop.Count != loopData.dotCount)
                        {
                            Debug.LogError($"FIRST LOOP FINAL COUNT MISMATCH! Expected: {loopData.dotCount}, Got: {firstLoop.Count}");
                        }
                        else
                        {
                            Debug.Log($"First loop successfully restored with {firstLoop.Count} dots");
                        }
                    }
                }
            }
        }
        else if (debugMode)
        {
            Debug.Log($"Loop {loopIndex + 1} left open (not completed or insufficient dots)");
        }
    }
    
    /// <summary>
    /// Special handling for closing the first loop with additional safety measures
    /// </summary>
    void ForceCloseFirstLoopSafely(LoopData originalLoopData)
    {
        if (dotPlacementHandler == null) return;
        
        if (debugMode)
            Debug.Log("Using special first loop closure logic");
        
        // Check if we have the expected number of dots before closing
        int currentDots = dotPlacementHandler.GetCurrentLoopDotCount();
        if (currentDots != originalLoopData.dotCount)
        {
            Debug.LogWarning($"First loop dot count mismatch before closure. Expected: {originalLoopData.dotCount}, Current: {currentDots}");
            
            // Try to fix by adding missing dots
            if (currentDots < originalLoopData.dotCount)
            {
                int missingDots = originalLoopData.dotCount - currentDots;
                Debug.LogWarning($"Attempting to add {missingDots} missing dots to first loop");
                
                // Add the missing dots from the end of the positions array
                for (int i = currentDots; i < originalLoopData.dotCount && i < originalLoopData.positions.Length; i++)
                {
                    PlaceDotAtPosition(originalLoopData.positions[i], Vector3.up);
                    if (debugMode)
                        Debug.Log($"Added missing dot {i + 1} at {originalLoopData.positions[i]}");
                }
            }
        }
        
        // Now close the loop
        ForceCloseCurrentLoopSafely();
    }
    
    /// <summary>
    /// Force close the current loop with additional safety checks
    /// </summary>
    void ForceCloseCurrentLoopSafely()
    {
        if (dotPlacementHandler == null) return;
        
        // Check if we have enough dots before closing
        int currentDots = dotPlacementHandler.GetCurrentLoopDotCount();
        if (currentDots < 3)
        {
            if (debugMode)
                Debug.LogWarning($"Cannot close loop - only {currentDots} dots, need at least 3");
            return;
        }
        
        try
        {
            // Store the current loop positions before closing (for debugging)
            List<Vector3> positionsBeforeClosing = new List<Vector3>();
            if (debugMode)
            {
                var allLoops = dotPlacementHandler.GetAllLoopPositions();
                if (allLoops.Count > 0)
                {
                    var currentLoop = allLoops[allLoops.Count - 1];
                    positionsBeforeClosing.AddRange(currentLoop);
                }
            }
            
            // Use reflection to call the private CloseCurrentLoop method
            var method = typeof(DotPlacementHandler_MRTK3).GetMethod("CloseCurrentLoop", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                method.Invoke(dotPlacementHandler, null);
                
                if (debugMode)
                {
                    Debug.Log($"Successfully closed loop with {currentDots} dots");
                    
                    // Verify the loop was closed correctly
                    var allLoopsAfter = dotPlacementHandler.GetAllLoopPositions();
                    int completedCount = dotPlacementHandler.GetCompletedLoopCount();
                    
                    if (allLoopsAfter.Count >= completedCount && completedCount > 0)
                    {
                        var closedLoop = allLoopsAfter[completedCount - 1];
                        if (closedLoop.Count != positionsBeforeClosing.Count)
                        {
                            Debug.LogWarning($"Dot count changed during closing! Before: {positionsBeforeClosing.Count}, After: {closedLoop.Count}");
                        }
                    }
                }
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
    /// Force close the current loop using reflection (legacy method - use ForceCloseCurrentLoopSafely instead)
    /// </summary>
    void ForceCloseCurrentLoop()
    {
        ForceCloseCurrentLoopSafely();
    }
    
    /// <summary>
    /// Find the first dot in the current loop being drawn
    /// </summary>
    GameObject FindFirstDotInCurrentLoop()
    {
        if (dotPlacementHandler?.dotsParent == null) 
            return null;
        
        // The first dot in the current loop should be the one with name starting with "TempLoop_Dot1"
        // Based on the DotPlacementHandler code, dots are named "TempLoop_Dot{number}"
        for (int i = 0; i < dotPlacementHandler.dotsParent.childCount; i++)
        {
            Transform child = dotPlacementHandler.dotsParent.GetChild(i);
            if (child != null && child.name.Contains("TempLoop_Dot1"))
            {
                return child.gameObject;
            }
        }
        
        // Alternative: find the first dot by getting current loop positions
        try
        {
            var allLoops = dotPlacementHandler.GetAllLoopPositions();
            if (allLoops.Count > 0)
            {
                var currentLoop = allLoops[allLoops.Count - 1]; // Last loop is current
                if (currentLoop.Count > 0)
                {
                    Vector3 firstPosition = currentLoop[0];
                    return FindDotAtPosition(firstPosition);
                }
            }
        }
        catch (System.Exception e)
        {
            if (debugMode)
                Debug.LogError($"Error finding first dot: {e.Message}");
        }
        
        return null;
    }
    
    /// <summary>
    /// Find a dot GameObject at or near a specific position
    /// </summary>
    GameObject FindDotAtPosition(Vector3 position)
    {
        if (dotPlacementHandler?.dotsParent == null) return null;
        
        float threshold = 0.05f; // Use the same threshold as dotClickThreshold in DotPlacementHandler
        
        for (int i = 0; i < dotPlacementHandler.dotsParent.childCount; i++)
        {
            Transform child = dotPlacementHandler.dotsParent.GetChild(i);
            if (child != null && Vector3.Distance(child.position, position) < threshold)
            {
                return child.gameObject;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Debug method to check if your loop data is being saved correctly
    /// Call this BEFORE saving to see what should be saved
    /// </summary>
    public void DebugCurrentLoopBeforeSaving()
    {
        if (dotPlacementHandler == null) return;
        
        Debug.Log("=== DEBUGGING CURRENT LOOPS BEFORE SAVING ===");
        var allLoops = dotPlacementHandler.GetAllLoopPositions();
        int completedLoops = dotPlacementHandler.GetCompletedLoopCount();
        
        for (int i = 0; i < allLoops.Count; i++)
        {
            var loop = allLoops[i];
            bool isCompleted = i < completedLoops;
            
            Debug.Log($"Loop {i + 1} - Completed: {isCompleted}, Dots: {loop.Count}");
            
            for (int j = 0; j < loop.Count; j++)
            {
                Debug.Log($"  Dot {j + 1}: {loop[j]}");
            }
            
            // Check if first and last positions are the same (which would be wrong)
            if (loop.Count > 1)
            {
                float distance = Vector3.Distance(loop[0], loop[loop.Count - 1]);
                if (distance < 0.01f)
                {
                    Debug.LogWarning($"  WARNING: First and last dots are at same position! Distance: {distance}");
                    Debug.LogWarning($"  This suggests the loop closure created a duplicate dot");
                }
            }
        }
    }
    
    /// <summary>
    /// Alternative method: Check if the issue is in the saved data format
    /// Call this method to debug what's actually being saved vs restored
    /// </summary>
    public void DebugSavedVsActualDots(LoopSystemData savedData)
    {
        if (debugMode && savedData?.loops != null)
        {
            Debug.Log("=== DEBUGGING SAVED VS ACTUAL DOTS ===");
            
            for (int i = 0; i < savedData.loops.Count; i++)
            {
                var savedLoop = savedData.loops[i];
                Debug.Log($"Saved Loop {i + 1}:");
                Debug.Log($"  dotCount: {savedLoop.dotCount}");
                Debug.Log($"  positions.Length: {savedLoop.positions?.Length ?? 0}");
                Debug.Log($"  isCompleted: {savedLoop.isCompleted}");
                
                if (savedLoop.positions != null)
                {
                    for (int j = 0; j < savedLoop.positions.Length; j++)
                    {
                        Debug.Log($"    Position {j}: {savedLoop.positions[j]}");
                    }
                }
            }
            
            // Now check what the current system has
            if (dotPlacementHandler != null)
            {
                var currentLoops = dotPlacementHandler.GetAllLoopPositions();
                Debug.Log($"Current system has {currentLoops.Count} loops");
                
                for (int i = 0; i < currentLoops.Count; i++)
                {
                    var currentLoop = currentLoops[i];
                    Debug.Log($"Current Loop {i + 1}: {currentLoop.Count} dots");
                    
                    for (int j = 0; j < currentLoop.Count; j++)
                    {
                        Debug.Log($"    Position {j}: {currentLoop[j]}");
                    }
                }
            }
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