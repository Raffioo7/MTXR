using UnityEngine;
using System.Reflection;
using System;

public class MeasurementDotManagementExtension_MRTK3 : MonoBehaviour
{
    [Header("MRTK3 Buttons")]
    [Tooltip("Button to clear all dots")]
    public GameObject clearAllDotsButton;
    
    [Tooltip("Button to remove the last placed dot")]
    public GameObject removeLastDotButton;
    
    [Tooltip("Button to export dot positions")]
    public GameObject exportPositionsButton;
    
    [Tooltip("Button to toggle dot visibility")]
    public GameObject toggleVisibilityButton;
    
    [Header("Line Management Buttons")]
    [Tooltip("Button to toggle line visibility")]
    public GameObject toggleLinesButton;
    
    [Header("Loop Management Buttons")]
    [Tooltip("Button to force close current loop")]
    public GameObject forceCloseLoopButton;
    
    [Tooltip("Button to clear only the last completed loop")]
    public GameObject clearLastLoopButton;
    
    [Header("Area Shading Buttons")]
    [Tooltip("Button to toggle shaded area visibility")]
    public GameObject toggleShadingButton;
    
    [Header("Measurement Buttons")]
    [Tooltip("Button to toggle measurement labels visibility")]
    public GameObject toggleMeasurementsButton;
    
    [Tooltip("Button to toggle between metric and imperial units")]
    public GameObject toggleUnitsButton;
    
    [Tooltip("Button to print measurement summary to console")]
    public GameObject printSummaryButton;
    
    [Header("Debug")]
    public bool debugMode = true;
    
    // Private fields
    private PropertyClickHandler_MRTK3 propertyHandler;
    private MeasurementDotPlacementHandler_MRTK3 dotHandler;
    private MeasurementLineConnectionHandler_MRTK3 lineHandler;
    private MeasurementLoopAreaShader_MRTK3 areaShader;
    private MeasurementStandaloneHandler_MRTK3 measurementHandler;
    private bool dotsVisible = true;
    private bool linesVisible = true;
    private bool autoDrawEnabled = true;
    private bool shadingVisible = true;
    private bool autoShadingEnabled = true;
    private bool measurementsVisible = true;
    
    void Start()
    {
        // Find required components
        propertyHandler = FindObjectOfType<PropertyClickHandler_MRTK3>();
        dotHandler = FindObjectOfType<MeasurementDotPlacementHandler_MRTK3>();
        lineHandler = FindObjectOfType<MeasurementLineConnectionHandler_MRTK3>();
        areaShader = FindObjectOfType<MeasurementLoopAreaShader_MRTK3>();
        measurementHandler = FindObjectOfType<MeasurementStandaloneHandler_MRTK3>();
        
        if (dotHandler == null)
        {
            Debug.LogError("MeasurementDotManagementExtension: MeasurementDotPlacementHandler_MRTK3 not found!");
            return;
        }
        
        if (lineHandler == null)
        {
            Debug.LogWarning("MeasurementDotManagementExtension: MeasurementLineConnectionHandler_MRTK3 not found! Line features will be disabled.");
        }
        
        if (areaShader == null)
        {
            Debug.LogWarning("MeasurementDotManagementExtension: MeasurementLoopAreaShader_MRTK3 not found! Area shading features will be disabled.");
        }
        
        if (measurementHandler == null)
        {
            Debug.LogWarning("MeasurementDotManagementExtension: MeasurementStandaloneHandler_MRTK3 not found! Measurement features will be disabled.");
        }
        
        // Subscribe to loop events for feedback
        if (dotHandler != null)
        {
            dotHandler.OnMeasurementLoopClosed += OnLoopClosed;
            dotHandler.OnMeasurementNewLoopStarted += OnNewLoopStarted;
        }
        
        // Set up all buttons
        SetupButtons();
    }
    
    void OnLoopClosed(int loopIndex)
    {
        if (debugMode)
            Debug.Log($"MeasurementDotManagementExtension: Loop {loopIndex + 1} was closed");
    }
    
    void OnNewLoopStarted(int loopIndex)
    {
        if (debugMode)
            Debug.Log($"MeasurementDotManagementExtension: New loop {loopIndex + 1} was started");
    }
    
