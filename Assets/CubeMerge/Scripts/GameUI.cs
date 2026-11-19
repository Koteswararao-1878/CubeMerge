using UnityEngine;

namespace CubeMerge
{
    /// <summary>Score, buttons, hints and the game over / win overlays (drawn with IMGUI).</summary>
    public class GameUI : MonoBehaviour
    {
        public CubeGameManager game;

        GUIStyle title, small, big, hint, panelTitle, panelSub, box, panel, button;
        GUIStyle shadowTitle, shadowPanelTitle;

        static Texture2D RoundTex(Color fill, int size, int rad)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(rad - (x + 0.5f), (x + 0.5f) - (size - rad), 0f);
                    float dy = Mathf.Max(rad - (y + 0.5f), (y + 0.5f) - (size - rad), 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(rad - dist + 0.5f);
                    t.SetPixel(x, y, new Color(fill.r, fill.g, fill.b, fill.a * a));
                }
            }
            t.Apply();
            return t;
        }

        void BuildStyles()
        {
            var rBox = RoundTex(new Color(0.09f, 0.28f, 0.42f, 0.80f), 32, 12);
            var rPanel = RoundTex(new Color(0.08f, 0.22f, 0.36f, 0.95f), 32, 14);
            var rBtn = RoundTex(new Color(1.00f, 0.62f, 0.15f, 1f), 32, 12);
            var rBtnH = RoundTex(new Color(1.00f, 0.72f, 0.30f, 1f), 32, 12);
            var rBtnA = RoundTex(new Color(0.88f, 0.48f, 0.08f, 1f), 32, 12);
            var border = new RectOffset(14, 14, 14, 14);

            title = new GUIStyle { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            title.normal.textColor = Color.white;
            shadowTitle = new GUIStyle(title);
            shadowTitle.normal.textColor = new Color(0f, 0f, 0f, 0.28f);

            small = new GUIStyle { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            small.normal.textColor = new Color(0.7f, 0.9f, 1f);

            big = new GUIStyle { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            big.normal.textColor = Color.white;

            hint = new GUIStyle { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            hint.normal.textColor = new Color(1f, 1f, 1f, 0.9f);

            panelTitle = new GUIStyle { fontSize = 56, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            panelTitle.normal.textColor = Color.white;
            shadowPanelTitle = new GUIStyle(panelTitle);
            shadowPanelTitle.normal.textColor = new Color(0f, 0f, 0f, 0.35f);

            panelSub = new GUIStyle { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            panelSub.normal.textColor = new Color(0.8f, 0.95f, 1f);

            box = new GUIStyle { border = border };
            box.normal.background = rBox;

            panel = new GUIStyle { border = border };
            panel.normal.background = rPanel;

            button = new GUIStyle { border = border, fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            button.normal.background = rBtn;
            button.hover.background = rBtnH;
            button.active.background = rBtnA;
            button.normal.textColor = Color.white;
            button.hover.textColor = Color.white;
            button.active.textColor = Color.white;
        }

        void ShadowLabel(Rect r, string text, GUIStyle style, GUIStyle shadow)
        {
            var sr = r; sr.x += 2f; sr.y += 3f;
            GUI.Label(sr, text, shadow);
            GUI.Label(r, text, style);
        }

        void ScoreBox(float x, float y, string label, int value)
        {
            GUI.Box(new Rect(x, y, 120f, 60f), GUIContent.none, box);
            GUI.Label(new Rect(x, y + 6f, 120f, 18f), label, small);
            GUI.Label(new Rect(x, y + 22f, 120f, 34f), value.ToString(), big);
        }

        void OnGUI()
        {
            if (game == null) return;
            if (title == null) BuildStyles();

            float s = Mathf.Max(0.6f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            float W = Screen.width / s;
            float H = Screen.height / s;

            bool overlay = game.OverlayVisible;

            // ---- top bar
            ShadowLabel(new Rect(24f, 12f, 420f, 60f), "2048 CUBES", title, shadowTitle);

            ScoreBox(W - 500f, 14f, "SCORE", game.Score);
            ScoreBox(W - 372f, 14f, "BEST", game.Best);

            GUI.enabled = game.CanUndo && !overlay;
            if (GUI.Button(new Rect(W - 244f, 14f, 108f, 60f), "UNDO", button)) game.Undo();
            GUI.enabled = !overlay;
            if (GUI.Button(new Rect(W - 128f, 14f, 108f, 60f), "NEW", button)) game.NewGame();
            GUI.enabled = true;

            // ---- bottom hint
            ShadowLabel(new Rect(0f, H - 64f, W, 28f),
                "Arrow keys / WASD slide the cubes along the board diagonals", hint, hint);
            GUI.Label(new Rect(0f, H - 38f, W, 28f),
                "UP = up-right    LEFT = up-left    RIGHT = down-right    DOWN = down-left    |    Z = undo    R = restart", hint);

            // ---- overlays
            if (!overlay) return;

            var old = GUI.color;
            GUI.color = new Color(0f, 0.05f, 0.12f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, W, H), Texture2D.whiteTexture);
            GUI.color = old;

            float pw = 480f, ph = 300f;
            var pr = new Rect((W - pw) * 0.5f, (H - ph) * 0.5f, pw, ph);
            GUI.Box(pr, GUIContent.none, panel);

            if (game.ShowWin)
            {
                ShadowLabel(new Rect(pr.x, pr.y + 22f, pw, 80f), "YOU WIN!", panelTitle, shadowPanelTitle);
                GUI.Label(new Rect(pr.x, pr.y + 106f, pw, 40f), "You made the 2048 cube!", panelSub);
                GUI.Label(new Rect(pr.x, pr.y + 142f, pw, 36f), "Score: " + game.Score, panelSub);

                if (GUI.Button(new Rect(pr.x + 36f, pr.y + 204f, 190f, 62f), "KEEP GOING", button)) game.ContinueAfterWin();
                if (GUI.Button(new Rect(pr.x + pw - 226f, pr.y + 204f, 190f, 62f), "NEW GAME", button)) game.NewGame();
            }
            else
            {
                ShadowLabel(new Rect(pr.x, pr.y + 22f, pw, 80f), "GAME OVER", panelTitle, shadowPanelTitle);
                GUI.Label(new Rect(pr.x, pr.y + 106f, pw, 40f), "No more moves", panelSub);
                GUI.Label(new Rect(pr.x, pr.y + 142f, pw, 36f), "Score: " + game.Score + "   Best: " + game.Best, panelSub);

                GUI.enabled = game.CanUndo;
                if (GUI.Button(new Rect(pr.x + 36f, pr.y + 204f, 190f, 62f), "UNDO", button)) game.Undo();
                GUI.enabled = true;
                if (GUI.Button(new Rect(pr.x + pw - 226f, pr.y + 204f, 190f, 62f), "TRY AGAIN", button)) game.NewGame();
            }
        }
    }
}
