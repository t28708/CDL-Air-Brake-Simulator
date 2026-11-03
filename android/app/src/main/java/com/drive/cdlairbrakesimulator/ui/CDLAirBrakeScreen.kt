package com.drive.cdlairbrakesimulator.ui

import android.content.Context
import android.graphics.Paint
import android.graphics.Typeface
import android.os.Build
import android.os.VibrationEffect
import android.os.Vibrator
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Error
import androidx.compose.material.icons.filled.FastForward
import androidx.compose.material.icons.filled.KeyboardArrowDown
import androidx.compose.material.icons.filled.KeyboardArrowUp
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.School
import androidx.compose.material.icons.filled.Timer
import androidx.compose.material.icons.automirrored.filled.VolumeMute
import androidx.compose.material.icons.automirrored.filled.VolumeUp
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.drawIntoCanvas
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.graphics.nativeCanvas
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import com.drive.cdlairbrakesimulator.core.AirBrakeState
import com.drive.cdlairbrakesimulator.core.IgnitionState
import com.drive.cdlairbrakesimulator.core.TruckPreset
import com.drive.cdlairbrakesimulator.dmv.DMVTestState
import com.drive.cdlairbrakesimulator.dmv.GameMode
import com.drive.cdlairbrakesimulator.dmv.HighlightTarget
import com.drive.cdlairbrakesimulator.dmv.InspectionStep
import kotlinx.coroutines.launch
import kotlin.math.cos
import kotlin.math.sin
import kotlin.math.sqrt

/**
 * Strict Single-Screen Cockpit Layout for the CDL Air Brake Simulator.
 * Scales TopAppBar, Compressed Gauges + DID, Centered Bendix MV-3 Valve Module,
 * Ignition/Throttle, Foot Pedals, and Safe-Area Examiner Drawer inside a single non-scrolling viewport.
 */
@Composable
fun CDLAirBrakeScreen(
    physics: AirBrakeState,
    dmvState: DMVTestState,
    isMuted: Boolean,
    onToggleMute: () -> Unit,
    onIgnitionChange: (IgnitionState) -> Unit,
    onRevChange: (Boolean) -> Unit,
    onYellowValveToggle: () -> Unit,
    onRedValveToggle: () -> Unit,
    onServiceBrakeHold: (Boolean) -> Unit,
    onFanBrakes: () -> Unit,
    onFastForward60s: () -> Unit,
    onLeakFaultToggle: (Boolean) -> Unit,
    onModeChange: (GameMode) -> Unit,
    onPresetChange: (TruckPreset) -> Unit,
    onResetSimulation: () -> Unit,
    onDismissFailure: () -> Unit,
    onDismissSuccess: () -> Unit
) {
    val context = LocalContext.current
    val vibrator = remember {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            val vibratorManager =
                context.getSystemService(Context.VIBRATOR_MANAGER_SERVICE) as? android.os.VibratorManager
            vibratorManager?.defaultVibrator
        } else {
            @Suppress("DEPRECATION")
            context.getSystemService(Context.VIBRATOR_SERVICE) as? Vibrator
        }
    }

    // Trigger haptic vibration on low air warning or valve pop
    LaunchedEffect(physics.isLowAirAlarmActive) {
        if (physics.isLowAirAlarmActive) {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                vibrator?.vibrate(VibrationEffect.createOneShot(120, VibrationEffect.DEFAULT_AMPLITUDE))
            } else {
                @Suppress("DEPRECATION")
                vibrator?.vibrate(120)
            }
        }
    }

    LaunchedEffect(physics.justPoppedYellow || physics.justPoppedRed) {
        if (physics.justPoppedYellow || physics.justPoppedRed) {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                vibrator?.vibrate(VibrationEffect.createWaveform(longArrayOf(0, 80, 50, 100), -1))
            } else {
                @Suppress("DEPRECATION")
                vibrator?.vibrate(150)
            }
        }
    }

    var isScriptExpanded by remember { mutableStateOf(false) }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color(0xFF101418))
            .navigationBarsPadding()
    ) {
        // STRICT SINGLE-SCREEN PRIMARY UI inside Safe Area (Zero verticalScroll, zero nav-bar bleed)
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(horizontal = 10.dp)
                .padding(top = 4.dp, bottom = 8.dp),
            verticalArrangement = Arrangement.spacedBy(6.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            // 1. MINIMALIST TOP APP BAR (~46dp height)
            MinimalistTopBar(
                physics = physics,
                dmvState = dmvState,
                isMuted = isMuted,
                onToggleMute = onToggleMute,
                onModeChange = onModeChange,
                onPresetChange = onPresetChange,
                onReset = onResetSimulation
            )

            // 2. COMPRESSED DASHBOARD (Gauges + Inline Status + Upgraded Center DID Screen)
            DualGaugesSection(
                physics = physics,
                warningJustDeactivated = dmvState.warningDeactivatedReached,
                stepShortName = dmvState.currentStep.shortName
            )

            // Green Checkpoint Toast when crossing 60 PSI in Step 5 ("Warning system deactivated")
            AnimatedVisibility(visible = dmvState.showWarningDeactivatedBanner) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(10.dp))
                        .background(Color(0xFF004D40))
                        .border(1.5.dp, Color(0xFF00E676), RoundedCornerShape(10.dp))
                        .padding(horizontal = 12.dp, vertical = 5.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.Center
                ) {
                    Icon(
                        imageVector = Icons.Default.CheckCircle,
                        contentDescription = null,
                        tint = Color(0xFF00E676),
                        modifier = Modifier.size(18.dp)
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text(
                        text = "Warning system deactivated (> 60 PSI)",
                        color = Color(0xFF69F0AE),
                        fontSize = 12.sp,
                        fontWeight = FontWeight.ExtraBold
                    )
                }
            }

            // 3. CONSOLIDATED CONTROLS ZONE (Centered MV-3 Module + Ignition/Throttle + Foot Pedals)
            ConsolidatedControlsZone(
                physics = physics,
                dmvState = dmvState,
                onYellowToggle = onYellowValveToggle,
                onRedToggle = onRedValveToggle,
                onIgnitionChange = onIgnitionChange,
                onRevChange = onRevChange,
                onServiceBrakeHold = onServiceBrakeHold,
                onFanBrakes = onFanBrakes,
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f)
            )

            // 4. SAFE-AREA DOCKED EXAMINER SCRIPT DRAWER (Never overlapped by Android Navigation Bar)
            ExaminerBottomSheetContent(
                physics = physics,
                dmvState = dmvState,
                isExpanded = isScriptExpanded,
                onToggleExpand = { isScriptExpanded = !isScriptExpanded },
                onFastForward = onFastForward60s,
                onLeakFaultToggle = onLeakFaultToggle,
                onModeChange = onModeChange
            )
        }

        // MODALS: Exam Failure & Exam Success Scorecard
        if (dmvState.isExamFailed) {
            ExamFailureDialog(
                dmvState = dmvState,
                onDismiss = onDismissFailure,
                onSwitchToTraining = {
                    onModeChange(GameMode.TRAINING)
                    onDismissFailure()
                }
            )
        }

        if (dmvState.isExamPassed) {
            ExamSuccessDialog(
                dmvState = dmvState,
                physics = physics,
                onDismiss = onDismissSuccess
            )
        }
    }
}

/**
 * 1. Minimalist TopAppBar (~48dp height):
 *    Contains only: Sound Toggle Icon, Truck Theme Selector, Compact Mode Pill, Step Indicator ("STEP 1/4"), and Reset Icon.
 */
@Composable
fun MinimalistTopBar(
    physics: AirBrakeState,
    dmvState: DMVTestState,
    isMuted: Boolean,
    onToggleMute: () -> Unit,
    onModeChange: (GameMode) -> Unit,
    onPresetChange: (TruckPreset) -> Unit,
    onReset: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .height(46.dp)
            .clip(RoundedCornerShape(12.dp))
            .background(Color(0xFF161C24))
            .border(1.dp, Color(0xFF263140), RoundedCornerShape(12.dp))
            .padding(horizontal = 8.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        // Sound Toggle Icon
        Box(
            contentAlignment = Alignment.Center,
            modifier = Modifier
                .size(32.dp)
                .clip(CircleShape)
                .background(if (isMuted) Color(0xFF37474F) else Color(0xFF00838F))
                .clickable { onToggleMute() }
        ) {
            Icon(
                imageVector = if (isMuted) Icons.AutoMirrored.Filled.VolumeMute else Icons.AutoMirrored.Filled.VolumeUp,
                contentDescription = "Sound Toggle",
                tint = Color.White,
                modifier = Modifier.size(17.dp)
            )
        }

        // Truck Theme Selector Pill
        val nextPreset = if (physics.truckPreset == TruckPreset.PETERBILT_389) {
            TruckPreset.FREIGHTLINER_CASCADIA
        } else {
            TruckPreset.PETERBILT_389
        }
        Box(
            modifier = Modifier
                .clip(RoundedCornerShape(7.dp))
                .background(Color(0xFF202936))
                .border(1.dp, Color(0xFF334155), RoundedCornerShape(7.dp))
                .clickable { onPresetChange(nextPreset) }
                .padding(horizontal = 8.dp, vertical = 5.dp)
        ) {
            Text(
                text = if (physics.truckPreset == TruckPreset.PETERBILT_389) "🚛 PETERBILT" else "🚚 CASCADIA 126",
                color = Color(0xFFFFCA28),
                fontSize = 10.sp,
                fontWeight = FontWeight.ExtraBold
            )
        }

        // Compact Mode Toggle Pill (TRAIN vs EXAM)
        val isTraining = dmvState.mode == GameMode.TRAINING
        Box(
            modifier = Modifier
                .clip(RoundedCornerShape(7.dp))
                .background(if (isTraining) Color(0xFF1565C0) else Color(0xFFC62828))
                .clickable {
                    onModeChange(if (isTraining) GameMode.DMV_EXAM else GameMode.TRAINING)
                }
                .padding(horizontal = 8.dp, vertical = 5.dp)
        ) {
            Text(
                text = if (isTraining) "🎓 TRAIN" else "⏱ EXAM",
                color = Color.White,
                fontSize = 10.sp,
                fontWeight = FontWeight.ExtraBold
            )
        }

        // Step Indicator ("STEP 1/4")
        Box(
            modifier = Modifier
                .clip(RoundedCornerShape(7.dp))
                .background(Color(0xFF00B0FF).copy(alpha = 0.18f))
                .border(1.dp, Color(0xFF00B0FF), RoundedCornerShape(7.dp))
                .padding(horizontal = 8.dp, vertical = 5.dp)
        ) {
            Text(
                text = dmvState.currentStep.shortName,
                color = Color(0xFF00E5FF),
                fontSize = 10.sp,
                fontWeight = FontWeight.ExtraBold
            )
        }

        // Reset / Restart Icon
        Box(
            contentAlignment = Alignment.Center,
            modifier = Modifier
                .size(32.dp)
                .clip(CircleShape)
                .background(Color(0xFF263238))
                .clickable { onReset() }
        ) {
            Icon(
                imageVector = Icons.Default.Refresh,
                contentDescription = "Reset Simulation",
                tint = Color.White,
                modifier = Modifier.size(17.dp)
            )
        }
    }
}

