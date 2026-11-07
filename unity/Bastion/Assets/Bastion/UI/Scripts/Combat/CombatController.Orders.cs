using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.Net;
using UnityEngine;
using Grid = Bastion.Engine.Grid;

namespace Bastion.UI.Combat
{
    /// <summary>A target an ability can be aimed at (game.ts TargetOption): who, and for a blow the tile it is struck from.</summary>
    public sealed class TargetOption
    {
        public Unit Unit;
        public Pos Tile;
        public int Hit;
    }

    public sealed partial class CombatController
    {
        /// <summary>
        /// What is armed (game.ts mode): moving; aiming an ability at someone (Target); an area to throw at, or something by
        /// hand on a tile beside (Tile); a self ability (Self), which the first press already uses (playtest 19/10 #17).
        /// </summary>
        enum Mode { Move, Target, Tile, Self }

        Mode mode = Mode.Move;

        /// <summary>Something is armed to aim (an attack, a support ability, something by hand): not a throw, which waits for the pointer.</summary>
        public bool Armed => mode == Mode.Target || HandArmed() != null;

        /// <summary>What is armed and open, for the lab's log.</summary>
        public string LabState() => $"{(Unit() is Unit me ? $"{me.Id} acciones {me.Ap}, movimiento {Bastion.UI.Hud.HudText.Moves.Tiles(Paths.MoveBudget(me))} de {Bastion.UI.Hud.HudText.Moves.Tiles(Paths.FullMoves(me))} en {me.Pos.X}/{me.Pos.Y}, " : "")}cobertura {(CoverMatters() ? "sí" : "no")} (disparan: {string.Join(" ", Shown.Units.Where(u => u.Alive && u.Team == Teams.Alien && Shoots(u)).Select(u => u.Template + (Visible(u) ? "*" : "")).Distinct())}), modo {mode} {modeAbility ?? "-"}, blanco {targetId ?? "-"}, menú de la barra {(Hud != null && Hud.AbilityMenuOpen ? "abierto" : "cerrado")}, menú {(Hud != null && Hud.MenuOpen ? "abierto" : "cerrado")}; barra: {string.Join(" ", BarKeys().Select(v => $"{v.Id}{(v.Menu != null ? "<" + v.Menu : "")}{(v.Enabled ? "" : "(off)")}"))}";
        string modeAbility;
        string targetId;
        /// <summary>By hand: the tile picked among those within reach (the pointer, Tab or a click).</summary>
        Pos handPick;

        /// <summary>The marks of what is armed; made with the controller's.</summary>
        public AimOverlay AimMarks;

        /// <summary>«Terminar turno» pressed once with soldiers that can still act: a second press ends it, until this time.</summary>
        float endArmedUntil = -1;
        bool EndArmed => Time.time < endArmedUntil;

        /// <summary>«Evacuar» pressed once: the second asks, until this time; the soldier it is for.</summary>
        float evacArmedUntil = -1;
        string evacArmed;

        /// <summary>The partner's last soldier moved, for «Ver» (game.ts partnerLast); the game sets it from the events.</summary>
        public string PartnerLast;

        /// <summary>Why an order cannot go out yet: the partner holds the command.</summary>
        string CommandLock() => $"Tiene el mando {PlayerName(State.Command)}: puedes planear, pero no dar la orden.";

        /// <summary>The confirm button's text while planning; null with the command.</summary>
        string LockLabel() => HasCommand() ? null : $"MANDO: {PlayerName(State.Command).ToUpperInvariant()}";

        /// <summary>What is armed, checked against the latest state (game.ts refreshInteractive): gone if it no longer can be.</summary>
        void CheckMode()
        {
            // The turn or the command gone, nothing stays armed for later (Arturo, 5/11 #15).
            if (mode != Mode.Move && (!CanAct() || !HasCommand())) CancelModeSilently();
            if (mode == Mode.Target && CurrentTarget() == null) CancelModeSilently();
            if (mode == Mode.Tile || mode == Mode.Self)
            {
                var u = Unit();
                if (u == null || !CanAct() || Abilities.AbilityBlocker(State, u, modeAbility) != null) CancelModeSilently();
            }
        }

        void CancelModeSilently()
        {
            concealWarn = null;
            mode = Mode.Move;
            modeAbility = null;
            targetId = null;
            handPick = null;
        }

        void CancelMode()
        {
            CancelModeSilently();
            Refresh();
        }

