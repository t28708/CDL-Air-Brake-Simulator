using System.Collections;
using UnityEngine;

namespace CDL.AirBrakeSimulator
{
    /// <summary>
    /// Audio manager for CDL Air Brake Simulator.
    /// Provides procedural audio synthesis for all essential truck cues:
    /// - Low Air Warning Buzzer (piercing tone/pulsing alarm)
    /// - Governor Cut-Out Purge ("Air Sneeze")
    /// - Pedal Exhaust Air Bleed ("Whoosh")
    /// - Emergency Spring Brake Valve Pop ("Clack")
    /// - Engine Starter Cranking & Firing sequence ("Chug-chug-chug-vroom!")
    /// - Engine Shutdown sequence ("Chug... chug... thump-pshhh")
    /// - Ignition Key ON Dashboard Self-Test Chime ("Pip-pip!")
    /// - Continuous Engine Idle / Compressor Rumbling Loop
    /// Also supports user-assigned custom audio clips in the Inspector.
    /// </summary>
    public class AirBrakeAudio : MonoBehaviour
    {
        [Header("Air System Reference")]
        [SerializeField] private AirBrakeSystem airSystem;

        [Header("Pneumatic & Alarm Audio Clips (Optional - procedural if null)")]
        [SerializeField] private AudioClip lowAirBuzzerClip;
        [SerializeField] private AudioClip governorSneezeClip;
        [SerializeField] private AudioClip pedalFanClip;
        [SerializeField] private AudioClip valvePopClip;
        [SerializeField] private AudioClip engineIdleClip;

        [Header("Engine & Ignition Audio Clips (Optional - procedural if null)")]
        [SerializeField] private AudioClip starterCrankClip;
        [SerializeField] private AudioClip engineShutdownClip;
        [SerializeField] private AudioClip ignitionChimeClip;

        [Header("Audio Volumes")]
        [Range(0.01f, 1f)]
        [SerializeField] private float lowAirBuzzerVolume = 0.20f;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource buzzerSource;
        [SerializeField] private AudioSource engineSource;

        private IgnitionState previousIgnitionState = IgnitionState.OFF;
        private Coroutine engineStartFadeCoroutine = null;

        private void Awake()
        {
            if (airSystem == null)
            {
                airSystem = GetComponent<AirBrakeSystem>();
                if (airSystem == null) airSystem = FindFirstObjectByType<AirBrakeSystem>();
            }

            SetupAudioSources();
            EnsureProceduralClips();

            if (airSystem != null)
            {
                previousIgnitionState = airSystem.CurrentIgnition;
            }
        }

        private void SetupAudioSources()
        {
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
            }

            if (buzzerSource == null)
            {
                buzzerSource = gameObject.AddComponent<AudioSource>();
                buzzerSource.loop = true;
                buzzerSource.playOnAwake = false;
            }
            buzzerSource.volume = lowAirBuzzerVolume;

            if (engineSource == null)
            {
                engineSource = gameObject.AddComponent<AudioSource>();
                engineSource.loop = true;
                engineSource.playOnAwake = false;
                engineSource.volume = 0.25f;
            }
        }

        private void EnsureProceduralClips()
        {
            if (lowAirBuzzerClip == null) lowAirBuzzerClip = CreateProceduralBuzzerClip();
            if (governorSneezeClip == null) governorSneezeClip = CreateProceduralSneezeClip();
            if (pedalFanClip == null) pedalFanClip = CreateProceduralAirExhaustClip();
            if (valvePopClip == null) valvePopClip = CreateProceduralValvePopClip();
            if (engineIdleClip == null) engineIdleClip = CreateProceduralEngineClip();

            if (starterCrankClip == null) starterCrankClip = CreateProceduralStarterCrankClip();
            if (engineShutdownClip == null) engineShutdownClip = CreateProceduralEngineShutdownClip();
            if (ignitionChimeClip == null) ignitionChimeClip = CreateProceduralIgnitionChimeClip();

            buzzerSource.clip = lowAirBuzzerClip;
            engineSource.clip = engineIdleClip;
        }