/**
 * 3. Consolidated Controls Zone (Centered Bendix MV-3 Valve Module, Ignition/Throttle, and Pedals):
 *    - Middle Dark Area: Flexible height container holding the centered, self-contained MV3_Module_Panel card
 *    - Ignition & Throttle Row: OFF / ACC / RUN + HOLD REV
 *    - Lower Controls: Wide, shorter "HOLD SERVICE BRAKE" and "FAN BRAKE" pedals immediately beneath
 */
@Composable
fun ConsolidatedControlsZone(
    physics: AirBrakeState,
    dmvState: DMVTestState,
    onYellowToggle: () -> Unit,
    onRedToggle: () -> Unit,
    onIgnitionChange: (IgnitionState) -> Unit,
    onRevChange: (Boolean) -> Unit,
    onServiceBrakeHold: (Boolean) -> Unit,
    onFanBrakes: () -> Unit,
    modifier: Modifier = Modifier
) {
    // 2.5 Hz continuous sine-wave pulse (200ms half-period, 400ms full cycle)
    val infiniteTransition = rememberInfiniteTransition(label = "TrainingStepPulse")
    val pulseWave by infiniteTransition.animateFloat(
        initialValue = 0f,
        targetValue = 1f,
        animationSpec = infiniteRepeatable(
            animation = tween(200, easing = FastOutSlowInEasing),
            repeatMode = RepeatMode.Reverse
        ),
        label = "PulseWave2_5Hz"
    )
    val highlightAlpha = 0.35f + 0.65f * pulseWave
    val highlightScale = 1.0f + 0.03f * pulseWave

    Column(
        modifier = modifier,
        verticalArrangement = Arrangement.spacedBy(6.dp)
    ) {
        // UPPER CONTROLS: Dark Cavity + Centered MV3_Module_Panel + Ignition/Throttle Row
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .weight(1f)
                .clip(RoundedCornerShape(14.dp))
                .background(
                    Brush.verticalGradient(
                        colors = listOf(Color(0xFF151A21), Color(0xFF0E1217), Color(0xFF0A0D12))
                    )
                )
                .border(1.dp, Color(0xFF263140), RoundedCornerShape(14.dp))
                .padding(horizontal = 8.dp, vertical = 6.dp),
            verticalArrangement = Arrangement.SpaceBetween,
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            // Middle Dark Area (FlexibleHeight = 1): Centers the self-contained MV3_Module_Panel card
            BoxWithConstraints(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f)
                    .clip(RoundedCornerShape(12.dp))
                    .background(
                        Brush.verticalGradient(
                            colors = listOf(Color(0xFF07090C), Color(0xFF0B0E14), Color(0xFF10151D))
                        )
                    )
                    .border(1.dp, Color(0xFF1E2631), RoundedCornerShape(12.dp))
                    .padding(horizontal = 8.dp, vertical = 6.dp),
                contentAlignment = Alignment.Center
            ) {
                // Adaptive knob size so compact 16:9 phones, tall 20:9 phones, and expanded drawers never clip
                val availableH = maxHeight
                val knobSize = (availableH * 0.52f).coerceIn(82.dp, 128.dp)

                // Self-contained Bendix MV-3 Dash Valve Module Card (Anchor: Middle-Center, max 460dp x 260dp)
                MV3ModulePanel(
                    physics = physics,
                    dmvState = dmvState,
                    knobSize = knobSize,
                    highlightAlpha = highlightAlpha,
                    highlightScale = highlightScale,
                    onYellowToggle = onYellowToggle,
                    onRedToggle = onRedToggle
                )
            }

            Spacer(modifier = Modifier.height(6.dp))

            // Tightly Grouped Ignition Switch (OFF | ACC | RUN) + Throttle Rev (1500 RPM)
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(6.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                // Ignition Segmented Group (OFF / ACC / RUN)
                Row(
                    modifier = Modifier
                        .weight(1.35f)
                        .height(38.dp)
                        .clip(RoundedCornerShape(8.dp))
                        .background(Color(0xFF090C10))
                        .border(1.dp, Color(0xFF1E293B), RoundedCornerShape(8.dp))
                        .padding(3.dp),
                    horizontalArrangement = Arrangement.spacedBy(4.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    IgnitionSwitchOption(
                        label = "OFF",
                        isSelected = (physics.ignition == IgnitionState.OFF),
                        isHighlighted = (dmvState.mode == GameMode.TRAINING && dmvState.targetHighlight == HighlightTarget.IGNITION_OFF),
                        highlightAlpha = highlightAlpha,
                        highlightScale = highlightScale,
                        onClick = { onIgnitionChange(IgnitionState.OFF) },
                        modifier = Modifier.weight(0.9f)
                    )
                    IgnitionSwitchOption(
                        label = "ACC",
                        isSelected = (physics.ignition == IgnitionState.ACC_ON),
                        isHighlighted = (dmvState.mode == GameMode.TRAINING && dmvState.targetHighlight == HighlightTarget.IGNITION_ACC),
                        highlightAlpha = highlightAlpha,
                        highlightScale = highlightScale,
                        onClick = { onIgnitionChange(IgnitionState.ACC_ON) },
                        modifier = Modifier.weight(0.95f)
                    )
                    IgnitionSwitchOption(
                        label = "RUN",
                        isSelected = (physics.ignition == IgnitionState.RUNNING),
                        isHighlighted = (dmvState.mode == GameMode.TRAINING && dmvState.targetHighlight == HighlightTarget.IGNITION_RUN),
                        highlightAlpha = highlightAlpha,
                        highlightScale = highlightScale,
                        onClick = { onIgnitionChange(IgnitionState.RUNNING) },
                        modifier = Modifier.weight(1f)
                    )
                }

                // Compact Hold-to-Rev Throttle Button (1500 RPM)
                var isRevPressed by remember { mutableStateOf(false) }
                val revHighlighted =
                    (dmvState.mode == GameMode.TRAINING && dmvState.targetHighlight == HighlightTarget.THROTTLE_REV)
                val revScale = if (revHighlighted) highlightScale else 1f
                val revBorderColor = if (revHighlighted) {
                    Color(0xFFFACC15).copy(alpha = highlightAlpha)
                } else {
                    Color(0xFF37474F)
                }

                Box(
                    modifier = Modifier
                        .weight(1f)
                        .height(38.dp)
                        .graphicsLayer(scaleX = revScale, scaleY = revScale)
                        .clip(RoundedCornerShape(8.dp))
                        .background(if (isRevPressed) Color(0xFFFF9800) else Color(0xFF1E2631))
                        .border(
                            width = if (revHighlighted) 2.dp else 1.dp,
                            color = revBorderColor,
                            shape = RoundedCornerShape(8.dp)
                        )
                        .pointerInput(Unit) {
                            detectTapGestures(
                                onPress = {
                                    isRevPressed = true
                                    onRevChange(true)
                                    tryAwaitRelease()
                                    isRevPressed = false
                                    onRevChange(false)
                                }
                            )
                        },
                    contentAlignment = Alignment.Center
                ) {
                    Text(
                        text = if (isRevPressed) "⚡ REV 1500" else "⚡ HOLD REV",
                        color = if (isRevPressed) Color.Black else if (revHighlighted) Color(0xFFFACC15) else Color.White,
                        fontSize = 10.5.sp,
                        fontWeight = FontWeight.ExtraBold
                    )
                }
            }
        }

        // LOWER CONTROLS: Wide, Shorter Interactive Foot Pedals Immediately Beneath Upper Controls
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .height(66.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            ServiceBrakeHoldPedal(
                physics = physics,
                dmvState = dmvState,
                highlightAlpha = highlightAlpha,
                highlightScale = highlightScale,
                onHoldChange = onServiceBrakeHold,
                modifier = Modifier
                    .weight(1f)
                    .fillMaxSize()
            )

            FanBrakePedal(
                dmvState = dmvState,
                highlightAlpha = highlightAlpha,
                highlightScale = highlightScale,
                onFanClick = onFanBrakes,
                modifier = Modifier
                    .weight(1f)
                    .fillMaxSize()
            )
        }
    }
}

