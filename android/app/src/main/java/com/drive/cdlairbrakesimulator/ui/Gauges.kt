package com.drive.cdlairbrakesimulator.ui

import android.graphics.Paint
import android.graphics.Typeface
import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
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
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.drawIntoCanvas
import androidx.compose.ui.graphics.nativeCanvas
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.drive.cdlairbrakesimulator.core.AirBrakeState
import com.drive.cdlairbrakesimulator.core.IgnitionState
import kotlin.math.cos
import kotlin.math.sin

/**
 * Compressed Freightliner Cascadia 126 Instrument Cluster Section:
 * - Compact status text row ("IGNITION: OFF | 0 RPM") directly above gauges (no bulky container)
 * - Left Gauge: Tachometer (RPM x100, 0..25 scale with redline at 21..25, White needle)
 * - Center Between Gauges: Integrated LOW AIR WARNING indicator pod (saves 45dp+ vertical height)
 * - Right Gauge: Dual-Needle Air Pressure Gauge (0..150 PSI, Red zone 0..60,
 *   White needle = Primary [1], Blue needle = Secondary [2])
 */
@Composable
fun DualGaugesSection(
    physics: AirBrakeState,
    warningJustDeactivated: Boolean = false,
    stepShortName: String = "STEP 1/5",
    modifier: Modifier = Modifier
) {
    Column(
        modifier = modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(Color(0xFF131820))
            .border(1.dp, Color(0xFF242E3B), RoundedCornerShape(14.dp))
            .padding(horizontal = 8.dp, vertical = 5.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.SpaceBetween
    ) {
        // Compact Status Text Row directly above gauges (no bulky background box)
        CompactDashboardStatusRow(physics = physics)

        // Gauges Row with Integrated Center DID (Driver Information Display) between the two circular dials
        BoxWithConstraints(
            modifier = Modifier
                .fillMaxWidth()
                .padding(vertical = 2.dp)
        ) {
            // Responsive gauge diameter so the center rectangular DID screen always has room to shine
            val gaugeDiameter = (maxWidth * 0.34f).coerceIn(112.dp, 138.dp)

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                CascadiaRpmGauge(
                    rpm = physics.rpm.toFloat(),
                    ignition = physics.ignition,
                    modifier = Modifier.size(gaugeDiameter)
                )

                // Upgraded Center "DID" (Driver Information Display) Digital Cockpit Screen
                CenterLowAirIndicator(
                    isActive = physics.isLowAirAlarmActive,
                    warningJustDeactivated = warningJustDeactivated,
                    lowestPsi = physics.lowestPsi,
                    stepShortName = stepShortName,
                    isCompressorBuilding = physics.isCompressorBuilding,
                    governorCutOutOccurred = physics.governorCutOutOccurred,
                    modifier = Modifier
                        .weight(1f)
                        .padding(horizontal = 6.dp)
                )

                CascadiaDualAirGauge(
                    primaryPsi = physics.primaryPsi,
                    secondaryPsi = physics.secondaryPsi,
                    isLowAir = (physics.primaryPsi <= 60f || physics.secondaryPsi <= 60f) &&
                            (physics.ignition != IgnitionState.OFF),
                    modifier = Modifier.size(gaugeDiameter)
                )
            }
        }
    }
}

/**
 * LEFT GAUGE: Freightliner Cascadia 126 Tachometer (0..25 RPM x100).
 */