    void SetupButtons()
    {
        // Set up original buttons
        if (clearAllDotsButton != null)
        {
            SetupButton(clearAllDotsButton, ClearAllDots, "Clear All Dots");
        }
        else if (debugMode)
        {
            Debug.LogWarning("MeasurementDotManagementExtension: Clear All Dots button not assigned");
        }
        
        if (removeLastDotButton != null)
        {
            SetupButton(removeLastDotButton, RemoveLastDot, "Remove Last Dot");
        }
        else if (debugMode)
        {
            Debug.LogWarning("MeasurementDotManagementExtension: Remove Last Dot button not assigned");
        }
        
        if (exportPositionsButton != null)
        {
            SetupButton(exportPositionsButton, ExportDotPositions, "Export Positions");
        }
        else if (debugMode)
        {
            Debug.LogWarning("MeasurementDotManagementExtension: Export Positions button not assigned");
        }
        
        if (toggleVisibilityButton != null)
        {
            SetupButton(toggleVisibilityButton, ToggleDotVisibility, "Toggle Visibility");
        }
        else if (debugMode)
        {
            Debug.LogWarning("MeasurementDotManagementExtension: Toggle Visibility button not assigned");
        }
        
        // Set up line management buttons
        if (toggleLinesButton != null)
        {
            SetupButton(toggleLinesButton, ToggleLineVisibility, "Toggle Lines");
        }
        else if (debugMode)
        {
            Debug.LogWarning("MeasurementDotManagementExtension: Toggle Lines button not assigned");
        }
        
        // Set up loop management buttons
        if (forceCloseLoopButton != null)
        {
            SetupButton(forceCloseLoopButton, ForceCloseCurrentLoop, "Force Close Loop");
        }
        else if (debugMode)
        {
            Debug.LogWarning("MeasurementDotManagementExtension: Force Close Loop button not assigned");
        }
        
        if (clearLastLoopButton != null)
        {
            SetupButton(clearLastLoopButton, ClearLastLoop, "Clear Last Loop");
        }
        else if (debugMode)
        {
            Debug.LogWarning("MeasurementDotManagementExtension: Clear Last Loop button not assigned");
        }
        
        // Set up area shading buttons
        if (toggleShadingButton != null)
        {
            SetupButton(toggleShadingButton, ToggleShadingVisibility, "Toggle Shading");
        }
        else if (debugMode)
        {
            Debug.LogWarning("MeasurementDotManagementExtension: Toggle Shading button not assigned");
        }
        
        // Set up measurement buttons
        if (toggleMeasurementsButton != null)
        {
            SetupButton(toggleMeasurementsButton, ToggleMeasurementVisibility, "Toggle Measurements");
        }
        else if (debugMode)
        {
            Debug.LogWarning("MeasurementDotManagementExtension: Toggle Measurements button not assigned");
        }
        
        if (toggleUnitsButton != null)
        {
            SetupButton(toggleUnitsButton, ToggleUnits, "Toggle Units");
        }
        else if (debugMode)
        {
            Debug.LogWarning("MeasurementDotManagementExtension: Toggle Units button not assigned");
        }
        
        if (printSummaryButton != null)
        {
            SetupButton(printSummaryButton, PrintMeasurementSummary, "Print Summary");
        }
        else if (debugMode)
        {
            Debug.LogWarning("MeasurementDotManagementExtension: Print Summary button not assigned");
        }
    }
    
    void SetupButton(GameObject buttonObj, UnityEngine.Events.UnityAction action, string buttonName)
    {
        Component buttonInteractable = buttonObj.GetComponent("StatefulInteractable");
        if (buttonInteractable != null)
        {
            bool subscribed = TrySubscribeToButtonClick(buttonInteractable, action);
            
            if (debugMode)
            {
                if (subscribed)
                    Debug.Log($"MeasurementDotManagementExtension: Successfully set up {buttonName} button");
                else
                    Debug.LogWarning($"MeasurementDotManagementExtension: Failed to set up {buttonName} button");
            }
        }
        else if (debugMode)
        {
            Debug.LogWarning($"MeasurementDotManagementExtension: No StatefulInteractable found on {buttonName} button");
        }
    }
    