/**
 * Self-contained "MV3_Module_Panel" card centered inside the middle dark dashboard area:
 * - Dark metallic bezel (#1E2530) with subtle border (#334155) and 4 corner screw accents
 * - Top recessed nameplate: "BENDIX MV-3 DASH VALVE MODULE"
 * - Two side-by-side vertical columns (LeftValveColumn & RightValveColumn) keeping
 *   Top Label Pill + 3D Valve Knob + Bottom Status Badge tightly grouped on all screen aspect ratios.
 */
@Composable
fun MV3ModulePanel(
    physics: AirBrakeState,
    dmvState: DMVTestState,
    knobSize: androidx.compose.ui.unit.Dp,
    highlightAlpha: Float,
    highlightScale: Float,
    onYellowToggle: () -> Unit,
    onRedToggle: () -> Unit
) {
    Box(
        modifier = Modifier
            .widthIn(max = 460.dp)
            .heightIn(max = 260.dp)
            .fillMaxWidth()
            .shadow(10.dp, RoundedCornerShape(14.dp))
            .clip(RoundedCornerShape(14.dp))
            .background(
                Brush.verticalGradient(
                    colors = listOf(Color(0xFF28313E), Color(0xFF1E2530), Color(0xFF171D26))
                )
            )
            .border(1.5.dp, Color(0xFF334155), RoundedCornerShape(14.dp))
            .padding(horizontal = 10.dp, vertical = 8.dp),
        contentAlignment = Alignment.Center
    ) {
        // Inner recessed dark metallic faceplate + 4 Corner Screw Accents
        Canvas(modifier = Modifier.fillMaxSize()) {
            val inset = 4.dp.toPx()
            drawRoundRect(
                color = Color(0xFF1B222C),
                topLeft = Offset(inset, inset),
                size = Size(size.width - inset * 2f, size.height - inset * 2f),
                cornerRadius = CornerRadius(10.dp.toPx(), 10.dp.toPx())
            )
            drawRoundRect(
                color = Color(0xFF2D3846),
                topLeft = Offset(inset, inset),
                size = Size(size.width - inset * 2f, size.height - inset * 2f),
                cornerRadius = CornerRadius(10.dp.toPx(), 10.dp.toPx()),
                style = Stroke(width = 1.dp.toPx())
            )

            // 4 Corner Metallic Screw Accents
            val screwOffset = 11.dp.toPx()
            val screwRadius = 4.5.dp.toPx()
            val corners = listOf(
                Offset(screwOffset, screwOffset),
                Offset(size.width - screwOffset, screwOffset),
                Offset(screwOffset, size.height - screwOffset),
                Offset(size.width - screwOffset, size.height - screwOffset)
            )
            for (c in corners) {
                // Recessed screw socket
                drawCircle(
                    color = Color(0xFF0D1117),
                    radius = screwRadius + 1.2.dp.toPx(),
                    center = c
                )
                // Metallic screw head
                drawCircle(
                    brush = Brush.linearGradient(
                        colors = listOf(Color(0xFF64748B), Color(0xFF334155)),
                        start = c - Offset(screwRadius, screwRadius),
                        end = c + Offset(screwRadius, screwRadius)
                    ),
                    radius = screwRadius,
                    center = c
                )
                // Cross slot
                val slotLen = screwRadius * 0.55f
                drawLine(
                    color = Color(0xFF0F172A),
                    start = Offset(c.x - slotLen, c.y - slotLen),
                    end = Offset(c.x + slotLen, c.y + slotLen),
                    strokeWidth = 1.2.dp.toPx()
                )
                drawLine(
                    color = Color(0xFF0F172A),
                    start = Offset(c.x + slotLen, c.y - slotLen),
                    end = Offset(c.x - slotLen, c.y + slotLen),
                    strokeWidth = 1.2.dp.toPx()
                )
            }
        }

        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 4.dp, vertical = 2.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.spacedBy(6.dp, Alignment.CenterVertically)
        ) {
            // Top Beveled Nameplate Header
            Box(
                modifier = Modifier
                    .clip(RoundedCornerShape(6.dp))
                    .background(Color(0xFF131820))
                    .border(1.dp, Color(0xFF334155), RoundedCornerShape(6.dp))
                    .padding(horizontal = 12.dp, vertical = 2.dp),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    text = "BENDIX MV-3 DASH VALVE MODULE",
                    color = Color(0xFF94A3B8),
                    fontSize = 9.sp,
                    fontFamily = FontFamily.Monospace,
                    fontWeight = FontWeight.Bold,
                    letterSpacing = 0.8.sp
                )
            }

            // HorizontalLayoutGroup: LeftValveColumn (Yellow) & RightValveColumn (Red)
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceEvenly,
                verticalAlignment = Alignment.CenterVertically
            ) {
                val isYellowHighlighted =
                    (dmvState.mode == GameMode.TRAINING && dmvState.targetHighlight == HighlightTarget.YELLOW_VALVE)
                TactileYellowDiamondKnob(
                    isPushed = physics.yellowTractorPushed,
                    isHighlighted = isYellowHighlighted,
                    highlightAlpha = highlightAlpha,
                    highlightScale = highlightScale,
                    knobSize = knobSize,
                    onClick = onYellowToggle
                )

                val isRedHighlighted =
                    (dmvState.mode == GameMode.TRAINING && dmvState.targetHighlight == HighlightTarget.RED_VALVE)
                TactileRedOctagonKnob(
                    isPushed = physics.redTrailerPushed,
                    isHighlighted = isRedHighlighted,
                    highlightAlpha = highlightAlpha,
                    highlightScale = highlightScale,
                    knobSize = knobSize,
                    onClick = onRedToggle
                )
            }
        }
    }
}

/**
 * Helper: Builds a rounded-corner diamond path centered at (cx, cy) with half-diagonal `halfDiag`
 * and corner rounding `cornerCut` (0f..0.3f of halfDiag).
 */
private fun createRoundedDiamondPath(
    cx: Float,
    cy: Float,
    halfDiag: Float,
    cornerRatio: Float = 0.16f
): Path {
    val c = halfDiag * cornerRatio
    return Path().apply {
        // Top vertex (cx, cy - halfDiag)
        moveTo(cx - c, cy - halfDiag + c)
        quadraticTo(cx, cy - halfDiag, cx + c, cy - halfDiag + c)
        // Right vertex (cx + halfDiag, cy)
        lineTo(cx + halfDiag - c, cy - c)
        quadraticTo(cx + halfDiag, cy, cx + halfDiag - c, cy + c)
        // Bottom vertex (cx, cy + halfDiag)
        lineTo(cx + c, cy + halfDiag - c)
        quadraticTo(cx, cy + halfDiag, cx - c, cy + halfDiag - c)
        // Left vertex (cx - halfDiag, cy)
        lineTo(cx - halfDiag + c, cy + c)
        quadraticTo(cx - halfDiag, cy, cx - halfDiag + c, cy - c)
        close()
    }
}

/**
 * Helper: Builds a rounded-corner regular octagon path centered at (cx, cy) with circumradius `radius`.
 */
private fun createRoundedOctagonPath(
    cx: Float,
    cy: Float,
    radius: Float,
    cornerRatio: Float = 0.22f
): Path {
    val vertices = Array(8) { i ->
        val angleRad = Math.toRadians((i * 45.0) - 22.5)
        Offset(
            cx + (radius * cos(angleRad)).toFloat(),
            cy + (radius * sin(angleRad)).toFloat()
        )
    }
    return Path().apply {
        for (i in 0 until 8) {
            val prev = vertices[(i + 7) % 8]
            val curr = vertices[i]
            val next = vertices[(i + 1) % 8]

            val startX = curr.x + (prev.x - curr.x) * cornerRatio
            val startY = curr.y + (prev.y - curr.y) * cornerRatio
            val endX = curr.x + (next.x - curr.x) * cornerRatio
            val endY = curr.y + (next.y - curr.y) * cornerRatio

            if (i == 0) {
                moveTo(startX, startY)
            } else {
                lineTo(startX, startY)
            }
            quadraticTo(curr.x, curr.y, endX, endY)
        }
        close()
    }
}

/**
 * LeftValveColumn: Self-contained VerticalLayoutGroup for Yellow Tractor Parking Brake Valve:
 * a) Top Label Pill ("TRACTOR PARKING (MV-3)")
 * b) 3D-styled Yellow Diamond Valve Button
 * c) Bottom Status Badge ("PULLED OUT • PARKED" / "PUSHED IN • RELEASED")
 */
