using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Крошечный 2D-растеризатор: рисует иконки, спрайты и текстуры прямо в рантайме.
    /// Нужен, чтобы игра не зависела от внешних графических ассетов.
    /// </summary>
    public class PixelCanvas
    {
        public readonly int Width;
        public readonly int Height;
        private readonly Color[] _pixels;

        public PixelCanvas(int width, int height)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            _pixels = new Color[Width * Height];
            Clear(Color.clear);
        }

        public void Clear(Color color)
        {
            for (int i = 0; i < _pixels.Length; i++) _pixels[i] = color;
        }

        public void Set(int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            _pixels[y * Width + x] = color;
        }

        public Color Get(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return Color.clear;
            return _pixels[y * Width + x];
        }

        public void Blend(int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            Color dst = _pixels[y * Width + x];
            float a = Mathf.Clamp01(color.a);
            _pixels[y * Width + x] = new Color(
                color.r * a + dst.r * (1f - a),
                color.g * a + dst.g * (1f - a),
                color.b * a + dst.b * (1f - a),
                a + dst.a * (1f - a));
        }

        public void FillRect(int x, int y, int w, int h, Color color)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                    Blend(i, j, color);
        }

        /// <summary>Версия с дробными координатами (округление до пикселя).</summary>
        public void FillRect(float x, float y, float w, float h, Color color)
        {
            FillRect(Mathf.RoundToInt(x), Mathf.RoundToInt(y), Mathf.RoundToInt(w), Mathf.RoundToInt(h), color);
        }

        public void StrokeRect(int x, int y, int w, int h, int thickness, Color color)
        {
            for (int t = 0; t < thickness; t++)
            {
                for (int i = x + t; i < x + w - t; i++)
                {
                    Blend(i, y + t, color);
                    Blend(i, y + h - 1 - t, color);
                }
                for (int j = y + t; j < y + h - t; j++)
                {
                    Blend(x + t, j, color);
                    Blend(x + w - 1 - t, j, color);
                }
            }
        }

        /// <summary>Версия с дробными координатами (округление до пикселя).</summary>
        public void StrokeRect(float x, float y, float w, float h, float thickness, Color color)
        {
            StrokeRect(Mathf.RoundToInt(x), Mathf.RoundToInt(y), Mathf.RoundToInt(w), Mathf.RoundToInt(h),
                       Mathf.RoundToInt(thickness), color);
        }

        public void FillCircle(float cx, float cy, float radius, Color color)
        {
            int x0 = Mathf.FloorToInt(cx - radius - 1), x1 = Mathf.CeilToInt(cx + radius + 1);
            int y0 = Mathf.FloorToInt(cy - radius - 1), y1 = Mathf.CeilToInt(cy + radius + 1);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float cov = Mathf.Clamp01(radius - d + 0.5f);
                    if (cov > 0.001f) Blend(x, y, new Color(color.r, color.g, color.b, color.a * cov));
                }
            }
        }

        public void StrokeCircle(float cx, float cy, float radius, float thickness, Color color)
        {
            float inner = Mathf.Max(0f, radius - thickness);
            int x0 = Mathf.FloorToInt(cx - radius - 1), x1 = Mathf.CeilToInt(cx + radius + 1);
            int y0 = Mathf.FloorToInt(cy - radius - 1), y1 = Mathf.CeilToInt(cy + radius + 1);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float cov = Mathf.Clamp01(radius - d + 0.5f) - Mathf.Clamp01(inner - d + 0.5f);
                    if (cov > 0.001f) Blend(x, y, new Color(color.r, color.g, color.b, color.a * cov));
                }
            }
        }

        /// <summary>Круг с плавным затуханием к краям (свечение).</summary>
        public void FillRadialGlow(float cx, float cy, float radius, Color color)
        {
            int x0 = Mathf.FloorToInt(cx - radius - 1), x1 = Mathf.CeilToInt(cx + radius + 1);
            int y0 = Mathf.FloorToInt(cy - radius - 1), y1 = Mathf.CeilToInt(cy + radius + 1);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Max(0.001f, radius);
                    if (d > 1f) continue;
                    float falloff = Mathf.Pow(1f - d, 2.2f);
                    Blend(x, y, new Color(color.r, color.g, color.b, color.a * falloff));
                }
            }
        }

        public void FillEllipse(float cx, float cy, float rx, float ry, Color color)
        {
            int x0 = Mathf.FloorToInt(cx - rx - 1), x1 = Mathf.CeilToInt(cx + rx + 1);
            int y0 = Mathf.FloorToInt(cy - ry - 1), y1 = Mathf.CeilToInt(cy + ry + 1);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - cx) / Mathf.Max(0.001f, rx);
                    float dy = (y + 0.5f - cy) / Mathf.Max(0.001f, ry);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float cov = Mathf.Clamp01(1f - d);
                    if (cov > 0.001f) Blend(x, y, new Color(color.r, color.g, color.b, color.a * cov));
                }
            }
        }

        public void FillRoundedRect(int x, int y, int w, int h, int radius, Color color)
        {
            radius = Mathf.Clamp(radius, 0, Mathf.Min(w, h) / 2);
            for (int j = y; j < y + h; j++)
            {
                for (int i = x; i < x + w; i++)
                {
                    float px = i + 0.5f, py = j + 0.5f;
                    float cx = Mathf.Clamp(px, x + radius, x + w - radius);
                    float cy = Mathf.Clamp(py, y + radius, y + h - radius);
                    float dx = px - cx, dy = py - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float cov = Mathf.Clamp01(radius - d + 0.5f);
                    if (cov > 0.001f) Blend(i, j, new Color(color.r, color.g, color.b, color.a * cov));
                }
            }
        }

        /// <summary>Версия с дробными координатами (округление до пикселя).</summary>
        public void FillRoundedRect(float x, float y, float w, float h, float radius, Color color)
        {
            FillRoundedRect(Mathf.RoundToInt(x), Mathf.RoundToInt(y), Mathf.RoundToInt(w), Mathf.RoundToInt(h),
                            Mathf.RoundToInt(radius), color);
        }

        public void StrokeRoundedRect(int x, int y, int w, int h, int radius, int thickness, Color color)
        {
            radius = Mathf.Clamp(radius, 0, Mathf.Min(w, h) / 2);
            for (int j = y; j < y + h; j++)
            {
                for (int i = x; i < x + w; i++)
                {
                    float px = i + 0.5f, py = j + 0.5f;
                    float cx = Mathf.Clamp(px, x + radius, x + w - radius);
                    float cy = Mathf.Clamp(py, y + radius, y + h - radius);
                    float dx = px - cx, dy = py - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float cov = Mathf.Clamp01(radius - d + 0.5f) - Mathf.Clamp01(radius - thickness - d + 0.5f);
                    if (cov > 0.001f) Blend(i, j, new Color(color.r, color.g, color.b, color.a * cov));
                }
            }
        }

        /// <summary>Версия с дробными координатами (округление до пикселя).</summary>
        public void StrokeRoundedRect(float x, float y, float w, float h, float radius, float thickness, Color color)
        {
            StrokeRoundedRect(Mathf.RoundToInt(x), Mathf.RoundToInt(y), Mathf.RoundToInt(w), Mathf.RoundToInt(h),
                              Mathf.RoundToInt(radius), Mathf.RoundToInt(thickness), color);
        }

        /// <summary>Заливает многоугольник (веерный алгоритм).</summary>
        public void FillPolygon(Vector2[] points, Color color)
        {
            if (points == null || points.Length < 3) return;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < points.Length; i++)
            {
                minY = Mathf.Min(minY, points[i].y);
                maxY = Mathf.Max(maxY, points[i].y);
            }
            int y0 = Mathf.FloorToInt(minY), y1 = Mathf.CeilToInt(maxY);
            var xs = new System.Collections.Generic.List<float>();
            for (int y = y0; y <= y1; y++)
            {
                float py = y + 0.5f;
                xs.Clear();
                for (int i = 0; i < points.Length; i++)
                {
                    Vector2 a = points[i];
                    Vector2 b = points[(i + 1) % points.Length];
                    if ((a.y <= py && b.y > py) || (b.y <= py && a.y > py))
                    {
                        float t = (py - a.y) / (b.y - a.y);
                        xs.Add(a.x + t * (b.x - a.x));
                    }
                }
                xs.Sort();
                for (int i = 0; i + 1 < xs.Count; i += 2)
                {
                    int sx = Mathf.RoundToInt(xs[i]);
                    int ex = Mathf.RoundToInt(xs[i + 1]);
                    for (int x = sx; x <= ex; x++) Blend(x, y, color);
                }
            }
        }

        public void FillLine(Vector2 from, Vector2 to, float thickness, Color color)
        {
            float dx = to.x - from.x, dy = to.y - from.y;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 0.0001f) return;
            int steps = Mathf.CeilToInt(len * 2f);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                FillCircle(Mathf.Lerp(from.x, to.x, t), Mathf.Lerp(from.y, to.y, t), thickness * 0.5f, color);
            }
        }

        public Texture2D ToTexture()
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels(_pixels);
            tex.Apply();
            return tex;
        }

        public Sprite ToSprite(Vector4? border = null)
        {
            Texture2D tex = ToTexture();
            if (border.HasValue)
                return Sprite.Create(tex, new Rect(0, 0, Width, Height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border.Value);
            return Sprite.Create(tex, new Rect(0, 0, Width, Height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
