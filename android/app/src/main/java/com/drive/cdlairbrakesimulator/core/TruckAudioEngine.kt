package com.drive.cdlairbrakesimulator.core

import android.content.Context
import android.media.AudioAttributes
import android.media.AudioFormat
import android.media.AudioTrack
import android.os.Process
import android.util.Log
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import java.io.InputStream
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicInteger
import kotlin.math.PI
import kotlin.math.cos
import kotlin.math.sin
import kotlin.random.Random

/**
 * Clean, mobile-speaker-optimized real-time audio engine for the CDL Air Brake Simulator:
 * - Dedicated URGENT_AUDIO priority thread + 4x hardware buffer to prevent underruns on Moto G Play 2023
 * - Per-sample RPM & crossfade interpolation (zero 43Hz block-step zipper noise)
 * - Band-limited (95 Hz - 2600 Hz) Cascadia / Cat 3406 diesel recordings with zero sub-bass cone rattle
 * - 2-pole low-pass filtered pneumatic air discharges (smooth compressed air hiss instead of harsh white noise)
 */
class TruckAudioEngine(private val context: Context? = null) {

    private val sampleRate = 44100
    private var audioTrack: AudioTrack? = null

    @Volatile
    private var isLoopActive = false
    private var audioThread: Thread? = null
    private val controlScope = CoroutineScope(Dispatchers.Default)

    // Pre-filtered & normalized Cascadia / Cat 3406 PCM audio sample buffers (44.1 kHz)
    @Volatile
    private var idleSamples: FloatArray = FloatArray(0)

    @Volatile
    private var revSamples: FloatArray = FloatArray(0)

    // Engine state & RPM parameters
    private val isEngineRunning = AtomicBoolean(false)
    private val isCranking = AtomicBoolean(false)
    private val targetRpm = AtomicInteger(0)

    // One-shot pneumatic & dashboard sound triggers
    private val airReleaseTotal = (sampleRate * 0.36f).toInt()
    private val airReleasePuff = AtomicInteger(0)

    private val governorSneezeTotal = (sampleRate * 0.50f).toInt()
    private val governorSneezeSamples = AtomicInteger(0)

    private val valvePopTotal = (sampleRate * 0.22f).toInt()
    private val valvePopSamples = AtomicInteger(0)

    private val lowAirBuzzerActive = AtomicBoolean(false)

    // Factory ACC Dashboard Chime / Diagnostic Beep
    private val chimeSampleCount = AtomicInteger(0)
    private val chimeFrequency = AtomicInteger(1046)
    private val chimeTotalDuration = (sampleRate * 0.20f).toInt()

    // Audio volume mute toggle
    private val isMuted = AtomicBoolean(false)

    init {
        loadEngineSamples()
    }

    private fun loadEngineSamples() {
        try {
            idleSamples = loadWavAsFloatArray("cascadia_idle.wav")
            revSamples = loadWavAsFloatArray("cascadia_rev.wav")
            Log.i(
                "TruckAudioEngine",
                "Loaded clean Cascadia samples: idle=${idleSamples.size}, rev=${revSamples.size}"
            )
        } catch (e: Exception) {
            Log.e("TruckAudioEngine", "Failed to load Cascadia WAV samples", e)
        }
    }

    private fun openAssetStream(fileName: String): InputStream? {
        return try {
            context?.assets?.open(fileName)
                ?: javaClass.classLoader?.getResourceAsStream("assets/$fileName")
        } catch (e: Exception) {
            javaClass.classLoader?.getResourceAsStream("assets/$fileName")
        }
    }

    /**
     * Reads a 16-bit mono 44.1 kHz PCM WAV file from assets and returns normalized floats in [-1f, 1f].
     */
    private fun loadWavAsFloatArray(fileName: String): FloatArray {
        val stream = openAssetStream(fileName) ?: return FloatArray(0)
        val bytes = stream.use { it.readBytes() }
        if (bytes.size <= 44) return FloatArray(0)

        var dataOffset = 44
        for (i in 12 until minOf(bytes.size - 8, 256)) {
            if (bytes[i] == 'd'.code.toByte() &&
                bytes[i + 1] == 'a'.code.toByte() &&
                bytes[i + 2] == 't'.code.toByte() &&
                bytes[i + 3] == 'a'.code.toByte()
            ) {
                dataOffset = i + 8
                break
            }
        }

        val pcmByteCount = bytes.size - dataOffset
        val sampleCount = pcmByteCount / 2
        val shortBuffer = ByteBuffer.wrap(bytes, dataOffset, sampleCount * 2)
            .order(ByteOrder.LITTLE_ENDIAN)
            .asShortBuffer()

        val out = FloatArray(sampleCount)
        for (i in 0 until sampleCount) {
            out[i] = shortBuffer.get(i) / 32768f
        }
        return out
    }

