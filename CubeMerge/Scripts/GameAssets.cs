using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CubeMerge
{
    /// <summary>
    /// Everything the game needs is generated from code: rounded cube mesh, materials,
    /// sound effects and textures. No external assets are required.
    /// </summary>
    public static class GameAssets
    {
        public const float CubeSize = 0.94f;
        public const float LabelOffset = CubeSize * 0.5f + 0.004f;

        // ------------------------------------------------------------------ palette
        // 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096+
        static readonly Color[] Palette =
        {
            new Color(0.15f, 0.62f, 1.00f), // 2    blue
            new Color(1.00f, 0.78f, 0.15f), // 4    yellow
            new Color(0.35f, 0.78f, 0.30f), // 8    green
            new Color(0.90f, 0.33f, 0.33f), // 16   red
            new Color(0.95f, 0.45f, 0.70f), // 32   pink
            new Color(0.58f, 0.35f, 0.88f), // 64   purple
            new Color(1.00f, 0.55f, 0.10f), // 128  orange
            new Color(0.10f, 0.75f, 0.72f), // 256  teal
            new Color(0.72f, 0.85f, 0.15f), // 512  lime
            new Color(0.20f, 0.35f, 0.90f), // 1024 deep blue
            new Color(1.00f, 0.86f, 0.10f), // 2048 gold
            new Color(0.18f, 0.18f, 0.24f), // 4096+
        };

        public static int Log2(int v)
        {
            int n = 0;
            while (v > 1) { v >>= 1; n++; }
            return n;
        }

        public static Color ColorFor(int value)
        {
            int idx = Mathf.Clamp(Log2(value) - 1, 0, Palette.Length - 1);
            return Palette[idx];
        }

        // ------------------------------------------------------------------ find helpers
        public static T FindOne<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<T>();
#else
            return UnityEngine.Object.FindObjectOfType<T>();
#endif
        }

        public static T[] FindAll<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return UnityEngine.Object.FindObjectsOfType<T>();
#endif
        }

        // ------------------------------------------------------------------ materials
        static Shader litShader;

        static Shader LitShader()
        {
            if (litShader != null) return litShader;
            if (GraphicsSettings.currentRenderPipeline != null)
                litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");
            if (litShader == null) litShader = Shader.Find("Unlit/Color");
            return litShader;
        }

        public static Material MakeLit(Color c, float smoothness)
        {
            var m = new Material(LitShader());
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            return m;
        }

        static readonly Dictionary<int, Material> cubeMats = new Dictionary<int, Material>();

        public static Material MaterialFor(int value)
        {
            Material m;
            if (!cubeMats.TryGetValue(value, out m))
            {
                m = MakeLit(ColorFor(value), 0.45f);
                cubeMats[value] = m;
            }
            return m;
        }

        // ------------------------------------------------------------------ mesh
        static Mesh cubeMesh;

        public static Mesh CubeMesh
        {
            get
            {
                if (cubeMesh == null) cubeMesh = RoundedBox(Vector3.one * CubeSize, 0.14f);
                return cubeMesh;
            }
        }

        static float[] Coords(float h, float r)
        {
            // Angles 45, 30, 15, 0 degrees give an evenly curved corner.
            return new float[]
            {
                -h, -h + r * 0.4226f, -h + r * 0.7321f, -h + r,
                 h - r, h - r * 0.7321f, h - r * 0.4226f, h
            };
        }

        /// <summary>Builds a box with rounded (bevelled) edges and smooth normals.</summary>
        public static Mesh RoundedBox(Vector3 size, float r)
        {
            Vector3 half = size * 0.5f;
            r = Mathf.Min(r, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.99f);

            // cross(u, v) == n for every face so the winding is consistent.
            Vector3[] ns = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            Vector3[] us = { Vector3.up, Vector3.forward, Vector3.forward, Vector3.right, Vector3.right, Vector3.up };
            Vector3[] vs = { Vector3.forward, Vector3.up, Vector3.right, Vector3.forward, Vector3.up, Vector3.right };

            const int K = 8;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();

            for (int f = 0; f < 6; f++)
            {
                Vector3 n = ns[f], u = us[f], v = vs[f];
                float hn = Mathf.Abs(Vector3.Dot(n, half));
                float hu = Mathf.Abs(Vector3.Dot(u, half));
                float hv = Mathf.Abs(Vector3.Dot(v, half));
                float[] cu = Coords(hu, r);
                float[] cv = Coords(hv, r);
                int b0 = verts.Count;

                for (int i = 0; i < K; i++)
                {
                    for (int j = 0; j < K; j++)
                    {
                        Vector3 p = n * hn + u * cu[i] + v * cv[j];
                        Vector3 inner = new Vector3(
                            Mathf.Clamp(p.x, -half.x + r, half.x - r),
                            Mathf.Clamp(p.y, -half.y + r, half.y - r),
                            Mathf.Clamp(p.z, -half.z + r, half.z - r));
                        Vector3 dir = (p - inner).normalized;
                        verts.Add(inner + dir * r);
                        normals.Add(dir);
                    }
                }

                for (int i = 0; i < K - 1; i++)
                {
                    for (int j = 0; j < K - 1; j++)
                    {
                        int a = b0 + i * K + j;
                        int b = b0 + (i + 1) * K + j;
                        int c = b0 + i * K + j + 1;
                        int d = b0 + (i + 1) * K + j + 1;
                        tris.Add(a); tris.Add(b); tris.Add(c);
                        tris.Add(b); tris.Add(d); tris.Add(c);
                    }
                }
            }

            var mesh = new Mesh();
            mesh.name = "RoundedBox";
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ text (TextMesh)
        static Font font;

        public static Font LabelFont
        {
            get
            {
                if (font != null) return font;
                try
                {
#if UNITY_2022_2_OR_NEWER
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
                    font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
                }
                catch (System.Exception) { font = null; }
                if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 64);
                return font;
            }
        }

        public static TextMesh MakeText(GameObject go)
        {
            var tm = go.AddComponent<TextMesh>();
            tm.font = LabelFont;
            tm.fontSize = 64;
            tm.characterSize = 0.1f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = Color.white;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = LabelFont.material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return tm;
        }

        static TextMesh measure;
        static readonly Dictionary<string, float> textScales = new Dictionary<string, float>();

        // Maximum size of a label on a cube face (world units)
        const float MaxLabelHeight = 0.50f;
        const float MaxLabelWidth = 0.68f;

        /// <summary>Returns the scale that makes the given text fit nicely on a cube face.</summary>
        public static float TextScale(string s)
        {
            float cached;
            if (textScales.TryGetValue(s, out cached)) return cached;

            if (measure == null)
            {
                var go = new GameObject("TextMeasure");
                go.transform.position = new Vector3(0f, -5000f, 0f);
                measure = MakeText(go);
                Object.DontDestroyOnLoad(go);
            }

            measure.text = s;
            Bounds b = measure.GetComponent<MeshRenderer>().bounds;
            float sc = 1f;
            if (b.size.x > 1e-4f && b.size.y > 1e-4f)
                sc = Mathf.Min(MaxLabelHeight / b.size.y, MaxLabelWidth / b.size.x);

            textScales[s] = sc;
            return sc;
        }

        // ------------------------------------------------------------------ textures
        public static Texture2D SoftCircle(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            t.Apply();
            return t;
        }

        public static Texture2D Gradient(Color bottom, Color top)
        {
            const int h = 64;
            var t = new Texture2D(1, h, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < h; y++)
                t.SetPixel(0, y, Color.Lerp(bottom, top, y / (float)(h - 1)));
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------------ audio
        const int Rate = 44100;

        static AudioClip Make(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        static void AddTone(float[] buf, int offset, float freq, float dur, float vol, float decay)
        {
            int n = (int)(Rate * dur);
            for (int i = 0; i < n; i++)
            {
                int idx = offset + i;
                if (idx >= buf.Length) break;
                float t = (float)i / Rate;
                float env = Mathf.Exp(-decay * t / dur) * Mathf.Min(1f, t / 0.004f);
                float ph = 2f * Mathf.PI * freq * t;
                buf[idx] += vol * env * (Mathf.Sin(ph) + 0.35f * Mathf.Sin(2f * ph) + 0.12f * Mathf.Sin(3f * ph));
            }
        }

        public static AudioClip Tone(string name, float freq, float dur, float vol, float decay)
        {
            var d = new float[(int)(Rate * dur)];
            AddTone(d, 0, freq, dur, vol, decay);
            return Make(name, d);
        }

        public static AudioClip Sequence(string name, float[] freqs, float noteDur, float vol)
        {
            int step = (int)(Rate * noteDur);
            int tail = (int)(Rate * noteDur * 2.2f);
            var d = new float[step * (freqs.Length - 1) + tail];
            for (int k = 0; k < freqs.Length; k++)
                AddTone(d, k * step, freqs[k], noteDur * 2.2f, vol, 5f);
            return Make(name, d);
        }

        public static AudioClip Swish(string name, float dur, float vol)
        {
            int n = (int)(Rate * dur);
            var d = new float[n];
            var rnd = new System.Random(7);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * 0.22f;
                float env = Mathf.Sin(Mathf.PI * i / n);
                d[i] = lp * env * vol;
            }
            return Make(name, d);
        }

        /// <summary>Pentatonic note for a merge result (higher cubes = higher pitch).</summary>
        public static float MergeFrequency(int value)
        {
            int idx = Mathf.Max(0, Log2(value) - 1);
            int[] pent = { 0, 2, 4, 7, 9 };
            int semis = pent[idx % 5] + 12 * (idx / 5);
            return 261.63f * Mathf.Pow(2f, semis / 12f);
        }
    }
}
