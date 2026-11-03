package com.drive.cdlairbrakesimulator.dmv

import com.drive.cdlairbrakesimulator.core.AirBrakeState
import com.drive.cdlairbrakesimulator.core.IgnitionState
import kotlin.math.abs
import kotlin.math.max

/**
 * Operating mode of the CDL simulator.
 */
enum class GameMode(val title: String, val badgeText: String) {
    TRAINING("Training Mode", "🎓 TRAINING MODE"),
    DMV_EXAM("DMV Exam Mode", "⏱️ DMV EXAM MODE")
}

/**
 * The 5 official CDL FMCSA In-Cab Air Brake Inspection steps (including Safe Air Pressure Recovery) plus Completion.
 */
enum class InspectionStep(
    val stepNumber: Int,
    val title: String,
    val shortName: String
) {
    STEP_1_GOVERNOR_CUTOUT(1, "Governor Cut-Out Test", "STEP 1/5"),
    STEP_2_APPLIED_LEAKAGE(2, "Applied Leakage Test (60s)", "STEP 2/5"),
    STEP_3_LOW_AIR_WARNING(3, "Low Air Warning Device Test", "STEP 3/5"),
    STEP_4_EMERGENCY_SPRING_BRAKES(4, "Emergency Spring Brakes Pop-Out", "STEP 4/5"),
    STEP_5_AIR_RECOVERY(5, "Safe Air Pressure Recovery", "STEP 5/5"),
    STEP_6_PASSED(6, "Inspection Completed & Scorecard", "PASSED")
}

/**
 * Scorecard record captured during the test.
 */
data class Scorecard(
    val governorCutOutPsi: Float = 0f,
    val initialLeakPsi: Float = 0f,
    val finalLeakPsi: Float = 0f,
    val leakPressureDrop: Float = 0f,
    val lowAirWarningPsi: Float = 0f,
    val trailerPopOutPsi: Float = 0f,
    val tractorPopOutPsi: Float = 0f,
    val recoveryCutOutPsi: Float = 0f,
    val totalTimeSeconds: Float = 0f,
    val leakPassed: Boolean = true,
    val allStepsPassed: Boolean = false
)

/**
 * State of the DMV Test evaluation.
 */
data class DMVTestState(
    val mode: GameMode = GameMode.TRAINING,
    val currentStep: InspectionStep = InspectionStep.STEP_1_GOVERNOR_CUTOUT,
    val instructionAction: String = "Start engine (RUNNING) and hold throttle to rev (1500 RPM) until governor cuts out.",
    val verbatimScript: String = "\"Examiner, I am starting the engine and revving to 1500 RPM. The governor must cut out between 120 and 140 PSI.\"",
    val stepProgressFraction: Float = 0f, // 0.0 to 1.0
    val targetHighlight: HighlightTarget? = HighlightTarget.IGNITION_RUN,
    
    // Leak test tracking
    val leakTimerRemaining: Float = 60f,
    val leakStartPsi: Float = 0f,
    val currentLeakDrop: Float = 0f,
    val isLeakTestRunning: Boolean = false,

    // Step 5 (60 PSI Warning Deactivated checkpoint banner)
    val warningDeactivatedReached: Boolean = false,
    val showWarningDeactivatedBanner: Boolean = false,
    val warningDeactivatedTimer: Float = 0f,
    
    // Recorded metrics
    val scorecard: Scorecard = Scorecard(),
    
    // Exam Failure
    val isExamFailed: Boolean = false,
    val failureReason: String = "",
    val failureDetail: String = "",
    
    // Exam Success Modal
    val isExamPassed: Boolean = false,
    
    // Elapsed session time
    val sessionElapsedTime: Float = 0f
)

/**
 * UI element target to highlight in Training Mode.
 */
enum class HighlightTarget {
    IGNITION_RUN,
    IGNITION_OFF,
    IGNITION_ACC,
    THROTTLE_REV,
    YELLOW_VALVE,
    RED_VALVE,
    SERVICE_BRAKE_HOLD,
    FAN_PEDAL,
    FAST_FORWARD
}

/**
 * Manages the DMV Air Brake Inspection state machine and validates user actions.
 */
class DMVTestManager {

    private var state = DMVTestState()

    fun getState(): DMVTestState = state

    fun setMode(mode: GameMode) {
        state = state.copy(mode = mode)
        resetTest()
    }

