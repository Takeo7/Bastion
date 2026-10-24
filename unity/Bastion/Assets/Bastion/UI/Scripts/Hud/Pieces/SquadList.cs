using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The squad list, top left (hud.ts setSquad; docs/UNITY-UI.md §2.1): a card per soldier, one's own first. A click
    /// on one of this player's picks it. Drawn again only when the view changes.
    /// </summary>
    public sealed class SquadList
    {
        readonly VisualElement host;
        readonly IHudHandlers handlers;
        readonly TooltipLayer tips;
        string key;

        public SquadList(VisualElement host, IHudHandlers handlers, TooltipLayer tips)
        {
            this.host = host;
            this.handlers = handlers;
            this.tips = tips;
        }

        public void Set(SquadView view)
        {
            var k = Ui.Key(view);
            if (k == key) return;
            key = k;
            host.Clear();
            Ui.Show(host, view != null && view.Cards.Count > 0);
            if (view == null) return;
            foreach (var c in view.Cards) host.Add(Card(c));
        }

        VisualElement Card(SquadCardView c)
        {
            var tree = Ui.Clone("SquadCard");
            var card = tree.Q("card");
            card.EnableInClassList("squad-card--mine", c.Mine);
            card.EnableInClassList("squad-card--theirs", !c.Mine);
            card.EnableInClassList("squad-card--selected", c.Selected);
            card.EnableInClassList("squad-card--dead", c.Dead);
            card.EnableInClassList("squad-card--evacuated", c.Evacuated);
            card.EnableInClassList("squad-card--in-play", c.InPlay);
            tree.Q("accent").style.backgroundColor = Ui.Hex(c.Accent, new Color32(0xe8, 0xed, 0xf2, 0xff));
            if (c.Skills.Length > 0)
            {
                var hex = new SkillHex();
                hex.Set(c.Skills, tips);
                tree.Q("name-row").Insert(0, hex);
            }
            tree.Q<Label>("name").text = c.Name;
            Ui.Pips(tree.Q("hp"), c.Hp, c.MaxHp);
            var ap = tree.Q("ap");
            Ui.Pips(ap, c.Ap, c.ApMax);
            if (c.Rounds)
            {
                ap.RemoveFromClassList("pips--ap");
                ap.AddToClassList("pips--ammo");
            }
            var tags = tree.Q("tags");
            // The soldier in play while turns go one at a time (playtest 15/10 #9), first.
            if (c.InPlay) tips.Attach(Ui.Text(HudText.Squad.InPlay, "tag tag--in-play", tags), HudText.Squad.InPlayTip);
            foreach (var t in c.Tags)
            {
                var tag = Ui.Text(t.Text, "tag tag--" + t.Kind, tags);
                if (!string.IsNullOrEmpty(t.Tip)) tips.Attach(tag, t.Tip);
            }
            tips.Attach(card, c.Title);
            var id = c.Id;
            if (c.Mine && !c.Dead) card.RegisterCallback<ClickEvent>(_ => handlers.OnSelectUnit(id));
            return tree;
        }
    }
}