@Composable
fun CascadiaRpmGauge(
    rpm: Float,
    ignition: IgnitionState,
    modifier: Modifier = Modifier
) {
    val animatedRpm by animateFloatAsState(
        targetValue = rpm.coerceIn(0f, 2500f),
        animationSpec = tween(durationMillis = 120, easing = FastOutSlowInEasing),
        label = "CascadiaRpmNeedle"
    )

    Box(
        modifier = modifier
            .shadow(6.dp, CircleShape)
            .clip(CircleShape)
            .background(Color(0xFF0C0E12)),
        contentAlignment = Alignment.Center
    ) {
        Canvas(modifier = Modifier.fillMaxSize()) {
            val center = Offset(size.width / 2f, size.height / 2f)
            val radius = size.minDimension / 2f - 3.dp.toPx()

            // 1. Cascadia 126 recessed dark gunmetal bezel
            drawCascadiaBezel(center, radius)

            // 2. Matte dark charcoal dial face
            val faceRadius = radius - 7.dp.toPx()
            drawCircle(
                brush = Brush.radialGradient(
                    colors = listOf(Color(0xFF191C23), Color(0xFF0B0D11)),
                    center = center,
                    radius = faceRadius
                ),
                radius = faceRadius,
                center = center
            )

            // Scale geometry: 0 is at 162° (lower-left), 25 is at 378° (18°, lower-right)
            val startAngle = 162f
            val totalSweep = 216f
            val arcRadius = faceRadius - 8.dp.toPx()
            val arcTopLeft = Offset(center.x - arcRadius, center.y - arcRadius)
            val arcSize = Size(arcRadius * 2f, arcRadius * 2f)

            // 3. Outer Scale Arc: White (0..21) and Red Redline (21..25)
            val whiteSweep = totalSweep * (21f / 25f)
            val redSweep = totalSweep * (4f / 25f)

            drawArc(
                color = Color(0xFFE2E8F0),
                startAngle = startAngle,
                sweepAngle = whiteSweep,
                useCenter = false,
                topLeft = arcTopLeft,
                size = arcSize,
                style = Stroke(width = 2.dp.toPx(), cap = StrokeCap.Butt)
            )

            drawArc(
                color = Color(0xFFEF4444),
                startAngle = startAngle + whiteSweep,
                sweepAngle = redSweep,
                useCenter = false,
                topLeft = arcTopLeft,
                size = arcSize,
                style = Stroke(width = 3.2.dp.toPx(), cap = StrokeCap.Butt)
            )

            // 4. Ticks (0..25) & Numbers (5, 10, 15, 20, 25)
            val numPaint = Paint().apply {
                isAntiAlias = true
                color = android.graphics.Color.WHITE
                textSize = faceRadius * 0.18f
                textAlign = Paint.Align.CENTER
                typeface = Typeface.create(Typeface.DEFAULT, Typeface.BOLD_ITALIC)
            }

            for (unit in 0..25) {
                val frac = unit / 25f
                val angleDeg = startAngle + frac * totalSweep
                val angleRad = Math.toRadians(angleDeg.toDouble())

                val isMajor = (unit % 5 == 0)
                val isRedline = (unit >= 21)

                val tickLen = if (isMajor) faceRadius * 0.13f else faceRadius * 0.065f
                val tickStroke = if (isMajor) 2.2.dp.toPx() else 1.1.dp.toPx()
                val tickColor = when {
                    isRedline && !isMajor -> Color(0xFFEF4444)
                    isRedline && unit == 25 -> Color(0xFFEF4444)
                    else -> Color(0xFFF8FAFC)
                }

                val ox = center.x + (arcRadius * cos(angleRad)).toFloat()
                val oy = center.y + (arcRadius * sin(angleRad)).toFloat()
                val ix = center.x + ((arcRadius - tickLen) * cos(angleRad)).toFloat()
                val iy = center.y + ((arcRadius - tickLen) * sin(angleRad)).toFloat()

                drawLine(
                    color = tickColor,
                    start = Offset(ix, iy),
                    end = Offset(ox, oy),
                    strokeWidth = tickStroke,
                    cap = StrokeCap.Butt
                )

                // Numbers 5, 10, 15, 20, 25 (matching Cascadia 126 photo)
                if (isMajor && unit > 0) {
                    val labelRadius = arcRadius - tickLen - faceRadius * 0.15f
                    val lx = center.x + (labelRadius * cos(angleRad)).toFloat()
                    val ly = center.y + (labelRadius * sin(angleRad)).toFloat() + numPaint.textSize * 0.35f
                    drawIntoCanvas { canvas ->
                        canvas.nativeCanvas.drawText(unit.toString(), lx, ly, numPaint)
                    }
                }
            }

            // 5. Authentic Cascadia Telltale Indicators (Check Engine / DPF / Stop)
            drawCascadiaRpmTelltales(center, faceRadius, ignition, animatedRpm)

            // 6. Labels "RPM" and "x100" below pivot hub
            val rpmLabelPaint = Paint().apply {
                isAntiAlias = true
                color = android.graphics.Color.parseColor("#B0BEC5")
                textSize = faceRadius * 0.13f
                textAlign = Paint.Align.CENTER
                typeface = Typeface.DEFAULT_BOLD
            }
            val subLabelPaint = Paint().apply {
                isAntiAlias = true
                color = android.graphics.Color.parseColor("#90A4AE")
                textSize = faceRadius * 0.11f
                textAlign = Paint.Align.CENTER
                typeface = Typeface.DEFAULT
            }

            drawIntoCanvas { canvas ->
                canvas.nativeCanvas.drawText("RPM", center.x, center.y + faceRadius * 0.32f, rpmLabelPaint)
                canvas.nativeCanvas.drawText("x100", center.x, center.y + faceRadius * 0.45f, subLabelPaint)
            }

            // 7. Compact Digital RPM Readout Pill at Bottom
            val boxW = faceRadius * 0.96f
            val boxH = faceRadius * 0.27f
            val boxTop = center.y + faceRadius * 0.52f
            val boxLeft = center.x - boxW / 2f

            drawRoundRect(
                color = Color(0xFF080B0F),
                topLeft = Offset(boxLeft, boxTop),
                size = Size(boxW, boxH),
                cornerRadius = CornerRadius(4.dp.toPx(), 4.dp.toPx())
            )
            drawRoundRect(
                color = Color(0xFF263238),
                topLeft = Offset(boxLeft, boxTop),
                size = Size(boxW, boxH),
                cornerRadius = CornerRadius(4.dp.toPx(), 4.dp.toPx()),
                style = Stroke(width = 1.dp.toPx())
            )

            val digitalRpmPaint = Paint().apply {
                isAntiAlias = true
                color = if (animatedRpm > 0f) android.graphics.Color.parseColor("#00E676") else android.graphics.Color.parseColor("#78909C")
                textSize = faceRadius * 0.135f
                textAlign = Paint.Align.CENTER
                typeface = Typeface.MONOSPACE
                isFakeBoldText = true
            }
            drawIntoCanvas { canvas ->
                canvas.nativeCanvas.drawText(
                    "${animatedRpm.toInt()} RPM",
                    center.x,
                    boxTop + boxH * 0.72f,
                    digitalRpmPaint
                )
            }

            // 8. Cascadia White Tapered Needle + Dark Center Cap
            val rpmUnits = (animatedRpm / 100f).coerceIn(0f, 25f)
            val needleAngleDeg = startAngle + (rpmUnits / 25f) * totalSweep
            drawCascadiaNeedle(
                center = center,
                angleDeg = needleAngleDeg,
                length = arcRadius - 4.dp.toPx(),
                needleColor = Color.White,
                coreHighlightColor = Color(0xFFE2E8F0)
            )
            drawCascadiaCenterHub(center)
        }
    }
}

