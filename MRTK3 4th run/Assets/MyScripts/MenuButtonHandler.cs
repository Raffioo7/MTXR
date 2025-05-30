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
            // Add listener to open the tile menu
            button.OnClicked.AddListener(() => tileMenuManager.OpenTileMenu());
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