    bool TrySubscribeToButtonClick(Component interactable, UnityEngine.Events.UnityAction action)
    {
        Type interactableType = interactable.GetType();
        string[] possibleEventNames = { "OnClicked", "onClicked", "Clicked", "clicked" };
        
        foreach (string eventName in possibleEventNames)
        {
            FieldInfo fieldInfo = interactableType.GetField(eventName);
            if (fieldInfo != null)
            {
                var eventValue = fieldInfo.GetValue(interactable) as UnityEngine.Events.UnityEvent;
                if (eventValue != null)
                {
                    eventValue.AddListener(action);
                    return true;
                }
            }
            
            PropertyInfo propertyInfo = interactableType.GetProperty(eventName);
            if (propertyInfo != null)
            {
                var eventValue = propertyInfo.GetValue(interactable) as UnityEngine.Events.UnityEvent;
                if (eventValue != null)
                {
                    eventValue.AddListener(action);
                    return true;
                }
            }
        }
        
        return false;
    }
    
    public void ClearAllDots()
    {
        if (dotHandler != null)
        {
            dotHandler.ClearAllDots();
            
            // Clear lines too
            if (lineHandler != null)
            {
                lineHandler.ClearAllLines();
            }
            
            // Clear shaded areas too
            if (areaShader != null)
            {
                areaShader.ClearAllShadedAreas();
            }
            
            // Clear measurements too
            if (measurementHandler != null)
            {
                measurementHandler.ClearAllMeasurements();
            }
            
            // Make sure dots are visible after clearing
            if (!dotsVisible && dotHandler.dotsParent != null)
            {
                dotsVisible = true;
                dotHandler.dotsParent.gameObject.SetActive(true);
            }
            
            if (debugMode)
                Debug.Log("MeasurementDotManagementExtension: Cleared all dots, lines, shaded areas, and measurements");
        }
    }
    
    public void RemoveLastDot()
    {
        if (dotHandler != null)
        {
            dotHandler.RemoveLastDot();
            
            // Update lines after removing dot
            if (lineHandler != null)
            {
                lineHandler.UpdateConnectionLines();
            }
            
            // Update shaded areas after removing dot
            if (areaShader != null)
            {
                areaShader.ForceUpdateAllAreas();
            }
            
            // Note: MeasurementHandler updates automatically via events, no manual update needed
            
            if (debugMode)
                Debug.Log("MeasurementDotManagementExtension: Removed last dot and updated lines, shading, and measurements");
        }
    }
    