/**
 * RIGHT GAUGE: Freightliner Cascadia 126 Dual-Needle Air Pressure Gauge (0..150 PSI).
 * - White Needle = Primary Air (Tank 1)
 * - Blue Needle  = Secondary Air (Tank 2)
 */
@Composable
fun CascadiaDualAirGauge(
    primaryPsi: Float,
    secondaryPsi: Float,
    isLowAir: Boolean,
    modifier: Modifier = Modifier
) {
    val animatedPrimary by animateFloatAsState(
        targetValue = primaryPsi.coerceIn(0f, 150f),
        animationSpec = tween(durationMillis = 90, easing = FastOutSlowInEasing),
        label = "PrimaryWhiteNeedle"
    )
    val animatedSecondary by animateFloatAsState(
        targetValue = secondaryPsi.coerceIn(0f, 150f),
        animationSpec = tween(durationMillis = 90, easing = FastOutSlowInEasing),
        label = "SecondaryBlueNeedle"
    )

    Box(
        modifier = modifier
            .shadow(6.dp, CircleShape)
            .clip(CircleShape)
            .background(Color(0xFF0C0E12)),
        contentAlignment = Alignment.Center
    ) {
        Canvas(modifier = Modifier.fillMaxSize()) {
            val center = Offset(size.width / 2f, size.height / 2f)
            val radius = size.minDimension / 2f - 3.dp.toPx()

            // 1. Cascadia 126 recessed dark gunmetal bezel
            drawCascadiaBezel(center, radius)

            // 2. Matte dark charcoal dial face
            val faceRadius = radius - 7.dp.toPx()
            drawCircle(
                brush = Brush.radialGradient(
                    colors = listOf(Color(0xFF191C23), Color(0xFF0B0D11)),
                    center = center,
                    radius = faceRadius
                ),
                radius = faceRadius,
                center = center
            )

            // Cascadia 126 AIR Gauge Scale Geometry (matches photo):
            // 0 PSI at 155° (8:10), 50 at 216.7° (10:15), 100 at 278.3° (12:15), 150 at 340° (2:20)
            // leaving clean space below 150 at 3:30 for the Blue Secondary Tank (2) icon
            val startAngle = 155f
            val totalSweep = 185f
            val arcRadius = faceRadius - 8.dp.toPx()
            val arcTopLeft = Offset(center.x - arcRadius, center.y - arcRadius)
            val arcSize = Size(arcRadius * 2f, arcRadius * 2f)

            // 3. Outer Scale Arc: Red Low-Air Warning Zone (0..60 PSI) & White Safe Zone (60..150 PSI)
            val redSweep = totalSweep * (60f / 150f)
            val whiteSweep = totalSweep * (90f / 150f)

            // Inner translucent red warning band from 0..60 PSI
            val redBandRadius = arcRadius - 3.5.dp.toPx()
            drawArc(
                color = Color(0xFFEF4444).copy(alpha = 0.35f),
                startAngle = startAngle,
                sweepAngle = redSweep,
                useCenter = false,
                topLeft = Offset(center.x - redBandRadius, center.y - redBandRadius),
                size = Size(redBandRadius * 2f, redBandRadius * 2f),
                style = Stroke(width = 4.5.dp.toPx(), cap = StrokeCap.Butt)
            )

            // Crisp Red Outer Arc (0..60 PSI)
            drawArc(
                color = Color(0xFFEF4444),
                startAngle = startAngle,
                sweepAngle = redSweep,
                useCenter = false,
                topLeft = arcTopLeft,
                size = arcSize,
                style = Stroke(width = 2.8.dp.toPx(), cap = StrokeCap.Butt)
            )

            // Crisp White Outer Arc (60..150 PSI)
            drawArc(
                color = Color(0xFFE2E8F0),
                startAngle = startAngle + redSweep,
                sweepAngle = whiteSweep,
                useCenter = false,
                topLeft = arcTopLeft,
                size = arcSize,
                style = Stroke(width = 1.8.dp.toPx(), cap = StrokeCap.Butt)
            )

            // 4. Ticks (0, 25, 50, 75, 100, 125, 150 + minor subdivisions) & Numbers (50, 100, 150)
            val numPaint = Paint().apply {
                isAntiAlias = true
                color = android.graphics.Color.WHITE
                textSize = faceRadius * 0.165f
                textAlign = Paint.Align.CENTER
                typeface = Typeface.create(Typeface.DEFAULT, Typeface.BOLD_ITALIC)
            }

            // Minor ticks every 5 PSI
            for (psi in 0..150 step 5) {
                val frac = psi / 150f
                val angleDeg = startAngle + frac * totalSweep
                val angleRad = Math.toRadians(angleDeg.toDouble())

                val isMajor = (psi == 0 || psi == 50 || psi == 100 || psi == 150)
                val isMedium = (psi == 25 || psi == 75 || psi == 125)

                val tickLen = when {
                    isMajor -> faceRadius * 0.13f
                    isMedium -> faceRadius * 0.095f
                    else -> faceRadius * 0.055f
                }
                val tickStroke = when {
                    isMajor -> 2.2.dp.toPx()
                    isMedium -> 1.6.dp.toPx()
                    else -> 0.9.dp.toPx()
                }
                val tickColor = if (psi <= 55 && !isMajor) Color(0xFFEF4444) else Color(0xFFF8FAFC)

                val ox = center.x + (arcRadius * cos(angleRad)).toFloat()
                val oy = center.y + (arcRadius * sin(angleRad)).toFloat()
                val ix = center.x + ((arcRadius - tickLen) * cos(angleRad)).toFloat()
                val iy = center.y + ((arcRadius - tickLen) * sin(angleRad)).toFloat()

                drawLine(
                    color = tickColor,
                    start = Offset(ix, iy),
                    end = Offset(ox, oy),
                    strokeWidth = tickStroke,
                    cap = StrokeCap.Butt
                )

                // Major labels: 50, 100, 150 (with 100 offset slightly right so vertical needles at ~90-100 PSI never obscure it)
                if (psi == 50 || psi == 100 || psi == 150) {
                    val labelRadius = arcRadius - tickLen - faceRadius * 0.155f
                    val labelAngleDeg = if (psi == 100) angleDeg + 9.5f else angleDeg
                    val labelAngleRad = Math.toRadians(labelAngleDeg.toDouble())
                    val lx = center.x + (labelRadius * cos(labelAngleRad)).toFloat()
                    val ly = center.y + (labelRadius * sin(labelAngleRad)).toFloat() + numPaint.textSize * 0.35f
                    drawIntoCanvas { canvas ->
                        canvas.nativeCanvas.drawText(psi.toString(), lx, ly, numPaint)
                    }
                }
            }

            // 5. Offset "PSI" & "AIR" labels below/away from upper vertical needle path (90-100 PSI)
            val psiLabelPaint = Paint().apply {
                isAntiAlias = true
                color = android.graphics.Color.parseColor("#CBD5E1")
                textSize = faceRadius * 0.125f
                textAlign = Paint.Align.CENTER
                typeface = Typeface.DEFAULT_BOLD
            }
            val airLabelPaint = Paint().apply {
                isAntiAlias = true
                color = android.graphics.Color.parseColor("#94A3B8")
                textSize = faceRadius * 0.13f
                textAlign = Paint.Align.CENTER
                typeface = Typeface.DEFAULT_BOLD
            }

            drawIntoCanvas { canvas ->
                // Placed cleanly below the center pivot cap (like RPM / x100 on the left gauge)
                // so vertical needles at 85-115 PSI never overlap the text
                canvas.nativeCanvas.drawText("AIR", center.x, center.y + faceRadius * 0.30f, airLabelPaint)
                canvas.nativeCanvas.drawText("PSI", center.x, center.y + faceRadius * 0.44f, psiLabelPaint)
            }

            // 6. Authentic Cascadia Tank Icons:
            //    Left = White Primary Circuit (1), Right = Cyan-Blue Secondary Circuit (2)
            drawCascadiaTankIcon(
                center = Offset(center.x - faceRadius * 0.46f, center.y + faceRadius * 0.08f),
                circuitNumber = "1",
                iconColor = Color.White
            )
            drawCascadiaTankIcon(
                center = Offset(center.x + faceRadius * 0.56f, center.y + faceRadius * 0.16f),
                circuitNumber = "2",
                iconColor = Color(0xFF00B0FF)
            )

            // 7. Dual Digital PSI Readout Box at Bottom (White = Primary, Blue = Secondary)
            val boxW = faceRadius * 1.18f
            val boxH = faceRadius * 0.27f
            val boxTop = center.y + faceRadius * 0.52f
            val boxLeft = center.x - boxW / 2f

            drawRoundRect(
                color = Color(0xFF080B0F),
                topLeft = Offset(boxLeft, boxTop),
                size = Size(boxW, boxH),
                cornerRadius = CornerRadius(4.dp.toPx(), 4.dp.toPx())
            )
            val borderCol = if (isLowAir) Color(0xFFEF4444) else Color(0xFF263238)
            drawRoundRect(
                color = borderCol,
                topLeft = Offset(boxLeft, boxTop),
                size = Size(boxW, boxH),
                cornerRadius = CornerRadius(4.dp.toPx(), 4.dp.toPx()),
                style = Stroke(width = 1.2.dp.toPx())
            )

            val priPaint = Paint().apply {
                isAntiAlias = true
                color = android.graphics.Color.WHITE
                textSize = faceRadius * 0.13f
                textAlign = Paint.Align.CENTER
                typeface = Typeface.MONOSPACE
                isFakeBoldText = true
            }
            val secPaint = Paint().apply {
                isAntiAlias = true
                color = android.graphics.Color.parseColor("#29B6F6")
                textSize = faceRadius * 0.13f
                textAlign = Paint.Align.CENTER
                typeface = Typeface.MONOSPACE
                isFakeBoldText = true
            }

            drawIntoCanvas { canvas ->
                canvas.nativeCanvas.drawText(
                    "1:${animatedPrimary.toInt()}",
                    center.x - boxW * 0.24f,
                    boxTop + boxH * 0.72f,
                    priPaint
                )
                canvas.nativeCanvas.drawText(
                    "2:${animatedSecondary.toInt()}",
                    center.x + boxW * 0.24f,
                    boxTop + boxH * 0.72f,
                    secPaint
                )
            }

            // 8. TWO NEEDLES:
            //    First draw Secondary (Blue/Cyan) needle underneath
            val secAngleDeg = startAngle + (animatedSecondary / 150f) * totalSweep
            drawCascadiaNeedle(
                center = center,
                angleDeg = secAngleDeg,
                length = arcRadius - 3.dp.toPx(),
                needleColor = Color(0xFF00B0FF),
                coreHighlightColor = Color(0xFF80D8FF),
                baseWidthDp = 4.2f
            )

            //    Second draw Primary (White) needle on top
            val priAngleDeg = startAngle + (animatedPrimary / 150f) * totalSweep
            drawCascadiaNeedle(
                center = center,
                angleDeg = priAngleDeg,
                length = arcRadius - 6.dp.toPx(),
                needleColor = Color.White,
                coreHighlightColor = Color(0xFFF8FAFC),
                baseWidthDp = 3.2f
            )

            //    Shared Central Black Hub Cap
            drawCascadiaCenterHub(center)
        }
    }
}