        // ------------------------------------------------------- targets

        /// <summary>Valid targets for a targeted ability, best first (game.ts targetOptions).</summary>
        List<TargetOption> TargetOptions(string ability)
        {
            var u = Unit();
            if (u == null || ability == null) return new List<TargetOption>();
            var s = State;
            var def = Content.Abilities[ability];
            if (def.Target == AbilityTargets.Melee && Content.IsAttack(ability))
                return Abilities.MeleeTargets(s, u, ability)
                    .Select(o => new TargetOption { Unit = o.Target, Tile = o.Tile, Hit = Aim.PreviewShot(s, u, o.Target, new ShotOptions { Ability = ability, From = o.Tile })?.Hit ?? 0 })
                    .OrderByDescending(o => o.Hit).ToList();
            if (def.Target == AbilityTargets.Enemy && Content.IsAttack(ability))
                return Abilities.RangedTargets(s, u, ability).Where(Visible)
                    .Select(t => new TargetOption { Unit = t, Hit = Aim.PreviewShot(s, u, t, new ShotOptions { Ability = ability })?.Hit ?? 0 })
                    .OrderByDescending(o => o.Hit).ToList();
            if (def.Target == AbilityTargets.Enemy) return Abilities.AbilityTargets(s, u, ability).Where(Visible).Select(t => new TargetOption { Unit = t }).ToList();
            if (def.Target == AbilityTargets.Ally)
                return Abilities.AbilityTargets(s, u, ability).OrderBy(a => (double)a.Hp / a.MaxHp).Select(t => new TargetOption { Unit = t }).ToList();
            return new List<TargetOption>();
        }

        TargetOption CurrentTarget() => mode == Mode.Target ? TargetOptions(modeAbility).FirstOrDefault(o => o.Unit.Id == targetId) : null;

        void EnterTarget(string ability, string id = null)
        {
            var list = TargetOptions(ability);
            if (list.Count == 0)
            {
                Toast(Content.Abilities[ability].Target == AbilityTargets.Ally ? "No hay aliados válidos." : "No hay enemigos a su alcance.");
                return;
            }
            mode = Mode.Target;
            modeAbility = ability;
            targetId = id != null && list.Any(t => t.Unit.Id == id) ? id : list[0].Unit.Id;
            FocusTarget();
            Refresh();
        }

        /// <summary>An ability aimed at `target` (if it is one of its targets), as if picked: for a capture.</summary>
        public void ArmAt(string ability, string target) => EnterTarget(ability, target);

        void CycleTarget(int dir)
        {
            if (HandArmed() != null)
            {
                CycleHand(dir);
                return;
            }
            if (mode != Mode.Target) return;
            var list = TargetOptions(modeAbility);
            if (list.Count == 0) return;
            var i = list.FindIndex(t => t.Unit.Id == targetId);
            targetId = list[(i + dir + list.Count) % list.Count].Unit.Id;
            FocusTarget();
            Refresh();
        }

        /// <summary>Another target's odds clicked over its flag (Arturo, 5/11 #11): aimed at, over the shoulder too.</summary>
        void PickTarget(string id)
        {
            if (mode != Mode.Target || id == targetId || !TargetOptions(modeAbility).Any(o => o.Unit.Id == id)) return;
            targetId = id;
            Debug.Log($"[hombro] objetivo {id} por su rótulo");
            Refresh();
        }

        /// <summary>The camera between the soldier and its target.</summary>
        void FocusTarget()
        {
            var u = Unit();
            var t = UnitById(State, targetId);
            if (u != null && t != null && Rig != null) Rig.Focus(Vector3.Lerp(Board.TileToWorld(State, u.Pos), Board.TileToWorld(State, t.Pos), 0.5f));
        }

        void ConfirmTarget()
        {
            if (mode == Mode.Self)
            {
                ConfirmSelf();
                return;
            }
            // The armed panel's button for Registrar or a door (playtest 13/10 #11).
            if (HandArmed() != null)
            {
                ConfirmHand(HandTarget());
                return;
            }
            var u = Unit();
            if (u == null || mode != Mode.Target || targetId == null) return;
            // Without the command, aiming only shows the odds.
            if (!HasCommand())
            {
                Toast(CommandLock());
                return;
            }
            Send(new AbilityCommand { Unit = u.Id, Ability = modeAbility, Target = targetId });
            CancelMode();
        }

