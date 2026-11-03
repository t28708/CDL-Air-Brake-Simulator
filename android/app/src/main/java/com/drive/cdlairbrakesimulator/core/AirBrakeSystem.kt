package com.drive.cdlairbrakesimulator.core

import kotlin.math.max
import kotlin.math.min
import kotlin.random.Random

/**
 * Ignition switch position in the truck cab.
 */
enum class IgnitionState {
    OFF,
    ACC_ON,
    RUNNING
}

/**
 * Truck model styling presets.
 */
enum class TruckPreset(val displayName: String, val subtitle: String) {
    PETERBILT_389("Peterbilt 389", "Classic Dual-Dial Chrome Dash"),
    FREIGHTLINER_CASCADIA("Freightliner Cascadia", "Modern Digital-Analog Dash")
}

/**
 * Snapshot of the pneumatic air brake system state.
 */
data class AirBrakeState(
    // Pressure in Pounds per Square Inch (PSI)
    val primaryPsi: Float = 100f,
    val secondaryPsi: Float = 98f,
    
    // Compressor & Governor
    val isCompressorBuilding: Boolean = false,
    val governorCutOutOccurred: Boolean = false,
    val governorCutOutTarget: Float = 125f, // Typically 120-140 PSI
    val governorCutInThreshold: Float = 100f,
    
    // Engine & Ignition
    val ignition: IgnitionState = IgnitionState.OFF,
    val isRevved: Boolean = false, // 1500 RPM vs 650 RPM Idle
    val rpm: Int = 0,
    
    // Pneumatic Valves (true = PUSHED IN / RELEASED / SUPPLY ON, false = PULLED OUT / POPPED / PARKED)
    val yellowTractorPushed: Boolean = false, // Yellow Diamond - Tractor Parking Brake
    val redTrailerPushed: Boolean = false,    // Red Octagon - Trailer Air Supply
    
    // Valve auto pop-out thresholds (randomized realistic range 20-45 PSI)
    val redPopOutPsi: Float = 35f,
    val yellowPopOutPsi: Float = 25f,
    
    // Warning system
    val isLowAirAlarmActive: Boolean = false,
    
    // Brake pedal inputs
    val isServiceBrakeHeld: Boolean = false,
    val brakeHoldDurationSeconds: Float = 0f,
    val initialHoldPsi: Float = 0f,
    
    // Simulation Fault Injections
    val isLeakFaultEnabled: Boolean = false,
    
    // Events / Visual cues
    val justPoppedYellow: Boolean = false,
    val justPoppedRed: Boolean = false,
    val justGovernorSneezed: Boolean = false,
    
    // Selected Truck Preset
    val truckPreset: TruckPreset = TruckPreset.FREIGHTLINER_CASCADIA
) {
    val averagePsi: Float get() = (primaryPsi + secondaryPsi) / 2f
    val lowestPsi: Float get() = min(primaryPsi, secondaryPsi)
    val isBothValvesPushed: Boolean get() = yellowTractorPushed && redTrailerPushed
    val isBothValvesPopped: Boolean get() = !yellowTractorPushed && !redTrailerPushed
}

/**
 * Pure physics engine for truck pneumatic brake system.
 * Updated at 60Hz.
 */
