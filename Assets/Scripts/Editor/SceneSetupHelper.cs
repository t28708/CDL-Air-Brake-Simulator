#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CDL.AirBrakeSimulator.Editor
{
    /// <summary>
    /// Programmatic Scene Setup & Procedural UI Builder for the CDL Air Brake Simulator.
    /// Creates the complete responsive Canvas UI hierarchy (1080x1920 Portrait),
    /// generates crisp multi-truck instrument sprites, and wires all simulation components automatically.
    /// </summary>
    public static class SceneSetupHelper
    {
        [MenuItem("CDL Simulator/▶ Start New Game", false, 0)]
        public static void StartNewGameFromMenu()
        {
            if (EditorApplication.isPlaying)
            {
                GameObject canvas = GameObject.Find("CDL_AirBrake_Canvas");
                Transform bg = canvas != null ? canvas.transform.Find("Background") : null;
                if (bg == null || bg.Find("3_PneumaticValvesSection") == null || bg.Find("5_ExaminerCardSection") == null)
                {
                    GameObject oldCanvas = GameObject.Find("CDL_AirBrake_Canvas");
                    if (oldCanvas != null)
                    {
                        oldCanvas.name = "CDL_OldCanvas_Disposed";
                        oldCanvas.SetActive(false);
                        Object.Destroy(oldCanvas);
                    }

                    GameObject builderGO = new GameObject("RuntimeSceneAutoBuilder");
                    RuntimeSceneBuilder builder = builderGO.AddComponent<RuntimeSceneBuilder>();
                    builder.BuildRuntimeUI();
                }
                else
                {
                    AirBrakeUIController uiCtrl = Object.FindFirstObjectByType<AirBrakeUIController>();
                    if (uiCtrl != null)
                    {
                        uiCtrl.StartNewGame();
                    }
                    else
                    {
                        DMVTestManager dmv = Object.FindFirstObjectByType<DMVTestManager>();
                        if (dmv != null) dmv.RestartFullTest();
                    }
                }
                Debug.Log("<color=#50fa7b><b>[CDL Air Brake Simulator] New Game Started in Play Mode!</b></color>");
            }
            else
            {
                BuildCompleteScene();
                EditorApplication.isPlaying = true;
                Debug.Log("<color=#50fa7b><b>[CDL Air Brake Simulator] Scene Built & Started New Game in Play Mode!</b></color>");
            }
        }

        [InitializeOnLoadMethod]
        private static void OnEditorReload()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                GameObject canvas = GameObject.Find("CDL_AirBrake_Canvas");
                Transform bg = canvas != null ? canvas.transform.Find("Background") : null;
                if (bg == null || bg.Find("Layout_V9_AspectRatioCockpit") == null)
                {
                    Debug.Log("<color=#38bdf8><b>[CDL Air Brake Simulator] Auto-building and saving 5-section UI hierarchy (Layout V9 Aspect Ratio Cockpit)...</b></color>");
                    BuildCompleteScene();
                }
            };
        }

        [MenuItem("CDL Simulator/Build & Setup Complete Scene", false, 20)]
        public static void BuildCompleteScene()
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Build CDL Air Brake Simulator Scene");
            int groupIndex = Undo.GetCurrentGroup();

            // 1. Setup or Find AirBrakeManager GameObject
            GameObject managerGO = GameObject.Find("AirBrakeManager");
            if (managerGO == null)
            {
                managerGO = new GameObject("AirBrakeManager");
                Undo.RegisterCreatedObjectUndo(managerGO, "Create AirBrakeManager");
            }

            AirBrakeSystem airSystem = managerGO.GetComponent<AirBrakeSystem>();
            if (airSystem == null) airSystem = Undo.AddComponent<AirBrakeSystem>(managerGO);

            AirBrakeAudio airAudio = managerGO.GetComponent<AirBrakeAudio>();
            if (airAudio == null) airAudio = Undo.AddComponent<AirBrakeAudio>(managerGO);

            DMVTestManager dmvManager = managerGO.GetComponent<DMVTestManager>();
            if (dmvManager == null) dmvManager = Undo.AddComponent<DMVTestManager>(managerGO);

            // 2. Generate Multi-Truck ScriptableObject Presets
            List<TruckCabinConfig> truckPresets = EnsurePresetAssets();

            // 3. Setup EventSystem with New Input System support
            EnsureEventSystem();

            // 4. Setup Canvas & Responsive Scaler (1080x1920, Match 0.5)
            GameObject canvasGO = GameObject.Find("CDL_AirBrake_Canvas");
            if (canvasGO != null)
            {
                Undo.DestroyObjectImmediate(canvasGO);
            }

            canvasGO = new GameObject("CDL_AirBrake_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create CDL_AirBrake_Canvas");

            Canvas canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Font defaultFont = GetDefaultFont();

            // Full-Screen Dark Backdrop behind aspect-fitted cockpit container
            GameObject backdropGO = CreateUIPanel(canvasGO.transform, "CanvasBackdrop", new Color(0.04f, 0.05f, 0.07f, 1f));
            SetStretch(backdropGO.GetComponent<RectTransform>());

            // Root Skinnable Dashboard Container with AspectRatioFitter (prevents horizontal/vertical stretching on 4:3 tablets & 20:9 phones)
            GameObject bgGO = CreateUIPanel(canvasGO.transform, "Background", new Color(0.08f, 0.09f, 0.11f, 1f));
            SetStretch(bgGO.GetComponent<RectTransform>());
            AspectRatioFitter bgAspectFitter = Undo.AddComponent<AspectRatioFitter>(bgGO);
            bgAspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            bgAspectFitter.aspectRatio = 1080f / 1920f;

            Image imgDashboardBackground = bgGO.GetComponent<Image>();
            if (truckPresets.Count > 0 && truckPresets[0].dashboardBackground != null)
            {
                imgDashboardBackground.sprite = truckPresets[0].dashboardBackground;
            }
            CreateUIPanel(bgGO.transform, "Layout_V9_AspectRatioCockpit", Color.clear);

            // 0. Top Safe Area Padding for mobile notches/cameras (Height: 90px, y = 0 to 90)
            GameObject safeTopGO = CreateUIPanel(bgGO.transform, "0_TopSafeAreaFiller", new Color(0.06f, 0.07f, 0.09f, 1f));
            SetAnchorTop(safeTopGO.GetComponent<RectTransform>(), 0f, 90f);

            // =========================================================================
            // 1. TOP HEADER & MODE SWITCHER (Height: 150px, y = 90 to 240)
            // =========================================================================
            GameObject headerGO = CreateUIPanel(bgGO.transform, "1_TopBarPanel", new Color(0.09f, 0.11f, 0.14f, 0.98f));
            SetAnchorTop(headerGO.GetComponent<RectTransform>(), 90f, 150f);

            // Row 1: System Bar (local Y = 32f, H = 48f)
            // 1a. Truck Cabin Selector (< PREV  TRUCK NAME  NEXT >)
            GameObject btnPrevTruckGO = CreateUIButton(headerGO.transform, "BtnPrevTruck", "◀", new Color(0.16f, 0.20f, 0.26f), defaultFont, 20);
            SetRect(btnPrevTruckGO.GetComponent<RectTransform>(), -470f, 32f, 50f, 48f);
            Button btnPrevTruck = btnPrevTruckGO.GetComponent<Button>();

            Text txtTruckModelName = CreateUIText(headerGO.transform, "TruckModelName", "PETERBILT 389", 18, FontStyle.Bold, new Color(0.90f, 0.92f, 0.95f), TextAnchor.MiddleCenter, defaultFont);
            SetRect(txtTruckModelName.rectTransform, -330f, 40f, 210f, 24f);

            Text txtTruckSubtitle = CreateUIText(headerGO.transform, "TruckSubtitle", "Conventional Cab", 12, FontStyle.Normal, new Color(0.55f, 0.60f, 0.68f), TextAnchor.MiddleCenter, defaultFont);
            SetRect(txtTruckSubtitle.rectTransform, -330f, 20f, 210f, 18f);

            GameObject btnNextTruckGO = CreateUIButton(headerGO.transform, "BtnNextTruck", "▶", new Color(0.16f, 0.20f, 0.26f), defaultFont, 20);
            SetRect(btnNextTruckGO.GetComponent<RectTransform>(), -190f, 32f, 50f, 48f);
            Button btnNextTruck = btnNextTruckGO.GetComponent<Button>();

            // 1b. Step Badge Pill
            GameObject stepPillGO = CreateUIPanel(headerGO.transform, "StepPillBox", new Color(0.14f, 0.18f, 0.24f, 0.95f));
            SetRect(stepPillGO.GetComponent<RectTransform>(), -35f, 32f, 160f, 48f);
            Text txtStepHeader = CreateUIText(stepPillGO.transform, "StepHeaderPill", "STEP 1/5", 17, FontStyle.Bold, new Color(0.98f, 0.85f, 0.25f), TextAnchor.MiddleCenter, defaultFont);
            SetStretch(txtStepHeader.rectTransform, 4f);

            // 1c. Toggle DMV Button
            GameObject btnToggleDmvGO = CreateUIButton(headerGO.transform, "BtnToggleDMV", "📋 HIDE DMV", new Color(0.14f, 0.32f, 0.55f), defaultFont, 16);
            SetRect(btnToggleDmvGO.GetComponent<RectTransform>(), 180f, 32f, 190f, 48f);
            Button btnToggleDMV = btnToggleDmvGO.GetComponent<Button>();
            Text txtToggleDMV = btnToggleDmvGO.GetComponentInChildren<Text>();
            ExaminerCardToggle toggleBar = Undo.AddComponent<ExaminerCardToggle>(btnToggleDmvGO);
            toggleBar.Setup(ExaminerCardToggle.ToggleAction.ToggleCard, null, null, txtToggleDMV);

            // 1d. Reset / Start New Game Button
            GameObject btnStartNewGameGO = CreateUIButton(headerGO.transform, "BtnStartNewGame", "↻ RESET", new Color(0.12f, 0.45f, 0.30f), defaultFont, 16);
            SetRect(btnStartNewGameGO.GetComponent<RectTransform>(), 385f, 32f, 120f, 48f);
            Button btnStartNewGame = btnStartNewGameGO.GetComponent<Button>();

            // Row 2: Dedicated Mode Switcher (local Y = -30f, H = 48f, total width 1020px)
            GameObject btnModeTrainingGO = CreateUIButton(headerGO.transform, "BtnModeTraining", "🎓 TRAINING MODE (ACTIVE)", new Color(0.08f, 0.45f, 0.32f), defaultFont, 18);
            SetRect(btnModeTrainingGO.GetComponent<RectTransform>(), -260f, -30f, 500f, 48f);
            Button btnModeTraining = btnModeTrainingGO.GetComponent<Button>();
            Image imgModeTrainingBg = btnModeTrainingGO.GetComponent<Image>();
            Text txtModeTraining = btnModeTrainingGO.GetComponentInChildren<Text>();

            GameObject btnModeExamGO = CreateUIButton(headerGO.transform, "BtnModeExam", "⏱️ START DMV EXAM", new Color(0.14f, 0.17f, 0.22f), defaultFont, 18);
            SetRect(btnModeExamGO.GetComponent<RectTransform>(), 260f, -30f, 500f, 48f);
            Button btnModeExam = btnModeExamGO.GetComponent<Button>();
            Image imgModeExamBg = btnModeExamGO.GetComponent<Image>();
            Text txtModeExam = btnModeExamGO.GetComponentInChildren<Text>();

            // =========================================================================
            // 2. DASHBOARD & GAUGES SECTION (Height: 460px, y = 245 to 705)
            // =========================================================================
            GameObject clusterGO = CreateUIPanel(bgGO.transform, "2_DashboardGaugesSection", new Color(0.07f, 0.09f, 0.11f, 0.98f));
            SetAnchorTop(clusterGO.GetComponent<RectTransform>(), 245f, 460f);

            Sprite initDial = truckPresets.Count > 0 ? truckPresets[0].primaryGaugeDial : null;
            Sprite initNeedle = truckPresets.Count > 0 ? truckPresets[0].needleSprite : null;

            // Primary Gauge (Left: Drive Axles, Diameter 370px)
            CreateGaugeVisual(clusterGO.transform, "PrimaryGauge", -265f, 0f, initDial, initNeedle, defaultFont, "PRIMARY AIR (DRIVE)",
                out RectTransform priNeedle, out Text priPsiText, out Image imgPrimaryDial, out Image imgPrimaryNeedle);

            // Secondary Gauge (Right: Steer Axle, Diameter 370px)
            CreateGaugeVisual(clusterGO.transform, "SecondaryGauge", 265f, 0f, initDial, initNeedle, defaultFont, "SECONDARY AIR (STEER)",
                out RectTransform secNeedle, out Text secPsiText, out Image imgSecondaryDial, out Image imgSecondaryNeedle);

            // Center Column: Upgraded Digital "DID" (Driver Information Display) & RPM Counter
            GameObject warningPanelGO = CreateUIPanel(clusterGO.transform, "DID_DriverInfoDisplay", new Color(0.05f, 0.08f, 0.13f, 0.98f));
            SetRect(warningPanelGO.GetComponent<RectTransform>(), 0f, 40f, 224f, 108f);
            Outline didOutline = Undo.AddComponent<Outline>(warningPanelGO);
            didOutline.effectColor = new Color(0.20f, 0.25f, 0.33f, 1f);
            didOutline.effectDistance = new Vector2(2f, -2f);

            Image warningIcon = warningPanelGO.GetComponent<Image>();
            warningIcon.sprite = EnsureSpriteAsset("Assets/Sprites/WarningLamp.png", () => ProceduralSpriteGenerator.CreateWarningLampTexture(128));
            warningIcon.color = new Color(0.06f, 0.10f, 0.16f, 0.98f);

            Text warningText = CreateUIText(warningPanelGO.transform, "WarningText", "DID • COCKPIT\nAIR SYSTEM: NORMAL\n(<60 PSI WARN)", 20, FontStyle.Bold, new Color(0.0f, 0.90f, 0.46f), TextAnchor.MiddleCenter, defaultFont);
            SetRect(warningText.rectTransform, 0f, 0f, 212f, 96f);

            // Digital RPM Counter
            GameObject rpmBoxGO = CreateUIPanel(clusterGO.transform, "RpmCounterBox", new Color(0.05f, 0.07f, 0.09f, 0.95f));
            SetRect(rpmBoxGO.GetComponent<RectTransform>(), 0f, -58f, 224f, 46f);
            Text txtRpmCounter = CreateUIText(rpmBoxGO.transform, "RpmCounterText", "POWER: OFF", 19, FontStyle.Bold, new Color(0.40f, 0.45f, 0.52f, 0.8f), TextAnchor.MiddleCenter, defaultFont);
            SetStretch(txtRpmCounter.rectTransform, 4f);

            GaugeController gaugeCtrl = Undo.AddComponent<GaugeController>(clusterGO);
            gaugeCtrl.BindAirSystem(airSystem);
            gaugeCtrl.SetNeedleReferences(priNeedle, secNeedle);
            gaugeCtrl.SetTextReferences(priPsiText, secPsiText);
            gaugeCtrl.SetImageReferences(imgPrimaryDial, imgSecondaryDial, imgPrimaryNeedle, imgSecondaryNeedle);

            // =========================================================================
            // 3. UNIFIED "BENDIX MV-3" VALVE MODULE (Centered MV3_Module_Panel)
            // =========================================================================
            GameObject valveConsoleGO = CreateUIPanel(bgGO.transform, "3_PneumaticValvesSection", new Color(0.06f, 0.08f, 0.11f, 0.98f));
            SetAnchorTop(valveConsoleGO.GetComponent<RectTransform>(), 710f, 280f);
            LayoutElement valveFlexLayout = Undo.AddComponent<LayoutElement>(valveConsoleGO);
            valveFlexLayout.flexibleHeight = 1f;
            valveFlexLayout.minHeight = 380f;

            // Self-contained MV3_Module_Panel card (Anchor: Middle-Center, max 920x520, #1E2530 bezel, #334155 border)
            GameObject mv3PanelGO = CreateUIPanel(valveConsoleGO.transform, "MV3_Module_Panel", new Color(0.118f, 0.145f, 0.188f, 0.99f));
            RectTransform mv3Rect = mv3PanelGO.GetComponent<RectTransform>();
            SetRect(mv3Rect, 0f, 0f, 920f, 268f);
            Outline mv3Border = Undo.AddComponent<Outline>(mv3PanelGO);
            mv3Border.effectColor = new Color(0.20f, 0.255f, 0.333f, 1f); // #334155
            mv3Border.effectDistance = new Vector2(2f, -2f);

            // 4 Corner Screw Accents on MV3_Module_Panel
            CreateCornerScrew(mv3PanelGO.transform, "ScrewTL", -436f, 112f, defaultFont);
            CreateCornerScrew(mv3PanelGO.transform, "ScrewTR", 436f, 112f, defaultFont);
            CreateCornerScrew(mv3PanelGO.transform, "ScrewBL", -436f, -112f, defaultFont);
            CreateCornerScrew(mv3PanelGO.transform, "ScrewBR", 436f, -112f, defaultFont);

            Text txtValvesHeader = CreateUIText(mv3PanelGO.transform, "ValvesHeader", "BENDIX MV-3 DASH VALVE MODULE", 15, FontStyle.Bold, new Color(0.58f, 0.64f, 0.72f), TextAnchor.MiddleCenter, defaultFont);
            SetRect(txtValvesHeader.rectTransform, 0f, 114f, 440f, 24f);

            // HorizontalLayoutGroup holding LeftValveColumn & RightValveColumn with fixed spacing
            GameObject columnsRowGO = CreateUIPanel(mv3PanelGO.transform, "ValveColumnsRow", Color.clear);
            SetRect(columnsRowGO.GetComponent<RectTransform>(), 0f, -6f, 860f, 244f);
            HorizontalLayoutGroup hlg = Undo.AddComponent<HorizontalLayoutGroup>(columnsRowGO);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 48f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            Sprite initYellow = truckPresets.Count > 0 ? truckPresets[0].yellowKnobPopped : null;
            Sprite initRed = truckPresets.Count > 0 ? truckPresets[0].redKnobPopped : null;

            CreateValveKnob(columnsRowGO.transform, "LeftValveColumn", -220f, 0f, initYellow, defaultFont, "TRACTOR PARKING (MV-3)", "PULLED OUT • PARKED",
                out Button btnYellow, out RectTransform yellowRect, out Text txtYellow, out Image imgYellowKnob);

            CreateValveKnob(columnsRowGO.transform, "RightValveColumn", 220f, 0f, initRed, defaultFont, "TRAILER AIR SUPPLY", "PULLED OUT • EVACUATED",
                out Button btnRed, out RectTransform redRect, out Text txtRed, out Image imgRedKnob);

            Text txtValveNotice = CreateUIText(mv3PanelGO.transform, "ValveNotice", "Auto Pop-Out:\n20–45 PSI", 14, FontStyle.Normal, new Color(0.48f, 0.54f, 0.62f), TextAnchor.MiddleCenter, defaultFont);
            SetRect(txtValveNotice.rectTransform, 0f, -8f, 140f, 44f);

            // =========================================================================
            // 4. CONTROLS & PEDALS SECTION (Height: 460px, y = 995 to 1455)
            // =========================================================================
            GameObject controlsGO = CreateUIPanel(bgGO.transform, "4_ControlsPedalsSection", new Color(0.07f, 0.09f, 0.12f, 0.98f));
            SetAnchorTop(controlsGO.GetComponent<RectTransform>(), 995f, 460f);

            // Row 1: Ignition Switch & Throttle Control (local Y = 165f)
            Text txtIgnLabel = CreateUIText(controlsGO.transform, "IgnitionLabel", "IGNITION SWITCH:", 15, FontStyle.Bold, new Color(0.55f, 0.62f, 0.72f), TextAnchor.MiddleLeft, defaultFont);
            SetRect(txtIgnLabel.rectTransform, -260f, 200f, 460f, 22f);

            GameObject btnOffGO = CreateUIButton(controlsGO.transform, "BtnIgnitionOff", "OFF", new Color(0.18f, 0.22f, 0.28f), defaultFont, 18);
            SetRect(btnOffGO.GetComponent<RectTransform>(), -410f, 165f, 140f, 48f);

            GameObject btnAccGO = CreateUIButton(controlsGO.transform, "BtnIgnitionAcc", "ACC / ON", new Color(0.18f, 0.22f, 0.28f), defaultFont, 17);
            SetRect(btnAccGO.GetComponent<RectTransform>(), -260f, 165f, 140f, 48f);

            GameObject btnRunGO = CreateUIButton(controlsGO.transform, "BtnIgnitionRun", "RUNNING", new Color(0.18f, 0.22f, 0.28f), defaultFont, 18);
            SetRect(btnRunGO.GetComponent<RectTransform>(), -110f, 165f, 140f, 48f);

            Text txtRevLabel = CreateUIText(controlsGO.transform, "RevLabel", "THROTTLE CONTROL:", 15, FontStyle.Bold, new Color(0.55f, 0.62f, 0.72f), TextAnchor.MiddleLeft, defaultFont);
            SetRect(txtRevLabel.rectTransform, 260f, 200f, 460f, 22f);

            GameObject btnRevGO = CreateUIButton(controlsGO.transform, "BtnRevEngine", "⚡ REV ENGINE (1500 RPM)", new Color(0.14f, 0.35f, 0.62f), defaultFont, 19);
            SetRect(btnRevGO.GetComponent<RectTransform>(), 260f, 165f, 460f, 48f);
            Text txtRevEngine = btnRevGO.GetComponentInChildren<Text>();

            // Middle Row: Foot Service Brake Timer Banner (local Y = 105f)
            GameObject pedalTimerBannerGO = CreateUIPanel(controlsGO.transform, "PedalTimerBanner", new Color(0.05f, 0.07f, 0.10f, 0.95f));
            SetRect(pedalTimerBannerGO.GetComponent<RectTransform>(), 0f, 105f, 980f, 30f);

            GameObject fillGO = CreateUIPanel(pedalTimerBannerGO.transform, "ProgressBarFill", new Color(0.18f, 0.55f, 0.88f, 0.85f));
            RectTransform fillRect = fillGO.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fillImg = fillGO.GetComponent<Image>();

            Text txtPedalTimer = CreateUIText(pedalTimerBannerGO.transform, "TimerBannerText", "FOOT SERVICE BRAKE (TOUCH & HOLD FOR 60s LEAK TEST)", 15, FontStyle.Bold, new Color(0.65f, 0.72f, 0.80f), TextAnchor.MiddleCenter, defaultFont);
            SetStretch(txtPedalTimer.rectTransform, 2f);

            // Row 2: Massive Comfortable Touch Pads for Pedals (local Y = 5f, Height: 150px)
            GameObject btnHoldPedalGO = CreateUIButton(controlsGO.transform, "BtnHoldPedal", "HOLD SERVICE BRAKE\n(60s LEAK TEST)\n[Touch & Hold]", new Color(0.14f, 0.18f, 0.25f), defaultFont, 20);
            SetRect(btnHoldPedalGO.GetComponent<RectTransform>(), -250f, 5f, 470f, 150f);
            Text txtHoldPedal = btnHoldPedalGO.GetComponentInChildren<Text>();

            InteractivePedal interactivePedal = Undo.AddComponent<InteractivePedal>(btnHoldPedalGO);
            interactivePedal.BindAirSystem(airSystem);
            interactivePedal.SetupTimerUI(txtHoldPedal, pedalTimerBannerGO, txtPedalTimer, fillRect, fillImg, dmvManager);

            GameObject btnFanGO = CreateUIButton(controlsGO.transform, "BtnFanPedal", "FAN PEDAL\n(PUMP BRAKES)\n[-8 to -12 PSI]", new Color(0.68f, 0.24f, 0.10f), defaultFont, 20);
            SetRect(btnFanGO.GetComponent<RectTransform>(), 250f, 5f, 470f, 150f);

            // Row 3: Hotkeys & Fast Forward Pedals Button (local Y = -105f)
            Text txtKeysLeft = CreateUIText(controlsGO.transform, "KeysLeftHint", "<I> Ignition  |  <F> Rev  |  <Space> Hold", 15, FontStyle.Normal, new Color(0.48f, 0.54f, 0.62f), TextAnchor.MiddleCenter, defaultFont);
            SetRect(txtKeysLeft.rectTransform, -250f, -105f, 470f, 42f);

            GameObject btnFastPedalGO = CreateUIButton(controlsGO.transform, "BtnFastForwardPedals", "⏩ FAST FORWARD 60s >>", new Color(0.65f, 0.45f, 0.08f), defaultFont, 18);
            SetRect(btnFastPedalGO.GetComponent<RectTransform>(), 250f, -105f, 470f, 42f);
            Button btnFastPedals = btnFastPedalGO.GetComponent<Button>();
            btnFastPedals.onClick.AddListener(() => dmvManager?.FastForwardAppliedLeakage());

            // =========================================================================
            // 5. DMV EXAMINER BOTTOM SHEET (Anchored above 56px minimum safe bottom margin)
            // =========================================================================
            float safeBottomMargin = 56f;
            GameObject dialogCardGO = CreateUIPanel(bgGO.transform, "5_ExaminerCardSection", new Color(0.09f, 0.11f, 0.15f, 0.98f));
            SetAnchorBottom(dialogCardGO.GetComponent<RectTransform>(), safeBottomMargin, 410f, 0f);

            // Compact Minimized Bar at the bottom (Height: 70px, above safeBottomMargin)
            GameObject minBarGO = CreateUIPanel(bgGO.transform, "ExaminerMinimizedBar", new Color(0.09f, 0.11f, 0.15f, 0.98f));
            SetAnchorBottom(minBarGO.GetComponent<RectTransform>(), safeBottomMargin, 70f, 0f);

            GameObject btnShowCardGO = CreateUIButton(minBarGO.transform, "BtnShowExaminerCard", "▲ SHOW DMV EXAMINER SCRIPT ▲", new Color(0.16f, 0.35f, 0.62f), defaultFont, 17);
            SetRect(btnShowCardGO.GetComponent<RectTransform>(), -170f, 0f, 680f, 50f);
            Button btnShowCard = btnShowCardGO.GetComponent<Button>();
            ExaminerCardToggle toggleShow = Undo.AddComponent<ExaminerCardToggle>(btnShowCardGO);
            toggleShow.Setup(ExaminerCardToggle.ToggleAction.ShowCard, dialogCardGO, minBarGO);

            GameObject btnFastMinBarGO = CreateUIButton(minBarGO.transform, "BtnFastForwardMinBar", "⏩ FAST FORWARD 60s >>", new Color(0.65f, 0.45f, 0.08f), defaultFont, 17);
            SetRect(btnFastMinBarGO.GetComponent<RectTransform>(), 355f, 0f, 320f, 50f);
            Button btnFastMinBar = btnFastMinBarGO.GetComponent<Button>();
            btnFastMinBar.onClick.AddListener(() => dmvManager?.FastForwardAppliedLeakage());

            minBarGO.SetActive(false);

            // Header Row (local Y = 190f, height 32px)
            Text txtInstructTitle = CreateUIText(dialogCardGO.transform, "TaskTitle", "📋 DMV EXAMINER & TRAINING GUIDE", 18, FontStyle.Bold, new Color(0.38f, 0.74f, 0.98f), TextAnchor.MiddleLeft, defaultFont);
            SetRect(txtInstructTitle.rectTransform, -120f, 190f, 740f, 32f);
            Button btnHeaderDismiss = Undo.AddComponent<Button>(txtInstructTitle.gameObject);
            ExaminerCardToggle toggleHeader = Undo.AddComponent<ExaminerCardToggle>(txtInstructTitle.gameObject);
            toggleHeader.Setup(ExaminerCardToggle.ToggleAction.HideCard, dialogCardGO, minBarGO);

            GameObject btnHideCardGO = CreateUIButton(dialogCardGO.transform, "BtnHideExaminerCard", "✕ HIDE", new Color(0.55f, 0.18f, 0.18f), defaultFont, 15);
            SetRect(btnHideCardGO.GetComponent<RectTransform>(), 410f, 190f, 180f, 32f);
            Button btnHideCard = btnHideCardGO.GetComponent<Button>();
            ExaminerCardToggle toggleHide = Undo.AddComponent<ExaminerCardToggle>(btnHideCardGO);
            toggleHide.Setup(ExaminerCardToggle.ToggleAction.HideCard, dialogCardGO, minBarGO);

            // Line 1: Next Action Box (local Y = 130f, height 54px)
            GameObject nextActionBoxGO = CreateUIPanel(dialogCardGO.transform, "NextActionBox", new Color(0.06f, 0.12f, 0.22f, 0.96f));
            SetRect(nextActionBoxGO.GetComponent<RectTransform>(), 0f, 130f, 1000f, 54f);

            Text txtNextAction = CreateUIText(nextActionBoxGO.transform, "NextActionText", "🎯 NEXT ACTION: Start engine (tap [ RUNNING ])", 19, FontStyle.Bold, new Color(0.22f, 0.75f, 0.98f), TextAnchor.MiddleLeft, defaultFont);
            SetStretch(txtNextAction.rectTransform, 10f);

            // Hidden legacy objects for compatibility
            GameObject whyWeDoThisBoxGO = CreateUIPanel(dialogCardGO.transform, "WhyWeDoThisBox", Color.clear);
            SetRect(whyWeDoThisBoxGO.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            Text txtWhyWeDoThis = CreateUIText(whyWeDoThisBoxGO.transform, "WhyWeDoThisText", "", 1, FontStyle.Normal, Color.clear, TextAnchor.MiddleLeft, defaultFont);
            whyWeDoThisBoxGO.SetActive(false);

            GameObject goalBoxGO = CreateUIPanel(dialogCardGO.transform, "GoalBox", Color.clear);
            SetRect(goalBoxGO.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            Text txtInstructions = CreateUIText(goalBoxGO.transform, "StepInstructions", "", 1, FontStyle.Normal, Color.clear, TextAnchor.MiddleLeft, defaultFont);
            goalBoxGO.SetActive(false);

            // Line 2: Recite Box (local Y = 35f, height 88px)
            GameObject scriptBoxGO = CreateUIPanel(dialogCardGO.transform, "ScriptBox", new Color(0.06f, 0.08f, 0.12f, 0.96f));
            SetRect(scriptBoxGO.GetComponent<RectTransform>(), 0f, 35f, 1000f, 88f);

            Text txtVerbalScript = CreateUIText(scriptBoxGO.transform, "VerbalScriptText", "\"Examiner, I am starting the engine to build air pressure to 120-140 PSI.\"", 19, FontStyle.Italic, new Color(0.99f, 0.94f, 0.54f), TextAnchor.MiddleCenter, defaultFont);
            SetStretch(txtVerbalScript.rectTransform, 6f);

            // Line 3: Bottom Controls Row (local Y = -140f, height 50px)
            Text txtTimer = CreateUIText(dialogCardGO.transform, "TimerText", "⏱️ TIMER: 00.0s / 60s", 19, FontStyle.Bold, new Color(0.29f, 0.87f, 0.50f), TextAnchor.MiddleLeft, defaultFont);
            SetRect(txtTimer.rectTransform, -340f, -140f, 280f, 46f);

            GameObject btnFastFwdGO = CreateUIButton(dialogCardGO.transform, "BtnFastForward", "⏩ FAST FORWARD 60s >>", new Color(0.65f, 0.45f, 0.08f), defaultFont, 18);
            SetRect(btnFastFwdGO.GetComponent<RectTransform>(), 20f, -140f, 360f, 46f);
            Button btnFastForward = btnFastFwdGO.GetComponent<Button>();

            GameObject toggleFaultGO = CreateUIToggle(dialogCardGO.transform, "ToggleFaultLeak", "Simulate Leak (>4 PSI)", defaultFont);
            SetRect(toggleFaultGO.GetComponent<RectTransform>(), 390f, -140f, 210f, 46f);
            Toggle toggleFaultLeak = toggleFaultGO.GetComponent<Toggle>();

            // =========================================================================
            // 6. MODALS & THEME / UI CONTROLLER WIRING
            // =========================================================================
            GameObject failModalGO = CreateModalDialog(canvasGO.transform, "FailureModal", new Color(0.5f, 0.1f, 0.1f, 0.98f), "DMV TEST FAILED", defaultFont,
                out Text failReasonTxt, out Text failRegTxt, out Button retryBtn, out Button restartBtn);

            GameObject passModalGO = CreateSuccessDialog(canvasGO.transform, "SuccessModal", new Color(0.1f, 0.4f, 0.2f, 0.98f), "CDL AIR BRAKE TEST PASSED!", defaultFont,
                out Text scoreTxt, out Button againBtn);

            failModalGO.SetActive(false);
            passModalGO.SetActive(false);

            // Attach interactive highlighters to controls
            UIElementHighlighter hlOff = Undo.AddComponent<UIElementHighlighter>(btnOffGO);
            hlOff.Setup(HighlightTarget.IgnitionOff);

            UIElementHighlighter hlAcc = Undo.AddComponent<UIElementHighlighter>(btnAccGO);
            hlAcc.Setup(HighlightTarget.IgnitionAcc);

            UIElementHighlighter hlRun = Undo.AddComponent<UIElementHighlighter>(btnRunGO);
            hlRun.Setup(HighlightTarget.IgnitionRun);

            UIElementHighlighter hlRev = Undo.AddComponent<UIElementHighlighter>(btnRevGO);
            hlRev.Setup(HighlightTarget.RevEngine);

            UIElementHighlighter hlYellow = Undo.AddComponent<UIElementHighlighter>(btnYellow.gameObject);
            hlYellow.Setup(HighlightTarget.YellowValve, new Color(0.95f, 0.85f, 0.1f));

            UIElementHighlighter hlRed = Undo.AddComponent<UIElementHighlighter>(btnRed.gameObject);
            hlRed.Setup(HighlightTarget.RedValve, new Color(0.95f, 0.35f, 0.35f));

            UIElementHighlighter hlHold = Undo.AddComponent<UIElementHighlighter>(btnHoldPedalGO);
            hlHold.Setup(HighlightTarget.HoldPedal);

            UIElementHighlighter hlFan = Undo.AddComponent<UIElementHighlighter>(btnFanGO);
            hlFan.Setup(HighlightTarget.FanPedal, new Color(0.98f, 0.45f, 0.2f));

            UIElementHighlighter hlFast = Undo.AddComponent<UIElementHighlighter>(btnFastFwdGO);
            hlFast.Setup(HighlightTarget.FastForward);

            // Cabin Theme Manager
            CabinThemeManager themeManager = Undo.AddComponent<CabinThemeManager>(canvasGO);
            themeManager.BindUIElements(
                imgDashboardBackground,
                imgPrimaryDial, imgSecondaryDial,
                imgPrimaryNeedle, imgSecondaryNeedle,
                imgYellowKnob, imgRedKnob,
                warningIcon,
                txtTruckModelName, txtTruckSubtitle,
                btnPrevTruck, btnNextTruck,
                truckPresets
            );

            // Connect Master AirBrakeUIController
            AirBrakeUIController uiCtrl = Undo.AddComponent<AirBrakeUIController>(canvasGO);
            uiCtrl.SetThemeManager(themeManager);
            uiCtrl.BindComponents(
                airSystem, dmvManager, interactivePedal,
                btnOffGO.GetComponent<Button>(), btnAccGO.GetComponent<Button>(), btnRunGO.GetComponent<Button>(),
                btnOffGO.GetComponent<Image>(), btnAccGO.GetComponent<Image>(), btnRunGO.GetComponent<Image>(),
                btnRevGO.GetComponent<Button>(), txtRevEngine,
                btnYellow, yellowRect, txtYellow,
                btnRed, redRect, txtRed,
                btnFanGO.GetComponent<Button>(),
                warningPanelGO, warningIcon, warningText,
                txtStepHeader, txtInstructions, txtVerbalScript, txtTimer,
                btnFastForward, toggleFaultLeak,
                failModalGO, failReasonTxt, failRegTxt, retryBtn, restartBtn,
                passModalGO, scoreTxt, againBtn,
                btnStartNewGame,
                txtRpmCounter
            );

            uiCtrl.BindExaminerCollapse(
                dialogCardGO,
                minBarGO,
                btnToggleDMV,
                txtToggleDMV,
                btnHideCard,
                btnShowCard,
                btnHeaderDismiss
            );

            uiCtrl.BindModeSwitcher(
                btnModeTraining, imgModeTrainingBg, txtModeTraining,
                btnModeExam, imgModeExamBg, txtModeExam
            );

            uiCtrl.BindTrainingCards(
                nextActionBoxGO, txtNextAction,
                whyWeDoThisBoxGO, txtWhyWeDoThis
            );

            uiCtrl.BindHighlighters(
                hlOff, hlAcc, hlRun, hlRev,
                hlYellow, hlRed, hlHold, hlFan, hlFast
            );

            // =========================================================================
            // 6. TUTORIAL / TRAINING GUIDANCE CARD & HIGHLIGHT OVERLAY
            // =========================================================================
            GameObject tutorialCardGO = CreateUIPanel(bgGO.transform, "TutorialGuidanceCard", new Color(0.08f, 0.11f, 0.16f, 0.98f));
            SetAnchorBottom(tutorialCardGO.GetComponent<RectTransform>(), 0f, 440f, 0f);

            GameObject tutHeaderGO = CreateUIPanel(tutorialCardGO.transform, "TutorialCardHeader", new Color(0.12f, 0.16f, 0.22f, 0.98f));
            SetRect(tutHeaderGO.GetComponent<RectTransform>(), 0f, 185f, 1000f, 42f);

            Text txtTutMainTitle = CreateUIText(tutHeaderGO.transform, "TutMainTitle", "🎓 УЧЕБНЫЙ РЕЖИМ (CDL TRAINING GUIDE)", 18, FontStyle.Bold, new Color(0.98f, 0.85f, 0.2f), TextAnchor.MiddleLeft, defaultFont);
            SetRect(txtTutMainTitle.rectTransform, -130f, 0f, 720f, 34f);

            GameObject btnCloseTutGO = CreateUIButton(tutHeaderGO.transform, "BtnCloseTutorial", "✕ ВЫЙТИ ИЗ ОБУЧЕНИЯ", new Color(0.55f, 0.18f, 0.18f), defaultFont, 15);
            SetRect(btnCloseTutGO.GetComponent<RectTransform>(), 390f, 0f, 220f, 34f);
            Button btnCloseTut = btnCloseTutGO.GetComponent<Button>();

            Text txtTutStepTitle = CreateUIText(tutorialCardGO.transform, "TutStepTitle", "ШАГ 1/5: ПРОВЕРКА ОТСЕЧКИ КОМПРЕССОРА", 18, FontStyle.Bold, new Color(0.35f, 0.85f, 1f), TextAnchor.MiddleLeft, defaultFont);
            SetRect(txtTutStepTitle.rectTransform, 0f, 135f, 960f, 32f);

            Text txtTutHow = CreateUIText(tutorialCardGO.transform, "TutHowAction", "▶ ДЕЙСТВИЕ: Нажмите подсвеченную кнопку [RUNNING] (клавиша I).", 18, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, defaultFont);
            SetRect(txtTutHow.rectTransform, 0f, 80f, 960f, 36f);

            Text txtTutWhy = CreateUIText(tutorialCardGO.transform, "TutWhyPurpose", "💡 ЗАЧЕМ: Чтобы компрессор начал нагнетать воздух в систему, двигатель должен быть запущен.", 16, FontStyle.Normal, new Color(0.78f, 0.86f, 0.95f), TextAnchor.MiddleLeft, defaultFont);
            SetRect(txtTutWhy.rectTransform, 0f, 20f, 960f, 44f);

            Text txtTutCriteria = CreateUIText(tutorialCardGO.transform, "TutCriteria", "✔ НОРМАТИВ: Давление поднимется до 120–140 PSI со звуком сброса воздуха (пшик).", 16, FontStyle.Normal, new Color(0.4f, 0.95f, 0.55f), TextAnchor.MiddleLeft, defaultFont);
            SetRect(txtTutCriteria.rectTransform, 0f, -40f, 960f, 36f);

            tutorialCardGO.SetActive(false);

            // Highlight overlay box on top of canvas (kept strictly non-blocking)
            GameObject highlightBoxGO = CreateUIPanel(canvasGO.transform, "TutorialHighlightBox", new Color(0.98f, 0.85f, 0.2f, 0.15f));
            RectTransform highlightRect = highlightBoxGO.GetComponent<RectTransform>();
            Image highlightImg = highlightBoxGO.GetComponent<Image>();
            highlightImg.raycastTarget = false;

            CanvasGroup highlightCg = Undo.AddComponent<CanvasGroup>(highlightBoxGO);
            highlightCg.blocksRaycasts = false;
            highlightCg.interactable = false;

            Outline highlightOutline = Undo.AddComponent<Outline>(highlightBoxGO);
            highlightOutline.effectColor = new Color(0.98f, 0.85f, 0.2f, 0.95f);
            highlightOutline.effectDistance = new Vector2(3f, -3f);

            Text highlightBadge = CreateUIText(highlightBoxGO.transform, "HighlightBadge", "👉 НАЖМИТЕ ЗДЕСЬ (CLICK)", 16, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, defaultFont);
            highlightBadge.raycastTarget = false;
            SetRect(highlightBadge.rectTransform, 0f, 42f, 360f, 32f);
            Outline badgeOutline = Undo.AddComponent<Outline>(highlightBadge.gameObject);
            badgeOutline.effectColor = Color.black;
            badgeOutline.effectDistance = new Vector2(2f, -2f);

            highlightBoxGO.SetActive(false);

            // Bind Tutorial Guide Controller
            TutorialGuideController tutorialCtrl = Undo.AddComponent<TutorialGuideController>(canvasGO);
            tutorialCtrl.BindTargetReferences(
                btnOffGO.GetComponent<RectTransform>(),
                btnAccGO.GetComponent<RectTransform>(),
                btnRunGO.GetComponent<RectTransform>(),
                btnRevGO.GetComponent<RectTransform>(),
                yellowRect,
                redRect,
                btnHoldPedalGO.GetComponent<RectTransform>(),
                btnFanGO.GetComponent<RectTransform>(),
                btnFastPedalGO.GetComponent<RectTransform>(),
                retryBtn.GetComponent<RectTransform>(),
                btnStartNewGame.GetComponent<RectTransform>()
            );

            tutorialCtrl.BindUIElements(
                btnModeTraining,
                txtModeTraining,
                tutorialCardGO,
                txtTutStepTitle,
                txtTutHow,
                txtTutWhy,
                txtTutCriteria,
                btnCloseTut,
                highlightBoxGO,
                highlightRect,
                highlightImg,
                highlightBadge,
                highlightOutline
            );

            uiCtrl.SetTutorialGuide(tutorialCtrl);

            // Mark Scene Dirty and Save
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(groupIndex);

            Debug.Log("<color=#50fa7b><b>[CDL Air Brake Simulator] Complete 5-Section Mobile UI Hierarchy Built & Auto-Wired!</b></color>");
        }

        #region ScriptableObject Presets Generation

        private static List<TruckCabinConfig> EnsurePresetAssets()
        {
            string presetDir = "Assets/Presets";
            if (!Directory.Exists(presetDir)) Directory.CreateDirectory(presetDir);

            List<TruckCabinConfig> list = new List<TruckCabinConfig>();

            // Preset 1: Peterbilt 389 Classic
            string petePath = "Assets/Presets/Peterbilt389_Classic.asset";
            TruckCabinConfig pete = AssetDatabase.LoadAssetAtPath<TruckCabinConfig>(petePath);
            if (pete == null)
            {
                pete = ScriptableObject.CreateInstance<TruckCabinConfig>();
                pete.truckName = "Peterbilt 389";
                pete.truckSubtitle = "Classic American Conventional (Woodgrain & Chrome)";
                pete.dashboardBackground = EnsureSpriteAsset("Assets/Sprites/Dashboard_Woodgrain.png", () => ProceduralSpriteGenerator.CreateWoodgrainDashboardTexture(256, 256));
                pete.primaryGaugeDial = EnsureSpriteAsset("Assets/Sprites/GaugeDial_Classic.png", () => ProceduralSpriteGenerator.CreateGaugeDialTexture(512, false));
                pete.secondaryGaugeDial = pete.primaryGaugeDial;
                pete.needleSprite = EnsureSpriteAsset("Assets/Sprites/GaugeNeedle_Classic.png", () => ProceduralSpriteGenerator.CreateNeedleTexture(48, 256, false), new Vector2(0.5f, 0.15f));
                pete.needleColor = new Color(0.95f, 0.25f, 0.15f, 1f);
                pete.yellowKnobPushed = EnsureSpriteAsset("Assets/Sprites/YellowKnob_Classic_Pushed.png", () => ProceduralSpriteGenerator.CreateYellowDiamondTexture(256, true, false));
                pete.yellowKnobPopped = EnsureSpriteAsset("Assets/Sprites/YellowKnob_Classic_Popped.png", () => ProceduralSpriteGenerator.CreateYellowDiamondTexture(256, false, false));
                pete.redKnobPushed = EnsureSpriteAsset("Assets/Sprites/RedKnob_Classic_Pushed.png", () => ProceduralSpriteGenerator.CreateRedOctagonTexture(256, true, false));
                pete.redKnobPopped = EnsureSpriteAsset("Assets/Sprites/RedKnob_Classic_Popped.png", () => ProceduralSpriteGenerator.CreateRedOctagonTexture(256, false, false));
                pete.warningLightColor = new Color(1.0f, 0.2f, 0.2f, 1f);
                pete.lowAirBuzzerSound = AirBrakeAudio.CreateProceduralMechanicalBuzzerClip();
                pete.accentColor = new Color(0.95f, 0.8f, 0.1f, 1f);
                pete.headerColor = new Color(0.14f, 0.11f, 0.09f, 1f);
                AssetDatabase.CreateAsset(pete, petePath);
            }
            list.Add(pete);

            // Preset 2: Freightliner Cascadia Modern
            string cascadiaPath = "Assets/Presets/FreightlinerCascadia_Modern.asset";
            TruckCabinConfig cascadia = AssetDatabase.LoadAssetAtPath<TruckCabinConfig>(cascadiaPath);
            if (cascadia == null)
            {
                cascadia = ScriptableObject.CreateInstance<TruckCabinConfig>();
                cascadia.truckName = "Freightliner Cascadia";
                cascadia.truckSubtitle = "Modern Aerodynamic Highway (Digital & Carbon Fiber)";
                cascadia.dashboardBackground = EnsureSpriteAsset("Assets/Sprites/Dashboard_Carbon.png", () => ProceduralSpriteGenerator.CreateCarbonFiberDashboardTexture(256, 256));
                cascadia.primaryGaugeDial = EnsureSpriteAsset("Assets/Sprites/GaugeDial_Modern.png", () => ProceduralSpriteGenerator.CreateGaugeDialTexture(512, true));
                cascadia.secondaryGaugeDial = cascadia.primaryGaugeDial;
                cascadia.needleSprite = EnsureSpriteAsset("Assets/Sprites/GaugeNeedle_Modern.png", () => ProceduralSpriteGenerator.CreateNeedleTexture(48, 256, true), new Vector2(0.5f, 0.15f));
                cascadia.needleColor = new Color(0.1f, 0.85f, 1.0f, 1f); // Neon Cyan Needle
                cascadia.yellowKnobPushed = EnsureSpriteAsset("Assets/Sprites/YellowKnob_Modern_Pushed.png", () => ProceduralSpriteGenerator.CreateYellowDiamondTexture(256, true, true));
                cascadia.yellowKnobPopped = EnsureSpriteAsset("Assets/Sprites/YellowKnob_Modern_Popped.png", () => ProceduralSpriteGenerator.CreateYellowDiamondTexture(256, false, true));
                cascadia.redKnobPushed = EnsureSpriteAsset("Assets/Sprites/RedKnob_Modern_Pushed.png", () => ProceduralSpriteGenerator.CreateRedOctagonTexture(256, true, true));
                cascadia.redKnobPopped = EnsureSpriteAsset("Assets/Sprites/RedKnob_Modern_Popped.png", () => ProceduralSpriteGenerator.CreateRedOctagonTexture(256, false, true));
                cascadia.warningLightColor = new Color(1.0f, 0.35f, 0.05f, 1f);
                cascadia.lowAirBuzzerSound = AirBrakeAudio.CreateProceduralModernChimeClip();
                cascadia.accentColor = new Color(0.2f, 0.8f, 1.0f, 1f);
                cascadia.headerColor = new Color(0.10f, 0.13f, 0.18f, 1f);
                AssetDatabase.CreateAsset(cascadia, cascadiaPath);
            }
            list.Add(cascadia);

            AssetDatabase.SaveAssets();
            return list;
        }

        #endregion

        #region UI Construction Helpers

        private static GameObject CreateUIPanel(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.color = color;
            if (color.a <= 0.001f) img.raycastTarget = false;
            return go;
        }

        private static Text CreateUIText(Transform parent, string name, string text, int fontSize, FontStyle style, Color color, TextAnchor alignment, Font font)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text t = go.GetComponent<Text>();
            t.text = text;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = color;
            t.alignment = alignment;
            t.font = font;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static GameObject CreateUIButton(Transform parent, string name, string label, Color bgColor, Font font, int fontSize = 20)
        {
            GameObject go = CreateUIPanel(parent, name, bgColor);
            Button btn = go.AddComponent<Button>();

            ColorBlock cb = btn.colors;
            cb.normalColor = bgColor;
            cb.highlightedColor = Color.Lerp(bgColor, Color.white, 0.2f);
            cb.pressedColor = Color.Lerp(bgColor, Color.black, 0.3f);
            btn.colors = cb;

            Text txt = CreateUIText(go.transform, "BtnText", label, fontSize, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, font);
            txt.raycastTarget = false;
            SetStretch(txt.rectTransform);

            return go;
        }

        private static GameObject CreateUIToggle(Transform parent, string name, string label, Font font)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            go.transform.SetParent(parent, false);
            Toggle toggle = go.GetComponent<Toggle>();

            GameObject bg = CreateUIPanel(go.transform, "Background", new Color(0.25f, 0.28f, 0.32f));
            SetRect(bg.GetComponent<RectTransform>(), -90f, 0f, 32f, 32f);

            GameObject check = CreateUIPanel(bg.transform, "Checkmark", new Color(0.2f, 0.9f, 0.4f));
            SetRect(check.GetComponent<RectTransform>(), 0f, 0f, 22f, 22f);

            Text txt = CreateUIText(go.transform, "Label", label, 18, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, font);
            SetRect(txt.rectTransform, 40f, 0f, 180f, 32f);

            toggle.targetGraphic = bg.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.isOn = false;

            return go;
        }

        private static void CreateGaugeVisual(Transform parent, string name, float posX, float posY,
            Sprite dialSprite, Sprite needleSprite, Font font, string title,
            out RectTransform needleRect, out Text psiText, out Image dialImg, out Image needleImg)
        {
            GameObject gaugeRoot = CreateUIPanel(parent, name, Color.clear);
            SetRect(gaugeRoot.GetComponent<RectTransform>(), posX, posY, 400f, 430f);

            Text txtTitle = CreateUIText(gaugeRoot.transform, "GaugeTitle", title, 18, FontStyle.Bold, new Color(0.90f, 0.92f, 0.95f), TextAnchor.MiddleCenter, font);
            SetRect(txtTitle.rectTransform, 0f, 195f, 380f, 26f);

            GameObject dialGO = CreateUIPanel(gaugeRoot.transform, "DialFace", Color.white);
            SetRect(dialGO.GetComponent<RectTransform>(), 0f, 0f, 370f, 370f);
            dialImg = dialGO.GetComponent<Image>();
            dialImg.sprite = dialSprite;
            dialImg.preserveAspect = true;
            AspectRatioFitter dialFitter = dialGO.AddComponent<AspectRatioFitter>();
            dialFitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            dialFitter.aspectRatio = 1f;

            // Scale numbers on dial face (with "100" and "PSI" slightly offset so vertical needles at ~90-100 PSI never obscure them)
            Text lbl50 = CreateUIText(dialGO.transform, "Scale50", "50", 16, FontStyle.Bold, new Color(0.92f, 0.95f, 0.98f), TextAnchor.MiddleCenter, font);
            SetRect(lbl50.rectTransform, -96f, 44f, 44f, 24f);
            Text lbl100 = CreateUIText(dialGO.transform, "Scale100", "100", 16, FontStyle.Bold, new Color(0.92f, 0.95f, 0.98f), TextAnchor.MiddleCenter, font);
            SetRect(lbl100.rectTransform, 58f, 92f, 48f, 24f);
            Text lbl150 = CreateUIText(dialGO.transform, "Scale150", "150", 16, FontStyle.Bold, new Color(0.92f, 0.95f, 0.98f), TextAnchor.MiddleCenter, font);
            SetRect(lbl150.rectTransform, 92f, -68f, 48f, 24f);
            Text lblPsiUnit = CreateUIText(dialGO.transform, "ScalePSIUnit", "AIR • PSI", 13, FontStyle.Bold, new Color(0.68f, 0.76f, 0.85f), TextAnchor.MiddleCenter, font);
            SetRect(lblPsiUnit.rectTransform, 0f, -28f, 90f, 20f);

            GameObject needleGO = CreateUIPanel(gaugeRoot.transform, "Needle", Color.white);
            needleRect = needleGO.GetComponent<RectTransform>();
            needleRect.pivot = new Vector2(0.5f, 0.15f);
            SetRect(needleRect, 0f, 0f, 26f, 185f);
            needleImg = needleGO.GetComponent<Image>();
            needleImg.sprite = needleSprite;
            needleImg.preserveAspect = true;

            // Center Pivot Cap sitting strictly on top of needles in sorting order
            GameObject pivotCapGO = CreateUIPanel(gaugeRoot.transform, "CenterPivotCap", new Color(0.16f, 0.19f, 0.24f, 1f));
            SetRect(pivotCapGO.GetComponent<RectTransform>(), 0f, 0f, 30f, 30f);
            pivotCapGO.transform.SetAsLastSibling();

            // Digital PSI Readout cleanly embedded in lower center of dial (y = -68f)
            GameObject readoutBox = CreateUIPanel(gaugeRoot.transform, "ReadoutBox", new Color(0.05f, 0.07f, 0.10f, 0.92f));
            SetRect(readoutBox.GetComponent<RectTransform>(), 0f, -68f, 170f, 48f);

            psiText = CreateUIText(readoutBox.transform, "DigitalPSI", "125 PSI", 34, FontStyle.Bold, new Color(0.2f, 0.95f, 0.45f), TextAnchor.MiddleCenter, font);
            SetStretch(psiText.rectTransform, 2f);
        }

        private static void CreateCornerScrew(Transform parent, string name, float posX, float posY, Font font)
        {
            GameObject screwGO = CreateUIPanel(parent, name, new Color(0.26f, 0.32f, 0.40f, 1f));
            SetRect(screwGO.GetComponent<RectTransform>(), posX, posY, 18f, 18f);
            Text crossTxt = CreateUIText(screwGO.transform, "Slot", "+", 13, FontStyle.Bold, new Color(0.08f, 0.10f, 0.14f, 1f), TextAnchor.MiddleCenter, font);
            SetStretch(crossTxt.rectTransform, 0f);
        }

        private static void CreateValveKnob(Transform parent, string name, float posX, float posY,
            Sprite knobSprite, Font font, string title, string defaultStatus,
            out Button btn, out RectTransform knobRect, out Text txtStatus, out Image knobImg)
        {
            GameObject root = CreateUIPanel(parent, name, Color.clear);
            SetRect(root.GetComponent<RectTransform>(), posX, posY, 380f, 252f);

            LayoutElement colLayout = root.AddComponent<LayoutElement>();
            colLayout.preferredWidth = 380f;
            colLayout.preferredHeight = 252f;
            colLayout.minWidth = 320f;
            colLayout.minHeight = 252f;
            colLayout.flexibleWidth = 0f;
            colLayout.flexibleHeight = 0f;

            VerticalLayoutGroup vlg = root.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 12f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            // a) Top Label Pill
            GameObject titlePillGO = CreateUIPanel(root.transform, "TopLabelPill", new Color(0.08f, 0.10f, 0.14f, 0.96f));
            SetRect(titlePillGO.GetComponent<RectTransform>(), 0f, 92f, 320f, 28f);
            LayoutElement topPillLayout = titlePillGO.AddComponent<LayoutElement>();
            topPillLayout.preferredWidth = 320f;
            topPillLayout.preferredHeight = 28f;
            topPillLayout.minWidth = 260f;
            topPillLayout.minHeight = 28f;
            topPillLayout.flexibleWidth = 0f;
            topPillLayout.flexibleHeight = 0f;
            Text txtTitle = CreateUIText(titlePillGO.transform, "Title", title, 16, FontStyle.Bold, new Color(0.95f, 0.85f, 0.35f), TextAnchor.MiddleCenter, font);
            SetStretch(txtTitle.rectTransform, 2f);

            // b) 3D-styled Octagon/Diamond Valve Button (LayoutElement forced exact 180x180 px, non-stretchable, preserveAspect = true)
            GameObject knobGO = CreateUIPanel(root.transform, "KnobButton", Color.white);
            knobRect = knobGO.GetComponent<RectTransform>();
            SetRect(knobRect, 0f, 0f, 180f, 180f);
            LayoutElement knobLayout = knobGO.AddComponent<LayoutElement>();
            knobLayout.preferredWidth = 180f;
            knobLayout.preferredHeight = 180f;
            knobLayout.minWidth = 180f;
            knobLayout.minHeight = 180f;
            knobLayout.flexibleWidth = 0f;
            knobLayout.flexibleHeight = 0f;

            knobImg = knobGO.GetComponent<Image>();
            knobImg.sprite = knobSprite;
            knobImg.preserveAspect = true;
            btn = knobGO.AddComponent<Button>();

            // c) Bottom Status Badge
            GameObject statusPillGO = CreateUIPanel(root.transform, "BottomStatusBadge", new Color(0.06f, 0.08f, 0.12f, 0.96f));
            SetRect(statusPillGO.GetComponent<RectTransform>(), 0f, -92f, 320f, 28f);
            LayoutElement botPillLayout = statusPillGO.AddComponent<LayoutElement>();
            botPillLayout.preferredWidth = 320f;
            botPillLayout.preferredHeight = 28f;
            botPillLayout.minWidth = 260f;
            botPillLayout.minHeight = 28f;
            botPillLayout.flexibleWidth = 0f;
            botPillLayout.flexibleHeight = 0f;
            txtStatus = CreateUIText(statusPillGO.transform, "Status", defaultStatus, 15, FontStyle.Bold, new Color(0.95f, 0.35f, 0.35f), TextAnchor.MiddleCenter, font);
            SetStretch(txtStatus.rectTransform, 2f);
        }

        private static GameObject CreateModalDialog(Transform parent, string name, Color headerCol, string title, Font font, out Text txtReason, out Text txtReg, out Button btnRetry, out Button btnRestart)
        {
            GameObject backdrop = CreateUIPanel(parent, name, new Color(0f, 0f, 0f, 0.88f));
            SetStretch(backdrop.GetComponent<RectTransform>());

            GameObject dialog = CreateUIPanel(backdrop.transform, "DialogBox", new Color(0.14f, 0.16f, 0.19f, 1f));
            SetRect(dialog.GetComponent<RectTransform>(), 0f, 0f, 920f, 640f);

            GameObject header = CreateUIPanel(dialog.transform, "Header", headerCol);
            SetRect(header.GetComponent<RectTransform>(), 0f, 270f, 920f, 100f);

            Text txtHeader = CreateUIText(header.transform, "Title", title, 36, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, font);
            SetStretch(txtHeader.rectTransform);

            txtReason = CreateUIText(dialog.transform, "FailureReason", "Reason for test failure.", 24, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter, font);
            SetRect(txtReason.rectTransform, 0f, 110f, 840f, 160f);

            txtReg = CreateUIText(dialog.transform, "RegulationText", "FMCSA Regulation: § 396.11", 20, FontStyle.Italic, new Color(0.9f, 0.75f, 0.2f), TextAnchor.MiddleCenter, font);
            SetRect(txtReg.rectTransform, 0f, -10f, 840f, 50f);

            GameObject btnRetryGO = CreateUIButton(dialog.transform, "BtnRetryStep", "RETRY THIS STEP", new Color(0.2f, 0.6f, 0.35f), font, 24);
            SetRect(btnRetryGO.GetComponent<RectTransform>(), 0f, -110f, 600f, 75f);
            btnRetry = btnRetryGO.GetComponent<Button>();

            GameObject btnRestartGO = CreateUIButton(dialog.transform, "BtnRestartFull", "RESTART ENTIRE TEST", new Color(0.5f, 0.2f, 0.2f), font, 22);
            SetRect(btnRestartGO.GetComponent<RectTransform>(), 0f, -210f, 600f, 75f);
            btnRestart = btnRestartGO.GetComponent<Button>();

            backdrop.SetActive(false);
            return backdrop;
        }

        private static GameObject CreateSuccessDialog(Transform parent, string name, Color headerCol, string title, Font font, out Text txtScorecard, out Button btnAgain)
        {
            GameObject backdrop = CreateUIPanel(parent, name, new Color(0f, 0f, 0f, 0.9f));
            SetStretch(backdrop.GetComponent<RectTransform>());

            GameObject dialog = CreateUIPanel(backdrop.transform, "DialogBox", new Color(0.14f, 0.16f, 0.19f, 1f));
            SetRect(dialog.GetComponent<RectTransform>(), 0f, 0f, 960f, 840f);

            GameObject header = CreateUIPanel(dialog.transform, "Header", headerCol);
            SetRect(header.GetComponent<RectTransform>(), 0f, 370f, 960f, 100f);

            Text txtHeader = CreateUIText(header.transform, "Title", title, 34, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, font);
            SetStretch(txtHeader.rectTransform);

            txtScorecard = CreateUIText(dialog.transform, "ScorecardText", "Scorecard summary here...", 22, FontStyle.Normal, Color.white, TextAnchor.UpperLeft, font);
            SetRect(txtScorecard.rectTransform, 0f, 60f, 860f, 440f);

            GameObject btnAgainGO = CreateUIButton(dialog.transform, "BtnInspectAgain", "PRACTICE AGAIN", new Color(0.2f, 0.65f, 0.35f), font, 24);
            SetRect(btnAgainGO.GetComponent<RectTransform>(), 0f, -320f, 600f, 80f);
            btnAgain = btnAgainGO.GetComponent<Button>();

            backdrop.SetActive(false);
            return backdrop;
        }

        private static void EnsureEventSystem()
        {
            EventSystem es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                GameObject esGO = new GameObject("EventSystem", typeof(EventSystem));
                Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
                es = esGO.GetComponent<EventSystem>();
            }

            var inputModule = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (inputModule == null)
            {
                var legacy = es.GetComponent<StandaloneInputModule>();
                if (legacy != null) Undo.DestroyObjectImmediate(legacy);

                Undo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(es.gameObject);
            }
        }

        private static Sprite EnsureSpriteAsset(string path, System.Func<Texture2D> textureFactory, Vector2? pivot = null)
        {
            if (File.Exists(path))
            {
                Sprite loaded = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (loaded != null) return loaded;
            }

            Texture2D tex = textureFactory();
            byte[] bytes = tex.EncodeToPNG();
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                if (pivot.HasValue)
                {
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = pivot.Value;
                }
                importer.SetTextureSettings(settings);
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Font GetDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }

        private static void SetStretch(RectTransform rt, float padding = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        private static void SetAnchorTop(RectTransform rt, float topOffset, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -topOffset - height);
            rt.offsetMax = new Vector2(0f, -topOffset);
        }

        private static void SetAnchorBottom(RectTransform rt, float bottomOffset, float height, float horizontalMargin = 0f)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(horizontalMargin, bottomOffset);
            rt.offsetMax = new Vector2(-horizontalMargin, bottomOffset + height);
        }

        private static void SetRect(RectTransform rt, float anchoredX, float anchoredY, float width, float height)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(anchoredX, anchoredY);
            rt.sizeDelta = new Vector2(width, height);
        }

        #endregion
    }
}
#endif
