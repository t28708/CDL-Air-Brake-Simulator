package com.drive.cdlairbrakesimulator

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import com.drive.cdlairbrakesimulator.ui.CDLAirBrakeScreen
import com.drive.cdlairbrakesimulator.ui.CDLAirBrakeViewModel

class MainActivity : ComponentActivity() {

    private val viewModel: CDLAirBrakeViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()

        setContent {
            val darkThemeColors = darkColorScheme(
                primary = Color(0xFF00E5FF),
                secondary = Color(0xFFFFB300),
                background = Color(0xFF101418),
                surface = Color(0xFF161B22),
                onPrimary = Color.Black,
                onSecondary = Color.Black,
                onBackground = Color.White,
                onSurface = Color.White
            )

            MaterialTheme(colorScheme = darkThemeColors) {
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .background(Color(0xFF101418))
                        .safeDrawingPadding()
                ) {
                    val physicsState by viewModel.physicsState.collectAsState()
                    val dmvState by viewModel.dmvState.collectAsState()
                    val isMuted by viewModel.isMuted.collectAsState()

                    CDLAirBrakeScreen(
                        physics = physicsState,
                        dmvState = dmvState,
                        isMuted = isMuted,
                        onToggleMute = viewModel::toggleMute,
                        onIgnitionChange = viewModel::setIgnition,
                        onRevChange = viewModel::setRevved,
                        onYellowValveToggle = viewModel::toggleYellowValve,
                        onRedValveToggle = viewModel::toggleRedValve,
                        onServiceBrakeHold = viewModel::setServiceBrakeHeld,
                        onFanBrakes = viewModel::fanBrakes,
                        onFastForward60s = viewModel::fastForward60Seconds,
                        onLeakFaultToggle = viewModel::setLeakFault,
                        onModeChange = viewModel::setGameMode,
                        onPresetChange = viewModel::setTruckPreset,
                        onResetSimulation = viewModel::resetSimulation,
                        onDismissFailure = viewModel::dismissFailureDialog,
                        onDismissSuccess = viewModel::dismissSuccessDialog
                    )
                }
            }
        }
    }
}
