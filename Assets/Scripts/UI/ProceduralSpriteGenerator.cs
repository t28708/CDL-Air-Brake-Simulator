using UnityEngine;

namespace CDL.AirBrakeSimulator
{
    /// <summary>
    /// Generates high-resolution procedural textures and UI sprites for the CDL simulator:
    /// - Multi-Truck Dashboards (Peterbilt 389 Classic Walnut vs Freightliner Cascadia Carbon)
    /// - Multi-Truck Circular Gauge Faces (Classic Cream/Chrome vs Modern Digital/Cyan)
    /// - Multi-Truck Needles (Vintage Chrome-Cap vs Modern Fluorescent)
    /// - Pushed & Popped Valve Knobs for Yellow Diamond and Red Octagon
    /// - Industrial Treaded Foot Brake Pedal
    /// - Low Air Warning Annunciator Lamp
    /// </summary>
    public static class ProceduralSpriteGenerator
    {
        #region Gauge Dials

        public static Texture2D CreateGaugeDialTexture(int size = 512, bool isModern = false)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.46f;
            float innerRadius = size * 0.43f;
            float tickInnerRadius = size * 0.35f;

            // Theme colors
            Color backgroundCol = isModern ? new Color(0.06f, 0.08f, 0.10f, 1f) : new Color(0.13f, 0.14f, 0.16f, 1f);
            Color bezelCol = isModern ? new Color(0.20f, 0.45f, 0.65f, 1f) : new Color(0.65f, 0.68f, 0.72f, 1f); // Modern Cyan/Titanium vs Classic Chrome
            Color tickCol = isModern ? new Color(0.85f, 0.95f, 1.0f, 1f) : new Color(0.95f, 0.95f, 0.96f, 1f);
            Color redArcCol = new Color(0.95f, 0.2f, 0.2f, 0.9f);
            Color greenArcCol = isModern ? new Color(0.0f, 0.9f, 0.95f, 0.9f) : new Color(0.2f, 0.85f, 0.35f, 0.9f); // Modern Cyan safe glow vs Classic Green

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float dist = Vector2.Distance(pos, center);
                    int idx = y * size + x;

                    if (dist > radius + 2f)
                    {
                        colors[idx] = Color.clear;
                    }
                    else if (dist > radius - 2f)
                    {
                        float alpha = Mathf.Clamp01(radius + 2f - dist);
                        colors[idx] = new Color(bezelCol.r, bezelCol.g, bezelCol.b, alpha);
                    }
                    else if (dist > innerRadius)
                    {
                        colors[idx] = bezelCol;
                    }
                    else
                    {
                        colors[idx] = backgroundCol;

                        Vector2 dir = (pos - center).normalized;
                        float angle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

                        // Sweep: -135 to +135 deg = 0 to 150 PSI
                        if (angle >= -135f && angle <= 135f)
                        {
                            float psi = Mathf.Lerp(0f, 150f, Mathf.InverseLerp(-135f, 135f, angle));

                            if (dist > innerRadius - 14f && dist <= innerRadius)
                            {
                                if (psi <= 60f)
                                {
                                    colors[idx] = redArcCol;
                                }
                                else if (psi >= 90f && psi <= 125f)
                                {
                                    colors[idx] = greenArcCol;
                                }
                            }

                            if (dist >= tickInnerRadius && dist <= innerRadius - 16f)
                            {
                                float nearestMinor = Mathf.Round(psi / 10f) * 10f;
                                float angleOfTick = Mathf.Lerp(-135f, 135f, nearestMinor / 150f);
                                float angleDiff = Mathf.Abs(Mathf.DeltaAngle(angle, angleOfTick));

                                bool isMajor = Mathf.Approximately(nearestMinor % 30f, 0f);
                                float tickThickness = isMajor ? 1.4f : 0.8f;
                                float currentTickInner = isMajor ? tickInnerRadius : (tickInnerRadius + 14f);

                                if (dist >= currentTickInner && angleDiff < (tickThickness * (256f / size)))
                                {
                                    colors[idx] = tickCol;
                                }
                            }
                        }
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        public static Sprite CreateGaugeDialSprite(int size = 512, bool isModern = false)
        {
            Texture2D tex = CreateGaugeDialTexture(size, isModern);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        #endregion

        #region Needles

        public static Texture2D CreateNeedleTexture(int width = 48, int height = 256, bool isModern = false)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] colors = new Color[width * height];
            float centerX = width * 0.5f;

            float pivotY = height * 0.15f;
            Color needleCol = isModern ? new Color(1.0f, 0.45f, 0.05f, 1f) : new Color(0.92f, 0.22f, 0.18f, 1f);
            Color hubCol = isModern ? new Color(0.12f, 0.14f, 0.18f, 1f) : new Color(0.6f, 0.62f, 0.66f, 1f); // Modern stealth vs Classic Chrome
            Color tipCol = isModern ? new Color(0.2f, 0.9f, 1.0f, 1f) : new Color(1.0f, 0.95f, 0.3f, 1f);

            for (int y = 0; y < height; y++)
            {
                float tY = (float)(y - pivotY) / (height - pivotY);
                float halfWidthAtY = Mathf.Lerp(width * 0.18f, 1.2f, Mathf.Clamp01(tY));

                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    float distFromCenter = Mathf.Abs(x - centerX);
                    float distFromPivot = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, pivotY));