    public void ExportDotPositions()
    {
        if (dotHandler != null)
        {
            var allLoops = dotHandler.GetAllLoopPositions();
            int completedLoops = dotHandler.GetCompletedLoopCount();
            int currentLoopDots = dotHandler.GetCurrentLoopDotCount();
            
            if (allLoops.Count == 0)
            {
                Debug.LogWarning("MeasurementDotManagementExtension: No dots to export!");
                return;
            }
            
            string export = "Measurement Dot Positions and Loop Data Export:\n";
            export += "=============================================\n";
            export += $"Completed Loops: {completedLoops}\n";
            export += $"Current Loop Dots: {currentLoopDots}\n";
            export += $"Total Loops: {allLoops.Count}\n";
            
            // Add line information if available
            if (lineHandler != null)
            {
                int lineCount = lineHandler.GetLineCount();
                export += $"Total Lines: {lineCount}\n";
            }
            
            // Add shading information if available
            if (areaShader != null)
            {
                int shadedAreaCount = areaShader.GetShadedAreaCount();
                export += $"Shaded Areas: {shadedAreaCount}\n";
            }
            
            // Add measurement information if available
            if (measurementHandler != null)
            {
                export += "\nMEASUREMENTS:\n";
                for (int i = 0; i < completedLoops; i++)
                {
                    float distance = measurementHandler.GetLoopTotalDistance(i);
                    float area = measurementHandler.GetLoopArea(i);
                    export += $"Loop {i + 1}: Perimeter = {distance:F2}m, Area = {area:F2}m²\n";
                }
                
                if (currentLoopDots > 0)
                {
                    float currentDistance = measurementHandler.GetCurrentLoopTotalDistance();
                    float currentArea = measurementHandler.GetCurrentLoopArea();
                    export += $"Current Loop: Distance = {currentDistance:F2}m, Area = {currentArea:F2}m²\n";
                }
            }
            
            export += "-------------------------------------\n";
            
            // Export each loop separately
            for (int loopIndex = 0; loopIndex < allLoops.Count; loopIndex++)
            {
                var loopPositions = allLoops[loopIndex];
                bool isCompleted = loopIndex < completedLoops;
                
                export += $"\n{(isCompleted ? "COMPLETED" : "CURRENT")} MEASUREMENT LOOP {loopIndex + 1}:\n";
                export += $"Dots in loop: {loopPositions.Count}\n";
                
                if (isCompleted)
                {
                    export += "Status: CLOSED (connects back to first dot)\n";
                }
                else
                {
                    export += "Status: OPEN (work in progress)\n";
                }
                
                export += "Dot Positions:\n";
                for (int dotIndex = 0; dotIndex < loopPositions.Count; dotIndex++)
                {
                    Vector3 pos = loopPositions[dotIndex];
                    export += $"  Dot {dotIndex + 1}: X={pos.x:F3}, Y={pos.y:F3}, Z={pos.z:F3}\n";
                }
                
                // Add line connections for this loop
                if (isCompleted && loopPositions.Count > 2)
                {
                    export += "Line Connections:\n";
                    for (int i = 0; i < loopPositions.Count - 1; i++)
                    {
                        export += $"  Line {i + 1}: Dot {i + 1} -> Dot {i + 2}\n";
                    }
                    export += $"  Closing Line: Dot {loopPositions.Count} -> Dot 1 (LOOP CLOSURE)\n";
                    export += "Shaded Area: YES\n";
                }
                else if (!isCompleted && loopPositions.Count > 1)
                {
                    export += "Line Connections:\n";
                    for (int i = 0; i < loopPositions.Count - 1; i++)
                    {
                        export += $"  Line {i + 1}: Dot {i + 1} -> Dot {i + 2}\n";
                    }
                    export += "Shaded Area: NO (loop not closed)\n";
                }
            }
            
            export += "\n=============================================\n";
            export += $"Exported at: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            
            Debug.Log(export);
            
            // Copy to clipboard (Unity 2020.1+)
            GUIUtility.systemCopyBuffer = export;
            
            if (debugMode)
                Debug.Log("MeasurementDotManagementExtension: Loop data exported to console and clipboard");
        }
    }
    
    public void ToggleDotVisibility()
    {
        if (dotHandler != null && dotHandler.dotsParent != null)
        {
            dotsVisible = !dotsVisible;
            dotHandler.dotsParent.gameObject.SetActive(dotsVisible);
            
            if (debugMode)
                Debug.Log($"MeasurementDotManagementExtension: Dots are now {(dotsVisible ? "visible" : "hidden")}");
        }
    }
    
    public void ToggleLineVisibility()
    {
        if (lineHandler != null)
        {
            lineHandler.ToggleLineVisibility();
            linesVisible = !linesVisible;
            
            if (debugMode)
                Debug.Log($"MeasurementDotManagementExtension: Lines are now {(linesVisible ? "visible" : "hidden")}");
        }
    }
    
    public void ToggleShadingVisibility()
    {
        if (areaShader != null)
        {
            areaShader.ToggleShadedAreaVisibility();
            shadingVisible = !shadingVisible;
            
            if (debugMode)
                Debug.Log($"MeasurementDotManagementExtension: Shaded areas are now {(shadingVisible ? "visible" : "hidden")}");
        }
    }
    
    public void ToggleMeasurementVisibility()
    {
        if (measurementHandler != null)
        {
            measurementHandler.ToggleMeasurementLabels();
            measurementsVisible = !measurementsVisible;
            
            if (debugMode)
                Debug.Log($"MeasurementDotManagementExtension: Measurements are now {(measurementsVisible ? "visible" : "hidden")}");
        }
    }
    
    public void ToggleUnits()
    {
        if (measurementHandler != null)
        {
            measurementHandler.ToggleUnits();
            
            if (debugMode)
                Debug.Log("MeasurementDotManagementExtension: Toggled measurement units");
        }
    }
    