    fun resetTest() {
        state = DMVTestState(
            mode = state.mode,
            currentStep = InspectionStep.STEP_1_GOVERNOR_CUTOUT,
            instructionAction = "Start engine (RUNNING) and hold throttle to rev (1500 RPM) until governor cuts out.",
            verbatimScript = "\"Examiner, I am starting the engine and revving to 1500 RPM. The governor must cut out between 120 and 140 PSI.\"",
            targetHighlight = HighlightTarget.IGNITION_RUN,
            sessionElapsedTime = 0f
        )
    }

    fun dismissFailureDialog() {
        resetTest()
    }

    fun dismissSuccessDialog() {
        resetTest()
    }

    /**
     * Evaluates physics state at every frame (60 FPS) and transitions steps or flags mistakes.
     */
    fun update(physics: AirBrakeState, dt: Float): DMVTestState {
        if (state.isExamFailed || state.isExamPassed) {
            return state
        }

        val elapsed = state.sessionElapsedTime + dt
        state = state.copy(sessionElapsedTime = elapsed)

        when (state.currentStep) {
            InspectionStep.STEP_1_GOVERNOR_CUTOUT -> evaluateStep1(physics)
            InspectionStep.STEP_2_APPLIED_LEAKAGE -> evaluateStep2(physics, dt)
            InspectionStep.STEP_3_LOW_AIR_WARNING -> evaluateStep3(physics)
            InspectionStep.STEP_4_EMERGENCY_SPRING_BRAKES -> evaluateStep4(physics)
            InspectionStep.STEP_5_AIR_RECOVERY -> evaluateStep5(physics, dt)
            InspectionStep.STEP_6_PASSED -> { /* Handled in step 5 transition */ }
        }

        return state
    }

    private fun evaluateStep1(physics: AirBrakeState) {
        // Step 1: Governor Cut-out Test (120-140 PSI)
        if (physics.ignition != IgnitionState.RUNNING) {
            state = state.copy(
                instructionAction = "Start engine by switching Ignition to RUNNING.",
                targetHighlight = HighlightTarget.IGNITION_RUN,
                stepProgressFraction = 0f
            )
            return
        }

        // Engine is running. Now build air to governor cut-out
        val currentMax = max(physics.primaryPsi, physics.secondaryPsi)
        val progress = ((currentMax - 90f) / (physics.governorCutOutTarget - 90f)).coerceIn(0f, 1f)

        if (!physics.governorCutOutOccurred) {
            state = state.copy(
                instructionAction = "Hold throttle [⚡ HOLD REV] to charge air to 120-140 PSI.",
                verbatimScript = "\"Examiner, I am revving to 1500 RPM until the governor cuts out between 120 and 140 PSI.\"",
                targetHighlight = HighlightTarget.THROTTLE_REV,
                stepProgressFraction = progress
            )
        } else {
            // Governor cut out!
            val cutOutPsi = currentMax
            val updatedScorecard = state.scorecard.copy(governorCutOutPsi = cutOutPsi)

            // Transition to Step 2
            state = state.copy(
                currentStep = InspectionStep.STEP_2_APPLIED_LEAKAGE,
                instructionAction = "Turn engine OFF, switch key to ACC/ON, then push IN both Yellow and Red valves.",
                verbatimScript = "\"Examiner, the governor cut out at ${cutOutPsi.toInt()} PSI. Now turning engine OFF and key to ON to test for leaks.\"",
                targetHighlight = HighlightTarget.IGNITION_OFF,
                stepProgressFraction = 0f,
                scorecard = updatedScorecard,
                leakTimerRemaining = 60f,
                isLeakTestRunning = false
            )
        }
    }