        /// <summary>A key of the bar, or a choice of one of its menus (game.ts useAbility).</summary>
        void UseAbility(string id)
        {
            if (id == "evac" || id == "evacAccept" || id == "evacRefuse")
            {
                UseEvac(id);
                return;
            }
            if (id == "abandon" || id == "abandonAccept" || id == "abandonRefuse")
            {
                var call = State.AbandonCall;
                if (id != "abandon" && call != null && call.By != MySlot && State.Outcome == null) Send(new AbandonAnswerMessage { Accept = id == "abandonAccept" });
                return;
            }
            var u = Unit();
            // Only in one's own turn and with the command: out of it a key neither arms nor waits for it (Arturo, 5/11 #15).
            if (u == null || !CanAct() || !HasCommand() || AbilityTexts.IsMenu(id)) return;
            // Its turn ended by hand (DoneCommand): what it has left goes, and another can play.
            if (id == DoneKey)
            {
                Send(new DoneCommand { Unit = u.Id });
                return;
            }
            var view = BarView(id);
            if (view == null || !view.Enabled)
            {
                if (view?.Reason != null) Toast(view.Reason);
                return;
            }
            switch (Content.Abilities[id].Target)
            {
                case AbilityTargets.Enemy:
                case AbilityTargets.Melee:
                case AbilityTargets.Ally:
                case AbilityTargets.Corpse:
                    if (mode == Mode.Target && modeAbility == id) ConfirmTarget();
                    else EnterTarget(id);
                    return;
                case AbilityTargets.Tile:
                    // By hand (Registrar, a door): armed on the tiles beside the soldier; a single door is opened or closed at once.
                    if (AbilityTexts.Hand.Contains(id))
                    {
                        var hand = HandTiles(id);
                        if (HandArmed() == id) ConfirmHand(HandTarget());
                        else if (hand.Count == 1) UseDoor(id, hand[0]);
                        else ArmHand(id);
                        return;
                    }
                    if (mode == Mode.Tile && modeAbility == id) CancelModeSilently();
                    else
                    {
                        mode = Mode.Tile;
                        modeAbility = id;
                        targetId = null;
                    }
                    Refresh();
                    return;
                case AbilityTargets.Self:
                    // The first press does it (playtest 19/10 #17): what it does is on the card, and there is nothing to aim.
                    mode = Mode.Self;
                    modeAbility = id;
                    targetId = null;
                    ConfirmSelf();
                    return;
            }
        }

        void ConfirmSelf()
        {
            var u = Unit();
            if (u == null || mode != Mode.Self) return;
            if (!HasCommand())
            {
                Toast(CommandLock());
                CancelMode();
                return;
            }
            Send(new AbilityCommand { Unit = u.Id, Ability = modeAbility });
            CancelMode();
        }

        /// <summary>
        /// Space and Enter use what is armed: a self ability goes, an aimed one fires at its target, a throw lands where the
        /// pointer is. With nothing armed, Space aims the first usable attack.
        /// </summary>
        void ConfirmOrAttack()
        {
            switch (mode)
            {
                case Mode.Self:
                    ConfirmSelf();
                    return;
                case Mode.Target:
                    ConfirmTarget();
                    return;
                case Mode.Tile:
                    if (HandArmed() != null) ConfirmHand(HandTarget());
                    else if (hover != null) ThrowAt(hover);
                    else Toast("Señala con el ratón dónde lanzarla.");
                    return;
            }
            // Nothing armed: the first attack, only with the command (#15).
            if (!CanAct() || !HasCommand()) return;
            var views = BarKeys();
            var usable = AbilityTexts.PrimaryAttacks.Select(id => views.FirstOrDefault(v => v.Id == id)).FirstOrDefault(v => v != null && v.Enabled);
            if (usable != null) UseAbility(usable.Id);
            else Toast(views.FirstOrDefault(v => v.Id == AbilityIds.Shoot)?.Reason ?? "No puede atacar ahora.");
        }

