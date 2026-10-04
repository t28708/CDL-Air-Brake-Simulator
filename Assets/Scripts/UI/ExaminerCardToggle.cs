using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CDL.AirBrakeSimulator
{
    /// <summary>
    /// Click and pointer event handler for expanding/collapsing the DMV Examiner dialogue bottom sheet.
    /// Safely synchronizes with AirBrakeUIController and includes debounce protection to prevent double-toggle bugs.
    /// </summary>
    public class ExaminerCardToggle : MonoBehaviour, IPointerClickHandler
    {
        public enum ToggleAction
        {
            HideCard,
            ShowCard,
            ToggleCard
        }

        [SerializeField] private ToggleAction action = ToggleAction.ToggleCard;
        [SerializeField] private GameObject cardSection;
        [SerializeField] private GameObject minimizedBar;
        [SerializeField] private Text toggleBtnText;

        private float lastActionTime = -1f;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        public void Setup(ToggleAction act, GameObject card, GameObject minBar, Text btnText = null)
        {
            action = act;
            cardSection = card;
            minimizedBar = minBar;
            toggleBtnText = btnText;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            ExecuteToggle();
        }

        public void ExecuteToggle()
        {
            if (Time.unscaledTime - lastActionTime < 0.25f) return;
            lastActionTime = Time.unscaledTime;

            AirBrakeUIController ui = FindFirstObjectByType<AirBrakeUIController>();
            if (ui != null)
            {
                switch (action)
                {
                    case ToggleAction.HideCard:
                        ui.SetExaminerCardVisible(false);
                        break;
                    case ToggleAction.ShowCard:
                        ui.SetExaminerCardVisible(true);
                        break;
                    case ToggleAction.ToggleCard:
                        ui.ToggleExaminerCard();
                        break;
                }
                return;
            }

            // Fallback if AirBrakeUIController is not present
            ResolveReferences();

            bool show = false;
            switch (action)
            {
                case ToggleAction.HideCard:
                    show = false;
                    break;
                case ToggleAction.ShowCard:
                    show = true;
                    break;
                case ToggleAction.ToggleCard:
                    if (cardSection != null)
                    {
                        show = !cardSection.activeSelf;
                    }
                    break;
            }

            ApplyStateDirect(show);
        }

        public void ApplyStateDirect(bool show)
        {
            ResolveReferences();

            if (cardSection != null)
            {
                cardSection.SetActive(show);
            }

            if (minimizedBar != null)
            {
                minimizedBar.SetActive(!show);
            }

            if (toggleBtnText != null)
            {
                toggleBtnText.text = show ? "📋 HIDE DMV" : "📋 SHOW DMV";
            }
            else
            {
                GameObject toggleBtnGO = GameObject.Find("BtnToggleDMV");
                if (toggleBtnGO != null)
                {
                    Text t = toggleBtnGO.GetComponentInChildren<Text>();
                    if (t != null) t.text = show ? "📋 HIDE DMV" : "📋 SHOW DMV";
                }
            }

            Debug.Log($"<color=#38bdf8><b>[CDL Air Brake] DMV Card Visibility set to: {show} (Action: {action}, cardSection: {(cardSection != null ? "ok" : "null")}, minBar: {(minimizedBar != null ? "ok" : "null")})</b></color>");
        }

        private void ResolveReferences()
        {
            if (cardSection != null && minimizedBar != null) return;

            Transform canvasRoot = null;
            if (transform != null && transform.root != null && transform.root.name == "CDL_AirBrake_Canvas")
            {
                canvasRoot = transform.root;
            }
            if (canvasRoot == null)
            {
                var canvasGO = GameObject.Find("CDL_AirBrake_Canvas");
                if (canvasGO != null) canvasRoot = canvasGO.transform;
            }
            if (canvasRoot == null)
            {
                var canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null) canvasRoot = canvas.transform;
            }

            if (canvasRoot != null)
            {
                Transform bg = canvasRoot.Find("Background");
                if (bg != null)
                {
                    if (cardSection == null)
                    {
                        Transform t = bg.Find("5_ExaminerCardSection");
                        if (t != null) cardSection = t.gameObject;
                    }
                    if (minimizedBar == null)
                    {
                        Transform t = bg.Find("ExaminerMinimizedBar");
                        if (t != null) minimizedBar = t.gameObject;
                    }
                }
            }

            if (cardSection == null) cardSection = GameObject.Find("5_ExaminerCardSection");
            if (minimizedBar == null) minimizedBar = GameObject.Find("ExaminerMinimizedBar");
        }
    }
}
