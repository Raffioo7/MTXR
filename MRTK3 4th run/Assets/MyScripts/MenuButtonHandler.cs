using UnityEngine;
using MixedReality.Toolkit.UX;

public class MenuButtonHandler : MonoBehaviour
{
    [SerializeField] private TileMenuManager tileMenuManager;
    
    void Start()
    {
        // Get the PressableButton component
        PressableButton button = GetComponent<PressableButton>();
        
        if (button != null && tileMenuManager != null)
        {
            // Add listener to toggle the tile menu (changed from OpenTileMenu to ToggleTileMenu)
            button.OnClicked.AddListener(() => tileMenuManager.ToggleTileMenu());
        }
        else
        {
            if (button == null)
                Debug.LogError("PressableButton component not found!");
            if (tileMenuManager == null)
                Debug.LogError("TileMenuManager reference not assigned!");
        }
    }
}