        /// <summary>A number key: a choice of the open menu, a key of the bar, or a menu opened (game.ts onKey).</summary>
        void NumberKey(int n)
        {
            // Out of one's turn or without the command, nothing (Arturo, 5/11 #15: the 3 armed Vigilar and waited).
            if (!CanAct() || !HasCommand()) return;
            if (Hud != null && Hud.AbilityMenuOpen)
            {
                Hud.PickMenu(n);
                return;
            }
            var bar = BarKeys();
            var keys = bar.Where(v => string.IsNullOrEmpty(v.Menu)).ToList();
            var i = n == 0 ? 9 : n - 1;
            if (i >= keys.Count) return;
            var v = keys[i];
            if (v.Id.StartsWith("evac")) return;
            var options = bar.Where(o => o.Menu == v.Id).ToList();
            if (options.Count > 1)
            {
                if (v.Enabled) Hud?.ToggleAbilityMenu(v.Id);
                else if (v.Reason != null) Toast(v.Reason);
            }
            else UseAbility(options.Count == 1 ? options[0].Id : v.Id);
        }

        /// <summary>Esc: a menu of the bar, then what is armed, then a pending «Terminar turno»; otherwise the game's menu.</summary>
        void Escape()
        {
            if (Hud != null && (Hud.CloseAbilityMenu() || Hud.ClosePackMenu())) return;
            if (CloseMiniMap()) return;
            if (mode != Mode.Move) CancelMode();
            // (A detected tile's warning too: game.ts.)
            else if (EndArmed || concealWarn != null)
            {
                concealWarn = null;
                DisarmEndTurn();
            }
            else ToggleMenu();
        }

        // ------------------------------------------------------- areas

        /// <summary>Throws (or casts) the area armed at `tile`, or as far toward it as it reaches.</summary>
        void ThrowAt(Pos tile)
        {
            var u = Unit();
            if (u == null || mode != Mode.Tile || !CanAct()) return;
            if (!HasCommand())
            {
                Toast(CommandLock());
                return;
            }
            var centre = AbilityTexts.ThrowTile(u, modeAbility, tile);
            if (Abilities.CanTargetTile(State, u, modeAbility, centre))
            {
                Send(new AbilityCommand { Unit = u.Id, Ability = modeAbility, Tile = centre });
                CancelMode();
            }
            else Toast("La escuadra no ve esa zona.");
        }

        /// <summary>A click on a tile with an area or something by hand armed (game.ts clickTile).</summary>
        void ClickTile(Pos tile)
        {
            var u = Unit();
            if (!CanAct() || u == null) return;
            var waiting = WaitingFor(u.Id);
            if (waiting != null)
            {
                Toast(waiting);
                return;
            }
            // By hand: a click on what is marked does it, anywhere else backs out.
            if (HandArmed() != null)
            {
                if (HandTiles().Any(t => Same(t, tile))) ConfirmHand(tile);
                else CancelMode();
                return;
            }
            ThrowAt(tile);
        }

        // ------------------------------------------------------- by hand

        /// <summary>Registrar, or a door opened or closed (playtest 13/10 #2), armed on the tiles beside the soldier.</summary>
        string HandArmed() => mode == Mode.Tile && modeAbility != null && AbilityTexts.Hand.Contains(modeAbility) ? modeAbility : null;

        /// <summary>The tiles beside the soldier `id` works on: furniture to search, closed or open doors, boarded windows.</summary>
        List<Pos> HandTiles(string id = null)
        {
            id ??= HandArmed();
            var u = Unit();
            if (u == null || id == null) return new List<Pos>();
            if (id == AbilityIds.Pry) return Abilities.BoardedBeside(State, u.Pos);
            return id == AbilityIds.Search ? Looting.SearchableTiles(State, u) : Abilities.DoorTiles(State, u, id == AbilityIds.OpenDoor ? "door" : "doorOpen");
        }

        /// <summary>The tile the armed hand would act on: the one picked, else the first within reach.</summary>
        Pos HandTarget()
        {
            var list = HandTiles();
            return (handPick != null ? list.FirstOrDefault(t => Same(t, handPick)) : null) ?? list.FirstOrDefault();
        }

        void ArmHand(string id, Pos tile = null)
        {
            mode = Mode.Tile;
            modeAbility = id;
            targetId = null;
            handPick = tile ?? HandTarget();
            Refresh();
        }

        void CycleHand(int dir)
        {
            var list = HandTiles();
            if (list.Count < 2) return;
            var current = HandTarget();
            var i = list.FindIndex(t => current != null && Same(t, current));
            handPick = list[(i + dir + list.Count) % list.Count];
            Refresh();
        }