        private void OnEnable()
        {
            if (airSystem == null)
            {
                airSystem = GetComponent<AirBrakeSystem>();
                if (airSystem == null) airSystem = FindFirstObjectByType<AirBrakeSystem>();
            }

            if (airSystem != null)
            {
                airSystem.OnGovernorCutOut += HandleGovernorCutOut;
                airSystem.OnLowAirWarningChanged += HandleLowAirWarningChanged;
                airSystem.OnEmergencyValvePopOut += HandleEmergencyValvePopOut;
                airSystem.OnValveStateChanged += HandleValveStateChanged;
                airSystem.OnPedalFanned += HandlePedalFanned;
                airSystem.OnIgnitionChanged += HandleIgnitionChanged;
                previousIgnitionState = airSystem.CurrentIgnition;
            }
        }

        private void OnDisable()
        {
            if (airSystem != null)
            {
                airSystem.OnGovernorCutOut -= HandleGovernorCutOut;
                airSystem.OnLowAirWarningChanged -= HandleLowAirWarningChanged;
                airSystem.OnEmergencyValvePopOut -= HandleEmergencyValvePopOut;
                airSystem.OnValveStateChanged -= HandleValveStateChanged;
                airSystem.OnPedalFanned -= HandlePedalFanned;
                airSystem.OnIgnitionChanged -= HandleIgnitionChanged;
            }

            if (engineStartFadeCoroutine != null)
            {
                StopCoroutine(engineStartFadeCoroutine);
                engineStartFadeCoroutine = null;
            }
        }

        private void Update()
        {
            if (airSystem != null && engineSource.isPlaying)
            {
                // Modulate engine pitch when revved (1.0x idle to 1.45x fast idle)
                float targetPitch = airSystem.IsEngineRevved ? 1.45f : 1.0f;
                engineSource.pitch = Mathf.Lerp(engineSource.pitch, targetPitch, Time.deltaTime * 5f);
            }
        }

        #region Event Callbacks

        private void HandleGovernorCutOut(float cutOutPressure)
        {
            if (governorSneezeClip != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(governorSneezeClip, 0.95f);
            }
        }

        private void HandleLowAirWarningChanged(bool isActive)
        {
            if (buzzerSource == null) return;

            buzzerSource.volume = lowAirBuzzerVolume;

            if (isActive)
            {
                if (!buzzerSource.isPlaying)
                {
                    buzzerSource.Play();
                }
            }
            else
            {
                if (buzzerSource.isPlaying)
                {
                    buzzerSource.Stop();
                }
            }
        }

        private void HandleEmergencyValvePopOut(ValveType valve, float pressure)
        {
            PlayValvePopSound();
        }

        private void HandleValveStateChanged(ValveType valve, bool pushedIn)
        {
            PlayValvePopSound();
        }

        private void PlayValvePopSound()
        {
            if (valvePopClip != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(valvePopClip, 0.85f);
            }
        }

        private void HandlePedalFanned(float dropAmount)
        {
            if (pedalFanClip != null && sfxSource != null)
            {
                // Slight pitch variation for realistic successive pumps
                sfxSource.pitch = Random.Range(0.92f, 1.08f);
                sfxSource.PlayOneShot(pedalFanClip, 0.8f);
                sfxSource.pitch = 1.0f;
            }
        }

        private void HandleIgnitionChanged(IgnitionState state)
        {
            IgnitionState prevState = previousIgnitionState;
            previousIgnitionState = state;

            if (state == IgnitionState.ACC_ON)
            {
                // 1. Switched from OFF to ACC_ON: play dashboard chime ("пик-пик")
                if (prevState == IgnitionState.OFF)
                {
                    PlayIgnitionChime();
                }
                // 2. Switched from RUNNING to ACC_ON: engine shut down while keeping dash active!
                else if (prevState == IgnitionState.RUNNING)
                {
                    StopEngineWithShutdownSound();
                }
            }
            else if (state == IgnitionState.RUNNING)
            {
                // Switched to RUNNING: start engine with heavy starter crank!
                if (prevState != IgnitionState.RUNNING)
                {
                    StartEngineWithStarterSound();
                }
            }
            else if (state == IgnitionState.OFF)
            {
                // Switched to OFF: if engine was running, play shutdown sound
                if (prevState == IgnitionState.RUNNING)
                {
                    StopEngineWithShutdownSound();
                }
                else
                {
                    if (engineSource != null && engineSource.isPlaying)
                    {
                        engineSource.Stop();
                    }
                }
            }
        }

