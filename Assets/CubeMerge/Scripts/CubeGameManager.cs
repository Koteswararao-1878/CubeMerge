using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CubeMerge
{
    /// <summary>
    /// 2048-style cube merging game. Builds the whole scene (camera, light, platform,
    /// effects, audio, UI) from code, so it works in any empty scene.
    /// </summary>
    public class CubeGameManager : MonoBehaviour
    {
        // ------------------------------------------------------------ tweakables
        public const int N = 4;                  // board size (N x N)
        const float CellSpacing = 1.25f;         // distance between cube centres
        const float CubeY = 0.05f + GameAssets.CubeSize * 0.5f;
        const float MoveTime = 0.12f;
        const float Pitch = 38f;                 // camera tilt
        const int WinValue = 2048;

        // ------------------------------------------------------------ state
        class Snapshot
        {
            public int[] values;
            public int score;
            public bool won;
        }

        class MergePair
        {
            public CubeTile mover;
            public CubeTile target;
        }

        readonly CubeTile[,] tiles = new CubeTile[N, N];
        readonly List<Snapshot> history = new List<Snapshot>();

        public int Score { get; private set; }
        public int Best { get; private set; }
        public bool GameOver { get; private set; }
        public bool ShowWin { get; private set; }
        public bool OverlayVisible { get { return (GameOver || ShowWin) && Time.time >= overlayAt; } }
        public bool CanUndo { get { return history.Count > 0 && !busy; } }

        bool busy;
        bool won;
        Vector2Int pending = Vector2Int.zero;
        float overlayAt;

        // ------------------------------------------------------------ scene objects
        Camera cam;
        Transform boardRoot;
        Transform bgQuad;
        ParticleSystem fx;
        AudioSource sfx;
        AudioClip moveClip, winClip, loseClip, undoClip, spawnClip;
        readonly Dictionary<int, AudioClip> mergeClips = new Dictionary<int, AudioClip>();

        class Floater
        {
            public Transform t;
            public Vector3 basePos;
            public float phase;
        }
        readonly List<Floater> floaters = new List<Floater>();

        // ============================================================ lifecycle
        void Awake()
        {
            Best = PlayerPrefs.GetInt("CubeMerge_Best", 0);
            BuildEnvironment();
            BuildBoard();
            BuildDecor();
            BuildFX();
            BuildAudio();
            gameObject.AddComponent<GameUI>().game = this;
            NewGame();
        }

        void Update()
        {
            UpdateCamera();
            AnimateDecor();

            if (GameInput.Restart()) { NewGame(); return; }

            if (GameOver || ShowWin)
            {
                if (OverlayVisible && GameInput.Confirm())
                {
                    if (ShowWin) ContinueAfterWin(); else NewGame();
                }
                if (GameInput.Undo() && GameOver) Undo();
                return;
            }

            Vector2Int d = Vector2Int.zero;
            // The camera is rotated 45 degrees, so the keys are mapped to the board diagonals:
            // UP = up-right, LEFT = up-left, RIGHT = down-right, DOWN = down-left on screen.
            if (GameInput.Up()) d = new Vector2Int(1, 0);
            else if (GameInput.Down()) d = new Vector2Int(-1, 0);
            else if (GameInput.Left()) d = new Vector2Int(0, 1);
            else if (GameInput.Right()) d = new Vector2Int(0, -1);

            if (d != Vector2Int.zero)
            {
                if (busy) pending = d;
                else StartCoroutine(DoMove(d));
            }
            else if (!busy && GameInput.Undo())
            {
                Undo();
            }
        }

        // ============================================================ public API (UI)
        public void NewGame()
        {
            StopAllCoroutines();
            busy = false;
            pending = Vector2Int.zero;
            ClearTiles();
            history.Clear();
            Score = 0;
            GameOver = false;
            ShowWin = false;
            won = false;
            SpawnRandom(false);
            SpawnRandom(false);
        }

        public void Undo()
        {
            if (busy || history.Count == 0) return;
            Snapshot s = history[history.Count - 1];
            history.RemoveAt(history.Count - 1);
            Restore(s);
            GameOver = false;
            ShowWin = false;
            Play(undoClip, 0.6f);
        }

        public void ContinueAfterWin()
        {
            ShowWin = false;
            if (!CanMove()) { GameOver = true; overlayAt = Time.time + 0.2f; }
        }

        // ============================================================ game logic
        static int[] Order(int dir)
        {
            var a = new int[N];
            for (int i = 0; i < N; i++) a[i] = dir > 0 ? N - 1 - i : i;
            return a;
        }

        Vector3 CellToWorld(int x, int z)
        {
            float o = (N - 1) * 0.5f;
            return new Vector3((x - o) * CellSpacing, CubeY, (z - o) * CellSpacing);
        }

        IEnumerator DoMove(Vector2Int d)
        {
            busy = true;
            Snapshot snap = TakeSnapshot();
            bool moved = false;
            int gained = 0;
            var merges = new List<MergePair>();
            var mergedFlag = new bool[N, N];

            foreach (int x in Order(d.x))
            {
                foreach (int z in Order(d.y))
                {
                    CubeTile t = tiles[x, z];
                    if (t == null) continue;

                    int cx = x, cz = z;
                    bool merged = false;

                    while (true)
                    {
                        int nx = cx + d.x, nz = cz + d.y;
                        if (nx < 0 || nz < 0 || nx >= N || nz >= N) break;

                        CubeTile o = tiles[nx, nz];
                        if (o == null)
                        {
                            tiles[nx, nz] = t;
                            tiles[cx, cz] = null;
                            cx = nx; cz = nz;
                            continue;
                        }

                        if (o.Value == t.Value && !mergedFlag[nx, nz])
                        {
                            tiles[cx, cz] = null;
                            mergedFlag[nx, nz] = true;
                            merges.Add(new MergePair { mover = t, target = o });
                            gained += t.Value * 2;
                            cx = nx; cz = nz;
                            merged = true;
                        }
                        break;
                    }

                    if (cx != x || cz != z)
                    {
                        moved = true;
                        t.SlideTo(CellToWorld(cx, cz), MoveTime);
                        if (!merged) t.Cell = new Vector2Int(cx, cz);
                    }
                }
            }

            if (!moved)
            {
                busy = false;
                pending = Vector2Int.zero;
                yield break;
            }

            history.Add(snap);
            if (history.Count > 50) history.RemoveAt(0);

            Play(moveClip, 0.5f);
            yield return new WaitForSeconds(MoveTime + 0.02f);

            int highest = 0;
            bool reachedWin = false;
            foreach (MergePair m in merges)
            {
                int nv = m.target.Value * 2;
                Vector3 pos = m.target.transform.position;
                Destroy(m.mover.gameObject);
                m.target.SetValue(nv);
                m.target.Pulse();
                Burst(pos, GameAssets.ColorFor(nv), 28);
                if (nv > highest) highest = nv;
                if (nv == WinValue) reachedWin = true;
            }

            if (merges.Count > 0) Play(GetMergeClip(highest), 0.9f);

            Score += gained;
            if (Score > Best)
            {
                Best = Score;
                PlayerPrefs.SetInt("CubeMerge_Best", Best);
                PlayerPrefs.Save();
            }

            yield return new WaitForSeconds(merges.Count > 0 ? 0.1f : 0.02f);
            SpawnRandom(true);
            Play(spawnClip, 0.35f);

            if (reachedWin && !won)
            {
                won = true;
                ShowWin = true;
                overlayAt = Time.time + 0.6f;
                Play(winClip, 0.8f);
            }
            else if (!CanMove())
            {
                GameOver = true;
                overlayAt = Time.time + 0.6f;
                Play(loseClip, 0.8f);
            }

            busy = false;

            if (pending != Vector2Int.zero && !GameOver && !ShowWin)
            {
                Vector2Int p = pending;
                pending = Vector2Int.zero;
                StartCoroutine(DoMove(p));
            }
            else
            {
                pending = Vector2Int.zero;
            }
        }

        bool CanMove()
        {
            for (int x = 0; x < N; x++)
            {
                for (int z = 0; z < N; z++)
                {
                    if (tiles[x, z] == null) return true;
                    int v = tiles[x, z].Value;
                    if (x + 1 < N && tiles[x + 1, z] != null && tiles[x + 1, z].Value == v) return true;
                    if (z + 1 < N && tiles[x, z + 1] != null && tiles[x, z + 1].Value == v) return true;
                }
            }
            return false;
        }

        void SpawnRandom(bool animate)
        {
            var empty = new List<Vector2Int>();
            for (int x = 0; x < N; x++)
                for (int z = 0; z < N; z++)
                    if (tiles[x, z] == null) empty.Add(new Vector2Int(x, z));
            if (empty.Count == 0) return;

            Vector2Int c = empty[Random.Range(0, empty.Count)];
            SpawnTile(c, Random.value < 0.9f ? 2 : 4, animate);
        }

        CubeTile SpawnTile(Vector2Int c, int value, bool animate)
        {
            CubeTile t = CubeTile.Create(boardRoot, value, c);
            t.transform.position = CellToWorld(c.x, c.y);
            tiles[c.x, c.y] = t;
            if (animate) t.Appear();
            return t;
        }

        void ClearTiles()
        {
            for (int x = 0; x < N; x++)
            {
                for (int z = 0; z < N; z++)
                {
                    if (tiles[x, z] != null) Destroy(tiles[x, z].gameObject);
                    tiles[x, z] = null;
                }
            }
            // Also remove any leftover moving tiles from an interrupted move.
            foreach (CubeTile t in GameAssets.FindAll<CubeTile>())
                if (t != null) Destroy(t.gameObject);
        }

        Snapshot TakeSnapshot()
        {
            var s = new Snapshot { values = new int[N * N], score = Score, won = won };
            for (int x = 0; x < N; x++)
                for (int z = 0; z < N; z++)
                    s.values[x * N + z] = tiles[x, z] != null ? tiles[x, z].Value : 0;
            return s;
        }

        void Restore(Snapshot s)
        {
            ClearTiles();
            for (int x = 0; x < N; x++)
                for (int z = 0; z < N; z++)
                    if (s.values[x * N + z] > 0)
                        SpawnTile(new Vector2Int(x, z), s.values[x * N + z], false);
            Score = s.score;
            won = s.won;
        }

        // ============================================================ scene building
        void BuildEnvironment()
        {
            foreach (Light l in GameAssets.FindAll<Light>()) l.enabled = false;

            cam = Camera.main;
            if (cam == null)
            {
                var cg = new GameObject("Main Camera");
                cg.tag = "MainCamera";
                cam = cg.AddComponent<Camera>();
            }
            if (GameAssets.FindOne<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();

            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.40f, 0.80f, 0.97f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.transform.rotation = Quaternion.Euler(Pitch, 45f, 0f);
            cam.transform.position = new Vector3(0f, 0.3f, 0f) - cam.transform.forward * 40f;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.66f, 0.76f);

            var lg = new GameObject("Sun");
            lg.transform.SetParent(transform, false);
            lg.transform.rotation = Quaternion.Euler(55f, 20f, 0f);
            var light = lg.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.97f, 0.9f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.55f;

            // Soft gradient sky behind everything
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(bg.GetComponent<Collider>());
            bg.name = "SkyGradient";
            bg.transform.SetParent(cam.transform, false);
            bg.transform.localPosition = new Vector3(0f, 0f, 90f);
            bg.transform.localRotation = Quaternion.identity;
            var bgMat = new Material(Shader.Find("Sprites/Default"));
            bgMat.mainTexture = GameAssets.Gradient(new Color(0.62f, 0.95f, 0.86f), new Color(0.25f, 0.76f, 0.98f));
            var bgR = bg.GetComponent<Renderer>();
            bgR.sharedMaterial = bgMat;
            bgR.shadowCastingMode = ShadowCastingMode.Off;
            bgR.receiveShadows = false;
            bgQuad = bg.transform;
        }

        void UpdateCamera()
        {
            float aspect = Mathf.Max(0.3f, cam.aspect);
            float size = Mathf.Max(5.4f, 4.9f / aspect);
            cam.orthographicSize = size;
            if (bgQuad != null)
                bgQuad.localScale = new Vector3(size * 2f * aspect + 6f, size * 2f + 6f, 1f);
        }

        GameObject Block(string blockName, Vector3 size, float radius, Vector3 pos, Color c, Transform parent)
        {
            var go = new GameObject(blockName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = GameAssets.RoundedBox(size, radius);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GameAssets.MakeLit(c, 0.3f);
            return go;
        }

        void BuildBoard()
        {
            boardRoot = new GameObject("Board").transform;
            boardRoot.SetParent(transform, false);

            float w = N * CellSpacing + 0.7f;
            Block("Pillar", new Vector3(w - 0.3f, 3.2f, w - 0.3f), 0.25f, new Vector3(0f, -2.2f, 0f),
                new Color(0.28f, 0.55f, 0.72f), boardRoot);
            Block("Platform", new Vector3(w, 1.0f, w), 0.3f, new Vector3(0f, -0.5f, 0f),
                new Color(0.45f, 0.74f, 0.80f), boardRoot);

            float o = (N - 1) * 0.5f;
            for (int x = 0; x < N; x++)
            {
                for (int z = 0; z < N; z++)
                {
                    Block("Tile", new Vector3(CellSpacing - 0.12f, 0.1f, CellSpacing - 0.12f), 0.04f,
                        new Vector3((x - o) * CellSpacing, 0f, (z - o) * CellSpacing),
                        new Color(0.64f, 0.88f, 0.87f), boardRoot);
                }
            }
        }

        void BuildDecor()
        {
            // Small floating islands with cubes, placed in screen space around the board.
            Vector3 right = Quaternion.Euler(0f, 45f, 0f) * Vector3.right;
            Vector3 fwdH = Quaternion.Euler(0f, 45f, 0f) * Vector3.forward;
            float p = Pitch * Mathf.Deg2Rad;
            Vector3 up = fwdH * Mathf.Sin(p) + Vector3.up * Mathf.Cos(p);

            Vector2[] offs = { new Vector2(-7.3f, 3.6f), new Vector2(7.5f, 3.4f), new Vector2(-7.4f, -3.4f), new Vector2(7.3f, -3.7f) };
            Color[] cubeCols = { new Color(0.15f, 0.62f, 1f), new Color(1f, 0.78f, 0.15f), new Color(0.58f, 0.35f, 0.88f), new Color(1f, 0.55f, 0.1f) };

            for (int i = 0; i < offs.Length; i++)
            {
                var root = new GameObject("Island" + i).transform;
                root.SetParent(transform, false);
                Vector3 pos = right * offs[i].x + up * offs[i].y;
                root.position = pos;

                Block("Top", new Vector3(2.6f, 0.5f, 2.6f), 0.18f, Vector3.zero, new Color(0.55f, 0.84f, 0.66f), root);
                Block("Base", new Vector3(1.8f, 1.2f, 1.8f), 0.18f, new Vector3(0f, -0.8f, 0f), new Color(0.38f, 0.66f, 0.76f), root);
                Block("CubeA", Vector3.one * 0.7f, 0.12f, new Vector3(-0.4f, 0.6f, -0.2f), cubeCols[i % 4], root);
                Block("CubeB", Vector3.one * 0.55f, 0.1f, new Vector3(0.5f, 0.52f, 0.4f), cubeCols[(i + 2) % 4], root);

                floaters.Add(new Floater { t = root, basePos = pos, phase = i * 1.7f });
            }
        }

        void AnimateDecor()
        {
            foreach (Floater f in floaters)
                f.t.position = f.basePos + Vector3.up * (Mathf.Sin(Time.time * 0.9f + f.phase) * 0.18f);
        }

        // ============================================================ effects & audio
        void BuildFX()
        {
            var go = new GameObject("MergeFX");
            go.transform.SetParent(transform, false);
            fx = go.AddComponent<ParticleSystem>();

            var main = fx.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.35f;
            main.maxParticles = 800;
            main.startLifetime = 0.7f;
            main.startSpeed = 0f;
            main.startSize = 0.3f;

            var em = fx.emission; em.enabled = false;
            var sh = fx.shape; sh.enabled = false;

            var col = fx.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var sz = fx.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));

            Shader sh2 = Shader.Find("Legacy Shaders/Particles/Additive");
            if (sh2 == null) sh2 = Shader.Find("Sprites/Default");
            var mat = new Material(sh2);
            mat.mainTexture = GameAssets.SoftCircle(64);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;

            fx.Play();
        }

        void Burst(Vector3 pos, Color tintColor, int count)
        {
            if (fx == null) return;
            var ep = new ParticleSystem.EmitParams();

            // big soft halo behind the merged cube
            ep.position = pos;
            ep.velocity = Vector3.zero;
            ep.startLifetime = 0.5f;
            ep.startSize = 2.6f;
            ep.startColor = Color.Lerp(tintColor, Color.white, 0.4f) * 0.55f;
            fx.Emit(ep, 1);

            // sparkles
            for (int i = 0; i < count; i++)
            {
                ep.position = pos + Random.insideUnitSphere * 0.3f;
                Vector3 v = Random.onUnitSphere;
                v.y = Mathf.Abs(v.y) * 0.8f + 0.3f;
                ep.velocity = v * Random.Range(1.2f, 3.4f);
                ep.startLifetime = Random.Range(0.45f, 0.95f);
                ep.startSize = Random.Range(0.12f, 0.34f);
                ep.startColor = Color.Lerp(tintColor, Color.white, Random.Range(0.35f, 0.95f));
                fx.Emit(ep, 1);
            }
        }

        void BuildAudio()
        {
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;

            moveClip = GameAssets.Swish("move", 0.09f, 0.5f);
            spawnClip = GameAssets.Tone("spawn", 700f, 0.08f, 0.25f, 5f);
            undoClip = GameAssets.Sequence("undo", new[] { 520f, 390f }, 0.06f, 0.3f);
            winClip = GameAssets.Sequence("win", new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f }, 0.11f, 0.3f);
            loseClip = GameAssets.Sequence("lose", new[] { 392f, 349.2f, 311.1f, 261.6f }, 0.2f, 0.3f);
        }

        AudioClip GetMergeClip(int value)
        {
            AudioClip c;
            if (!mergeClips.TryGetValue(value, out c))
            {
                c = GameAssets.Tone("merge" + value, GameAssets.MergeFrequency(value), 0.32f, 0.4f, 6f);
                mergeClips[value] = c;
            }
            return c;
        }

        void Play(AudioClip clip, float volume)
        {
            if (sfx != null && clip != null) sfx.PlayOneShot(clip, volume);
        }
    }
}