        void ConfirmHand(Pos tile)
        {
            var u = Unit();
            var id = HandArmed();
            if (u == null || id == null || !CanAct()) return;
            if (!HasCommand())
            {
                Toast(CommandLock());
                return;
            }
            if (tile == null)
            {
                Toast(id == AbilityIds.Search ? "No hay nada que abrir ni registrar a tu lado." : id == AbilityIds.Pry ? "No hay ninguna ventana ni puerta tapiada a tu lado." : "No hay ninguna puerta a tu lado.");
                return;
            }
            Send(new AbilityCommand { Unit = u.Id, Ability = id, Tile = tile });
            CancelMode();
        }

        /// <summary>By hand on `tile`, at once: a door opened or closed (free, and it can be undone).</summary>
        void UseDoor(string id, Pos tile)
        {
            var u = Unit();
            if (u == null || !CanAct()) return;
            if (!HasCommand())
            {
                Toast(CommandLock());
                return;
            }
            Send(new AbilityCommand { Unit = u.Id, Ability = id, Tile = tile });
            CancelMode();
        }

        // ------------------------------------------------------- marks

        /// <summary>
        /// The marks of what is armed (game.ts aim and updateHover): the target's ring (marked, an enemy picked to shoot or
        /// strike; a line to an ally); a throw's reach and its area where the pointer is; what the hand reaches.
        /// </summary>
        void ShowArmed()
        {
            Pose();
            if (AimMarks == null) return;
            AimMarks.Clear();
            var u = Unit();
            if (u == null) return;
            var hand = HandArmed();
            if (hand != null)
            {
                // The pointer on one of those within reach picks it. A body or a car's boot gets its rucksack instead of the
                // tile's frame (Arturo, 5/11 #12).
                var list = HandTiles();
                if (hover != null && list.Any(t => Same(t, hover))) handPick = hover;
                var pick = HandTarget();
                var things = hand == AbilityIds.Search ? list.Where(t => Looting.SearchSpot(State, t) is string k && IsBodyOrBoot(k)).ToList() : new List<Pos>();
                AimMarks.ShowSpots(State, list.Where(t => !things.Any(b => Same(b, t))).ToList(), pick);
                searchNext = things.Select(t => (t, Same(t, pick))).ToList();
                return;
            }
            if (mode == Mode.Tile)
            {
                AimMarks.ShowRange(State, u.Pos, Abilities.AbilityRange(u, modeAbility));
                if (hover == null) return;
                var centre = AbilityTexts.ThrowTile(u, modeAbility, hover);
                var area = Abilities.BlastTiles(State, modeAbility, centre, u);
                var allies = area.Where(t => Grid.UnitAt(State, t)?.Team == Teams.Xcom).ToList();
                AimMarks.ShowBlast(State, centre, area, Abilities.CanTargetTile(State, u, modeAbility, centre), allies, Abilities.BreachedWalls(State, modeAbility, centre, u), modeAbility == AbilityIds.Molotov);
                return;
            }
            var option = CurrentTarget();
            if (option == null) return;
            var friendly = Content.Abilities[modeAbility].Target == AbilityTargets.Ally;
            var attack = Content.IsAttack(modeAbility) && !friendly;
            var shot = attack ? Aim.PreviewShot(State, u, option.Unit, new ShotOptions { Ability = modeAbility, From = option.Tile }) : null;
            AimMarks.ShowAim(State, shot?.Origin ?? u.Pos, option.Unit.Pos, friendly, attack);
        }

        /// <summary>
        /// Where a throw lands, its line (game.ts updateHover; docs/FASE7.md §4–5): «Ruido 4 · Rompe una pared · Hunde el
        /// suelo», what of it there is; none, the tile's own line stays.
        /// </summary>
        void ThrowNote()
        {
            var u = Unit();
            if (u == null || hover == null) return;
            var centre = AbilityTexts.ThrowTile(u, modeAbility, hover);
            var loud = Abilities.NoiseOf(State, u, modeAbility);
            var walls = Abilities.BreachedWalls(State, modeAbility, centre, u).Count;
            // A molotov burns on the ground floor only (Arturo, 01/11 #10): up a storey it says so, else how long it burns.
            var molotov = modeAbility == AbilityIds.Molotov;
            var parts = new[]
            {
                molotov && centre.Z != 0 ? "Solo a la calle o a una planta baja" : null,
                molotov && centre.Z == 0 ? $"Arde {Content.MolotovTurns} turnos" : null,
                loud > 0 ? $"Ruido {AbilityTexts.Heard(State, loud)}" : null,
                walls == 0 ? null : walls == 1 ? "Rompe una pared" : $"Rompe {walls} paredes",
                Abilities.BreaksFloor(State, modeAbility, centre) ? "Hunde el suelo" : null,
            };
            if (parts.Any(p => p != null)) Note(centre, parts);
        }

