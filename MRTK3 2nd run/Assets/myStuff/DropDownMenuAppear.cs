using UnityEngine;

public class DropdownController : MonoBehaviour
{
    public GameObject dropdownPanel;
    public TextMesh toggleText;

    void Start()
    {
        dropdownPanel.SetActive(false);
    }

    public void ToggleDropdown()
    {
        dropdownPanel.SetActive(!dropdownPanel.activeSelf);
    }

    public void SelectOption(string option)
    {
        toggleText.text = option + " ▼";
        dropdownPanel.SetActive(false);
    }
}