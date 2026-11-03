package com.drive.cdlairbrakesimulator.ui

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.drive.cdlairbrakesimulator.core.AirBrakeState
import com.drive.cdlairbrakesimulator.core.AirBrakeSystem
import com.drive.cdlairbrakesimulator.core.IgnitionState
import com.drive.cdlairbrakesimulator.core.TruckAudioEngine
import com.drive.cdlairbrakesimulator.core.TruckPreset
import com.drive.cdlairbrakesimulator.dmv.DMVTestManager
import com.drive.cdlairbrakesimulator.dmv.DMVTestState
import com.drive.cdlairbrakesimulator.dmv.GameMode
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch

/**
 * ViewModel managing the 60 FPS Air Brake Physics simulation, DMV state evaluation,
 * and realistic real-time Truck Audio Engine (Authentic Cascadia/Cat 3406 Diesel Audio, Starter Crank, Air Pssht, Sneeze, Buzzer).
 */
class CDLAirBrakeViewModel(application: Application) : AndroidViewModel(application) {

    private val physicsEngine = AirBrakeSystem()
    private val dmvTestManager = DMVTestManager()
    val audioEngine = TruckAudioEngine(application.applicationContext)

    private val _physicsState = MutableStateFlow(physicsEngine.getCurrentState())
    val physicsState: StateFlow<AirBrakeState> = _physicsState.asStateFlow()

    private val _dmvState = MutableStateFlow(dmvTestManager.getState())
    val dmvState: StateFlow<DMVTestState> = _dmvState.asStateFlow()

    private val _isMuted = MutableStateFlow(false)
    val isMuted: StateFlow<Boolean> = _isMuted.asStateFlow()

    init {
        audioEngine.start()
        startSimulationLoop()
    }

    override fun onCleared() {
        super.onCleared()
        audioEngine.stop()
    }

    private fun startSimulationLoop() {
        viewModelScope.launch {
            val frameTimeMs = 16L // ~60 FPS
            val dt = frameTimeMs / 1000f

            while (isActive) {
                // Step 1: Update Physics Engine
                val newPhysics = physicsEngine.update(dt)
                _physicsState.value = newPhysics

                // Step 2: Update DMV Test State Machine
                val newDmv = dmvTestManager.update(newPhysics, dt)
                _dmvState.value = newDmv

                if (newDmv.isExamPassed || newDmv.isExamFailed) {
                    if (newPhysics.isRevved) {
                        physicsEngine.setRevved(false)
                    }
                    if (newPhysics.isServiceBrakeHeld) {
                        physicsEngine.setServiceBrakeHeld(false)
                    }
                }

                // Step 3: Update Audio Engine with live parameters
                audioEngine.setRpm(newPhysics.rpm)
                audioEngine.setLowAirBuzzer(newPhysics.isLowAirAlarmActive)

                if (newPhysics.justGovernorSneezed) {
                    audioEngine.triggerGovernorSneeze()
                }
                if (newPhysics.justPoppedYellow || newPhysics.justPoppedRed) {
                    audioEngine.triggerValvePop()
                }

                delay(frameTimeMs)
            }
        }
    }

    fun setIgnition(ignition: IgnitionState) {
        val oldState = physicsEngine.getCurrentState().ignition
        physicsEngine.setIgnition(ignition)
        val newState = physicsEngine.getCurrentState().ignition
        audioEngine.onIgnitionChanged(oldState, newState)
        _physicsState.value = physicsEngine.getCurrentState()
    }

    fun setRevved(revved: Boolean) {
        physicsEngine.setRevved(revved)
        _physicsState.value = physicsEngine.getCurrentState()
    }

    fun toggleYellowValve() {
        physicsEngine.toggleYellowValve()
        audioEngine.triggerValvePop()
        _physicsState.value = physicsEngine.getCurrentState()
    }

    fun toggleRedValve() {
        physicsEngine.toggleRedValve()
        audioEngine.triggerValvePop()
        _physicsState.value = physicsEngine.getCurrentState()
    }

    fun setServiceBrakeHeld(held: Boolean) {
        physicsEngine.setServiceBrakeHeld(held)
        if (held) {
            audioEngine.triggerAirBrakeRelease()
        }
        _physicsState.value = physicsEngine.getCurrentState()
    }

    fun fanBrakes() {
        physicsEngine.fanBrakes()
        audioEngine.triggerAirBrakeRelease()
        _physicsState.value = physicsEngine.getCurrentState()
    }

    fun fastForward60Seconds() {
        physicsEngine.fastForward60Seconds()
        audioEngine.triggerAirBrakeRelease()
        _physicsState.value = physicsEngine.getCurrentState()
    }

    fun setLeakFault(enabled: Boolean) {
        physicsEngine.setLeakFault(enabled)
        _physicsState.value = physicsEngine.getCurrentState()
    }

    fun toggleMute() {
        val newMuted = !_isMuted.value
        _isMuted.value = newMuted
        audioEngine.setMuted(newMuted)
    }

    fun setGameMode(mode: GameMode) {
        dmvTestManager.setMode(mode)
        physicsEngine.reset()
        audioEngine.onIgnitionChanged(IgnitionState.RUNNING, IgnitionState.OFF)
        _dmvState.value = dmvTestManager.getState()
        _physicsState.value = physicsEngine.getCurrentState()
    }

    fun setTruckPreset(preset: TruckPreset) {
        physicsEngine.setTruckPreset(preset)
        _physicsState.value = physicsEngine.getCurrentState()
    }

    fun resetSimulation() {
        physicsEngine.reset()
        dmvTestManager.resetTest()
        audioEngine.onIgnitionChanged(IgnitionState.RUNNING, IgnitionState.OFF)
        _physicsState.value = physicsEngine.getCurrentState()
        _dmvState.value = dmvTestManager.getState()
    }

    fun dismissFailureDialog() {
        dmvTestManager.dismissFailureDialog()
        physicsEngine.reset()
        audioEngine.onIgnitionChanged(IgnitionState.RUNNING, IgnitionState.OFF)
        _physicsState.value = physicsEngine.getCurrentState()
        _dmvState.value = dmvTestManager.getState()
    }

    fun dismissSuccessDialog() {
        dmvTestManager.dismissSuccessDialog()
        physicsEngine.reset()
        audioEngine.onIgnitionChanged(IgnitionState.RUNNING, IgnitionState.OFF)
        _physicsState.value = physicsEngine.getCurrentState()
        _dmvState.value = dmvTestManager.getState()
    }
}