    public void PrintMeasurementSummary()
    {
        if (measurementHandler != null)
        {
            measurementHandler.PrintMeasurementSummary();
            
            if (debugMode)
                Debug.Log("MeasurementDotManagementExtension: Printed measurement summary to console");
        }
    }
    
    public void ForceCloseCurrentLoop()
    {
        if (dotHandler != null)
        {
            int currentLoopDots = dotHandler.GetCurrentLoopDotCount();
            
            if (currentLoopDots < 3)
            {
                if (debugMode)
                    Debug.LogWarning("MeasurementDotManagementExtension: Cannot close loop - need at least 3 dots");
                return;
            }
            
            // Force close by using reflection to call the private method
            var type = dotHandler.GetType();
            var method = type.GetMethod("CloseCurrentLoop", BindingFlags.NonPublic | BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(dotHandler, null);
                
                if (debugMode)
                    Debug.Log("MeasurementDotManagementExtension: Force closed current loop");
            }
            else if (debugMode)
            {
                Debug.LogError("MeasurementDotManagementExtension: Could not find CloseCurrentLoop method");
            }
        }
    }
    
    public void ClearLastLoop()
    {
        if (dotHandler != null)
        {
            int completedLoops = dotHandler.GetCompletedLoopCount();
            int currentLoopDots = dotHandler.GetCurrentLoopDotCount();
            
            if (currentLoopDots > 0)
            {
                // Clear current loop first
                var allLoops = dotHandler.GetAllLoopPositions();
                if (allLoops.Count > 0)
                {
                    var currentLoop = allLoops[allLoops.Count - 1];
                    // Use RemoveLastDot multiple times to clear current loop
                    for (int i = 0; i < currentLoop.Count; i++)
                    {
                        dotHandler.RemoveLastDot();
                    }
                    
                    if (debugMode)
                        Debug.Log("MeasurementDotManagementExtension: Cleared current loop");
                }
            }
            else if (completedLoops > 0)
            {
                // Clear last completed loop
                dotHandler.RemoveLastDot(); // This should remove the last completed loop
                
                if (debugMode)
                    Debug.Log("MeasurementDotManagementExtension: Cleared last completed loop");
            }
            else if (debugMode)
            {
                Debug.LogWarning("MeasurementDotManagementExtension: No loops to clear");
            }
            
            // Update lines after clearing
            if (lineHandler != null)
            {
                lineHandler.UpdateConnectionLines();
            }
            
            // Update shaded areas after clearing
            if (areaShader != null)
            {
                areaShader.ForceUpdateAllAreas();
            }
            
            // Note: MeasurementHandler updates automatically via events
        }
    }
    
    // Visual feedback for buttons
    void UpdateButtonVisuals()
    {
        UpdateToggleButtonVisual(toggleVisibilityButton, dotsVisible);
        UpdateToggleButtonVisual(toggleLinesButton, linesVisible);
        UpdateToggleButtonVisual(toggleShadingButton, shadingVisible);
        UpdateToggleButtonVisual(toggleMeasurementsButton, measurementsVisible);
        
        // Update loop-specific button visuals
        UpdateLoopButtonVisuals();
    }
    
    void UpdateLoopButtonVisuals()
    {
        if (dotHandler != null)
        {
            int currentLoopDots = dotHandler.GetCurrentLoopDotCount();
            int completedLoops = dotHandler.GetCompletedLoopCount();
            
            // Update force close button (enabled only if current loop has 3+ dots)
            UpdateActionButtonVisual(forceCloseLoopButton, currentLoopDots >= 3);
            
            // Update clear last loop button (enabled if there are loops to clear)
            UpdateActionButtonVisual(clearLastLoopButton, currentLoopDots > 0 || completedLoops > 0);
        }
    }
    
    void UpdateToggleButtonVisual(GameObject button, bool isActive)
    {
        if (button != null)
        {
            Renderer buttonRenderer = button.GetComponentInChildren<Renderer>();
            if (buttonRenderer != null && buttonRenderer.material != null)
            {
                if (buttonRenderer.material.HasProperty("_Color"))
                {
                    buttonRenderer.material.color = isActive ? Color.white : Color.gray;
                }
                else if (buttonRenderer.material.HasProperty("_BaseColor"))
                {
                    buttonRenderer.material.SetColor("_BaseColor", isActive ? Color.white : Color.gray);
                }
            }
        }
    }
    