@Composable
fun TactileYellowDiamondKnob(
    isPushed: Boolean,
    isHighlighted: Boolean,
    highlightAlpha: Float,
    highlightScale: Float = 1f,
    knobSize: androidx.compose.ui.unit.Dp = 124.dp,
    onClick: () -> Unit
) {
    // 0f = pushed in (released), 1f = pulled out (parked/popped)
    val pullProgress by animateFloatAsState(
        targetValue = if (isPushed) 0f else 1f,
        animationSpec = tween(durationMillis = 220, easing = FastOutSlowInEasing),
        label = "YellowKnobPull3D"
    )

    val statusText = if (isPushed) "PUSHED IN • RELEASED" else "PULLED OUT • PARKED"
    val statusColor = if (isPushed) Color(0xFF00E676) else Color(0xFFFFAB00)
    val activeScale = if (isHighlighted) highlightScale else 1f

    Column(
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(5.dp, Alignment.CenterVertically),
        modifier = Modifier
            .graphicsLayer(scaleX = activeScale, scaleY = activeScale)
            .clickable(
                interactionSource = remember { MutableInteractionSource() },
                indication = null,
                onClick = onClick
            )
            .padding(horizontal = 2.dp, vertical = 2.dp)
    ) {
        // a) Top Label Pill ("TRACTOR PARKING (MV-3)")
        Box(
            modifier = Modifier
                .clip(RoundedCornerShape(6.dp))
                .background(Color(0xFF12171E))
                .border(
                    width = if (isHighlighted) 1.5.dp else 1.dp,
                    color = if (isHighlighted) Color(0xFFFACC15).copy(alpha = highlightAlpha) else Color(0xFF4B3D1A),
                    shape = RoundedCornerShape(6.dp)
                )
                .padding(horizontal = 8.dp, vertical = 3.dp),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = "TRACTOR PARKING (MV-3)",
                color = Color(0xFFFFCA28),
                fontSize = 9.sp,
                fontWeight = FontWeight.ExtraBold,
                letterSpacing = 0.4.sp,
                maxLines = 1
            )
        }

        // b) 3D-styled Yellow Diamond Valve Button
        Box(
            modifier = Modifier.size(knobSize),
            contentAlignment = Alignment.Center
        ) {
            Canvas(modifier = Modifier.fillMaxSize()) {
                val baseCenter = Offset(size.width / 2f, size.height / 2f + 2.dp.toPx())
                val halfDiag = size.width * 0.43f * (0.94f + 0.08f * pullProgress)

                // 1. Recessed Circular Socket / Rubber Boot in Dash Cavity
                val socketRadius = size.width * 0.25f
                drawCircle(
                    brush = Brush.radialGradient(
                        colors = listOf(Color(0xFF020304), Color(0xFF0C1015), Color(0xFF1E2630)),
                        center = baseCenter,
                        radius = socketRadius * 1.15f
                    ),
                    radius = socketRadius * 1.1f,
                    center = baseCenter
                )
                drawCircle(
                    color = Color(0xFF2A3442),
                    radius = socketRadius * 1.1f,
                    center = baseCenter,
                    style = Stroke(width = 1.5.dp.toPx())
                )

                // Training Mode Pulsing Halo Ring (2.5 Hz sine pulse)
                if (isHighlighted) {
                    val glowPath = createRoundedDiamondPath(
                        baseCenter.x,
                        baseCenter.y,
                        halfDiag + 6.5.dp.toPx()
                    )
                    drawPath(
                        path = glowPath,
                        color = Color(0xFFFACC15).copy(alpha = highlightAlpha),
                        style = Stroke(width = 3.5.dp.toPx())
                    )
                }

                // 3D Elevation Offset: when pulled out, knob lifts toward top-left, casting shadow bottom-right
                val liftX = -3.5.dp.toPx() * pullProgress
                val liftY = -6.5.dp.toPx() * pullProgress
                val knobCenter = Offset(baseCenter.x + liftX, baseCenter.y + liftY)

                // 2. Deep Directional Drop Shadow on the Recessed Dash Tray
                val shadowOffsetX = 4.dp.toPx() + 8.dp.toPx() * pullProgress
                val shadowOffsetY = 5.dp.toPx() + 10.dp.toPx() * pullProgress
                val shadowPath = createRoundedDiamondPath(
                    knobCenter.x + shadowOffsetX,
                    knobCenter.y + shadowOffsetY,
                    halfDiag * 1.02f
                )
                drawPath(
                    path = shadowPath,
                    color = Color.Black.copy(alpha = 0.55f + 0.25f * pullProgress)
                )

                // 3. 3D Extruded Side Wall / Skirt (gives thick molded plastic depth)
                val skirtDepth = 3.5.dp.toPx() + 5.5.dp.toPx() * pullProgress
                val skirtSteps = 6
                for (s in skirtSteps downTo 1) {
                    val frac = s.toFloat() / skirtSteps
                    val layerPath = createRoundedDiamondPath(
                        knobCenter.x + skirtDepth * 0.55f * frac,
                        knobCenter.y + skirtDepth * frac,
                        halfDiag
                    )
                    drawPath(
                        path = layerPath,
                        color = if (s == skirtSteps) Color(0xFF5C4300) else Color(0xFF8F6B00)
                    )
                }

                // 4. Outer 3D Bevel Rim of the Yellow Diamond
                val outerDiamondPath = createRoundedDiamondPath(knobCenter.x, knobCenter.y, halfDiag)
                drawPath(
                    path = outerDiamondPath,
                    brush = Brush.linearGradient(
                        colors = listOf(
                            Color(0xFFFFF59D), // Top-left specular highlight
                            Color(0xFFFFCA28),
                            Color(0xFFB28400)  // Bottom-right shaded bevel
                        ),
                        start = Offset(knobCenter.x - halfDiag, knobCenter.y - halfDiag),
                        end = Offset(knobCenter.x + halfDiag, knobCenter.y + halfDiag)
                    )
                )

                // 5. Main Molded Yellow Diamond Face
                val innerHalfDiag = halfDiag * 0.92f
                val innerDiamondPath = createRoundedDiamondPath(knobCenter.x, knobCenter.y, innerHalfDiag)
                drawPath(
                    path = innerDiamondPath,
                    brush = Brush.radialGradient(
                        colors = if (isPushed) {
                            listOf(
                                Color(0xFFFBC02D),
                                Color(0xFFF5B000),
                                Color(0xFFD49000)
                            )
                        } else {
                            listOf(
                                Color(0xFFFFEA55),
                                Color(0xFFFFCC00),
                                Color(0xFFE09E00)
                            )
                        },
                        center = Offset(knobCenter.x - innerHalfDiag * 0.18f, knobCenter.y - innerHalfDiag * 0.18f),
                        radius = innerHalfDiag * 1.3f
                    )
                )

                // Subtle Inner Bevel Groove
                drawPath(
                    path = innerDiamondPath,
                    color = Color(0x667A5900),
                    style = Stroke(width = 1.2.dp.toPx())
                )

                // 6. Central Concave Circular Recess (like real Bendix MV-3 knob center)
                val dishRadius = innerHalfDiag * 0.44f
                drawCircle(
                    brush = Brush.linearGradient(
                        colors = listOf(
                            Color(0xFFC48B00), // Top-left inner shadow of concave dish
                            Color(0xFFFDD835),
                            Color(0xFFFFF176)  // Bottom-right catchlight of concave dish
                        ),
                        start = Offset(knobCenter.x - dishRadius, knobCenter.y - dishRadius),
                        end = Offset(knobCenter.x + dishRadius, knobCenter.y + dishRadius)
                    ),
                    radius = dishRadius,
                    center = knobCenter
                )
                drawCircle(
                    color = Color(0x448A6200),
                    radius = dishRadius,
                    center = knobCenter,
                    style = Stroke(width = 1.dp.toPx())
                )

                // 7. Authentic Bendix MV-3 Black Engraved Typography
                drawIntoCanvas { canvas ->
                    val native = canvas.nativeCanvas
                    val edgePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
                        color = android.graphics.Color.parseColor("#161616")
                        textAlign = Paint.Align.CENTER
                        typeface = Typeface.create(Typeface.SANS_SERIF, Typeface.BOLD)
                        textSize = innerHalfDiag * 0.135f
                        letterSpacing = 0.03f
                    }

                    // Distance from center to the midpoints of the 4 diamond edges is 0.7071 * innerHalfDiag.
                    // At x = +-0.405 * innerHalfDiag, y = +-0.405 * innerHalfDiag, radial distance is 0.573 * innerHalfDiag,
                    // sitting cleanly centered between the concave dish (0.44) and the outer diamond edge (0.707).
                    val edgeOffset = innerHalfDiag * 0.405f

                    // Top-Left Edge (-45 deg): "PULL"
                    native.save()
                    native.translate(knobCenter.x - edgeOffset, knobCenter.y - edgeOffset)
                    native.rotate(-45f)
                    native.drawText("PULL", 0f, edgePaint.textSize * 0.35f, edgePaint)
                    native.restore()

                    // Top Corner (0 deg): "TO"
                    native.save()
                    native.translate(knobCenter.x, knobCenter.y - innerHalfDiag * 0.69f)
                    native.drawText("TO", 0f, edgePaint.textSize * 0.35f, edgePaint)
                    native.restore()

                    // Top-Right Edge (+45 deg): "APPLY"
                    native.save()
                    native.translate(knobCenter.x + edgeOffset, knobCenter.y - edgeOffset)
                    native.rotate(45f)
                    native.drawText("APPLY", 0f, edgePaint.textSize * 0.35f, edgePaint)
                    native.restore()

                    // Bottom-Left Edge (+45 deg): "PUSH"
                    native.save()
                    native.translate(knobCenter.x - edgeOffset, knobCenter.y + edgeOffset)
                    native.rotate(45f)
                    native.drawText("PUSH", 0f, edgePaint.textSize * 0.35f, edgePaint)
                    native.restore()

                    // Bottom Corner (0 deg): "TO"
                    native.save()
                    native.translate(knobCenter.x, knobCenter.y + innerHalfDiag * 0.73f)
                    native.drawText("TO", 0f, edgePaint.textSize * 0.35f, edgePaint)
                    native.restore()

                    // Bottom-Right Edge (-45 deg): "RELEASE"
                    val releasePaint = Paint(edgePaint).apply {
                        textSize = innerHalfDiag * 0.122f
                    }
                    native.save()
                    native.translate(knobCenter.x + edgeOffset, knobCenter.y + edgeOffset)
                    native.rotate(-45f)
                    native.drawText("RELEASE", 0f, releasePaint.textSize * 0.35f, releasePaint)
                    native.restore()

                    // Center Main Title: "PARKING" / "BRAKE"
                    val centerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
                        color = android.graphics.Color.parseColor("#111111")
                        textAlign = Paint.Align.CENTER
                        typeface = Typeface.create(Typeface.SANS_SERIF, Typeface.BOLD)
                        textSize = innerHalfDiag * 0.19f
                        letterSpacing = 0.02f
                    }
                    native.drawText("PARKING", knobCenter.x, knobCenter.y - innerHalfDiag * 0.10f, centerPaint)
                    native.drawText("BRAKE", knobCenter.x, knobCenter.y + innerHalfDiag * 0.11f, centerPaint)

                    // "Bendix" Brand Logo below BRAKE
                    val bendixPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
                        color = android.graphics.Color.parseColor("#1A1A1A")
                        textAlign = Paint.Align.CENTER
                        typeface = Typeface.create(Typeface.SERIF, Typeface.BOLD_ITALIC)
                        textSize = innerHalfDiag * 0.145f
                    }
                    val bendixY = knobCenter.y + innerHalfDiag * 0.31f
                    native.drawText("Bendix", knobCenter.x, bendixY, bendixPaint)
                }

                // Bendix Swoosh Ribbon under "Bendix"
                val swooshY = knobCenter.y + innerHalfDiag * 0.35f
                val swooshHalfW = innerHalfDiag * 0.23f
                val swooshPath = Path().apply {
                    moveTo(knobCenter.x - swooshHalfW, swooshY)
                    quadraticTo(
                        knobCenter.x,
                        swooshY + innerHalfDiag * 0.07f,
                        knobCenter.x + swooshHalfW,
                        swooshY - innerHalfDiag * 0.02f
                    )
                }
                drawPath(
                    path = swooshPath,
                    color = Color(0xFF1A1A1A),
                    style = Stroke(width = 1.5.dp.toPx())
                )
            }
        }

        // Tactile Status Pill Badge below the knob
        Box(
            modifier = Modifier
                .clip(RoundedCornerShape(6.dp))
                .background(Color(0xFF0A0D12))
                .border(1.dp, statusColor.copy(alpha = 0.65f), RoundedCornerShape(6.dp))
                .padding(horizontal = 8.dp, vertical = 3.dp)
        ) {
            Text(
                text = statusText,
                color = statusColor,
                fontSize = 9.sp,
                fontWeight = FontWeight.ExtraBold,
                letterSpacing = 0.4.sp
            )
        }
    }
}

