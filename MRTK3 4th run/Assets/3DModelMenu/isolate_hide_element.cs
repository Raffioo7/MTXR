using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

public class MeshVisibilityHandler_MRTK3 : MonoBehaviour
{
    [Header("MRTK3 Buttons")]
    public GameObject isolateButton;
    public GameObject hideButton;
    public GameObject showAllButton;

    private GameObject selectedObject;
    private List<GameObject> hiddenObjects = new List<GameObject>();
    private List<GameObject> allRevitObjects = new List<GameObject>();

    private Component[] handRayComponents;
    private float lastHandRayUpdateTime;
    private const float handRayUpdateInterval = 0.1f;

    void Start()
    {
        allRevitObjects = FindObjectsOfType<RevitData>().Select(r => r.gameObject).ToList();
        UpdateHandRayComponentsCache();
        SetupButton(isolateButton, IsolateSelectedObject);
        SetupButton(hideButton, HideSelectedObject);
        SetupButton(showAllButton, ShowAllObjects);

        SubscribeToObjectClicks();
    }

    void UpdateHandRayComponentsCache()
    {
        var allComponents = FindObjectsOfType<MonoBehaviour>();
        handRayComponents = allComponents.Where(mb =>
            mb.GetType().Name.Contains("HandRay") ||
            mb.GetType().Name.Contains("RayInteractor")).ToArray();
    }

    void SetupButton(GameObject button, UnityAction action)
    {
        if (button == null) return;
        var interactable = button.GetComponent("StatefulInteractable");
        if (interactable == null) return;

        var type = interactable.GetType();
        var evt = type.GetField("OnClicked")?.GetValue(interactable) as UnityEvent
                  ?? type.GetProperty("OnClicked")?.GetValue(interactable) as UnityEvent;

        evt?.AddListener(action);
    }

    void SubscribeToObjectClicks()
    {
        foreach (GameObject obj in allRevitObjects)
        {
            var interactable = obj.GetComponent("StatefulInteractable");
            if (interactable != null)
                TrySubscribeToClick(interactable, obj);
        }
    }

    void TrySubscribeToClick(Component interactable, GameObject targetObj)
    {
        var type = interactable.GetType();
        var evt = type.GetField("OnClicked")?.GetValue(interactable) as UnityEvent
                  ?? type.GetProperty("OnClicked")?.GetValue(interactable) as UnityEvent;

        if (evt != null)
            evt.AddListener(() => OnMeshClicked(targetObj));
    }

    void OnMeshClicked(GameObject clickedObj)
    {
        selectedObject = clickedObj;
        Debug.Log("Selected object: " + clickedObj.name);
    }

    void IsolateSelectedObject()
    {
        if (selectedObject == null) return;

        foreach (var obj in allRevitObjects)
        {
            if (obj != selectedObject)
            {
                obj.SetActive(false);
                if (!hiddenObjects.Contains(obj)) hiddenObjects.Add(obj);
            }
        }

        selectedObject.SetActive(true);
        Debug.Log("Isolated: " + selectedObject.name);
    }

    void HideSelectedObject()
    {
        if (selectedObject == null) return;
        selectedObject.SetActive(false);
        if (!hiddenObjects.Contains(selectedObject))
            hiddenObjects.Add(selectedObject);

        Debug.Log("Hid: " + selectedObject.name);
    }

    void ShowAllObjects()
    {
        foreach (var obj in allRevitObjects)
        {
            obj.SetActive(true);
        }
        hiddenObjects.Clear();
        selectedObject = null;
        Debug.Log("Restored all objects");
    }
}