    /**
     * Reads from a looped sample array at fractional index [pos] using linear interpolation.
     */
    private fun sampleLoopedLinear(buffer: FloatArray, pos: Double): Float {
        val size = buffer.size
        if (size == 0) return 0f
        var wrapped = pos % size
        if (wrapped < 0.0) wrapped += size
        val idx0 = wrapped.toInt().coerceIn(0, size - 1)
        val idx1 = (idx0 + 1) % size
        val frac = (wrapped - idx0).toFloat()
        return buffer[idx0] * (1f - frac) + buffer[idx1] * frac
    }

    fun start() {
        if (audioTrack != null) return
        if (idleSamples.isEmpty()) {
            loadEngineSamples()
        }

        val minBufferSize = AudioTrack.getMinBufferSize(
            sampleRate,
            AudioFormat.CHANNEL_OUT_MONO,
            AudioFormat.ENCODING_PCM_16BIT
        )
        // Use a generous 4x buffer (~185ms) so mobile CPUs never underrun or crackle during UI animations
        val bufferSize = (minBufferSize * 4).coerceAtLeast(16384)

        try {
            audioTrack = AudioTrack.Builder()
                .setAudioAttributes(
                    AudioAttributes.Builder()
                        .setUsage(AudioAttributes.USAGE_GAME)
                        .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
                        .build()
                )
                .setAudioFormat(
                    AudioFormat.Builder()
                        .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                        .setSampleRate(sampleRate)
                        .setChannelMask(AudioFormat.CHANNEL_OUT_MONO)
                        .build()
                )
                .setBufferSizeInBytes(bufferSize)
                .setTransferMode(AudioTrack.MODE_STREAM)
                .build()

            audioTrack?.setVolume(0.90f)
            audioTrack?.play()
        } catch (e: Exception) {
            Log.e("TruckAudioEngine", "Error starting AudioTrack", e)
        }

        startAudioThread()
    }

    fun stop() {
        isLoopActive = false
        try {
            audioThread?.join(300)
        } catch (_: Exception) {
        }
        audioThread = null
        try {
            audioTrack?.stop()
            audioTrack?.release()
        } catch (e: Exception) {
            Log.e("TruckAudioEngine", "Error stopping AudioTrack", e)
        }
        audioTrack = null
    }

    fun setMuted(muted: Boolean) {
        isMuted.set(muted)
    }

    fun isMuted(): Boolean = isMuted.get()

    /**
     * Updates ignition state and triggers realistic factory chimes / starter crank.
     */
    fun onIgnitionChanged(oldState: IgnitionState, newState: IgnitionState) {
        when (newState) {
            IgnitionState.ACC_ON -> {
                isCranking.set(false)
                isEngineRunning.set(false)
                targetRpm.set(0)
                triggerAccClusterChime()
            }
            IgnitionState.RUNNING -> {
                if (oldState != IgnitionState.RUNNING) {
                    triggerStarterCrank()
                } else {
                    isEngineRunning.set(true)
                }
            }
            IgnitionState.OFF -> {
                isCranking.set(false)
                isEngineRunning.set(false)
                targetRpm.set(0)
            }
        }
    }

    fun setRpm(rpm: Int) {
        if (!isCranking.get()) {
            targetRpm.set(rpm)
            if (rpm > 0) {
                isEngineRunning.set(true)
            }
        }
    }

    /**
     * Authentic factory instrument cluster power-on chime sequence (3 smooth harmonic beeps).
     */
    fun triggerAccClusterChime() {
        controlScope.launch {
            val notes = intArrayOf(1046, 1318, 1568)
            for (freq in notes) {
                chimeFrequency.set(freq)
                chimeSampleCount.set(chimeTotalDuration)
                kotlinx.coroutines.delay(220)
            }
        }
    }

    fun triggerStarterCrank() {
        controlScope.launch {
            isCranking.set(true)
            isEngineRunning.set(false)
            targetRpm.set(260)
            kotlinx.coroutines.delay(550)
            if (isCranking.get()) {
                isCranking.set(false)
                isEngineRunning.set(true)
                // Smooth startup flare (880 RPM -> 650 RPM)
                targetRpm.set(880)
                kotlinx.coroutines.delay(420)
                if (isEngineRunning.get() && targetRpm.get() == 880) {
                    targetRpm.set(650)
                }
            }
        }
    }