        private void PlayIgnitionChime()
        {
            if (ignitionChimeClip != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(ignitionChimeClip, 0.90f);
            }
        }

        private void StartEngineWithStarterSound()
        {
            if (engineStartFadeCoroutine != null)
            {
                StopCoroutine(engineStartFadeCoroutine);
                engineStartFadeCoroutine = null;
            }

            // Play starter crank SFX (crank, chug, catch & surge)
            if (starterCrankClip != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(starterCrankClip, 0.95f);
            }

            // Start continuous engine idle loop timed to take over as the engine catches (~1.2s)
            engineStartFadeCoroutine = StartCoroutine(CoEngineStartSequence());
        }

        private IEnumerator CoEngineStartSequence()
        {
            if (engineSource == null) yield break;

            engineSource.volume = 0f;
            engineSource.pitch = 1.0f;
            if (!engineSource.isPlaying) engineSource.Play();

            // Wait until starter cranks and combustion catches (~1.25 seconds)
            float elapsed = 0f;
            while (elapsed < 1.25f)
            {
                if (airSystem != null && airSystem.CurrentIgnition != IgnitionState.RUNNING)
                {
                    // Aborted before engine finished starting
                    engineSource.Stop();
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Smoothly fade idle loop into full target volume over 0.6 seconds
            float fadeDuration = 0.6f;
            float targetVolume = 0.25f;
            float timer = 0f;
            while (timer < fadeDuration)
            {
                if (airSystem != null && airSystem.CurrentIgnition != IgnitionState.RUNNING)
                {
                    engineSource.Stop();
                    yield break;
                }
                timer += Time.deltaTime;
                engineSource.volume = Mathf.Lerp(0f, targetVolume, timer / fadeDuration);
                yield return null;
            }

            engineSource.volume = targetVolume;
            engineStartFadeCoroutine = null;
        }

        private void StopEngineWithShutdownSound()
        {
            if (engineStartFadeCoroutine != null)
            {
                StopCoroutine(engineStartFadeCoroutine);
                engineStartFadeCoroutine = null;
            }

            // Immediately stop continuous idle loop so it doesn't mask the shutdown sound
            if (engineSource != null && engineSource.isPlaying)
            {
                engineSource.Stop();
            }

            // Play heavy diesel engine shutdown audio (deceleration, compression thuds, air sigh)
            if (engineShutdownClip != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(engineShutdownClip, 0.95f);
            }
        }

        #endregion

        #region Public Setters & Theme Switching

        /// <summary>
        /// Swaps the active low air warning buzzer audio clip (e.g. Classic Mechanical Buzzer vs Modern Chime).
        /// </summary>
        public void SetCustomBuzzerClip(AudioClip clip)
        {
            if (clip == null) return;

            lowAirBuzzerClip = clip;
            if (buzzerSource != null)
            {
                bool wasPlaying = buzzerSource.isPlaying;
                buzzerSource.Stop();
                buzzerSource.clip = lowAirBuzzerClip;
                buzzerSource.volume = lowAirBuzzerVolume;
                if (wasPlaying && airSystem != null && airSystem.IsLowAirWarningActive)
                {
                    buzzerSource.Play();
                }
            }
        }

        public float LowAirBuzzerVolume
        {
            get => lowAirBuzzerVolume;
            set
            {
                lowAirBuzzerVolume = Mathf.Clamp01(value);
                if (buzzerSource != null) buzzerSource.volume = lowAirBuzzerVolume;
            }
        }

        public void SetBuzzerVolume(float volume) => LowAirBuzzerVolume = volume;

        public void SetCustomStarterCrankClip(AudioClip clip) => starterCrankClip = clip;
        public void SetCustomEngineShutdownClip(AudioClip clip) => engineShutdownClip = clip;
        public void SetCustomIgnitionChimeClip(AudioClip clip) => ignitionChimeClip = clip;

        #endregion

        #region Procedural Audio Generators (Zero External Asset Dependency)

        private AudioClip CreateProceduralBuzzerClip()
        {
            return CreateProceduralMechanicalBuzzerClip();
        }

        public static AudioClip CreateProceduralMechanicalBuzzerClip()
        {
            int sampleRate = 44100;
            float duration = 0.5f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float freq1 = 760f;
            float freq2 = 820f;
            float chatterFreq = 60f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float pulse = (t < 0.35f) ? 1f : 0f;

                float sin1 = Mathf.Sin(2f * Mathf.PI * freq1 * t);
                float sin2 = Mathf.Sin(2f * Mathf.PI * freq2 * t);
                // Smoothed waveform (blend of pure tone and soft buzz) for a comfortable volume & timbre
                float wave1 = sin1 + 0.25f * Mathf.Sign(sin1);
                float wave2 = sin2 + 0.25f * Mathf.Sign(sin2);
                float chatter = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * chatterFreq * t);

                samples[i] = (wave1 + wave2) * 0.12f * pulse * chatter;
            }

            AudioClip clip = AudioClip.Create("MechanicalBuzzer", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static AudioClip CreateProceduralModernChimeClip()
        {
            int sampleRate = 44100;
            float duration = 0.6f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float freq1 = 659.25f; // E5
            float freq2 = 880.0f;   // A5

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float signal = 0f;

                // Tone 1: 0.0s - 0.22s with exponential decay
                if (t >= 0f && t < 0.22f)
                {
                    float decay1 = Mathf.Exp(-t * 14f);
                    signal += (Mathf.Sin(2f * Mathf.PI * freq1 * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * freq1 * 2f * t)) * decay1;
                }

                // Tone 2: 0.2s - 0.55s with exponential decay
                if (t >= 0.2f && t < 0.55f)
                {
                    float t2 = t - 0.2f;
                    float decay2 = Mathf.Exp(-t2 * 12f);
                    signal += (Mathf.Sin(2f * Mathf.PI * freq2 * t2) + 0.25f * Mathf.Sin(2f * Mathf.PI * freq2 * 2f * t2)) * decay2;
                }

                samples[i] = signal * 0.20f;
            }

            AudioClip clip = AudioClip.Create("ModernDigitalChime", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Procedurally generates a crisp, two-tone electronic cluster confirmation chime ("Pip-pip!")
        /// when turning the ignition key to ACC/ON.
        /// </summary>
        public static AudioClip CreateProceduralIgnitionChimeClip()
        {
            int sampleRate = 44100;
            float duration = 0.45f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            // Tone 1: 1174.66 Hz (D6), Tone 2: 1567.98 Hz (G6)
            float t1Start = 0.02f;
            float t1Dur = 0.12f;
            float freq1 = 1174.66f;

            float t2Start = 0.18f;
            float t2Dur = 0.18f;
            float freq2 = 1567.98f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float s = 0f;

                // Beep 1 ("Pip")
                if (t >= t1Start && t < t1Start + t1Dur)
                {
                    float dt = t - t1Start;
                    float attack = Mathf.Clamp01(dt / 0.006f);
                    float decay = Mathf.Exp(-dt * 22f);
                    float tone = Mathf.Sin(2f * Mathf.PI * freq1 * dt) + 0.22f * Mathf.Sin(2f * Mathf.PI * freq1 * 2f * dt);
                    s += tone * attack * decay * 0.5f;
                }

                // Beep 2 ("Pip")
                if (t >= t2Start && t < t2Start + t2Dur)
                {
                    float dt = t - t2Start;
                    float attack = Mathf.Clamp01(dt / 0.006f);
                    float decay = Mathf.Exp(-dt * 16f);
                    float tone = Mathf.Sin(2f * Mathf.PI * freq2 * dt) + 0.22f * Mathf.Sin(2f * Mathf.PI * freq2 * 2f * dt);
                    s += tone * attack * decay * 0.5f;
                }

                samples[i] = Mathf.Clamp(s, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("IgnitionChimePipPip", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Procedurally generates a heavy diesel truck starter crank & firing surge:
        /// 1. Solenoid contactor engagement clack.
        /// 2. Electric starter motor whine + rhythmic heavy cylinder compression strokes.
        /// 3. Combustion catch, throaty engine firing roar flare-up into idle.
        /// </summary>
        public static AudioClip CreateProceduralStarterCrankClip()
        {
            int sampleRate = 44100;
            float duration = 2.1f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float phaseMotor = 0f;
            float phaseCrank = 0f;
            float phaseCatch = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float s = 0f;

                // Phase 1: Solenoid Click (t: 0.01s - 0.07s)
                if (t >= 0.01f && t < 0.07f)
                {
                    float dt = t - 0.01f;
                    float clickDecay = Mathf.Exp(-dt * 65f);
                    float clickTone = Mathf.Sin(2f * Mathf.PI * 650f * dt) + (Random.value * 2f - 1f) * 0.6f;
                    s += clickTone * clickDecay * 0.7f;
                }

                // Phase 2: Electric Starter Motor Whine + Cranking Compression Strokes (t: 0.04s - 1.40s)
                if (t >= 0.04f && t < 1.40f)
                {
                    float crankEnv = Mathf.Clamp01((t - 0.04f) / 0.15f);
                    if (t > 1.22f) crankEnv *= Mathf.Clamp01(1f - (t - 1.22f) / 0.18f); // fade out as engine catches

                    // Cranking speed accelerates slightly: 5.2 Hz to 7.0 Hz
                    float crankRate = Mathf.Lerp(5.2f, 7.0f, Mathf.Clamp01(t / 1.25f));
                    phaseCrank += 2f * Mathf.PI * crankRate / sampleRate;

                    // Compression stroke peaking every cycle
                    float strokeWave = Mathf.Sin(phaseCrank);
                    float compressionPulse = Mathf.Pow(Mathf.Clamp01(strokeWave), 3.5f);

                    // Starter motor whine: pitch modulates down during heavy compression
                    float motorFreq = Mathf.Lerp(195f, 135f, compressionPulse) + Mathf.Lerp(0f, 45f, t / 1.3f);
                    phaseMotor += 2f * Mathf.PI * motorFreq / sampleRate;
                    float motorWhine = (Mathf.Sin(phaseMotor) + 0.35f * Mathf.Sin(phaseMotor * 2f)) * 0.22f;

                    // Heavy cylinder compression thump & air friction
                    float compressionThump = Mathf.Sin(2f * Mathf.PI * 46f * t) * compressionPulse * 0.65f;
                    float compressionMechanical = (Random.value * 2f - 1f) * compressionPulse * 0.25f;

                    s += (motorWhine + compressionThump + compressionMechanical) * crankEnv;
                }

                // Phase 3: Engine Catches, Fires & Roars to Idle (t: 1.20s - 2.1s)
                if (t >= 1.20f)
                {
                    float dt = t - 1.20f;
                    float catchEnv = Mathf.Clamp01(dt / 0.12f);

                    // Engine flare up to ~1100 RPM, then settles to ~650 RPM
                    float flareProgress = Mathf.Clamp01(dt / 0.75f);
                    float engineFreq = Mathf.Lerp(75f, 42f, flareProgress);
                    if (dt < 0.22f) engineFreq = Mathf.Lerp(45f, 75f, dt / 0.22f);

                    phaseCatch += 2f * Mathf.PI * engineFreq / sampleRate;
                    float combustion = Mathf.Sin(phaseCatch)
                                     + 0.6f * Mathf.Sin(phaseCatch * 2f)
                                     + 0.3f * Mathf.Sin(phaseCatch * 3f);

                    // Non-linear cylinder combustion warmth
                    combustion = Mathf.Clamp(combustion * 1.4f, -1f, 1f);

                    // Exhaust blast roar & turbo whistle
                    float blastNoise = (Random.value * 2f - 1f) * Mathf.Exp(-dt * 3.5f) * 0.35f;
                    float fireIntensity = Mathf.Exp(-Mathf.Max(0f, dt - 0.2f) * 1.6f) * 0.45f + 0.35f;

                    s += (combustion * fireIntensity + blastNoise) * catchEnv * 0.85f;
                }

                samples[i] = Mathf.Clamp(s, -1f, 1f) * 0.9f;
            }

            AudioClip clip = AudioClip.Create("ProceduralStarterCrank", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Procedurally generates a heavy diesel truck engine shutdown:
        /// 1. Crankshaft and flywheel deceleration from 650 RPM down to 0.
        /// 2. Spaced-out compression rock-back thuds ("chug... chug... thump").
        /// 3. Intake depressurization sigh & mechanical settle tap.
        /// </summary>
        public static AudioClip CreateProceduralEngineShutdownClip()
        {
            int sampleRate = 44100;
            float duration = 1.7f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float phase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float progress = Mathf.Clamp01(t / 1.45f);

                // Firing frequency drops from 42 Hz (650 RPM) down to 0
                float currentFreq = Mathf.Lerp(42f, 0f, Mathf.Pow(progress, 0.75f));
                phase += 2f * Mathf.PI * currentFreq / sampleRate;

                // Amplitude decays as rotational inertia bleeds off
                float decay = Mathf.Clamp01(1f - progress);

                // Cylinder pulse becomes more pronounced and separated as speed slows
                float pulse = Mathf.Sin(phase);
                float heavyShudder = Mathf.Sign(pulse) * Mathf.Pow(Mathf.Abs(pulse), 2.2f);
                float subThump = Mathf.Sin(phase * 0.5f) * 0.5f;

                // Valve train friction & air purge sigh
                float airSigh = (Random.value * 2f - 1f) * Mathf.Sin(progress * Mathf.PI) * 0.15f;
                float mechanical = (Random.value * 2f - 1f) * decay * 0.08f;

                float s = (heavyShudder * 0.6f + subThump + airSigh + mechanical) * decay * 0.85f;

                // Final settle click/tap at t = 1.35s
                if (t >= 1.32f && t < 1.38f)
                {
                    float dt = t - 1.32f;
                    s += Mathf.Sin(2f * Mathf.PI * 480f * dt) * Mathf.Exp(-dt * 80f) * 0.3f;
                }

                samples[i] = Mathf.Clamp(s, -1f, 1f) * 0.85f;
            }

            AudioClip clip = AudioClip.Create("ProceduralEngineShutdown", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralSneezeClip()
        {
            int sampleRate = 44100;
            float duration = 0.75f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                // Sharp initial burst, rapid exponential decay
                float envelope = Mathf.Exp(-t * 6f);
                // Filtered white noise
                float noise = (Random.value * 2f - 1f);

                // Add mechanical metallic "clack" click at 0.05s
                float click = (t > 0.04f && t < 0.08f) ? Mathf.Sin(2f * Mathf.PI * 2200f * t) * 0.5f : 0f;

                samples[i] = (noise * envelope * 0.8f) + click;
            }

            AudioClip clip = AudioClip.Create("ProceduralAirSneeze", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralAirExhaustClip()
        {
            int sampleRate = 44100;
            float duration = 0.35f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                float noise = (Random.value * 2f - 1f);

                samples[i] = noise * envelope * 0.45f;
            }

            AudioClip clip = AudioClip.Create("ProceduralAirExhaust", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralValvePopClip()
        {
            int sampleRate = 44100;
            float duration = 0.18f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 28f);
                float tone = Mathf.Sin(2f * Mathf.PI * 340f * t) + Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.5f;
                float noise = (Random.value * 2f - 1f) * 0.4f;

                samples[i] = (tone + noise) * envelope * 0.8f;
            }

            AudioClip clip = AudioClip.Create("ProceduralValvePop", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralEngineClip()
        {
            int sampleRate = 44100;
            float duration = 1.0f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float baseFreq = 42f; // Low diesel idle rumble (~600 RPM / 6 cylinders)

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float r1 = Mathf.Sin(2f * Mathf.PI * baseFreq * t);
                float r2 = Mathf.Sin(2f * Mathf.PI * (baseFreq * 2f) * t) * 0.5f;
                float r3 = Mathf.Sin(2f * Mathf.PI * (baseFreq * 3f) * t) * 0.25f;
                float airHiss = (Random.value * 2f - 1f) * 0.08f;

                samples[i] = (r1 + r2 + r3 + airHiss) * 0.3f;
            }

            AudioClip clip = AudioClip.Create("ProceduralEngineIdle", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        #endregion
    }
}