class AirBrakeSystem(
    initialPrimaryPsi: Float? = null,
    initialSecondaryPsi: Float? = null
) {
    private var state: AirBrakeState

    init {
        val (pPsi, sPsi) = if (initialPrimaryPsi != null && initialSecondaryPsi != null) {
            Pair(initialPrimaryPsi, initialSecondaryPsi)
        } else {
            generateRandomStartupPressures()
        }
        val govCutOut = Random.nextInt(124, 134).toFloat()
        val redPop = Random.nextInt(32, 42).toFloat()
        val yellowPop = Random.nextInt(22, 28).toFloat()

        state = AirBrakeState(
            primaryPsi = pPsi,
            secondaryPsi = sPsi,
            isCompressorBuilding = true,
            governorCutOutTarget = govCutOut,
            redPopOutPsi = redPop,
            yellowPopOutPsi = yellowPop
        )
    }

    fun getCurrentState(): AirBrakeState = state

    companion object {
        fun generateRandomStartupPressures(): Pair<Float, Float> {
            val base = Random.nextInt(88, 98).toFloat()
            val delta = Random.nextInt(-3, 4).toFloat()
            val primary = base
            val secondary = (base + delta).coerceIn(85f, 99f)
            return Pair(primary, secondary)
        }
    }

    /**
     * Resets simulation to a fresh truck state with randomized pressures and governor targets.
     */
    fun reset(preset: TruckPreset? = null) {
        val (pPsi, sPsi) = generateRandomStartupPressures()
        val govCutOut = Random.nextInt(124, 134).toFloat()
        val redPop = Random.nextInt(32, 42).toFloat()
        val yellowPop = Random.nextInt(22, 28).toFloat()
        val currentPreset = preset ?: state.truckPreset

        state = AirBrakeState(
            primaryPsi = pPsi,
            secondaryPsi = sPsi,
            isCompressorBuilding = true,
            governorCutOutOccurred = false,
            governorCutOutTarget = govCutOut,
            redPopOutPsi = redPop,
            yellowPopOutPsi = yellowPop,
            ignition = IgnitionState.OFF,
            isRevved = false,
            rpm = 0,
            yellowTractorPushed = false,
            redTrailerPushed = false,
            isLowAirAlarmActive = false,
            isServiceBrakeHeld = false,
            brakeHoldDurationSeconds = 0f,
            initialHoldPsi = 0f,
            isLeakFaultEnabled = false,
            truckPreset = currentPreset
        )
    }

    fun setTruckPreset(preset: TruckPreset) {
        state = state.copy(truckPreset = preset)
    }

    fun setIgnition(newIgnition: IgnitionState) {
        val rpm = when (newIgnition) {
            IgnitionState.OFF -> 0
            IgnitionState.ACC_ON -> 0
            IgnitionState.RUNNING -> if (state.isRevved) 1500 else 650
        }
        state = state.copy(ignition = newIgnition, rpm = rpm)
    }

    fun setRevved(revved: Boolean) {
        val rpm = when (state.ignition) {
            IgnitionState.RUNNING -> if (revved) 1500 else 650
            else -> 0
        }
        state = state.copy(isRevved = revved, rpm = rpm)
    }

    fun toggleYellowValve() {
        val newYellow = !state.yellowTractorPushed
        // Discharging small amount of air when pushing valve into system
        var p = state.primaryPsi
        var s = state.secondaryPsi
        if (newYellow) {
            p = max(0f, p - 1.5f)
            s = max(0f, s - 1.5f)
        }
        state = state.copy(
            yellowTractorPushed = newYellow,
            primaryPsi = p,
            secondaryPsi = s,
            justPoppedYellow = false
        )
    }

    fun toggleRedValve() {
        val newRed = !state.redTrailerPushed
        var p = state.primaryPsi
        var s = state.secondaryPsi
        if (newRed) {
            // Charging trailer reservoir creates a 3-5 PSI drop
            p = max(0f, p - 3.0f)
            s = max(0f, s - 3.0f)
        }
        state = state.copy(
            redTrailerPushed = newRed,
            primaryPsi = p,
            secondaryPsi = s,
            justPoppedRed = false
        )
    }

    fun setServiceBrakeHeld(held: Boolean) {
        val initialPsi = if (held) state.lowestPsi else 0f
        val holdDur = if (held) state.brakeHoldDurationSeconds else 0f
        state = state.copy(
            isServiceBrakeHeld = held,
            brakeHoldDurationSeconds = holdDur,
            initialHoldPsi = if (held && state.initialHoldPsi == 0f) initialPsi else state.initialHoldPsi
        )
    }

    fun fanBrakes() {
        // Pumping service brake releases a shot of compressed air (8-12 PSI drop)
        val dropPrimary = Random.nextDouble(8.0, 11.5).toFloat()
        val dropSecondary = Random.nextDouble(7.5, 11.0).toFloat()
        
        val newP = max(0f, state.primaryPsi - dropPrimary)
        val newS = max(0f, state.secondaryPsi - dropSecondary)

        state = state.copy(
            primaryPsi = newP,
            secondaryPsi = newS,
            isServiceBrakeHeld = false,
            brakeHoldDurationSeconds = 0f
        )
    }

    fun setLeakFault(enabled: Boolean) {
        state = state.copy(isLeakFaultEnabled = enabled)
    }

    fun fastForward60Seconds() {
        // Fast forward 60 seconds of brake hold leak test
        val startPsi = if (state.initialHoldPsi > 0f) state.initialHoldPsi else state.lowestPsi
        val drop = if (state.isLeakFaultEnabled) Random.nextDouble(8.5, 10.5).toFloat() else Random.nextDouble(1.8, 2.8).toFloat()
        val newP = max(0f, state.primaryPsi - drop)
        val newS = max(0f, state.secondaryPsi - (drop * 0.95f))
        state = state.copy(
            isServiceBrakeHeld = true,
            initialHoldPsi = startPsi,
            primaryPsi = newP,
            secondaryPsi = newS,
            brakeHoldDurationSeconds = state.brakeHoldDurationSeconds + 60f
        )
    }

    /**
     * Physics frame update.
     * @param dt Delta time in seconds (e.g. ~0.0166s for 60 FPS).
     */
    fun update(dt: Float): AirBrakeState {
        var p = state.primaryPsi
        var s = state.secondaryPsi
        var isBuilding = state.isCompressorBuilding
        var cutOutOccurred = state.governorCutOutOccurred
        var justSneezed = false
        var justPopYellow = false
        var justPopRed = false
        var yellowPushed = state.yellowTractorPushed
        var redPushed = state.redTrailerPushed
        var holdDuration = state.brakeHoldDurationSeconds

        // 1. Engine & Compressor Dynamics
        if (state.ignition == IgnitionState.RUNNING) {
            val minPsi = min(p, s)
            val maxPsi = max(p, s)

            // Cut-in logic: build if pressure is at/below cut-in threshold (100 PSI),
            // OR if governor cut-out has not occurred yet and pressure is below cut-out target.
            if (minPsi <= state.governorCutInThreshold || (!cutOutOccurred && maxPsi < state.governorCutOutTarget)) {
                isBuilding = true
                if (minPsi <= state.governorCutInThreshold) {
                    cutOutOccurred = false
                }
            }

            // Cut-out logic
            if (maxPsi >= state.governorCutOutTarget) {
                if (isBuilding) {
                    justSneezed = true
                    cutOutOccurred = true
                }
                isBuilding = false
            }

            // Pressure build rates
            if (isBuilding) {
                val rate = if (state.isRevved) 14.0f else 5.5f // PSI per second
                val increase = rate * dt
                p = min(145f, p + increase)
                s = min(145f, s + (increase * 0.98f))
            }
        } else {
            isBuilding = false
        }

        // 2. Service Brake Hold & Leakage Physics (15x Time Acceleration: 60s simulated in 4 real seconds)
        if (state.isServiceBrakeHeld) {
            val simDt = dt * 15.0f
            holdDuration += simDt
            // Natural baseline leak rate (~2 PSI per simulated min = 0.035 PSI/sim-sec)
            var leakRate = 0.035f
            if (state.isLeakFaultEnabled) {
                // Severe leak fault: ~9 PSI per simulated min = 0.155 PSI/sim-sec
                leakRate = 0.155f
            }
            // Valves being pushed in expands the volume to the brake chambers
            if (yellowPushed && redPushed) {
                leakRate *= 1.15f
            }

            p = max(0f, p - (leakRate * simDt))
            s = max(0f, s - (leakRate * 0.95f * simDt))
        } else {
            if (holdDuration > 0f && state.ignition != IgnitionState.RUNNING) {
                // If not holding, reset hold timer unless in active test step
            }
        }

        // 3. Automatic Spring Brake Pop-Out Physics (20-45 PSI)
        val lowestPressure = min(p, s)
        
        // Trailer Red valve pops first (typically 35-40 PSI)
        if (redPushed && lowestPressure <= state.redPopOutPsi) {
            redPushed = false
            justPopRed = true
        }

        // Tractor Yellow valve pops second (typically 20-25 PSI)
        if (yellowPushed && lowestPressure <= state.yellowPopOutPsi) {
            yellowPushed = false
            justPopYellow = true
        }

        // 4. Low Air Warning Buzzer & Flasher (<60 PSI when Key is ON/ACC or RUNNING)
        val isKeyPowered = (state.ignition == IgnitionState.ACC_ON || state.ignition == IgnitionState.RUNNING)
        val isLowAir = isKeyPowered && (p <= 60f || s <= 60f)

        // 5. Update RPM dynamically based on ignition & rev state
        val targetRpm = when (state.ignition) {
            IgnitionState.OFF -> 0
            IgnitionState.ACC_ON -> 0
            IgnitionState.RUNNING -> if (state.isRevved) 1500 else 650
        }

        state = state.copy(
            primaryPsi = p,
            secondaryPsi = s,
            isCompressorBuilding = isBuilding,
            governorCutOutOccurred = cutOutOccurred,
            isLowAirAlarmActive = isLowAir,
            yellowTractorPushed = yellowPushed,
            redTrailerPushed = redPushed,
            brakeHoldDurationSeconds = holdDuration,
            rpm = targetRpm,
            justGovernorSneezed = justSneezed,
            justPoppedYellow = justPopYellow,
            justPoppedRed = justPopRed
        )

        return state
    }
}
