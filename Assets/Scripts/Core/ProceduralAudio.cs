using System.Collections.Generic;
using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Синтез музыки и звуков прямо в рантайме.
    /// Никаких mp3 — треки генерируются из нот, барабанов и винила, поэтому проект весит копейки
    /// и сразу звучит «как надо».
    /// </summary>
    public static class ProceduralAudio
    {
        public enum Style { Menu, City, Settlement, Office, Wheatfield }

        public enum Sfx
        {
            Shoot, ShootEmpty, Impact, KiwiHit, KiwiDie, Pickup,
            UiClick, UiBack, Win, Lose, Hurt, Reload, Jump
        }

        private const int SampleRate = 44100;

        private static readonly Dictionary<Style, AudioClip> MusicCache = new Dictionary<Style, AudioClip>();
        private static readonly Dictionary<Sfx, AudioClip> SfxCache = new Dictionary<Sfx, AudioClip>();

        // ---------------------------------------------------------------
        //  Музыка
        // ---------------------------------------------------------------

        /// <summary>Музыкальный стиль для темы уровня.</summary>
        public static Style StyleFor(LevelTheme theme)
        {
            switch (theme)
            {
                case LevelTheme.Settlement: return Style.Settlement;
                case LevelTheme.Office: return Style.Office;
                case LevelTheme.Wheatfield: return Style.Wheatfield;
                default: return Style.City;
            }
        }

        /// <summary>
        /// Внешний трек из Resources (например «Music/Wheatfield»).
        /// Если файла нет — возвращается null, и используется процедурная музыка.
        ///
        /// СОМНЕНИЕ: про «Хлеб» из фильма — это чужой трек, и его нельзя просто так
        /// вшить в игру. Поэтому сделал так: положи свой файл в
        /// Assets/Resources/Music/Wheatfield (mp3/ogg/wav) — он автоматически
        /// заменит процедурную музыку на 4-м уровне. Без файла играет оригинальная
        /// процедурная баллада в том же духе.
        /// </summary>
        public static AudioClip GetExternalMusic(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            try
            {
                return Resources.Load<AudioClip>(resourcePath);
            }
            catch
            {
                return null;
            }
        }

        public static AudioClip GetMusic(Style style)
        {
            AudioClip clip;
            if (MusicCache.TryGetValue(style, out clip) && clip != null) return clip;
            clip = BuildMusic(style);
            MusicCache[style] = clip;
            return clip;
        }

        public static AudioClip GetSfx(Sfx sfx)
        {
            AudioClip clip;
            if (SfxCache.TryGetValue(sfx, out clip) && clip != null) return clip;
            clip = BuildSfx(sfx);
            SfxCache[sfx] = clip;
            return clip;
        }

        private struct Chord
        {
            public int Root;
            public int[] Intervals;
            public Chord(int root, int[] intervals) { Root = root; Intervals = intervals; }
        }

        private static AudioClip BuildMusic(Style style)
        {
            int bpm;
            int bars = 8;
            float warmth;      // насколько тёмный фильтр
            float drumLevel;
            float vinylLevel;
            float melodyLevel;
            Chord[] progression;

            switch (style)
            {
                case Style.Menu:
                    bpm = 78; warmth = 0.62f; drumLevel = 0.55f; vinylLevel = 0.9f; melodyLevel = 0.85f;
                    progression = new[]
                    {
                        new Chord(57, new[] { 0, 3, 7, 10 }),
                        new Chord(53, new[] { 0, 4, 7, 11 }),
                        new Chord(48, new[] { 0, 4, 7, 11 }),
                        new Chord(55, new[] { 0, 4, 7, 10 }),
                    };
                    break;
                case Style.City:
                    bpm = 94; warmth = 0.45f; drumLevel = 0.8f; vinylLevel = 0.25f; melodyLevel = 0.7f;
                    progression = new[]
                    {
                        new Chord(45, new[] { 0, 3, 7 }),
                        new Chord(41, new[] { 0, 4, 7 }),
                        new Chord(48, new[] { 0, 4, 7 }),
                        new Chord(43, new[] { 0, 4, 7 }),
                    };
                    break;
                case Style.Settlement:
                    bpm = 86; warmth = 0.72f; drumLevel = 0.45f; vinylLevel = 0.6f; melodyLevel = 0.95f;
                    progression = new[]
                    {
                        new Chord(50, new[] { 0, 3, 7 }),
                        new Chord(46, new[] { 0, 4, 7 }),
                        new Chord(41, new[] { 0, 4, 7 }),
                        new Chord(45, new[] { 0, 4, 7 }),
                    };
                    break;
                case Style.Wheatfield:
                    // Тёплая, тихая народная баллада: мягкий «винил», почти без барабанов,
                    // много струнных и медленная мелодия — «хлебное» настроение.
                    bpm = 72; warmth = 0.82f; drumLevel = 0.16f; vinylLevel = 0.85f; melodyLevel = 0.8f;
                    progression = new[]
                    {
                        new Chord(57, new[] { 0, 4, 7 }),
                        new Chord(54, new[] { 0, 4, 7 }),
                        new Chord(50, new[] { 0, 4, 7 }),
                        new Chord(55, new[] { 0, 4, 7 }),
                    };
                    break;
                default:
                    bpm = 104; warmth = 0.35f; drumLevel = 0.9f; vinylLevel = 0.05f; melodyLevel = 0.35f;
                    progression = new[]
                    {
                        new Chord(45, new[] { 0, 3, 7 }),
                        new Chord(44, new[] { 0, 4, 7 }),
                        new Chord(41, new[] { 0, 3, 7 }),
                        new Chord(40, new[] { 0, 4, 7 }),
                    };
                    break;
            }

            float beat = 60f / bpm;
            float barDur = beat * 4f;
            float duration = barDur * bars;
            int total = Mathf.CeilToInt(duration * SampleRate);
            var buffer = new float[total];

            for (int bar = 0; bar < bars; bar++)
            {
                Chord chord = progression[bar % progression.Length];
                float barStart = bar * barDur;

                // --- пад (аккорд) ---
                for (int n = 0; n < chord.Intervals.Length; n++)
                {
                    float freq = MidiToFreq(chord.Root + chord.Intervals[n]);
                    AddPad(buffer, barStart, barDur * 1.02f, freq, 0.17f, 0.45f);
                }

                // --- бас ---
                float bassFreq = MidiToFreq(chord.Root - 12);
                AddBass(buffer, barStart, bassFreq, beat * 1.5f, 0.34f);
                AddBass(buffer, barStart + beat * 2.5f, bassFreq, beat * 0.75f, 0.24f);
                if (style == Style.Office)
                    AddBass(buffer, barStart + beat * 3.5f, bassFreq, beat * 0.5f, 0.2f);

                // --- мелодия (пентатоника) ---
                int[] scale = { 0, 3, 5, 7, 10 };
                for (int s = 0; s < 4; s++)
                {
                    if (style != Style.Menu && style != Style.Settlement && (bar + s) % 2 == 1) continue;
                    int deg = (bar * 3 + s * 2) % scale.Length;
                    int octave = ((bar + s) % 3 == 0) ? 12 : 0;
                    float note = MidiToFreq(chord.Root + 12 + scale[deg] + octave);
                    AddPluck(buffer, barStart + s * beat, note, beat * 1.4f, 0.16f * melodyLevel);
                }

                // --- барабаны ---
                AddKick(buffer, barStart, 0.55f * drumLevel);
                AddKick(buffer, barStart + beat * 2.5f, 0.40f * drumLevel);
                AddSnare(buffer, barStart + beat, 0.30f * drumLevel);
                AddSnare(buffer, barStart + beat * 3, 0.32f * drumLevel);
                for (int h = 0; h < 8; h++)
                {
                    float accent = (h % 2 == 0) ? 0.16f : 0.09f;
                    AddHat(buffer, barStart + h * beat * 0.5f, accent * drumLevel, h == 7);
                }
            }

            // --- винил и шум ---
            if (vinylLevel > 0.01f)
                AddVinyl(buffer, vinylLevel);

            // --- тёплый фильтр и реверберация ---
            LowPass(buffer, warmth);
            float[] wet = Reverb(buffer, 0.28f, 0.16f);
            for (int i = 0; i < total; i++) buffer[i] = Mathf.Clamp(buffer[i] * 0.72f + wet[i], -1f, 1f);

            Normalize(buffer, 0.86f);

            // --- стерео-ширина ---
            float[] data = new float[total * 2];
            int haas = (int)(SampleRate * 0.012f);
            for (int i = 0; i < total; i++)
            {
                data[i * 2] = buffer[i];
                data[Mathf.Max(0, i - haas) * 2 + 1] = buffer[Mathf.Max(0, i - haas)] * 0.85f;
            }

            AudioClip clip = AudioClip.Create("KiviMusic_" + style, total, 2, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float MidiToFreq(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        private static void AddPad(float[] buf, float start, float dur, float freq, float amp, float detune)
        {
            int startIdx = Mathf.Max(0, (int)(start * SampleRate));
            int len = (int)(dur * SampleRate);
            float attack = 0.35f, release = 0.45f;
            for (int i = 0; i < len; i++)
            {
                int idx = startIdx + i;
                if (idx >= buf.Length) break;
                float t = i / (float)SampleRate;
                float p = i / (float)len;
                float env = Mathf.Min(1f, p / attack) * Mathf.Min(1f, (1f - p) / release);
                float f = freq * (1f + detune * 0.004f);
                float s = Mathf.Sin(2f * Mathf.PI * f * t) * 0.62f
                          + Mathf.Sin(2f * Mathf.PI * f * 2.001f * t) * 0.16f
                          + Mathf.Sin(2f * Mathf.PI * f * 1.003f * t) * 0.34f;
                buf[idx] += s * env * amp;
            }
        }

        private static void AddBass(float[] buf, float start, float freq, float dur, float amp)
        {
            int startIdx = Mathf.Max(0, (int)(start * SampleRate));
            int len = (int)(dur * SampleRate);
            for (int i = 0; i < len; i++)
            {
                int idx = startIdx + i;
                if (idx >= buf.Length) break;
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 5.5f) * Mathf.Min(1f, t * 120f);
                float s = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.85f
                          + Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.15f;
                buf[idx] += s * env * amp;
            }
        }

        private static void AddPluck(float[] buf, float start, float freq, float dur, float amp)
        {
            int startIdx = Mathf.Max(0, (int)(start * SampleRate));
            int len = (int)(dur * SampleRate);
            for (int i = 0; i < len; i++)
            {
                int idx = startIdx + i;
                if (idx >= buf.Length) break;
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 6.5f) * Mathf.Min(1f, t * 200f);
                float s = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f
                          + Mathf.Sin(2f * Mathf.PI * freq * 3f * t) * 0.2f
                          + Mathf.Sin(2f * Mathf.PI * freq * 5f * t) * 0.08f;
                buf[idx] += s * env * amp;
            }
        }

        private static void AddKick(float[] buf, float start, float amp)
        {
            int startIdx = Mathf.Max(0, (int)(start * SampleRate));
            int len = (int)(0.28f * SampleRate);
            for (int i = 0; i < len; i++)
            {
                int idx = startIdx + i;
                if (idx >= buf.Length) break;
                float t = i / (float)SampleRate;
                float f = 120f * Mathf.Exp(-t * 22f) + 44f;
                float env = Mathf.Exp(-t * 12f);
                buf[idx] += Mathf.Sin(2f * Mathf.PI * f * t) * env * amp;
            }
        }

        private static void AddSnare(float[] buf, float start, float amp)
        {
            int startIdx = Mathf.Max(0, (int)(start * SampleRate));
            int len = (int)(0.22f * SampleRate);
            uint seed = 1337u;
            for (int i = 0; i < len; i++)
            {
                int idx = startIdx + i;
                if (idx >= buf.Length) break;
                float t = i / (float)SampleRate;
                seed = seed * 1664525u + 1013904223u;
                float noise = ((seed >> 8) / (float)(1 << 24)) * 2f - 1f;
                float env = Mathf.Exp(-t * 18f);
                float tone = Mathf.Sin(2f * Mathf.PI * 190f * t) * 0.35f;
                buf[idx] += (noise * 0.8f + tone) * env * amp;
            }
        }

        private static void AddHat(float[] buf, float start, float amp, bool open)
        {
            int startIdx = Mathf.Max(0, (int)(start * SampleRate));
            int len = (int)((open ? 0.16f : 0.06f) * SampleRate);
            uint seed = 9871u;
            float last = 0f;
            for (int i = 0; i < len; i++)
            {
                int idx = startIdx + i;
                if (idx >= buf.Length) break;
                float t = i / (float)SampleRate;
                seed = seed * 1103515245u + 12345u;
                float noise = ((seed >> 9) / (float)(1 << 23)) * 2f - 1f;
                float hp = noise - last;      // простой хай-пасс
                last = last * 0.6f + noise * 0.4f;
                float env = Mathf.Exp(-t * (open ? 16f : 45f));
                buf[idx] += hp * env * amp;
            }
        }

        private static void AddVinyl(float[] buf, float level)
        {
            uint seed = 20261u;
            for (int i = 0; i < buf.Length; i++)
            {
                seed = seed * 1664525u + 1013904223u;
                float r = ((seed >> 8) / (float)(1 << 24));
                if (r > 0.99965f)
                    buf[i] += (r > 0.9999f ? 0.22f : -0.16f) * level;
                buf[i] += (((seed >> 16) / (float)(1 << 16)) * 2f - 1f) * 0.006f * level;
            }
        }

        private static void LowPass(float[] buf, float amount)
        {
            float a = Mathf.Lerp(0.55f, 0.18f, Mathf.Clamp01(amount));
            float y1 = 0f, y2 = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float y = a * buf[i] + (1f - a) * y1;
                y2 = y1;
                y1 = y;
                buf[i] = y2 * 0.6f + y * 0.4f;
            }
        }

        private static float[] Reverb(float[] dry, float delay1, float delay2)
        {
            int d1 = (int)(SampleRate * 0.083f);
            int d2 = (int)(SampleRate * 0.121f);
            var wet = new float[dry.Length];
            for (int i = 0; i < dry.Length; i++)
            {
                float v = 0f;
                if (i - d1 >= 0) v += dry[i - d1] * delay1;
                if (i - d2 >= 0) v += dry[i - d2] * delay2;
                if (i - d1 - d2 >= 0) v += wet[i - d1 - d2] * 0.18f;
                wet[i] = v;
            }
            return wet;
        }

        private static void Normalize(float[] buf, float peak)
        {
            float max = 0f;
            for (int i = 0; i < buf.Length; i++) max = Mathf.Max(max, Mathf.Abs(buf[i]));
            if (max < 0.0001f) return;
            float k = peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= k;
        }

        // ---------------------------------------------------------------
        //  Звуки
        // ---------------------------------------------------------------

        private static AudioClip BuildSfx(Sfx sfx)
        {
            float duration;
            switch (sfx)
            {
                case Sfx.Shoot: duration = 0.16f; break;
                case Sfx.ShootEmpty: duration = 0.12f; break;
                case Sfx.Impact: duration = 0.12f; break;
                case Sfx.KiwiHit: duration = 0.22f; break;
                case Sfx.KiwiDie: duration = 0.45f; break;
                case Sfx.Pickup: duration = 0.28f; break;
                case Sfx.UiClick: duration = 0.08f; break;
                case Sfx.UiBack: duration = 0.10f; break;
                case Sfx.Win: duration = 1.1f; break;
                case Sfx.Lose: duration = 1.0f; break;
                case Sfx.Hurt: duration = 0.35f; break;
                case Sfx.Reload: duration = 0.30f; break;
                default: duration = 0.18f; break;
            }

            int count = Mathf.CeilToInt(duration * SampleRate);
            var buf = new float[count];
            uint seed = 99991u;

            switch (sfx)
            {
                case Sfx.Shoot:
                {
                    for (int i = 0; i < count; i++)
                    {
                        float t = i / (float)SampleRate;
                        seed = seed * 1664525u + 1013904223u;
                        float noise = ((seed >> 8) / (float)(1 << 24)) * 2f - 1f;
                        float env = Mathf.Exp(-t * 34f);
                        float thump = Mathf.Sin(2f * Mathf.PI * (220f * Mathf.Exp(-t * 14f)) * t) * 0.5f;
                        buf[i] = (noise * 0.85f + thump) * env * 0.8f;
                    }
                    break;
                }
                case Sfx.ShootEmpty:
                {
                    for (int i = 0; i < count; i++)
                    {
                        float t = i / (float)SampleRate;
                        seed = seed * 1664525u + 1013904223u;
                        float noise = ((seed >> 8) / (float)(1 << 24)) * 2f - 1f;
                        buf[i] = noise * Mathf.Exp(-t * 60f) * 0.35f;
                    }
                    break;
                }
                case Sfx.Impact:
                {
                    for (int i = 0; i < count; i++)
                    {
                        float t = i / (float)SampleRate;
                        seed = seed * 1664525u + 1013904223u;
                        float noise = ((seed >> 8) / (float)(1 << 24)) * 2f - 1f;
                        buf[i] = noise * Mathf.Exp(-t * 40f) * 0.55f;
                    }
                    break;
                }
                case Sfx.KiwiHit:
                {
                    for (int i = 0; i < count; i++)
                    {
                        float t = i / (float)SampleRate;
                        float f = 520f + 420f * t;
                        float env = Mathf.Exp(-t * 11f) * Mathf.Min(1f, t * 90f);
                        buf[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.55f;
                    }
                    break;
                }
                case Sfx.KiwiDie:
                {
                    for (int i = 0; i < count; i++)
                    {
                        float t = i / (float)SampleRate;
                        float f = 700f * Mathf.Exp(-t * 3.4f) + 90f;
                        float env = Mathf.Exp(-t * 6f);
                        seed = seed * 1664525u + 1013904223u;
                        float noise = ((seed >> 8) / (float)(1 << 24)) * 2f - 1f;
                        buf[i] = (Mathf.Sin(2f * Mathf.PI * f * t) * 0.7f + noise * 0.25f) * env * 0.6f;
                    }
                    break;
                }
                case Sfx.Laugh:
                {
                    // «Хи-хи-хи» — короткие всплески тона с вибрато, спускающиеся вниз.
                    for (int i = 0; i < count; i++)
                    {
                        float t = i / (float)SampleRate;
                        float burst = Mathf.Floor(t / 0.16f);          // номер «хи"
                        float local = t - burst * 0.16f;
                        float f = (520f - burst * 70f) * (1f + 0.06f * Mathf.Sin(2f * Mathf.PI * 9f * t));
                        float env = Mathf.Exp(-local * 18f) * Mathf.Min(1f, local * 60f) * Mathf.Exp(-t * 1.2f);
                        buf[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.5f;
                    }
                    break;
                }
                case Sfx.Pickup:
                {
                    int[] steps = { 880, 1174, 1568 };
                    for (int i = 0; i < count; i++)
                    {
                        float t = i / (float)SampleRate;
                        int step = Mathf.Clamp((int)(t / 0.075f), 0, 2);
                        float local = t - step * 0.075f;
                        float env = Mathf.Exp(-local * 14f) * Mathf.Min(1f, local * 200f);
                        buf[i] = Mathf.Sin(2f * Mathf.PI * steps[step] * t) * env * 0.45f;
                    }
                    break;
                }
                case Sfx.UiClick:
                {
                    for (int i = 0; i < count; i++)
                    {
                        float t = i / (float)SampleRate;
                        float env = Mathf.Exp(-t * 42f);
                        buf[i] = Mathf.Sin(2f * Mathf.PI * 1180f * t) * env * 0.3f;
                    }
                    break;
                }
                case Sfx.UiBack:
                {
                    for (int i = 0; i < count; i++)
                    {
                        float t = i / (float)SampleRate;
                        float env = Mathf.Exp(-t * 32f);
                        buf[i] = Mathf.Sin(2f * Mathf.PI * 660f * t) * env * 0.3f;
                    }
                    break;
                }
                case Sfx.Win:
                {
                    int[] steps = { 523, 659, 784, 1046 };
                    for (int i = 0; i < count; i++)
                    {
                        float t = i / (float)SampleRate;
                        int step = Mathf.Clamp((int)(t / 0.16f), 0, 3);
                        float local = t - step * 0.16f;
                        float env = Mathf.Exp(-local * 5.5f) * Mathf.Min(1f, local * 90f);
                        buf[i] = Mathf.Sin(2f * Mathf.PI * steps[step] * t) * env * 0.4f;
                    }
                    break;
                }
                case Sfx.Lose:
                {
                    int[] steps = { 440, 392, 330, 262 };
                    for (int i = 0; i < count; i++)
                    {
                        float t = i / (float)SampleRate;
                        int step = Mathf.Clamp((int)(t / 0.2f), 0, 3);
                        float local = t - step * 0.2f;
                        float env = Mathf.Exp(-local * 4f) * Mathf.Min(1f, local * 60f);
                        buf[i] = Mathf.Sin(2f * Mathf.PI * steps[step] * t) * env * 0.4f;
                    }
                    break;
                }
                case Sfx.Hurt:
                {
                    for (int i = 0; i < count; i++)
                    {
                        float t = i / (float)SampleRate;
                        float f = 320f * Mathf.Exp(-t * 5f) + 60f;
                        float env = Mathf.Exp(-t * 8f);
                        buf[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.5f;
                    }
                    break;
                }
                case Sfx.Reload:
                {
                    for (int i = 0; i < count; i++)
                    {
                        float t = i / (float)SampleRate;
                        float env = Mathf.Exp(-t * 55f);
                        bool click = t < 0.05f || (t > 0.12f && t < 0.17f);
                        if (!click) env = 0f;
                        seed = seed * 1664525u + 1013904223u;
                        float noise = ((seed >> 8) / (float)(1 << 24)) * 2f - 1f;
                        buf[i] = noise * env * 0.4f;
                    }
                    break;
                }
                default:
                {
                    for (int i = 0; i < count; i++)
                    {
                        float t = i / (float)SampleRate;
                        float f = 300f + 500f * (t / duration);
                        float env = Mathf.Exp(-t * 14f);
                        buf[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.4f;
                    }
                    break;
                }
            }

            float[] data = new float[count * 2];
            for (int i = 0; i < count; i++)
            {
                data[i * 2] = buf[i];
                data[i * 2 + 1] = buf[i];
            }

            AudioClip clip = AudioClip.Create("KiviSfx_" + sfx, count, 2, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
