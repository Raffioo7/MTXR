using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;

namespace MixedReality.Toolkit.UX
{
    public class ButtonHoverText : MonoBehaviour
    {
        [Header("Hover Text Settings")]
        [SerializeField] private string hoverMessage = "Button hover text";
        [SerializeField] private GameObject hoverTextPrefab;
        [SerializeField] private Vector3 hoverTextOffset = new Vector3(0, 0.1f, 0);
        
        [Header("Interaction Settings")]
        [SerializeField] private bool enableHandRayHover = true;
        [SerializeField] private bool filterHandRayOnly = true; // New option to filter interactions
        
        private GameObject hoverTextInstance;
        private TextMeshPro hoverTextComponent;
        private Vector3 lastOffset;
        private StatefulInteractable interactable;
        
        private StatefulInteractable Interactable
        {
            get
            {
                if (interactable == null)
                {
                    interactable = GetComponent<StatefulInteractable>();
                }
                return interactable;
            }
        }
        
        void Start()
        {
            if (hoverTextPrefab != null)
            {
                hoverTextInstance = Instantiate(hoverTextPrefab, transform);
                hoverTextInstance.transform.localPosition = hoverTextOffset;
                hoverTextComponent = hoverTextInstance.GetComponent<TextMeshPro>();
                lastOffset = hoverTextOffset;
                hoverTextInstance.SetActive(false);
            }
        }
        
        void Update()
        {
            if (hoverTextInstance != null && lastOffset != hoverTextOffset)
            {
                hoverTextInstance.transform.localPosition = hoverTextOffset;
                lastOffset = hoverTextOffset;
            }
        }
        
        protected void OnEnable()
        {
            if (enableHandRayHover && Interactable != null)
            {
                Interactable.hoverEntered.AddListener(OnHoverEntered);
                Interactable.hoverExited.AddListener(OnHoverExited);
            }
        }
        
        protected void OnDisable()
        {
            if (enableHandRayHover && interactable != null)
            {
                Interactable.hoverEntered.RemoveListener(OnHoverEntered);
                Interactable.hoverExited.RemoveListener(OnHoverExited);
            }
        }
        
        private void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (enableHandRayHover)
            {
                // Filter for hand rays only if enabled
                if (!filterHandRayOnly || IsHandRayInteractor(args.interactorObject))
                {
                    ShowHoverText();
                }
            }
        }
        
        private void OnHoverExited(HoverExitEventArgs args)
        {
            if (enableHandRayHover)
            {
                if (!filterHandRayOnly || IsHandRayInteractor(args.interactorObject))
                {
                    HideHoverText();
                }
            }
        }
        
        private bool IsHandRayInteractor(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor interactor)
        {
            string interactorName = interactor.GetType().Name.ToLower();
            string transformName = interactor.transform.name.ToLower();
            
            // Look for hand-related keywords
            bool isHandInteractor = interactorName.Contains("hand") || 
                                   interactorName.Contains("ray") ||
                                   transformName.Contains("hand") ||
                                   transformName.Contains("ray");
            
            // Exclude eye/gaze tracking interactors
            bool isEyeInteractor = interactorName.Contains("eye") || 
                                  interactorName.Contains("gaze") ||
                                  transformName.Contains("eye") ||
                                  transformName.Contains("gaze");
            
            return isHandInteractor && !isEyeInteractor;
        }
        
        private void ShowHoverText()
        {
            if (hoverTextComponent != null)
            {
                hoverTextComponent.text = hoverMessage;
                hoverTextInstance.SetActive(true);
            }
        }
        
        private void HideHoverText()
        {
            if (hoverTextInstance != null)
                hoverTextInstance.SetActive(false);
        }
        
        public void SetHandRayHover(bool enabled)
        {
            if (enableHandRayHover != enabled)
            {
                enableHandRayHover = enabled;
                
                if (enabled)
                {
                    if (Interactable != null)
                    {
                        Interactable.hoverEntered.AddListener(OnHoverEntered);
                        Interactable.hoverExited.AddListener(OnHoverExited);
                    }
                }
                else
                {
                    if (interactable != null)
                    {
                        Interactable.hoverEntered.RemoveListener(OnHoverEntered);
                        Interactable.hoverExited.RemoveListener(OnHoverExited);
                    }
                    HideHoverText();
                }
            }
        }
        
        void OnDestroy()
        {
            if (hoverTextInstance != null)
                Destroy(hoverTextInstance);
        }
    }
}