/**
 * Draws the recessed dark metallic bezel ring of the Freightliner Cascadia 126 gauge pod.
 */
private fun DrawScope.drawCascadiaBezel(center: Offset, radius: Float) {
    val outerStroke = 5.5.dp.toPx()
    val bezelColors = listOf(
        Color(0xFF4A5262),
        Color(0xFF22262E),
        Color(0xFF5A6475),
        Color(0xFF181B20),
        Color(0xFF4A5262)
    )
    drawCircle(
        brush = Brush.sweepGradient(bezelColors, center),
        radius = radius,
        center = center,
        style = Stroke(width = outerStroke)
    )
    drawCircle(
        color = Color(0xFF090B0E),
        radius = radius - outerStroke * 0.55f,
        center = center,
        style = Stroke(width = 1.5.dp.toPx())
    )
}

/**
 * Draws a tapered, illuminated Freightliner Cascadia 126 pointer needle.
 */
private fun DrawScope.drawCascadiaNeedle(
    center: Offset,
    angleDeg: Float,
    length: Float,
    needleColor: Color,
    coreHighlightColor: Color,
    baseWidthDp: Float = 3.5f
) {
    val angleRad = Math.toRadians(angleDeg.toDouble())
    val perpRad = angleRad + Math.PI / 2.0

    val cosA = cos(angleRad).toFloat()
    val sinA = sin(angleRad).toFloat()
    val cosP = cos(perpRad).toFloat()
    val sinP = sin(perpRad).toFloat()

    val tailLen = 10.dp.toPx()
    val baseHalfW = (baseWidthDp * 0.5f).dp.toPx()
    val tipHalfW = 0.8.dp.toPx()

    val tailCenter = Offset(center.x - tailLen * cosA, center.y - tailLen * sinA)
    val tipCenter = Offset(center.x + length * cosA, center.y + length * sinA)

    // Drop Shadow Path
    val shadowOff = 2.dp.toPx()
    val shadowPath = Path().apply {
        moveTo(tailCenter.x + baseHalfW * cosP + shadowOff, tailCenter.y + baseHalfW * sinP + shadowOff)
        lineTo(tipCenter.x + tipHalfW * cosP + shadowOff, tipCenter.y + tipHalfW * sinP + shadowOff)
        lineTo(tipCenter.x - tipHalfW * cosP + shadowOff, tipCenter.y - tipHalfW * sinP + shadowOff)
        lineTo(tailCenter.x - baseHalfW * cosP + shadowOff, tailCenter.y - baseHalfW * sinP + shadowOff)
        close()
    }
    drawPath(path = shadowPath, color = Color(0x88000000))

    // Main Tapered Needle Body
    val needlePath = Path().apply {
        moveTo(tailCenter.x + baseHalfW * cosP, tailCenter.y + baseHalfW * sinP)
        lineTo(tipCenter.x + tipHalfW * cosP, tipCenter.y + tipHalfW * sinP)
        lineTo(tipCenter.x - tipHalfW * cosP, tipCenter.y - tipHalfW * sinP)
        lineTo(tailCenter.x - baseHalfW * cosP, tailCenter.y - baseHalfW * sinP)
        close()
    }
    drawPath(path = needlePath, color = needleColor)

    // Luminous spine line
    drawLine(
        color = coreHighlightColor,
        start = center,
        end = Offset(center.x + length * 0.88f * cosA, center.y + length * 0.88f * sinA),
        strokeWidth = 1.1.dp.toPx(),
        cap = StrokeCap.Round
    )
}