        string highlighted;
        string aiming;

        /// <summary>
        /// The figures while aiming (game.ts aim and updateAimPose; Arte's UnitViews): the enemy picked to shoot or strike lit
        /// red (Arturo, 23/10 #3), and the soldier turned to it with its gun up; let go, the gun down.
        /// </summary>
        void Pose()
        {
            var views = Bastion.Client.Units.UnitViews.Current;
            if (views == null) return;
            var u = Unit();
            var option = CurrentTarget();
            var enemy = u != null && option != null && Content.Abilities[modeAbility].Target != AbilityTargets.Ally && option.Unit.Team != Teams.Xcom ? option.Unit : null;
            var target = enemy?.Id;
            if (target != highlighted)
            {
                highlighted = target;
                views.Highlight(target);
            }
            var aimer = enemy != null ? u.Id : null;
            if (aiming != null && aiming != aimer) views.AimAt(aiming, null);
            // On guard for a blow, the blade from the belt; the pistol out of its holster if it fires that (game.ts updateAimPose).
            var melee = aimer != null && Content.Abilities[modeAbility].Target == AbilityTargets.Melee;
            var sidearm = aimer != null && Content.IsAttack(modeAbility) && Content.FiresSidearm(u, modeAbility);
            if (aimer != null) views.AimAt(aimer, FeetOf(enemy.Id) is Vector3 at ? at + Vector3.up * 1.25f : (Vector3?)null, melee, sidearm);
            aiming = aimer;
            AimCam(enemy != null ? u : null, enemy);
        }

        // ------------------------------------------------------- the turn

        void ToggleReady()
        {
            if (!CanAct()) return;
            var ready = State.Ready[MySlot];
            // Without the command too (Arturo, 7/11: «no deja pasar turno»; the rules take it): «A punto», and the turn is
            // over when the partner's is.
            // Ending the turn with soldiers that can still act asks first (Backspace is easy to hit by mistake).
            var idle = IdleSoldiers();
            if (!ready && idle > 0 && !EndArmed)
            {
                endArmedUntil = Time.time + 4;
                var partner = PartnerHere();
                var next = partner == null || State.Ready[partner.Slot] ? "juega el enemigo" : HasCommand() ? $"el mando pasa a {partner.Name}" : $"{partner.Name} sigue con el mando";
                // (Actions or movement left: Lore, 5/11 #14.)
                endQuestion = $"{(idle == 1 ? "A un soldado le quedan" : $"A {idle} soldados les quedan")} acciones o movimiento. Pulsa otra vez para terminar: {next}.";
                Toast(endQuestion);
                RefreshHud();
                return;
            }
            DisarmEndTurn();
            // The question answered goes with it (Gameplay, 26/10: it stayed through the enemy's turn).
            if (endQuestion != null) Hud?.ClearToast(endQuestion);
            endQuestion = null;
            Send(new EndTurnCommand { Ready = !ready });
        }

        /// <summary>The end-of-turn question last asked, to take it away once answered.</summary>
        string endQuestion;

        /// <summary>My soldiers that still have actions this turn.</summary>
        /// <summary>One's soldiers with anything left: an action or movement (Arturo, 5/11 #14: the turn ends by itself only when none has).</summary>
        int IdleSoldiers() => MyUnits().Count(Paths.HasTurnLeft);

        void DisarmEndTurn()
        {
            if (endArmedUntil < 0) return;
            endArmedUntil = -1;
            RefreshHud();
        }

        void PassCommand()
        {
            if (!CanAct()) return;
            if (!HasCommand())
            {
                Toast($"Tiene el mando {PlayerName(State.Command)}.");
                return;
            }
            var partner = PartnerHere();
            if (partner == null) Toast("El otro jugador no está conectado.");
            else if (State.Ready[partner.Slot]) Toast($"{partner.Name} ya ha terminado el turno.");
            else if (!AbilityTexts.BusySlots(State).Contains(partner.Slot)) Toast($"A {partner.Name} no le quedan acciones ni movimiento.");
            else Send(new PassCommand());
        }