                    if (distFromPivot <= width * 0.42f)
                    {
                        colors[idx] = hubCol;
                    }
                    else if (y >= pivotY && y < height - 4 && distFromCenter <= halfWidthAtY)
                    {
                        if (distFromCenter < 1.0f)
                        {
                            colors[idx] = (tY > 0.8f) ? tipCol : Color.white;
                        }
                        else
                        {
                            colors[idx] = needleCol;
                        }
                    }
                    else if (y < pivotY && y >= pivotY - (width * 0.35f) && distFromCenter <= width * 0.12f)
                    {
                        colors[idx] = needleCol;
                    }
                    else
                    {
                        colors[idx] = Color.clear;
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        public static Sprite CreateNeedleSprite(int width = 48, int height = 256, bool isModern = false)
        {
            Texture2D tex = CreateNeedleTexture(width, height, isModern);
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.15f), 100f);
        }

        #endregion

        #region Dashboards (Woodgrain & Carbon Fiber)

        public static Texture2D CreateWoodgrainDashboardTexture(int width = 256, int height = 256)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] colors = new Color[width * height];

            Color darkWalnut = new Color(0.18f, 0.11f, 0.08f, 1f);
            Color richAmber = new Color(0.28f, 0.17f, 0.11f, 1f);
            Color deepGrain = new Color(0.12f, 0.07f, 0.05f, 1f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Simulated natural wood grain streaks along horizontal axis
                    float streak = Mathf.Sin(y * 0.12f + Mathf.PerlinNoise(x * 0.04f, y * 0.04f) * 8f);
                    Color col = (streak > 0.3f) ? richAmber : ((streak < -0.3f) ? deepGrain : darkWalnut);
                    colors[y * width + x] = col;
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        public static Sprite CreateWoodgrainDashboardSprite(int width = 256, int height = 256)
        {
            Texture2D tex = CreateWoodgrainDashboardTexture(width, height);
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        public static Texture2D CreateCarbonFiberDashboardTexture(int width = 256, int height = 256)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] colors = new Color[width * height];

            Color c1 = new Color(0.11f, 0.12f, 0.14f, 1f);
            Color c2 = new Color(0.16f, 0.18f, 0.20f, 1f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 2x2 twill carbon weave pattern
                    bool isCross = ((x / 4) % 2 == (y / 4) % 2);
                    colors[y * width + x] = isCross ? c1 : c2;
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        public static Sprite CreateCarbonFiberDashboardSprite(int width = 256, int height = 256)
        {
            Texture2D tex = CreateCarbonFiberDashboardTexture(width, height);
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        #endregion

        #region Valve Knobs (Pushed vs Popped)

        public static Texture2D CreateYellowDiamondTexture(int size = 256, bool pushed = false, bool isModern = false)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = pushed ? size * 0.41f : size * 0.45f; // Pushed has inset depth

            Color yellowBody = pushed ? new Color(0.85f, 0.70f, 0.10f, 1f) : new Color(0.98f, 0.82f, 0.15f, 1f);
            Color borderCol = isModern ? (pushed ? new Color(0.2f, 0.8f, 0.3f, 1f) : new Color(0.9f, 0.2f, 0.2f, 1f)) : new Color(0.12f, 0.12f, 0.14f, 1f);
            Color centerCap = pushed ? new Color(0.65f, 0.52f, 0.08f, 1f) : new Color(0.85f, 0.70f, 0.10f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int idx = y * size + x;
                    float manhattan = Mathf.Abs(x - center.x) + Mathf.Abs(y - center.y);

                    if (manhattan > radius)
                    {
                        colors[idx] = Color.clear;
                    }
                    else if (manhattan > radius - 14f)
                    {
                        colors[idx] = borderCol;
                    }
                    else if (manhattan > radius - 20f)
                    {
                        colors[idx] = pushed ? Color.black * 0.4f : Color.white * 0.9f;
                    }
                    else
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), center);
                        if (dist < size * 0.22f)
                        {
                            colors[idx] = (dist < size * 0.19f) ? centerCap : borderCol;
                        }
                        else
                        {
                            colors[idx] = yellowBody;
                        }
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        public static Sprite CreateYellowDiamondSprite(int size = 256, bool pushed = false, bool isModern = false)
        {
            Texture2D tex = CreateYellowDiamondTexture(size, pushed, isModern);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        public static Texture2D CreateRedOctagonTexture(int size = 256, bool pushed = false, bool isModern = false)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float r = pushed ? size * 0.41f : size * 0.45f;

            Color redBody = pushed ? new Color(0.72f, 0.14f, 0.14f, 1f) : new Color(0.88f, 0.18f, 0.18f, 1f);
            Color whiteBorder = isModern ? (pushed ? new Color(0.2f, 0.85f, 0.35f, 1f) : new Color(1.0f, 0.3f, 0.3f, 1f)) : new Color(0.96f, 0.96f, 0.98f, 1f);
            Color darkCap = pushed ? new Color(0.50f, 0.08f, 0.08f, 1f) : new Color(0.70f, 0.12f, 0.12f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int idx = y * size + x;
                    float dx = Mathf.Abs(x - center.x);
                    float dy = Mathf.Abs(y - center.y);

                    float octDist = Mathf.Max(Mathf.Max(dx, dy), (dx + dy) * 0.7071f);

                    if (octDist > r)
                    {
                        colors[idx] = Color.clear;
                    }
                    else if (octDist > r - 12f)
                    {
                        colors[idx] = whiteBorder;
                    }
                    else
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), center);
                        if (dist < size * 0.22f)
                        {
                            colors[idx] = (dist < size * 0.19f) ? darkCap : whiteBorder;
                        }
                        else
                        {
                            colors[idx] = redBody;
                        }
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        public static Sprite CreateRedOctagonSprite(int size = 256, bool pushed = false, bool isModern = false)
        {
            Texture2D tex = CreateRedOctagonTexture(size, pushed, isModern);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        #endregion

        #region Pedal & Warning Lamp

        public static Texture2D CreateBrakePedalTexture(int width = 256, int height = 384)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] colors = new Color[width * height];

            Color rubberBase = new Color(0.18f, 0.19f, 0.21f, 1f);
            Color rubberRib = new Color(0.32f, 0.34f, 0.38f, 1f);
            Color metallicRim = new Color(0.6f, 0.62f, 0.65f, 1f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    float borderDist = Mathf.Min(x, Mathf.Min(width - 1 - x, Mathf.Min(y, height - 1 - y)));
                    if (borderDist < 2)
                    {
                        colors[idx] = Color.clear;
                    }
                    else if (borderDist < 12)
                    {
                        colors[idx] = metallicRim;
                    }
                    else
                    {
                        bool isRib = (y % 28 >= 14 && y % 28 <= 22) && (x > 24 && x < width - 24);
                        colors[idx] = isRib ? rubberRib : rubberBase;
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        public static Sprite CreateBrakePedalSprite(int width = 256, int height = 384)
        {
            Texture2D tex = CreateBrakePedalTexture(width, height);
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        public static Texture2D CreateWarningLampTexture(int size = 128)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.44f;

            Color glowRed = new Color(1.0f, 0.15f, 0.15f, 1f);
            Color bezelCol = new Color(0.25f, 0.25f, 0.28f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int idx = y * size + x;
                    float dist = Vector2.Distance(new Vector2(x, y), center);

                    if (dist > radius)
                    {
                        colors[idx] = Color.clear;
                    }
                    else if (dist > radius - 8f)
                    {
                        colors[idx] = bezelCol;
                    }
                    else
                    {
                        float t = 1f - Mathf.Clamp01(dist / (radius - 8f));
                        colors[idx] = Color.Lerp(glowRed, Color.white, t * t);
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        public static Sprite CreateWarningLampSprite(int size = 128)
        {
            Texture2D tex = CreateWarningLampTexture(size);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        #endregion
    }
}