/**
 * Draws the dark matte Cascadia 126 center needle hub cap on top of all needles.
 */
private fun DrawScope.drawCascadiaCenterHub(center: Offset) {
    drawCircle(
        color = Color(0xCC05070A),
        radius = 12.dp.toPx(),
        center = center + Offset(1.2.dp.toPx(), 1.8.dp.toPx())
    )
    drawCircle(
        color = Color(0xFF3E4856),
        radius = 10.8.dp.toPx(),
        center = center
    )
    drawCircle(
        brush = Brush.radialGradient(
            colors = listOf(Color(0xFF2B323C), Color(0xFF12151A)),
            center = center - Offset(2.dp.toPx(), 2.dp.toPx()),
            radius = 9.5.dp.toPx()
        ),
        radius = 9.dp.toPx(),
        center = center
    )
    drawCircle(
        color = Color(0xFF64748B),
        radius = 2.2.dp.toPx(),
        center = center
    )
}

/**
 * Draws the Freightliner Cascadia 126 Air Circuit symbol `( 1 )` in White or `( 2 )` in Blue.
 */
private fun DrawScope.drawCascadiaTankIcon(
    center: Offset,
    circuitNumber: String,
    iconColor: Color
) {
    val circleRadius = 6.dp.toPx()
    val bracketRadius = 8.5.dp.toPx()

    // Inner tank circle
    drawCircle(
        color = iconColor,
        radius = circleRadius,
        center = center,
        style = Stroke(width = 1.3.dp.toPx())
    )

    // Left & Right brake shoe curved brackets
    val topLeft = Offset(center.x - bracketRadius, center.y - bracketRadius)
    val arcSize = Size(bracketRadius * 2f, bracketRadius * 2f)
    drawArc(
        color = iconColor,
        startAngle = 140f,
        sweepAngle = 80f,
        useCenter = false,
        topLeft = topLeft,
        size = arcSize,
        style = Stroke(width = 1.3.dp.toPx(), cap = StrokeCap.Round)
    )
    drawArc(
        color = iconColor,
        startAngle = 320f,
        sweepAngle = 80f,
        useCenter = false,
        topLeft = topLeft,
        size = arcSize,
        style = Stroke(width = 1.3.dp.toPx(), cap = StrokeCap.Round)
    )

    // Circuit Number "1" or "2"
    val numPaint = Paint().apply {
        isAntiAlias = true
        color = android.graphics.Color.argb(
            (iconColor.alpha * 255).toInt(),
            (iconColor.red * 255).toInt(),
            (iconColor.green * 255).toInt(),
            (iconColor.blue * 255).toInt()
        )
        textSize = 8.5.dp.toPx()
        textAlign = Paint.Align.CENTER
        typeface = Typeface.DEFAULT_BOLD
    }
    drawIntoCanvas { canvas ->
        canvas.nativeCanvas.drawText(circuitNumber, center.x, center.y + 3.dp.toPx(), numPaint)
    }
}

