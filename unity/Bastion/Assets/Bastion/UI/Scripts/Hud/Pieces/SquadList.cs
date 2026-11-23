using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The squad's cards, under the picked soldier's sheet, bottom left (UI-COMBATE UI-01, UI-02; hud.ts setSquad;
    /// docs/UNITY-UI.md §2.1): one per soldier, this player's first and then the partner's (Ana, 7/11 #12), its bust on a
    /// studio backdrop in its owner's colour with its states over it, its name in a strip of its own under it (#13), its
    /// health in green and its actions in yellow; the picked one bigger, no border round it (#13), all on one foot. Four
    /// take a quarter of the screen; with more the cards narrow, the row no wider than the room left of the keys. A click
    /// on one's own picks it, a double click takes the camera to it (UI-08); a click on the partner's takes the camera
    /// there; the pointer over a living one points it out on the map (Arturo, 31/10 #11). Drawn again only when the view
    /// changes.
    /// </summary>
    public sealed class SquadList
    {
        /// <summary>A card's width with four or fewer, what the picked one has more, the gap between them (panel pixels).</summary>
        const float CardW = 78;
        const float PickedMore = 14;
        const float Gap = 4;
        /// <summary>The room before Separados' second group (U27), its line in the middle (SquadList.uss .squad-cards__divider).</summary>
        const float GroupGap = 14;
        /// <summary>
        /// The row's widest, with more than four: the room left of the keys at 1440 (Ana, 7/11 #12: the partner's cards too,
        /// six and more in the row); the cards narrow to fit it, no narrower than MinW.
        /// </summary>
        const float MaxRow = 352;
        const float MinW = 40;
        /// <summary>The plate's padding a side (Hud.uss .squad-plate), and its least width.</summary>
        const float Padding = 8;
        const float Least = 354;
        /// <summary>Narrower than this a card says only the first name.</summary>
        const float Narrow = 64;
        /// <summary>
        /// With the finger (docs/IOS.md, phase 2; Gameplay, 7/11): the plate goes to the left, under the top strip, and the
        /// cards stand two a row, all as wide (the picked one marked, not bigger), so the keys have the foot of the screen.
        /// </summary>
        const float TouchCardW = 82;
        const int TouchColumns = 2;

        /// <summary>Two clicks on one card within this long are a double click (seconds, unscaled: the pause doesn't hold it).</summary>
        const float DoubleClick = 0.4f;

        readonly VisualElement host;
        readonly VisualElement plate;
        readonly IHudHandlers handlers;
        readonly TooltipLayer tips;
        string key;
        SquadView last;
        /// <summary>The last card clicked and when: the cards are drawn anew as one is picked, so the double click is counted here.</summary>
        string clicked;
        float clickedAt = -1;

        /// <summary>A soldier's bust by id (Arte's Portraits.Soldier); null, none yet.</summary>
        public Func<string, Texture> PortraitOf;

        /// <param name="host">The cards' row.</param>
        /// <param name="plate">The squad's plate, as wide as the row needs.</param>
        /// <summary>
        /// On narrow busts (six or more) the states as icons (Arturo, in the cuadro, 18/11: the words didn't fit, and shorter
        /// ones would have changed their names everywhere); their names in full in the bust's help, on the flag over the
        /// figure and on the picked one's sheet. BASTION_ESTADOS_ICONO=0 shows the words, for a capture of before.
        /// </summary>
        public static bool StateIcons = System.Environment.GetEnvironmentVariable("BASTION_ESTADOS_ICONO") != "0";

        /// <summary>The HUD's icons (HudRoot), for the states as icons.</summary>
        public Func<string, Sprite> Icons;

        public SquadList(VisualElement host, VisualElement plate, IHudHandlers handlers, TooltipLayer tips)
        {
            this.host = host;
            this.plate = plate;
            this.handlers = handlers;
            this.tips = tips;
        }

        public void Set(SquadView view)
        {
            last = view;
            var k = Ui.Key(view);
            if (k == key) return;
            key = k;
            host.Clear();
            var cards = view?.Cards ?? new List<SquadCardView>();
            // No soldier yet (the HUD before the first view): no plate either.
            Ui.Show(plate, cards.Count > 0);
            var n = cards.Count;
            if (Bastion.Client.GameInput.Touch)
            {
                // With the finger: two a row, all as wide, the plate as wide as two.
                plate.style.width = TouchColumns * TouchCardW + (TouchColumns - 1) * Gap + 2 * Padding + 2;
                for (var i = 0; i < n; i++)
                {
                    var card = Card(cards[i], TouchCardW);
                    if (i % TouchColumns > 0) card.style.marginLeft = Gap;
                    if (i >= TouchColumns) card.style.marginTop = Gap;
                    host.Add(card);
                }
                return;
            }
            var groups = 0;
            for (var i = 1; i < n; i++)
                if (cards[i].GroupStart) groups++;
            var extra = groups * (GroupGap - Gap);
            var width = n <= 4 ? CardW : Mathf.Max(MinW, Mathf.Floor((MaxRow - PickedMore - (n - 1) * Gap - extra) / n));
            var row = n * width + PickedMore + Mathf.Max(0, n - 1) * Gap + extra;
            plate.style.width = Mathf.Max(Least, row + 2 * Padding);
            for (var i = 0; i < n; i++)
            {
                // Separados' groups apart (U27): the wider gap, a line down its middle.
                if (i > 0 && cards[i].GroupStart) Ui.El("squad-cards__divider", host).pickingMode = PickingMode.Ignore;
                var card = Card(cards[i], cards[i].Selected ? width + PickedMore : width);
                if (i > 0 && !cards[i].GroupStart) card.style.marginLeft = Gap;
                host.Add(card);
            }
        }

        /// <summary>A bust came: the cards drawn again with it.</summary>
        public void Redraw()
        {
            key = null;
            Set(last);
        }

        VisualElement Card(SquadCardView c, float width)
        {
            var tree = Ui.Clone("SquadCard");
            var card = tree.Q("card");
            // By its soldier, for the labs' presses (PackSteps «retrato»).
            card.name = "card-" + c.Id;
            card.style.width = width;
            card.EnableInClassList("squad-card--mine", c.Mine);
            card.EnableInClassList("squad-card--theirs", !c.Mine);
            card.EnableInClassList("squad-card--selected", c.Selected);
            card.EnableInClassList("squad-card--dead", c.Dead);
            card.EnableInClassList("squad-card--evacuated", c.Evacuated);
            card.EnableInClassList("squad-card--in-play", c.InPlay);
            // The bust on a studio backdrop in its owner's colour (Ana, 7/11 #13): the colour dark, lit in its middle.
            var owner = Ui.Hex(c.Color, new Color(0.5f, 0.5f, 0.5f));
            tree.Q("bust").style.backgroundColor = new Color(owner.r * 0.3f, owner.g * 0.3f, owner.b * 0.3f, 1);
            var studio = tree.Q("studio");
            studio.style.backgroundImage = new StyleBackground(Studio());
            studio.style.unityBackgroundImageTintColor = new Color(owner.r, owner.g, owner.b, 0.85f);
            // The bust's edges lit from behind in the same colour (Arte's Portraits.Light, 0.12.24).
            Bastion.Client.Portraits.Light(c.Id, owner);
            // The partner's, lit and ringed in their colour (cuadro 124, Arturo 23/11): dimmed is for the fallen alone, now
            // with its word over the bust.
            if (!c.Mine && !c.Dead)
            {
                card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = owner;
                card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = 2;
            }
            if (c.Dead) Ui.Text(c.Female ? HudText.Squad.FallenFemale : HudText.Squad.Fallen, "squad-card__fallen font-title", tree.Q("bust")).pickingMode = PickingMode.Ignore;
            var face = tree.Q("face");
            var portrait = c.Bust ? PortraitOf?.Invoke(c.Id) : null;
            if (portrait is RenderTexture rt) face.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
            else if (portrait is Texture2D t2) face.style.backgroundImage = new StyleBackground(t2);
            var narrow = width - (c.Selected ? PickedMore : 0) < Narrow;
            card.EnableInClassList("squad-card--narrow", narrow);
            // The first name (or the nickname) always, as the base's Soldados (Producto, 18/11: «Anja» beside «Yuki Tanaka»
            // was all of a piece with nothing); the whole name is on the sheet over the cards.
            FitName(tree.Q<Label>("name"), !string.IsNullOrEmpty(c.Short) ? c.Short : c.Name, $"{width}|{c.Selected}|{narrow}");
            Ui.Pips(tree.Q("hp"), c.Hp, c.MaxHp);
            var ap = tree.Q("ap");
            Ui.Pips(ap, c.Ap, c.ApMax);
            // The movement left, a piece a tile, apart from the actions (Arturo, 5/11 #14).
            var moves = tree.Q("moves");
            if (c.MovesMax > 0) Ui.Pips(moves, c.Moves, c.MovesMax);
            else moves.RemoveFromHierarchy();
            if (c.Rounds)
            {
                ap.RemoveFromClassList("pips--ap");
                ap.AddToClassList("pips--ammo");
            }
            // Its states over the bust, the first two (the soldier in play while turns go one at a time first: playtest
            // 15/10 #9); all of them, with what each means, in the card's help.
            var tags = tree.Q("tags");
            var help = new List<string> { c.Name, c.Title };
            var shown = 0;
            // Narrow, as icons (StateIcons): two in a row, as big as the bust lets them (18 px at six, 14 at eight), «EN
            // JUEGO» left to the card's top edge; all of them, with their words, in the help.
            var asIcons = StateIcons && narrow && Icons != null;
            tags.EnableInClassList("squad-card__tags--icons", asIcons);
            var side = Mathf.Clamp(Mathf.Floor((width - 10 - 2) / 2), 13, 18);
            void Tag(string text, string kind, string tip, string icon = null)
            {
                if (asIcons && icon != null && Icons(icon) != null)
                {
                    if (shown++ < 2)
                    {
                        var mark = Ui.Icon(Icons, icon, "squad-card__state tag--" + kind, tags);
                        mark.style.width = side;
                        mark.style.height = side;
                    }
                }
                else if (!(asIcons && kind == "in-play") && shown++ < 2) FitTag(Ui.Text(text, "tag squad-card__tag tag--" + kind, tags), $"{width}");
                help.Add(string.IsNullOrEmpty(tip) ? text : $"{text}: {tip}");
            }
            if (c.InPlay) Tag(HudText.Squad.InPlay, "in-play", HudText.Squad.InPlayTip);
            foreach (var t in c.Tags) Tag(t.Text, t.Kind, t.Tip, t.Icon);
            tips.Attach(card, string.Join("\n", help));
            var id = c.Id;
            if (c.Mine && !c.Dead) card.RegisterCallback<ClickEvent>(_ => Click(id));
            // The partner's: the camera to it (Ana, 7/11 #12).
            else if (!c.Dead && !c.Evacuated) card.RegisterCallback<ClickEvent>(_ => handlers.OnLookUnit(id));
            if (!c.Dead && !c.Evacuated)
            {
                card.RegisterCallback<PointerEnterEvent>(_ => handlers.OnHoverSquad(id));
                card.RegisterCallback<PointerLeaveEvent>(_ => handlers.OnHoverSquad(null));
            }
            // The card out of its template's container: the row lays out cards.
            card.RemoveFromHierarchy();
            return card;
        }

        /// <summary>The names already measured on a card of that width (one line in the log each, if it doesn't fit).</summary>
        static readonly HashSet<string> namesMeasured = new HashSet<string>();

        /// <summary>
        /// The card's name, the first one: never smaller; one that doesn't fit whole is said in the log, for Lore (Producto,
        /// 18/11: a shorter word, not a smaller one).
        /// </summary>
        static void FitName(Label label, string name, string card)
        {
            label.text = name;
            var key = name + "|" + card;
            if (namesMeasured.Contains(key)) return;
            label.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var room = label.contentRect.width;
                if (float.IsNaN(room) || room <= 0 || !namesMeasured.Add(key)) return;
                var wide = label.MeasureTextSize(name, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;
                if (wide > room + 0.5f) Debug.Log($"[ui] «{name}» no cabe entero en el busto ({wide:0.#} de {room:0.#} px)");
            });
        }

        /// <summary>The size a state's word was made to fit the bust whole, by the word and the card's width.</summary>
        static readonly Dictionary<string, float> tagSizes = new Dictionary<string, float>();

        /// <summary>
        /// The least a state's word goes down to, half a point under its own 8.5 (Producto, 18/11: read at the interface's
        /// 90 %). A word that doesn't fit so is Lore's to shorten, not smaller: it is cut at the end as before.
        /// </summary>
        const float LeastTag = 8f;

        /// <summary>
        /// A state's word over the bust, whole (Producto, 18/11: «EVACUA…»): a little smaller and closer when it doesn't fit,
        /// down to LeastTag; the words that still don't fit say so in the log, for Lore.
        /// </summary>
        static void FitTag(Label tag, string card)
        {
            tag.pickingMode = PickingMode.Ignore;
            var key = tag.text + "|" + card;
            if (tagSizes.TryGetValue(key, out var size))
            {
                if (size > 0)
                {
                    tag.style.fontSize = size;
                    tag.style.letterSpacing = 0;
                }
                return;
            }
            tag.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var room = tag.contentRect.width;
                if (float.IsNaN(room) || room <= 0) return;
                var wide = tag.MeasureTextSize(tag.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;
                if (wide <= room + 0.5f)
                {
                    if (!tagSizes.ContainsKey(key)) tagSizes[key] = tag.style.fontSize.keyword == StyleKeyword.Undefined ? 0 : tag.resolvedStyle.fontSize;
                    return;
                }
                var now = tag.resolvedStyle.fontSize;
                if (now <= LeastTag)
                {
                    tagSizes[key] = LeastTag;
                    Debug.Log($"[ui] «{tag.text}» no cabe entero en el busto ({wide:0.#} de {room:0.#} px a {LeastTag} px)");
                    return;
                }
                tag.style.letterSpacing = 0;
                tag.style.fontSize = Mathf.Max(LeastTag, Mathf.Floor(now * room / wide * 0.97f * 4) / 4);
            });
        }

        static Texture2D studio;

        /// <summary>
        /// The studio's light behind a bust (Ana, 7/11 #13; ours: a soft round glow, white for its tint), strongest a little
        /// above the middle, where the head is, and gone at the edges. The base's Soldados too (SoldiersScreen, 0.12.24).
        /// </summary>
        public static Texture2D Studio()
        {
            if (studio != null) return studio;
            const int size = 64;
            studio = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Estudio" };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    // The texture's rows go up: its middle-high on the card is 0.6 of the way up.
                    var dx = (x + 0.5f) / size - 0.5f;
                    var dy = (y + 0.5f) / size - 0.6f;
                    var r = Mathf.Sqrt(dx * dx + dy * dy) / 0.62f;
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(1 - r * r));
                }
            studio.SetPixels(pixels);
            studio.Apply();
            return studio;
        }

        /// <summary>A click picks the soldier; the second on the same card soon after takes the camera to it (UI-08).</summary>
        void Click(string id)
        {
            var now = Time.realtimeSinceStartup;
            var twice = clicked == id && now - clickedAt < DoubleClick;
            clicked = twice ? null : id;
            clickedAt = now;
            if (twice) handlers.OnLookUnit(id);
            else handlers.OnSelectUnit(id);
        }
    }
}