    private fun evaluateStep2(physics: AirBrakeState, dt: Float) {
        // Step 2: Applied Leakage Test (60 Seconds)
        // Proper CDL Sequence after Governor Cut-Out:
        // 1. Release throttle & turn Engine OFF -> Switch Key to ACC/ON
        // 2. Push Yellow (Tractor) & Red (Trailer) valves IN to release spring brakes
        // 3. Press and HOLD Service Brake for 60 seconds

        // In Strict DMV Exam Mode, disqualify ONLY if the driver attempts to start the 60s brake hold
        // before preparing the vehicle properly:
        if (state.mode == GameMode.DMV_EXAM && physics.isServiceBrakeHeld) {
            if (physics.ignition == IgnitionState.RUNNING) {
                failExam(
                    "Engine must be turned OFF before conducting the 60-second applied leakage test.",
                    "FMCSA Step 2 requires shutting off the engine and turning the electrical key to ACC/ON before holding the service brake."
                )
                return
            }
            if (physics.ignition == IgnitionState.OFF) {
                failExam(
                    "Electrical key must be in ACC/ON before conducting the 60-second applied leakage test.",
                    "Turn key to ACC/ON so gauges and warning sensors are active before holding the service brake."
                )
                return
            }
            if (!physics.yellowTractorPushed || !physics.redTrailerPushed) {
                failExam(
                    "Both Parking Brake Valves (Yellow & Red) must be pushed IN before the 60-second leakage test.",
                    "You must release both Tractor and Trailer spring brakes to test the complete air brake system for leaks."
                )
                return
            }
        }

        if (physics.ignition == IgnitionState.RUNNING) {
            val actionMsg = if (physics.isRevved) {
                "Governor cut out! Release throttle pedal, then turn engine OFF and switch key to ACC/ON."
            } else {
                "Turn engine OFF, switch key to ACC/ON, then push IN both Yellow and Red valves."
            }
            state = state.copy(
                instructionAction = actionMsg,
                targetHighlight = HighlightTarget.IGNITION_OFF
            )
            return
        }

        if (physics.ignition == IgnitionState.OFF) {
            state = state.copy(
                instructionAction = "Switch key to [ ACC / ON ] so electrical gauges and warning buzzer are powered.",
                targetHighlight = HighlightTarget.IGNITION_ACC
            )
            return
        }

        // Ignition is ACC_ON. Now ensure both valves are pushed IN to release spring brakes
        if (!physics.yellowTractorPushed) {
            state = state.copy(
                instructionAction = "Push Yellow Diamond knob IN to release tractor parking brake.",
                targetHighlight = HighlightTarget.YELLOW_VALVE
            )
            return
        }

        if (!physics.redTrailerPushed) {
            state = state.copy(
                instructionAction = "Push Red Octagon knob IN to supply air and release trailer emergency brakes.",
                targetHighlight = HighlightTarget.RED_VALVE
            )
            return
        }

        // Both valves are pushed in. Now hold service brake for 60 seconds
        if (!physics.isServiceBrakeHeld) {
            state = state.copy(
                instructionAction = "Press and HOLD [ HOLD SERVICE BRAKE ] firmly for 60 seconds.",
                verbatimScript = "\"Examiner, holding the foot brake for 60 seconds. Air loss must not exceed 4 PSI.\"",
                targetHighlight = HighlightTarget.SERVICE_BRAKE_HOLD,
                isLeakTestRunning = false
            )
            return
        }

        // Brake is actively held!
        val startPsi = if (state.leakStartPsi == 0f) physics.lowestPsi else state.leakStartPsi
        val remaining = max(0f, 60f - physics.brakeHoldDurationSeconds)
        val elapsedHold = physics.brakeHoldDurationSeconds
        val drop = max(0f, startPsi - physics.lowestPsi)
        val progress = (elapsedHold / 60f).coerceIn(0f, 1f)

        state = state.copy(
            leakStartPsi = startPsi,
            leakTimerRemaining = remaining,
            currentLeakDrop = drop,
            isLeakTestRunning = true,
            stepProgressFraction = progress,
            instructionAction = "Holding service brake... (${remaining.toInt()}s remaining). Loss: ${"%.1f".format(drop)} PSI (Max 4 PSI).",
            verbatimScript = "\"Examiner, holding foot brake for 1 full minute. Current air loss is ${"%.1f".format(drop)} PSI.\"",
            targetHighlight = HighlightTarget.SERVICE_BRAKE_HOLD
        )

        // Check if 60 seconds completed
        if (elapsedHold >= 60f) {
            val passed = drop <= 4.0f
            val updatedScorecard = state.scorecard.copy(
                initialLeakPsi = startPsi,
                finalLeakPsi = physics.lowestPsi,
                leakPressureDrop = drop,
                leakPassed = passed
            )

            if (!passed && state.mode == GameMode.DMV_EXAM) {
                failExam(
                    "Applied Leakage Test Failed: Pressure dropped ${"%.1f".format(drop)} PSI in 60s (Max allowed is 4.0 PSI).",
                    "A Class A combination vehicle must not lose more than 4 PSI in 1 minute with service brakes applied."
                )
                return
            }

            // Advance to Step 3: Low Air Warning Test
            state = state.copy(
                currentStep = InspectionStep.STEP_3_LOW_AIR_WARNING,
                instructionAction = "Pump/Fan the brake pedal repeatedly until low air alarm activates at/above 60 PSI.",
                verbatimScript = "\"Examiner, with key in ON position, I will pump the brakes to test the low air warning at 60 PSI.\"",
                targetHighlight = HighlightTarget.FAN_PEDAL,
                stepProgressFraction = 0f,
                scorecard = updatedScorecard,
                isLeakTestRunning = false
            )
        }
    }

