using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CDL.AirBrakeSimulator
{
    /// <summary>
    /// Handles realistic physical interaction with the Foot Service Brake Pedal:
    /// - Press-and-Hold detection with live timer countdown & hold duration display
    /// - Dynamic button label & banner timer display (seconds held / 60s countdown)
    /// - Visual depression animation & active pressure feedback
    /// - Keyboard fallback (Space key)
    /// - Fast Fan (pump) capability (B key)
    /// </summary>
    public class InteractivePedal : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Header("System References")]
        [SerializeField] private AirBrakeSystem airSystem;
        [SerializeField] private DMVTestManager dmvManager;

        [Header("Pedal Button Visuals")]
        [SerializeField] private RectTransform pedalTransform;
        [SerializeField] private Image pedalImage;
        [SerializeField] private Text txtPedalButton;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color pressedColor = new Color(0.18f, 0.45f, 0.30f);
        [SerializeField] private Vector3 pressedScale = new Vector3(0.94f, 0.94f, 1f);

        [Header("Timer Banner UI")]
        [SerializeField] private GameObject timerBannerGO;
        [SerializeField] private Text txtTimerBanner;
        [SerializeField] private RectTransform rectProgressBarFill;
        [SerializeField] private Image imgProgressBarFill;

        private Vector3 originalScale = Vector3.one;
        private bool isPointerDown = false;
        private bool isPedalHeldDown = false;
        private float holdDuration = 0f;
        private float releasedBannerTimer = 0f;

        private string defaultButtonText = "HOLD PEDAL\n(60s LEAK TEST)\n[Touch & Hold]";
        private string defaultBannerText = "FOOT SERVICE BRAKE (TOUCH & HOLD FOR 60s)";
        private Color defaultBannerTextColor = new Color(0.72f, 0.78f, 0.85f);

        private void Awake()
        {
            if (airSystem == null) airSystem = FindFirstObjectByType<AirBrakeSystem>();
            if (dmvManager == null) dmvManager = FindFirstObjectByType<DMVTestManager>();
            if (pedalTransform == null) pedalTransform = GetComponent<RectTransform>();
            if (pedalImage == null) pedalImage = GetComponent<Image>();
            if (pedalImage != null) normalColor = pedalImage.color;
            if (txtPedalButton == null) txtPedalButton = GetComponentInChildren<Text>();

            if (pedalTransform != null) originalScale = pedalTransform.localScale;
            if (txtPedalButton != null && !string.IsNullOrEmpty(txtPedalButton.text))
            {
                defaultButtonText = txtPedalButton.text;
            }
        }

        private void Update()
        {
            if (airSystem == null) airSystem = FindFirstObjectByType<AirBrakeSystem>();
            if (dmvManager == null) dmvManager = FindFirstObjectByType<DMVTestManager>();

            bool spaceHeld = false;
            bool bPressed = false;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                spaceHeld = kb.spaceKey.isPressed;
                bPressed = kb.bKey.wasPressedThisFrame;
            }

            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && !mouse.leftButton.isPressed && isPointerDown)
            {
                if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count == 0)
                {
                    isPointerDown = false;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (!spaceHeld) { try { spaceHeld = Input.GetKey(KeyCode.Space); } catch {} }
            if (!bPressed) { try { bPressed = Input.GetKeyDown(KeyCode.B); } catch {} }
            try
            {
                if (Input.touchCount == 0 && !Input.GetMouseButton(0) && isPointerDown)
                {
                    isPointerDown = false;
                }
            }
            catch {}
#endif

            bool shouldBeHeld = isPointerDown || spaceHeld;

            if (shouldBeHeld && !isPedalHeldDown)
            {
                OnPedalPressed();
            }
            else if (!shouldBeHeld && isPedalHeldDown)
            {
                OnPedalReleased();
            }

            if (isPedalHeldDown)
            {
                holdDuration += Time.deltaTime;
                UpdateTimerVisuals(holdDuration);
            }
            else if (releasedBannerTimer > 0f)
            {
                releasedBannerTimer -= Time.deltaTime;
                if (releasedBannerTimer <= 0f)
                {
                    ResetBannerToDefault();
                }
            }

            if (bPressed) FanOnce();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPointerDown = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPointerDown = false;
        }

        private void OnPedalPressed()
        {
            isPedalHeldDown = true;
            holdDuration = 0f;
            releasedBannerTimer = 0f;

            if (airSystem != null)
            {
                airSystem.SetPedalHeld(true);
            }

            if (pedalTransform != null)
            {
                pedalTransform.localScale = Vector3.Scale(originalScale, pressedScale);
            }

            if (pedalImage != null)
            {
                pedalImage.color = pressedColor;
            }

            UpdateTimerVisuals(0f);
        }

        private void OnPedalReleased()
        {
            isPedalHeldDown = false;

            if (airSystem != null)
            {
                airSystem.SetPedalHeld(false);
            }

            if (pedalTransform != null)
            {
                pedalTransform.localScale = originalScale;
            }

            if (pedalImage != null)
            {
                pedalImage.color = normalColor;
            }

            // Restore pedal button text
            if (txtPedalButton != null)
            {
                txtPedalButton.text = defaultButtonText;
                txtPedalButton.color = Color.white;
            }

            // Banner feedback
            if (txtTimerBanner != null)
            {
                if (holdDuration >= 60f)
                {
                    txtTimerBanner.text = "✓ 60.0s COMPLETED! 1-MINUTE APPLIED LEAK TEST PASSED";
                    txtTimerBanner.color = new Color(0.25f, 0.95f, 0.45f);
                    releasedBannerTimer = 3.5f;
                }
                else
                {
                    txtTimerBanner.text = $"PEDAL RELEASED AT {holdDuration:F1}s / 60.0s (ABORTED)";
                    txtTimerBanner.color = new Color(0.96f, 0.45f, 0.25f);
                    releasedBannerTimer = 2.2f;
                }
            }

            if (rectProgressBarFill != null)
            {
                rectProgressBarFill.anchorMax = new Vector2(0f, 1f);
            }
        }

        private void UpdateTimerVisuals(float duration)
        {
            float target = 60f;
            float remaining = Mathf.Max(0f, target - duration);
            float progress = Mathf.Clamp01(duration / target);

            // 1. Update text directly on the pedal button
            if (txtPedalButton != null)
            {
                if (duration < target)
                {
                    txtPedalButton.text = $"HOLDING PEDAL...\n⏱ {duration:F1}s / {target:F0}s\n[KEEP PRESSED]";
                    txtPedalButton.color = new Color(0.98f, 0.88f, 0.22f); // Vibrant gold
                }
                else
                {
                    txtPedalButton.text = "✓ 60.0s COMPLETED!\nYOU CAN RELEASE\n[PASSED]";
                    txtPedalButton.color = new Color(0.25f, 0.95f, 0.45f); // Vibrant green
                }
            }

            // 2. Update dedicated Timer Banner text
            if (txtTimerBanner != null)
            {
                if (duration < target)
                {
                    txtTimerBanner.text = $"⏱ BRAKE HELD: {duration:F1}s / {target:F0}s  (REMAINING: {remaining:F1}s)";
                    txtTimerBanner.color = new Color(0.98f, 0.88f, 0.22f);
                }
                else
                {
                    txtTimerBanner.text = "✓ 60.0s COMPLETED! 1-MINUTE APPLIED LEAK TEST PASSED";
                    txtTimerBanner.color = new Color(0.25f, 0.95f, 0.45f);
                }
            }

            // 3. Update Visual Progress Bar
            if (rectProgressBarFill != null)
            {
                rectProgressBarFill.anchorMax = new Vector2(progress, 1f);
            }

            if (imgProgressBarFill != null)
            {
                imgProgressBarFill.color = Color.Lerp(
                    new Color(0.18f, 0.55f, 0.88f, 0.85f),
                    new Color(0.22f, 0.85f, 0.35f, 0.95f),
                    progress
                );
            }
        }

        private void ResetBannerToDefault()
        {
            if (txtTimerBanner != null)
            {
                txtTimerBanner.text = defaultBannerText;
                txtTimerBanner.color = defaultBannerTextColor;
            }
            if (rectProgressBarFill != null)
            {
                rectProgressBarFill.anchorMax = new Vector2(0f, 1f);
            }
        }

        /// <summary>
        /// Explicit trigger to Fan (pump) the pedal once.
        /// </summary>
        public void FanOnce()
        {
            if (airSystem != null)
            {
                airSystem.FanPedal();
            }

            if (pedalTransform != null)
            {
                pedalTransform.localScale = Vector3.Scale(originalScale, pressedScale);
                CancelInvoke(nameof(ResetPedalVisual));
                Invoke(nameof(ResetPedalVisual), 0.12f);
            }
        }

        private void ResetPedalVisual()
        {
            if (!isPedalHeldDown && pedalTransform != null)
            {
                pedalTransform.localScale = originalScale;
            }
        }

        public void BindAirSystem(AirBrakeSystem system)
        {
            airSystem = system;
        }

        public void SetupTimerUI(
            Text buttonText,
            GameObject bannerPanel,
            Text bannerText,
            RectTransform fillRect,
            Image fillImage,
            DMVTestManager dmv)
        {
            txtPedalButton = buttonText;
            timerBannerGO = bannerPanel;
            txtTimerBanner = bannerText;
            rectProgressBarFill = fillRect;
            imgProgressBarFill = fillImage;
            dmvManager = dmv;

            if (txtPedalButton != null && !string.IsNullOrEmpty(txtPedalButton.text))
            {
                defaultButtonText = txtPedalButton.text;
            }
            if (txtTimerBanner != null && !string.IsNullOrEmpty(txtTimerBanner.text))
            {
                defaultBannerText = txtTimerBanner.text;
                defaultBannerTextColor = txtTimerBanner.color;
            }
            if (rectProgressBarFill != null)
            {
                rectProgressBarFill.anchorMin = Vector2.zero;
                rectProgressBarFill.anchorMax = new Vector2(0f, 1f);
                rectProgressBarFill.offsetMin = Vector2.zero;
                rectProgressBarFill.offsetMax = Vector2.zero;
            }
        }
    }
}
