using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.UI.Gear;
using Bastion.UI.Hud;
using Bastion.UI.Strategy;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// A run's pack in combat (H3; .dev-snapshots/gameplay/runs/LEEME-H3.md): the soldier on the panel's pack, kit, the
    /// floor at its feet and the partners beside, as grids (RunPackView); a thing moved on them is the engine's `item`
    /// order, asked first (RunPack.Refusal, as the server would): to the pack from the floor «take», within it or from the
    /// kit «arrange» (free), onto a kit place «equip», to the floor «drop», onto a partner «give»; a medicine «use».
    /// </summary>
    public sealed partial class CombatController : IGearHandlers
    {
        /// <summary>Whose pack the panel shows: the orders' unit.</summary>
        string packUnit;

        /// <summary>The floor's grid: what lies there packed four wide, in the order it fell.</summary>
        const string Floor = "floor";

        RunPackView RunPackOf(GameState v, Unit u, bool live)
        {
            packUnit = u.Id;
            // A weapon in the pack or on the floor against what the soldier carries in its place (T4).
            var against = u.Kit != null ? (slot => Gears.InSlot(u.Kit, slot), u.Name.Split(' ')[0]) : ((System.Func<string, Bastion.Engine.Gear>, string)?)null;
            var view = new RunPackView { Pack = StrategyPresenter.Grid(u.Pack, u.Id, KitSlots.Pack, live, 0, against) };
            foreach (var slot in new[] { KitSlots.Primary, KitSlots.Melee, KitSlots.Pistol, KitSlots.Vest })
            {
                var (w, h) = Box(slot);
                var g = u.Kit != null ? Gears.InSlot(u.Kit, slot) : null;
                view.Kit.Add(new GearSlotView { Owner = u.Id, Slot = slot, Label = GearText.SlotLabel(slot), W = w, H = h, Gear = g != null ? StrategyPresenter.Item(g) : null, Mine = live });
            }
            // The belt as it stands, its uses left (RunPack.BeltLeft).
            var belt = RunPack.BeltLeft(u);
            for (var i = 0; i < 2; i++)
                view.Kit.Add(new GearSlotView { Owner = u.Id, Slot = i == 0 ? KitSlots.Belt0 : KitSlots.Belt1, Label = "", W = 1, H = 1, Gear = i < belt.Length && belt[i] != null ? StrategyPresenter.Item(belt[i]) : null, Mine = live });
            // What lies at its feet, and what a fallen comrade beside left (G16, U12: from beside its body, as Registrar).
            var underfoot = RunPack.FloorInReach(v, u);
            if (underfoot.Count > 0)
            {
                var laid = new GearGrid { W = 4, H = 16 };
                foreach (var d in underfoot)
                    if (Gears.FirstFit(laid, d.Gear) is Placed at) Gears.Place(laid, d.Gear, at.X, at.Y, at.Turned);
                var rows = laid.Items.Count == 0 ? 1 : laid.Items.Max(p => p.Y + (p.Turned ? Content.GearSize(p.Gear.Kind, p.Gear.Type).W : Content.GearSize(p.Gear.Kind, p.Gear.Type).H));
                laid.H = rows;
                view.Floor = StrategyPresenter.Grid(laid, Floor, null, live, 0, against);
            }
            if (live)
                foreach (var o in v.Units.Where(o => o.Id != u.Id && o.Alive && o.Team == Teams.Xcom && !o.Captive && o.Pack != null && Aim.InMeleeReach(v, u.Pos, o.Pos)))
                    view.Give.Add(new GearSlotView { Owner = o.Id, Slot = "give", Label = o.Name.Split(' ')[0].ToUpperInvariant(), W = 2, H = 1, Mine = true });
            return view;
        }

        /// <summary>A kit place's box in combat, in cells of 40 points (smaller than the base's: the panel is narrow).</summary>
        static (int, int) Box(string slot)
        {
            switch (slot)
            {
                case KitSlots.Primary: return (4, 1);
                case KitSlots.Melee: return (3, 1);
                case KitSlots.Pistol: return (2, 1);
                case KitSlots.Vest: return (2, 1);
                default: return (1, 1);
            }
        }

        /// <summary>The order a move on the grids is: by where the thing is and where it goes; null, none (it can't go there).</summary>
        ItemCommand ItemOf(GearMove m)
        {
            var u = UnitById(State, packUnit);
            if (u == null || m.Onto != null || m.Throw) return null;
            var inPack = RunPack.InPack(u, m.Gear) != null;
            var onFloor = RunPack.OnFloor(State, m.Gear) != null;
            var inKit = RunPack.SlotOf(u, m.Gear) != null;
            if (m.Slot == "give") return inPack ? new ItemCommand { Unit = u.Id, Action = ItemActions.Give, Gear = m.Gear, To = m.To } : null;
            if (m.To == Floor) return inPack ? new ItemCommand { Unit = u.Id, Action = ItemActions.Drop, Gear = m.Gear } : null;
            if (m.To != u.Id) return null;
            if (m.Slot == KitSlots.Pack)
            {
                if (onFloor) return new ItemCommand { Unit = u.Id, Action = ItemActions.Take, Gear = m.Gear, X = m.X, Y = m.Y, Turned = m.Turned };
                if (inPack || inKit) return new ItemCommand { Unit = u.Id, Action = ItemActions.Arrange, Gear = m.Gear, X = m.X, Y = m.Y, Turned = m.Turned };
                return null;
            }
            return inPack ? new ItemCommand { Unit = u.Id, Action = ItemActions.Equip, Gear = m.Gear, Slot = m.Slot } : null;
        }

        public string GearRefusal(GearMove m)
        {
            if (State == null) return "";
            var cmd = ItemOf(m);
            if (cmd == null) return HudText.Pack.NotThere;
            if (!HasCommand()) return CommandLock();
            return RunPack.Refusal(State, cmd, MySlot, ActiveSlots());
        }

        public void GearMoved(GearMove m)
        {
            var cmd = ItemOf(m);
            if (cmd != null) Send(cmd);
        }

        /// <summary>No bin in combat: a thing is dropped (it stays on the floor).</summary>
        public void GearDropped(string gear) { }

        /// <summary>A thing's other orders: a medicine on itself or a partner beside; drop it; give it to one beside.</summary>
        public IReadOnlyList<GearActionView> GearActions(string gear)
        {
            var list = new List<GearActionView>();
            var u = UnitById(State, packUnit);
            var placed = u != null ? RunPack.InPack(u, gear) : null;
            if (placed == null) return list;
            string Why(ItemCommand c) => !HasCommand() ? CommandLock() : RunPack.Refusal(State, c, MySlot, ActiveSlots());
            var near = State.Units.Where(o => o.Id != u.Id && o.Alive && o.Team == Teams.Xcom && !o.Captive && o.Pack != null && Aim.InMeleeReach(State, u.Pos, o.Pos)).ToList();
            // Mordido's bitten beside (U23), found, before talking to them too: a medicine heals their health, not the bite
            // (the engine's why, Lore's MedicineNo), and the antiviral cures them.
            var bitten = State.Mission.Bitten == true && UnitById(State, State.Mission.ObjectiveUnit) is Unit b && b.Alive && b.Id != u.Id && !Unfound(b) && Aim.InMeleeReach(State, u.Pos, b.Pos) ? b : null;
            GearActionView UseOn(Unit o) => new GearActionView { Id = "use:" + o.Id, Label = HudText.Pack.HealOther(o.Name.Split(' ')[0]), Why = Why(new ItemCommand { Unit = u.Id, Action = ItemActions.Use, Gear = gear, To = o.Id }) };
            if (placed.Gear.Kind == GearKinds.Medicine)
            {
                list.Add(new GearActionView { Id = "use:" + u.Id, Label = HudText.Pack.UseSelf, Why = Why(new ItemCommand { Unit = u.Id, Action = ItemActions.Use, Gear = gear, To = u.Id }) });
                foreach (var o in near) list.Add(UseOn(o));
                if (bitten != null && !near.Contains(bitten)) list.Add(UseOn(bitten));
            }
            if (placed.Gear.Kind == GearKinds.Antiviral)
            {
                // On the bitten beside; the bitten themselves, on themselves.
                if (u.Id == State.Mission.ObjectiveUnit) list.Add(new GearActionView { Id = "use:" + u.Id, Label = HudText.Pack.UseSelf, Why = Why(new ItemCommand { Unit = u.Id, Action = ItemActions.Use, Gear = gear, To = u.Id }) });
                if (bitten != null) list.Add(UseOn(bitten));
            }
            foreach (var o in near)
                list.Add(new GearActionView { Id = "give:" + o.Id, Label = HudText.Pack.GiveTo(o.Name.Split(' ')[0]), Why = Why(new ItemCommand { Unit = u.Id, Action = ItemActions.Give, Gear = gear, To = o.Id }) });
            list.Add(new GearActionView { Id = "drop", Label = HudText.Pack.DropIt, Why = Why(new ItemCommand { Unit = u.Id, Action = ItemActions.Drop, Gear = gear }) });
            return list;
        }

        public void GearAction(string gear, string action)
        {
            var u = UnitById(State, packUnit);
            if (u == null) return;
            var at = action.IndexOf(':');
            var what = at < 0 ? action : action.Substring(0, at);
            var to = at < 0 ? null : action.Substring(at + 1);
            switch (what)
            {
                case "use": Send(new ItemCommand { Unit = u.Id, Action = ItemActions.Use, Gear = gear, To = to }); break;
                case "give": Send(new ItemCommand { Unit = u.Id, Action = ItemActions.Give, Gear = gear, To = to }); break;
                case "drop": Send(new ItemCommand { Unit = u.Id, Action = ItemActions.Drop, Gear = gear }); break;
            }
        }
    }
}
