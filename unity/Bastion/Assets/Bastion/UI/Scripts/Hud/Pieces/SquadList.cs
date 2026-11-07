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
            var width = n <= 4 ? CardW : Mathf.Max(MinW, Mathf.Floor((MaxRow - PickedMore - (n - 1) * Gap) / n));
            var row = n * width + PickedMore + Mathf.Max(0, n - 1) * Gap;
            plate.style.width = Mathf.Max(Least, row + 2 * Padding);
            for (var i = 0; i < n; i++)
            {
                var card = Card(cards[i], cards[i].Selected ? width + PickedMore : width);
                if (i > 0) card.style.marginLeft = Gap;
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
            var face = tree.Q("face");
            var portrait = c.Bust ? PortraitOf?.Invoke(c.Id) : null;
            if (portrait is RenderTexture rt) face.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
            else if (portrait is Texture2D t2) face.style.backgroundImage = new StyleBackground(t2);
            var narrow = width - (c.Selected ? PickedMore : 0) < Narrow;
            card.EnableInClassList("squad-card--narrow", narrow);
            tree.Q<Label>("name").text = narrow && !string.IsNullOrEmpty(c.Short) ? c.Short : c.Name;
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
            void Tag(string text, string kind, string tip)
            {
                if (shown++ < 2) Ui.Text(text, "tag squad-card__tag tag--" + kind, tags).pickingMode = PickingMode.Ignore;
                help.Add(string.IsNullOrEmpty(tip) ? text : $"{text}: {tip}");
            }
            if (c.InPlay) Tag(HudText.Squad.InPlay, "in-play", HudText.Squad.InPlayTip);
            foreach (var t in c.Tags) Tag(t.Text, t.Kind, t.Tip);
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

        static Texture2D studio;

        /// <summary>
        /// The studio's light behind a bust (Ana, 7/11 #13; ours: a soft round glow, white for its tint), strongest a little
        /// above the middle, where the head is, and gone at the edges.
        /// </summary>
        static Texture2D Studio()
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