    fun triggerAirBrakeRelease() {
        airReleasePuff.set(airReleaseTotal)
    }

    fun triggerGovernorSneeze() {
        governorSneezeSamples.set(governorSneezeTotal)
    }

    fun triggerValvePop() {
        valvePopSamples.set(valvePopTotal)
    }

    fun setLowAirBuzzer(active: Boolean) {
        lowAirBuzzerActive.set(active)
    }

    private fun startAudioThread() {
        isLoopActive = true
        audioThread = Thread({
            try {
                Process.setThreadPriority(Process.THREAD_PRIORITY_URGENT_AUDIO)
            } catch (_: Exception) {
            }

            val chunkSamples = 2048
            val pcmBuffer = ShortArray(chunkSamples)

            var smoothRpm = 0f
            var idleReadPos = 0.0
            var revReadPos = 0.0
            var crankPhase = 0.0
            var buzzerPhase = 0.0
            var buzzerCadencePhase = 0.0
            var chimePhase = 0.0

            // 2-pole low-pass filter state for pneumatic air hiss (eliminates harsh white-noise static)
            var airLp1 = 0f
            var airLp2 = 0f

            // High-pass DC/sub-bass blocker state (prevents mobile speaker cone rattle below 95 Hz)
            var hpPrevX = 0f
            var hpPrevY = 0f
            val hpCoeff = 0.986f // ~98 Hz cutoff at 44.1 kHz

            while (isLoopActive) {
                val muted = isMuted.get()
                val target = targetRpm.get().toFloat()
                val cranking = isCranking.get()
                val engineFlag = isEngineRunning.get()
                val buzzerOn = lowAirBuzzerActive.get()

                val idleBuf = idleSamples
                val revBuf = revSamples

                for (i in 0 until chunkSamples) {
                    // Per-sample exponential RPM smoothing (eliminates 43Hz zipper noise!)
                    val slew = if (target > smoothRpm) 0.00022f else 0.00030f
                    smoothRpm += (target - smoothRpm) * slew
                    if (smoothRpm < 1f && target == 0f) smoothRpm = 0f

                    var sample = 0f

                    if (!muted) {
                        // 1. FACTORY ACC DASHBOARD CHIME
                        val chimeRem = chimeSampleCount.get()
                        if (chimeRem > 0) {
                            chimeSampleCount.decrementAndGet()
                            val norm = 1f - (chimeRem.toFloat() / chimeTotalDuration)
                            // Smooth attack (first 10%) and exponential decay (remaining 90%)
                            val attack = (norm / 0.10f).coerceIn(0f, 1f)
                            val decay = (1f - norm) * (1f - norm)
                            val env = attack * decay

                            val freq = chimeFrequency.get().toDouble()
                            chimePhase += 2.0 * PI * freq / sampleRate
                            if (chimePhase > 2.0 * PI) chimePhase -= 2.0 * PI

                            val mainTone = sin(chimePhase).toFloat()
                            val overtone = sin(chimePhase * 2.0).toFloat() * 0.25f
                            sample += (mainTone + overtone) * env * 0.38f
                        }

                        // 2. SMOOTH DIESEL STARTER MOTOR CRANKING (No harsh white noise)
                        if (cranking) {
                            crankPhase += 2.0 * PI * 13.5 / sampleRate
                            if (crankPhase > 2.0 * PI) crankPhase -= 2.0 * PI

                            val compWave = sin(crankPhase).toFloat() * 0.5f + 0.5f
                            val compPulse = compWave * compWave
                            val starterTone = sin(crankPhase * 12.0).toFloat() * 0.22f
                            val lowChug = sin(crankPhase * 4.0).toFloat() * compPulse * 0.35f
                            sample += (starterTone + lowChug) * 0.65f
                        }

                        // 3. CLEAN CASCADIA / CAT 3406 ENGINE AUDIO (Per-sample crossfade & pitch)
                        val running = (engineFlag || smoothRpm > 60f) && !cranking
                        if (running && idleBuf.isNotEmpty() && revBuf.isNotEmpty()) {
                            val idlePitchRate = (smoothRpm / 650.0).coerceIn(0.50, 1.65)
                            val revPitchRate = (smoothRpm / 1420.0).coerceIn(0.60, 1.35)

                            val idleVal = sampleLoopedLinear(idleBuf, idleReadPos)
                            val revVal = sampleLoopedLinear(revBuf, revReadPos)

                            idleReadPos += idlePitchRate
                            if (idleReadPos >= idleBuf.size) idleReadPos -= idleBuf.size

                            revReadPos += revPitchRate
                            if (revReadPos >= revBuf.size) revReadPos -= revBuf.size

                            val revNorm = ((smoothRpm - 720f) / 680f).coerceIn(0f, 1f)
                            val idleWeight = cos(revNorm * PI * 0.5).toFloat()
                            val revWeight = sin(revNorm * PI * 0.5).toFloat()

                            val engineEnv = (smoothRpm / 400f).coerceIn(0f, 1f)
                            val rpmGain = 0.72f + ((smoothRpm / 1500f) * 0.20f)

                            val engineSample = (idleVal * idleWeight + revVal * revWeight) * engineEnv * rpmGain
                            sample += engineSample
                        }

                        // 4. PNEUMATIC AIR EFFECTS (Filtered through 2-pole Low-Pass so there is zero static crackle)
                        var rawAirNoise = 0f

                        val airRemaining = airReleasePuff.get()
                        if (airRemaining > 0) {
                            airReleasePuff.decrementAndGet()
                            val t = 1f - (airRemaining.toFloat() / airReleaseTotal)
                            val attack = (t / 0.06f).coerceIn(0f, 1f)
                            val decay = (1f - t) * (1f - t)
                            rawAirNoise += (Random.nextFloat() * 2f - 1f) * attack * decay * 0.55f
                        }

                        val sneezeRemaining = governorSneezeSamples.get()
                        if (sneezeRemaining > 0) {
                            governorSneezeSamples.decrementAndGet()
                            val t = 1f - (sneezeRemaining.toFloat() / governorSneezeTotal)
                            val attack = (t / 0.04f).coerceIn(0f, 1f)
                            val decay = (1f - t) * (1f - t)
                            rawAirNoise += (Random.nextFloat() * 2f - 1f) * attack * decay * 0.65f
                        }

                        val valveRemaining = valvePopSamples.get()
                        if (valveRemaining > 0) {
                            valvePopSamples.decrementAndGet()
                            val t = 1f - (valveRemaining.toFloat() / valvePopTotal)
                            val attack = (t / 0.05f).coerceIn(0f, 1f)
                            val decay = (1f - t) * (1f - t)
                            rawAirNoise += (Random.nextFloat() * 2f - 1f) * attack * decay * 0.50f
                        }

                        // 2-pole warm low-pass filter (~1350 Hz cutoff) turns harsh static into smooth air "pssshhht"
                        airLp1 += 0.16f * (rawAirNoise - airLp1)
                        airLp2 += 0.16f * (airLp1 - airLp2)
                        sample += airLp2

                        // 5. PULSED LOW-AIR WARNING ALARM (Smooth 3.5 Hz cadence beep, gentle on phone speakers)
                        if (buzzerOn) {
                            buzzerPhase += 2.0 * PI * 680.0 / sampleRate
                            if (buzzerPhase > 2.0 * PI) buzzerPhase -= 2.0 * PI

                            buzzerCadencePhase += 2.0 * PI * 3.5 / sampleRate
                            if (buzzerCadencePhase > 2.0 * PI) buzzerCadencePhase -= 2.0 * PI

                            // Smooth sine-squared pulse envelope (zero clicks on pulse edges)
                            val pulseGate = (sin(buzzerCadencePhase).toFloat() * 0.5f + 0.5f)
                            val smoothGate = pulseGate * pulseGate
                            val buzzerTone = sin(buzzerPhase).toFloat() * smoothGate * 0.22f
                            sample += buzzerTone
                        }
                    }

                    // High-pass filter at ~98 Hz removes sub-audible DC/rumble that rattles small mobile speakers
                    val hpOut = hpCoeff * (hpPrevY + sample - hpPrevX)
                    hpPrevX = sample
                    hpPrevY = hpOut

                    // Clean linear headroom clamping (no harsh tanh overdrive)
                    val clamped = hpOut.coerceIn(-0.92f, 0.92f)
                    pcmBuffer[i] = (clamped * 32767f).toInt().toShort()
                }

                try {
                    audioTrack?.write(pcmBuffer, 0, chunkSamples)
                } catch (_: Exception) {
                }
            }
        }, "TruckAudioRealtimeThread").apply {
            isDaemon = true
            start()
        }
    }
}