    private fun evaluateStep3(physics: AirBrakeState) {
        // Step 3: Low Air Warning Device Test (<60 PSI)
        if (physics.ignition == IgnitionState.OFF) {
            state = state.copy(
                instructionAction = "Turn key to [ ACC / ON ] so low air buzzer and light can activate.",
                targetHighlight = HighlightTarget.IGNITION_ACC
            )
            return
        }

        val lowest = physics.lowestPsi
        val progress = ((120f - lowest) / (120f - 60f)).coerceIn(0f, 1f)

        if (!physics.isLowAirAlarmActive) {
            state = state.copy(
                instructionAction = "Tap [ FAN BRAKE ] repeatedly to drop air below 60 PSI.",
                verbatimScript = "\"Examiner, fanning service brakes to verify low air alarm activates at or above 60 PSI.\"",
                targetHighlight = HighlightTarget.FAN_PEDAL,
                stepProgressFraction = progress
            )
        } else {
            // Low air alarm triggered!
            val updatedScorecard = state.scorecard.copy(lowAirWarningPsi = lowest)

            // Advance to Step 4: Emergency Spring Brakes Pop-out Test (20-45 PSI)
            state = state.copy(
                currentStep = InspectionStep.STEP_4_EMERGENCY_SPRING_BRAKES,
                instructionAction = "Continue fanning brake pedal down to 20-45 PSI until both valves pop out automatically.",
                verbatimScript = "\"Examiner, low air warning activated at ${lowest.toInt()} PSI. Continuing to fan until emergency valves pop out.\"",
                targetHighlight = HighlightTarget.FAN_PEDAL,
                stepProgressFraction = 0f,
                scorecard = updatedScorecard
            )
        }
    }

    private fun evaluateStep4(physics: AirBrakeState) {
        // Step 4: Emergency Spring Brakes Auto Pop-out (20-45 PSI)
        val lowest = physics.lowestPsi
        val progress = ((60f - lowest) / (60f - 20f)).coerceIn(0f, 1f)

        // Record pop-out pressures
        var sc = state.scorecard
        if (!physics.redTrailerPushed && sc.trailerPopOutPsi == 0f) {
            sc = sc.copy(trailerPopOutPsi = lowest)
        }
        if (!physics.yellowTractorPushed && sc.tractorPopOutPsi == 0f) {
            sc = sc.copy(tractorPopOutPsi = lowest)
        }

        if (physics.yellowTractorPushed || physics.redTrailerPushed) {
            val status = when {
                !physics.redTrailerPushed && physics.yellowTractorPushed -> "Trailer (Red) popped at ${sc.trailerPopOutPsi.toInt()} PSI. Keep fanning for Tractor (Yellow)..."
                else -> "Continue tapping [ FAN BRAKE ] until both valves pop out (20-45 PSI)."
            }
            state = state.copy(
                instructionAction = status,
                verbatimScript = "\"Examiner, fanning brakes until tractor and trailer emergency valves pop out between 20 and 45 PSI.\"",
                targetHighlight = HighlightTarget.FAN_PEDAL,
                stepProgressFraction = progress,
                scorecard = sc
            )
        } else {
            // Both valves popped out! Advance to Step 5: Safe Air Pressure Recovery!
            val updatedSc = sc.copy(
                tractorPopOutPsi = if (sc.tractorPopOutPsi == 0f) lowest else sc.tractorPopOutPsi,
                trailerPopOutPsi = if (sc.trailerPopOutPsi == 0f) lowest else sc.trailerPopOutPsi
            )

            state = state.copy(
                currentStep = InspectionStep.STEP_5_AIR_RECOVERY,
                instructionAction = "Start the engine and build air pressure.",
                verbatimScript = "\"Examiner, both valves popped out between 20-45 PSI. Now I will start the engine and build air pressure back to safe operating range.\"",
                targetHighlight = HighlightTarget.IGNITION_RUN,
                stepProgressFraction = 0f,
                scorecard = updatedSc,
                warningDeactivatedReached = false,
                showWarningDeactivatedBanner = false,
                warningDeactivatedTimer = 0f
            )
        }
    }

