using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.UI.Hud;
using Bastion.UI.Start;
using UnityEngine;
using Grid = Bastion.Engine.Grid;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// What the HUD shows (game.ts refreshHud and refreshStatus, and what hud.ts read off the units; UI's: docs/UNITY-UI.md
    /// §3): the views built from the state. What the player reads (turn, objective, players, squad, the picked soldier)
    /// comes from Shown, what has been played out: a death or a new turn must not show before it is seen. Orders and their
    /// checks (the bar, the aim, ending the turn, the command) go by State.
    /// </summary>
    public sealed partial class CombatController
    {
        const string Enemy = "#d64545";

        /// <summary>Everything on the HUD again (game.ts refreshHud): each piece draws only what changed (Ui.Key).</summary>
        void RefreshHud()
        {
            if (Hud == null || State == null) return;
            RefreshStatus();
            var s = State;
            var busy = AbilityTexts.BusySlots(s);
            var partner = PartnerHere();
            var sharing = partner != null && s.ActiveTeam == Teams.Xcom && s.Outcome == null;
            var mine = HasCommand();
            var bar = new ActionBarView { Abilities = BarKeys() };
            var u = Unit();
            if (u != null && Commands(u) && CanAct() && !mine) bar.Lock = $"Turno de {PlayerName(s.Command)}: puedes apuntar y planear";
            Hud.SetAbilities(bar);
            Hud.SetCall(CallView());
            Hud.SetTarget(TargetPanel());

            var ready = s.Ready[MySlot];
            var waiting = partner != null && busy.Contains(partner.Slot) && !s.Ready[partner.Slot] ? partner.Name : null;
            var idle = IdleSoldiers();
            var endMode = !CanAct() ? (s.ActiveTeam == Teams.Alien || Shown.ActiveTeam == Teams.Alien ? "enemy" : "disabled") : ready ? "ready" : !mine ? "waiting" : EndArmed && idle > 0 ? "confirm" : "act";
            var detail = endMode == "confirm" ? $"{idle} {(idle == 1 ? "soldado" : "soldados")} con acciones" : !mine && !ready ? PlayerName(s.Command) : waiting;
            Hud.SetEndTurn(new EndTurnView { Mode = endMode, Detail = detail });
            Hud.SetCommand(sharing && CanAct()
                ? new CommandView { Shared = true, Mine = mine, Holder = PlayerName(s.Command), Color = PlayerColor(s.Command ?? MySlot), CanPass = mine && !s.Ready[partner.Slot] && busy.Contains(partner.Slot) }
                : null);
            // «Ir a …» (#15): while this player gives the orders and someone still has actions.
            var next = CanAct() && mine && !ready && s.ActiveTeam == Teams.Xcom ? TurnSoldier(s, MySlot) : null;
            Hud.SetGoTo(next?.Name);
            Hud.SetFloor(MaxLevel > 0 ? new FloorView { Level = Level, Max = MaxLevel } : new FloorView());
        }

        /// <summary>What the player reads about the fight, from what has been seen (game.ts refreshStatus).</summary>
        void RefreshStatus()
        {
            var v = Shown;
            var sharing = PartnerHere() != null && v.ActiveTeam == Teams.Xcom && v.Outcome == null;
            var holder = v.Command == null || !Players.Any(p => p.Slot == v.Command && p.Connected) ? MySlot : v.Command.Value;
            // Whose turn it is, unmistakably (playtest): mine, the partner's while they hold the command, or the enemy's.
            var turn = new TurnView { Turn = v.Turn, Concealed = v.Concealed };
            if (v.Outcome != null) turn.Owner = "over";
            else if (v.ActiveTeam == Teams.Alien)
            {
                turn.Owner = "enemy";
                turn.Color = Enemy;
            }
            else if (sharing && holder != MySlot)
            {
                turn.Owner = "partner";
                turn.OwnerName = PlayerName(holder);
                turn.Color = PlayerColor(holder);
            }
            else
            {
                turn.Owner = "mine";
                turn.Color = PlayerColor(MySlot);
            }
            if (v.Mission.Night == true)
            {
                turn.NightLabel = Lore.NightText.Label;
                turn.NightRules = string.Join("\n", Lore.NightText.Rules);
            }
            // One soldier at a time with a partner (playtest 15/10 #9): the one in play, by the label and on its card.
            var playing = sharing ? UnitById(v, v.ActiveUnit) : null;
            var inPlay = playing != null && playing.Alive && playing.Ap > 0 ? playing : null;
            turn.InPlay = inPlay?.Name;
            Hud.SetTurn(turn);
            Hud.SetMission(MissionView());
            Hud.SetPlayers(PlayersView(v, sharing ? holder : (int?)null));
            Hud.SetSquad(SquadView(v, inPlay?.Id));
            var shown = UnitById(v, picked);
            Hud.SetSelected(shown != null && shown.Alive ? UnitPanel(shown) : null);
            RefreshFlags();
            RefreshMarks();
        }

        // ------------------------------------------------------- the mission's marks

        /// <summary>The mission's marks on the map (made with the controller: CombatSession, the lab).</summary>
        public MissionMarks Marks;

        /// <summary>Where the partner just made something happen (the event player's notice), pointed at for a while.</summary>
        (Vector3 at, string who)? moment;
        float momentUntil;
        int markCycle;

        /// <summary>A partner's moment, pointed at from the screen's edge for 8 s (game.ts moment); null clears it.</summary>
        public void SetMoment(Vector3? at, string who)
        {
            moment = at != null ? (at.Value, who) : ((Vector3, string)?)null;
            momentUntil = Time.time + 8;
            RefreshMarks();
        }

        /// <summary>
        /// The mission's marks from what has been seen (game.ts refreshView and marks): the evacuation zone, the beams over
        /// the equipment to fix and the one to reach, the item lying loose, the flares; and the arrows at the screen's edge
        /// to the objective (or the evacuation) and to the partner's moment.
        /// </summary>
        void RefreshMarks()
        {
            var v = Shown;
            var m = v.Mission;
            var beacons = new List<(Pos, int)>();
            if (m.Terminal != null && !m.ObjectiveDone) beacons.Add((m.Terminal, 0x54d1ff));
            var objective = UnitById(v, m.ObjectiveUnit);
            if (objective != null && objective.Alive && !m.ObjectiveDone) beacons.Add((objective.Pos, objective.Team == Teams.Alien ? 0xff4a6a : 0x4dff9a));
            var item = m.Item != null && m.Item.Carrier == null && m.Item.Evacuated != true ? m.Item.Pos : null;
            Marks?.Show(v, m.EvacZone, beacons, item, m.Reinforcements.Where(r => r.Pos != null && !r.Landed).Select(r => r.Pos).ToList());
            if (Hud == null) return;
            var view = new EdgeMarksView();
            var from = Unit()?.Pos;
            foreach (var (kind, name, pos) in MissionGoals())
                view.Marks.Add(new EdgeMarkView { Kind = kind, Name = name, Label = from != null ? $"{name} · {Mathf.RoundToInt((float)Grid.Distance(from, pos))} casillas" : name, World = Board.TileToWorld(v, pos, Board.Tile) });
            if (moment != null && Time.time < momentUntil) view.Marks.Add(new EdgeMarkView { Kind = "alert", Name = moment.Value.who, Label = moment.Value.who, World = moment.Value.at + Vector3.up * Board.Tile });
            Hud.SetEdgeMarks(view);
        }

        /// <summary>Where the squad has to go (game.ts missionMarks): the objective while it is still to do, else the evacuation's middle.</summary>
        List<(string kind, string name, Pos pos)> MissionGoals()
        {
            var s = Shown;
            var m = s.Mission;
            Pos goal = null;
            if (!m.ObjectiveDone)
            {
                if (m.Kind == MissionKinds.Recovery && m.Item != null && m.Item.Carrier == null && m.Item.Evacuated != true) goal = m.Item.Pos;
                if (m.Kind == MissionKinds.Hack) goal = m.Terminal;
                if (m.Kind == MissionKinds.Sabotage || m.Kind == MissionKinds.Rescue)
                {
                    var target = UnitById(s, m.ObjectiveUnit);
                    if (target != null && target.Alive && (m.Kind == MissionKinds.Sabotage || target.Captive)) goal = target.Pos;
                }
            }
            if (goal != null) return new List<(string, string, Pos)> { ("objective", "Objetivo", goal) };
            var zone = m.EvacZone;
            if (zone.Count == 0) return new List<(string, string, Pos)>();
            var mx = zone.Average(t => t.X);
            var my = zone.Average(t => t.Y);
            var centre = zone.OrderBy(t => (t.X - mx) * (t.X - mx) + (t.Y - my) * (t.Y - my)).First();
            return new List<(string, string, Pos)> { ("evac", "Evacuación", new Pos { X = centre.X, Y = centre.Y }) };
        }

        /// <summary>O: the camera to the mission's marks, one after another.</summary>
        void NextMark()
        {
            var marks = MissionGoals();
            if (marks.Count == 0)
            {
                Toast("Esta misión no tiene un punto al que ir: eliminad a los hostiles.");
                return;
            }
            LookAt(Board.TileToWorld(Shown, marks[markCycle++ % marks.Count].pos));
        }

        /// <summary>The player asked to look somewhere (O, an arrow at the edge): it ends any camera pass.</summary>
        void LookAt(Vector3 at)
        {
            if (Rig == null) return;
            Rig.TakeControl();
            Rig.Focus(at);
        }

        // ------------------------------------------------------- sight and flags

        /// <summary>The enemies in the squad's sight, from what has been seen (game.ts refreshView: visibleAliens).</summary>
        readonly HashSet<string> seen = new HashSet<string>();

        void See()
        {
            var v = Shown;
            seen.Clear();
            // In a siege the base has no fog by day (playtest 18/10 #18): every enemy is in sight.
            var all = v.Mission.Siege != null && v.Mission.Night != true;
            foreach (var u in all ? v.Units.Where(x => x.Alive && x.Team == Teams.Alien) : Los.VisibleEnemies(v, Teams.Xcom)) seen.Add(u.Id);
        }

        /// <summary>
        /// The flags over the units in sight (unitViews.ts renderFlag): health, armour, cover, what is on them; the name on
        /// the picked one's and the hovered one's; over the enemies, what the pointer's tile means (game.ts enemyTags).
        /// </summary>
        void RefreshFlags()
        {
            if (Hud == null) return;
            var v = Shown;
            var view = new FlagsView();
            // Only those that shoot: cover means nothing against the dead, who come to bite (docs/ZOMBIS-PRIMERO.md).
            var watchers = v.Units.Where(a => a.Alive && a.Team == Teams.Alien && Visible(a) && AbilityTexts.ShootsFromRange(a)).ToList();
            var selected = UnitById(v, picked);
            var hovered = hover != null ? Grid.UnitAt(v, hover) : null;
            var tags = EnemyTags();
            var watching = Watching();
            var carrier = v.Mission.Item?.Carrier;
            foreach (var u in v.Units)
            {
                if (!u.Alive || !Visible(u)) continue;
                var f = new FlagView { Id = u.Id, Enemy = u.Team != Teams.Xcom, Hp = u.Hp, MaxHp = u.MaxHp, Armor = u.Armor, Cover = -1 };
                if (u.Id == picked || u.Id == hovered?.Id) f.Name = u.Team == Teams.Xcom && u.Template != TemplateIds.Vip && u.Nickname != null ? $"«{u.Nickname}»" : u.Name;
                if (u.Team == Teams.Xcom)
                {
                    // With no shooter in sight nobody flanks anyone: the shield only shows cover, never the red «flanked».
                    if (watchers.Count == 0) f.Cover = Cover.HasAnyCover(v, u.Pos) ? Cover.CoverSides(v, u.Pos).Max() : -1;
                    else f.Cover = watchers.Min(a => Cover.CoverAgainst(v, u.Pos, a.Pos));
                }
                // How far the soldier sees it: shorter at night, unless it stands in the light (los.ts sightTo).
                else if (selected != null && selected.Alive && Los.SightOrigin(v, selected.Pos, u.Pos, Los.SightTo(v, selected, u.Pos)) != null) f.Cover = Cover.CoverAgainst(v, u.Pos, selected.Pos);
                void Tag(bool on, string text, string kind)
                {
                    if (on) f.Tags.Add(new TagView { Text = text, Kind = kind });
                }
                Tag(u.ControlledBy != null, "CONTROLADO", "psi");
                Tag(u.Overwatch, u.KillZone ? "ZONA LETAL" : u.WatchMelee == true ? "HOJA EN ALTO" : "VIGILANCIA", "ow");
                Tag(u.Hunkered, "CUBRIÉNDOSE", "hk");
                Tag(u.Aided > 0, "PROTEGIDO", "aid");
                Tag(u.Stunned, "ATURDIDO", "stun");
                Tag(u.Disoriented > 0, "DESORIENTADO", "psi");
                Tag(u.Panicked, "PÁNICO", "psi");
                Tag(u.Infected == true, Lore.BiteTag, "bite");
                Tag(u.SuppressedBy != null, "SUPRIMIDO", "stun");
                Tag(u.Marked > 0, "MARCADO", "aid");
                Tag(u.Ruptured > 0, "VULNERABLE", "stun");
                Tag(carrier == u.Id, Lore.ItemTag(RecoveryItem()), "data");
                Tag(u.Captive, "CAUTIVO", "data");
                Tag(u.Objective, "OBJETIVO", "stun");
                if (tags.TryGetValue(u.Id, out var t))
                {
                    f.HoverKind = t.kind;
                    f.HoverText = t.text;
                }
                f.Target = mode == Mode.Target && targetId == u.Id && u.Team != Teams.Xcom;
                f.Watching = watching.Contains(u.Id);
                view.Flags.Add(f);
            }
            Hud.SetFlags(view);
        }

        /// <summary>
        /// The enemies in sight that see one of the squad themselves, for an eye on their flag (game.ts watching, playtest
        /// 21/10 #11). While the squad is hidden nobody has seen it (the frames say how close).
        /// </summary>
        HashSet<string> Watching()
        {
            var v = Shown;
            if (watching.state == v && watching.seq == v.Seq) return watching.set;
            var set = new HashSet<string>();
            watching = (v, v.Seq, set);
            if (v.Concealed || v.Outcome != null) return set;
            var squad = Grid.Combatants(v, Teams.Xcom);
            foreach (var a in v.Units)
                if (a.Alive && a.Team == Teams.Alien && Visible(a) && squad.Any(u => Los.CanSee(v, a, u))) set.Add(a.Id);
            return set;
        }

        (string key, List<Pos> seen, List<Pos> noticed)? vision;

        /// <summary>Who sees one of the squad, worked out once per state seen.</summary>
        (GameState state, int seq, HashSet<string> set) watching;

        /// <summary>
        /// An enemy under the pointer: what it sees (Arturo, 17/10: the dead see in a cone; game.ts visionOf). Not the one
        /// aimed at: its red over the ground drowned the target's own mark (Arturo, 23/10 #2). Worked out once per enemy and state.
        /// </summary>
        void ShowVision()
        {
            if (AimMarks == null) return;
            var v = Shown;
            var a = hover != null ? Grid.UnitAt(v, hover) : null;
            if (a == null || a.Team != Teams.Alien || !Visible(a) || mode == Mode.Target || v.Outcome != null)
            {
                AimMarks.ShowVision(null, null, null);
                return;
            }
            var key = $"{a.Id}:{v.Seq}:{v.Concealed}:{a.Pos.X},{a.Pos.Y},{a.Pos.Z}:{a.Facing}";
            if (vision?.key != key)
            {
                var range = Los.SightOf(a);
                var seen = new List<Pos>();
                var noticed = new List<Pos>();
                for (var y = Math.Max(0, a.Pos.Y - range); y <= Math.Min(v.Height - 1, a.Pos.Y + range); y++)
                for (var x = Math.Max(0, a.Pos.X - range); x <= Math.Min(v.Width - 1, a.Pos.X + range); x++)
                {
                    if (x == a.Pos.X && y == a.Pos.Y) continue;
                    var p = new Pos { X = x, Y = y, Z = a.Pos.Z };
                    if (!Grid.CanStandOn(Grid.KindAt(v, p)) || !Los.InViewCone(a, p) || Los.SightOrigin(v, a.Pos, p, Los.SightTo(v, a, p)) == null) continue;
                    seen.Add(p);
                    if (v.Concealed && Grid.Distance(a.Pos, p) <= Los.DetectionTo(v, a, p)) noticed.Add(p);
                }
                vision = (key, seen, noticed);
            }
            AimMarks.ShowVision(v, vision.Value.seen, vision.Value.noticed);
        }

        /// <summary>
        /// Over the enemies' flags, what the pointer's tile means (game.ts enemyTags): on an enemy, the odds of a shot at it;
        /// on a tile the picked one can move to, whom it would see or flank from there and the odds if it could still shoot;
        /// with a throw armed, what it takes off each one caught (blastTags).
        /// </summary>
        Dictionary<string, (string kind, string text)> EnemyTags()
        {
            var tags = new Dictionary<string, (string, string)>();
            var u = Unit();
            var tile = hover;
            if (u == null || tile == null || !CanAct() || !Commands(u)) return tags;
            if (mode == Mode.Tile && HandArmed() == null) return BlastTags(u, modeAbility, tile);
            if (mode != Mode.Move || !Abilities.AbilitiesOf(u).Contains(AbilityIds.Shoot)) return tags;
            var occupant = Grid.UnitAt(State, tile);
            if (occupant != null)
            {
                if (occupant.Team != Teams.Alien || !Visible(occupant)) return tags;
                var shot = Aim.PreviewShot(State, u, occupant, new ShotOptions { Ability = AbilityIds.Shoot });
                tags[occupant.Id] = shot != null ? (shot.Flanked ? "flank" : "aim", $"{shot.Hit} % · crít. {Math.Min(shot.Crit, shot.Hit)} %") : ("none", "Sin línea de tiro");
                return tags;
            }
            var dest = tiles?.FirstOrDefault(t => Same(t.Pos, tile));
            if (dest == null) return tags;
            var canShoot = !dest.Dash && u.Ap - 1 >= Abilities.ApNeeded(u, AbilityIds.Shoot);
            foreach (var a in State.Units)
            {
                if (!a.Alive || a.Team != Teams.Alien || !Visible(a)) continue;
                var shot = Aim.PreviewShot(State, u, a, new ShotOptions { Ability = AbilityIds.Shoot, From = tile });
                if (shot == null) continue;
                var chance = canShoot ? $"{shot.Hit} %" : "";
                tags[a.Id] = shot.Flanked ? ("flank", chance != "" ? $"Flanco · {chance}" : "Flanco") : ("sight", chance != "" ? chance : "A la vista");
            }
            return tags;
        }

        /// <summary>
        /// A grenade aimed: what it would take off everyone caught, over their flags (playtest: «no sé dónde está el daño
        /// que hago»); enemies with whether it kills them, our own in warning.
        /// </summary>
        Dictionary<string, (string kind, string text)> BlastTags(Unit u, string ability, Pos tile)
        {
            var tags = new Dictionary<string, (string, string)>();
            var centre = AbilityTexts.ThrowTile(u, ability, tile);
            if (!Abilities.CanTargetTile(State, u, ability, centre)) return tags;
            // Controlled detonation leaves the grenadier's squad out of the blast.
            var grenade = ability == AbilityIds.Grenade || ability == AbilityIds.Launch;
            foreach (var p in Abilities.BlastTiles(State, ability, centre, u))
            {
                var t = Grid.UnitAt(State, p);
                if (t == null || (grenade && Content.HasPerk(u, PerkIds.SafeBlast) && t.Team == u.Team) || (t.Team == Teams.Alien && !Visible(t))) continue;
                var d = Rules.AbilityDamage(State, u, ability, t);
                if (d == null) return tags;
                var text = $"Daño {AbilityTexts.Span(d.Hit)}";
                if (t.Team == Teams.Xcom) tags[t.Id] = ("flank", $"¡Aliado! {text}");
                else tags[t.Id] = ("aim", string.Join(" · ", new[] { text, AbilityTexts.Lethality(d, t) }.Where(x => x != null)));
            }
            return tags;
        }

        // ------------------------------------------------------- players

        /// <summary>The players on the strip (hud.ts setPlayers): alone, the turn's state is the end-turn button's business.</summary>
        PlayersView PlayersView(GameState v, int? commander)
        {
            var view = new PlayersView();
            var busy = AbilityTexts.BusySlots(v);
            var alone = !Players.Any(p => p.Slot != MySlot && p.Connected);
            foreach (var p in Players)
            {
                var holds = commander == p.Slot;
                var status = !p.Connected ? "Desconectado" : p.Away == true ? "Ausente" : alone || holds ? "" : v.Ready[p.Slot] ? "Listo" : busy.Contains(p.Slot) ? "Con acciones" : "Sin acciones";
                view.Players.Add(new PlayerView { Slot = p.Slot, Name = p.Name, Color = p.Color, Me = p.Slot == MySlot, Connected = p.Connected, Ready = v.Ready[p.Slot], Command = holds, Status = status });
            }
            return view;
        }

        // ------------------------------------------------------- squad

        /// <summary>
        /// The squad's cards (hud.ts setSquad): one's own first, then the partner's commanded while they're gone, then the
        /// rest; a siege's turrets too, for their health and rounds.
        /// </summary>
        SquadView SquadView(GameState v, string inPlay)
        {
            var view = new SquadView();
            var units = v.Units.Where(u => u.Team == Teams.Xcom || u.ControlledBy != null).ToList();
            int Rank(Unit u) => !Commands(u) ? 2 : u.Owner == MySlot || u.Owner == null ? 0 : 1;
            var carrier = v.Mission.Item?.Carrier;
            foreach (var u in units.OrderBy(Rank))
            {
                var mine = Commands(u);
                var theirs = u.Owner != null && u.Owner != MySlot;
                var card = new SquadCardView
                {
                    Id = u.Id,
                    Name = (u.Template == TemplateIds.Rookie ? "" : Content.Templates[u.Template].Short + " ") + (u.Nickname != null ? $"«{u.Nickname}»" : u.Name),
                    Accent = u.Owner == null ? "#e8edf2" : PlayerColor(u.Owner.Value),
                    Mine = mine,
                    Selected = u.Id == picked,
                    Dead = !u.Alive && !u.Evacuated,
                    Evacuated = u.Evacuated,
                    InPlay = u.Id == inPlay,
                    Hp = u.Hp,
                    MaxHp = u.MaxHp,
                    Skills = SkillValues(u),
                    Title = u.Automaton != null ? "Va sola: nadie le da órdenes." : !theirs ? "Seleccionar" : mine ? "Soldado del otro jugador: lo mandas tú mientras no está" : "Soldado del otro jugador",
                };
                if (u.Automaton != null)
                {
                    // A turret has no actions of its own: its rounds instead.
                    card.Rounds = true;
                    card.Ap = u.Ammo;
                    card.ApMax = u.Ammo;
                }
                else
                {
                    card.Ap = u.Ap;
                    card.ApMax = Math.Max(Content.ApPerTurnOf(u), u.Ap);
                }
                void Tag(bool on, string text, string kind, string tip = null)
                {
                    if (on) card.Tags.Add(new TagView { Text = text, Kind = kind, Tip = tip });
                }
                Tag(u.Id == inPlay, "EN JUEGO", "in-play", "En juego: termina sus acciones antes que otro");
                Tag(u.ControlledBy != null, "CONTROLADO", "psi");
                Tag(u.Captive, "CAUTIVO", "data");
                Tag(u.Disoriented > 0, "DESORIENTADO", "psi");
                Tag(u.Panicked, "PÁNICO", "psi");
                // Bitten (docs/FASE3.md): if it falls it rises, and back at the base it turns unless treated.
                Tag(u.Infected == true && u.Alive, Lore.BiteTag, "bite", BiteNote());
                Tag(u.Evacuated, "EVACUADO", "evac");
                Tag(carrier == u.Id, Lore.ItemTag(RecoveryItem()), "data");
                Tag(u.Overwatch, u.KillZone ? "ZONA LETAL" : u.WatchMelee == true ? "HOJA EN ALTO" : "VIGILANCIA", "ow");
                Tag(u.Hunkered, "CUBRIÉNDOSE", "hk");
                Tag(u.Stunned, "ATURDIDO", "stun");
                view.Cards.Add(card);
            }
            return view;
        }

        /// <summary>What a bite does in this mission (playtest 15/10 #13), for the bite tags' tooltip.</summary>
        string BiteNote() => Lore.BiteNote(Shown.Mission.Quarantine != false);

        /// <summary>What a recovery carries out: the chapter's own name for it, or the default (game.ts recoveryItem).</summary>
        string RecoveryItem() => Shown.CampaignMission?.Item ?? Lore.DefaultItem;

        /// <summary>The six lines' values (SkillIds.All order) for a soldier's hexagon: what it has learnt so far, this mission included.</summary>
        static float[] SkillValues(Unit u)
        {
            if (u.Lines == null) return Array.Empty<float>();
            var skills = Bars.LiveSkills(u.Lines, u.Deeds);
            return SkillIds.All.Select(id => skills.TryGetValue(id, out var n) ? (float)n : 0).ToArray();
        }

        // ------------------------------------------------------- the picked one

        /// <summary>
        /// The soldier's panel (hud.ts setSelected): symbols, pips and numbers, readable at a glance (full words in the
        /// tooltips); a partner's soldier says whose.
        /// </summary>
        UnitPanelView UnitPanel(Unit u)
        {
            var t = Content.Templates[u.Template];
            // A soldier's own arms (docs/FASE8.md §A): what is in the hand (the pistol if nothing), the pistol beside it, the knife or machete.
            var arms = u.Arms;
            var pistol = Content.Weapons[WeaponIds.Pistol].Name;
            var weapon = u.WeaponName ?? (arms != null ? (!string.IsNullOrEmpty(arms.Primary) ? Content.FoundWeapons[arms.Primary].Name : pistol) : Content.Weapons[t.Weapon].Name);
            var extras = arms != null
                ? new[] { !string.IsNullOrEmpty(arms.Primary) ? pistol : "", !string.IsNullOrEmpty(arms.Melee) ? Content.FoundWeapons[arms.Melee].Name : Content.Weapons[WeaponIds.Knife].Name }
                : new[] { t.Secondary != null ? Content.Weapons[t.Secondary].Name : "", t.MeleeWeapon != null ? Content.Weapons[t.MeleeWeapon].Name : "" };
            var clip = Content.ClipOf(u, AbilityIds.Shoot);
            var sidearm = !string.IsNullOrEmpty(arms?.Primary);
            // A soldier's veterancy, in the feminine for a woman.
            var soldier = u.Team == Teams.Xcom && u.Template == TemplateIds.Rookie;
            var step = soldier && u.Rank < Bars.Veterancy.Count ? Bars.Veterancy[u.Rank] : null;
            var rank = step != null ? (u.Appearance?.Sex == "f" ? step.Female : step.Name) : "";
            var view = new UnitPanelView
            {
                Id = u.Id,
                Soldier = soldier,
                Rank = soldier ? u.Rank : 0,
                RankName = soldier ? rank : t.Short,
                Skills = SkillValues(u),
                Name = u.Nickname != null ? $"{u.Name} «{u.Nickname}»" : u.Name,
                Weapon = weapon,
                WeaponTip = string.Join(" · ", new[] { weapon }.Concat(extras).Where(x => !string.IsNullOrEmpty(x))),
            };
            // A partner's soldier says whose, in the partner's colour (Arturo, 17/10).
            if (u.Team == Teams.Xcom && u.Owner != null && u.Owner != MySlot)
            {
                view.Whose = $"DE {PlayerName(u.Owner).ToUpperInvariant()}";
                view.WhoseColor = PlayerColor(u.Owner.Value);
            }
            if (u.Infected == true && u.Alive)
            {
                view.Bite = Lore.BiteTag;
                view.BiteTip = BiteNote();
            }
            var aim = t.Aim + u.Mods.Aim;
            view.Stats.Add(new StatView { Kind = "hp", Tip = $"Salud {u.Hp}/{u.MaxHp}", Filled = u.Hp, Total = u.MaxHp });
            view.Stats.Add(new StatView { Kind = "ap", Tip = $"Acciones {u.Ap}/{Content.ApPerTurnOf(u)}", Filled = u.Ap, Total = Math.Max(Content.ApPerTurnOf(u), u.Ap) });
            // The spare magazines after the clip (docs/FASE7.md): «+3», red at none; the enemies don't count theirs.
            if (clip > 0)
            {
                var spares = u.Mags == null ? "" : u.Mags > 0 ? $" · {Lore.MagsCount(u.Mags.Value)} de repuesto" : " · sin cargadores de repuesto";
                view.Stats.Add(new StatView { Kind = "ammo", Tip = $"{weapon}: {u.Ammo}/{clip}{spares}", Filled = u.Ammo, Total = clip, Value = u.Mags != null ? $"+{u.Mags}" : null, ValueBad = u.Mags == 0 });
            }
            // The pistol carried beside a main gun counts its own rounds; the spares serve both.
            if (sidearm) view.Stats.Add(new StatView { Kind = "sidearm", Tip = $"{pistol}: {u.PistolAmmo ?? 0}/{Content.PistolClip}", Filled = u.PistolAmmo ?? 0, Total = Content.PistolClip });
            view.Stats.Add(new StatView { Kind = "aim", Tip = $"Puntería con armas de fuego {aim + u.Mods.GunAim}", Value = (aim + u.Mods.GunAim).ToString() });
            if (soldier) view.Stats.Add(new StatView { Kind = "melee", Tip = $"Puntería cuerpo a cuerpo {aim + u.Mods.MeleeAim}", Value = (aim + u.Mods.MeleeAim).ToString() });
            if (u.Charges.TryGetValue(ChargeIds.Grenade, out var grenades)) view.Stats.Add(new StatView { Kind = "grenade", Tip = $"Granadas: {grenades}", Value = $"×{grenades}" });
            if (u.Armor > 0) view.Stats.Add(new StatView { Kind = "armor", Tip = $"Blindaje {u.Armor}", Value = u.Armor.ToString() });
            // A vest found and put on out there (playtest 16/10): its health is already in the pips.
            foreach (var item in u.Worn ?? new List<string>())
            {
                var def = Content.Items[item];
                view.Stats.Add(new StatView { Kind = "worn", Tip = $"Lleva puesto: {def.Name} ({def.Description.TrimEnd('.')})", Value = $"+{def.Hp ?? 0}" });
            }
            return view;
        }

        // ------------------------------------------------------- the objective

        /// <summary>The objective's card (game.ts missionView), in Lore's words for each moment of it.</summary>
        MissionView MissionView()
        {
            var s = Shown;
            var m = s.Mission;
            if (m.Siege != null) return SiegeView();
            var objective = "Eliminad a todos los hostiles";
            string tip = null;
            switch (m.Kind)
            {
                case MissionKinds.Recovery:
                {
                    var carrier = UnitById(s, m.Item?.Carrier);
                    var item = RecoveryItem();
                    objective = m.Item?.Evacuated == true ? Lore.ItemObjective(item, "out")
                        : carrier != null && carrier.Alive && Abilities.InEvacZone(s, carrier.Pos) ? Lore.ItemObjective(item, "aboard")
                        : carrier != null ? Lore.ItemObjective(item, "carried", carrier.Name)
                        : Lore.ItemObjective(item, "loose");
                    break;
                }
                case MissionKinds.Sabotage:
                    objective = m.ObjectiveDone ? Lore.ObjectiveDone[MissionKinds.Sabotage] : Lore.AlarmObjective(m.Alarm);
                    break;
                case MissionKinds.Hack:
                    objective = m.ObjectiveDone ? Lore.ObjectiveDone[MissionKinds.Hack] : "Arreglad el equipo (junto a él)";
                    break;
                case MissionKinds.Rescue:
                {
                    var vip = UnitById(s, m.ObjectiveUnit);
                    var who = vip?.Name ?? "el VIP";
                    objective = m.ObjectiveDone ? Lore.VipObjective(who, "out")
                        : vip != null && vip.Alive && !vip.Captive && Abilities.InEvacZone(s, vip.Pos) ? Lore.VipObjective(who, "aboard")
                        : vip != null && vip.Captive ? Lore.VipObjective(who, "captive")
                        : Lore.VipObjective(who, "loose");
                    break;
                }
                case MissionKinds.Scavenge:
                {
                    // What the squad carries counts once it is out (or standing at the end); a fallen soldier's is lost.
                    var n = s.Units.Where(u => u.Team == Teams.Xcom && (u.Alive || u.Evacuated)).Sum(u => u.Backpack?.Count ?? 0);
                    objective = n > 0 ? Lore.Scavenge.Carrying.Replace("{n}", n.ToString()) : Lore.Scavenge.Objective;
                    if (n > 0) tip = Lore.Scavenge.CarryingTip.Replace("{n}", n.ToString());
                    break;
                }
            }
            // The hordes on the way are not said on the card (playtest 19/10 #20); a flare drop, still.
            var alert = m.Reinforcements.Any(r => !r.Landed && r.Horde != true && r.Pos != null) ? "VIENEN MÁS" : null;
            return new MissionView
            {
                Label = MissionName(m.Kind),
                Area = s.MapName,
                Objective = objective,
                ObjectiveTip = tip,
                TurnsLeft = m.TurnsLeft ?? -1,
                Alert = alert,
            };
        }

        /// <summary>A mission's name (setup.ts MISSION_NAMES, as the room lists them, until setup is in C#).</summary>
        static string MissionName(string kind) => RoomText.Missions.FirstOrDefault(x => x.Id == kind).Name ?? kind;

        /// <summary>
        /// A siege's strip (docs/FASE12.md §10, Lore's words): «Asedio», kill them all (Ana, 23/10 #36) and when the next wave
        /// comes; or that none are coming any more.
        /// </summary>
        MissionView SiegeView()
        {
            var s = Shown;
            var waves = s.Mission.Reinforcements.GroupBy(r => r.Turn).Select(g => (Turn: g.Key, Landed: g.All(r => r.Landed))).OrderBy(w => w.Turn).ToList();
            var next = waves.FirstOrDefault(w => !w.Landed);
            var view = new MissionView { Label = Lore.SiegeText.Name, Area = s.MapName, Objective = Lore.SiegeObjective(), ObjectiveTip = Lore.SiegeObjectiveTip() };
            if (next.Turn > 0 || waves.Any(w => !w.Landed))
            {
                view.WaveText = $"OLEADA · {Lore.WaveDueShort(next.Turn, s.Turn).ToUpperInvariant()}";
                view.WaveTip = Lore.WaveDueLine(next.Turn, s.Turn);
                view.WaveUrgent = next.Turn <= s.Turn;
            }
            else
            {
                view.WaveText = Lore.SiegeText.NoMoreWaves.ToUpperInvariant();
                view.WaveTip = Lore.SiegeText.NoMoreWaves;
            }
            return view;
        }

        // ------------------------------------------------------- the aim's panel

        /// <summary>
        /// The aim's panel (game.ts targetPanel): by hand, what it acts on; a self ability armed; aiming, the odds and the
        /// damage of an attack, or what a support ability does. Null with nothing armed.
        /// </summary>
        ShotView TargetPanel()
        {
            var u = Unit();
            if (u != null && HandArmed() != null) return HandPanel(u);
            if (u != null && mode == Mode.Self)
            {
                var self = Content.Abilities[modeAbility];
                return new ShotView { Kind = "self", Title = self.Name, Action = self.Name, Locked = LockLabel(), Cost = AbilityTexts.CostText(u, modeAbility), Limits = AbilityTexts.LimitsText(u, modeAbility), Effect = self.Description, Detail = SelfDetail(u, modeAbility), Noise = AbilityTexts.NoiseText(State, u, modeAbility) };
            }
            var option = CurrentTarget();
            if (u == null || option == null || mode != Mode.Target) return null;
            var ability = modeAbility;
            var def = Content.Abilities[ability];
            var list = TargetOptions(ability);
            var index = list.FindIndex(o => o.Unit.Id == option.Unit.Id);
            var target = option.Unit;
            if (def.Target == AbilityTargets.Ally || !Content.IsAttack(ability))
            {
                var friendly = def.Target == AbilityTargets.Ally;
                var dmg = friendly ? null : Rules.AbilityDamage(State, u, ability, target);
                var noise = Abilities.NoiseOf(State, u, ability);
                var support = new ShotView
                {
                    Kind = "support",
                    Title = def.Name,
                    Action = def.Name,
                    Locked = LockLabel(),
                    TargetName = target.Name,
                    Effect = AbilityTexts.SupportEffects.TryGetValue(ability, out var effect) ? effect : def.Description,
                    Index = index,
                    Total = list.Count,
                    Noise = noise > 0 ? $"Ruido {noise}" : null,
                };
                if (dmg != null)
                {
                    support.HasDamage = true;
                    support.Damage = new DamageView { Hit = AbilityTexts.Span(dmg.Hit), Notes = AbilityTexts.ArmorNotes(dmg, target), Kill = AbilityTexts.Lethality(dmg, target) };
                }
                return support;
            }
            var shot = Aim.PreviewShot(State, u, target, new ShotOptions { Ability = ability, From = option.Tile });
            if (shot == null) return null;
            var d = Rules.AbilityDamage(State, u, ability, target, shot);
            var loud = Abilities.NoiseOf(State, u, ability);
            var view = new ShotView
            {
                Kind = "attack",
                Title = $"{def.Name} · {AbilityTexts.WeaponLabel(u, ability)}",
                Action = def.Name,
                TargetName = target.Name,
                Hit = shot.Hit,
                Crit = Math.Min(shot.Crit, shot.Hit),
                Graze = shot.Graze,
                Mods = shot.Mods.Select(m => new ModView { Label = m.Label, Value = m.Value }).ToList(),
                CritMods = shot.CritMods.Select(m => new ModView { Label = m.Label, Value = m.Value }).ToList(),
                Index = index,
                Total = list.Count,
                Note = AbilityTexts.AttackNotes.TryGetValue(ability, out var note) ? note : null,
                Locked = LockLabel(),
                Noise = loud > 0 ? $"Ruido {loud}" : "Sin ruido",
            };
            if (d != null)
            {
                view.HasDamage = true;
                view.Damage = new DamageView
                {
                    Hit = AbilityTexts.Span(d.Hit),
                    Crit = d.Crit != null && shot.Crit > 0 ? AbilityTexts.Span(d.Crit) : null,
                    Graze = d.Graze != null && shot.Graze > 0 ? AbilityTexts.Span(d.Graze) : null,
                    Notes = AbilityTexts.ArmorNotes(d, target),
                    Kill = AbilityTexts.Lethality(d, target, shot.Crit > 0),
                };
            }
            return view;
        }

        /// <summary>The armed hand's panel (game.ts handPanel): «Registrar el maletero» with the room left in the backpack, «Abrir la puerta».</summary>
        ShotView HandPanel(Unit u)
        {
            var id = HandArmed();
            var tile = HandTarget();
            if (id == null || tile == null) return null;
            var def = Content.Abilities[id];
            var others = HandTiles().Count - 1;
            var view = new ShotView { Kind = "self", Action = def.Name, Locked = LockLabel(), Cost = AbilityTexts.CostText(u, id), Noise = AbilityTexts.NoiseText(State, u, id), Effect = def.Description, Title = def.Name };
            if (id != AbilityIds.Search)
            {
                var what = id == AbilityIds.OpenDoor ? "abrir" : "cerrar";
                view.Hint = $"Otra vez, Espacio o clic en la puerta: {what}{(others > 0 ? $" · Tab: {(others == 1 ? "la otra" : "otra")} puerta" : "")} · Esc: cancelar";
                return view;
            }
            var decor = Looting.SearchSpot(State, tile);
            if (decor == null) return null;
            var slots = u.PackSlots ?? Content.BackpackSlots;
            var room = slots - (u.Backpack?.Count ?? 0);
            view.Title = Lore.BoxTitle(decor);
            view.Detail = $"Mochila: {(room == 1 ? "queda 1 hueco" : $"quedan {room} huecos")} de {slots}";
            view.Hint = $"Otra vez, Espacio o clic en la caja: abrir{(others > 0 ? $" · Tab: {(others == 1 ? "la otra" : "otra")} caja" : "")} · Esc: cancelar";
            return view;
        }

        /// <summary>What an armed self ability would do right now: whom it reaches, the odds, the clip.</summary>
        string SelfDetail(Unit u, string id)
        {
            switch (id)
            {
                case AbilityIds.Faceoff:
                {
                    var n = Abilities.RangedTargets(State, u, AbilityIds.Faceoff).Count(Visible);
                    return n > 0 ? $"{n} {(n == 1 ? "enemigo" : "enemigos")} a su alcance" : "Ningún enemigo a su alcance";
                }
                case AbilityIds.Hack:
                    return $"Probabilidad: {Abilities.HackChance(State, u)} %";
                case AbilityIds.Reload:
                    return $"Cargador: {u.Ammo}/{Content.AttackWeapon(u, AbilityIds.Shoot).Clip}{(u.Mags == null ? "" : u.Mags > 0 ? $" · {u.Mags} de repuesto" : " · sin repuesto")}";
            }
            return null;
        }

        // ------------------------------------------------------- the arrival

        /// <summary>The log's colour for the living and the dead fighting each other (game.ts FEUD_COLOR).</summary>
        const string FeudColor = "#e8d8c0";

        /// <summary>
        /// The arrival card (game.ts intro), XCOM-style: what kind of fight, the operation's name, the objective, the turns
        /// under it and the briefing whole (playtest 13/10 #7, 19/10 #12). The log keeps a night's rules (Arturo, 22/10
        /// #57), and says when the living are among the dead (18/10 #47).
        /// </summary>
        public void Intro()
        {
            var s = State;
            if (Hud == null || s.Outcome != null) return;
            var mission = MissionView();
            var cm = s.CampaignMission;
            var briefing = cm?.Briefing ?? (s.Mission.Siege != null ? Lore.SiegeText.Briefing : null);
            Hud.Intro(new IntroView
            {
                Kicker = Kicker() + (s.Mission.Night == true ? $" · {Lore.NightText.Label.ToUpperInvariant()}" : ""),
                Title = cm?.Name ?? (s.Mission.Siege != null ? Lore.SiegeText.Name : MissionName(s.Mission.Kind)),
                Objective = mission.Objective,
                Turns = mission.TurnsLeft >= 0 ? $"{mission.TurnsLeft} turnos" : null,
                Briefing = briefing,
            });
            if (s.Units.Any(u => u.Alive && u.Team == Teams.Alien && !u.Objective && !Content.Templates[u.Template].Undead)) Hud.Log(Lore.LivingTip, FeudColor);
            if (s.Mission.Night == true) Hud.Log(string.Join(" ", Lore.NightText.Rules), "#2a4a7a");
        }

        /// <summary>The briefing's kicker: the operation's, the story chapter's, or what kind of fight it is.</summary>
        string Kicker()
        {
            var cm = State.CampaignMission;
            var chapter = cm?.Story;
            if (chapter == 1) return "";
            return cm?.Kicker ?? (chapter != null ? $"DIARIO · HOJA {chapter}" : cm != null ? "MISIÓN EN CURSO" : "ESCARAMUZA");
        }

        /// <summary>
        /// The opening shot (game.ts openingShot, UX-REVISION-2 #32): the squad framed with the map, or the enemies already
        /// in sight, in front of it, at most six tiles towards them; the player's first move of the camera ends it.
        /// </summary>
        public void OpeningShot()
        {
            if (Rig == null) return;
            var v = Shown;
            var squad = v.Units.Where(u => u.Alive && u.Team == Teams.Xcom && !u.Captive && !u.Objective).ToList();
            if (squad.Count == 0) return;
            var here = new UnityEngine.Vector2((float)squad.Average(u => u.Pos.X), (float)squad.Average(u => u.Pos.Y));
            var foes = v.Units.Where(a => a.Alive && a.Team == Teams.Alien && Visible(a)).ToList();
            var toward = foes.Count > 0 ? new UnityEngine.Vector2((float)foes.Average(a => a.Pos.X), (float)foes.Average(a => a.Pos.Y)) : new UnityEngine.Vector2(v.Width / 2f, v.Height / 2f);
            var gap = UnityEngine.Vector2.Distance(here, toward);
            var k = gap > 0 ? Math.Min(6, gap / 2) / gap : 0;
            var at = here + (toward - here) * k;
            Rig.Focus(Board.TileToWorld(v, new Pos { X = (int)Math.Round(at.x), Y = (int)Math.Round(at.y) }), true);
        }

        /// <summary>
        /// The host's events, just applied to the state (CombatSession): what the controller keeps from them. The partner's
        /// last soldier moved, for «Ver» (game.ts partnerLast).
        /// </summary>
        public void Played(IList<GameEvent> events, int? by)
        {
            foreach (var e in events)
                if (e is MovedEvent moved && UnitById(State, moved.Unit) is Unit u && u.Owner != null && u.Owner != MySlot) PartnerLast = u.Id;
        }

        // ------------------------------------------------------- calls

        /// <summary>
        /// The partner's call waiting for this player's answer (Ana, 22/10 #39): to leave in the bus or to abandon, in the
        /// middle of the screen with its clock; none for one's own call (game.ts callView).
        /// </summary>
        CallView CallView()
        {
            var s = State;
            if (s.Outcome != null) return null;
            var evac = s.EvacCall;
            if (evac != null && evac.By != MySlot)
            {
                var ask = Lore.EvacAsk(PlayerName(evac.By), EvacSituation(s));
                return new CallView
                {
                    Kind = "evac",
                    Kicker = s.Mission.Kind == MissionKinds.Elimination ? "Retirada" : "Evacuación",
                    Title = ask.Title,
                    Text = ask.Text,
                    ClockNote = $"Sin respuesta, {Lore.Bus.Name} sale",
                    Accept = Lore.EvacBar.Accept,
                    Refuse = Lore.EvacBar.Wait,
                };
            }
            var abandon = s.AbandonCall;
            if (abandon != null && abandon.By != MySlot)
                return new CallView
                {
                    Kind = "abandon",
                    Kicker = "",
                    Title = Lore.AbandonAsk(PlayerName(abandon.By)),
                    Text = Lore.AbandonText.Stakes,
                    ClockNote = "Sin respuesta, se acepta sola",
                    Accept = Lore.AbandonText.Accept,
                    Refuse = Lore.AbandonText.Refuse,
                };
            return null;
        }
    }
}
