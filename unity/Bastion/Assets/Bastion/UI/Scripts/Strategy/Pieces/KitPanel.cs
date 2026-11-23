using System;
using System.Linq;
using Bastion.UI.Gear;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// A soldier's kit on a side plate (the runs): the soldiers to choose as tabs, with their health in a run, and the
    /// chosen one's primary, melee weapon, pistol, vest, the belt's two and the pack, on the grids' board. Almacén's right
    /// plate, and the squad's on the map between stages. A soldier is chosen with OnSoldier, as in Soldados.
    /// </summary>
    public sealed class KitPanel
    {
        public readonly VisualElement Root;
        readonly TooltipLayer tips;
        readonly IStrategyHandlers handlers;
        readonly GearBoard board;
        readonly VisualElement chooser;
        readonly Label head;
        readonly Label name;
        readonly Label line;
        readonly VisualElement foot;
        readonly Label note;
        readonly VisualElement pack;
        readonly VisualElement[] holders = new VisualElement[6];
        string chooserKey;
        System.Collections.Generic.List<KitTabView> lastSoldiers;

        /// <summary>A soldier's bust by id (StrategyRoot's: Arte's Portraits); null, the silhouette.</summary>
        public System.Func<string, Texture> PortraitOf;

        /// <summary>The HUD's icons (the silhouette while a bust is drawn).</summary>
        public System.Func<string, Sprite> Icon;

        static readonly string[] Places = { "primary", "melee", "pistol", "vest", "belt0", "belt1" };

        /// <summary>
        /// The squad folded to its busts (the map on paper, cuadro 132): the chosen one's kit only once «Equipo ▸» is pressed,
        /// and back with «Mapa ◂». Folded changes go to `FoldChanged` (the map hides its stage card while the kit is open).
        /// </summary>
        public event System.Action<bool> FoldChanged;
        public bool Folded { get; private set; }
        readonly VisualElement body;
        readonly Button fold;
        string openWords, closeWords;

        /// <summary>The fold's key and its two words (null: never folded, as Almacén's); folded to start with.</summary>
        public void Foldable(string open, string close)
        {
            openWords = open;
            closeWords = close;
            Fold(true);
        }

        public void Fold(bool folded)
        {
            if (openWords == null) return;
            Folded = folded;
            Ui.Show(body, !folded);
            fold.text = (folded ? openWords : closeWords).ToUpperInvariant();
            Ui.Show(fold, true);
            FoldChanged?.Invoke(folded);
        }

        public KitPanel(VisualElement parent, string classes, TooltipLayer tips, IStrategyHandlers handlers, GearBoard board)
        {
            this.tips = tips;
            this.handlers = handlers;
            this.board = board;
            Root = Ui.El(classes, parent);
            var top = Ui.El("kit-top", Root);
            head = Ui.Text("", "kit-head strat-head__text font-title", top);
            fold = new Button(() => Fold(!Folded)) { name = "kit-fold" };
            Ui.AddClasses(fold, "kit-fold font-title");
            top.Add(fold);
            Ui.Show(fold, false);
            chooser = Ui.El("stash-chooser", Root);
            // What folds away: the chosen one's name, board, places, pack and note.
            body = Ui.El("kit-body", Root);
            name = Ui.Text("", "stash-name font-title", body);
            line = Ui.Text("", "stash-line muted", body);
            // Soldados' «Tablero de habilidades» between stages under the chosen one's name (U10, Arturo 18/11 cuadro 56:
            // «B»), and its points to spend.
            foot = Ui.El("skill-foot kit-board", body);
            var places = Ui.El("stash-places", body);
            // The primary on its own; the melee weapon and the pistol; the vest and the belt's two.
            var row1 = Ui.El("stash-row", places);
            holders[0] = Ui.El("stash-place", row1);
            var row2 = Ui.El("stash-row", places);
            holders[1] = Ui.El("stash-place", row2);
            holders[2] = Ui.El("stash-place", row2);
            var row3 = Ui.El("stash-row", places);
            holders[3] = Ui.El("stash-place", row3);
            var belt = Ui.El("stash-belt", row3);
            Ui.Text(GearText.Belt, "stash-label muted font-title", belt);
            var beltRow = Ui.El("stash-row stash-row--tight", belt);
            holders[4] = Ui.El("stash-place stash-place--tight", beltRow);
            holders[5] = Ui.El("stash-place stash-place--tight", beltRow);
            Ui.Text(GearText.Pack, "stash-label muted font-title", body);
            pack = Ui.El("stash-pack", body);
            note = Ui.Text("", "stash-note muted", body);
        }

        /// <summary>`soldiers` as tabs and `kit`'s places (null: only the tabs); `title` over them, if any.</summary>
        public void Set(System.Collections.Generic.List<KitTabView> soldiers, KitView kit, string title = null)
        {
            head.text = (title ?? "").ToUpperInvariant();
            Ui.Show(head, !string.IsNullOrEmpty(title));
            Chooser(soldiers);
            Ui.Show(name, kit != null);
            Ui.Show(line, kit != null && !string.IsNullOrEmpty(kit.Line));
            foreach (var h in holders) Ui.Show(h, false);
            Ui.Show(pack, kit != null);
            if (kit == null)
            {
                Ui.Show(note, false);
                Ui.Show(foot, false);
                return;
            }
            name.text = (kit.Name ?? "").ToUpperInvariant();
            line.text = kit.Line ?? "";
            for (var i = 0; i < Places.Length; i++)
            {
                var s = kit.Slots.FirstOrDefault(p => p.Slot == Places[i]);
                Ui.Show(holders[i], s != null);
                if (s != null) board.SetSlot(holders[i], s);
            }
            if (kit.Pack != null) board.SetGrid(pack, kit.Pack);
            note.text = kit.Note ?? "";
            Ui.Show(note, !string.IsNullOrEmpty(kit.Note));
            foot.Clear();
            Ui.Show(foot, kit.Board != null);
            if (kit.Board != null)
            {
                var id = kit.Soldier;
                var open = new Button(() => handlers.OnBoardFor(id)) { text = kit.Board.ToUpperInvariant() };
                Ui.AddClasses(open, "skill-board font-title");
                open.EnableInClassList("skill-board--pulse", kit.BoardPulse);
                foot.Add(open);
                if (!string.IsNullOrEmpty(kit.BoardPoints)) Ui.Text(kit.BoardPoints, "skill-foot__points font-title", foot);
            }
        }

        /// <summary>A bust came (StrategyRoot.Repaint): the tabs drawn again with it, the grids left as they are.</summary>
        public void Repaint()
        {
            if (lastSoldiers != null) Chooser(lastSoldiers);
        }

        /// <summary>
        /// The soldiers as tabs (anew only if they changed, or a bust came): a click chooses one. Each its bust on its
        /// studio's light as Soldados' (Arturo, 18/11 T5: «su icono igual que en soldados», not the name alone), its name
        /// under it, and in a run its health under that, every bar as wide as the next (T10: as wide as each name, a long
        /// name looked like more health).
        /// </summary>
        void Chooser(System.Collections.Generic.List<KitTabView> soldiers)
        {
            lastSoldiers = soldiers;
            var key = Ui.Key(soldiers) + "|" + StrategyRoot.PictureStamp;
            if (key == chooserKey) return;
            chooserKey = key;
            chooser.Clear();
            foreach (var s in soldiers)
            {
                var id = s.Id;
                var b = new Button(() => handlers.OnSoldier(id));
                Ui.AddClasses(b, "stash-tab font-title");
                // A thing let go over another's bust goes onto them (M12, Arturo 23/11: between stages too).
                board.SetSoldier(b, id, s.Mine);
                b.EnableInClassList("stash-tab--on", s.On);
                b.EnableInClassList("stash-tab--theirs", !s.Mine);
                b.EnableInClassList("stash-tab--hp", s.HpMax > 0);
                var face = Ui.El("stash-tab__face", b);
                face.pickingMode = PickingMode.Ignore;
                SoldiersScreen.Studio(face, s.Theirs);
                Bastion.Client.Portraits.Light(id, s.Theirs ? SoldiersScreen.TheirLight : SoldiersScreen.MineLight);
                var bust = Ui.El("stash-tab__bust", face);
                bust.pickingMode = PickingMode.Ignore;
                var portrait = PortraitOf?.Invoke(id);
                if (portrait is RenderTexture rt) bust.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
                else if (portrait is Texture2D t2) bust.style.backgroundImage = new StyleBackground(t2);
                else if (Icon?.Invoke("tab:barracks") is Sprite silhouette)
                {
                    var shape = Ui.El("stash-tab__silhouette", bust);
                    shape.style.backgroundImage = new StyleBackground(silhouette);
                }
                // A cell to learn now (U10): Soldados' «▲» on the bust, a mark only (the board is the key under the name).
                if (s.Points)
                {
                    var mark = Ui.Text("▲", "soldier-tile__points font-title", face);
                    tips.Attach(mark, s.PointsTip);
                }
                Ui.Text(s.Name, "stash-tab__label", b).pickingMode = PickingMode.Ignore;
                if (s.HpMax > 0)
                {
                    var bar = Ui.El("stash-tab__hp", b);
                    var fill = Ui.El("stash-tab__fill", bar);
                    fill.style.width = Length.Percent(100f * Mathf.Clamp01(s.Hp / (float)s.HpMax));
                    fill.EnableInClassList("stash-tab__fill--low", s.Hp * 3 <= s.HpMax);
                }
                tips.Attach(b, s.Tip);
                chooser.Add(b);
            }
        }
    }
}
