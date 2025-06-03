using UnityEngine;
using MixedReality.Toolkit.UX;

public class PanelToggleMRTKV2 : MonoBehaviour
{
    [SerializeField] private PressableButton toggleButton;
    [SerializeField] private PressableButton closeButton;
    [SerializeField] private GameObject panel;
    [SerializeField] private PressableButton additionalButton1;
    [SerializeField] private PressableButton additionalButton2;
    [SerializeField] private PressableButton additionalButton3;
    
    void Start()
    {
        // Subscribe to MRTK button events
        toggleButton.OnClicked.AddListener(TogglePanel);
        closeButton.OnClicked.AddListener(HidePanel);
        
        // Start with panel and additional buttons hidden
        HidePanel();
    }
    
    void TogglePanel()
    {
        if (panel.activeSelf)
        {
            HidePanel();
        }
        else
        {
            ShowPanel();
        }
    }
    
    void ShowPanel()
    {
        panel.SetActive(true);
        additionalButton1.gameObject.SetActive(true);
        additionalButton2.gameObject.SetActive(true);
        additionalButton3.gameObject.SetActive(true);
    }
    
    void HidePanel()
    {
        panel.SetActive(false);
        additionalButton1.gameObject.SetActive(false);
        additionalButton2.gameObject.SetActive(false);
        additionalButton3.gameObject.SetActive(false);
    }
    
    void OnDestroy()
    {
        // Clean up listeners
        if (toggleButton != null)
            toggleButton.OnClicked.RemoveListener(TogglePanel);
        if (closeButton != null)
            closeButton.OnClicked.RemoveListener(HidePanel);
    }
}