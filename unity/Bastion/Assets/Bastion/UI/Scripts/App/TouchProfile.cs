using System;
using Bastion.Client;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.App
{
    /// <summary>
    /// The screens at finger size (docs/IOS.md, phase 2): where the game is played with the finger (GameInput.Touch), a
    /// design pixel is an iOS point (the screen's own scale), so 44 of them are Apple's 44-point target on a Duo or an iPad
    /// alike. Room then: the Duo open 951 × 669 (closed, 678 × 466: too small), an iPad mini 1133 × 744, an 11" 1194 × 834
    /// (the desktop's HUD is laid out on 1440 × 900). And each screen's root gets the class `touch`, for the styles that
    /// only the finger has (bigger keys, the touch-only buttons, no keys' names). The desktop keeps its panel as it is.
    /// </summary>
    sealed class TouchProfile : MonoBehaviour
    {
        /// <summary>A design pixel's size on the glass, dots an inch: an iPhone's point.</summary>
        public const float Dpi = 161;

        /// <summary>How often the screens are looked over for one without the class (screens come with the scenes and the flow).</summary>
        const float Look = 0.5f;

        /// <summary>
        /// The screen's short side, in design pixels, under which it is too small to play (Gameplay, 7/11: a Duo closed is
        /// 466 of them, open 669, an iPad mini 744): a notice over everything to open it.
        /// </summary>
        public const float TooSmall = 600;

        static PanelSettings touchPanel;
        /// <summary>
        /// The touch copy of each panel a screen came with: the app has one (the project's); a lab capture puts copies of
        /// its own on the screens, drawing into its texture (Shots.PanelsInto), and the touch copy of one keeps that.
        /// </summary>
        static readonly System.Collections.Generic.Dictionary<PanelSettings, PanelSettings> touchOf = new System.Collections.Generic.Dictionary<PanelSettings, PanelSettings>();
        float lookedAt = -10;
        UIDocument notice;
        /// <summary>The screens' room in design pixels, as a screen's panel has laid it out (the lab's captures too, whose Screen says otherwise).</summary>
        Vector2 room;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Make()
        {
            if (!GameInput.Touch) return;
            var o = new GameObject("Perfil táctil");
            DontDestroyOnLoad(o);
            o.hideFlags = HideFlags.HideInHierarchy;
            o.AddComponent<TouchProfile>();
        }

        /// <summary>
        /// Screen pixels in a design pixel: on a phone or a tablet an iOS point, the screen's own scale (GameInput.PixelsPerPoint:
        /// Screen.dpi said 326 on the simulator's Duo, @3x, and the closed one measured as big as the open one); on the desktop
        /// the screen's dots an inch over Dpi. BASTION_TACTIL_ESCALA sets it (the lab, on a screen with no dots an inch said:
        /// 1 lays the HUD out as a Duo has it, on a capture of 951 × 669).
        /// </summary>
        public static float PixelsPerDesign
        {
            get
            {
                var said = Environment.GetEnvironmentVariable("BASTION_TACTIL_ESCALA");
                if (!string.IsNullOrEmpty(said) && float.TryParse(said, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) && s > 0) return s;
                if (Application.isMobilePlatform) return GameInput.PixelsPerPoint;
                return Screen.dpi > 0 ? Screen.dpi / Dpi : 1;
            }
        }

        void Update()
        {
            if (Time.unscaledTime - lookedAt < Look) return;
            lookedAt = Time.unscaledTime;
            foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (doc.panelSettings == null || doc == notice) continue;
                // A copy of the screen's panel, at a constant size (the asset itself untouched: in the editor a change to it stays).
                var had = doc.panelSettings;
                if (!touchOf.ContainsValue(had))
                {
                    if (!touchOf.TryGetValue(had, out var copy) || copy == null)
                    {
                        copy = Instantiate(had);
                        copy.name = had.name + " (táctil)";
                        copy.scaleMode = PanelScaleMode.ConstantPixelSize;
                        copy.scale = PixelsPerDesign;
                        touchOf[had] = copy;
                        var w = had.targetTexture != null ? had.targetTexture.width : Screen.width;
                        var h = had.targetTexture != null ? had.targetTexture.height : Screen.height;
                        Debug.Log($"[táctil] {w} × {h} px, {Screen.dpi:0} ppp: un píxel del HUD son {copy.scale:0.##} de la pantalla; caben {w / copy.scale:0} × {h / copy.scale:0}");
                    }
                    touchPanel = copy;
                    doc.panelSettings = copy;
                }
                var root = doc.rootVisualElement;
                if (root != null && !root.ClassListContains("touch")) root.AddToClassList("touch");
                var laid = root?.panel?.visualTree?.layout.size ?? Vector2.zero;
                if (laid.x > 0 && laid.y > 0 && !float.IsNaN(laid.x)) room = laid;
            }
            Notice();
        }

        /// <summary>
        /// A Duo closed (its outer screen) or any screen too small: over everything, to open it (Lore's words). The game goes
        /// on underneath (for the room, as one with the game out of sight); the fingers on it are the notice's.
        /// </summary>
        void Notice()
        {
            if (touchPanel == null || room == Vector2.zero) return;
            var small = Mathf.Min(room.x, room.y) < TooSmall;
            if (notice == null)
            {
                if (!small) return;
                var o = new GameObject("Aviso del Duo cerrado");
                o.transform.SetParent(transform, false);
                notice = o.AddComponent<UIDocument>();
                notice.panelSettings = touchPanel;
                notice.sortingOrder = 10000;
                var root = notice.rootVisualElement;
                root.AddToClassList("touch");
                var veil = new VisualElement { name = "duo-cerrado" };
                veil.style.position = Position.Absolute;
                veil.style.left = veil.style.top = veil.style.right = veil.style.bottom = 0;
                veil.style.backgroundColor = new Color(0.04f, 0.05f, 0.06f, 1f);
                veil.style.alignItems = Align.Center;
                veil.style.justifyContent = Justify.Center;
                var words = new Label(Text);
                words.style.maxWidth = Length.Percent(80);
                words.style.whiteSpace = WhiteSpace.Normal;
                words.style.unityTextAlign = TextAnchor.MiddleCenter;
                words.style.fontSize = 22;
                words.style.color = new Color(0.93f, 0.91f, 0.86f);
                veil.Add(words);
                root.Add(veil);
                Debug.Log($"[táctil] pantalla pequeña: {room.x:0} × {room.y:0}: el aviso");
            }
            notice.rootVisualElement.style.display = small ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>The notice's words (Lore, .dev-snapshots/lore/TEXTOS-TACTILES.md).</summary>
        public const string Text = "Abre el teléfono para jugar: Bastion necesita la pantalla grande.";
    }
}