        /// <summary>
        /// `slot`'s soldier in play, else (one's own) the picked one, else its first with actions (game.ts turnSoldier).
        /// `opening`: the turn's start, when everyone's actions are still the last turn's.
        /// </summary>
        Unit TurnSoldier(GameState v, int slot, bool opening = false)
        {
            var inPlay = UnitById(v, v.ActiveUnit);
            if (inPlay != null && inPlay.Alive && inPlay.Owner == slot) return inPlay;
            var mine = slot == MySlot ? Unit() : null;
            if (mine != null && mine.Team == Teams.Xcom && (opening || Paths.HasTurnLeft(mine))) return mine;
            return v.Units.FirstOrDefault(x => x.Alive && x.Team == Teams.Xcom && x.Owner == slot && !x.Evacuated && (opening || Paths.HasTurnLeft(x)));
        }

        /// <summary>The camera to the soldier whose turn it is, and it picked (F, «Ir a…»: playtest 19/10 #15).</summary>
        void GoToSoldier()
        {
            var u = TurnSoldier(Shown, MySlot) ?? Unit();
            if (u == null || !u.Alive) return;
            if (u.Id != picked && Commands(u)) Pick(u.Id, false);
            SetLevel(u.Pos.Z);
            if (Rig != null)
            {
                Rig.TakeControl();
                Rig.Focus(FeetOf(u.Id) ?? Board.TileToWorld(Shown, u.Pos));
            }
        }

        /// <summary>«Ver» (playtest 16/10 #8): with the partner holding the command, the camera to their soldier in play, else the last one they moved.</summary>
        void WatchPartner()
        {
            var inPlay = UnitById(Shown, State.ActiveUnit);
            var id = inPlay != null && inPlay.Owner != MySlot ? inPlay.Id : PartnerLast;
            var u = UnitById(Shown, id);
            if (u == null || !u.Alive)
            {
                Toast($"Todavía no hay nada que ver: {PlayerName(State.Command)} no ha movido a nadie.");
                return;
            }
            SetLevel(u.Pos.Z);
            if (Rig != null)
            {
                Rig.TakeControl();
                Rig.Focus(FeetOf(u.Id) ?? Board.TileToWorld(Shown, u.Pos));
            }
        }

        // ------------------------------------------------------- evacuation

        /// <summary>What leaving now costs, for Lore's words: a Limpieza, the objective, and who would stay outside.</summary>
        Lore.EvacSituation EvacSituation(GameState s)
        {
            return new Lore.EvacSituation(
                s.Mission.Kind == MissionKinds.Elimination,
                Rules.EvacWins(s),
                // Outside, and the bitten with no Cuarentena to go to (playtest 15/10 #13): they stay too.
                s.Units.Where(x => x.Alive && x.Team == Teams.Xcom && (!Abilities.InEvacZone(s, x.Pos) || (x.Infected == true && s.Mission.Quarantine == false))).Select(x => x.Name).ToList());
        }

        /// <summary>Our soldiers in the bus, on the state orders go by.</summary>
        List<Unit> MineAboard() => State.Units.Where(x => x.Alive && Commands(x) && Abilities.InEvacZone(State, x.Pos)).ToList();

        /// <summary>The partner whose yes leaving needs (Sim.callEvac): here, and with soldiers on the field.</summary>
        PlayerInfo EvacPartner()
        {
            var active = ActiveSlots();
            var p = Players.FirstOrDefault(q => q.Slot != MySlot && active.Contains(q.Slot));
            return p != null && State.Units.Any(x => x.Alive && x.Team == Teams.Xcom && x.Owner == p.Slot) ? p : null;
        }

        /// <summary>The bar's evacuation, pressed: arm then ask (ours), or answer (the partner's).</summary>
        void UseEvac(string id)
        {
            if (id != "evac")
            {
                if (State.EvacCall != null && State.EvacCall.By != MySlot) Send(new EvacAnswerCommand { Accept = id == "evacAccept" });
                return;
            }
            // The same soldier the bar offered (Bar: EvacActions).
            var s = State;
            var sel = Unit();
            var u = MineAboard().OrderBy(x => x.Id != sel?.Id).FirstOrDefault(x => !(x.Infected == true && s.Mission.Quarantine == false));
            if (u == null || s.EvacCall != null) return;
            if (!(evacArmed == u.Id && Time.time < evacArmedUntil))
            {
                // The first press only says what leaving costs: it can't be undone.
                evacArmed = u.Id;
                evacArmedUntil = Time.time + 5;
                var p = Lore.EvacPrompt(EvacSituation(State), EvacPartner()?.Name);
                Toast($"{p.Text} Pulsa otra vez «{p.Go}».");
                RefreshHud();
                return;
            }
            evacArmed = null;
            Send(new EvacCommand { Unit = u.Id });
        }

