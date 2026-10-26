using System;
using System.Collections.Generic;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The Mapa (screen.ts renderGeoscape, geoscape.ts, valley.css): over the valley (ValleyMap) the zones' markers
    /// (the pin in its state's colour, the name, a Nido's skull, the operations' pins, a rumour), the places one knows
    /// the valley by, the Bastion and La Lata, placed every frame; on the left Misiones, Esperar un día, Nidos,
    /// Encargos, Rumores and Red de enclaves; on the right the mission picked (its briefing, Desplegar) or the zone.
    /// The valley fits the room the panels leave.
    /// </summary>
    public sealed class MapScreen
    {
        readonly VisualElement marks;
        readonly VisualElement left;
        readonly VisualElement right;
        readonly TooltipLayer tips;
        readonly Func<string, Sprite> icon;
        readonly IStrategyHandlers handlers;
        readonly Dictionary<string, VisualElement> zones = new Dictionary<string, VisualElement>();
        readonly List<(VisualElement e, float x, float z)> places = new List<(VisualElement, float, float)>();
        VisualElement lata;
        ValleyMap valley;
        MapView last;
        string key;
        bool shown;

        public MapScreen(VisualElement view, TooltipLayer tips, Func<string, Sprite> icon, IStrategyHandlers handlers)
        {
            this.tips = tips;
            this.icon = icon;
            this.handlers = handlers;
            marks = Ui.El("valley-marks", view);
            marks.pickingMode = PickingMode.Ignore;
            left = Ui.El("strat-side strat-side--left strat-side--fit map-left panel", view);
            right = Ui.El("strat-side strat-side--right strat-side--fit panel", view);
            left.Add(new ScrollView(ScrollViewMode.Vertical) { name = "left-scroll", verticalScrollerVisibility = ScrollerVisibility.Auto, horizontalScrollerVisibility = ScrollerVisibility.Hidden });
            right.Add(new ScrollView(ScrollViewMode.Vertical) { name = "right-scroll", verticalScrollerVisibility = ScrollerVisibility.Auto, horizontalScrollerVisibility = ScrollerVisibility.Hidden });
            marks.schedule.Execute(Place).Every(16);
        }

        /// <summary>The valley under the map's panels (StrategyRoot makes it with its camera).</summary>
        public void Bind(ValleyMap map)
        {
            valley = map;
            if (map?.Data == null) return;
            // The places one knows the valley by, the Bastion and La Lata: made once.
            foreach (var l in map.Data.landmarks)
            {
                var e = Ui.El("valley-landmark", marks);
                // Lore's name, not valley.json's copy of it (that goes stale when Lore changes one).
                var name = Bastion.Engine.Lore.Landmarks.TryGetValue(l.key ?? "", out var named) ? named : l.name;
                Ui.Text(name.ToUpperInvariant(), "valley-landmark__name", e);
                places.Add((e, l.x, l.z));
            }
            var home = Ui.El("valley-place", marks);
            Ui.Icon(icon, "map:home", "valley-home", home);
            Ui.Text(Bastion.Engine.Lore.Landmarks["atalaya"].ToUpperInvariant(), "valley-name valley-name--home font-title", home);
            places.Add((home, map.Data.bastion.x, map.Data.bastion.z));
            lata = Ui.El("valley-lata", marks);
            Ui.El("valley-lata__shadow", lata);
            Ui.El("valley-lata__edge", lata);
            Ui.El("valley-lata__body", lata);
            Ui.Text(Bastion.Engine.Lore.Bus.Name.ToUpperInvariant(), "valley-lata__name", lata);
            vehicle = -1;
            Vehicle(last?.Vehicle ?? 1);
            Show(shown);
        }

        /// <summary>
        /// La Lata as the campaign has it (valley.css .valley-bus, #45): the vehicle's own profile (the 0.10.1's
        /// silhouettes: Resources/Valley/lata-0 to 3, todoterreno, furgoneta, camión, autobús) at its proportions, and the
        /// name just past it: left, top, width and height from La Lata's place, and the name's left (UI pixels).
        /// </summary>
        static readonly (float x, float y, float w, float h, float name)[] Vehicles =
        {
            (-19, -7, 38, 14, 22), (-16, -10, 32, 20, 19), (-18, -7, 37, 15, 22), (-22, -7, 44, 13, 25),
        };

        int vehicle = -1;

        /// <summary>The profile three times: a soft shadow under it, a dark edge a pixel round it, the yellow over.</summary>
        void Vehicle(int n)
        {
            n = Mathf.Clamp(n, 0, Vehicles.Length - 1);
            if (lata == null || n == vehicle) return;
            vehicle = n;
            var picture = Resources.Load<Texture2D>($"Valley/lata-{n}");
            var (x, y, w, h, name) = Vehicles[n];
            Box(lata.Q(className: "valley-lata__shadow"), picture, x, y + 1.5f, w, h);
            Box(lata.Q(className: "valley-lata__edge"), picture, x - 1, y - 1, w + 2, h + 2);
            Box(lata.Q(className: "valley-lata__body"), picture, x, y, w, h);
            lata.Q(className: "valley-lata__name").style.left = name;
        }

        static void Box(VisualElement e, Texture2D picture, float x, float y, float w, float h)
        {
            e.style.backgroundImage = picture;
            e.style.left = x;
            e.style.top = y;
            e.style.width = w;
            e.style.height = h;
        }

        public bool Bound => valley != null;

        /// <summary>The clearing's word on the valley (StrategyRoot: Settlement.MapShown); none, no clearing: it goes with the tab.</summary>
        bool? valleyUp;

        public void ValleyUp(bool? on)
        {
            valleyUp = on;
            Show(shown);
        }

        /// <summary>The panels with the tab; the valley when the clearing gives it the screen, the marks over it with both.</summary>
        public void Show(bool on)
        {
            shown = on;
            var up = valleyUp ?? on;
            Ui.Show(marks, on && up);
            Ui.Show(left, on && last != null);
            Ui.Show(right, on && last?.Detail != null);
            valley?.Show(up);
        }

        public void Set(MapView v)
        {
            var k = Ui.Key(v);
            if (k == key) return;
            key = k;
            last = v;
            var leftBody = left.Q<ScrollView>("left-scroll").contentContainer;
            var rightBody = right.Q<ScrollView>("right-scroll").contentContainer;
            leftBody.Clear();
            rightBody.Clear();
            Show(shown);
            if (v == null) return;
            valley?.SetLevel(v.Level);
            valley?.DriveTo(v.Lata);
            Vehicle(v.Vehicle);
            Zones(v);
            Head("Misiones", null, leftBody);
            if (v.Missions.Count == 0 && !string.IsNullOrEmpty(v.MissionsEmpty)) Ui.Text(v.MissionsEmpty, "map-empty muted", leftBody);
            foreach (var m in v.Missions) Mission(m, leftBody);
            if (v.Wait != null) Wait(v.Wait, leftBody);
            if (v.Nests.Count > 0)
            {
                Head("Nidos", v.NestsTip, leftBody);
                foreach (var n in v.Nests) Row(n, "nest", leftBody);
            }
            if (v.Quests.Count > 0)
            {
                Head("Encargos", v.QuestsTip, leftBody);
                foreach (var q in v.Quests) Row(q, "quest", leftBody);
            }
            if (v.Rumors.Count > 0)
            {
                Head("Rumores", v.RumorsTip, leftBody);
                foreach (var r in v.Rumors) Rumor(r, leftBody);
            }
            Head("Red de enclaves", StrategyText.Map.NetworkTip, leftBody);
            Meter(v.Contacts, v.ContactsMax, v.ContactsTip, leftBody);
            if (v.Detail != null) Detail(v.Detail, rightBody);
        }

        void Head(string text, string tip, VisualElement parent)
        {
            var head = Ui.El("strat-head map-head", parent);
            Ui.Text(text.ToUpperInvariant(), "strat-head__text font-title", head);
            if (!string.IsNullOrEmpty(tip)) tips.Attach(head, tip);
        }

        /// <summary>An operation's pin: the map pin in its tone, a dark window, its kind's icon in the tone.</summary>
        VisualElement Pin(string tone, string kind, string cls, VisualElement parent)
        {
            var color = Ui.Hex(tone, new Color(1f, 0.69f, 0.19f));
            // The pack's pin fills 55 % × 76 % of its picture: drawn larger than its box, so the pin itself is the box.
            var pin = Ui.El("mission-pin " + cls, parent);
            pin.pickingMode = PickingMode.Ignore;
            var shape = Ui.Icon(icon, "map:pin", "mission-pin__shape", pin);
            shape.style.unityBackgroundImageTintColor = color;
            var window = Ui.El("mission-pin__window", pin);
            var k = Ui.Icon(icon, "kind:" + kind, "mission-pin__kind", window);
            k.style.unityBackgroundImageTintColor = color;
            return pin;
        }

        void Mission(MissionRowView m, VisualElement parent)
        {
            var id = m.Id;
            var row = new Button(() => handlers.OnOffer(id));
            Ui.AddClasses(row, "map-row map-row--mission");
            row.EnableInClassList("map-row--on", m.Selected);
            Pin(m.Tone, m.Kind, "", row);
            var words = Ui.El("map-row__words", row);
            Ui.Text(m.Name, "map-row__name font-title", words);
            Ui.Text(m.Where, "map-row__line", words);
            Chip(m.Chip, m.ChipTone, m.ChipTip, row);
            if (!string.IsNullOrEmpty(m.Tip)) tips.Attach(row, m.Tip);
            parent.Add(row);
        }

        void Row(MapRowView r, string kind, VisualElement parent)
        {
            var id = r.Id;
            var row = new Button(() => handlers.OnZone(id, false));
            Ui.AddClasses(row, "map-row map-row--" + kind);
            row.EnableInClassList("map-row--on", r.Selected);
            var words = Ui.El("map-row__words", row);
            Ui.Text(r.Name, "map-row__name font-title", words);
            if (!string.IsNullOrEmpty(r.Line)) Ui.Text(r.Line, "map-row__line", words);
            Chip(r.Chip, r.ChipTone, r.ChipTip, row);
            parent.Add(row);
        }

        void Rumor(RumorRowView r, VisualElement parent)
        {
            var row = Ui.El("map-row map-row--rumor", parent);
            row.EnableInClassList("map-row--on", r.Selected);
            var words = Ui.El("map-row__words", row);
            Ui.Text(r.Region, "map-row__name font-title", words);
            Ui.Text(r.Text, "map-row__line", words);
            if (!string.IsNullOrEmpty(r.Listening)) Ui.Text(r.Listening, "map-row__listening font-title", words);
            else if (!string.IsNullOrEmpty(r.Button))
            {
                var id = r.Id;
                var b = new Button(() => handlers.OnListen(id)) { text = r.Button.ToUpperInvariant() };
                Ui.AddClasses(b, "armory-small map-row__listen font-title");
                b.SetEnabled(r.Enabled);
                if (!string.IsNullOrEmpty(r.Why)) tips.Attach(b, r.Why);
                words.Add(b);
            }
            Chip(r.Chip, r.ChipTone, null, row);
        }

        void Chip(string text, string tone, string tip, VisualElement parent)
        {
            if (string.IsNullOrEmpty(text)) return;
            var chip = Ui.Text(text, "strat-chip map-row__chip", parent);
            if (!string.IsNullOrEmpty(tone)) chip.AddToClassList("strat-chip--" + tone);
            if (!string.IsNullOrEmpty(tip)) tips.Attach(chip, tip);
        }

        void Wait(WaitDayView w, VisualElement parent)
        {
            var b = new Button(handlers.OnWait) { text = w.Label.ToUpperInvariant() };
            Ui.AddClasses(b, "map-wait font-title");
            b.EnableInClassList("btn-primary", w.Asked && !w.Ready);
            b.EnableInClassList("map-wait--ready", w.Ready);
            b.SetEnabled(w.Enabled);
            if (!string.IsNullOrEmpty(w.Tip)) tips.Attach(b, w.Tip);
            parent.Add(b);
            if (!string.IsNullOrEmpty(w.Why)) Ui.Text(w.Why, "strat-why strat-why--small", parent);
            if (!string.IsNullOrEmpty(w.Note)) Ui.Text(w.Note, "map-wait__note", parent);
        }

        void Meter(int filled, int total, string tip, VisualElement parent)
        {
            var row = Ui.El("map-meter", parent);
            if (!string.IsNullOrEmpty(tip)) tips.Attach(row, tip);
            var marks = Ui.El("map-meter__marks", row);
            for (var i = 0; i < total; i++) Ui.El(i < filled ? "map-meter__mark map-meter__mark--on" : "map-meter__mark", marks);
            Ui.Text($"{filled}/{total}", "map-meter__text font-title", row);
        }

        void Detail(MapDetailView d, VisualElement parent)
        {
            if (!string.IsNullOrEmpty(d.Kicker))
            {
                var kicker = Ui.Text(d.Kicker.ToUpperInvariant(), "sheet__kicker font-title", parent);
                if (!string.IsNullOrEmpty(d.KickerTone)) kicker.AddToClassList("map-kicker--" + d.KickerTone);
            }
            Ui.Text(d.Title.ToUpperInvariant(), "armory-detail__name map-detail__title font-title", parent);
            if (!string.IsNullOrEmpty(d.Subtitle)) Ui.Text(d.Subtitle, "sheet__subtitle muted", parent);
            foreach (var t in d.Text) Ui.Text(StrategyText.Marked(t), "map-detail__text", parent);
            // The signature right under the report it signs, then the amber line (Lore, 26/10; as the 0.10.1).
            if (!string.IsNullOrEmpty(d.Note)) Ui.Text(d.Note, "map-detail__note", parent);
            if (!string.IsNullOrEmpty(d.Warning)) Ui.Text(d.Warning, "squad-mission__warning", parent);
            foreach (var l in d.Lines)
            {
                var line = Ui.Text(l.Text, l.Tone == "bad" ? "map-detail__line strat-chip--bad" : "map-detail__line muted", parent);
                if (!string.IsNullOrEmpty(l.Tip)) tips.Attach(line, l.Tip);
            }
            if (d.Missions.Count > 0)
            {
                Ui.Text(StrategyText.Map.ZoneMissions.ToUpperInvariant(), "sheet__head font-title", parent);
                foreach (var m in d.Missions) Mission(m, parent);
            }
            if (d.Rows.Count > 0)
            {
                var box = Ui.El("map-briefing", parent);
                foreach (var r in d.Rows)
                {
                    var row = Ui.El("map-briefing__row", box);
                    Ui.Text(r.Key.ToUpperInvariant(), "map-briefing__key font-title", row);
                    Ui.Text(r.Value, "map-briefing__value", row);
                }
            }
            if (d.Buttons.Count > 0)
            {
                var actions = Ui.El("map-detail__actions", parent);
                foreach (var b in d.Buttons)
                {
                    var action = b.Action;
                    var button = new Button(() => handlers.OnMapAction(action)) { text = b.Label.ToUpperInvariant() };
                    Ui.AddClasses(button, "map-detail__button font-title");
                    if (b.Primary) button.AddToClassList("btn-primary");
                    button.SetEnabled(b.Enabled);
                    actions.Add(button);
                }
            }
            if (!string.IsNullOrEmpty(d.Why)) Ui.Text(d.Why, "strat-why", parent);
            // The network's meter, under the contact's reason (screen.ts regionInfo).
            if (d.MeterMax > 0) Meter(d.Meter, d.MeterMax, StrategyText.Map.ZoneMeterTip, parent);
        }

        /// <summary>The zones' markers, made again when the view changes (they are few).</summary>
        void Zones(MapView v)
        {
            foreach (var z in zones.Values) z.RemoveFromHierarchy();
            zones.Clear();
            foreach (var z in v.Zones)
            {
                var id = z.Id;
                var mark = Ui.El("valley-zone", marks);
                mark.EnableInClassList("valley-zone--contacted", z.Contacted);
                mark.EnableInClassList("valley-zone--marea", z.Flooded);
                mark.EnableInClassList("valley-zone--selected", z.Selected);
                if (z.Selected) Ui.El("valley-zone__ring", mark);
                var pin = Ui.El("valley-zone__disc", mark);
                Ui.Icon(icon, "map:zone", "valley-zone__pin", pin);
                Ui.Text(z.Name.ToUpperInvariant(), "valley-name font-title", mark);
                var badges = Ui.El("valley-badges", mark);
                if (z.Nest) Ui.Icon(icon, "map:nest", "valley-badge valley-badge--nest", badges);
                for (var i = 0; i < z.Missions.Count; i++) Pin(z.Missions[i], i < z.Kinds.Count ? z.Kinds[i] : "elimination", "valley-badge valley-badge--mission", badges);
                if (z.Rumor)
                {
                    var rumor = Ui.Icon(icon, "map:rumor", "valley-badge valley-badge--rumor", badges);
                    rumor.EnableInClassList("valley-badge--listening", z.Listening);
                }
                // The zone's pin and name take the pointer: a click picks it.
                mark.pickingMode = PickingMode.Position;
                mark.RegisterCallback<ClickEvent>(_ => handlers.OnZone(id, true));
                zones[id] = mark;
            }
            if (lata != null) lata.BringToFront();
        }

        /// <summary>Each frame: the valley fitted to the free room, the markers on their places.</summary>
        void Place()
        {
            if (!shown || valley?.Data == null) return;
            var panel = marks.panel?.visualTree.layout.size ?? Vector2.zero;
            if (panel.x <= 0) return;
            var top = 46f;
            var bottom = panel.y - 48f;
            var leftEdge = left.resolvedStyle.display == DisplayStyle.None ? 0 : left.worldBound.xMax;
            var free = new Rect(leftEdge, top, panel.x - leftEdge, bottom - top);
            float? sheet = right.resolvedStyle.display == DisplayStyle.None ? (float?)null : right.worldBound.xMin;
            Vector2? keep = null;
            var data = valley.Data;
            if (last?.Selected != null)
            {
                var z = data.ZoneOf(last.Selected);
                if (z != null) keep = new Vector2(z.x, z.z);
            }
            valley.Fit(free, panel, keep, sheet);
            foreach (var z in data.zones)
            {
                if (!zones.TryGetValue(z.id, out var mark)) continue;
                At(mark, valley.ToPanel(z.x, z.z, panel));
            }
            // The places' names only once the map is big enough to read them beside the zones' (geoscape.ts: 30 px a unit).
            var big = valley.Scale >= 30;
            foreach (var (e, x, z) in places)
            {
                var landmark = e.ClassListContains("valley-landmark");
                At(e, landmark && !big ? null : valley.ToPanel(x, z, panel));
            }
            if (lata != null) At(lata, valley.ToPanel(valley.Lata.x, valley.Lata.y, panel));
        }

        static void At(VisualElement e, Vector2? p)
        {
            Ui.Show(e, p.HasValue);
            if (!p.HasValue) return;
            e.style.left = p.Value.x;
            e.style.top = p.Value.y;
        }
    }
}