/**
 * RightValveColumn: Self-contained VerticalLayoutGroup for Red Trailer Air Supply Valve:
 * a) Top Label Pill ("TRAILER AIR SUPPLY")
 * b) 3D-styled Red Octagon Valve Button
 * c) Bottom Status Badge ("PULLED OUT • EVACUATED" / "PUSHED IN • SUPPLY ON")
 */
@Composable
fun TactileRedOctagonKnob(
    isPushed: Boolean,
    isHighlighted: Boolean,
    highlightAlpha: Float,
    highlightScale: Float = 1f,
    knobSize: androidx.compose.ui.unit.Dp = 124.dp,
    onClick: () -> Unit
) {
    // 0f = pushed in (supply on), 1f = pulled out (emergency / evacuated)
    val pullProgress by animateFloatAsState(
        targetValue = if (isPushed) 0f else 1f,
        animationSpec = tween(durationMillis = 220, easing = FastOutSlowInEasing),
        label = "RedKnobPull3D"
    )

    val statusText = if (isPushed) "PUSHED IN • SUPPLY ON" else "PULLED OUT • EVACUATED"
    val statusColor = if (isPushed) Color(0xFF00E676) else Color(0xFFFF5252)
    val activeScale = if (isHighlighted) highlightScale else 1f

    Column(
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(5.dp, Alignment.CenterVertically),
        modifier = Modifier
            .graphicsLayer(scaleX = activeScale, scaleY = activeScale)
            .clickable(
                interactionSource = remember { MutableInteractionSource() },
                indication = null,
                onClick = onClick
            )
            .padding(horizontal = 2.dp, vertical = 2.dp)
    ) {
        // a) Top Label Pill ("TRAILER AIR SUPPLY")
        Box(
            modifier = Modifier
                .clip(RoundedCornerShape(6.dp))
                .background(Color(0xFF161318))
                .border(
                    width = if (isHighlighted) 1.5.dp else 1.dp,
                    color = if (isHighlighted) Color(0xFFFACC15).copy(alpha = highlightAlpha) else Color(0xFF4A2026),
                    shape = RoundedCornerShape(6.dp)
                )
                .padding(horizontal = 10.dp, vertical = 3.dp),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = "TRAILER AIR SUPPLY",
                color = Color(0xFFFF6B6B),
                fontSize = 9.sp,
                fontWeight = FontWeight.ExtraBold,
                letterSpacing = 0.4.sp,
                maxLines = 1
            )
        }

        // b) 3D-styled Red Octagon Valve Button
        Box(
            modifier = Modifier.size(knobSize),
            contentAlignment = Alignment.Center
        ) {
            Canvas(modifier = Modifier.fillMaxSize()) {
                val baseCenter = Offset(size.width / 2f, size.height / 2f + 2.dp.toPx())
                val radius = size.width * 0.39f * (0.94f + 0.08f * pullProgress)

                // 1. Recessed Circular Socket / Rubber Boot in Dash Cavity
                val socketRadius = size.width * 0.25f
                drawCircle(
                    brush = Brush.radialGradient(
                        colors = listOf(Color(0xFF020304), Color(0xFF0C1015), Color(0xFF1E2630)),
                        center = baseCenter,
                        radius = socketRadius * 1.15f
                    ),
                    radius = socketRadius * 1.1f,
                    center = baseCenter
                )
                drawCircle(
                    color = Color(0xFF2A3442),
                    radius = socketRadius * 1.1f,
                    center = baseCenter,
                    style = Stroke(width = 1.5.dp.toPx())
                )

                // Training Mode Pulsing Halo Ring (2.5 Hz sine pulse)
                if (isHighlighted) {
                    val glowPath = createRoundedOctagonPath(
                        baseCenter.x,
                        baseCenter.y,
                        radius + 6.dp.toPx()
                    )
                    drawPath(
                        path = glowPath,
                        color = Color(0xFFFACC15).copy(alpha = highlightAlpha),
                        style = Stroke(width = 3.5.dp.toPx())
                    )
                }

                // 3D Elevation Offset when pulled out
                val liftX = -3.5.dp.toPx() * pullProgress
                val liftY = -6.5.dp.toPx() * pullProgress
                val knobCenter = Offset(baseCenter.x + liftX, baseCenter.y + liftY)

                // 2. Deep Directional Drop Shadow on Recessed Dash Tray
                val shadowOffsetX = 4.dp.toPx() + 8.dp.toPx() * pullProgress
                val shadowOffsetY = 5.dp.toPx() + 10.dp.toPx() * pullProgress
                val shadowPath = createRoundedOctagonPath(
                    knobCenter.x + shadowOffsetX,
                    knobCenter.y + shadowOffsetY,
                    radius * 1.02f
                )
                drawPath(
                    path = shadowPath,
                    color = Color.Black.copy(alpha = 0.55f + 0.25f * pullProgress)
                )

                // 3. 3D Extruded Side Wall / Skirt
                val skirtDepth = 3.5.dp.toPx() + 5.5.dp.toPx() * pullProgress
                val skirtSteps = 6
                for (s in skirtSteps downTo 1) {
                    val frac = s.toFloat() / skirtSteps
                    val layerPath = createRoundedOctagonPath(
                        knobCenter.x + skirtDepth * 0.55f * frac,
                        knobCenter.y + skirtDepth * frac,
                        radius
                    )
                    drawPath(
                        path = layerPath,
                        color = if (s == skirtSteps) Color(0xFF4A0707) else Color(0xFF7C1010)
                    )
                }

                // 4. Outer 3D Bevel Rim of the Red Octagon
                val outerOctagonPath = createRoundedOctagonPath(knobCenter.x, knobCenter.y, radius)
                drawPath(
                    path = outerOctagonPath,
                    brush = Brush.linearGradient(
                        colors = listOf(
                            Color(0xFFFF8A80), // Top-left specular highlight
                            Color(0xFFE53935),
                            Color(0xFF7F0000)  // Bottom-right shaded bevel
                        ),
                        start = Offset(knobCenter.x - radius, knobCenter.y - radius),
                        end = Offset(knobCenter.x + radius, knobCenter.y + radius)
                    )
                )

                // 5. Main Molded Red Octagon Face
                val innerRadius = radius * 0.91f
                val innerOctagonPath = createRoundedOctagonPath(knobCenter.x, knobCenter.y, innerRadius)
                drawPath(
                    path = innerOctagonPath,
                    brush = Brush.radialGradient(
                        colors = if (isPushed) {
                            listOf(
                                Color(0xFFE53935),
                                Color(0xFFC62828),
                                Color(0xFF9E1515)
                            )
                        } else {
                            listOf(
                                Color(0xFFFF5252),
                                Color(0xFFD92525),
                                Color(0xFFA81212)
                            )
                        },
                        center = Offset(knobCenter.x - innerRadius * 0.18f, knobCenter.y - innerRadius * 0.18f),
                        radius = innerRadius * 1.3f
                    )
                )

                // Subtle Inner Bevel Groove
                drawPath(
                    path = innerOctagonPath,
                    color = Color(0x664A0808),
                    style = Stroke(width = 1.2.dp.toPx())
                )

                // 6. Central Concave Circular Recess
                val dishRadius = innerRadius * 0.54f
                drawCircle(
                    brush = Brush.linearGradient(
                        colors = listOf(
                            Color(0xFF8E1111), // Top-left inner shadow of concave dish
                            Color(0xFFD32F2F),
                            Color(0xFFFF6E6E)  // Bottom-right catchlight of concave dish
                        ),
                        start = Offset(knobCenter.x - dishRadius, knobCenter.y - dishRadius),
                        end = Offset(knobCenter.x + dishRadius, knobCenter.y + dishRadius)
                    ),
                    radius = dishRadius,
                    center = knobCenter
                )
                drawCircle(
                    color = Color(0x44590A0A),
                    radius = dishRadius,
                    center = knobCenter,
                    style = Stroke(width = 1.dp.toPx())
                )

                // 7. Authentic Bendix MV-3 White Engraved Typography
                drawIntoCanvas { canvas ->
                    val native = canvas.nativeCanvas
                    val edgePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
                        color = android.graphics.Color.parseColor("#F5F7FA")
                        textAlign = Paint.Align.CENTER
                        typeface = Typeface.create(Typeface.SANS_SERIF, Typeface.BOLD)
                        textSize = innerRadius * 0.14f
                        letterSpacing = 0.03f
                    }

                    // Octagon apothem is cos(22.5 deg) * innerRadius = 0.9238 * innerRadius.
                    // Place text along the top 3 and bottom 3 flat octagon faces:
                    val radialTextDist = innerRadius * 0.74f
                    val diagOffset = radialTextDist * 0.7071f

                    // Top-Left Face (-45 deg): "PULL"
                    native.save()
                    native.translate(knobCenter.x - diagOffset, knobCenter.y - diagOffset)
                    native.rotate(-45f)
                    native.drawText("PULL", 0f, edgePaint.textSize * 0.35f, edgePaint)
                    native.restore()

                    // Top Face (0 deg): "TO"
                    native.save()
                    native.translate(knobCenter.x, knobCenter.y - radialTextDist)
                    native.drawText("TO", 0f, edgePaint.textSize * 0.35f, edgePaint)
                    native.restore()

                    // Top-Right Face (+45 deg): "EVACUATE"
                    val evacPaint = Paint(edgePaint).apply {
                        textSize = innerRadius * 0.12f
                    }
                    native.save()
                    native.translate(knobCenter.x + diagOffset, knobCenter.y - diagOffset)
                    native.rotate(45f)
                    native.drawText("EVACUATE", 0f, evacPaint.textSize * 0.35f, evacPaint)
                    native.restore()

                    // Bottom-Left Face (+45 deg): "PUSH"
                    native.save()
                    native.translate(knobCenter.x - diagOffset, knobCenter.y + diagOffset)
                    native.rotate(45f)
                    native.drawText("PUSH", 0f, edgePaint.textSize * 0.35f, edgePaint)
                    native.restore()

                    // Bottom Face (0 deg): "TO"
                    native.save()
                    native.translate(knobCenter.x, knobCenter.y + radialTextDist + edgePaint.textSize * 0.15f)
                    native.drawText("TO", 0f, edgePaint.textSize * 0.35f, edgePaint)
                    native.restore()

                    // Bottom-Right Face (-45 deg): "SUPPLY"
                    native.save()
                    native.translate(knobCenter.x + diagOffset, knobCenter.y + diagOffset)
                    native.rotate(-45f)
                    native.drawText("SUPPLY", 0f, edgePaint.textSize * 0.35f, edgePaint)
                    native.restore()

                    // Center Main Title: "TRAILER" / "AIR SUPPLY" / "NOT FOR PARKING"
                    val centerPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
                        color = android.graphics.Color.WHITE
                        textAlign = Paint.Align.CENTER
                        typeface = Typeface.create(Typeface.SANS_SERIF, Typeface.BOLD)
                        textSize = innerRadius * 0.168f
                        letterSpacing = 0.02f
                    }
                    native.drawText("TRAILER", knobCenter.x, knobCenter.y - innerRadius * 0.19f, centerPaint)
                    native.drawText("AIR SUPPLY", knobCenter.x, knobCenter.y - innerRadius * 0.01f, centerPaint)

                    val subPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
                        color = android.graphics.Color.parseColor("#FFE5E5")
                        textAlign = Paint.Align.CENTER
                        typeface = Typeface.create(Typeface.SANS_SERIF, Typeface.BOLD)
                        textSize = innerRadius * 0.098f
                        letterSpacing = 0.02f
                    }
                    native.drawText("NOT FOR PARKING", knobCenter.x, knobCenter.y + innerRadius * 0.14f, subPaint)

                    // "Bendix" Brand Logo in White
                    val bendixPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
                        color = android.graphics.Color.WHITE
                        textAlign = Paint.Align.CENTER
                        typeface = Typeface.create(Typeface.SERIF, Typeface.BOLD_ITALIC)
                        textSize = innerRadius * 0.15f
                    }
                    val bendixY = knobCenter.y + innerRadius * 0.34f
                    native.drawText("Bendix", knobCenter.x, bendixY, bendixPaint)
                }

                // White Bendix Swoosh Ribbon under "Bendix"
                val swooshY = knobCenter.y + innerRadius * 0.38f
                val swooshHalfW = innerRadius * 0.24f
                val swooshPath = Path().apply {
                    moveTo(knobCenter.x - swooshHalfW, swooshY)
                    quadraticTo(
                        knobCenter.x,
                        swooshY + innerRadius * 0.07f,
                        knobCenter.x + swooshHalfW,
                        swooshY - innerRadius * 0.02f
                    )
                }
                drawPath(
                    path = swooshPath,
                    color = Color.White,
                    style = Stroke(width = 1.5.dp.toPx())
                )
            }
        }

        // Tactile Status Pill Badge below the knob
        Box(
            modifier = Modifier
                .clip(RoundedCornerShape(6.dp))
                .background(Color(0xFF0A0D12))
                .border(1.dp, statusColor.copy(alpha = 0.65f), RoundedCornerShape(6.dp))
                .padding(horizontal = 8.dp, vertical = 3.dp)
        ) {
            Text(
                text = statusText,
                color = statusColor,
                fontSize = 9.sp,
                fontWeight = FontWeight.ExtraBold,
                letterSpacing = 0.4.sp
            )
        }
    }
}