        // ------------------------------------------------------- the HUD's buttons (IHudHandlers)

        public void OnAbility(string id) => UseAbility(id);
        public void OnEndTurn() => ToggleReady();

        /// <summary>A card clicked (UI-COMBATE UI-02, UI-08): the soldier picked where the camera is; twice, the camera goes to it.</summary>
        public void OnSelectUnit(string id)
        {
            var u = UnitById(State, id);
            if (u != null && u.Alive && Commands(u)) Pick(id, false);
        }

        /// <summary>A card clicked twice (UI-08): the camera to that soldier, on its floor.</summary>
        public void OnLookUnit(string id)
        {
            var u = UnitById(Shown, id);
            if (u == null || !u.Alive || u.Evacuated) return;
            SetLevel(u.Pos.Z);
            // From the map above, the camera comes down there.
            if (Overhead) OverheadClick(u.Pos);
            else if (Rig != null)
            {
                Rig.TakeControl();
                Rig.Focus(FeetOf(u.Id) ?? Board.TileToWorld(Shown, u.Pos));
            }
        }

        /// <summary>The soldier whose card in the list is under the pointer (#11): its ring lit, an arrow over it or at the edge.</summary>
        string listPointed;

        public void OnHoverSquad(string id)
        {
            if (listPointed == id) return;
            listPointed = id;
            RefreshFlags();
            RefreshMarks();
        }

        public void OnConfirmTarget() => ConfirmTarget();
        public void OnCancel() => CancelMode();
        public void OnCycleTarget(int dir) => CycleTarget(dir);
        public void OnPassCommand() => PassCommand();
        public void OnWatchPartner() => WatchPartner();
        public void OnGoToSoldier() => GoToSoldier();
        public void OnFollowTurn(bool on) { }

        public void OnCallAnswer(bool accept)
        {
            // The question about the one in play (LeaveCall): its turn ended, or back to it.
            if (LeaveCall() != null && !(State.EvacCall != null && State.EvacCall.By != MySlot) && !(State.AbandonCall != null && State.AbandonCall.By != MySlot))
            {
                var busy = leaveAsked;
                leaveAsked = null;
                if (accept) Send(new DoneCommand { Unit = busy });
                else Pick(busy);
                RefreshHud();
                return;
            }
            if (State.EvacCall != null && State.EvacCall.By != MySlot) Send(new EvacAnswerCommand { Accept = accept });
            else if (State.AbandonCall != null && State.AbandonCall.By != MySlot) Send(new AbandonAnswerMessage { Accept = accept });
        }

        /// <summary>The arrival card closed, the stamp let go: the game's (the turn clock), through these events.</summary>
        public event System.Action IntroClosed;
        public event System.Action BannerReleased;

        public void OnIntroClosed() => IntroClosed?.Invoke();
        public void OnBannerReleased() => BannerReleased?.Invoke();

        /// <summary>The skirmish's kinds and maps «Misión al azar» picks from (hud.ts KINDS, MAPS).</summary>
        static readonly string[] SkirmishKinds = { MissionKinds.Elimination, MissionKinds.Recovery, MissionKinds.Sabotage, MissionKinds.Hack, MissionKinds.Rescue, MissionKinds.Scavenge };

        /// <summary>«Otra igual» (the same kind, a new map), or «Misión al azar»: any kind on any map.</summary>
        public void OnNewMission(bool random)
        {
            if (!random)
            {
                Send(new NewMissionMessage());
                return;
            }
            var maps = SkirmishMaps.All.ToList();
            Send(new NewMissionMessage { Mission = SkirmishKinds[UnityEngine.Random.Range(0, SkirmishKinds.Length)], Map = maps[UnityEngine.Random.Range(0, maps.Count)] });
        }

        /// <summary>A campaign mission over: both back to the base (the room ignores backToMenu then).</summary>
        public void OnReturnToBase() => Send(new ReturnToBaseMessage());
        public void OnBackToMenu() => Send(new BackToMenuMessage());
        public void OnSheet(string unit) => OpenSheet(unit);
        public void OnFloor(int dir) => SetLevel(Level + dir);
        public void OnMenu() => ToggleMenu();
    }
}
