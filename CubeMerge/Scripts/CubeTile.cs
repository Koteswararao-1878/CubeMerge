using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace CubeMerge
{
    /// <summary>One numbered 3D cube on the board.</summary>
    public class CubeTile : MonoBehaviour
    {
        public int Value { get; private set; }
        public Vector2Int Cell;

        Renderer rend;
        MaterialPropertyBlock mpb;
        TextMesh[] labels;
        Color tint;
        float baseGlow;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        public static CubeTile Create(Transform parent, int value, Vector2Int cell)
        {
            var go = new GameObject("Cube");
            go.transform.SetParent(parent, false);

            go.AddComponent<MeshFilter>().sharedMesh = GameAssets.CubeMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;

            var tile = go.AddComponent<CubeTile>();
            tile.rend = mr;
            tile.mpb = new MaterialPropertyBlock();

            float o = GameAssets.LabelOffset;
            // The camera looks at the board from the -X / -Z side, so the visible faces are
            // the top, the -X face and the -Z face.
            tile.labels = new TextMesh[3];
            tile.labels[0] = tile.MakeLabel("LabelTop", new Vector3(0f, o, 0f), Quaternion.Euler(90f, 45f, 0f));
            tile.labels[1] = tile.MakeLabel("LabelLeft", new Vector3(-o, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
            tile.labels[2] = tile.MakeLabel("LabelRight", new Vector3(0f, 0f, -o), Quaternion.identity);

            tile.Cell = cell;
            tile.SetValue(value);
            return tile;
        }

        TextMesh MakeLabel(string labelName, Vector3 localPos, Quaternion localRot)
        {
            var go = new GameObject(labelName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            return GameAssets.MakeText(go);
        }

        public void SetValue(int v)
        {
            Value = v;
            tint = GameAssets.ColorFor(v);
            rend.sharedMaterial = GameAssets.MaterialFor(v);

            string s = v.ToString();
            float sc = GameAssets.TextScale(s);
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].text = s;
                labels[i].transform.localScale = Vector3.one * sc;
            }

            baseGlow = v >= 2048 ? 0.35f : 0f;
            SetGlow(baseGlow);
        }

        void SetGlow(float k)
        {
            rend.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionId, tint * k);
            rend.SetPropertyBlock(mpb);
        }

        // ------------------------------------------------------------------ animations
        public void SlideTo(Vector3 to, float duration)
        {
            StartCoroutine(Slide(to, duration));
        }

        IEnumerator Slide(Vector3 to, float duration)
        {
            Vector3 from = transform.position;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                k = k * k * (3f - 2f * k);
                transform.position = Vector3.Lerp(from, to, k);
                yield return null;
            }
            transform.position = to;
        }

        public void Appear()
        {
            StartCoroutine(AppearRoutine());
        }

        IEnumerator AppearRoutine()
        {
            transform.localScale = Vector3.zero;
            float t = 0f, dur = 0.22f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                const float c1 = 1.70158f;
                const float c3 = c1 + 1f;
                float e = 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
                transform.localScale = Vector3.one * e;
                yield return null;
            }
            transform.localScale = Vector3.one;
        }

        /// <summary>Pop + glow used when two cubes merge.</summary>
        public void Pulse()
        {
            StartCoroutine(PulseRoutine());
        }

        IEnumerator PulseRoutine()
        {
            float t = 0f, dur = 0.4f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                float s = Mathf.Sin(Mathf.PI * k);
                transform.localScale = Vector3.one * (1f + 0.28f * s);
                SetGlow(baseGlow + s * 1.5f);
                yield return null;
            }
            transform.localScale = Vector3.one;
            SetGlow(baseGlow);
        }
    }
}
