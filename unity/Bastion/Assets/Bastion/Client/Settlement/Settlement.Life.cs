using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client
{
    /// <summary>
    /// The soldiers out about the clearing while the Asentamiento is open (0.10.1's clearingLife.ts, Arturo 22/10 #1): the
    /// camp's life (ClearingLife, exact with 0.10.1) on the base's soldiers on their feet, both commanders', each a figure
    /// as the squad's row makes them, its gun slung (CampFigure: the life's calls on Sidekick and FigureAnimator). What they
    /// keep off: La Lata, the Armería's corner, the soldier in Soldados (clearingArt.ts lifeTaken) and what is being placed
    /// (Ghost, GhostLine). Only on the Asentamiento's tab (screen.ts: setLife there, none elsewhere).
    /// </summary>
    public sealed partial class Settlement
    {
        ClearingLife life;
        Transform lifeRoot;
        /// <summary>Who is out (screen.ts lifeEntries), and the campaign they are of: their looks, arms, the soldiers by id.</summary>
        List<LifeEntry> lifeEntries = new List<LifeEntry>();
        CampaignState lifeCampaign;
        /// <summary>The life's own clock (seconds): its moves and gestures are timed by it.</summary>
        double lifeTime;

        /// <summary>Those out about the clearing now (for the labs).</summary>
        public int LifeCount => life?.Idlers.Count ?? 0;

        /// <summary>The figure of the one out whose seed is `seed` (for the labs), or null.</summary>
        public Transform LifeFigure(string seed) =>
            life?.Idlers.FirstOrDefault(q => q.key.Contains($"\"seed\":\"{seed}\""))?.figure is CampFigure f ? f.Root : null;

        /// <summary>The animator's weights of the one out whose seed is `seed` (for the labs), with what the life has it doing.</summary>
        public string LifeWeights(string seed)
        {
            var d = life?.Idlers.FirstOrDefault(q => q.key.Contains($"\"seed\":\"{seed}\""));
            return d?.figure is CampFigure f ? $"{d.kind}{(d.act != null ? " act " + d.act.move : "")}{(d.spot != null ? " " + d.spot.loop : "")}{(d.settled ? " settled" : "")}: {f.Weights()}" : "-";
        }

        /// <summary>What each of those out is doing (for the labs): its kind, where, its place's loop, settled or on its way.</summary>
        public string LifeReport() => life == null ? "none" : string.Join("; ", life.Idlers.Select(d =>
            FormattableString.Invariant($"{d.kind} at {d.at.x:0.0},{d.at.z:0.0}{(d.spot != null ? " " + d.spot.loop : "")}{(d.settled ? " settled" : "")}{(d.goal != null ? " walking" : "")}{(d.stance != null ? " " + d.stance : "")}{(d.habit != null ? " " + d.habit : "")}")));

        ClearingLife Life
        {
            get
            {
                if (life != null) return life;
                lifeRoot = new GameObject("Vida del campamento").transform;
                lifeRoot.SetParent(transform, false);
                life = new ClearingLife(MakeCampFigure, DropCampFigure, () => UnityEngine.Random.value, () => Time.realtimeSinceStartupAsDouble * 1000);
                return life;
            }
        }

        /// <summary>The campaign's soldiers on their feet, both commanders', each in their player's colour, with their gear and arms (screen.ts lifeEntries, gearFor).</summary>
        void LifeOf(CampaignState c)
        {
            lifeCampaign = c;
            int armor = Mathf.Clamp(Mathf.RoundToInt(Progression.ArmorTier(c)), 1, 3);
            lifeEntries = c.Soldiers.Where(s => s.Alive && s.WoundedDays <= 0).Select(s => new LifeEntry
            {
                Template = "rookie",
                Colour = SquadTint.ColourOf(s.Owner),
                Appearance = s.Appearance,
                // (weaponTier is 1 for all: progression.ts.)
                Gear = new LifeGear { Weapon = 1, Armor = armor, Arms = Campaign.SoldierArms(c, s) },
                Seed = s.Id,
            }).ToList();
        }

        /// <summary>
        /// For the labs: the camp without its fires (and without La Lata's sides unless `walls`, without its chairs unless
        /// `seats`), its things kept. Only so do two of them talk (clearingLife.ts kindsFor: LIFE_MAX's six, with a fire,
        /// leave one standing about); and with no chair free the one walking goes to lean on La Lata (freeSpot).
        /// </summary>
        public void LifeBareCamp(bool walls = false, bool seats = true)
        {
            var now = Life.CampNow;
            var bare = new ClearingLife.Camp();
            if (seats) bare.seats.AddRange(now.seats);
            bare.things.AddRange(now.things);
            if (walls) bare.walls.AddRange(now.walls);
            Life.SetCamp(bare);
        }

        /// <summary>For the labs: these cells kept clear of those out, as a building's ghost keeps them (Ghost: KeepClear).</summary>
        public void LifeKeepClear(IReadOnlyList<Vector2Int> cells) => KeepClear(cells);

        /// <summary>Those out about the clearing given as they are (claro.html's vida: the lab's), not from a campaign; shown on the Asentamiento's tab.</summary>
        public void SetLife(IReadOnlyList<LifeEntry> entries)
        {
            lifeEntries = entries?.ToList() ?? new List<LifeEntry>();
            LifeView();
        }

        /// <summary>The clearing as it stands to the life: its base, what is taken, the camp (its fires, chairs, things, La Lata's sides).</summary>
        void LifeBase()
        {
            if (Base == null || built == null) return;
            Life.SetBase(Base, LifeTaken);
            var camp = ClearingLife.Camp.Of(ClearingState(Base, Snow), built.BoxOf);
            if (Lata != null && busParked && !away)
            {
                // Out from its side as Unity's bus has it, not 0.10.1's half width and LEAN_OFF (Gameplay 26/10: a third of a
                // tile off): its plates, or its body in a doorway (on its doors' side, 0.75 tiles either way of one's middle),
                // and the body's reach to that side leant (LeanReach).
                double yaw = Math.Atan2(busDir.x, busDir.y), dirX = Math.Sin(yaw), dirZ = Math.Cos(yaw);
                var doors = Lata.Doorways();
                double Reach(double k, int side)
                {
                    bool doorway = doors.Any(d =>
                    {
                        double dx = d.x - busAt.x, dz = d.z - busAt.y;
                        return Math.Abs(dx * dirX + dz * dirZ - k) < 0.75 && (dx * dirZ - dz * dirX) * side > 0;
                    });
                    return (doorway ? Lata.Width : Lata.PlatedWidth) / 2 + LeanReach;
                }
                camp.AddBusSides(busAt.x, busAt.y, yaw, Lata.Length, Reach, (place?.spots ?? new List<Vector2>()).Select(p => new ClearingLife.Vec(p.x, p.y)));
            }
            Life.SetCamp(camp);
        }

        /// <summary>
        /// How far the body reaches to its side leant on La Lata, past its feet (tiles): 0.48 m to its right in leanRight,
        /// 0.51 m to its left in leanLeft, as skinned, male and female alike (ClipProbe.Alone BASTION_ANCHO, 26/10).
        /// </summary>
        const double LeanReach = 0.33;

        /// <summary>clearingArt.ts lifeTaken: La Lata with a step round it, the Armería's corner, Soldados' place (tiles).</summary>
        bool LifeTaken(double x, double z)
        {
            if (Lata != null && busParked && !away)
            {
                double yaw = Math.Atan2(busDir.x, busDir.y);
                double dx = x - busAt.x, dz = z - busAt.y;
                double along = dx * Math.Sin(yaw) + dz * Math.Cos(yaw);
                double across = dx * Math.Cos(yaw) - dz * Math.Sin(yaw);
                if (Math.Abs(along) < Lata.Length / 2 + 1 && Math.Abs(across) < Lata.Width / 2 + 1) return true;
            }
            return Js.Hypot(x - armoryAt.x, z - armoryAt.y) < 2 || Js.Hypot(x - soldierAt.x, z - soldierAt.y) < 1.5;
        }

        /// <summary>Those out on the Asentamiento's tab, none on the others (screen.ts render: setLife).</summary>
        void LifeView()
        {
            if (Base == null) return;
            // Those about the clearing but the squad standing by La Lata (clearingArt.ts relife: the same soldier was in both).
            var inRow = new HashSet<string>(lineupEntries.Where(e => e?.SoldierId != null).Select(e => e.SoldierId));
            Life.Set(view == Views.Facilities ? lifeEntries.Where(e => e.Seed == null || !inRow.Contains(e.Seed)).ToList() : new List<LifeEntry>());
        }

        void StepLife(float dt)
        {
            if (life == null) return;
            lifeTime += dt;
            life.Tick(dt, lifeTime);
        }

        ICampFigure MakeCampFigure(LifeEntry entry)
        {
            var s = lifeCampaign?.Soldiers.FirstOrDefault(q => q.Id == entry.Seed);
            var look = entry.Appearance;
            string named = s != null && (look == null || string.IsNullOrEmpty(look.Sex)) ? Content.SexOfName(s.Name) : null;
            var dress = Dress.Of(look, entry.Seed, named);
            var holder = new GameObject(s?.Name ?? entry.Seed).transform;
            holder.SetParent(lifeRoot, false);
            var stander = new Stander { root = holder, build = dress.sex == "f" ? "femn" : "masc" };
            try
            {
                var figure = SoldierFigure.Make(dress, s?.Name ?? entry.Seed, true);
                SquadTint.Paint(figure, look, entry.Colour);
                FaceLit(figure);
                figure.transform.SetParent(holder, false);
                figure.transform.localScale = Vector3.one * Figures.CharacterScale;
                foreach (var r in figure.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                stander.animator = figure.GetComponentInChildren<Animator>();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[settlement] {entry.Seed}: no figure ({e.Message})");
            }
            var catalog = FigureCatalog.Load();
            if (stander.animator != null && stander.animator.isHuman && catalog != null)
            {
                stander.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                // (The pistol, the only gun, rides in its holster: Sling.)
                string gun = GunOf(entry);
                stander.pistol = gun == "pistol";
                stander.gun = stander.pistol ? UnitViews.Pistol(holder, catalog) : UnitViews.Gun(Anims.Weapon(gun), holder, catalog, out _);
                string build = stander.build;
                stander.anim = new FigureAnimator(stander.animator, m => catalog.Clip(Anims.Motion(build, m, false)), null, m => false);
            }
            return new CampFigure(this, stander);
        }

        /// <summary>
        /// The model of the gun one out has slung (syntyFigures.ts armamentOf's weapon): a soldier's own from the Armería at
        /// its weapon tier, none for the pistol; one given only its class (the labs' entries), its class's (UNIT_WEAPONS).
        /// </summary>
        static string GunOf(LifeEntry entry)
        {
            int tier = entry.Gear?.Weapon ?? 1;
            var arms = entry.Gear?.Arms;
            return arms != null ? arms.Primary != null ? Anims.FoundModel(arms.Primary, tier) : "pistol" : Anims.UnitWeapon(entry.Template, tier);
        }

        void DropCampFigure(ICampFigure f)
        {
            if (f is CampFigure c) c.Dispose();
        }

        /// <summary>
        /// A soldier out about the clearing (ClearingLife's ICampFigure on the squad row's figure): 0.10.1's rig calls, in
        /// tiles and three's yaw, onto Sidekick: the position and turn, the walk and the idle, the camp's loops and moves
        /// (CAMP_LOOPS, CAMP_MOVES: a part of a clip, or a span: Anims.CampPartOf, CampSpan), its variations now and then
        /// while it stands at ease (CAMP_VARIATIONS, every 5 to 14 s: syntyAnims.ts nextWait), the gestures, the stances, and
        /// the cup or the bar in the right hand where a gun's grip goes (holdItem: the cup kept level).
        /// </summary>
        sealed class CampFigure : ICampFigure
        {
            readonly Settlement owner;
            readonly Stander s;
            readonly FigureCatalog catalog = FigureCatalog.Load();
            float walkSpeed = 1;
            bool walking, posed;
            string loop;
            bool varies;
            float variationWait = Wait();
            string move;
            double moveAt, moveLength;
            double gestureUntil = -1;
            string stance = "-";
            Transform item;
            string itemId;
            bool itemUpright;
            Vector3 itemCentre;
            double time = -1;

            /// <summary>What it holds sits this far up the grip from where the palm closes (syntyFigures.ts ITEM_AT: tiles).</summary>
            const float ItemUp = 0.02f;

            static float Wait() => 5 + UnityEngine.Random.value * 9;

            public CampFigure(Settlement owner, Stander s)
            {
                this.owner = owner;
                this.s = s;
            }

            public Transform Root => s.root;

            public string Weights() => $"loop {loop ?? "-"} move {move ?? "-"} stance {stance ?? "-"} varies {varies} | {s.anim?.Weights() ?? "-"}";

            public void Dispose()
            {
                if (s.root != null) DestroyNow(s.root.gameObject);
            }

            public void SetPosition(double x, double z) => s.root.localPosition = World(new Vector3((float)x, 0, (float)z));

            public void Sling(bool on)
            {
                if (on) owner.Sling(s);
            }

            public void SetGait(string motion, double speed) => walkSpeed = (float)speed;

            public double CampMove(string next)
            {
                if (next == null)
                {
                    if (move != null) s.anim?.StopWhole(0.25f);
                    move = null;
                    return 1;
                }
                if (next != move)
                {
                    move = next;
                    var (clip, from, to) = Part(Anims.CampMove(s.build, next), false);
                    if (clip == null || s.anim == null)
                    {
                        moveLength = 0;
                        return 1;
                    }
                    s.anim.PlayWholePart(clip, from, to, 0.25f, false);
                    moveAt = owner.lifeTime;
                    moveLength = Math.Max(1e-3, to - from);
                }
                return moveLength <= 0 ? 1 : Math.Min(1, (owner.lifeTime - moveAt) / moveLength);
            }

            public void Camp(string next, bool on, bool now)
            {
                varies = on;
                if (next == loop) return;
                loop = next;
                if (s.anim == null) return;
                var (clip, from, to) = Part(Anims.CampLoop(s.build, next), true);
                var part = Anims.CampPartOf(Anims.CampLoop(s.build, next));
                s.anim.CampLoop(clip, now, from, to, part != null);
            }

            public bool Gesture(string gesture)
            {
                if (s.anim == null || owner.lifeTime < gestureUntil) return false;
                var clip = catalog?.Clip(Anims.Gesture(s.build, gesture));
                if (clip == null) return false;
                float length = s.anim.PlayUpper(clip);
                gestureUntil = owner.lifeTime + Math.Max(length, clip.length);
                return true;
            }

            public void HoldItem(string next, bool upright)
            {
                itemUpright = upright;
                if (next == itemId) return;
                itemId = next;
                if (item != null) DestroyNow(item.gameObject);
                item = null;
                if (next == null || owner.built == null) return;
                var o = owner.built.PropAlone(next, s.root);
                if (o == null) return;
                foreach (var r in o.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                item = o.transform;
                // Its middle where the palm closes (syntyFigures.ts holdItem: the box's centre at ITEM_AT).
                var renderers = o.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var b = renderers[0].bounds;
                    foreach (var r in renderers) b.Encapsulate(r.bounds);
                    itemCentre = item.InverseTransformPoint(b.center);
                }
            }

            public void SetStance(string next)
            {
                if (next == stance) return;
                stance = next;
                s.anim?.Rest(next == null ? null : catalog?.Clip(Anims.Stance(s.build, next)));
            }

            public void ApplyPose(double yaw, double phase, double run, double now)
            {
                float y = (float)yaw;
                s.root.localRotation = Quaternion.LookRotation(new Vector3(Mathf.Sin(y), 0, -Mathf.Cos(y)), Vector3.up);
                bool w = run > 0.5;
                if (s.anim != null && (w != walking || !posed))
                {
                    walking = w;
                    posed = true;
                    s.anim.Motion(w ? "walk" : "idle", w ? walkSpeed : 1);
                }
                float dt = time < 0 ? 0 : Mathf.Clamp((float)(now - time), 0, 0.1f);
                time = now;
                Vary(dt);
                if (s.anim != null) s.anim.Step(dt);
                if (s.slung) KeepSlung(s);
                PlaceItem();
            }

            /// <summary>Standing at ease, a camp variation now and then in the plain idle's place (syntyAnims.ts update: calm, no stance, no gesture, no move).</summary>
            void Vary(float dt)
            {
                if (s.anim == null) return;
                variationWait -= dt;
                if (variationWait > 0) return;
                variationWait = Wait();
                if (!varies || walking || loop != null || stance != null || move != null || owner.lifeTime < gestureUntil || s.anim.InCamp || s.anim.Varying) return;
                var ids = Anims.CampVariations(s.build);
                if (ids.Count == 0) return;
                var clip = catalog?.Clip(ids[UnityEngine.Random.Range(0, ids.Count)]);
                if (clip != null) s.anim.Vary(clip);
            }

            /// <summary>The cup or the bar in the right palm (UnitViews.Hold's palm), along the fist, or level (`upright`: the cup).</summary>
            void PlaceItem()
            {
                if (item == null || s.animator == null) return;
                Transform right = s.animator.GetBoneTransform(HumanBodyBones.RightHand), rightBase = s.animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
                if (right == null) return;
                var palm = rightBase != null ? Vector3.Lerp(right.position, rightBase.position, 0.6f) : right.position;
                var turn = itemUpright ? s.root.rotation : right.rotation;
                item.rotation = turn;
                item.position = palm + turn * (Vector3.up * ItemUp * MapSpace.Tile - Vector3.Scale(itemCentre, item.lossyScale));
            }

            /// <summary>
            /// A camp clip by its id, or a part of one (`id`In / `id`Out of a CampPart, its loop when `looped`), or a span of
            /// it (CampSpan): the clip and its seconds.
            /// </summary>
            (AnimationClip clip, float from, float to) Part(string id, bool looped)
            {
                if (id == null || catalog == null) return (null, 0, 0);
                string whole = id;
                float[] span = null;
                if (looped && Anims.CampPartOf(id) is Anims.CampPart l) span = l.loop;
                else if (id.EndsWith("In") && Anims.CampPartOf(id.Substring(0, id.Length - 2)) is Anims.CampPart i)
                {
                    whole = id.Substring(0, id.Length - 2);
                    span = i.into;
                }
                else if (id.EndsWith("Out") && Anims.CampPartOf(id.Substring(0, id.Length - 3)) is Anims.CampPart o)
                {
                    whole = id.Substring(0, id.Length - 3);
                    span = o.@out;
                }
                var clip = catalog.Clip(whole);
                if (clip == null) return (null, 0, 0);
                if (span != null && span.Length >= 2) return (clip, span[0], span[1]);
                if (Anims.CampSpan(whole) is (float from, float to) cut) return (clip, cut.from, cut.to);
                return (clip, 0, clip.length);
            }
        }
    }
}