/**
 * Draws subtle authentic Cascadia 126 telltale icons inside the RPM tachometer face.
 */
private fun DrawScope.drawCascadiaRpmTelltales(
    center: Offset,
    faceRadius: Float,
    ignition: IgnitionState,
    rpm: Float
) {
    val isBulbCheck = (ignition == IgnitionState.ACC_ON)
    val amberCol = if (isBulbCheck) Color(0xFFFFB300) else Color(0xFF282C34)
    val redStopCol = if (isBulbCheck) Color(0xFFFF3B30) else Color(0xFF282C34)

    // Small Check Engine emblem (left-upper inner)
    val ceCenter = Offset(center.x - faceRadius * 0.30f, center.y - faceRadius * 0.12f)
    drawRoundRect(
        color = amberCol,
        topLeft = Offset(ceCenter.x - 6.dp.toPx(), ceCenter.y - 4.dp.toPx()),
        size = Size(12.dp.toPx(), 8.dp.toPx()),
        cornerRadius = CornerRadius(2.dp.toPx(), 2.dp.toPx()),
        style = Stroke(width = 1.3.dp.toPx())
    )

    // Small STOP Engine indicator (right inner, near 20-25)
    val stopCenter = Offset(center.x + faceRadius * 0.34f, center.y - faceRadius * 0.04f)
    drawCircle(
        color = redStopCol,
        radius = 5.5.dp.toPx(),
        center = stopCenter,
        style = Stroke(width = 1.3.dp.toPx())
    )
}

