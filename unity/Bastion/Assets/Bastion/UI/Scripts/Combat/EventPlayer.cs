using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bastion.Client.Effects;
using Bastion.Client.Units;
using Bastion.Engine;
using Bastion.UI.Hud;
using UnityEngine;
using Grid = Bastion.Engine.Grid;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// The host's events played one by one (game.ts pump, settle and play; UI's, Gameplay 24/10): each is seen on the
    /// state as it was before it (View), with the figures' animations and the effects (Arte's UnitViews and Fx, awaited:
    /// «each action is seen whole before the next»), its line in the log, what rises over whom it touches, its stamp, and
    /// the camera on it while the player hasn't taken the camera; then it is applied to View, the figures follow (Sync)
    /// and so does the HUD. The squad's turn is the player's at once (orders go by State); the enemy's is seen at its pace.
    /// The enemy's turn (docs/UX-RITMO.md): a pause between those who act, the camera to each one acting in sight and a
    /// still moment, each attack's result read before what follows; the dead that only walk, all at once under one
    /// framing; arrivals and contacts of the same moment, and a blast's results, together. It plays at the chosen speed,
    /// «Rápida» while the space bar is held (Time.timeScale; the camera keeps unscaled time, it is the player's).
    /// Without Arte's figures in the scene (the lab), an event takes only its pause to be read.
    /// The attack that kills is seen over the shoulder (ShotCam), and one of ours falling, close (Ana, 22/10 #21); a dead's
    /// blow on one of ours, over its shoulder too (Ana, Arturo 29/10).
    /// The dead walking together are planned so nobody walks through anybody (WalkPlan, walkPlan.ts).
    /// </summary>
    public sealed class EventPlayer
    {
        readonly CombatController control;
        readonly CancellationToken stop;
        readonly Queue<(GameEvent e, int? by, int epoch)> queue = new Queue<(GameEvent, int?, int)>();
        bool playing;

        /// <summary>What the squad sees of the ground (los.ts teamVisibility), from View: the fog's, for what to show.</summary>
        bool[] sight;

        // What earlier events leave for later ones (game.ts): the last shot's result for its damage, who struck, a bite's toll.
        (string target, string result)? lastShot;
        string lastAttacker;
        (string unit, string ability)? lastUsed;
        bool biteTick;
        bool medicineHeal;
        bool medicineCure;
        bool itemFell;
        bool alarmRaised;
        bool risenSeen;
        bool departedOnTime;
        ConcealmentBrokenEvent exposure;

        /// <summary>A partner's moment (game.ts moment): where it happened, for «Ver» (F) while its notice is up.</summary>
        Vector3? moment;

        static readonly System.Random dice = new System.Random();

        // docs/UX-RITMO.md's times (game.ts): a still moment once the camera is on an enemy, an attack's result read,
        // the pause between two enemies acting, how near the camera counts as there, how long a camera the player moved is
        // theirs, how much quicker the dead walk together.
        const float SettleTime = 0.35f;
        const float ReadHit = 0.9f;
        const float ReadMiss = 0.7f;
        const float Between = 0.5f;
        const float NearEnough = 5 * Board.Tile;
        const float PlayerCamera = 2.5f;
        const float WalkersPace = 1.25f;
        /// <summary>«Rápida»: everything takes 0.6 of the time (tween.ts FAST_RATE).</summary>
        const float FastRate = 1 / 0.6f;
        /// <summary>The shot over the shoulder stays this long once the target has fallen; on one of ours, longer; eaten, a moment more.</summary>
        const float KillHold = 0.5f;
        const float DeathHold = 1.2f;
        const float FeedHold = 0.5f;
        /// <summary>A shooter stepping out onto a tile something takes leans this far towards it instead.</summary>
        const float LeanOut = 0.35f * Board.Tile;

        /// <summary>The one whose fall the shot over the shoulder waits for, and how long it stays after.</summary>
        string killCam;
        float? killHold;

        /// <summary>What starts something new: the shot over the shoulder of the last one ends before it.</summary>
        static bool NewAction(GameEvent e) => e is ShotEvent || e is MovedEvent || e is AbilityUsedEvent || e is ExplosiveEvent || e is StoneThrownEvent || e is TurnBeganEvent || e is MissionEndedEvent;

        /// <summary>The shot over a shoulder on now, and who is in it (the attacker, the target): the buildings' cutaway leaves its room whole.</summary>
        public (string attacker, string target)? CloseFocus => control.Rig != null && control.Rig.CloseUp ? (lastAttacker, killCam) : ((string, string)?)null;

        /// <summary>The shot on now is a dead's blow, not a kill: its next blow on the same one keeps it.</summary>
        bool biting;

        void StartKillCam(string id, (Vector3 pos, Vector3 look) shot, bool bite = false)
        {
            killCam = id;
            biting = bite;
            control.Rig?.CloseShot(shot.pos, shot.look);
        }

        /// <summary>Back from the shot over the shoulder to where the camera was.</summary>
        void EndKillCam()
        {
            if (killCam == null) return;
            killCam = null;
            biting = false;
            control.Rig?.EndCloseShot();
        }

        /// <summary>
        /// One of ours about to fall (Ana, 22/10 #21): close on them, from in front and a little aside, out towards whoever
        /// does it (or where the camera was), so the fall is seen whole and the one doing it in the frame where there is
        /// room; null where there is none.
        /// </summary>
        (Vector3 pos, Vector3 look)? DeathShot(Unit victim, Unit by, bool fallen = false)
        {
            var chest = fallen ? (Units != null ? Units.BodyPos(victim.Id) : Feet(victim.Id)) + Vector3.up * (0.25f * Board.Tile) : Chest(victim.Id);
            var toward = by != null && by.Id != victim.Id ? Feet(by.Id) : control.Rig != null ? control.Rig.Camera.transform.position : chest + Vector3.back;
            var away = new Vector3(toward.x - chest.x, 0, toward.z - chest.z);
            if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
            away = Quaternion.Euler(0, -0.6f * Mathf.Rad2Deg, 0) * away.normalized;
            var from = chest + away * (2.6f * Board.Tile);
            from.y = chest.y + (fallen ? 1.1f : 0.4f) * Board.Tile;
            return by != null ? ShotCam.OverShoulder(V, from, chest, victim.Id, by.Id) : ShotCam.OverShoulder(V, from, chest, victim.Id);
        }

        /// <summary>What follows an enemy's action and belongs to it: it doesn't end the action, nor start the next (game.ts FOLLOW_UP).</summary>
        static bool FollowUp(GameEvent e) => e is DamagedEvent || e is DiedEvent || e is InfectedEvent || e is StunnedEvent || e is DisorientedEvent || e is NoiseEvent || e is TileChangedEvent || e is FloorBrokenEvent || e is FellEvent || e is ItemDroppedEvent;

        /// <summary>The pause after the last event of one of this player's own actions before the controls are back (game.ts HOLD_AFTER).</summary>
        static float HoldAfter(GameEvent e)
        {
            switch (e)
            {
                case DamagedEvent _: return 1;
                case DiedEvent _: return 0.6f;
                case ShotEvent _: return 0.8f;
                case StunnedEvent _: return 0.6f;
                case TileChangedEvent _: return 0.6f;
                case ExplosiveEvent _: return 0.6f;
                case HackedEvent _: return 0.8f;
                case VipRescuedEvent _: return 0.5f;
                case SuppressedEvent _: return 0.6f;
                case CleansedEvent _: return 0.4f;
                case RefundEvent _: return 0.4f;
                case AbilityUsedEvent _: return 0.3f;
                case HealedEvent _: return 0.5f;
                case AidedEvent _: return 0.5f;
                case EvacuatedEvent _: return 0.4f;
                case SearchedEvent _: return 0.7f;
                case InfectedEvent _: return 0.75f;
                case MovedEvent _: return 0.25f;
                default: return 0.2f;
            }
        }

        /// <summary>The enemy acting last (or «walkers»), the result still to be read, and the last group's framing.</summary>
        string lastActor;
        float resultHold;
        (HashSet<string> ids, Vector3 centre, float radius)? groupFrame;
        /// <summary>Playing several at once: no single one takes the camera.</summary>
        bool grouped;

        /// <summary>The group's walkers' plans (WalkPlan): when each stands before each step, where it swerves, how quick.</summary>
        Dictionary<string, WalkPlan.Plan> groupPlans;
        /// <summary>The command has come to this player: its planned routes are walked once what is queued has been seen.</summary>
        bool planDue;

        /// <summary>The events of this player's own orders still to be seen, and the pause owed after the last.</summary>
        int myPending;
        float holdAfter;
        bool hurry;

        /// <summary>The enemy turn's speed (Ajustes' «Velocidad del combate»: ×1/1.4, ×1, ×1/0.6), kept on this computer.</summary>
        public static float EnemySpeed
        {
            get => PlayerPrefs.GetFloat("bastion.enemySpeed", 1);
            set => PlayerPrefs.SetFloat("bastion.enemySpeed", value);
        }

        /// <summary>«Velocidad de los soldados» (settings.ts walkSpeed; Ana, 22/10 #32): how fast the squad walks on screen, ×0.5 to ×2, kept on this computer.</summary>
        public static float WalkSpeed
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat("bastion.walkSpeed", 1), 0.5f, 2f);
            set => PlayerPrefs.SetFloat("bastion.walkSpeed", value);
        }

        /// <summary>The space bar held in the enemy's turn: at least «Rápida» until it is let go.</summary>
        public void SetHurry(bool on)
        {
            hurry = on;
            Pace();
        }

        /// <summary>Whether the enemy's turn is on screen now (the space bar hurries it then, and gives no orders).</summary>
        public bool EnemyTurnPlaying => V != null && V.ActiveTeam == Teams.Alien && V.Outcome == null;

        /// <summary>Either player's menu open in the mission (CombatController.PauseTick): the battle's time stands still.</summary>
        public void SetHalted(bool on)
        {
            halted = on;
            Pace();
        }

        bool halted;

        /// <summary>The battle's time: stopped in the pause; the enemy's turn at its speed while it plays; the rest at 1.</summary>
        void Pace()
        {
            Time.timeScale = halted ? 0 : playing && EnemyTurnPlaying ? (hurry ? Math.Max(EnemySpeed, FastRate) : EnemySpeed) : 1;
        }

        public EventPlayer(CombatController control, CancellationToken stop)
        {
            this.control = control;
            this.stop = stop;
        }

        /// <summary>Events wait to be seen, or are being seen: the turn clock waits (game.ts reportIdle).</summary>
        public bool Busy => playing || queue.Count > 0;

        /// <summary>The event being seen now.</summary>
        GameEvent current;

        /// <summary>Whether `id` still has events to be seen (game.ts isBusy): its move's marks wait for them.</summary>
        public bool IsBusy(string id)
        {
            if (id == null) return false;
            if (current != null && UnitOf(current) == id) return true;
            foreach (var (e, _, _) in queue)
                if (UnitOf(e) == id) return true;
            return false;
        }

        GameState V => control.View;
        HudRoot Hud => control.Hud;
        static UnitViews Units => UnitViews.Current;
        static Fx Effects => Fx.Current;

        /// <summary>A whole state (a snapshot): what is seen starts from it, the figures with it, and whatever was waiting goes.</summary>
        public void Load(GameState state)
        {
            queue.Clear();
            control.Aboard.Clear();
            control.View = Events.CloneState(state);
            See();
            ShowSeen(true);
        }

        /// <summary>The figures and the fog of war (Arte's) as `View` is now; `instant`, the fog at once (a snapshot), else fading.</summary>
        void ShowSeen(bool instant = false)
        {
            Units?.Sync(V);
            Bastion.Client.Map.Fog.Current?.Show(V, instant);
        }

        /// <summary>The host's events (already on State), to be seen in order.</summary>
        public void Push(IList<GameEvent> events, int? by)
        {
            var epoch = control.Rig != null ? control.Rig.Epoch : 0;
            foreach (var e in events) queue.Enqueue((e, by, epoch));
            if (by == control.MySlot) myPending += events.Count;
            if (!playing) _ = Pump();
        }

        async Awaitable Pump()
        {
            playing = true;
            try
            {
                while (queue.Count > 0 && !stop.IsCancellationRequested)
                {
                    Pace();
                    var item = queue.Dequeue();
                    // (A dead's next blow on the same soldier stays over its shoulder: no out and in between two.)
                    if (killCam != null && NewAction(item.e) && !(biting && item.e is ShotEvent again && again.Unit == lastAttacker && again.Target == killCam)) EndKillCam();
                    // The dead that only walk this turn come last (ai.ts), and walk all at once, under one framing.
                    var walkers = WalkingWith(item);
                    if (walkers.Count > 0)
                    {
                        walkers.Insert(0, item);
                        await PlayGroup(walkers);
                        continue;
                    }
                    // Arrivals, or contacts, of the same moment play together too (playtest 18/10 #6).
                    var moment = SameMoment(item);
                    if (moment.Count > 0)
                    {
                        moment.Insert(0, item);
                        await PlayTogether(moment);
                        continue;
                    }
                    await BeforeActing(item.e);
                    var epoch = control.Rig != null ? control.Rig.Epoch : 0;
                    current = item.e;
                    await PlaySafe(item);
                    current = null;
                    NoteResult(item.e);
                    await Settle(item.e, item.by, false, epoch);
                    // The one it killed has fallen: a moment more over the shoulder (on one of ours, longer), then back.
                    if (killCam != null && item.e is DiedEvent fell && fell.Unit == killCam)
                    {
                        await Wait(killHold ?? (Unit(fell.Unit)?.Team == Teams.Xcom ? DeathHold : KillHold));
                        killHold = null;
                        EndKillCam();
                    }
                    // What a blast does to everyone in it plays at once (playtest 15/10 #16).
                    if (item.e is ExplosiveEvent)
                    {
                        var blast = BlastResults();
                        if (blast.Count > 0) await PlayBlast(blast);
                    }
                }
                EndKillCam();
                // The last enemy attack of the turn is read too, before the turn changes hands.
                if (resultHold > 0)
                {
                    var hold = resultHold;
                    resultHold = 0;
                    await Wait(hold);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                playing = false;
                current = null;
                grouped = false;
                Pace();
            }
            if (stop.IsCancellationRequested) return;
            control.Refresh();
            // The routes planned without the command, walked one by one now that everything has been seen (18/10 #3).
            if (planDue)
            {
                planDue = false;
                control.StartFollowing();
            }
            control.FollowPlans();
        }

        /// <summary>One event played, whatever goes wrong in it.</summary>
        async Awaitable PlaySafe((GameEvent e, int? by, int epoch) item)
        {
            try
            {
                // With the sound's trace on (BASTION_SONIDO_TRAZA), each event as it starts playing, beside what it sounds.
                Bastion.Client.Audio.Sound.Note($"se toca {item.e.GetType().Name.Replace("Event", "")}");
                await Play(item.e, item.by, item.epoch);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception err)
            {
                Debug.LogException(err);
            }
        }

        /// <summary>
        /// docs/UX-RITMO.md, before an event plays (game.ts beforeActing): the result of the last enemy attack is read first;
        /// and an enemy that starts acting in sight is shown before it does (a pause after the previous one, the camera to
        /// it unless it is near or the player has the camera, and a still moment).
        /// </summary>
        async Awaitable BeforeActing(GameEvent e)
        {
            if (FollowUp(e)) return;
            var hold = resultHold;
            resultHold = 0;
            var actor = EnemyActor(e);
            if (actor == null || actor.Id == lastActor)
            {
                if (hold > 0) await Wait(hold);
                return;
            }
            var first = lastActor == null;
            lastActor = actor.Id;
            // One that was in the last group's framing is in view already: only what is left of reading the last result.
            if (InGroupFrame(actor))
            {
                if (hold > 0) await Wait(hold);
                return;
            }
            groupFrame = null;
            await Wait(Math.Max(first ? 0 : Between, hold));
            await ShowEnemy(new List<Vector3> { Feet(actor.Id) });
        }

        /// <summary>The enemy that `e` makes act in sight in the enemy's turn (it moves, attacks, shouts or throws); null for anything else.</summary>
        Unit EnemyActor(GameEvent e)
        {
            if (V.ActiveTeam != Teams.Alien || FollowUp(e)) return null;
            var u = Unit(UnitOf(e));
            if (u == null || u.Team != Teams.Alien || !u.Alive) return null;
            var seen = ShownToSquad(u) || (e is MovedEvent m && m.Path.Any(p => IsVisible(p.X, p.Y)));
            return seen ? u : null;
        }

        bool InGroupFrame(Unit u)
        {
            if (groupFrame == null || control.Rig == null) return false;
            var f = groupFrame.Value;
            var at = Feet(u.Id);
            var reach = Math.Max(NearEnough, f.radius + Board.Tile);
            return (f.ids.Contains(u.Id) || Vector3.Distance(at, f.centre) <= reach) && Vector3.Distance(at, control.Rig.Target) <= reach;
        }

        /// <summary>The camera to where `points` are (unless near, or the player has had it lately), there, and a still moment.</summary>
        async Awaitable ShowEnemy(List<Vector3> points)
        {
            var rig = control.Rig;
            if (rig != null && points.Count > 0)
            {
                var bounds = new Bounds(points[0], Vector3.zero);
                foreach (var p in points) bounds.Encapsulate(p);
                var centre = bounds.center;
                var theirs = Time.unscaledTime - rig.TakenAt < PlayerCamera;
                if (!theirs && Vector3.Distance(centre, rig.Target) > NearEnough)
                {
                    rig.Focus(centre);
                    for (var i = 0; i < 24 && !rig.Settled; i++) await Wait(0.05f);
                }
            }
            await Wait(SettleTime);
        }

        /// <summary>An enemy attack just played: its result is to be read before the next thing starts.</summary>
        void NoteResult(GameEvent e)
        {
            if (V.ActiveTeam != Teams.Alien) return;
            if (e is ShotEvent s)
            {
                var shooter = Unit(s.Unit);
                var target = Unit(s.Target);
                if (shooter?.Team == Teams.Alien && (ShownToSquad(shooter) || (target != null && ShownToSquad(target)))) resultHold = s.Result == "miss" ? ReadMiss : ReadHit;
            }
            else if ((e is ExplosiveEvent || e is PsiEvent) && EnemyActor(e) != null) resultHold = ReadHit;
        }

        /// <summary>Whether `e` is a walk of one of the dead in the enemy's turn (they only bite: docs/ZOMBIS-PRIMERO.md).</summary>
        bool DeadWalker(GameEvent e)
        {
            if (!(e is MovedEvent m) || V.ActiveTeam != Teams.Alien) return false;
            var u = Unit(m.Unit);
            return u != null && u.Team == Teams.Alien && u.Alive && !AbilityTexts.ShootsFromRange(u);
        }

        /// <summary>
        /// The walks of the dead right after `item`'s, taken off the queue (none if it walks alone): the engine moves the
        /// dead with nobody in reach after those going for the squad, so theirs come in one run at the end of the turn. A
        /// door one of them opens on its way (its tileChanged and its noise) stays in the run (playtest 17/10 #46).
        /// </summary>
        List<(GameEvent e, int? by, int epoch)> WalkingWith((GameEvent e, int? by, int epoch) item)
        {
            var run = new List<(GameEvent, int?, int)>();
            if (!DeadWalker(item.e)) return run;
            var all = queue.ToList();
            var n = 0;
            var last = 0;
            while (n < all.Count)
            {
                var e = all[n].e;
                if (DeadWalker(e)) last = ++n;
                else if (e is TileChangedEvent || e is NoiseEvent) n++;
                else break;
            }
            for (var i = 0; i < last; i++) run.Add(queue.Dequeue());
            return run;
        }

        /// <summary>
        /// The dead walking in one run (game.ts playGroup; playtest 16/10 #6): one framing over where they are and where they
        /// go, a still moment, and they all walk at once, each one's actions as one stretch, a little quicker. The doors they
        /// open go first, at once.
        /// </summary>
        async Awaitable PlayGroup(List<(GameEvent e, int? by, int epoch)> all)
        {
            var v = V;
            var doors = all.Where(q => !(q.e is MovedEvent)).ToList();
            var items = all.Where(q => q.e is MovedEvent).ToList();
            var shown = items.Where(q => EnemyActor(q.e) != null).ToList();
            var hold = resultHold;
            resultHold = 0;
            if (shown.Count > 0)
            {
                var first = lastActor == null;
                lastActor = "walkers";
                await Wait(Math.Max(first ? 0 : Between, hold));
                var points = new List<Vector3>();
                foreach (var q in shown)
                {
                    var m = (MovedEvent)q.e;
                    points.Add(Feet(m.Unit));
                    points.Add(m.Path.Count > 0 ? Board.TileToWorld(v, m.Path[m.Path.Count - 1]) : Feet(m.Unit));
                }
                var bounds = new Bounds(points[0], Vector3.zero);
                foreach (var p in points) bounds.Encapsulate(p);
                groupFrame = (new HashSet<string>(items.Select(q => ((MovedEvent)q.e).Unit)), bounds.center, bounds.size.magnitude / 2);
                await ShowEnemy(points);
            }
            else if (hold > 0) await Wait(hold);
            foreach (var q in doors) await Settle(q.e, q.by, false, q.epoch);
            // Each one's walks as one: its two actions a single stretch, no stop between.
            var walks = new Dictionary<string, MovedEvent>();
            var order = new List<string>();
            foreach (var q in items)
            {
                var m = (MovedEvent)q.e;
                if (walks.TryGetValue(m.Unit, out var had)) walks[m.Unit] = new MovedEvent { Unit = m.Unit, Path = had.Path.Concat(m.Path).ToList(), Ap = had.Ap + m.Ap };
                else
                {
                    walks[m.Unit] = m;
                    order.Add(m.Unit);
                }
            }
            groupPlans = PlanGroup(order.Select(id => walks[id]).ToList());
            grouped = true;
            current = items[0].e;
            var playing = order.Select(id => PlaySafe((walks[id], items[0].by, items[0].epoch))).ToList();
            try
            {
                foreach (var w in playing) await w;
            }
            finally
            {
                grouped = false;
                groupPlans = null;
                current = null;
            }
            // All of them where they stopped before the view syncs (playtest 21/10 #21: they walked in, vanished, and came
            // back all at once).
            foreach (var q in items) Events.ApplyEvent(V, q.e);
            foreach (var q in items) await Settle(q.e, q.by, true, q.epoch);
        }

        /// <summary>
        /// When each of a group's walkers sets off and where it stands a moment (game.ts planGroup): the engine planned them
        /// one after another (in `walks`' order), each from where the ones before ended, and they are played at once, at
        /// their own paces. Only the walks the squad sees (the others are not played); none without Arte's figures.
        /// </summary>
        Dictionary<string, WalkPlan.Plan> PlanGroup(List<MovedEvent> walks)
        {
            var units = Units;
            if (units == null) return null;
            var v = V;
            var ids = new List<string>();
            var walkers = new List<WalkPlan.Walker>();
            var paced = new List<Func<float, float[]>>();
            foreach (var e in walks)
            {
                var u = Unit(e.Unit);
                if (u == null || e.Path.Count == 0 || !(ShownToSquad(u) || e.Path.Any(p => IsVisible(p.X, p.Y)))) continue;
                var tiles = new List<Pos> { u.Pos };
                tiles.AddRange(e.Path);
                var id = u.Id;
                var from = u.Pos;
                ids.Add(id);
                paced.Add(pace => units.StepSeconds(id, from, e.Path, e.Ap, pace));
                walkers.Add(new WalkPlan.Walker
                {
                    Tiles = tiles.Select(p => Grid.NodeOf(v, p)).ToArray(),
                    Steps = units.StepSeconds(id, from, e.Path, e.Ap, WalkersPace),
                    Corners = tiles.Select((p, k) =>
                    {
                        if (k + 1 >= tiles.Count) return ((int, int)?)null;
                        var next = tiles[k + 1];
                        return next.X != p.X && next.Y != p.Y && next.Z == p.Z ? (Grid.NodeOf(v, new Pos { X = next.X, Y = p.Y, Z = p.Z }), Grid.NodeOf(v, new Pos { X = p.X, Y = next.Y, Z = p.Z })) : ((int, int)?)null;
                    }).ToArray(),
                });
            }
            if (walkers.Count == 0) return null;
            // Quicker if they would take too long all together: each step as MoveAlong plays it at that pace.
            var plans = WalkPlan.PlanHasted(haste => haste == 1 ? walkers : walkers.Select((w, i) => new WalkPlan.Walker { Tiles = w.Tiles, Steps = paced[i](WalkersPace * haste), Corners = w.Corners }).ToList());
            var map = new Dictionary<string, WalkPlan.Plan>();
            for (var i = 0; i < ids.Count; i++) map[ids[i]] = plans[i];
            return map;
        }

        /// <summary>The arrivals (`spawned`) or the contacts (`podActivated`) that follow `item`, of its kind: seen at once (playtest 18/10 #6).</summary>
        List<(GameEvent e, int? by, int epoch)> SameMoment((GameEvent e, int? by, int epoch) item)
        {
            var run = new List<(GameEvent, int?, int)>();
            var kind = item.e.GetType();
            if (!(item.e is SpawnedEvent) && !(item.e is PodActivatedEvent)) return run;
            while (queue.Count > 0 && queue.Peek().e.GetType() == kind) run.Add(queue.Dequeue());
            return run;
        }

        /// <summary>A moment's arrivals or contacts at once: one framing over all of them in sight, then each plays at the same time.</summary>
        async Awaitable PlayTogether(List<(GameEvent e, int? by, int epoch)> all)
        {
            var v = V;
            var points = new List<Vector3>();
            foreach (var q in all)
            {
                if (q.e is SpawnedEvent sp) points.AddRange(sp.Units.Where(u => IsVisible(u.Pos.X, u.Pos.Y)).Select(u => Board.TileToWorld(v, u.Pos)));
                else if (q.e is PodActivatedEvent pa)
                {
                    var feud = pa.Cause == "attacked" && pa.Unit == null;
                    var pod = v.Pods.FirstOrDefault(p => p.Id == pa.Pod);
                    points.AddRange((pod?.UnitIds ?? new List<string>()).Select(Unit).Where(m => m != null && m.Alive && (!feud || ShownToSquad(m))).Select(m => Board.TileToWorld(v, m.Pos)));
                }
            }
            if (points.Count > 0 && (v.ActiveTeam == Teams.Alien || all.Any(q => q.by == control.MySlot))) await ShowEnemy(points);
            grouped = true;
            current = all[0].e;
            var plays = all.Select(PlaySafe).ToList();
            try
            {
                foreach (var p in plays) await p;
            }
            finally
            {
                grouped = false;
                current = null;
            }
            foreach (var q in all)
            {
                NoteResult(q.e);
                await Settle(q.e, q.by, false, q.epoch);
            }
        }

        /// <summary>Whether the next event is another soldier boarding La Lata (then it waits for them).</summary>
        bool NextBoards() => queue.Count > 0 && queue.Peek().e is EvacuatedEvent;

        /// <summary>La Lata pulling away off the map (game.ts busLeaves: Arte's MapView.LataDeparts, ≈2.6 s; at once without it).</summary>
        async Awaitable LataLeaves()
        {
            var map = Bastion.Client.Map.MapView.Current;
            if (map?.LataAt is Vector3 bus) control.Sfx.Vehicle(bus);
            if (map != null) await map.LataDeparts(stop);
        }

        /// <summary>What a blast leaves on everyone it caught, right after its `explosive`, taken off the queue; none unless at least two.</summary>
        List<(GameEvent e, int? by, int epoch)> BlastResults()
        {
            var all = queue.ToList();
            var n = 0;
            while (n < all.Count && (all[n].e is DamagedEvent || all[n].e is DiedEvent || all[n].e is InfectedEvent || all[n].e is StunnedEvent)) n++;
            var run = new List<(GameEvent, int?, int)>();
            if (n < 2) return run;
            for (var i = 0; i < n; i++) run.Add(queue.Dequeue());
            return run;
        }

        /// <summary>A blast's results at once (playtest 15/10 #16): everyone it caught is hit together, then all who die fall together.</summary>
        async Awaitable PlayBlast(List<(GameEvent e, int? by, int epoch)> items)
        {
            grouped = true;
            try
            {
                foreach (var falls in new[] { false, true })
                {
                    var these = items.Where(q => (q.e is DiedEvent) == falls).ToList();
                    if (these.Count == 0) continue;
                    current = these[0].e;
                    var plays = these.Select(PlaySafe).ToList();
                    foreach (var p in plays) await p;
                }
            }
            finally
            {
                grouped = false;
                current = null;
            }
            foreach (var q in items)
            {
                NoteResult(q.e);
                await Settle(q.e, q.by, false, q.epoch);
            }
        }

        /// <summary>What a played event leaves behind (game.ts settle): View has it, the figures, the sight and the HUD follow.</summary>
        /// <remarks>`epoch`: the camera's as the event began to play (none: now); the player who moved it since keeps it.</remarks>
        async Awaitable Settle(GameEvent e, int? by, bool applied = false, int? epoch = null)
        {
            var call = V.EvacCall;
            // Who was in sight until this event, for the newcomers' callouts (AnnounceSighted).
            var before = new HashSet<string>(V.Units.Where(a => a.Alive && a.Team == Teams.Alien && control.Visible(a)).Select(a => a.Id));
            var floorBefore = new HashSet<string>((V.Dropped ?? new List<DroppedLoot>()).Select(d => d.Id));
            if (!applied) Events.ApplyEvent(V, e);
            // A soldier falls and their rucksack stays on the floor (#49), said once.
            if (e is DiedEvent died && (V.Dropped ?? new List<DroppedLoot>()).Any(d => d.From == died.Unit && !floorBefore.Contains(d.Id)) && Unit(died.Unit) is Unit fallen) Log(Lore.FallenPackLine(fallen.Name), "#ff8a78");
            // A find with the backpack full stays on the floor (docs/FASE8.md §C).
            if ((e is SearchedEvent || e is RoomSearchedEvent) && by == control.MySlot)
            {
                var who = Unit(e is SearchedEvent se ? se.Unit : ((RoomSearchedEvent)e).Unit);
                var left = (V.Dropped ?? new List<DroppedLoot>()).FirstOrDefault(d => !floorBefore.Contains(d.Id) && who != null && d.Pos.X == who.Pos.X && d.Pos.Y == who.Pos.Y && d.Pos.Z == who.Pos.Z);
                if (left != null) control.PackFull(who.Id, left.Id, Lore.PackFullLine(left.Loot));
            }
            // The next thing of a rucksack being taken, a swap's find (CombatController.Pack).
            control.LootSettled(e, by, V);
            // A call nobody answered lapses with the squad's turn.
            if (e is TurnBeganEvent && call != null && V.EvacCall == null) Log(Lore.EvacText.Lapsed, "#ffc93a");
            // Plans (playtest 18/10 #3): the partner's go as a turn begins; one's own last until the command comes to this
            // player, who then walks them.
            if (e is TurnBeganEvent tb)
            {
                var mine = tb.Team == Teams.Xcom && (tb.Command ?? control.MySlot) == control.MySlot;
                control.PlansAtTurn(tb.Team == Teams.Xcom, mine);
                if (mine) planDue = true;
            }
            else if (e is CommandPassedEvent cp && cp.To == control.MySlot) planDue = true;
            else if (e is MovedEvent || e is DiedEvent || e is EvacuatedEvent) control.DropPlan(UnitOf(e));
            // A door, a wall, a floor changed: the map rebuilds those tiles and what stood on them (Arte's MapView.Rebuild);
            // a siege's door or barricade struck, its wear (the bags knocked off, MapBuilder.DrawSiege); a search, what was
            // searched as it stays (mapView.ts searchedSeen: the furniture open, the crate, the boot, the shop's shelves).
            // A copy: the map keeps the state it drew to tell the next change, and the events change View in place (with
            // View itself, from the second change on it compared View with View and drew nothing).
            if (e is TileChangedEvent || e is FloorBrokenEvent || e is SmashedEvent || e is SearchedEvent || e is RoomSearchedEvent) Bastion.Client.Map.MapView.Current?.Rebuild(Events.CloneState(V));
            See();
            ShowSeen();
            control.Refresh();
            await AnnounceSighted(e, by, before, epoch ?? (control.Rig != null ? control.Rig.Epoch : 0));
            // Let the player see the outcome of their own action before the pick moves on.
            if (by == control.MySlot && !(e is NoiseEvent)) holdAfter = HoldAfter(e);
            if (by == control.MySlot && myPending > 0 && --myPending == 0)
            {
                await Wait(holdAfter);
                control.Rig?.RestoreZoom();
                control.Refresh();
            }
            if (e is TurnBeganEvent began)
            {
                lastActor = null;
                groupFrame = null;
                // The space bar hurries the enemy's turn only.
                if (began.Team == Teams.Xcom) hurry = false;
            }
        }

        /// <summary>
        /// Enemies that have just come into the squad's sight (`before`: who was in sight until this event) get a callout
        /// over them and a line in the log, so nobody «appears out of nowhere» (game.ts announceSighted; playtest): «A LA
        /// VISTA» a group that hasn't noticed the squad (the chance of an ambush), «¡APARECE UN …!» one that has. In the
        /// enemy's turn the camera holds on the first a moment, unless the player took it during the event (`epoch`);
        /// after one's own action a short pause; the partner's get the callout alone. Contacts, arrivals and the risen
        /// have their own show.
        /// </summary>
        async Awaitable AnnounceSighted(GameEvent e, int? by, HashSet<string> before, int epoch)
        {
            if (e is PodActivatedEvent || e is SpawnedEvent || e is ReanimatedEvent || e is MissionEndedEvent) return;
            var v = V;
            var fresh = v.Units.Where(a => a.Alive && a.Team == Teams.Alien && !a.Objective && control.Visible(a) && !before.Contains(a.Id)).ToList();
            if (fresh.Count == 0) return;
            bool Alert(Unit a) => v.Pods.FirstOrDefault(p => p.Id == a.PodId)?.Active == true;
            foreach (var a in fresh)
            {
                var name = Content.Templates[a.Template].Name.ToUpperInvariant();
                Float(Chest(a.Id), Alert(a) ? $"¡APARECE UN {name}!" : $"A LA VISTA: {name}", "callout");
            }
            var names = string.Join(", ", fresh.Select(a => a.Name));
            // A group that hasn't noticed the squad yet: the chance to set up an ambush.
            var unaware = !fresh.Any(Alert);
            Log(unaware ? $"A la vista: {names}{(v.Concealed ? " (aún no os han visto)" : "")}" : fresh.Count == 1 ? $"¡Aparece {AnEnemy(fresh[0])}!" : $"¡Aparecen: {names}!", "#ff6b5a");
            if (v.ActiveTeam == Teams.Alien)
            {
                if (control.Rig != null && control.Rig.Epoch == epoch) control.Rig.Focus(Feet(fresh[0].Id));
                await Wait(0.9f);
            }
            else if (by == control.MySlot) await Wait(0.5f);
        }

        /// <summary>The squad's sight of the ground, again (game.ts refreshView).</summary>
        void See()
        {
            sight = Los.TeamVisibility(V, Teams.Xcom);
        }

        // ------------------------------------------------------- helpers

        Unit Unit(string id) => id == null ? null : V.Units.FirstOrDefault(x => x.Id == id);

        bool ShownToSquad(Unit u) => u.Team == Teams.Xcom || control.Visible(u);

        /// <summary>Whether the squad sees tile (x, y) (mapView.isVisible).</summary>
        bool IsVisible(int x, int y) => sight != null && Grid.InBounds(V, x, y) && sight[y * V.Width + x];

        void Log(string text, string color = null) => Hud?.Log(text, color);

        /// <summary>A unit's feet: its figure's, else its tile's middle.</summary>
        Vector3 Feet(string id) => Units != null && Figures.FeetOf(id) != null ? Units.Feet(id) : Figures.FeetOf(id) is Transform t ? t.position : Unit(id) is Unit u ? Board.TileToWorld(V, u.Pos) : Vector3.zero;

        /// <summary>Its chest, where words rise from and shots go to (unitViews.ts chestPos).</summary>
        Vector3 Chest(string id) => Feet(id) + Vector3.up * 1.25f;

        Vector3 Head(string id) => Units != null ? Units.Head(id) : Feet(id) + Vector3.up * 1.75f;

        void Float(Vector3 at, string text, string kind, string iconId = null) => Hud?.Float(at, text, kind, iconId);

        static string Capitalize(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        /// <summary>«un Zombi», «una Alarma» (game.ts anEnemy).</summary>
        static string AnEnemy(Unit u) => u == null ? "un enemigo" : $"{(u.Template == "relay" ? "una" : "un")} {Content.Templates[u.Template].Name}";

        /// <summary>«3 zombis», «un enemigo»: the dead by their name, the living (or a mix) as enemies.</summary>
        static string HeardCount(List<Unit> units)
        {
            var dead = units.All(u => Content.Templates[u.Template].Undead);
            if (units.Count == 1) return dead ? "un zombi" : "un enemigo";
            return $"{units.Count} {(dead ? "zombis" : "enemigos")}";
        }

        string PlayerColor(int slot) => control.Players.FirstOrDefault(p => p.Slot == slot)?.Color ?? Content.PlayerColors[slot];
        string PlayerName(int? slot) => control.Players.FirstOrDefault(p => p.Slot == slot)?.Name ?? "el otro jugador";

        Awaitable Wait(float seconds) => Awaitable.WaitForSecondsAsync(seconds, stop);

        /// <summary>Something awaited that the event doesn't wait for (an animation that ends on its own, a bullet's sparks).</summary>
        static async void Loose(Awaitable a)
        {
            try
            {
                await a;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception err)
            {
                Debug.LogException(err);
            }
        }

        async Awaitable Later(float seconds, Action then)
        {
            await Wait(seconds);
            then();
        }

        static async Awaitable Then(Awaitable first, Action then)
        {
            try
            {
                await first;
            }
            finally
            {
                then();
            }
        }

        /// <summary>A bullet, and its sparks where it lands.</summary>
        async Awaitable BulletThen(Vector3 from, Vector3 to, Color colour, float speed, bool heavy, Color sparks, int count)
        {
            await Effects.Bullet(from, to, colour, speed, heavy, stop);
            Effects.Sparks(to, sparks, count);
        }

        /// <summary>Where a missed round ends up: past the target, off to one side (game.ts missPoint).</summary>
        static Vector3 MissPoint(Vector3 from, Vector3 chest, int i, float spread = 1)
        {
            var dir = chest - from;
            dir.y = 0;
            dir.Normalize();
            var side = new Vector3(-dir.z, 0, dir.x);
            var p = chest + side * ((i % 2 == 1 ? 1 : -1) * (0.45f + (float)dice.NextDouble() * 0.45f) * spread * Board.Tile) + dir * ((2 + (float)dice.NextDouble() * 2) * Board.Tile);
            p.y = chest.y - 1.25f + (0.25f + (float)dice.NextDouble() * 1.3f) * Board.Tile;
            return p;
        }

        static Vector3 Jitter(float amount) => new Vector3((float)(dice.NextDouble() - 0.5), (float)(dice.NextDouble() - 0.5), (float)(dice.NextDouble() - 0.5)) * (amount * Board.Tile);

        /// <summary>The soldier of ours nearest a tile, where a woken enemy turns to look.</summary>
        Vector3 ClosestSoldier(Pos p)
        {
            var best = V.Units.Where(u => u.Alive && u.Team == Teams.Xcom).OrderBy(u => Grid.Distance(u.Pos, p)).FirstOrDefault();
            return best != null ? Feet(best.Id) : Board.TileToWorld(V, p);
        }

        /// <summary>The abilities whose name rises over the one using it (game.ts SHOUTED).</summary>
        static readonly HashSet<string> Shouted = new HashSet<string> { AbilityIds.RunAndGun, AbilityIds.RapidFire, AbilityIds.ChainShot, AbilityIds.Rupture, AbilityIds.AimedShot, AbilityIds.LightningHands, AbilityIds.Faceoff, AbilityIds.Restoration };

        // ------------------------------------------------------- play

        /// <summary>
        /// One event seen (game.ts play), on View as it was before it. The camera goes to it (`look`) when it is this
        /// player's own action or the enemy's turn, and the player hasn't moved the camera since (Rig.Epoch).
        /// </summary>
        async Awaitable Play(GameEvent e, int? by, int epoch)
        {
            // Right after the squad's turn begins come the bites' tolls (a `damaged` for each bitten, and a fall).
            biteTick = e is TurnBeganEvent tb ? tb.Team == Teams.Xcom : biteTick && (e is DamagedEvent || e is DiedEvent);
            if (by != null && by != control.MySlot) NotePartner(e, by.Value);
            var v = V;
            var alienPhase = v.ActiveTeam == Teams.Alien;
            var cinematic = by == control.MySlot || alienPhase;
            var rig = control.Rig;
            var at = alienPhase && rig != null ? rig.Epoch : epoch;
            bool Cam() => cinematic && rig != null && rig.Epoch == at;
            void Look(Vector3 p)
            {
                if (Cam()) rig.Focus(p);
            }
            var units = Units;
            var fx = Effects;

            switch (e)
            {
                case MovedEvent m:
                {
                    var u = Unit(m.Unit);
                    // Out of the fog (playtest 20/10 #10): shown from the step it takes into a tile the squad sees.
                    var into = ShownToSquad(u) ? 0 : m.Path.FindIndex(p => IsVisible(p.X, p.Y));
                    if (into < 0) return;
                    if (units == null)
                    {
                        if (!grouped) Look(Feet(u.Id));
                        await Wait(Math.Min(1.2f, 0.12f * m.Path.Count));
                        return;
                    }
                    // Up to there, its shade in the fog (Ana, 23/10 #26): dark, itself as it steps into sight.
                    if (into == 0) units.ForceVisible(u.Id);
                    else units.Shade(u.Id, true);
                    // In a group of the dead, the framing is the group's, they walk a little quicker, and each stands where its
                    // plan says so nobody walks through anybody. The squad, at the pace Ajustes sets (only how it looks).
                    WalkPlan.Plan plan = null;
                    if (grouped) groupPlans?.TryGetValue(u.Id, out plan);
                    var pace = grouped ? WalkersPace * (plan?.Haste ?? 1) : u.Team == Teams.Xcom ? WalkSpeed : 1;
                    var steps = units.StepSeconds(u.Id, u.Pos, m.Path, m.Ap, pace);
                    var reveal = 0f;
                    for (var k = 0; k < into && k < steps.Length; k++) reveal += steps[k] + (plan != null && k < plan.Waits.Length ? plan.Waits[k] : 0);
                    // Every other tile reached, a step heard (sfx.ts step), the shade in the fog's too.
                    var stepped = 0;
                    Action<Vector3> step = foot =>
                    {
                        if (stepped++ % 2 == 0) control.Sfx.Step(u, foot);
                    };
                    var walk = plan != null
                        ? units.MoveAlong(u.Id, m.Path, m.Ap, pace, plan.Waits, plan.Passes.Skip(1).ToList(), step, stop)
                        : units.MoveAlong(u.Id, m.Path, m.Ap, pace, step, stop);
                    var start = Time.time;
                    var shown = into == 0;
                    try
                    {
                        // The camera goes along with the walker frame by frame, not tile by tile (Arturo, 21/10 #4).
                        while (!walk.GetAwaiter().IsCompleted)
                        {
                            if (!shown && Time.time - start >= reveal)
                            {
                                shown = true;
                                units.ForceVisible(u.Id);
                                units.Shade(u.Id, false);
                            }
                            if (!grouped) Look(units.Feet(u.Id));
                            await Awaitable.NextFrameAsync(stop);
                        }
                        await walk;
                    }
                    finally
                    {
                        units.Shade(u.Id, false);
                    }
                    return;
                }
                case ShotEvent s:
                {
                    // One unseen striking at one we see (Ana, 23/10 #26): its shade for the attack, and on through the meal.
                    var shooter = Unit(s.Unit);
                    var shade = units != null && !ShownToSquad(shooter) && ShownToSquad(Unit(s.Target));
                    if (shade) units.Shade(shooter.Id, true);
                    try
                    {
                        await PlayAttack(s, Cam, Look);
                    }
                    finally
                    {
                        if (shade && !(Fells(s) && units.CanFeed(shooter.Id, s.Target))) units.Shade(shooter.Id, false);
                    }
                    return;
                }
                case DamagedEvent d:
                {
                    var target = Unit(d.Unit);
                    // The bite's toll as the squad's turn begins (playtest 15/10 #13): said as such, not as a hit.
                    var bite = biteTick && target.Infected == true;
                    var shot = bite ? null : lastShot?.target == d.Unit ? lastShot?.result : null;
                    var crit = shot == "crit";
                    // Enemies fighting each other (18/10 #47): only what the squad sees of it.
                    var from = Unit(lastAttacker);
                    var feud = target.Team == Teams.Alien && from?.Team == Teams.Alien;
                    if (feud && !ShownToSquad(target)) return;
                    if (d.Amount > 0) Log(bite ? Lore.BiteLossLine(target.Name) : $"{target.Name} recibe {d.Amount} de daño{(crit ? " (crítico)" : "")}", feud ? "#e8d8c0" : target.Team == Teams.Alien ? "#54d1ff" : "#ff6b5a");
                    if (!ShownToSquad(target)) return;
                    var chest = Chest(target.Id);
                    if (d.Amount > 0) Float(chest, bite ? Lore.BiteFloat : crit ? $"CRÍTICO −{d.Amount}" : shot == "graze" ? $"ROZADURA −{d.Amount}" : $"−{d.Amount}", crit ? "crit" : "damage");
                    // One of ours hurt short of dying (the death has its own): a scream if the dead bit them.
                    if (d.Amount > 0 && target.Team == Teams.Xcom && target.Hp > d.Amount) control.Sfx.Hurt(target, chest, !bite && from != null && Content.Templates[from.Template].Undead);
                    if (d.Mitigated > 0)
                    {
                        Float(chest - Vector3.up * 0.45f, $"BLINDAJE −{d.Mitigated}", "info");
                        control.Sfx.Impact("armor", chest);
                    }
                    if (d.Shred > 0) Float(chest + Vector3.up * 0.45f, "BLINDAJE DAÑADO", "warn");
                    if (crit && Cam()) rig.Shake(0.18f * Board.Tile, 0.3f);
                    if (units != null) await units.HitReact(target.Id, crit ? 1.5f : d.Amount > 0 ? 1 : 0.4f, stop);
                    else await Wait(0.4f);
                    return;
                }
                case DiedEvent died:
                {
                    var u = Unit(died.Unit);
                    var killer = Unit(died.By);
                    if (u.Team == Teams.Alien && killer?.Team == Teams.Alien)
                    {
                        if (!ShownToSquad(u) && !ShownToSquad(killer)) return;
                        Log($"{Capitalize(AnEnemy(killer))} acaba con {AnEnemy(u)}.", "#e8d8c0");
                    }
                    else Log(u.Team == Teams.Xcom ? $"{u.Name} ha caído en combate" : $"{u.Name} eliminado", u.Team == Teams.Xcom ? "#ff6b5a" : "#54d1ff");
                    if (!ShownToSquad(u)) return;
                    // One of ours falling with no attack framed first (a bite's toll, a blast): close on them (#21).
                    if (u.Team == Teams.Xcom && killCam == null && Cam() && DeathShot(u, killer) is (Vector3, Vector3) close)
                    {
                        StartKillCam(u.Id, close);
                        await Wait(0.35f);
                    }
                    control.Sfx.Death(u, Chest(u.Id), killer);
                    if (units == null)
                    {
                        await Wait(0.7f);
                        return;
                    }
                    // The Hinchado bursts as it falls: the blast (the next event) doesn't wait for the body.
                    if (Content.Templates[u.Template].Abilities.Contains(AbilityIds.Burst))
                    {
                        Loose(units.Die(u.Id, stop));
                        await Wait(0.15f);
                        return;
                    }
                    // One of ours killed by one of the dead at hand: it goes down with the body and eats (#21).
                    var fall = units.Die(u.Id, stop);
                    var eat = killer != null && units.CanFeed(killer.Id, u.Id) ? units.Feed(killer.Id, u.Id, null, stop) : null;
                    await fall;
                    // One of ours seen falling close: the camera comes down to the body on the ground, and whoever is over it.
                    if (u.Team == Teams.Xcom && killCam == u.Id && Cam() && DeathShot(u, killer, true) is (Vector3, Vector3) low) rig.CloseShot(low.Item1, low.Item2);
                    if (eat != null)
                    {
                        await eat;
                        killHold = FeedHold;
                    }
                    if (killer != null && !ShownToSquad(killer)) units.Shade(killer.Id, false);
                    await Wait(0.2f);
                    return;
                }
                case StunnedEvent st:
                {
                    var u = Unit(st.Unit);
                    Log($"{u.Name} queda {UnitWords.Agree(u, "aturdido")}", "#6ad8ff");
                    if (!ShownToSquad(u)) return;
                    fx?.Sparks(Chest(u.Id), Fx.Hex(0x6ad8ff), 14);
                    control.Sfx.Stun(Chest(u.Id));
                    Float(Chest(u.Id), UnitWords.Agree(u, "ATURDIDO"), "warn");
                    await Wait(0.5f);
                    return;
                }
                case BarFilledEvent bar:
                {
                    var u = Unit(bar.Unit);
                    Log(Lore.BarLine(u.Name, bar.Skill, new[] { bar.Growth }), "#6dff9a");
                    if (!ShownToSquad(u)) return;
                    Look(Feet(u.Id));
                    Float(Chest(u.Id), Lore.BarFillFloat(bar.Skill, bar.Growth), "bar");
                    await Wait(1.1f);
                    return;
                }
                case StoneThrownEvent stone:
                {
                    var u = Unit(stone.Unit);
                    if (!ShownToSquad(u) && !IsVisible(stone.Target.X, stone.Target.Y)) return;
                    var to = Board.TileToWorld(v, stone.Target);
                    var struck = Unit(stone.Hit);
                    Look(Vector3.Lerp(Feet(u.Id), to, 0.5f));
                    if (units == null || fx == null)
                    {
                        await Wait(0.6f);
                        return;
                    }
                    var hand = await units.TossStone(u.Id, to, stop);
                    control.Sfx.StoneThrow(hand);
                    await fx.Stone(hand, struck != null ? Chest(struck.Id) : to, struck != null, stop);
                    control.Sfx.StoneLand(to, struck != null);
                    return;
                }
                case OverwatchEvent _:
                case HunkerEvent _:
                {
                    var u = Unit(e is OverwatchEvent ow ? ow.Unit : ((HunkerEvent)e).Unit);
                    if (!ShownToSquad(u)) return;
                    Look(Feet(u.Id));
                    // The blade's watch (docs/FASE10.md §4) is said as such: on guard, not watching with a gun.
                    var text = e is HunkerEvent ? "CUBRIÉNDOSE" : ((OverwatchEvent)e).KillZone ? "ZONA LETAL" : ((OverwatchEvent)e).Melee == true ? "HOJA EN ALTO" : "VIGILANCIA";
                    Float(Chest(u.Id), text, "info");
                    if (e is HunkerEvent) control.Sfx.Hunker(Feet(u.Id));
                    else control.Sfx.Overwatch(Chest(u.Id));
                    await Wait(0.45f);
                    return;
                }
                case AbilityUsedEvent used:
                {
                    lastUsed = (used.Unit, used.Ability);
                    var u = Unit(used.Unit);
                    if (used.Ability == AbilityIds.Mindspin && ShownToSquad(u))
                    {
                        // The Chillón's scream (docs/FASE2.md): what it does to the soldier comes with the `psi` that follows.
                        Look(Feet(u.Id));
                        if (units != null)
                        {
                            units.ForceVisible(u.Id);
                            Loose(units.Scream(u.Id, stop));
                        }
                        fx?.Scream(Head(u.Id));
                        Float(Chest(u.Id), "¡GRITA!", "callout");
                        control.Sfx.Psi(Chest(u.Id));
                        Log($"¡{Capitalize(AnEnemy(u))} grita!", "#e8d8c0");
                        await Wait(Cam() ? 0.7f : 0.3f);
                        return;
                    }
                    if (!Shouted.Contains(used.Ability) || !ShownToSquad(u)) return;
                    Look(Feet(u.Id));
                    Float(Chest(u.Id), Content.Abilities[used.Ability].Name.ToUpperInvariant(), "alert");
                    Log($"{u.Name}: {Content.Abilities[used.Ability].Name}");
                    await Wait(0.45f);
                    return;
                }
                case CleansedEvent c:
                {
                    var target = Unit(c.Target);
                    fx?.Heal(Chest(target.Id));
                    Float(Chest(target.Id), "RECUPERADO", "heal");
                    Log($"{target.Name} se recupera", "#6dff9a");
                    await Wait(0.3f);
                    return;
                }
                case RefundEvent r:
                {
                    var u = Unit(r.Unit);
                    Float(Chest(u.Id), r.Reason == "implacable" ? "IMPLACABLE · +1 ACCIÓN" : "MUERTE DESDE ARRIBA · +1 ACCIÓN", "heal");
                    Log($"{u.Name} recupera una acción", "#6dff9a");
                    await Wait(0.4f);
                    return;
                }
                case SuppressedEvent sup:
                {
                    var u = Unit(sup.Unit);
                    var target = Unit(sup.Target);
                    Look(Vector3.Lerp(Feet(u.Id), Feet(target.Id), 0.5f));
                    var chest = Chest(target.Id);
                    if (units != null && fx != null)
                    {
                        var aim = await units.RaiseWeapon(u.Id, chest, false, stop);
                        var blue = Fx.Hex(0x9fe8ff);
                        for (var i = 0; i < 8; i++)
                        {
                            var from = units.Muzzle(u.Id);
                            units.Recoil(u.Id);
                            fx.MuzzleFlash(from, blue);
                            control.Sfx.Shot(u, Content.AttackWeapon(u).Id, from);
                            Loose(BulletThen(from, MissPoint(from, chest, i, 1.2f), blue, 60, false, Fx.Hex(0xffd28a), 3));
                            await Wait(0.07f);
                        }
                        Loose(Later(0.8f, () => Loose(units.LowerWeapon(u.Id, aim, stop))));
                    }
                    else await Wait(0.6f);
                    Float(chest, UnitWords.Agree(target, "SUPRIMIDO"), "warn");
                    Log($"{u.Name} suprime a {target.Name}", "#ffc93a");
                    return;
                }
                case DisorientedEvent dis:
                {
                    var u = Unit(dis.Unit);
                    if (ShownToSquad(u)) Float(Chest(u.Id), UnitWords.Agree(u, "DESORIENTADO"), "crit");
                    Log($"{u.Name} queda {UnitWords.Agree(u, "desorientado")}", "#e8d8c0");
                    await Wait(0.15f);
                    return;
                }
                case MarkedEvent mk:
                    if (Unit(mk.Unit) is Unit marked && ShownToSquad(marked)) Float(Chest(marked.Id) + Vector3.up * 0.4f, "MARCADO", "info");
                    return;
                case RupturedEvent rp:
                    if (Unit(rp.Unit) is Unit ruptured && ShownToSquad(ruptured)) Float(Chest(ruptured.Id) + Vector3.up * 0.4f, "RUPTURA", "warn");
                    return;
                case PsiEvent psi:
                {
                    var u = Unit(psi.Unit);
                    var target = Unit(psi.Target);
                    Look(Vector3.Lerp(Feet(u.Id), Feet(target.Id), 0.5f));
                    if (units != null)
                    {
                        units.ForceVisible(u.Id);
                        units.Face(u.Id, Feet(target.Id));
                    }
                    // No second shriek: the scream ('mindspin') was just heard; this is what it does to the soldier.
                    if (fx != null) await fx.PsiBeam(Chest(u.Id) + Vector3.up * 0.35f, Chest(target.Id), null, stop);
                    else await Wait(0.4f);
                    var text = psi.Effect == "disoriented" ? UnitWords.Agree(target, "DESORIENTADO") : psi.Effect == "panicked" ? "PÁNICO" : psi.Effect == "controlled" ? "FUERA DE SÍ" : "RESISTE";
                    Float(Chest(target.Id), text, psi.Success ? "crit" : "info");
                    Log(psi.Success ? $"{target.Name}: {text.ToLowerInvariant()} ({AbilityTexts.Number(psi.Chance)}%)" : $"{target.Name} resiste el grito ({AbilityTexts.Number(psi.Chance)}%)", psi.Success ? "#e8d8c0" : null);
                    if (psi.Effect == "controlled") Hud?.Banner("¡FUERA DE SÍ!", "alien");
                    await Wait(0.7f);
                    return;
                }
                case ReleasedEvent rel:
                {
                    var u = Unit(rel.Unit);
                    fx?.Sparks(Chest(u.Id), Fx.Hex(0xf0e4d4), 14);
                    Float(Chest(u.Id), "LIBERADO", "heal");
                    Log($"{u.Name} vuelve en sí", "#6dff9a");
                    await Wait(0.5f);
                    return;
                }
                case ReanimatedEvent rise:
                {
                    // No `unit`: an infected soldier rising by itself (docs/FASE3.md), with no Chillón to shriek.
                    var u = Unit(rise.Unit);
                    var corpse = Unit(rise.Corpse);
                    // Out of sight it costs no time (docs/UX-RITMO.md): the body is just up when it comes into view.
                    if (!IsVisible(corpse.Pos.X, corpse.Pos.Y) && !(u != null && ShownToSquad(u))) return;
                    var place = Board.TileToWorld(v, corpse.Pos, 0.75f);
                    Look(place);
                    if (u != null) units?.ForceVisible(u.Id);
                    // A fallen soldier of ours: the camera settles on the body a moment before it moves.
                    if (u == null && Cam()) await Wait(0.6f);
                    if (u != null) control.Sfx.Psi(Chest(u.Id));
                    control.Sfx.Moan(place);
                    if (u != null && fx != null) await fx.PsiBeam(Chest(u.Id) + Vector3.up * 0.35f, place, null, stop);
                    fx?.EvacBeam(Board.TileToWorld(v, corpse.Pos));
                    if (units != null) await units.Reanimate(corpse.Id, rise.Zombie.Id, stop);
                    else await Wait(0.6f);
                    Float(Board.TileToWorld(v, rise.Zombie.Pos, 0.75f), "SE LEVANTA", "crit");
                    Log(u != null ? $"El grito de un Chillón levanta el cuerpo de {corpse.Name}" : $"Mordieron a {corpse.Name}: se levanta", "#e8d8c0");
                    // The first of the squad to rise gets the stamp.
                    if (u == null && !risenSeen)
                    {
                        risenSeen = true;
                        Hud?.Banner("¡SE LEVANTA!", "alien", $"Mordieron a {corpse.Name}: ya no es de los vuestros");
                    }
                    await Wait(u != null ? 0.6f : 1f);
                    return;
                }
                case InfectedEvent inf:
                {
                    var u = Unit(inf.Unit);
                    Log($"Han mordido a {u.Name}", "#ff6b5a");
                    if (!ShownToSquad(u)) return;
                    Float(Chest(u.Id) + Vector3.up * 0.5f, "¡MORDISCO!", "alert");
                    await Wait(0.5f);
                    return;
                }
                case ReloadEvent rl:
                {
                    var u = Unit(rl.Unit);
                    if (!ShownToSquad(u)) return;
                    Look(Feet(u.Id));
                    Float(Chest(u.Id), "RECARGANDO", "info");
                    control.Sfx.Reload(Chest(u.Id));
                    if (units != null) await units.Reload(u.Id, stop);
                    else await Wait(0.8f);
                    return;
                }
                case ExplosiveEvent ex:
                    await PlayExplosive(ex, Cam, Look);
                    return;
                case HealedEvent _:
                case AidedEvent _:
                {
                    var healed = e as HealedEvent;
                    var aided = e as AidedEvent;
                    var u = Unit(healed?.Unit ?? aided.Unit);
                    var target = Unit(healed?.Target ?? aided.Target);
                    var amount = healed?.Amount ?? aided.Amount;
                    // A medicine from the backpack (docs/FASE8.md §C), like Restoration, heals at once.
                    var medicine = healed != null && medicineHeal;
                    if (medicine) medicineHeal = false;
                    if (healed != null && (medicine || (lastUsed?.unit == u.Id && lastUsed?.ability == AbilityIds.Restoration)))
                    {
                        fx?.Heal(Chest(target.Id));
                        control.Sfx.Heal(Chest(target.Id));
                        Float(Chest(target.Id), $"+{amount}", "heal");
                        if (!medicine) Log($"{target.Name} recupera {amount}", "#6dff9a");
                        await Wait(0.2f);
                        return;
                    }
                    Look(Vector3.Lerp(Feet(u.Id), Feet(target.Id), 0.5f));
                    await Wait(0.3f);
                    if (healed != null)
                    {
                        fx?.Heal(Chest(target.Id));
                        control.Sfx.Heal(Chest(target.Id));
                        Float(Chest(target.Id), $"+{amount}", "heal");
                        // One who treats themself «se cura» (Lore, 26/10).
                        Log(target.Id == u.Id ? $"{u.Name} se cura {amount}" : $"{u.Name} cura {amount} a {target.Name}", "#6dff9a");
                    }
                    else
                    {
                        fx?.Shield(Chest(target.Id));
                        control.Sfx.Shield(Chest(target.Id));
                        Float(Chest(target.Id), $"+{amount} DEFENSA", "info");
                        Log($"{u.Name} protege a {target.Name}", "#54d1ff");
                    }
                    await Wait(0.45f);
                    return;
                }
                case RoomSearchedEvent room:
                {
                    var u = Unit(room.Unit);
                    var place = Feet(u.Id);
                    Look(place);
                    control.Sfx.Search(place);
                    if (units != null) await units.Rummage(u.Id, place + new Vector3(0.9f, 0.6f, 0), stop);
                    else await Wait(0.6f);
                    Float(Chest(u.Id), Lore.FindLabel(room.Loot), room.Loot?.Weapon != null ? "callout" : room.Loot != null ? "heal" : "info");
                    if (room.Loot != null) control.Sfx.Found(place);
                    // The seed comes from the event, so both players read the same line.
                    var spot = Looting.RoomAt(v, u.Pos)?.Spot ?? "home";
                    Log(Lore.RoomSearchLine(u.Name, spot, room.Loot, room.Room * 7 + v.Turn), room.Loot?.Weapon != null ? "#ffd23a" : room.Loot != null ? "#6dff9a" : null);
                    await Wait(0.5f);
                    return;
                }
                case LootDroppedEvent dropped:
                {
                    var u = Unit(dropped.Unit);
                    var loot = u.Backpack != null && dropped.Index < u.Backpack.Count ? u.Backpack[dropped.Index] : null;
                    if (loot != null) Log(Lore.LootDroppedLine(u.Name, loot));
                    await Wait(0.15f);
                    return;
                }
                case LootTakenEvent taken:
                {
                    var u = Unit(taken.Unit);
                    var left = v.Dropped?.FirstOrDefault(d => d.Id == taken.Id);
                    if (left != null)
                    {
                        Float(Chest(u.Id), Lore.FindLabel(left.Loot), "info");
                        // A fallen soldier's (#49): whose it was, in Lore's words.
                        var fallen = Unit(left.From);
                        Log(fallen != null ? Lore.FallenPackTakenLine(u.Name.Split(' ')[0], fallen.Name, Lore.LootName(left.Loot)) : Lore.LootTakenLine(u.Name, left.Loot), "#6dff9a");
                    }
                    await Wait(0.3f);
                    return;
                }
                case LootGivenEvent given:
                {
                    var u = Unit(given.Unit);
                    var to = Unit(given.To);
                    var loot = u.Backpack != null && given.Index < u.Backpack.Count ? u.Backpack[given.Index] : null;
                    units?.Face(u.Id, Feet(to.Id));
                    units?.Face(to.Id, Feet(u.Id));
                    Look(Vector3.Lerp(Feet(u.Id), Feet(to.Id), 0.5f));
                    if (loot != null)
                    {
                        Float(Chest(to.Id), Lore.FindLabel(loot), "info");
                        Log(Lore.LootGivenLine(u.Name, to.Name, loot), "#6dff9a");
                    }
                    await Wait(0.6f);
                    return;
                }
                case MedicineUsedEvent med:
                {
                    var u = Unit(med.Unit);
                    var target = Unit(med.Target);
                    if (target.Id != u.Id) units?.Face(u.Id, Feet(target.Id));
                    Look(Feet(target.Id));
                    // One line for the whole cure (Lore): what the `healed` and `cured` that follow it do.
                    var after = queue.Take(3).Select(q => q.e).ToList();
                    var healed = after.OfType<HealedEvent>().FirstOrDefault(x => x.Target == target.Id)?.Amount ?? 0;
                    var cured = after.OfType<CuredEvent>().Any(x => x.Unit == target.Id);
                    Log(Lore.MedicineLine(u.Name, target.Id == u.Id ? null : target.Name, healed, cured), "#6dff9a");
                    medicineHeal = true;
                    medicineCure = cured;
                    await Wait(0.3f);
                    return;
                }
                case CuredEvent cure:
                {
                    var u = Unit(cure.Unit);
                    Float(Chest(u.Id), Lore.CuredFloat, "heal");
                    if (!medicineCure) Log($"{u.Name}: {Lore.CuredFloat.ToLowerInvariant()}", "#6dff9a");
                    medicineCure = false;
                    await Wait(0.5f);
                    return;
                }
                case WeaponEquippedEvent we:
                {
                    var u = Unit(we.Unit);
                    var loot = u.Backpack != null && we.Index < u.Backpack.Count ? u.Backpack[we.Index] : null;
                    Look(Feet(u.Id));
                    if (loot?.Weapon != null)
                    {
                        Float(Chest(u.Id), Content.FoundWeapons[loot.Weapon].Name.ToUpperInvariant(), "callout", Ui.WeaponIcon(Content.FoundWeapons[loot.Weapon].Weapon));
                        Log(Lore.WeaponEquippedLine(u.Name, loot.Weapon), "#6dff9a");
                    }
                    control.Sfx.Reload(Feet(u.Id));
                    // The swap itself is seen as the figures sync after it.
                    await Wait(1f);
                    return;
                }
                case VestWornEvent vest:
                {
                    var u = Unit(vest.Unit);
                    var item = u.Backpack != null && vest.Index < u.Backpack.Count ? u.Backpack[vest.Index]?.Item : null;
                    Look(Feet(u.Id));
                    if (item != null)
                    {
                        var hp = Content.Items[item].Hp ?? 0;
                        Float(Chest(u.Id), $"+{hp} SALUD", "heal");
                        Log($"{u.Name} se pone {Lore.ItemFinds[item]}: +{hp} de salud.", "#6dff9a");
                    }
                    await Wait(0.6f);
                    return;
                }
                case BeltEquippedEvent belt:
                {
                    var u = Unit(belt.Unit);
                    var item = u.Backpack != null && belt.Index < u.Backpack.Count ? u.Backpack[belt.Index]?.Item : null;
                    if (item == null) return;
                    Log(Lore.BeltLine(u.Name, item), "#6dff9a");
                    if (!ShownToSquad(u)) return;
                    Look(Feet(u.Id));
                    Float(Chest(u.Id), Lore.BeltText.Float, "info");
                    await Wait(0.5f);
                    return;
                }
                case EvacCalledEvent call:
                    // The answer waits in the middle, with its clock (Cards), and on the bar.
                    Log(Lore.EvacAsk(PlayerName(call.Slot), Situation(v)).Title, PlayerColor(call.Slot));
                    return;
                case EvacRefusedEvent refused:
                {
                    var line = Lore.EvacRefusedLine(PlayerName(refused.Slot));
                    Log(line, PlayerColor(refused.Slot));
                    if (refused.Slot != control.MySlot) Hud?.Toast(line);
                    return;
                }
                case AbandonCalledEvent ab:
                    Log(Lore.AbandonAsk(PlayerName(ab.Slot)), PlayerColor(ab.Slot));
                    return;
                case AbandonRefusedEvent abr:
                {
                    var line = Lore.AbandonRefusedLine(PlayerName(abr.Slot));
                    Log(line, PlayerColor(abr.Slot));
                    if (abr.Slot != control.MySlot) Hud?.Toast(line);
                    return;
                }
                case EvacDepartedEvent _:
                {
                    var aboard = v.Units.Where(u => u.Alive && u.Team == Teams.Xcom && Abilities.InEvacZone(v, u.Pos)).ToList();
                    // Leaving in the enemy turn is leaving on time (playtest 18/10 #8): nobody asked for it.
                    if (v.ActiveTeam == Teams.Alien)
                    {
                        departedOnTime = true;
                        Log(Lore.TimeText.Departs, "#ff6b5a");
                    }
                    Log(Lore.EvacDepartLine(aboard.Select(u => u.Name).ToList()), "#6dff9a");
                    // The camera to La Lata (else the zone), everyone in it boards (the `evacuated` that follow), and then it
                    // pulls away (Arte's MapView.LataDeparts).
                    var zone = v.Mission.EvacZone;
                    var lata = Bastion.Client.Map.MapView.Current?.LataAt;
                    if (rig != null && (lata != null || zone.Count > 0))
                    {
                        rig.Focus(lata ?? Board.TileToWorld(v, new Pos { X = zone[zone.Count / 2].X, Y = zone[zone.Count / 2].Y }));
                        rig.CinematicZoom(16 * Board.Tile);
                    }
                    await Wait(0.5f);
                    if (!NextBoards()) await LataLeaves();
                    return;
                }
                case EvacuatedEvent evac:
                {
                    // Into the bus: a puff of dust where they stood, and out of sight at once (playtest 16/10 #2).
                    fx?.Dust(Feet(evac.Unit), 4);
                    await Wait(0.18f);
                    units?.Board(evac.Unit);
                    // Their flag goes with them (the HUD's, not the figure's); after the last one aboard, La Lata goes
                    // (game.ts busLeaves), before the boarding is on the view, as the 0.10.1: their sight holds the fog off.
                    control.Aboard.Add(evac.Unit);
                    control.Refresh();
                    if (!NextBoards()) await LataLeaves();
                    return;
                }
                case LeftBehindEvent lb:
                {
                    var u = Unit(lb.Unit);
                    Float(Chest(u.Id), "SE QUEDA FUERA", "callout");
                    Log(Lore.LeftBehindLine(u.Name), "#ff8a78");
                    await Wait(0.6f);
                    return;
                }
                case ItemPickedEvent picked:
                {
                    var u = Unit(picked.Unit);
                    var item = RecoveryItem();
                    Look(Feet(u.Id));
                    Log(Lore.ItemTakenLine(u.Name, item), "#6dff9a");
                    Float(Chest(u.Id), Lore.ItemTakenFloat(item), "callout");
                    control.Sfx.Data();
                    await Wait(0.9f);
                    return;
                }
                case ItemDroppedEvent itemDropped:
                {
                    var item = RecoveryItem();
                    Float(Board.TileToWorld(v, itemDropped.Pos, 1.5f), Lore.ItemDroppedFloat(item), "warn");
                    // Down with a broken floor, RubbleText.Item has said it already.
                    if (!itemFell) Log(Lore.ItemDroppedLine(item), "#ffc93a");
                    itemFell = false;
                    await Wait(0.4f);
                    return;
                }
                case SmashedEvent smashed:
                {
                    var u = Unit(smashed.Unit);
                    var kind = Grid.KindAt(v, smashed.Tile);
                    var door = kind == "door" || kind == "doorOpen";
                    var place = Board.TileToWorld(v, smashed.Tile, door ? 1.65f : 0.9f);
                    var gone = smashed.Left <= 0;
                    // What gives way is known even unseen: a breach in the wall is what matters (its line and marks).
                    if (gone) Log(Lore.GiveWayLine(door ? "door" : "barricade", CombatController.SideOf(v, smashed.Tile)), "#ff6b5a");
                    if (!ShownToSquad(u) && !IsVisible(smashed.Tile.X, smashed.Tile.Y)) return;
                    units?.ForceVisible(u.Id);
                    Look(place);
                    if (units != null) await units.Smash(u.Id, smashed.Tile, null, stop);
                    else await Wait(0.4f);
                    fx?.Smash(place, door ? "door" : "barricade", gone);
                    control.Sfx.Smash(place, door ? "door" : "barricade", gone);
                    Float(place, $"−{smashed.Amount}", "damage");
                    if (gone)
                    {
                        await Wait(0.2f);
                        Float(Board.TileToWorld(v, smashed.Tile, 2.4f), Lore.GiveWayFloat[door ? "door" : "barricade"], "warn");
                    }
                    await Wait(Cam() ? 0.35f : 0.15f);
                    return;
                }
                case TileChangedEvent tc:
                {
                    var before = Grid.KindAt(v, tc.Pos);
                    // A siege's barricade blown away by a grenade, not by blows: a breach all the same, said once (the blows' in
                    // Smashed, which leaves it at 0 before this). A door a grenade opens stays a door: it can be shut again.
                    if (v.Breakables != null && v.Breakables.TryGetValue(Grid.NodeOf(v, tc.Pos), out var held) && held > 0 && tc.Kind != "door" && tc.Kind != "doorOpen" && tc.Kind != "low")
                        Log(Lore.GiveWayLine(CombatController.BreakableOf(v, tc.Pos).kind, CombatController.SideOf(v, tc.Pos)), "#ff6b5a");
                    if (fx == null || !IsVisible(tc.Pos.X, tc.Pos.Y)) return;
                    if (tc.Kind == "doorOpen" && before == "door")
                    {
                        control.Sfx.Door(Board.TileToWorld(v, tc.Pos, Board.Tile));
                        return;
                    }
                    if (tc.Kind == "windowBroken")
                    {
                        fx.Debris(Board.TileToWorld(v, tc.Pos, 1.95f), Fx.Hex(0x9fd8ff), 10);
                        control.Sfx.Glass(Board.TileToWorld(v, tc.Pos, 1.95f));
                        return;
                    }
                    if (before == "door" || before == "doorOpen" || tc.Decor == "sirenCar" || tc.Decor == "part") return;
                    if (before == "wall")
                    {
                        fx.Debris(Board.TileToWorld(v, tc.Pos, 1.8f), Fx.Hex(0x9a8a78), 12);
                        return;
                    }
                    fx.Debris(Board.TileToWorld(v, tc.Pos, before == "high" ? 1.65f : 0.7f), before == "low" ? Fx.Hex(0x8f6c45) : Fx.Hex(0x3f7480), 7);
                    return;
                }
                case HackedEvent hack:
                {
                    var u = Unit(hack.Unit);
                    var terminal = v.Mission.Terminal;
                    if (terminal == null) return;
                    var screen = Board.TileToWorld(v, terminal, 1.95f);
                    Look(Vector3.Lerp(Feet(u.Id), screen, 0.5f));
                    var aim = 0;
                    if (units != null)
                    {
                        units.Face(u.Id, screen);
                        aim = await units.RaiseWeapon(u.Id, screen, false, stop);
                    }
                    control.Sfx.Hack(screen);
                    fx?.Sparks(screen, hack.Success ? Fx.Hex(0x4dff9a) : Fx.Hex(0xff4a3a), 16);
                    await Wait(0.5f);
                    Float(screen, hack.Success ? "ARREGLADO" : "NO ARRANCA", hack.Success ? "heal" : "warn");
                    Log($"{u.Name} {(hack.Success ? "arregla el equipo" : "no consigue arreglarlo")} ({AbilityTexts.Number(hack.Chance)}%)", hack.Success ? "#6dff9a" : "#ffc93a");
                    control.Sfx.Hacked(hack.Success, screen);
                    if (hack.Success) Hud?.Banner("EQUIPO ARREGLADO", "xcom");
                    else
                    {
                        Hud?.Banner("¡ALARMA!", "alien");
                        alarmRaised = true;
                    }
                    if (units != null) Loose(units.LowerWeapon(u.Id, aim, stop));
                    return;
                }
                case VipRescuedEvent vr:
                {
                    var vip = Unit(vr.Unit);
                    Look(Feet(vip.Id));
                    Float(Chest(vip.Id), UnitWords.Agree(vip, "RESCATADO"), "heal");
                    Log($"{vip.Name} se une a la escuadra", "#6dff9a");
                    control.Sfx.Rescue();
                    await Wait(0.6f);
                    return;
                }
                case ReinforcementsIncomingEvent ri:
                {
                    if (ri.R.Horde == true)
                    {
                        var when = ri.R.Turn <= v.Turn ? "al final de este turno enemigo" : "al final del próximo turno enemigo";
                        var alarm = alarmRaised;
                        alarmRaised = false;
                        if (alarm) Hud?.Banner("LA ALARMA SE OYE LEJOS", "alien", $"Llegan muertos por un borde {when}");
                        else Hud?.Banner("MÁS ZOMBIS EN CAMINO", "alien", $"Llegan por un borde {when}");
                        var many = ri.R.Units.Count == 1 ? "Viene un zombi" : $"Vienen {ri.R.Units.Count} zombis";
                        Log(alarm ? $"La alarma se oye lejos. {many} por un borde: llegan {when}." : $"{many} más por un borde: llegan {when}. Cuanto más ruido, antes vienen.", "#ff6b5a");
                        await Wait(alarm ? 1f : 0.6f);
                        return;
                    }
                    if (ri.R.Pos == null) return;
                    Look(Board.TileToWorld(v, ri.R.Pos));
                    Hud?.Banner("VIENEN MÁS", "alien");
                    Log("Una bengala roja marca el sitio: llegan más al final del próximo turno enemigo", "#ff6b5a");
                    control.Sfx.Flare(Board.TileToWorld(v, ri.R.Pos));
                    await Wait(Cam() ? 0.9f : 0.3f);
                    return;
                }
                case SpawnedEvent sp:
                    await PlaySpawned(sp, v, Cam, Look);
                    return;
                case NoiseEvent noise:
                    PlayNoise(noise);
                    return;
                case FloorBrokenEvent fb:
                {
                    if (!IsVisible(fb.Pos.X, fb.Pos.Y)) return;
                    var place = Board.TileToWorld(v, fb.Pos, 0.15f);
                    Look(place);
                    fx?.Debris(place, Fx.Hex(0x8f6c45), 14);
                    if (Cam()) rig.Shake(0.2f * Board.Tile, 0.3f);
                    Log(Lore.RubbleText.Floor, "#ffb070");
                    var item = v.Mission.Item;
                    if (item != null && item.Carrier == null && item.Pos != null && item.Pos.X == fb.Pos.X && item.Pos.Y == fb.Pos.Y && item.Pos.Z == fb.Pos.Z)
                    {
                        Log(Lore.RubbleText.Item, "#ffd23a");
                        itemFell = true;
                    }
                    await Wait(Cam() ? 0.45f : 0.2f);
                    return;
                }
                case FellEvent fell:
                {
                    var u = Unit(fell.Unit);
                    if (!ShownToSquad(u) && !IsVisible(fell.To.X, fell.To.Y)) return;
                    Log(Lore.FallLine(u.Name, Math.Max(1, fell.From.Z - Grid.HeightAt(v, fell.To))), u.Team == Teams.Alien ? "#54d1ff" : "#ff6b5a");
                    Look(Vector3.Lerp(Feet(u.Id), Board.TileToWorld(v, fell.To), 0.5f));
                    Float(Chest(u.Id), Lore.RubbleText.FallFloat, "alert");
                    if (units != null) await units.Fall(u.Id, fell.To, stop);
                    control.Sfx.Land(Board.TileToWorld(v, fell.To));
                    await Wait(Cam() ? 0.35f : 0.15f);
                    return;
                }
                case SearchedEvent searched:
                {
                    var u = Unit(searched.Unit);
                    var place = Board.TileToWorld(v, searched.Tile, 0.9f);
                    var decor = Looting.SearchSpot(v, searched.Tile);
                    Look(Vector3.Lerp(Feet(u.Id), place, 0.5f));
                    control.Sfx.Search(place);
                    if (units != null) await units.Rummage(u.Id, place, stop);
                    else await Wait(0.6f);
                    Float(place + Vector3.up, Lore.FindLabel(searched.Loot), searched.Loot?.Weapon != null ? "callout" : searched.Loot != null ? "heal" : "info",
                        searched.Loot?.Weapon != null ? Ui.WeaponIcon(Content.FoundWeapons[searched.Loot.Weapon].Weapon) : null);
                    if (searched.Loot != null) control.Sfx.Found(place);
                    if (decor != null) Log(Lore.SearchLine(u.Name, decor, searched.Loot, searched.Tile.X * 7 + searched.Tile.Y * 13 + v.Turn), searched.Loot?.Weapon != null ? "#ffd23a" : searched.Loot != null ? "#6dff9a" : null);
                    await Wait(0.5f);
                    return;
                }
                case PodActivatedEvent pod:
                    await PlayContact(pod, v, Cam, Look);
                    return;
                case ConcealmentBrokenEvent cb:
                {
                    exposure = cb;
                    control.Sfx.Reveal();
                    var why = ExposureText(cb);
                    Hud?.Banner("OCULTACIÓN ROTA", "warn", why);
                    Log($"Ocultación rota: {why}", "#ffc93a");
                    var spotter = Unit(cb.By);
                    if (spotter != null && spotter.Alive && spotter.Team == Teams.Alien && (cb.Reason == "spotted" || cb.Reason == "patrol"))
                    {
                        if (units != null)
                        {
                            units.ForceVisible(spotter.Id);
                            units.Face(spotter.Id, cb.Unit != null ? Feet(cb.Unit) : ClosestSoldier(spotter.Pos));
                        }
                        Float(Chest(spotter.Id), "OS HA VISTO", "callout");
                        Look(Feet(spotter.Id));
                        await Wait(Cam() ? 1.4f : 0.6f);
                        return;
                    }
                    await Wait(0.8f);
                    return;
                }
                case TurnBeganEvent turn:
                {
                    if (turn.Team == Teams.Xcom) rig?.RestoreZoom();
                    if (turn.Team == Teams.Xcom && units != null)
                    {
                        await units.StandUpFed(stop);
                        foreach (var o in v.Units)
                            if (o.Team != Teams.Xcom && !ShownToSquad(o)) units.Shade(o.Id, false);
                    }
                    if (turn.Team == Teams.Alien && !EnemyTurnShows()) return;
                    control.Sfx.Turn(turn.Team == Teams.Alien, !v.Concealed);
                    var partner = turn.Team == Teams.Xcom && control.Players.Any(p => p.Slot != control.MySlot && p.Connected) && turn.Command != null && turn.Command != control.MySlot;
                    Hud?.Banner(turn.Team == Teams.Alien ? "TURNO ENEMIGO" : partner ? $"TURNO DE {PlayerName(turn.Command).ToUpperInvariant()}" : "TU TURNO", turn.Team == Teams.Xcom ? "xcom" : "alien", null, turn.Team == Teams.Xcom && !partner);
                    // The squad's turn: the camera to the soldier who plays first (#47).
                    if (turn.Team == Teams.Xcom) control.TurnCamera(v, turn.Command ?? control.MySlot, true);
                    var left = v.Mission.TurnsLeft != null && turn.Turn > 1 ? v.Mission.TurnsLeft - 1 : v.Mission.TurnsLeft;
                    if (turn.Team == Teams.Xcom && v.Mission.Siege == null && !v.Mission.ObjectiveDone && (left == 5 || left == 3 || left == 1))
                    {
                        // Three left: that the mission is lost then (Ana, 28/10; Lore's words), and over the first soldier, big.
                        var line = left == 1 ? Lore.TimeText.LastTurn : left == 3 ? Lore.TurnsToLoseWarning(3) : Lore.TurnsLeftWarning(left.Value);
                        Log(line, "#ff6b5a");
                        Hud?.Toast(line);
                        if (left == 3 && control.PickedId != null) Hud?.Pin(Chest(control.PickedId), Lore.TurnsLeftFloat(3), "#ff6b5a", 4);
                    }
                    await Wait(1f);
                    return;
                }
                case CommandPassedEvent cp:
                {
                    control.Sfx.Command();
                    if (control.Players.Any(p => p.Slot != control.MySlot && p.Connected))
                    {
                        if (cp.To == control.MySlot) Hud?.Banner("TU TURNO", "xcom", "Tienes el mando: das tú las órdenes", true);
                        else Hud?.Banner($"TURNO DE {PlayerName(cp.To).ToUpperInvariant()}", "xcom", control.State.Ready[control.MySlot] ? $"Has terminado: juega {PlayerName(cp.To)}" : "Mientras, puedes apuntar y planear");
                    }
                    // To the soldier who plays now: the one in play of whoever takes the command, or their first with actions (#47).
                    control.TurnCamera(v, cp.To);
                    Log(cp.To == control.MySlot ? "Tienes el mando" : $"{PlayerName(cp.To)} tiene el mando", PlayerColor(cp.To));
                    return;
                }
                case MissionEndedEvent end:
                    control.Sfx.Outcome(end.Outcome == "victory");
                    await Wait(0.8f);
                    ShowOutcome(end.Outcome, end.Reason);
                    return;
            }
        }

        /// <summary>
        /// Whether this attack fells its target: its `died` comes in the same batch, behind the shot, before anyone does
        /// anything else (game.ts fells).
        /// </summary>
        bool Fells(ShotEvent e)
        {
            foreach (var (next, _, _) in queue)
            {
                if (next is DiedEvent d && d.Unit == e.Target) return true;
                if (next is MovedEvent || next is ShotEvent || next is ExplosiveEvent || next is AbilityUsedEvent || next is TurnBeganEvent) return false;
            }
            return false;
        }

        /// <summary>
        /// An attack (game.ts playAttack): a blow is a lunge and a slash (a bite for the claw); a shot steps out of cover if
        /// it is fired from beside it, raises the gun and fires its rounds (a shotgun's pellets, a heavy gun slower), each a
        /// flash, a bullet and its sparks, then lowers the gun and steps back. A reaction says so; a miss says the odds it
        /// had, never the roll (playtest 20/10 #4).
        /// </summary>
        async Awaitable PlayAttack(ShotEvent e, Func<bool> cam, Action<Vector3> look)
        {
            var v = V;
            var shooter = Unit(e.Unit);
            var target = Unit(e.Target);
            lastShot = (target.Id, e.Result);
            lastAttacker = shooter.Id;
            if (!ShownToSquad(shooter) && !ShownToSquad(target)) return;
            var units = Units;
            var fx = Effects;
            var weapon = Content.AttackWeapon(shooter, e.Ability);
            var chest = Chest(target.Id);
            var hit = e.Result != "miss";
            var colour = shooter.Team == Teams.Xcom ? Fx.Hex(0x9fe8ff) : Fx.Hex(0xff6b5a);
            var stepOut = e.From.X != shooter.Pos.X || e.From.Y != shooter.Pos.Y || e.From.Z != shooter.Pos.Z;
            var from = stepOut ? Board.TileToWorld(v, e.From, Chest(shooter.Id).y - Board.TileToWorld(v, e.From).y) : Chest(shooter.Id);
            // The attack that kills, over the shoulder (Arturo, 20/10 #11); one of ours killed, close on them (#21); and a
            // dead's blow on one of ours, over the dead's shoulder too (Ana, Arturo 29/10; not as they come near: a horde would
            // bring it at every step). Back at the next action.
            var fells = Fells(e);
            var bite = !fells && weapon.Melee && shooter.Team != Teams.Xcom && target.Team == Teams.Xcom && ShownToSquad(shooter);
            var close = !cam() ? null
                : fells ? (target.Team == Teams.Xcom ? DeathShot(target, shooter) : ShownToSquad(shooter) ? ShotCam.OverShoulder(v, from, chest, shooter.Id, target.Id) : null)
                : bite ? ShotCam.OverShoulder(v, from, chest, shooter.Id, target.Id) : null;
            if (close != null) StartKillCam(target.Id, close.Value, bite);
            else if (cam())
            {
                look(Vector3.Lerp(Feet(shooter.Id), Feet(target.Id), 0.5f));
                control.Rig.CinematicZoom(Mathf.Clamp(Vector3.Distance(Feet(shooter.Id), Feet(target.Id)) * 1.1f + 9 * Board.Tile, 13 * Board.Tile, 24 * Board.Tile));
            }
            if (e.Reaction)
            {
                Float(Chest(shooter.Id), weapon.Melee ? "TAJO DE REACCIÓN" : "FUEGO DE REACCIÓN", "warn");
                control.Sfx.Alert();
            }
            if (units == null || fx == null)
            {
                await Wait(weapon.Melee ? 0.45f : 0.6f);
                if (!hit) ShowMiss(e, shooter, target);
                await Wait(0.35f);
                return;
            }

            if (weapon.Melee)
            {
                await Wait(cam() ? 0.2f : 0.05f);
                control.Sfx.Melee("swing", Chest(shooter.Id), Content.AttackWeapon(shooter, e.Ability).Id);
                var stun = e.Ability == AbilityIds.StunLance;
                await units.Lunge(shooter.Id, target.Id, Fells(e) && units.CanFeed(shooter.Id, target.Id), () =>
                {
                    if (!hit) return;
                    fx.Slash(chest, stun ? Fx.Hex(0x6ad8ff) : Fx.Hex(0xe8f6ff));
                    control.Sfx.Melee(stun ? "stun" : e.Ability == AbilityIds.Claw ? "claw" : "sword", chest, Content.AttackWeapon(shooter, e.Ability).Id);
                    control.Sfx.Impact(Bastion.Client.Audio.CombatSound.Body(target), chest, e.Result == "crit");
                    if (e.Ability == AbilityIds.Claw) fx.Bite(chest, Feet(shooter.Id));
                }, stop);
                if (!hit) ShowMiss(e, shooter, target);
                await Wait(0.25f);
                return;
            }

            // Fired from a tile beside its own (from cover): it steps out to it (leaning towards it if something is there), and back after.
            if (stepOut)
            {
                var beside = Board.TileToWorld(v, e.From);
                var feet = Feet(shooter.Id);
                var lean = beside - feet;
                lean.y = 0;
                await units.Slide(shooter.Id, units.ClearFor(e.From, shooter.Id) ? beside : feet + lean.normalized * LeanOut, 0.22f, stop);
            }
            var aim = await units.RaiseWeapon(shooter.Id, Feet(target.Id), Content.FiresSidearm(shooter, e.Ability), stop);
            await Wait(cam() ? 0.25f : 0.08f);
            var heavy = weapon.ApCost > 1;
            var pellets = weapon.Range == RangeTableIds.Short ? 6 : 0;
            var rounds = Math.Max(1, weapon.Burst);
            for (var i = 0; i < rounds; i++)
            {
                var muzzle = units.Muzzle(shooter.Id);
                units.Recoil(shooter.Id);
                fx.MuzzleFlash(muzzle, colour);
                control.Sfx.Shot(shooter, weapon.Id, muzzle);
                if (heavy && cam()) control.Rig.Shake(0.08f * Board.Tile, 0.15f);
                var shots = pellets > 0 ? pellets : 1;
                for (var p = 0; p < shots; p++)
                {
                    var dest = hit ? chest + Jitter(pellets > 0 ? 0.6f : 0.25f) : MissPoint(muzzle, chest, i + p, pellets > 0 ? 1.6f : 1);
                    var bullet = BulletThen(muzzle, dest, colour, heavy ? 45 : 60, heavy, hit ? Fx.Hex(0xff7a5a) : Fx.Hex(0xffd28a), pellets > 0 ? 3 : hit ? 8 : 5);
                    // Where the last round lands, heard (its first pellet): flesh, metal or a miss.
                    if (i == rounds - 1 && p == 0) bullet = Then(bullet, () => { if (!stop.IsCancellationRequested) control.Sfx.Impact(hit ? Bastion.Client.Audio.CombatSound.Body(target) : "miss", dest, e.Result == "crit"); });
                    Loose(bullet);
                }
                await Wait(weapon.Burst > 3 ? 0.08f : 0.11f);
            }
            await Wait(heavy ? 0.25f : 0.15f);
            if (!hit)
            {
                ShowMiss(e, shooter, target);
                await Wait(0.35f);
            }
            var home = Board.TileToWorld(v, shooter.Pos);
            Loose(Later(1.1f, () =>
            {
                Loose(units.LowerWeapon(shooter.Id, aim, stop));
                if (stepOut) Loose(units.Slide(shooter.Id, home, 0.25f, stop));
            }));
        }

        void ShowMiss(ShotEvent e, Unit shooter, Unit target)
        {
            Float(Chest(target.Id), "FALLO", "miss");
            // An enemy with its article (Lore): «Marcus Reid falla contra un Zombi», «Un Zombi falla contra Marcus Reid».
            var who = shooter.Team == Teams.Alien ? Capitalize(AnEnemy(shooter)) : shooter.Name;
            var whom = target.Team == Teams.Alien ? AnEnemy(target) : target.Name;
            Log($"{who} falla contra {whom}: tenía un {AbilityTexts.Number(e.Chance)} % de acertar.");
            var owner = shooter.Team == Teams.Xcom && shooter.ControlledBy == null ? shooter.Owner : null;
            if (owner == null) return;
            var streak = V.MissStreak != null && owner.Value < V.MissStreak.Length ? V.MissStreak[owner.Value] : 0;
            var bonus = Aim.BadLuckAim * Math.Min(Aim.BadLuckMisses, streak + 1);
            var whose = owner == control.MySlot ? "tu próximo disparo" : $"el próximo disparo de {PlayerName(owner)}";
            Log($"Mala racha: {whose} tendrá +{bonus} de puntería", PlayerColor(owner.Value));
        }

        /// <summary>
        /// A throw, a launch, missiles, the Hinchado bursting (game.ts playExplosive): the throw's motion and its arc, then
        /// the blast, the smoke or the flash, and the walls it broke said.
        /// </summary>
        async Awaitable PlayExplosive(ExplosiveEvent e, Func<bool> cam, Action<Vector3> look)
        {
            lastShot = null;
            var u = Unit(e.Unit);
            lastAttacker = u.Id;
            var def = Content.Abilities[e.Ability];
            var to = Board.TileToWorld(V, e.Target, 0.2f);
            if (u.Team == Teams.Alien && !ShownToSquad(u) && !IsVisible(e.Target.X, e.Target.Y)) return;
            var units = Units;
            var fx = Effects;
            units?.BlastAt(to);
            if (e.Ability == AbilityIds.Burst)
            {
                if (!IsVisible(e.Target.X, e.Target.Y)) return;
                look(to);
                Float(Board.TileToWorld(V, e.Target, 0.6f), "¡REVIENTA!", "callout");
                Log($"¡{Capitalize(AnEnemy(u))} revienta!", "#ff6b5a");
                control.Sfx.Burst(to);
                if (cam()) control.Rig.Shake(0.35f * Board.Tile, 0.5f);
                if (fx != null)
                {
                    Loose(fx.Explosion(to, (float)(def.Radius ?? 1.5), stop));
                    fx.Burst(to, (float)(def.Radius ?? 1.5));
                }
                await Wait(0.55f);
                return;
            }
            Log($"{u.Name}: {def.Name.ToLowerInvariant()}");
            if (cam())
            {
                look(Vector3.Lerp(Feet(u.Id), to, 0.5f));
                control.Rig.CinematicZoom(18 * Board.Tile);
            }
            if (units == null || fx == null)
            {
                await Wait(1.3f);
            }
            else
            {
                units.Face(u.Id, to);
                await Wait(0.25f);
                if (e.Ability == AbilityIds.Missiles)
                {
                    var from = Chest(u.Id) + Vector3.up * 0.5f;
                    var spots = new List<Vector3> { to };
                    for (var i = 0; i < 3; i++) spots.Add(to + new Vector3((float)(dice.NextDouble() - 0.5) * 2, 0, (float)(dice.NextDouble() - 0.5) * 2) * Board.Tile);
                    control.Sfx.Missile(from);
                    await fx.Missiles(from, spots, stop);
                }
                else if (e.Ability == AbilityIds.Launch)
                {
                    var aim = await units.RaiseWeapon(u.Id, to, false, stop);
                    var from = units.Muzzle(u.Id);
                    units.Recoil(u.Id);
                    fx.MuzzleFlash(from, Fx.Hex(0xffc080));
                    control.Sfx.Launch(from);
                    await fx.GrenadeArc(from, to, stop);
                    control.Sfx.Land(to);
                    Loose(units.LowerWeapon(u.Id, aim, stop));
                }
                else
                {
                    // The arc leaves the free hand as the throw lets it go, and is seen to land.
                    var landed = false;
                    control.Sfx.Throw(Chest(u.Id));
                    await units.ThrowMotion(u.Id, () => Loose(Then(fx.GrenadeArc(units.ThrowHand(u.Id), to, stop), () => landed = true)), stop);
                    while (!landed && !stop.IsCancellationRequested) await Awaitable.NextFrameAsync(stop);
                    control.Sfx.Land(to);
                }
                if (e.Ability == AbilityIds.Smoke)
                {
                    control.Sfx.Explosion(to, "smoke");
                    fx.Puffs(to, Fx.Hex(0xd8d6d0), 12, 0.6f, 1.2f, 0.4f, 0.8f, 3, 1.6f);
                    await Wait(0.3f);
                    return;
                }
                if (e.Ability == AbilityIds.Flashbang)
                {
                    control.Sfx.Explosion(to, "flash");
                    fx.MuzzleFlash(to + Vector3.up * 0.9f, Color.white);
                    fx.Sparks(to + Vector3.up * 0.9f, Color.white, 24);
                    if (cam()) control.Rig.Shake(0.15f * Board.Tile, 0.3f);
                    await Wait(0.4f);
                    return;
                }
                var radius = Abilities.BlastRadius(u, e.Ability);
                control.Sfx.Explosion(to);
                if (cam()) control.Rig.Shake(0.45f * Board.Tile, 0.6f);
                Loose(fx.Explosion(to, (float)(radius > 0 ? radius : 2.5), stop));
            }
            var walls = Abilities.BreachedWalls(V, e.Ability, e.Target, u).Count;
            if (walls > 0 && IsVisible(e.Target.X, e.Target.Y)) Log(Lore.BreachLine(walls), "#ffb070");
            await Wait(0.4f);
        }

        /// <summary>Arrivals (game.ts spawned): a horde's or a wave's stamp and line, and those that come into sight.</summary>
        async Awaitable PlaySpawned(SpawnedEvent e, GameState v, Func<bool> cam, Action<Vector3> look)
        {
            var seen = e.Units.Where(u => IsVisible(u.Pos.X, u.Pos.Y)).ToList();
            var wave = v.Mission.Reinforcements.FirstOrDefault(r => r.Id == e.Reinforcement);
            if (wave?.Horde == true)
            {
                var where = EdgeName(v, wave.Pos ?? e.Units.FirstOrDefault()?.Pos ?? new Pos());
                var siege = v.Mission.Siege != null;
                var turns = v.Mission.Reinforcements.Select(r => r.Turn).Distinct().OrderBy(t => t).ToList();
                var first = !siege || v.Mission.Reinforcements.FirstOrDefault(r => r.Turn == wave.Turn)?.Id == wave.Id;
                var line = siege ? Lore.WaveEntersLine(turns.IndexOf(wave.Turn) + 1, turns.Count) : $"La horda llega por {where}:";
                if (siege)
                {
                    if (first) Log(line, "#ff6b5a");
                }
                else Log($"{line} {string.Join(", ", e.Units.Select(u => u.Name))}", "#ff6b5a");
                var n = siege ? v.Mission.Reinforcements.Where(r => r.Turn == wave.Turn).Sum(r => r.Units.Count) : e.Units.Count;
                if (first) Hud?.Banner(siege ? "¡OLEADA!" : "¡LA HORDA!", "alien", $"{(n == 1 ? "Ha llegado un muerto" : $"Han llegado {n} muertos")}{(siege ? "" : $" por {where}")}{(seen.Count > 0 ? "" : ", fuera de vuestra vista")}");
            }
            else
            {
                Log($"Llegan más: {string.Join(", ", e.Units.Select(u => u.Name))}", "#ff6b5a");
                Hud?.Banner("¡LLEGAN MÁS!", "alien", $"{(e.Units.Count == 1 ? "Ha llegado un enemigo" : $"Han llegado {e.Units.Count} enemigos")}{(seen.Count > 0 ? "" : " fuera de vuestra vista")}");
            }
            if (seen.Count == 0) return;
            if (!grouped) look(Board.TileToWorld(v, seen[0].Pos));
            // arrivals.ts's sound: the dead's moan, or the living's engine (or boots) at the first of them.
            var dead = seen.Count(u => Gaits.MovesDead(u.Template));
            if (dead > 0) control.Sfx.Moan(Board.TileToWorld(v, seen[0].Pos), dead);
            if (seen.FirstOrDefault(u => !Gaits.MovesDead(u.Template)) is Unit living) control.Sfx.Vehicle(Board.TileToWorld(v, living.Pos));
            // They come in on foot from the edge, or get up out of the dust: no capsules (Arrivals, arrivals.ts).
            await Arrivals.Play(Units, v, seen, Wait, stop);
            foreach (var u in seen) Float(Board.TileToWorld(v, u.Pos, 1.5f), $"¡APARECE UN {Content.Templates[u.Template].Name.ToUpperInvariant()}!", "callout");
            await Wait(cam() ? 0.8f : 0.3f);
        }

        /// <summary>A pod woken (game.ts podActivated): its «!», the contact's stamp and line, or the living and the dead fighting.</summary>
        async Awaitable PlayContact(PodActivatedEvent e, GameState v, Func<bool> cam, Action<Vector3> look)
        {
            var pod = v.Pods.FirstOrDefault(p => p.Id == e.Pod);
            var all = pod?.UnitIds.Select(Unit).Where(m => m != null && m.Alive).ToList() ?? new List<Unit>();
            // Woken by the other enemies (18/10 #47): attacked, and not by the squad.
            var feud = e.Cause == "attacked" && e.Unit == null;
            var members = feud ? all.Where(ShownToSquad).ToList() : all;
            if (members.Count == 0) return;
            var units = Units;
            if (feud)
            {
                foreach (var m in members)
                {
                    Float(Chest(m.Id), "!", "alert");
                    if (units != null) Loose(units.Alert(m.Id, stop));
                }
                Log($"Vivos y muertos se pelean: {string.Join(", ", members.Select(m => m.Name))}", "#e8d8c0");
                await Wait(0.5f);
                return;
            }
            var centre = members.Aggregate(Vector3.zero, (acc, m) => acc + Board.TileToWorld(v, m.Pos)) / members.Count;
            if (!grouped)
            {
                look(centre);
                if (cam()) await Wait(0.35f);
            }
            foreach (var m in members)
            {
                if (units != null)
                {
                    units.ForceVisible(m.Id);
                    units.Face(m.Id, ClosestSoldier(m.Pos));
                    Loose(units.Alert(m.Id, stop));
                }
                Float(Chest(m.Id), "!", "alert");
            }
            control.Sfx.Contact(centre, members.Any(m => Content.Templates[m.Template].Undead));
            var how = ContactCause(e);
            Hud?.Banner("¡CONTACTO!", "alien", how);
            Log($"Contacto: {string.Join(", ", members.Select(m => m.Name))} · {how}", "#ff6b5a");
            await Wait(0.9f);
        }

        /// <summary>
        /// A noise (game.ts playNoise): a ring of its radius spreading from where it sounded, for the squad's own and for
        /// what the squad sees make one (the scream). Enemies out of sight that heard the squad and only now come to look
        /// get «Os han oído», a stamp and a line, never the camera.
        /// </summary>
        void PlayNoise(NoiseEvent e)
        {
            var by = Unit(e.By);
            var ours = by?.Team == Teams.Xcom;
            var scream = by != null && lastUsed?.unit == by.Id && lastUsed?.ability == AbilityIds.Mindspin;
            if (ours || IsVisible(e.Tile.X, e.Tile.Y)) control.AimMarks?.Noise(V, e.Tile, (float)e.Radius, MoveOverlay.Hex(ours ? 0xffc93a : scream ? 0xc58cff : 0xff6b5a));
            var fresh = e.Heard.Select(Unit).Where(a => a != null && a.Alive && a.Heard == null && !control.Visible(a)).ToList();
            if (fresh.Count == 0 || by == null) return;
            if (!ours)
            {
                if (scream && control.Visible(by)) Log($"El grito se oye a {AbilityTexts.Number(e.Radius)} casillas: {HeardCount(fresh)} {(fresh.Count == 1 ? "acude" : "acuden")}", "#e8d8c0");
                return;
            }
            var away = Grid.Distance(by.Pos, e.Tile) > 1.5;
            var one = fresh.Count == 1;
            var text = $"{HeardCount(fresh)} {(away ? (one ? "va hacia donde sonó" : "van hacia donde sonó") : one ? "viene hacia aquí" : "vienen hacia aquí")}";
            if (!alarmRaised) Hud?.Banner("OS HAN OÍDO", "warn", Capitalize(text));
            Log($"Os han oído: {text}", "#ffc93a");
        }

        /// <summary>Whether anything of the enemy turn now queued will be seen: something done in sight, or to one of ours.</summary>
        bool EnemyTurnShows()
        {
            foreach (var (e, _, _) in queue)
            {
                if (e is TurnBeganEvent) return false;
                if (e is ReinforcementsIncomingEvent || e is MissionEndedEvent || e is ConcealmentBrokenEvent) return true;
                if (e is ShotEvent s && Unit(s.Target) is Unit t && ShownToSquad(t)) return true;
                var u = Unit(UnitOf(e));
                if (u != null && (u.Team == Teams.Xcom || ShownToSquad(u) || (e is MovedEvent m && m.Path.Any(p => IsVisible(p.X, p.Y))))) return true;
            }
            return false;
        }

        static string UnitOf(GameEvent e)
        {
            var p = e.GetType().GetProperty("Unit");
            return p != null && p.PropertyType == typeof(string) ? (string)p.GetValue(e) : null;
        }

        /// <summary>Why the squad was found, in plain words: who saw whom and from how far, or what gave it away.</summary>
        string ExposureText(ConcealmentBrokenEvent e)
        {
            var soldier = Unit(e.Unit);
            var alien = Unit(e.By);
            var who = soldier?.Name ?? "un soldado";
            var range = e.Range != null ? $" ({AbilityTexts.Number(e.Range.Value)} casillas)" : "";
            switch (e.Reason)
            {
                case "spotted":
                    var unseen = alien != null && !control.Visible(alien) ? " que no veíais" : "";
                    return $"{Capitalize(AnEnemy(alien))}{unseen} ha visto a {who}: entró en su radio de detección{range}.";
                case "patrol":
                    return $"{Capitalize(AnEnemy(alien))} de patrulla se ha acercado hasta tener a {who} en su radio de detección{range}.";
                case "attack":
                    return $"{Capitalize(who)} ha atacado{(alien != null ? $" a {AnEnemy(alien)}" : "")}: el combate os delata.";
                case "explosive":
                    return $"La explosión de {who} se ha oído en toda la zona.";
                case "alarm":
                    return $"{(soldier != null ? $"{who} no ha podido arreglarlo" : "El arreglo ha fallado")}: ha saltado la alarma.";
                case "reinforcements":
                    return "Los que llegan vienen alertados: saben dónde estáis.";
                default:
                    return "El enemigo sabe dónde estáis.";
            }
        }

        /// <summary>Who found whom in a contact: they saw the squad, the squad came upon them, they were attacked or heard it.</summary>
        string ContactCause(PodActivatedEvent e)
        {
            var soldier = Unit(e.Unit);
            var who = soldier?.Name ?? "la escuadra";
            switch (e.Cause)
            {
                case "sighted":
                    if (exposure != null && exposure.Reason == "spotted" && exposure.By == e.By && exposure.Unit == e.Unit) return $"Os han visto: han descubierto a {who}";
                    return $"{Capitalize(who)} los ha avistado";
                case "patrol":
                    return $"Una patrulla ha dado con {who}";
                case "attacked":
                    return soldier != null ? $"Responden al ataque de {who}" : "Responden al ataque";
                case "noise":
                    return "Han oído la explosión";
                default:
                    return "Os han localizado";
            }
        }

        /// <summary>Which edge of the map a tile is nearest to, as the radio says it (the map's top row is the north).</summary>
        static string EdgeName(GameState s, Vec2 p)
        {
            var gaps = new List<(int gap, string name)> { (p.Y, "el norte"), (s.Height - 1 - p.Y, "el sur"), (p.X, "el oeste"), (s.Width - 1 - p.X, "el este") };
            return gaps.OrderBy(g => g.gap).First().name;
        }

        string RecoveryItem() => V.CampaignMission?.Item ?? Lore.DefaultItem;

        Lore.EvacSituation Situation(GameState s) => new Lore.EvacSituation(
            s.Mission.Kind == MissionKinds.Elimination,
            Rules.EvacWins(s),
            s.Units.Where(x => x.Alive && x.Team == Teams.Xcom && (!Abilities.InEvacZone(s, x.Pos) || (x.Infected == true && s.Mission.Quarantine == false))).Select(x => x.Name).ToList());

        // ------------------------------------------------------- the partner's moments

        /// <summary>
        /// What the partner just did that this player should see (game.ts notePartner): a contact, the squad found, one of
        /// ours fallen, the item picked up, the equipment fixed; a notice with «Ver», and F goes there while it is up.
        /// </summary>
        void NotePartner(GameEvent e, int by)
        {
            var v = V;
            var who = PlayerName(by);
            Pos pos = null;
            string text;
            switch (e)
            {
                case PodActivatedEvent pod:
                {
                    if (pod.Cause == "attacked" && pod.Unit == null) return;
                    var first = v.Pods.FirstOrDefault(p => p.Id == pod.Pod)?.UnitIds.Select(Unit).FirstOrDefault(m => m != null && m.Alive);
                    pos = first?.Pos;
                    text = $"{who}: ¡contacto!";
                    break;
                }
                case ConcealmentBrokenEvent cb:
                {
                    var spotter = Unit(cb.By);
                    pos = spotter?.Team == Teams.Alien ? spotter.Pos : Unit(cb.Unit ?? control.PartnerLast)?.Pos;
                    text = $"{who} ha roto la ocultación";
                    break;
                }
                case DiedEvent died:
                {
                    var u = Unit(died.Unit);
                    if (u?.Team == Teams.Xcom) text = $"{u.Name} ha caído";
                    else if (u?.Objective == true) text = Lore.AlarmDoneLine(who, v.Mission.Alarm).TrimEnd('.');
                    else return;
                    pos = u.Pos;
                    break;
                }
                case ItemPickedEvent picked:
                    pos = Unit(picked.Unit)?.Pos;
                    text = Lore.ItemPartnerLine(who, RecoveryItem());
                    break;
                case HackedEvent hack:
                    if (!hack.Success) return;
                    pos = Unit(hack.Unit)?.Pos;
                    text = $"{who}: equipo en marcha";
                    break;
                default:
                    return;
            }
            if (pos == null) return;
            moment = Board.TileToWorld(v, pos);
            control.SetMoment(moment, who);
            Hud?.Notice(text, PlayerColor(by), () => SeeMoment());
        }

        /// <summary>«Ver» on a partner's moment: the camera there.</summary>
        public bool SeeMoment()
        {
            if (moment == null || control.Rig == null) return false;
            control.Rig.TakeControl();
            control.Rig.Focus(moment.Value);
            moment = null;
            control.SetMoment(null, null);
            return true;
        }

        // ------------------------------------------------------- the end

        /// <summary>
        /// The mission's end (game.ts showOutcome): won or lost, and one line with why, the haul, those killed, the losses
        /// and who stayed outside.
        /// </summary>
        public void ShowOutcome(string outcome, string reason)
        {
            if (Hud == null) return;
            if (outcome == null)
            {
                Hud.SetOutcome(null);
                return;
            }
            Hud.CloseIntro(true);
            var s = control.State;
            var lost = s.Units.Count(u => u.Team == Teams.Xcom && !u.Alive && !u.Evacuated && u.Lost != true);
            var left = s.Units.Count(u => u.Lost == true);
            var evacuated = s.Units.Count(u => u.Evacuated);
            var killed = s.Units.Count(u => u.Team == Teams.Alien && !u.Alive);
            var vip = s.Mission.Kind == MissionKinds.Rescue && s.Mission.ObjectiveUnit != null ? s.Units.FirstOrDefault(u => u.Id == s.Mission.ObjectiveUnit)?.Name : null;
            var haul = Looting.BroughtBack(s);
            var loot = Lore.LootSummary(haul.Supplies, haul.Items, haul.Medicines, haul.Food, haul.Parts, haul.Weapons);
            string success;
            switch (s.Mission.Kind)
            {
                case MissionKinds.Recovery: success = $"{Capitalize(RecoveryItem())} a salvo"; break;
                case MissionKinds.Sabotage: success = "Ya no suena"; break;
                case MissionKinds.Hack: success = "Equipo en marcha"; break;
                case MissionKinds.Rescue: success = vip != null ? $"{vip}, a salvo" : "La persona retenida, a salvo"; break;
                case MissionKinds.Scavenge: success = loot != null ? Lore.Scavenge.Success : Lore.Scavenge.EmptyHanded; break;
                default: success = "No queda ninguno en pie"; break;
            }
            string why;
            var siege = s.Mission.Siege != null;
            switch (reason)
            {
                case "objective":
                    why = siege ? (s.Units.Any(u => u.Alive && u.Team == Teams.Alien) ? Lore.SiegeText.Held : Lore.SiegeText.Cleared) : departedOnTime ? Lore.TimeText.WonOnTime : success;
                    break;
                case "wiped":
                    why = siege ? Lore.SiegeText.Wiped : evacuated > 0 ? "La escuadra se retiró sin cumplir el objetivo" : "Escuadra eliminada";
                    break;
                case "timer":
                    why = s.Mission.EvacZone.Count == 0 ? Lore.TimeText.TimeUp : s.Units.Any(u => u.Team == Teams.Xcom && u.Evacuated) ? Lore.TimeText.LostOnTime : Lore.TimeText.LeftEmpty;
                    break;
                case "abandoned":
                    why = s.Mission.EvacDone == true ? (s.Mission.Kind == MissionKinds.Elimination ? Lore.EvacText.Retreat : Lore.EvacText.WithoutObjective) : "Misión abandonada";
                    break;
                case "objectiveLost":
                    why = vip != null ? $"{vip} ha muerto" : "La persona retenida ha muerto";
                    break;
                default:
                    why = "";
                    break;
            }
            string Count(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";
            var found = s.CampaignMission == null && outcome == "victory" && loot != null ? loot.TrimEnd('.') : "";
            var head = found != "" && reason == "objective" && s.Mission.Kind == MissionKinds.Scavenge ? $"{why}: {found.Replace("El botín: ", "")}" : string.Join(" · ", new[] { why, found }.Where(x => x != ""));
            var line = s.CampaignMission != null
                ? why
                : string.Join(" · ", new[] { head, Count(killed, "abatido", "abatidos"), lost > 0 ? Count(lost, "baja", "bajas") : "sin bajas", left > 0 ? (left == 1 ? "1 se quedó fuera" : $"{left} se quedaron fuera") : "" }.Where(x => x != ""));
            Hud.SetOutcome(new OutcomeView { Victory = outcome == "victory", Line = line, Campaign = s.CampaignMission != null, Siege = siege });
        }
    }
}