    private fun evaluateStep5(physics: AirBrakeState, dt: Float) {
        // Step 5: Safe Air Pressure Recovery
        // 1. Start engine (switch Ignition to RUNNING)
        // 2. Hold throttle (HOLD REV) to build air pressure
        // 3. First Checkpoint (> 60 PSI): Low Air Warning deactivates + green badge "Warning system deactivated"
        // 4. Finish (100+ PSI / Governor Cut-Out): DMV TEST PASSED!

        var warnReached = state.warningDeactivatedReached
        var showBanner = state.showWarningDeactivatedBanner
        var bannerTimer = state.warningDeactivatedTimer

        // Checkpoint 1: Crossing 60 PSI deactivates Low Air Warning
        if (!warnReached && physics.lowestPsi > 60f && physics.ignition == IgnitionState.RUNNING) {
            warnReached = true
            showBanner = true
            bannerTimer = 3.5f
        }

        if (showBanner) {
            bannerTimer -= dt
            if (bannerTimer <= 0f) {
                showBanner = false
                bannerTimer = 0f
            }
        }

        if (physics.ignition != IgnitionState.RUNNING) {
            state = state.copy(
                instructionAction = "Start the engine and build air pressure (Switch Ignition to RUN).",
                verbatimScript = "\"Examiner, starting the engine to recharge the air tanks to safe operating pressure.\"",
                targetHighlight = HighlightTarget.IGNITION_RUN,
                warningDeactivatedReached = warnReached,
                showWarningDeactivatedBanner = showBanner,
                warningDeactivatedTimer = bannerTimer
            )
            return
        }

        val currentMax = max(physics.primaryPsi, physics.secondaryPsi)
        val progress = ((physics.lowestPsi - 25f) / (physics.governorCutOutTarget - 25f)).coerceIn(0f, 1f)

        // Check for completion at Governor Cut-Out (or >= 120 PSI)
        if (physics.governorCutOutOccurred || physics.lowestPsi >= 120f) {
            val finalScorecard = state.scorecard.copy(
                recoveryCutOutPsi = currentMax,
                totalTimeSeconds = state.sessionElapsedTime,
                allStepsPassed = true
            )

            state = state.copy(
                currentStep = InspectionStep.STEP_6_PASSED,
                instructionAction = "🎉 DMV TEST PASSED! Safe operating pressure restored.",
                verbatimScript = "\"Examiner, warning turned off above 60 PSI and air tanks are recharged to ${currentMax.toInt()} PSI. This concludes my CDL Air Brake Test.\"",
                targetHighlight = null,
                stepProgressFraction = 1f,
                scorecard = finalScorecard,
                warningDeactivatedReached = true,
                showWarningDeactivatedBanner = false,
                isExamPassed = true
            )
            return
        }

        // Still building air pressure
        val nextPrompt = if (!warnReached) {
            "Hold [ ⚡ HOLD REV ] to build air pressure above 60 PSI (deactivate low air warning)."
        } else {
            "✅ Warning system deactivated! Keep revving to Governor Cut-Out (120-140 PSI) to finish."
        }

        state = state.copy(
            instructionAction = nextPrompt,
            verbatimScript = if (!warnReached) {
                "\"Examiner, building air pressure at 1500 RPM until low air warning turns off above 60 PSI.\""
            } else {
                "\"Examiner, low air warning deactivated above 60 PSI. Continuing to build air to governor cut-out.\""
            },
            targetHighlight = HighlightTarget.THROTTLE_REV,
            stepProgressFraction = progress,
            warningDeactivatedReached = warnReached,
            showWarningDeactivatedBanner = showBanner,
            warningDeactivatedTimer = bannerTimer
        )
    }

    private fun failExam(reason: String, detail: String) {
        state = state.copy(
            isExamFailed = true,
            failureReason = reason,
            failureDetail = detail
        )
    }
}