/**
 * Compact status text row placed directly above the gauges without bulky background containers.
 * Displays: "IGNITION: OFF | 0 RPM" on the left and "PRI: 93 • SEC: 90 PSI" on the right.
 */
@Composable
fun CompactDashboardStatusRow(
    physics: AirBrakeState,
    modifier: Modifier = Modifier
) {
    val (engineText, engineColor) = when (physics.ignition) {
        IgnitionState.OFF -> Pair("IGNITION: OFF", Color(0xFF90A4AE))
        IgnitionState.ACC_ON -> Pair("IGNITION: ACC", Color(0xFFFFCA28))
        IgnitionState.RUNNING -> Pair("IGNITION: RUN", Color(0xFF00E676))
    }

    val compTag = when {
        physics.ignition != IgnitionState.RUNNING -> ""
        physics.isCompressorBuilding -> " • COMP ⚡"
        physics.governorCutOutOccurred -> " • CUT-OUT ✓"
        else -> ""
    }

    Row(
        modifier = modifier
            .fillMaxWidth()
            .padding(horizontal = 4.dp, vertical = 1.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Row(
            verticalAlignment = Alignment.CenterVertically,
            modifier = Modifier.weight(1f, fill = false)
        ) {
            Box(
                modifier = Modifier
                    .size(6.dp)
                    .clip(CircleShape)
                    .background(engineColor)
            )
            Spacer(modifier = Modifier.width(4.dp))
            Text(
                text = "$engineText | ${physics.rpm} RPM$compTag",
                color = engineColor,
                fontSize = 9.5.sp,
                maxLines = 1,
                fontFamily = FontFamily.Monospace,
                fontWeight = FontWeight.ExtraBold
            )
        }

        // Compact PRI (White) / SEC (Blue) Legend
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(
                modifier = Modifier
                    .size(6.dp, 3.dp)
                    .background(Color.White, RoundedCornerShape(1.dp))
            )
            Spacer(modifier = Modifier.width(3.dp))
            Text(
                text = "PRI:${physics.primaryPsi.toInt()}",
                color = Color.White,
                fontSize = 9.5.sp,
                maxLines = 1,
                fontFamily = FontFamily.Monospace,
                fontWeight = FontWeight.Bold
            )
            Spacer(modifier = Modifier.width(6.dp))
            Box(
                modifier = Modifier
                    .size(6.dp, 3.dp)
                    .background(Color(0xFF00B0FF), RoundedCornerShape(1.dp))
            )
            Spacer(modifier = Modifier.width(3.dp))
            Text(
                text = "SEC:${physics.secondaryPsi.toInt()}",
                color = Color(0xFF29B6F6),
                fontSize = 9.5.sp,
                maxLines = 1,
                fontFamily = FontFamily.Monospace,
                fontWeight = FontWeight.Bold
            )
        }
    }
}

/**
 * Upgraded Digital Cockpit "DID" (Driver Information Display) positioned directly BETWEEN the two circular gauges.
 * Displays high-contrast digital status readout ("AIR SYSTEM: NORMAL / PRE-TRIP STEP 1/5") and flashes
 * when Low Air Warning (<60 PSI) is active.
 */