/**
 * Compact Ignition toggle item for single-screen upper controls with 2.5 Hz Training Mode pulse.
 */
@Composable
fun IgnitionSwitchOption(
    label: String,
    isSelected: Boolean,
    isHighlighted: Boolean,
    highlightAlpha: Float = 1f,
    highlightScale: Float = 1f,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    val bg = if (isSelected) Color(0xFF00E5FF) else Color(0xFF1E2833)
    val textColor = when {
        isSelected -> Color.Black
        isHighlighted -> Color(0xFFFACC15)
        else -> Color(0xFFB0BEC5)
    }
    val scale = if (isHighlighted) highlightScale else 1f
    val borderColor = if (isHighlighted) {
        Color(0xFFFACC15).copy(alpha = highlightAlpha)
    } else {
        Color(0xFF37474F)
    }

    Box(
        modifier = modifier
            .graphicsLayer(scaleX = scale, scaleY = scale)
            .clip(RoundedCornerShape(6.dp))
            .background(bg)
            .border(
                width = if (isHighlighted) 2.dp else 0.5.dp,
                color = borderColor,
                shape = RoundedCornerShape(6.dp)
            )
            .clickable(onClick = onClick)
            .padding(vertical = 6.dp),
        contentAlignment = Alignment.Center
    ) {
        Text(
            text = label,
            color = textColor,
            fontSize = 10.sp,
            fontWeight = FontWeight.ExtraBold
        )
    }
}

/**
 * Wide, shorter interactive Service Brake Hold Pedal (~66dp height) with live 60s progress bar
 * and 2.5 Hz Training Mode pulse.
 */
@Composable
fun ServiceBrakeHoldPedal(
    physics: AirBrakeState,
    dmvState: DMVTestState,
    highlightAlpha: Float = 1f,
    highlightScale: Float = 1f,
    onHoldChange: (Boolean) -> Unit,
    modifier: Modifier = Modifier
) {
    var isPressed by remember { mutableStateOf(false) }
    val isHighlighted = (dmvState.mode == GameMode.TRAINING && dmvState.targetHighlight == HighlightTarget.SERVICE_BRAKE_HOLD)

    val pedalBg = if (isPressed) Color(0xFF00897B) else Color(0xFF1A2634)
    val borderColor = if (isHighlighted) {
        Color(0xFF00E5FF).copy(alpha = highlightAlpha)
    } else {
        Color(0xFF2C3E50)
    }
    val activeScale = if (isHighlighted && !isPressed) highlightScale else 1f

    Box(
        modifier = modifier
            .height(66.dp)
            .graphicsLayer(scaleX = activeScale, scaleY = activeScale)
            .clip(RoundedCornerShape(10.dp))
            .background(pedalBg)
            .border(
                width = if (isHighlighted) 2.2.dp else 1.5.dp,
                color = borderColor,
                shape = RoundedCornerShape(10.dp)
            )
            .pointerInput(Unit) {
                detectTapGestures(
                    onPress = {
                        isPressed = true
                        onHoldChange(true)
                        tryAwaitRelease()
                        isPressed = false
                        onHoldChange(false)
                    }
                )
            }
            .padding(horizontal = 8.dp, vertical = 6.dp),
        contentAlignment = Alignment.Center
    ) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.SpaceBetween,
            modifier = Modifier.fillMaxSize()
        ) {
            Text(
                text = "HOLD SERVICE BRAKE",
                color = if (isHighlighted) Color(0xFF00E5FF) else Color.White,
                fontSize = 10.5.sp,
                fontWeight = FontWeight.Black,
                textAlign = TextAlign.Center
            )

            val holdProgress = (physics.brakeHoldDurationSeconds / 60f).coerceIn(0f, 1f)
            val holdElapsed = physics.brakeHoldDurationSeconds.coerceIn(0f, 60f)

            if (isPressed || physics.isServiceBrakeHeld || physics.brakeHoldDurationSeconds > 0f) {
                LinearProgressIndicator(
                    progress = { holdProgress },
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(9.dp)
                        .clip(RoundedCornerShape(5.dp)),
                    color = Color(0xFF00E5FF),
                    trackColor = Color(0xFF0A1218)
                )

                Text(
                    text = "⏱ ${holdElapsed.toInt()} / 60 SEC",
                    color = Color(0xFFFFEB3B),
                    fontSize = 10.sp,
                    fontWeight = FontWeight.ExtraBold
                )
            } else {
                // Compact horizontal rubber tread lines when idle
                Canvas(modifier = Modifier.size(width = 84.dp, height = 14.dp)) {
                    val lineGap = size.height / 3f
                    for (i in 0..2) {
                        val y = (i * lineGap) + 2.dp.toPx()
                        drawLine(
                            color = Color(0xFF455A64),
                            start = Offset(0f, y),
                            end = Offset(size.width, y),
                            strokeWidth = 2.5.dp.toPx()
                        )
                    }
                }

                Text(
                    text = "TOUCH & HOLD (60 SEC)",
                    color = Color(0xFF90A4AE),
                    fontSize = 9.5.sp,
                    fontWeight = FontWeight.Bold
                )
            }
        }
    }
}

