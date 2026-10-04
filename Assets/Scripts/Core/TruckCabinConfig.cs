using UnityEngine;

namespace CDL.AirBrakeSimulator
{
    /// <summary>
    /// ScriptableObject defining visual and auditory cabin assets for a specific truck model.
    /// Allows seamless multi-truck skin switching (e.g., Peterbilt 389 Classic vs Freightliner Cascadia Modern)
    /// without modifying underlying air physics or DMV examination logic.
    /// </summary>
    [CreateAssetMenu(fileName = "NewTruckCabinConfig", menuName = "CDL Simulator/Truck Cabin Config", order = 10)]
    public class TruckCabinConfig : ScriptableObject
    {
        [Header("Truck Identification")]
        [Tooltip("Display name of the truck model (e.g., 'Peterbilt 389', 'Freightliner Cascadia').")]
        public string truckName = "Peterbilt 389";

        [Tooltip("Cabin subtitle / generation description.")]
        public string truckSubtitle = "Classic Conventional (Woodgrain & Chrome)";

        [Header("Dashboard Architecture")]
        [Tooltip("Background texture for the in-cab dashboard panel.")]
        public Sprite dashboardBackground;

        [Tooltip("Primary Air Gauge Dial face sprite.")]
        public Sprite primaryGaugeDial;

        [Tooltip("Secondary Air Gauge Dial face sprite.")]
        public Sprite secondaryGaugeDial;

        [Tooltip("Pointer needle sprite for gauge cluster.")]
        public Sprite needleSprite;

        [Tooltip("Color tint for the needle pointers.")]
        public Color needleColor = new Color(0.95f, 0.35f, 0.1f, 1f);

        [Header("Push-Pull Valve Knob Skins")]
        [Tooltip("Yellow Diamond Parking Brake Knob when PUSHED IN (Brakes Released).")]
        public Sprite yellowKnobPushed;

        [Tooltip("Yellow Diamond Parking Brake Knob when POPPED OUT (Brakes Applied).")]
        public Sprite yellowKnobPopped;

        [Tooltip("Red Octagon Trailer Supply Knob when PUSHED IN (Trailer Supplied).")]
        public Sprite redKnobPushed;

        [Tooltip("Red Octagon Trailer Supply Knob when POPPED OUT (Trailer Emergency Locked).")]
        public Sprite redKnobPopped;

        [Header("Visual Alarm Annunciator")]
        [Tooltip("Color tint for the Low Air Warning illuminator lamp.")]
        public Color warningLightColor = Color.red;

        [Header("Audio Tone")]
        [Tooltip("Audio clip played when low air warning buzzer is active (e.g., harsh mechanical buzzer vs modern digital chime).")]
        public AudioClip lowAirBuzzerSound;

        [Header("Styling Colors")]
        [Tooltip("Main accent highlight color for buttons and card borders.")]
        public Color accentColor = new Color(0.95f, 0.8f, 0.1f, 1f);

        [Tooltip("Header bar background color.")]
        public Color headerColor = new Color(0.12f, 0.14f, 0.17f, 1f);
    }
}
