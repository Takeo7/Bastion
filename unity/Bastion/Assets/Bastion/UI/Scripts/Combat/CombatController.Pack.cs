using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.UI.Hud;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// The backpack (docs/FASE8.md §C; game.ts packView, itemOrder, takeAll, swapItem, openSwap): what the soldier on the
    /// panel carries and what lies on its tile, as the panel shows it, and the orders on them, the engine's `item` command
    /// through the room like any other. A fallen soldier's rucksack is taken a thing at a time, each once the last is in;
    /// a swap with the backpack full leaves the slot on the floor, then picks up the find (LootSettled sends the second half).
    /// </summary>
    public sealed partial class CombatController
    {
        /// <summary>A fallen soldier's rucksack being taken (takeAll): whose backpack, and what is left to take.</summary>
        string packQueueUnit;
        List<string> packQueue;

        /// <summary>A swap's second half (swapItem): who takes what once the slot is on the floor.</summary>
        string swapUnit;
        string swapTake;

        /// <summary>
        /// The soldier's backpack and the squad's haul for the panel (game.ts packView): where there is anything to search,
        /// or it carries something, or something lies on its tile (a fallen soldier's rucksack: #49). Null, none.
        /// </summary>
        PackView PackOf(Unit u)
        {
            var v = Shown;
            var dropped = v.Dropped ?? new List<DroppedLoot>();
            var underfoot = dropped.Where(d => Grid.SamePos(d.Pos, u.Pos)).ToList();
            var backpack = u.Backpack ?? new List<Loot>();
            if (Content.Templates[u.Template].Team != Teams.Xcom || u.Template == TemplateIds.Vip || (!HasSpots && backpack.Count == 0 && underfoot.Count == 0)) return null;
            var size = u.PackSlots ?? Content.BackpackSlots;
            var pack = new PackView { Size = size, Live = Commands(u) && u.Alive && CanAct(), Tip = HudText.Pack.Tip(backpack.Count, size) };
            // The partner holds the command: what can be done is shown, each order saying why not yet (the engine's refusal).
            if (pack.Live && !HasCommand()) pack.Locked = HudText.Pack.Locked(PlayerName(State.Command));
            // Partners right next to it: whom it can hand a thing to, or treat.
            var near = v.Units.Where(o => o.Id != u.Id && o.Alive && o.Team == Teams.Xcom && !o.Captive && o.Template != TemplateIds.Vip && Aim.InMeleeReach(v, u.Pos, o.Pos)).ToList();
            for (var i = 0; i < backpack.Count; i++)
            {
                var slot = SlotOf(backpack[i]);
                if (pack.Live) slot.Acts = SlotActs(v, u, slot, backpack[i], i, near, pack.Locked);
                pack.Slots.Add(slot);
            }
            if (pack.Live)
            {
                foreach (var d in underfoot)
                {
                    var look = SlotOf(d.Loot);
                    pack.Floor.Add(new PackFloorView { Id = d.Id, Kind = look.Kind, Label = look.Label, Name = look.Name });
                }
                // A fallen soldier's rucksack (#49): its name, and one order that takes what fits.
                var room = size - backpack.Count;
                foreach (var whose in underfoot.Where(d => d.From != null).GroupBy(d => d.From))
                {
                    var name = UnitById(v, whose.Key)?.Name;
                    if (name == null) continue;
                    var ids = whose.Select(d => d.Id).ToList();
                    pack.Fallen.Add(new PackFallenView
                    {
                        Mark = Lore.FallenPackMark(name),
                        Action = Lore.FallenPackAction(name),
                        Ids = ids,
                        Off = room <= 0 || pack.Locked != null,
                        Tip = pack.Locked ?? (room <= 0 ? HudText.Pack.FullFallen : HudText.Pack.FallenTip(System.Math.Min(room, ids.Count), ids.Count)),
                    });
                }
            }
            // What the whole squad carries: only what gets home counts.
            var q = Looting.LootTotalsOf(v.Units.Where(o => o.Team == Teams.Xcom && (o.Alive || o.Evacuated)).SelectMany(o => o.Backpack ?? new List<Loot>()).ToList());
            void Total(string kind, int n, string text, string icon = null, bool after = false)
            {
                if (n != 0) pack.Totals.Add(new PackTotalView { Kind = kind, Text = text, Icon = icon, IconAfter = after });
            }
            Total("supplies", q.Supplies, $"{q.Supplies}§");
            Total("food", q.Food, q.Food.ToString(), "store:food", true);
            Total("parts", q.Parts, q.Parts.ToString(), "store:parts", true);
            Total("weapon", q.Weapons.Count, $"×{q.Weapons.Count}", "stat:weapon");
            Total("item", q.Items.Count, $"×{q.Items.Count}", "search");
            Total("medicines", q.Medicines, $"×{q.Medicines}", "store:medicines");
            pack.SquadTip = Lore.LootSummary(q.Supplies, q.Items, q.Medicines, q.Food, q.Parts, q.Weapons) ?? HudText.Pack.NothingTip;
            return pack;
        }

        /// <summary>A backpack's thing as its slot (game.ts packSlots; hud.ts slotEl): one kind each, the first it has.</summary>
        static PackSlotView SlotOf(Loot l)
        {
            var slot = new PackSlotView { Label = Lore.FindLabel(l), Name = Lore.LootName(l) };
            if (!string.IsNullOrEmpty(l.Item))
            {
                var def = Content.Items[l.Item];
                slot.Kind = "item";
                slot.Icon = ItemIcon(l.Item);
                slot.Tip = $"{def.Name}: {def.Description}";
            }
            else if (!string.IsNullOrEmpty(l.Weapon))
            {
                slot.Kind = "weapon";
                slot.Icon = Ui.WeaponIcon(Content.FoundWeapons[l.Weapon].Weapon);
                slot.Tip = HudText.Pack.Weapon(Content.FoundWeapons[l.Weapon].Name);
            }
            else if (l.Medicines.GetValueOrDefault() != 0)
            {
                slot.Kind = "medicines";
                slot.Icon = "store:medicines";
                slot.Tip = HudText.Pack.Medicines(l.Medicines.Value);
            }
            else if (l.Food.GetValueOrDefault() != 0)
            {
                slot.Kind = "food";
                slot.Text = l.Food.Value.ToString();
                slot.Icon = "store:food";
                slot.Tip = HudText.Pack.Food(l.Food.Value);
            }
            else if (l.Parts.GetValueOrDefault() != 0)
            {
                slot.Kind = "parts";
                slot.Text = l.Parts.Value.ToString();
                slot.Icon = "store:parts";
                slot.Tip = HudText.Pack.Parts(l.Parts.Value);
            }
            else
            {
                var n = l.Supplies ?? 0;
                slot.Kind = "supplies";
                slot.Text = $"{n}§";
                slot.Tip = HudText.Pack.Supplies(n);
            }
            return slot;
        }

        /// <summary>An accessory's icon on its slot (hud.ts ITEM_ICONS): the grenades', the medkit's, the vest's, the ear plugs', the rounds'.</summary>
        static string ItemIcon(string item)
        {
            switch (item)
            {
                case ItemIds.FirstAid: return AbilityIds.FirstAid;
                case ItemIds.NanoVest: return "stat:armor";
                case ItemIds.MindShield: return "base:will";
                case ItemIds.ApRounds:
                case ItemIds.TracerRounds: return "stat:ammo";
                default: return "stat:grenade";
            }
        }

        /// <summary>
        /// A slot's menu (hud.ts packMenuEl): soltar, free; equipar, ponerse, al cinturón, usar, curar a and dar a, an action
        /// each. One that can't be given now says why in place of its cost, in the engine's words (Rules.BackpackRefusal):
        /// a cure to one with no wounds, a thing to a partner whose backpack is full.
        /// </summary>
        static List<PackActView> SlotActs(GameState v, Unit u, PackSlotView slot, Loot l, int i, List<Unit> near, string locked)
        {
            var acts = new List<PackActView>();
            void Act(string label, string cost, PackOrder order, bool main = false, string off = null) =>
                acts.Add(new PackActView { Label = label, Cost = cost, Order = order, Main = main, Off = off ?? locked });
            PackOrder Order(string action, string to = null) => new PackOrder { Action = action, Index = i, To = to };
            var one = HudText.Pack.OneAction;
            if (slot.Kind == "weapon") Act(HudText.Pack.Equip, one, Order(ItemActions.Equip), true);
            // A vest is put on there and then (playtest 16/10), one at a time (rules.ts).
            if (slot.Kind == "item" && Rules.Wearable(l.Item)) Act(HudText.Pack.PutOn, one, Order(ItemActions.Equip), true, u.Worn?.Count > 0 ? HudText.Pack.VestOn : null);
            // On the belt, to use there and then (playtest 18/10 #5); what only gives, once.
            if (slot.Kind == "item" && Rules.Beltable(l.Item))
            {
                var worn = Content.Items[l.Item].Charges == null && u.Gear.Contains(l.Item);
                Act(Lore.BeltText.Button, one, Order(ItemActions.Equip), true, worn ? HudText.Pack.GearOn : null);
            }
            if (slot.Kind == "medicines")
            {
                bool Whole(Unit t) => t.Hp >= t.MaxHp && !(t.Infected == true && Rules.MedicineStopsBite(v, u));
                Act(HudText.Pack.Use, one, Order(ItemActions.Use), true, Whole(u) ? HudText.Pack.NoWoundsSelf : null);
                foreach (var n in near) Act(HudText.Pack.Heal(n.Name), one, Order(ItemActions.Use, n.Id), false, Whole(n) ? HudText.Pack.NoWounds : null);
            }
            foreach (var n in near) Act(HudText.Pack.Give(n.Name), one, Order(ItemActions.Give, n.Id), false, (n.Backpack?.Count ?? 0) >= (n.PackSlots ?? Content.BackpackSlots) ? HudText.Pack.NoRoom : null);
            Act(HudText.Pack.Drop, HudText.Pack.Gratis, Order(ItemActions.Drop));
            return acts;
        }

        /// <summary>«GASTADA» or «ROTA», and «+2» for its improvements (arms.ts wearMarks): a weapon of the Armería's.</summary>
        static List<TagView> WeaponMarks(double condition, List<string> mods)
        {
            var marks = new List<TagView>();
            if (condition <= 0) marks.Add(new TagView { Text = "ROTA", Kind = "broken", Tip = $"Rota: no dispara. Se arregla en la Armería. (estado {System.Math.Max(0, Js.Round(condition))})" });
            else if (condition < Content.WornBelow) marks.Add(new TagView { Text = "GASTADA", Kind = "worn", Tip = $"Gastada: −{Content.WornAim} de puntería. Pide piezas. (estado {Js.Round(condition)})" });
            if (mods != null && mods.Count > 0) marks.Add(new TagView { Text = $"+{mods.Count}", Kind = "mods", Tip = string.Join("\n", mods.Select(m => $"{Content.WeaponMods[m].Name}: {Content.WeaponMods[m].Line}")) });
            return marks;
        }

        // ------------------------------------------------------- the orders

        /// <summary>An order on the picked soldier's backpack or tile (game.ts itemOrder); the engine says why not, if not.</summary>
        public void OnItem(PackOrder o)
        {
            var u = Unit();
            if (u == null || !CanAct() || o == null) return;
            Send(new ItemCommand { Unit = u.Id, Action = o.Action, Index = o.Index >= 0 ? o.Index : (int?)null, Id = o.Id, To = o.To });
            Refresh();
        }

        /// <summary>A fallen soldier's rucksack taken (#49): one thing after another, each once the last is in, while it fits.</summary>
        public void OnTakeAll(List<string> ids)
        {
            var u = Unit();
            if (u == null || !CanAct() || ids == null || ids.Count == 0) return;
            packQueueUnit = u.Id;
            packQueue = ids.Skip(1).ToList();
            Send(new ItemCommand { Unit = u.Id, Action = ItemActions.Take, Id = ids[0] });
            Refresh();
        }

        /// <summary>The backpack full: slot `drop` left on the floor, then `take` picked up (LootSettled sends the second half).</summary>
        public void OnSwap(string take, int drop)
        {
            var u = Unit();
            if (u == null || !CanAct()) return;
            swapUnit = u.Id;
            swapTake = take;
            Send(new ItemCommand { Unit = u.Id, Action = ItemActions.Drop, Index = drop });
        }

        /// <summary>
        /// What a played event means for the backpack's orders under way (game.ts settle): the next thing of a rucksack
        /// being taken, while it fits and is still there; a swap's find, once its slot is on the floor. `v`: the view
        /// with the event in; `by`: whose order it was.
        /// </summary>
        public void LootSettled(GameEvent e, int? by, GameState v)
        {
            if (e is LootTakenEvent taken && taken.Unit == packQueueUnit && packQueue != null)
            {
                var u = UnitById(v, taken.Unit);
                var next = packQueue.FirstOrDefault(id => v.Dropped?.Any(d => d.Id == id) == true);
                packQueue.Remove(next);
                if (u != null && next != null && (u.Backpack?.Count ?? 0) < (u.PackSlots ?? Content.BackpackSlots)) Send(new ItemCommand { Unit = u.Id, Action = ItemActions.Take, Id = next });
                else packQueue = null;
            }
            if (e is LootDroppedEvent dropped && by == MySlot && dropped.Unit == swapUnit && swapTake != null)
            {
                var id = swapTake;
                swapTake = null;
                if (v.Dropped?.Any(d => d.Id == id) == true) Send(new ItemCommand { Unit = dropped.Unit, Action = ItemActions.Take, Id = id });
            }
        }

        /// <summary>
        /// A find with the backpack full stayed on the floor (docs/FASE8.md §C): the notice says so, with «Cambiar», which
        /// shows the soldier and asks its backpack what to leave for it (game.ts openSwap).
        /// </summary>
        public void PackFull(string unit, string id, string text)
        {
            if (Hud == null) return;
            Hud.PackOffer(text, () =>
            {
                if (picked != unit) Pick(unit);
                Hud.OpenSwap(unit, id);
                Refresh();
            });
        }
    }
}