/**
 * Wide, shorter interactive Fan Brake (Pump) Pedal (~66dp height) with 2.5 Hz Training Mode pulse.
 */
@Composable
fun FanBrakePedal(
    dmvState: DMVTestState,
    highlightAlpha: Float = 1f,
    highlightScale: Float = 1f,
    onFanClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    val isHighlighted = (dmvState.mode == GameMode.TRAINING && dmvState.targetHighlight == HighlightTarget.FAN_PEDAL)
    var isTapped by remember { mutableStateOf(false) }

    val pedalBg = if (isTapped) Color(0xFFC62828) else Color(0xFF221A1A)
    val borderColor = if (isHighlighted) {
        Color(0xFFFACC15).copy(alpha = highlightAlpha)
    } else {
        Color(0xFF4E342E)
    }
    val activeScale = if (isHighlighted && !isTapped) highlightScale else 1f

    Box(
        modifier = modifier
            .height(66.dp)
            .graphicsLayer(scaleX = activeScale, scaleY = activeScale)
            .clip(RoundedCornerShape(10.dp))
            .background(pedalBg)
            .border(
                width = if (isHighlighted) 2.2.dp else 1.5.dp,
                color = borderColor,
                shape = RoundedCornerShape(10.dp)
            )
            .pointerInput(Unit) {
                detectTapGestures(
                    onPress = {
                        isTapped = true
                        onFanClick()
                        tryAwaitRelease()
                        isTapped = false
                    }
                )
            }
            .padding(horizontal = 8.dp, vertical = 6.dp),
        contentAlignment = Alignment.Center
    ) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.SpaceBetween,
            modifier = Modifier.fillMaxSize()
        ) {
            Text(
                text = "FAN BRAKE",
                color = if (isHighlighted) Color(0xFFFACC15) else Color.White,
                fontSize = 10.5.sp,
                fontWeight = FontWeight.Black,
                textAlign = TextAlign.Center
            )

            // Compact dot matrix grip canvas
            Canvas(modifier = Modifier.size(width = 76.dp, height = 14.dp)) {
                val rows = 2
                val cols = 6
                val dx = size.width / (cols + 1)
                val dy = size.height / (rows + 1)
                for (r in 1..rows) {
                    for (c in 1..cols) {
                        drawCircle(
                            color = Color(0xFF6D4C41),
                            radius = 2.dp.toPx(),
                            center = Offset(c * dx, r * dy)
                        )
                    }
                }
            }

            Text(
                text = "TAP TO PUMP (-10 PSI)",
                color = Color(0xFFFF8A80),
                fontSize = 9.5.sp,
                fontWeight = FontWeight.Bold
            )
        }
    }
}

/**
 * 4. Safe-Area Docked Examiner Drawer:
 * - Collapsed State: Displays ONLY the compact "NEXT ACTION" banner securely above the Android
 *   system navigation bar (zero bleed of VERBATIM EXAMINER SCRIPT under the navigation buttons).
 * - Expanded State: Smoothly expands upward when tapped to reveal "VERBATIM EXAMINER SCRIPT",
 *   "FAST FORWARD 60s", "FAULT LEAK" toggle, and Mode Selector.
 */
@Composable
fun ExaminerBottomSheetContent(
    physics: AirBrakeState,
    dmvState: DMVTestState,
    isExpanded: Boolean,
    onToggleExpand: () -> Unit,
    onFastForward: () -> Unit,
    onLeakFaultToggle: (Boolean) -> Unit,
    onModeChange: (GameMode) -> Unit
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(Color(0xFF141B24))
            .border(1.dp, Color(0xFF263344), RoundedCornerShape(14.dp))
    ) {
        // COMPACT NEXT ACTION BANNER - Always visible above Safe Area
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .clickable(onClick = onToggleExpand)
                .padding(horizontal = 12.dp, vertical = 8.dp),
            verticalArrangement = Arrangement.spacedBy(4.dp)
        ) {
            // Drag Handle Pill
            Box(
                modifier = Modifier
                    .align(Alignment.CenterHorizontally)
                    .width(34.dp)
                    .height(3.5.dp)
                    .clip(RoundedCornerShape(2.dp))
                    .background(Color(0xFF455A64))
            )

            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(
                            text = "🎯 NEXT ACTION",
                            color = Color(0xFF00E5FF),
                            fontSize = 10.sp,
                            fontWeight = FontWeight.Black,
                            letterSpacing = 0.5.sp
                        )
                        Spacer(modifier = Modifier.width(6.dp))
                        Text(
                            text = "• ${dmvState.currentStep.title.uppercase()}",
                            color = Color(0xFFFFB300),
                            fontSize = 9.5.sp,
                            fontWeight = FontWeight.Bold,
                            maxLines = 1,
                            overflow = TextOverflow.Ellipsis
                        )
                    }
                    Spacer(modifier = Modifier.height(2.dp))
                    Text(
                        text = dmvState.instructionAction,
                        color = Color.White,
                        fontSize = 12.sp,
                        fontWeight = FontWeight.SemiBold,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis,
                        lineHeight = 14.sp
                    )
                }

                Spacer(modifier = Modifier.width(8.dp))

                // Expand / Collapse Indicator Pill
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    modifier = Modifier
                        .clip(RoundedCornerShape(6.dp))
                        .background(Color(0xFF1E2936))
                        .border(1.dp, Color(0xFF34465A), RoundedCornerShape(6.dp))
                        .padding(horizontal = 7.dp, vertical = 4.dp)
                ) {
                    Text(
                        text = if (isExpanded) "HIDE" else "SCRIPT",
                        color = Color(0xFF90CAF9),
                        fontSize = 9.sp,
                        fontWeight = FontWeight.ExtraBold
                    )
                    Icon(
                        imageVector = if (isExpanded) Icons.Default.KeyboardArrowDown else Icons.Default.KeyboardArrowUp,
                        contentDescription = if (isExpanded) "Collapse Examiner Sheet" else "Expand Examiner Sheet",
                        tint = Color(0xFF90CAF9),
                        modifier = Modifier.size(16.dp)
                    )
                }
            }
        }

        // EXPANDED DRAWER CONTENT: Only rendered when expanded so it never peeks under the Android Nav Bar!
        AnimatedVisibility(visible = isExpanded) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 12.dp)
                    .padding(bottom = 12.dp)
            ) {
                Spacer(modifier = Modifier.height(4.dp))

                // Verbatim Examiner Script Card
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(10.dp))
                        .background(Color(0xFF0B1015))
                        .border(1.dp, Color(0xFF243242), RoundedCornerShape(10.dp))
                        .padding(10.dp)
                ) {
                    Column {
                        Text(
                            text = "💬 VERBATIM EXAMINER SCRIPT (FMCSA CDL MANUAL):",
                            color = Color(0xFFFFCA28),
                            fontSize = 10.sp,
                            fontWeight = FontWeight.ExtraBold
                        )
                        Spacer(modifier = Modifier.height(3.dp))
                        Text(
                            text = dmvState.verbatimScript,
                            color = Color(0xFFCFD8DC),
                            fontSize = 11.5.sp,
                            fontFamily = FontFamily.SansSerif,
                            lineHeight = 15.sp
                        )
                    }
                }

                Spacer(modifier = Modifier.height(8.dp))

                // Utilities Row: Fast Forward 60s & Fault Leak Toggle
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Button(
                        onClick = onFastForward,
                        colors = ButtonDefaults.buttonColors(
                            containerColor = Color(0xFF00838F),
                            contentColor = Color.White
                        ),
                        shape = RoundedCornerShape(8.dp),
                        contentPadding = PaddingValues(horizontal = 12.dp, vertical = 6.dp)
                    ) {
                        Icon(
                            imageVector = Icons.Default.FastForward,
                            contentDescription = null,
                            modifier = Modifier.size(15.dp)
                        )
                        Spacer(modifier = Modifier.width(6.dp))
                        Text(
                            text = "FAST FORWARD 60s",
                            fontSize = 10.5.sp,
                            fontWeight = FontWeight.Bold
                        )
                    }

                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(
                            text = "FAULT LEAK",
                            color = if (physics.isLeakFaultEnabled) Color(0xFFFF5252) else Color(0xFF90A4AE),
                            fontSize = 10.sp,
                            fontWeight = FontWeight.Bold
                        )
                        Spacer(modifier = Modifier.width(6.dp))
                        Switch(
                            checked = physics.isLeakFaultEnabled,
                            onCheckedChange = onLeakFaultToggle,
                            colors = SwitchDefaults.colors(
                                checkedThumbColor = Color(0xFFFF5252),
                                checkedTrackColor = Color(0xFF880E4F),
                                uncheckedThumbColor = Color(0xFF546E7A),
                                uncheckedTrackColor = Color(0xFF263238)
                            )
                        )
                    }
                }

                Spacer(modifier = Modifier.height(6.dp))

                // Full Mode Selector inside Expanded Sheet
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(8.dp))
                        .background(Color(0xFF0B1015))
                        .padding(4.dp),
                    horizontalArrangement = Arrangement.spacedBy(6.dp)
                ) {
                    val isTraining = dmvState.mode == GameMode.TRAINING
                    Box(
                        modifier = Modifier
                            .weight(1f)
                            .clip(RoundedCornerShape(6.dp))
                            .background(if (isTraining) Color(0xFF00ACC1) else Color.Transparent)
                            .clickable { onModeChange(GameMode.TRAINING) }
                            .padding(vertical = 6.dp),
                        contentAlignment = Alignment.Center
                    ) {
                        Text(
                            text = "🎓 TRAINING MODE (HINTS)",
                            color = if (isTraining) Color.Black else Color(0xFF78909C),
                            fontSize = 10.sp,
                            fontWeight = FontWeight.ExtraBold
                        )
                    }
                    Box(
                        modifier = Modifier
                            .weight(1f)
                            .clip(RoundedCornerShape(6.dp))
                            .background(if (!isTraining) Color(0xFFFF8F00) else Color.Transparent)
                            .clickable { onModeChange(GameMode.DMV_EXAM) }
                            .padding(vertical = 6.dp),
                        contentAlignment = Alignment.Center
                    ) {
                        Text(
                            text = "📋 STRICT DMV EXAM MODE",
                            color = if (!isTraining) Color.Black else Color(0xFF78909C),
                            fontSize = 10.sp,
                            fontWeight = FontWeight.ExtraBold
                        )
                    }
                }
            }
        }
    }
}