@Composable
fun CenterLowAirIndicator(
    isActive: Boolean,
    warningJustDeactivated: Boolean = false,
    lowestPsi: Float,
    stepShortName: String = "STEP 1/5",
    isCompressorBuilding: Boolean = false,
    governorCutOutOccurred: Boolean = false,
    modifier: Modifier = Modifier
) {
    val infiniteTransition = rememberInfiniteTransition(label = "CenterWarningBlink")
    val blinkAlpha by infiniteTransition.animateFloat(
        initialValue = 0.35f,
        targetValue = 1f,
        animationSpec = infiniteRepeatable(
            animation = tween(durationMillis = 320, easing = FastOutSlowInEasing),
            repeatMode = RepeatMode.Reverse
        ),
        label = "BlinkAlpha"
    )

    val boxBrush = when {
        isActive -> Brush.verticalGradient(
            colors = listOf(
                Color(0xFF991B1B).copy(alpha = blinkAlpha),
                Color(0xFF450A0A).copy(alpha = blinkAlpha)
            )
        )
        warningJustDeactivated -> Brush.verticalGradient(
            colors = listOf(Color(0xFF064E3B), Color(0xFF042F2E))
        )
        else -> Brush.verticalGradient(
            colors = listOf(Color(0xFF0D1520), Color(0xFF090E16))
        )
    }

    val outerBorderColor = when {
        isActive -> Color(0xFFFF5252)
        warningJustDeactivated -> Color(0xFF00E676)
        else -> Color(0xFF2D3B4E)
    }

    val sysStatusText = when {
        isActive -> "LOW AIR"
        warningJustDeactivated -> "WARN OFF"
        governorCutOutOccurred -> "CUT-OUT"
        isCompressorBuilding -> "BUILDING"
        lowestPsi >= 60f -> "NORMAL"
        else -> "LOW PSI"
    }

    val sysStatusColor = when {
        isActive -> Color(0xFFFFEB3B)
        warningJustDeactivated -> Color(0xFF69F0AE)
        lowestPsi >= 100f || governorCutOutOccurred -> Color(0xFF00E676)
        lowestPsi >= 60f -> Color(0xFF4ADE80)
        else -> Color(0xFFFFB300)
    }

    Column(
        modifier = modifier
            .height(94.dp)
            .clip(RoundedCornerShape(10.dp))
            .background(boxBrush)
            .border(1.5.dp, outerBorderColor, RoundedCornerShape(10.dp))
            .padding(horizontal = 6.dp, vertical = 5.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.SpaceBetween
    ) {
        // Top Header Bar inside DID Screen
        Row(
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.Center,
            modifier = Modifier.fillMaxWidth()
        ) {
            if (isActive || warningJustDeactivated) {
                Icon(
                    imageVector = if (warningJustDeactivated && !isActive) Icons.Default.CheckCircle else Icons.Default.Warning,
                    contentDescription = "Low Air Status",
                    tint = sysStatusColor,
                    modifier = Modifier.size(13.dp)
                )
                Spacer(modifier = Modifier.width(3.dp))
            }
            Text(
                text = if (isActive) "LOW AIR ALARM" else "DID • COCKPIT",
                color = if (isActive) Color(0xFFFFCDD2) else Color(0xFF94A3B8),
                fontSize = 9.sp,
                fontFamily = FontFamily.Monospace,
                fontWeight = FontWeight.Bold,
                letterSpacing = 0.6.sp,
                maxLines = 1
            )
        }

        // Center Primary High-Contrast Readout
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center
        ) {
            Text(
                text = "AIR SYSTEM:",
                color = if (isActive) Color.White else Color(0xFF38BDF8),
                fontSize = 10.sp,
                fontFamily = FontFamily.Monospace,
                fontWeight = FontWeight.ExtraBold,
                lineHeight = 11.sp,
                textAlign = TextAlign.Center
            )
            Text(
                text = sysStatusText,
                color = sysStatusColor,
                fontSize = 13.5.sp,
                fontFamily = FontFamily.Monospace,
                fontWeight = FontWeight.Black,
                lineHeight = 15.sp,
                textAlign = TextAlign.Center,
                maxLines = 1
            )
        }

        // Bottom Sub-Readout Pill
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(4.dp))
                .background(Color(0xFF060A0F).copy(alpha = 0.75f))
                .padding(vertical = 2.dp, horizontal = 3.dp),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = when {
                    isActive -> "⚠ <60 PSI (${lowestPsi.toInt()})"
                    warningJustDeactivated -> "✓ >60 PSI OK"
                    else -> "PRE-TRIP $stepShortName"
                },
                color = when {
                    isActive -> Color(0xFFFF8A80)
                    warningJustDeactivated -> Color(0xFF69F0AE)
                    else -> Color(0xFF7DD3FC)
                },
                fontSize = 9.5.sp,
                fontFamily = FontFamily.Monospace,
                fontWeight = FontWeight.ExtraBold,
                textAlign = TextAlign.Center,
                maxLines = 1
            )
        }
    }
}

