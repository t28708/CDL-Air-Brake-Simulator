using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CDL.AirBrakeSimulator
{
    /// <summary>
    /// Interactive Component Highlighter for CDL Guided Training Mode.
    /// Provides dynamic pedagogical visual cues:
    /// - Pulsing glow / halo outline around target controls (animating alpha 0.3f to 1.0f).
    /// - Subtle breathing scale animation (1.0f to 1.07f).
    /// - Animated pulsing finger pointer / arrow pointing directly to the target control.
    /// - Tactile shake animation for wrong actions in Training Mode (soft error feedback).
    /// - Bright pulse hint to re-orient the student driver.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIElementHighlighter : MonoBehaviour
    {
        [Header("Target Identification")]
        [SerializeField] private HighlightTarget targetType = HighlightTarget.None;

        [Header("Visual Elements")]
        [SerializeField] private GameObject glowOutlineObject;
        [SerializeField] private Image glowOutlineImage;
        [SerializeField] private GameObject pointerArrowObject;
        [SerializeField] private RectTransform pointerArrowRect;
        [SerializeField] private Text pointerArrowText;

        [Header("Animation Settings")]
        [SerializeField] private Color glowColor = new Color(0.98f, 0.80f, 0.08f, 1f); // Bright Amber (#FACC15)
        [SerializeField] private Color boostColor = new Color(0.0f, 0.90f, 1.0f, 1f);  // Bright Cyan (#00E5FF)
        [SerializeField] private float pulseSpeed = 15.70796f; // 2.5 Hz (2.5 * 2 * PI rad/s)
        [SerializeField] private float minAlpha = 0.35f;
        [SerializeField] private float maxAlpha = 1.0f;
        [SerializeField] private float minScale = 1.0f;
        [SerializeField] private float maxScale = 1.03f;

        private RectTransform rectTransform;
        private Vector3 initialScale = Vector3.one;
        private Vector2 initialPointerPos;
        private bool isHighlighted = false;
        private Coroutine shakeCoroutine = null;
        private float boostIntensity = 0f;

        public HighlightTarget TargetType => targetType;
        public bool IsHighlighted => isHighlighted;

        private void Awake()
        {
            EnsureVisualComponents();
        }

        private void Start()
        {
            SetHighlighted(false);
        }

        public void Setup(HighlightTarget type, Color? customGlowColor = null)
        {
            targetType = type;
            if (customGlowColor.HasValue) glowColor = customGlowColor.Value;
            EnsureVisualComponents();
        }

        private void EnsureVisualComponents()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
                if (rectTransform != null) initialScale = rectTransform.localScale;
            }

            if (glowOutlineObject == null)
            {
                // Create procedural glow halo under/around the control
                Transform existingGlow = transform.Find("Highlighter_Glow");
                if (existingGlow != null)
                {
                    glowOutlineObject = existingGlow.gameObject;
                    glowOutlineImage = glowOutlineObject.GetComponent<Image>();
                }
                else
                {
                    glowOutlineObject = new GameObject("Highlighter_Glow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    glowOutlineObject.transform.SetParent(transform, false);
                    glowOutlineObject.transform.SetAsFirstSibling();

                    RectTransform glowRect = glowOutlineObject.GetComponent<RectTransform>();
                    glowRect.anchorMin = Vector2.zero;
                    glowRect.anchorMax = Vector2.one;
                    glowRect.offsetMin = new Vector2(-12f, -12f);
                    glowRect.offsetMax = new Vector2(12f, 12f);

                    glowOutlineImage = glowOutlineObject.GetComponent<Image>();
                    glowOutlineImage.color = glowColor;
                    glowOutlineImage.raycastTarget = false;

                    CanvasGroup glowCg = glowOutlineObject.GetComponent<CanvasGroup>();
                    if (glowCg == null) glowCg = glowOutlineObject.AddComponent<CanvasGroup>();
                    glowCg.blocksRaycasts = false;
                    glowCg.interactable = false;

                    Outline outline = glowOutlineObject.AddComponent<Outline>();
                    outline.effectColor = glowColor;
                    outline.effectDistance = new Vector2(4f, -4f);
                }
            }

            if (pointerArrowObject == null)
            {
                // Create animated pointer badge (pointing down or inward)
                Transform existingPointer = transform.Find("Highlighter_Pointer");
                if (existingPointer != null)
                {
                    pointerArrowObject = existingPointer.gameObject;
                    pointerArrowRect = pointerArrowObject.GetComponent<RectTransform>();
                    pointerArrowText = pointerArrowObject.GetComponentInChildren<Text>();
                }
                else
                {
                    pointerArrowObject = new GameObject("Highlighter_Pointer", typeof(RectTransform), typeof(CanvasRenderer));
                    pointerArrowObject.transform.SetParent(transform, false);
                    pointerArrowObject.transform.SetAsLastSibling();

                    pointerArrowRect = pointerArrowObject.GetComponent<RectTransform>();
                    pointerArrowRect.anchorMin = new Vector2(0.5f, 1f);
                    pointerArrowRect.anchorMax = new Vector2(0.5f, 1f);
                    pointerArrowRect.pivot = new Vector2(0.5f, 0f);
                    pointerArrowRect.anchoredPosition = new Vector2(0f, 6f);
                    pointerArrowRect.sizeDelta = new Vector2(130f, 26f);

                    GameObject badgeBg = new GameObject("BadgeBg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    badgeBg.transform.SetParent(pointerArrowObject.transform, false);
                    RectTransform bgRect = badgeBg.GetComponent<RectTransform>();
                    bgRect.anchorMin = Vector2.zero;
                    bgRect.anchorMax = Vector2.one;
                    bgRect.offsetMin = Vector2.zero;
                    bgRect.offsetMax = Vector2.zero;

                    Image bgImg = badgeBg.GetComponent<Image>();
                    bgImg.color = new Color(0.06f, 0.09f, 0.14f, 0.95f);
                    bgImg.raycastTarget = true;

                    if (pointerArrowObject.GetComponent<HighlightBadgeClickForwarder>() == null)
                    {
                        pointerArrowObject.AddComponent<HighlightBadgeClickForwarder>();
                    }

                    Outline badgeOutline = badgeBg.AddComponent<Outline>();
                    badgeOutline.effectColor = new Color(0.22f, 0.74f, 0.97f, 0.9f);
                    badgeOutline.effectDistance = new Vector2(1.5f, -1.5f);

                    GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                    txtGO.transform.SetParent(pointerArrowObject.transform, false);
                    RectTransform txtRect = txtGO.GetComponent<RectTransform>();
                    txtRect.anchorMin = Vector2.zero;
                    txtRect.anchorMax = Vector2.one;
                    txtRect.offsetMin = Vector2.zero;
                    txtRect.offsetMax = Vector2.zero;

                    pointerArrowText = txtGO.GetComponent<Text>();
                    pointerArrowText.text = "👉 TAP HERE";
                    pointerArrowText.fontSize = 13;
                    pointerArrowText.fontStyle = FontStyle.Bold;
                    pointerArrowText.color = new Color(0.98f, 0.85f, 0.2f);
                    pointerArrowText.alignment = TextAnchor.MiddleCenter;
                    pointerArrowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                    pointerArrowText.raycastTarget = false;

                    pointerArrowObject.SetActive(false);
                }
            }

            if (pointerArrowRect != null)
            {
                initialPointerPos = pointerArrowRect.anchoredPosition;
            }
        }

        private void Update()
        {
            if (!isHighlighted) return;

            float time = Time.unscaledTime * pulseSpeed;
            float t = (Mathf.Sin(time) + 1f) * 0.5f; // 0 to 1 smooth sine

            // Breathing scale animation (1.0f to 1.07f)
            float currentScale = Mathf.Lerp(minScale, maxScale, t);
            if (boostIntensity > 0f) currentScale += boostIntensity * 0.05f;
            rectTransform.localScale = initialScale * currentScale;

            // Soft pulsing glow/outline alpha (0.3f to 1.0f)
            if (glowOutlineImage != null)
            {
                float currentAlpha = Mathf.Lerp(minAlpha, maxAlpha, t);
                Color activeColor = Color.Lerp(glowColor, boostColor, boostIntensity);
                activeColor.a = Mathf.Clamp01(currentAlpha + boostIntensity * 0.4f);
                glowOutlineImage.color = activeColor;
            }

            // Bobbing pointer arrow
            if (pointerArrowRect != null)
            {
                float bobOffset = Mathf.Sin(time * 1.5f) * 6f;
                pointerArrowRect.anchoredPosition = initialPointerPos + new Vector2(0f, bobOffset);
            }

            // Decay boost intensity
            if (boostIntensity > 0f)
            {
                boostIntensity = Mathf.Max(0f, boostIntensity - Time.unscaledDeltaTime * 2.5f);
            }
        }

        /// <summary>
        /// Activates or deactivates the dynamic training highlight.
        /// </summary>
        public void SetHighlighted(bool highlight)
        {
            isHighlighted = highlight;

            EnsureVisualComponents();

            if (glowOutlineObject != null) glowOutlineObject.SetActive(highlight);
            if (pointerArrowObject != null) pointerArrowObject.SetActive(highlight);

            if (!highlight)
            {
                if (rectTransform != null) rectTransform.localScale = initialScale;
                if (pointerArrowRect != null) pointerArrowRect.anchoredPosition = initialPointerPos;
            }
        }

        /// <summary>
        /// Soft error feedback: gently shakes the button when tapped incorrectly in Training Mode.
        /// Does NOT fail the test.
        /// </summary>
        public void TriggerWrongActionShake()
        {
            if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
            shakeCoroutine = StartCoroutine(ShakeRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            Vector2 origin = rectTransform.anchoredPosition;
            float elapsed = 0f;
            float duration = 0.35f;
            float magnitude = 12f;

            // Flash red briefly to signal invalid action
            Color originalGlow = glowColor;
            if (glowOutlineImage != null)
            {
                if (glowOutlineObject != null) glowOutlineObject.SetActive(true);
                glowOutlineImage.color = new Color(0.95f, 0.25f, 0.25f, 0.9f);
            }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = elapsed / duration;
                float damping = 1f - progress;
                float xOffset = Mathf.Sin(progress * Mathf.PI * 8f) * magnitude * damping;
                rectTransform.anchoredPosition = origin + new Vector2(xOffset, 0f);
                yield return null;
            }

            rectTransform.anchoredPosition = origin;

            if (glowOutlineImage != null)
            {
                glowOutlineImage.color = originalGlow;
                if (!isHighlighted && glowOutlineObject != null) glowOutlineObject.SetActive(false);
            }

            shakeCoroutine = null;
        }

        /// <summary>
        /// Pulses the correct target brighter to draw student attention after an error.
        /// </summary>
        public void PulseBrightHint()
        {
            boostIntensity = 1.0f;
            if (!isHighlighted)
            {
                SetHighlighted(true);
            }
        }

        private void OnDisable()
        {
            if (rectTransform != null) rectTransform.localScale = initialScale;
        }
    }

    /// <summary>
    /// Helper component attached to floating highlight badges (e.g. "👉 TAP HERE").
    /// Forwards clicks/taps directly to the parent Button so tapping the badge activates the target control.
    /// </summary>
    public class HighlightBadgeClickForwarder : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
    {
        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            Button btn = GetComponentInParent<Button>();
            if (btn != null && btn.interactable && btn.gameObject.activeInHierarchy)
            {
                btn.onClick?.Invoke();
            }
        }
    }
}
