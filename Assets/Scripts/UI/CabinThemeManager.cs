using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CDL.AirBrakeSimulator
{
    /// <summary>
    /// Manages multi-truck cabin skins (e.g. Peterbilt 389 Classic vs Freightliner Cascadia Modern).
    /// Dynamically swaps dashboard backgrounds, circular dials, needles, push-pull valve knob sprites,
    /// warning lamps, and auditory buzzer clips at runtime while keeping all simulation physics and
    /// DMV examination state machines synchronized.
    /// Saves and restores student truck selection via PlayerPrefs.
    /// </summary>
    public class CabinThemeManager : MonoBehaviour
    {
        private const string PREF_KEY_TRUCK_INDEX = "SelectedTruckIndex";

        [Header("System References")]
        [SerializeField] private AirBrakeSystem airSystem;
        [SerializeField] private AirBrakeAudio airAudio;

        [Header("Truck Themes List")]
        [SerializeField] private List<TruckCabinConfig> availableThemes = new List<TruckCabinConfig>();
        [SerializeField] private int currentThemeIndex = 0;

        [Header("Dashboard UI Elements To Skin")]
        [SerializeField] private Image imgDashboardBackground;
        [SerializeField] private Image imgPrimaryDial;
        [SerializeField] private Image imgSecondaryDial;
        [SerializeField] private Image imgPrimaryNeedle;
        [SerializeField] private Image imgSecondaryNeedle;
        [SerializeField] private Image imgYellowKnob;
        [SerializeField] private Image imgRedKnob;
        [SerializeField] private Image imgWarningLight;

        [Header("Truck Label & Selector UI")]
        [SerializeField] private Text txtTruckModelName;
        [SerializeField] private Text txtTruckSubtitle;
        [SerializeField] private Button btnPrevTruck;
        [SerializeField] private Button btnNextTruck;

        // Events
        public event Action<TruckCabinConfig> OnThemeChanged;

        #region Public Properties

        public int CurrentThemeIndex => currentThemeIndex;
        public int TotalThemes => availableThemes != null ? availableThemes.Count : 0;
        public TruckCabinConfig CurrentConfig => (availableThemes != null && availableThemes.Count > currentThemeIndex && currentThemeIndex >= 0) ? availableThemes[currentThemeIndex] : null;

        #endregion

        private void Awake()
        {
            if (airSystem == null) airSystem = FindFirstObjectByType<AirBrakeSystem>();
            if (airAudio == null) airAudio = FindFirstObjectByType<AirBrakeAudio>();

            EnsureDefaultPresets();
            WireSelectorButtons();
        }

        private void Start()
        {
            // Restore saved preference
            int savedIndex = PlayerPrefs.GetInt(PREF_KEY_TRUCK_INDEX, 0);
            if (availableThemes != null && availableThemes.Count > 0)
            {
                currentThemeIndex = Mathf.Clamp(savedIndex, 0, availableThemes.Count - 1);
            }

            ApplyTheme(currentThemeIndex);
        }

        private void OnEnable()
        {
            if (airSystem != null)
            {
                airSystem.OnValveStateChanged += HandleValveStateChanged;
            }
        }

        private void OnDisable()
        {
            if (airSystem != null)
            {
                airSystem.OnValveStateChanged -= HandleValveStateChanged;
            }
        }

        private void WireSelectorButtons()
        {
            if (btnPrevTruck != null)
            {
                btnPrevTruck.onClick.RemoveAllListeners();
                btnPrevTruck.onClick.AddListener(PreviousTheme);
            }

            if (btnNextTruck != null)
            {
                btnNextTruck.onClick.RemoveAllListeners();
                btnNextTruck.onClick.AddListener(NextTheme);
            }
        }

        #region Theme Switching Logic

        /// <summary>
        /// Switch to the next available truck cabin theme.
        /// </summary>
        public void NextTheme()
        {
            if (availableThemes == null || availableThemes.Count == 0) return;
            int next = (currentThemeIndex + 1) % availableThemes.Count;
            SetCabinTheme(next);
        }

        /// <summary>
        /// Switch to the previous available truck cabin theme.
        /// </summary>
        public void PreviousTheme()
        {
            if (availableThemes == null || availableThemes.Count == 0) return;
            int prev = (currentThemeIndex - 1 + availableThemes.Count) % availableThemes.Count;
            SetCabinTheme(prev);
        }

        /// <summary>
        /// Sets and applies the truck cabin theme by index, updating UI sprites, colors, and audio.
        /// </summary>
        public void SetCabinTheme(int themeIndex)
        {
            if (availableThemes == null || availableThemes.Count == 0) return;

            currentThemeIndex = Mathf.Clamp(themeIndex, 0, availableThemes.Count - 1);
            PlayerPrefs.SetInt(PREF_KEY_TRUCK_INDEX, currentThemeIndex);
            PlayerPrefs.Save();

            ApplyTheme(currentThemeIndex);
        }

        private void ApplyTheme(int index)
        {
            TruckCabinConfig config = CurrentConfig;
            if (config == null) return;

            // 1. Dashboard Background
            if (imgDashboardBackground != null && config.dashboardBackground != null)
            {
                imgDashboardBackground.sprite = config.dashboardBackground;
            }

            // 2. Gauges
            if (imgPrimaryDial != null && config.primaryGaugeDial != null)
            {
                imgPrimaryDial.sprite = config.primaryGaugeDial;
            }
            if (imgSecondaryDial != null && config.secondaryGaugeDial != null)
            {
                imgSecondaryDial.sprite = config.secondaryGaugeDial;
            }

            // 3. Needles
            if (imgPrimaryNeedle != null && config.needleSprite != null)
            {
                imgPrimaryNeedle.sprite = config.needleSprite;
                imgPrimaryNeedle.color = config.needleColor;
            }
            if (imgSecondaryNeedle != null && config.needleSprite != null)
            {
                imgSecondaryNeedle.sprite = config.needleSprite;
                imgSecondaryNeedle.color = config.needleColor;
            }

            // 4. Push-Pull Valve Knobs (respecting current physical state)
            UpdateKnobSprites(config);

            // 5. Warning Annunciator Lamp
            if (imgWarningLight != null)
            {
                imgWarningLight.color = config.warningLightColor;
            }

            // 6. Audio Clip (Mechanical Buzzer vs Modern Digital Chime)
            if (airAudio != null && config.lowAirBuzzerSound != null)
            {
                airAudio.SetCustomBuzzerClip(config.lowAirBuzzerSound);
            }

            // 7. Labels
            if (txtTruckModelName != null)
            {
                txtTruckModelName.text = config.truckName.ToUpper();
            }
            if (txtTruckSubtitle != null)
            {
                txtTruckSubtitle.text = config.truckSubtitle;
            }

            OnThemeChanged?.Invoke(config);
        }

        private void HandleValveStateChanged(ValveType valve, bool pushedIn)
        {
            UpdateKnobSprites(CurrentConfig);
        }

        private void UpdateKnobSprites(TruckCabinConfig config)
        {
            if (config == null || airSystem == null) return;

            // Yellow Tractor Knob
            if (imgYellowKnob != null)
            {
                Sprite targetYellow = airSystem.IsYellowBrakePushedIn ? config.yellowKnobPushed : config.yellowKnobPopped;
                if (targetYellow != null)
                {
                    imgYellowKnob.sprite = targetYellow;
                }
            }

            // Red Trailer Knob
            if (imgRedKnob != null)
            {
                Sprite targetRed = airSystem.IsRedTrailerPushedIn ? config.redKnobPushed : config.redKnobPopped;
                if (targetRed != null)
                {
                    imgRedKnob.sprite = targetRed;
                }
            }
        }

        #endregion

        #region Default Presets Generator (Fallback & Self-Assembly)

        private void EnsureDefaultPresets()
        {
            if (availableThemes != null && availableThemes.Count >= 2 && availableThemes[0] != null && availableThemes[1] != null)
            {
                return;
            }

            availableThemes = new List<TruckCabinConfig>();

            // Preset 1: Peterbilt 389 Classic
            TruckCabinConfig peterbilt = ScriptableObject.CreateInstance<TruckCabinConfig>();
            peterbilt.truckName = "Peterbilt 389";
            peterbilt.truckSubtitle = "Classic American Conventional (Woodgrain & Chrome)";
            peterbilt.dashboardBackground = ProceduralSpriteGenerator.CreateWoodgrainDashboardSprite(256, 256);
            peterbilt.primaryGaugeDial = ProceduralSpriteGenerator.CreateGaugeDialSprite(512, false);
            peterbilt.secondaryGaugeDial = ProceduralSpriteGenerator.CreateGaugeDialSprite(512, false);
            peterbilt.needleSprite = ProceduralSpriteGenerator.CreateNeedleSprite(48, 256, false);
            peterbilt.needleColor = new Color(0.95f, 0.25f, 0.15f, 1f);
            peterbilt.yellowKnobPushed = ProceduralSpriteGenerator.CreateYellowDiamondSprite(256, true, false);
            peterbilt.yellowKnobPopped = ProceduralSpriteGenerator.CreateYellowDiamondSprite(256, false, false);
            peterbilt.redKnobPushed = ProceduralSpriteGenerator.CreateRedOctagonSprite(256, true, false);
            peterbilt.redKnobPopped = ProceduralSpriteGenerator.CreateRedOctagonSprite(256, false, false);
            peterbilt.warningLightColor = new Color(1.0f, 0.2f, 0.2f, 1f);
            peterbilt.lowAirBuzzerSound = AirBrakeAudio.CreateProceduralMechanicalBuzzerClip();
            peterbilt.accentColor = new Color(0.95f, 0.8f, 0.1f, 1f);
            peterbilt.headerColor = new Color(0.14f, 0.11f, 0.09f, 1f);
            availableThemes.Add(peterbilt);

            // Preset 2: Freightliner Cascadia Modern
            TruckCabinConfig cascadia = ScriptableObject.CreateInstance<TruckCabinConfig>();
            cascadia.truckName = "Freightliner Cascadia";
            cascadia.truckSubtitle = "Modern Aerodynamic Highway (Digital & Carbon Fiber)";
            cascadia.dashboardBackground = ProceduralSpriteGenerator.CreateCarbonFiberDashboardSprite(256, 256);
            cascadia.primaryGaugeDial = ProceduralSpriteGenerator.CreateGaugeDialSprite(512, true);
            cascadia.secondaryGaugeDial = ProceduralSpriteGenerator.CreateGaugeDialSprite(512, true);
            cascadia.needleSprite = ProceduralSpriteGenerator.CreateNeedleSprite(48, 256, true);
            cascadia.needleColor = new Color(0.1f, 0.85f, 1.0f, 1f); // Neon Cyan Needle
            cascadia.yellowKnobPushed = ProceduralSpriteGenerator.CreateYellowDiamondSprite(256, true, true);
            cascadia.yellowKnobPopped = ProceduralSpriteGenerator.CreateYellowDiamondSprite(256, false, true);
            cascadia.redKnobPushed = ProceduralSpriteGenerator.CreateRedOctagonSprite(256, true, true);
            cascadia.redKnobPopped = ProceduralSpriteGenerator.CreateRedOctagonSprite(256, false, true);
            cascadia.warningLightColor = new Color(1.0f, 0.35f, 0.05f, 1f);
            cascadia.lowAirBuzzerSound = AirBrakeAudio.CreateProceduralModernChimeClip();
            cascadia.accentColor = new Color(0.2f, 0.8f, 1.0f, 1f);
            cascadia.headerColor = new Color(0.10f, 0.13f, 0.18f, 1f);
            availableThemes.Add(cascadia);
        }

        #endregion

        #region Public Bindings (for SceneSetupHelper)

        public void BindUIElements(
            Image dashBg,
            Image priDial, Image secDial,
            Image priNeedle, Image secNeedle,
            Image yellowKnob, Image redKnob,
            Image warningLight,
            Text modelName, Text subtitle,
            Button prevBtn, Button nextBtn,
            List<TruckCabinConfig> configs = null)
        {
            imgDashboardBackground = dashBg;
            imgPrimaryDial = priDial;
            imgSecondaryDial = secDial;
            imgPrimaryNeedle = priNeedle;
            imgSecondaryNeedle = secNeedle;
            imgYellowKnob = yellowKnob;
            imgRedKnob = redKnob;
            imgWarningLight = warningLight;
            txtTruckModelName = modelName;
            txtTruckSubtitle = subtitle;
            btnPrevTruck = prevBtn;
            btnNextTruck = nextBtn;

            if (configs != null && configs.Count > 0)
            {
                availableThemes = configs;
            }

            WireSelectorButtons();
            ApplyTheme(currentThemeIndex);
        }

        #endregion
    }
}