    void UpdateActionButtonVisual(GameObject button, bool isEnabled)
    {
        if (button != null)
        {
            Renderer buttonRenderer = button.GetComponentInChildren<Renderer>();
            if (buttonRenderer != null && buttonRenderer.material != null)
            {
                if (buttonRenderer.material.HasProperty("_Color"))
                {
                    buttonRenderer.material.color = isEnabled ? Color.white : Color.red;
                }
                else if (buttonRenderer.material.HasProperty("_BaseColor"))
                {
                    buttonRenderer.material.SetColor("_BaseColor", isEnabled ? Color.white : Color.red);
                }
            }
        }
    }
    
    void Update()
    {
        // Update button visuals
        UpdateButtonVisuals();
    }
    
    void OnDestroy()
    {
        // Unsubscribe from loop events
        if (dotHandler != null)
        {
            dotHandler.OnMeasurementLoopClosed -= OnLoopClosed;
            dotHandler.OnMeasurementNewLoopStarted -= OnNewLoopStarted;
        }
        
        // Remove all button listeners
        UnsubscribeFromAllButtons();
    }
    
    void UnsubscribeFromAllButtons()
    {
        if (clearAllDotsButton != null)
            UnsubscribeFromButton(clearAllDotsButton, ClearAllDots);
            
        if (removeLastDotButton != null)
            UnsubscribeFromButton(removeLastDotButton, RemoveLastDot);
            
        if (exportPositionsButton != null)
            UnsubscribeFromButton(exportPositionsButton, ExportDotPositions);
            
        if (toggleVisibilityButton != null)
            UnsubscribeFromButton(toggleVisibilityButton, ToggleDotVisibility);
            
        if (toggleLinesButton != null)
            UnsubscribeFromButton(toggleLinesButton, ToggleLineVisibility);
            
        if (forceCloseLoopButton != null)
            UnsubscribeFromButton(forceCloseLoopButton, ForceCloseCurrentLoop);
            
        if (clearLastLoopButton != null)
            UnsubscribeFromButton(clearLastLoopButton, ClearLastLoop);
            
        if (toggleShadingButton != null)
            UnsubscribeFromButton(toggleShadingButton, ToggleShadingVisibility);
            
        if (toggleMeasurementsButton != null)
            UnsubscribeFromButton(toggleMeasurementsButton, ToggleMeasurementVisibility);
            
        if (toggleUnitsButton != null)
            UnsubscribeFromButton(toggleUnitsButton, ToggleUnits);
            
        if (printSummaryButton != null)
            UnsubscribeFromButton(printSummaryButton, PrintMeasurementSummary);
    }
    
    void UnsubscribeFromButton(GameObject buttonObj, UnityEngine.Events.UnityAction action)
    {
        Component buttonInteractable = buttonObj.GetComponent("StatefulInteractable");
        if (buttonInteractable != null)
        {
            UnsubscribeFromButtonClickEvent(buttonInteractable, action);
        }
    }
    
    void UnsubscribeFromButtonClickEvent(Component interactable, UnityEngine.Events.UnityAction action)
    {
        Type interactableType = interactable.GetType();
        string[] possibleEventNames = { "OnClicked", "onClicked", "Clicked", "clicked" };
        
        foreach (string eventName in possibleEventNames)
        {
            // Try as field
            FieldInfo fieldInfo = interactableType.GetField(eventName);
            if (fieldInfo != null)
            {
                var eventValue = fieldInfo.GetValue(interactable) as UnityEngine.Events.UnityEvent;
                if (eventValue != null)
                {
                    eventValue.RemoveListener(action);
                    return;
                }
            }
            
            // Try as property
            PropertyInfo propertyInfo = interactableType.GetProperty(eventName);
            if (propertyInfo != null)
            {
                var eventValue = propertyInfo.GetValue(interactable) as UnityEngine.Events.UnityEvent;
                if (eventValue != null)
                {
                    eventValue.RemoveListener(action);
                    return;
                }
            }
        }
    }
}