/**
 * Exam Failure Modal Overlay (Rendered in-window to prevent Android API 36 Dialog surface blackouts).
 */
@Composable
fun ExamFailureDialog(
    dmvState: DMVTestState,
    onDismiss: () -> Unit,
    onSwitchToTraining: () -> Unit
) {
    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color.Black.copy(alpha = 0.82f))
            .clickable(
                interactionSource = remember { MutableInteractionSource() },
                indication = null,
                onClick = {} // Consume background taps
            )
            .padding(horizontal = 20.dp),
        contentAlignment = Alignment.Center
    ) {
        Card(
            modifier = Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(20.dp))
                .border(1.5.dp, Color(0xFFFF5252), RoundedCornerShape(20.dp)),
            colors = CardDefaults.cardColors(containerColor = Color(0xFF1E1719))
        ) {
            Column(
                modifier = Modifier.padding(20.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Icon(
                    imageVector = Icons.Default.Error,
                    contentDescription = null,
                    tint = Color(0xFFFF3B30),
                    modifier = Modifier.size(54.dp)
                )

                Spacer(modifier = Modifier.height(10.dp))

                Text(
                    text = "DMV EXAM DISQUALIFICATION",
                    color = Color(0xFFFF5252),
                    fontSize = 16.sp,
                    fontWeight = FontWeight.Black,
                    textAlign = TextAlign.Center
                )

                Spacer(modifier = Modifier.height(12.dp))

                Text(
                    text = dmvState.failureReason,
                    color = Color.White,
                    fontSize = 13.sp,
                    fontWeight = FontWeight.SemiBold,
                    textAlign = TextAlign.Center
                )

                Spacer(modifier = Modifier.height(8.dp))

                Text(
                    text = dmvState.failureDetail,
                    color = Color(0xFFB0BEC5),
                    fontSize = 11.sp,
                    textAlign = TextAlign.Center
                )

                Spacer(modifier = Modifier.height(18.dp))

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    OutlinedButton(
                        onClick = onSwitchToTraining,
                        modifier = Modifier.weight(1f),
                        shape = RoundedCornerShape(10.dp)
                    ) {
                        Text(
                            text = "TRAIN FIRST",
                            color = Color(0xFF00E5FF),
                            fontSize = 11.sp,
                            fontWeight = FontWeight.Bold
                        )
                    }

                    Button(
                        onClick = onDismiss,
                        modifier = Modifier.weight(1f),
                        colors = ButtonDefaults.buttonColors(containerColor = Color(0xFFD32F2F)),
                        shape = RoundedCornerShape(10.dp)
                    ) {
                        Text(
                            text = "RETRY EXAM",
                            color = Color.White,
                            fontSize = 11.sp,
                            fontWeight = FontWeight.Bold
                        )
                    }
                }
            }
        }
    }
}

/**
 * Exam Success & Scorecard Modal Overlay (Rendered in-window for 100% reliable rendering over Canvas gauges).
 */
@Composable
fun ExamSuccessDialog(
    dmvState: DMVTestState,
    physics: AirBrakeState,
    onDismiss: () -> Unit
) {
    val sc = dmvState.scorecard

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color.Black.copy(alpha = 0.84f))
            .clickable(
                interactionSource = remember { MutableInteractionSource() },
                indication = null,
                onClick = {} // Consume background taps
            )
            .padding(horizontal = 18.dp),
        contentAlignment = Alignment.Center
    ) {
        Card(
            modifier = Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(20.dp))
                .border(2.dp, Color(0xFF00E676), RoundedCornerShape(20.dp)),
            colors = CardDefaults.cardColors(containerColor = Color(0xFF13201C))
        ) {
            Column(
                modifier = Modifier.padding(20.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Icon(
                    imageVector = Icons.Default.CheckCircle,
                    contentDescription = null,
                    tint = Color(0xFF00E676),
                    modifier = Modifier.size(56.dp)
                )

                Spacer(modifier = Modifier.height(8.dp))

                Text(
                    text = "DMV TEST PASSED! 🎉",
                    color = Color(0xFF00E676),
                    fontSize = 20.sp,
                    fontWeight = FontWeight.Black,
                    textAlign = TextAlign.Center
                )

                Spacer(modifier = Modifier.height(4.dp))

                Text(
                    text = "Official FMCSA In-Cab Air Brake & Safe Recovery Certified",
                    color = Color(0xFF80CBC4),
                    fontSize = 11.sp,
                    fontWeight = FontWeight.SemiBold,
                    textAlign = TextAlign.Center
                )

                Spacer(modifier = Modifier.height(14.dp))

                // Scorecard Metrics Table
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(10.dp))
                        .background(Color(0xFF0B1411))
                        .border(1.dp, Color(0xFF1E3A2F), RoundedCornerShape(10.dp))
                        .padding(12.dp)
                ) {
                    Text(
                        text = "OFFICIAL SCORECARD METRICS",
                        color = Color(0xFFFFD54F),
                        fontSize = 11.sp,
                        fontWeight = FontWeight.ExtraBold
                    )

                    Spacer(modifier = Modifier.height(8.dp))

                    ScorecardRow(label = "1. Governor Cut-Out:", value = "${sc.governorCutOutPsi.toInt()} PSI (120-140)", pass = true)
                    ScorecardRow(label = "2. 60s Applied Leak:", value = "-${"%.1f".format(sc.leakPressureDrop)} PSI (Max 4.0)", pass = sc.leakPassed)
                    ScorecardRow(label = "3. Low Air Warning:", value = "${sc.lowAirWarningPsi.toInt()} PSI (<=60 PSI)", pass = true)
                    ScorecardRow(label = "4. Trailer Pop-Out:", value = "${sc.trailerPopOutPsi.toInt()} PSI (20-45 PSI)", pass = true)
                    ScorecardRow(label = "5. Tractor Pop-Out:", value = "${sc.tractorPopOutPsi.toInt()} PSI (20-45 PSI)", pass = true)
                    ScorecardRow(label = "6. Safe Air Recovery:", value = "${sc.recoveryCutOutPsi.toInt()} PSI (Warn Off)", pass = true)
                    ScorecardRow(label = "Total Elapsed Time:", value = "${sc.totalTimeSeconds.toInt()} seconds", pass = true)
                }

                Spacer(modifier = Modifier.height(16.dp))

                Button(
                    onClick = onDismiss,
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(48.dp),
                    colors = ButtonDefaults.buttonColors(containerColor = Color(0xFF00E676)),
                    shape = RoundedCornerShape(12.dp)
                ) {
                    Text(
                        text = "OK • RETURN TO MAIN MENU",
                        color = Color.Black,
                        fontSize = 13.5.sp,
                        fontWeight = FontWeight.Black
                    )
                }
            }
        }
    }
}

@Composable
fun ScorecardRow(
    label: String,
    value: String,
    pass: Boolean
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 3.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(
            text = label,
            color = Color(0xFFB0BEC5),
            fontSize = 10.5.sp,
            maxLines = 1
        )
        Text(
            text = "$value ${if (pass) "✓" else "✗"}",
            color = if (pass) Color(0xFF69F0AE) else Color(0xFFFF5252),
            fontSize = 10.5.sp,
            fontWeight = FontWeight.Bold,
            fontFamily = FontFamily.Monospace,
            maxLines = 1
        )
    }
}
