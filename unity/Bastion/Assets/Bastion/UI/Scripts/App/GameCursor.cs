using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Bastion.UI.App
{
    /// <summary>
    /// The game's own cursors (playtest 19/10 #5, «del estilo del juego»; styles.css --cursor-*: Synty's Apocalypse HUD,
    /// Resources/Cursors): the arrow; the arrow in the accent's yellow over whatever can be pressed (a button of the
    /// panels, or what the 3D under it says a click picks: a piece of the clearing, one's own soldier); the crosshair on an
    /// enemy to shoot; the brackets to signal a spot (Mayús). One for the whole app, made as it starts, drawn by the system
    /// (Cursor.SetCursor); UI Toolkit's own `cursor` works only in the editor. The 3D asks each frame (Ask); unasked, the arrow.
    /// </summary>
    public sealed class GameCursor : MonoBehaviour
    {
        public enum Kind { Arrow, Hot, Aim, Signal }

        static readonly string[] Names = { "pointer", "pointer-hot", "aim", "signal" };
        /// <summary>Where each one points, in its 32 px (styles.css: 2 2 for the arrows, 16 16 for the others).</summary>
        static readonly Vector2[] Spots = { new Vector2(2, 2), new Vector2(2, 2), new Vector2(16, 16), new Vector2(16, 16) };
        /// <summary>The panels' list is looked for again this often (s): screens come and go with the scenes.</summary>
        const float Look = 0.5f;

        static Kind asked;
        static int askedAt = -10;

        Texture2D[] pictures;
        int shown = -1;
        readonly List<IPanel> panels = new List<IPanel>();
        float panelsAt = -10;

        /// <summary>What the 3D under the pointer asks for this frame (the battle's map, the clearing).</summary>
        public static void Ask(Kind kind)
        {
            asked = kind;
            askedAt = Time.frameCount;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Make()
        {
            // The captures have no pointer.
            if (Application.isBatchMode) return;
            var o = new GameObject("Cursor del juego");
            DontDestroyOnLoad(o);
            o.AddComponent<GameCursor>();
        }

        void Awake()
        {
            pictures = new Texture2D[Names.Length];
            for (var i = 0; i < Names.Length; i++) pictures[i] = Resources.Load<Texture2D>("Cursors/" + Names[i]);
        }

        void OnDestroy() => UnityEngine.Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

        // After every Update: what the 3D asked this frame is in.
        void LateUpdate()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            var at = mouse.position.ReadValue();
            var kind = askedAt >= Time.frameCount - 1 ? asked : Kind.Arrow;
            // Over a panel the 3D has no say: the yellow arrow on what can be pressed, else the arrow.
            if (OverPanel(at, out var pressable)) kind = pressable ? Kind.Hot : Kind.Arrow;
            Show(kind);
        }

        void Show(Kind kind)
        {
            if ((int)kind == shown) return;
            shown = (int)kind;
            var picture = pictures[(int)kind];
            UnityEngine.Cursor.SetCursor(picture, picture != null ? Spots[(int)kind] : Vector2.zero, CursorMode.Auto);
        }

        /// <summary>Whether a screen point (y up) is on a panel of any screen, and on something there that can be pressed.</summary>
        bool OverPanel(Vector2 at, out bool pressable)
        {
            pressable = false;
            if (Time.unscaledTime - panelsAt > Look)
            {
                panelsAt = Time.unscaledTime;
                panels.Clear();
                foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                {
                    var panel = doc.rootVisualElement?.panel;
                    if (panel != null && !panels.Contains(panel)) panels.Add(panel);
                }
            }
            var over = false;
            foreach (var panel in panels)
            {
                if (panel?.visualTree == null) continue;
                var picked = panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, new Vector2(at.x, Screen.height - at.y)));
                if (picked == null || picked == panel.visualTree || picked.parent == panel.visualTree || picked.pickingMode != PickingMode.Position) continue;
                over = true;
                for (var e = picked; e != null; e = e.parent)
                    if (Pressable(e))
                    {
                        pressable = e.enabledInHierarchy;
                        return true;
                    }
            }
            return over;
        }

        /// <summary>What a click does something on (styles.css: button, select, label, checkbox, radio, range), or marked `hot`.</summary>
        static bool Pressable(VisualElement e) =>
            e is Button || e is Toggle || e is Slider || e is SliderInt || e is DropdownField || e.ClassListContains("hot");
    }
}
