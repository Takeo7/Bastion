using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// The special dead's figures (syntyManifest.ts UNIT_BODIES, CHARACTERS): POLYGON's City Zombies and Boss Zombies, not
    /// Sidekick's, each the body its pack's prefab has on (the prefab holds every body of its rig) and what it keeps on;
    /// the common dead's that are POLYGON's too (City Zombies' in the crowd, Horror Asylum's patients); how every one of
    /// the dead leans and sways over its clips, and the Chillón's scream on its body.
    /// </summary>
    public sealed partial class UnitViews
    {
        /// <summary>
        /// One of them: its pack's prefab, the body in it, its build, its size over the figures', the attachments it keeps,
        /// the atlas it wears instead of its own (none: its own), and where in its atlas a living one's skin is (u0, v0, u1,
        /// v1; none: it was dead already), made the dead's.
        /// </summary>
        sealed class DeadBody
        {
            public string prefab;
            public string body;
            public bool female;
            public float scale = 1;
            public string[] keep;
            public string atlas;
            public Vector4 living;
        }

        /// <summary>
        /// A City Zombies character by its name (Jock_Male_01: SM_Chr_Zombie_Jock_Male_01 and its body Zombie_Jock_Male_01; or
        /// `body`, the prisoner's, Zombie_Prisinor_Male_01 in the pack).
        /// </summary>
        static DeadBody City(string name, bool female = false, float scale = 1, string body = null) =>
            new DeadBody { prefab = "SM_Chr_Zombie_" + name, body = body ?? "Zombie_" + name, female = female, scale = scale };

        /// <summary>
        /// The Chillón's hair (the 3D review's A-28, 10/11: the first of its prefab's two over its face in every pose but one
        /// thrown far back); BASTION_PELO_CHILLON=2, the other, for the labs to set them side by side.
        /// </summary>
        static readonly string WretchHair = System.Environment.GetEnvironmentVariable("BASTION_PELO_CHILLON") == "2" ? "SM_Chr_Attach_Wretch_Hair_02" : "SM_Chr_Attach_Wretch_Hair_01";

        /// <summary>
        /// The Corredores (the recently dead, still quick), the Acorazados (riot police, police and firemen, vest and helmet
        /// still on, a little bigger; no soldier, the common dead's in uniform: Gameplay, 2/11), the Chillón (Boss Zombies' Wretch, a head taller than the rest) and the Hinchados, the bosses with
        /// their hair and nothing else (the Blobber's prefab has a tiara on, 0.10.1's had none). The Hinchados five (Ana's
        /// #13, 29/10: «siempre tiene la misma pinta»; Gameplay: not all pink): Boss Zombies' two fat bodies, the Blobber and
        /// the Slobber, with their hairs (and the Slobber's beard), in their pack's atlases, whose patterns differ.
        /// </summary>
        static DeadBody Bloater(string body, string atlas, params string[] keep) => new DeadBody { prefab = "SM_Chr_ZombieBoss_" + body + "_01", body = "SM_Chr_ZombieBoss_" + body + "_01", scale = 1.08f, keep = keep, atlas = atlas };
        static readonly Dictionary<string, DeadBody[]> DeadBodies = new Dictionary<string, DeadBody[]>
        {
            ["xenoid"] = new[] { City("Jock_Male_01"), City("Cheerleader_Female_01", true), City("Hoodie_Male_01"), City("SchoolGirl_Female_01", true) },
            ["mec"] = new[] { City("RiotCop_Male_01", false, 1.06f), City("Police_Male_01", false, 1.06f), City("Firefighter_Male_01", false, 1.06f) },
            ["sectoid"] = new[] { new DeadBody { prefab = "SM_Chr_ZombieBoss_Wretch_01", body = "SM_Chr_ZombieBoss_Wretch_01", scale = 1.12f, keep = new[] { WretchHair } } },
            ["bloater"] = new[]
            {
                Bloater("Blobber", null, "SM_Chr_Attach_Blobber_Hair_01"),
                Bloater("Slobber", null, "SM_Chr_Attach_Slobber_Hair_01", "SM_Chr_Attach_Slobber_FacialHair_01"),
                Bloater("Blobber", "PolygonZombieBoss_03_B", "SM_Chr_Attach_Blobber_Hair_02"),
                Bloater("Slobber", "PolygonZombieBoss_02_C", "SM_Chr_Attach_Slobber_Hair_01"),
                Bloater("Blobber", "PolygonZombieBoss_04_A", "SM_Chr_Attach_Blobber_Hair_01"),
            },
        };

        /// <summary>
        /// The City Zombies among the common dead in their own clothes (Arturo, 9/11: the twenty Biblioteca proposed, «sueltos
        /// en la multitud»; but their policewoman, who is the police's, CrowdCops): one in CityShare of them, by its id
        /// (FromCity), dealt as Sidekick's are among the rest. Their 4–7 thousand vertices to a Sidekick dead's 21 thousand.
        /// </summary>
        static readonly DeadBody[] Crowd =
        {
            City("BioHazardSuit_Male_01"), City("Prisoner_Male_01", body: "Zombie_Prisinor_Male_01"), City("Paramedic_Female_01", true), City("Biker_Male_01"), City("Roadworker_Male_01"),
            City("Hobo_Male_01"), City("Businessman_Male_01"), City("Jacket_Male_01"), City("Jacket_Female_01", true), City("Coat_Female_01", true),
            City("Grandpa_Male_01"), City("Grandma_Female_01", true), City("ShopKeeper_Male_01"), City("Patient_Female_01", true), City("Punk_Male_01"),
            City("Bride_Female_01", true), City("Footballer_Male_01"), City("Mother_Female_01", true), City("Father_Male_01"),
        };

        /// <summary>City Zombies' policewoman, dealt with Sidekick's policemen to the common dead in a policeman's uniform.</summary>
        static readonly DeadBody[] CrowdCops = { City("Police_Female_01", true) };

        /// <summary>One in this many of the common dead in their own clothes is City Zombies' (Crowd), by its id.</summary>
        const int CityShare = 3;

        /// <summary>Whether one of the common dead in their own clothes is drawn as City Zombies' (Crowd), by its id: on every screen the same.</summary>
        public static bool FromCity(string id) => !NewDeadOff && Fnv("city|" + id) % CityShare == 0;

        /// <summary>
        /// Labs: the common dead as 0.12.14 found them (BASTION_SIN_MUERTOS_NUEVOS=1: no City Zombies in the crowd nor among the
        /// police, no patients, none of UndeadFigure.Ragged), the strips' before.
        /// </summary>
        public static bool NewDeadOff = System.Environment.GetEnvironmentVariable("BASTION_SIN_MUERTOS_NUEVOS") == "1";

        /// <summary>
        /// Where Horror Asylum's atlases have a living one's skin (the swatches its people's faces and hands paint from, with
        /// their lips: 0–170 × 3206–3454 px of 4096), made the dead's (SoldierPalette.DullAtlas).
        /// </summary>
        static readonly Vector4 AsylumSkin = new Vector4(0, 0.1567f, 0.0415f, 0.2173f);

        /// <summary>
        /// A Horror Asylum character by its name (Patient_Male_01: PolygonHorrorAsylum/SM_Chr_Patient_Male_01, by its pack's
        /// folder, Apocalypse has a SM_Chr_Patient_Female_01 of its own; its body of the same name), with what it keeps on.
        /// </summary>
        static DeadBody Asylum(string name, bool female, params string[] keep) =>
            new DeadBody { prefab = "PolygonHorrorAsylum/SM_Chr_" + name, body = "SM_Chr_" + name, female = female, keep = keep, living = AsylumSkin };

        /// <summary>
        /// The common dead in a patient's gown (Unit.Outfit patient: who started in a clinic's or a hospital's rooms; Arturo,
        /// 9/11): Horror Asylum's four patients, its orderly and its nurse, their skin made the dead's, and City Zombies'
        /// patient and paramedic.
        /// </summary>
        static readonly DeadBody[] Patients =
        {
            Asylum("Patient_Male_01", false, "SM_Chr_Attach_Hair_03", "SM_Chr_Attach_Mask_03"), Asylum("Patient_Male_02", false), Asylum("Patient_Male_03", false, "SM_Chr_Attach_Hair_05"),
            Asylum("Patient_Female_01", true, "SM_Chr_Attach_Hair_04"), Asylum("Orderly_01", false, "SM_Chr_Attach_Hair_08", "SM_Chr_Attach_Glasses_03"),
            Asylum("Nurse_01", true, "SM_Chr_Attach_Hat_01", "SM_Chr_Attach_Earrings_01", "SM_Chr_Attach_Mask_02"),
            City("Patient_Female_01", true), City("Paramedic_Female_01", true),
        };

        /// <summary>The atlases the special dead wear instead of their own (FigureCatalogMake puts them in the catalog).</summary>
        public static IEnumerable<string> DeadAtlases => DeadBodies.Values.SelectMany(b => b).Select(b => b.atlas).Where(a => a != null).Distinct();

        /// <summary>
        /// Each pack material without the blood its shader lays over the atlas by a mask (PolygonGeneric's «Bloody» shaders:
        /// City Zombies' _Blood_Mask, Boss Zombies' _Blood_Map); 0.10.1 drew the atlas alone, its own stains in it. And its
        /// atlas's colours dulled as the common dead's are (SoldierPalette.DullAtlas; Ana's #20, 29/10: their blue, purple
        /// and green skins). A copy each.
        /// </summary>
        static readonly Dictionary<(Material, Texture, Vector4), Material> bloodless = new Dictionary<(Material, Texture, Vector4), Material>();

        static readonly string[] BloodMasks = { "_Blood_Mask", "_Blood_Map" };

        /// <summary>
        /// `m` without its blood, its atlas dulled (`flesh`: Boss Zombies', their skin's swatch as skin; `living`, a living
        /// one's skin swatch made the dead's: Horror Asylum's, whose material has no blood); in `atlas` instead of its own if
        /// given (a Hinchado's other).
        /// </summary>
        static Material Bloodless(Material m, Texture atlas = null, bool flesh = false, Vector4 living = default)
        {
            if (m == null || !BloodMasks.Any(m.HasProperty) && (living == default || !m.HasProperty("_Albedo_Map"))) return m;
            if (!bloodless.TryGetValue((m, atlas, living), out var copy) || copy == null)
            {
                copy = new Material(m) { name = m.name + " (sin sangre)" };
                foreach (string mask in BloodMasks)
                    if (copy.HasProperty(mask)) copy.SetTexture(mask, Texture2D.blackTexture);
                if (copy.HasProperty("_Albedo_Map")) copy.SetTexture("_Albedo_Map", SoldierPalette.DullAtlas(atlas != null ? atlas : copy.GetTexture("_Albedo_Map"), flesh, living));
                bloodless[(m, atlas, living)] = copy;
            }
            return copy;
        }

        /// <summary>
        /// The common dead who were soldiers (Unit.Outfit military; Arturo's A9 of 2/11): City Zombies' soldier at the common
        /// one's size. Sidekick has none: its police and civilians are the others' (UnitViews.Bodies).
        /// </summary>
        static readonly DeadBody[] Soldiers = { City("Military_Male_01") };

        /// <summary>The prefabs the special dead, the soldiers, the crowd's and the patients are drawn from (FigureCatalogMake puts them in the catalog's characters).</summary>
        public static IEnumerable<string> DeadPrefabs => DeadBodies.Values.SelectMany(b => b).Concat(Soldiers).Concat(Crowd).Concat(CrowdCops).Concat(Patients).Select(b => b.prefab).Distinct();

        /// <summary>A special one's figure, dealt among its kind's (Dealt; its build and size in `view`); null if it isn't one or its prefab is missing.</summary>
        static GameObject DeadFigureOf(Unit u, View view, FigureCatalog catalog) =>
            DeadBodies.TryGetValue(u.Template, out var bodies) ? DeadFigureOf(bodies, view, catalog) : null;

        /// <summary>One of `bodies` (POLYGON's dead), dealt (Dealt; its build and size in `view`); null if its prefab is missing.</summary>
        static GameObject DeadFigureOf(DeadBody[] bodies, View view, FigureCatalog catalog) =>
            DeadFigure(bodies[Dealt(bodies.Length, view, i => DealtId(bodies[i]))], view, catalog);

        /// <summary>Which it is, in a deck (Dealt).</summary>
        static string DealtId(DeadBody b) => b.prefab + "|" + b.body + "|" + b.atlas;

        /// <summary>That one of POLYGON's dead (its build and size in `view`); null if its prefab is missing.</summary>
        static GameObject DeadFigure(DeadBody b, View view, FigureCatalog catalog)
        {
            var prefab = catalog.Character(b.prefab);
            if (prefab == null) return null;
            var figure = Instantiate(prefab);
            figure.name = b.prefab;
            // Its body and what it keeps; the prefab's other bodies and attachments gone.
            var atlas = b.atlas != null ? catalog.Atlas(b.atlas) : null;
            foreach (var r in figure.GetComponentsInChildren<Renderer>(true).ToList())
            {
                bool kept = r.gameObject.name == b.body || b.keep != null && b.keep.Contains(r.gameObject.name);
                r.gameObject.SetActive(kept);
                if (!kept && r.gameObject != figure) DestroyNow(r.gameObject);
                else r.sharedMaterials = r.sharedMaterials.Select(m => Bloodless(m, atlas, b.prefab.StartsWith("SM_Chr_ZombieBoss_"), b.living)).ToArray();
            }
            view.build = b.female ? "femn" : "masc";
            view.size = b.scale;
            return figure;
        }

        /// <summary>
        /// The Chillón's scream (unitViews.ts scream: its aim pose): the trunk and the head thrown back and both arms flung
        /// out and back, in over ScreamIn and held ScreamHold; done then, it lets go over ScreamOut more (the rings are
        /// Fx.Scream). With Synty's roar (Emotes and Taunts' «Roar High», roarHighMasc: the 3D review's A-28, 10/11), its arms
        /// up and its head back are the clip's, RoarFrom to RoarTo of it over the scream's 1.03 s on the upper body, and
        /// the lean back half (DeadPose): by hand, its arms rose 20 cm and its head stayed under its hair.
        /// </summary>
        public async Awaitable Scream(string id, CancellationToken ct = default)
        {
            if (!views.TryGetValue(id, out var v))
            {
                await Awaitable.WaitForSecondsAsync(ScreamIn + ScreamHold, ct);
                return;
            }
            var roar = v.anim != null ? FigureCatalog.Load()?.Clip(RoarClip) : null;
            v.roar = roar != null;
            void Roar(float t)
            {
                if (roar != null) v.anim.Scrub(roar, RoarFrom + (RoarTo - RoarFrom) * Mathf.Clamp01(t / (ScreamIn + ScreamHold + ScreamOut)), v.scream);
            }
            float t = 0;
            for (; t < ScreamIn; t += Time.deltaTime)
            {
                v.scream = Mathf.SmoothStep(0, 1, t / ScreamIn);
                Roar(t);
                await Awaitable.NextFrameAsync(ct);
            }
            v.scream = 1;
            for (; t < ScreamIn + ScreamHold; t += Time.deltaTime)
            {
                Roar(t);
                await Awaitable.NextFrameAsync(ct);
            }
            LetGo();
            async void LetGo()
            {
                try
                {
                    for (float u = 0; u < ScreamOut && v.scream > 0; u += Time.deltaTime)
                    {
                        v.scream = 1 - Mathf.SmoothStep(0, 1, u / ScreamOut);
                        Roar(ScreamIn + ScreamHold + u);
                        await Awaitable.NextFrameAsync();
                    }
                }
                finally
                {
                    v.scream = 0;
                    if (roar != null) v.anim?.Scrub(null, 0, 0);
                    v.roar = false;
                }
            }
        }

        /// <summary>The scream's in, held and out (seconds: unitViews.ts scream's 0.18, 0.55 and 0.3).</summary>
        const float ScreamIn = 0.18f, ScreamHold = 0.55f, ScreamOut = 0.3f;

        /// <summary>
        /// The Chillón's roar (A-28): Synty's «Roar High» (a man's, as the Chillón's body is; Emotes and Taunts, in by
        /// importar-synty.sh), the part of it with its hands highest and its head furthest back, seconds (ClipProbe.Alone
        /// BASTION_MANOS=1 BASTION_CABEZA=1: crouched to wind up at 0.6–0.8 s, both hands up past 1.9 m from 1.2 s to 2.5,
        /// the head furthest back at 1.3–1.4).
        /// </summary>
        const string RoarClip = "roarHighMasc";
        const float RoarFrom = 1.2f, RoarTo = 1.9f;

        /// <summary>
        /// What each kind of the dead adds to its clips (syntyFigures.ts STYLES, gaits.ts): how far it leans forward (radians),
        /// how much its hips roll and its shoulders twist. The common dead drag themselves hunched; the Corredor goes bent;
        /// the Acorazado stands straighter and sways heavily; the Hinchado wobbles; the Chillón as any. And how much more it
        /// leans moving (`moving`, Unity's own): Goblin Locomotion's walk and run through Mecanim go straighter than through
        /// 0.10.1's retarget, by body (26/10, the trunk from the hips to the neck, the same body each side: the common
        /// zombie 70.7° against 82.0°, the Hinchado 40.6° against 61.9°, the Corredor 59.1° against 64.1°; standing, and the
        /// Acorazado and the Chillón moving, within 3°). A lean shows four fifths of itself in the trunk.
        /// </summary>
        static readonly Dictionary<string, (float lean, float roll, float twist, float moving)> DeadStyles = new Dictionary<string, (float, float, float, float)>
        {
            ["zombie"] = (0.5f, 1.5f, 1.3f, 0.25f),
            ["xenoid"] = (0.55f, 0.6f, 0.6f, 0.11f),
            ["mec"] = (0.2f, 1.7f, 0.4f, 0),
            ["bloater"] = (0.12f, 2.6f, 0.8f, 0.46f),
            ["sectoid"] = (0.38f, 1, 1, 0),
        };

        /// <summary>syntyFigures.ts CADENCE: steps a second of each motion (MOTION_ORDER) at its own speed, the hips' roll once a stride (π each).</summary>
        static readonly float[] RollRates = { 0, 0, Mathf.PI * 1.9f, Mathf.PI * 2.9f, Mathf.PI * 1.9f, Mathf.PI * 3.3f };
        static readonly float[] TwistRates = { 1.7f, 1.7f, 1.7f, 1.7f, 1.7f, 1.7f };

        /// <summary>
        /// One of the dead over what its clips have it in (syntyFigures.ts applyPose, for a body moving by its clips): its
        /// lean spread up the spine (0.35, 0.7 and all of it by the bone, each turned in the body's frame from the clip's
        /// own), the hips rolling and the shoulders twisting in step with its clips, the neck lolled to the side and back
        /// up by 0.55 of the lean so the eyes stay up; the legs as the clip has them. The Chillón's scream (`v.scream`)
        /// throws the lean back half a radian and the neck 0.6 more, and the arms out. None while it dies, nor while a clip
        /// takes the whole body (`live`).
        /// </summary>
        static void DeadPose(View v)
        {
            var a = v.animator;
            if (a == null || !a.isHuman || v.anim == null) return;
            // (Eased off as its death comes in, life, not gone in its first frame: the 3D review's A-04, 10/11. And as a clip
            // takes the whole body, a bite, going down to eat, eating, getting up, rising out of a body: the lean is a body
            // standing's, and over «Zombie Biting» it laid the chest flat on the body, over a body lying it bent it into the
            // floor or up like a board; back with the clip's fade out. The 3D review's M-2, 10/11. The scream keeps its own.)
            float live = v.life * (1 - v.anim.WholeShare), k = v.scream;
            if (live <= 0 && k <= 0) return;
            var style = DeadStyles.TryGetValue(v.template, out var s) ? s : DeadStyles["sectoid"];
            // (With the roar's clip its own trunk goes back: the lean back halved, not bent twice. A-28, 10/11.)
            float lean = (style.lean + style.moving * v.anim.Moving) * live - k * (v.roar ? 0.25f : 0.5f);
            float twist = v.anim.Sway(1.7f, TwistRates) * 0.12f * style.twist * live;
            float roll = v.anim.Sway(1.3f, RollRates) * (0.05f + v.anim.Moving * 0.08f) * style.roll * live;
            var body = v.root.rotation;
            // A turn in the body's frame (rot Z by roll, then Y by twist, then X by lean: forward is a lean over).
            Quaternion Turn(float r, float t, float l) => Quaternion.AngleAxis(r * Mathf.Rad2Deg, Vector3.forward) * Quaternion.AngleAxis(t * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(l * Mathf.Rad2Deg, Vector3.right);
            Quaternion World(Quaternion turn) => body * turn * Quaternion.Inverse(body);
            Transform hips = a.GetBoneTransform(HumanBodyBones.Hips), legL = a.GetBoneTransform(HumanBodyBones.LeftUpperLeg), legR = a.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            var spine = new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest }.Select(a.GetBoneTransform).Where(b => b != null).ToList();
            Transform neck = a.GetBoneTransform(HumanBodyBones.Neck) ?? a.GetBoneTransform(HumanBodyBones.Head);
            // The clip's turns, before any is changed (each bone is set from its own: the body's frame, as 0.10.1's).
            Quaternion? wasL = legL != null ? legL.rotation : (Quaternion?)null, wasR = legR != null ? legR.rotation : (Quaternion?)null;
            var was = spine.Select(b => b.rotation).ToList();
            var wasNeck = neck != null ? neck.rotation : Quaternion.identity;
            if (hips != null) hips.rotation = World(Turn(roll, 0, 0)) * hips.rotation;
            if (legL != null) legL.rotation = wasL.Value;
            if (legR != null) legR.rotation = wasR.Value;
            // (0.10.1's three: 0.35, 0.7 and 1 of the lean, 0.3, 0.6 and 1 of the twist; fewer bones, the last takes all.)
            float[] leans = spine.Count >= 3 ? new[] { 0.35f, 0.7f, 1f } : spine.Count == 2 ? new[] { 0.5f, 1f } : new[] { 1f };
            float[] twists = spine.Count >= 3 ? new[] { 0.3f, 0.6f, 1f } : spine.Count == 2 ? new[] { 0.5f, 1f } : new[] { 1f };
            var upper = Quaternion.identity;
            for (int i = 0; i < spine.Count; i++)
            {
                upper = Turn(roll, twist * twists[i], lean * leans[i]);
                spine[i].rotation = World(upper) * was[i];
            }
            if (neck != null)
            {
                // (Roaring, the clip throws the head back itself: the neck's own throw a third, A-28.)
                var head = upper * Quaternion.AngleAxis(0.32f * live * Mathf.Rad2Deg, Vector3.forward) * Quaternion.AngleAxis((-lean * 0.55f - k * (v.roar ? 0.2f : 0.6f)) * Mathf.Rad2Deg, Vector3.right);
                neck.rotation = World(head) * wasNeck;
            }
            DeadArms(v);
            if (k <= 0 || v.roar) return;
            // (0.10.1's model x is to its left, Unity's −x, and its z behind it, Unity's −z: its own screams, seen from
            // the game's camera and from the side, have the arms out wide and ahead of it.)
            FlingArm(a, v.root, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, new Vector3(-0.85f, -0.15f, 0.4f), new Vector3(-0.8f, 0.25f, 0.35f), k);
            FlingArm(a, v.root, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, new Vector3(0.85f, -0.15f, 0.4f), new Vector3(0.8f, 0.25f, 0.35f), k);
        }

        /// <summary>The bone 0.10.1 calls Spine_03: the upper chest, or the chest where there is none.</summary>
        static Transform ChestOf(Animator a) => a.GetBoneTransform(HumanBodyBones.UpperChest) ?? a.GetBoneTransform(HumanBodyBones.Chest);

        /// <summary>syntyFigures.ts SOFT_REACH: the last of an arm's reach eased, so the elbow bends in smoothly (m, the model's).</summary>
        const float SoftReach = 0.025f;
        /// <summary>Seconds the dead's own pose (its lean, its arms out) takes to go as its death comes in (View.life; the 3D review's A-04).</summary>
        const float DeathEase = 0.3f;
        /// <summary>The least of an arm's length a dead one's hand reaches out to (DeadArms).</summary>
        const float StraightArm = 0.985f;

        /// <summary>
        /// syntyFigures.ts applyPose, the dead: arms out in front, swaying, raised further on a lunge and closed on its blow;
        /// each hand 0.4 ahead of the upper chest and 0.02 under it (the model's metres, its rest), 0.12 out to its side,
        /// where the chest has turned since its rest; reached by two-bone IK, the elbow out, down and back (the pole). Not
        /// while a clip takes the whole body (a bite, eating, getting up) nor fallen.
        /// </summary>
        /// <summary>For the labs: the dead's arms left as their clips have them (to tell what DeadArms does).</summary>
        public static bool DeadArmsOff;

        static void DeadArms(View v)
        {
            var a = v.animator;
            // (Dying, let down over DeathEase as its death comes in, life: not cut in the first frame, the death's clip over
            // the whole body from then on; the 3D review's A-04, 10/11.)
            if (DeadArmsOff || v.feeding || v.chestRest is not Quaternion rest || (!v.dead && v.anim.WholeShare > 0.01f) || v.life <= 0) return;
            // (Roaring, the arms the roar's as far as it is in: A-28.)
            float keep = v.life * (v.roar ? 1 - v.scream : 1);
            if (keep <= 0) return;
            var chest = ChestOf(a);
            if (chest == null) return;
            float scale = v.body != null ? v.body.lossyScale.x : 1;
            var turn = chest.rotation * Quaternion.Inverse(v.root.rotation * rest);
            float aim = v.reachAim, grab = v.reachGrab;
            foreach (var (s, upper, lower, hand) in new[] { (1f, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand), (-1f, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand) })
            {
                float sway = v.anim.Sway(1.6f, ArmRates, s) * 0.05f;
                // (0.10.1's model x is to its left: Unity's −x.)
                var offset = new Vector3(-s * 0.12f * (1 - 0.7f * grab), -0.02f + aim * 0.12f + sway + grab * 0.05f, 0.4f + aim * 0.08f + grab * 0.12f) * scale;
                var target = chest.position + turn * (v.root.rotation * offset);
                var pole = v.root.rotation * new Vector3(-s * 0.8f, -0.7f, -0.3f);
                Transform s0 = a.GetBoneTransform(upper), s1 = a.GetBoneTransform(lower), s2 = a.GetBoneTransform(hand);
                if (s0 == null || s1 == null || s2 == null) continue;
                var was = (s0.rotation, s1.rotation, s2.rotation);
                Reach(a, upper, lower, hand, target, pole, SoftReach * scale, true);
                if (keep >= 1) continue;
                // As much of the reach as is left of its life (and of a roar), from the shoulder down (each turn in the
                // world, the parent's first).
                var now = (s0.rotation, s1.rotation, s2.rotation);
                s0.rotation = Quaternion.Slerp(was.Item1, now.Item1, keep);
                s1.rotation = Quaternion.Slerp(was.Item2, now.Item2, keep);
                s2.rotation = Quaternion.Slerp(was.Item3, now.Item3, keep);
            }
        }

        static readonly float[] ArmRates = { 1.6f, 1.6f, 1.6f, 1.6f, 1.6f, 1.6f };

        /// <summary>
        /// Two-bone IK (syntyFigures.ts reach): the hand to `target`, the elbow bent towards `pole`, the arm's lengths kept;
        /// `straight`, out at least StraightArm of its length (the dead's arms).
        /// </summary>
        static void Reach(Animator a, HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones handBone, Vector3 target, Vector3 pole, float soft, bool straight = false)
        {
            Transform shoulder = a.GetBoneTransform(upperBone), elbow = a.GetBoneTransform(lowerBone), hand = a.GetBoneTransform(handBone);
            if (shoulder == null || elbow == null || hand == null) return;
            float upperLength = Vector3.Distance(shoulder.position, elbow.position), lowerLength = Vector3.Distance(elbow.position, hand.position);
            var dir = target - shoulder.position;
            // (Out as far as the arm goes, near enough straight, as 0.10.1's dead hold them, 160–173° at the elbow, whatever the
            // body: City Zombies' and Boss Zombies' arms are longer than the 0.4 ahead reaches for a Sidekick's.)
            if (straight && dir.magnitude < (upperLength + lowerLength) * StraightArm) dir = dir.normalized * (upperLength + lowerLength) * StraightArm;
            float full = upperLength + lowerLength, len = dir.magnitude;
            if (len > full - soft) len = full - soft * Mathf.Exp(-(len - (full - soft)) / Mathf.Max(soft, 1e-5f));
            len = Mathf.Clamp(len, Mathf.Abs(upperLength - lowerLength) + 0.01f, full - 0.001f);
            dir.Normalize();
            float cos = Mathf.Clamp((upperLength * upperLength + len * len - lowerLength * lowerLength) / (2 * upperLength * len), -1, 1);
            // The elbow in the plane of the arm and the pole, on the pole's side.
            var side = Vector3.ProjectOnPlane(pole, dir);
            if (side.sqrMagnitude < 1e-8f) side = Vector3.ProjectOnPlane(Vector3.down, dir);
            side.Normalize();
            var elbowAt = shoulder.position + (dir * cos + side * Mathf.Sqrt(1 - cos * cos)) * upperLength;
            var handAt = shoulder.position + dir * len;
            shoulder.rotation = Quaternion.FromToRotation(elbow.position - shoulder.position, elbowAt - shoulder.position) * shoulder.rotation;
            elbow.rotation = Quaternion.FromToRotation(hand.position - elbow.position, handAt - elbow.position) * elbow.rotation;
        }

        /// <summary>A lunge's arms (unitViews.ts lunge: aimFx up as it winds up, swing −1 to 1 through the stroke, back over 0.35 s; grab |swing|).</summary>
        static async Awaitable LungeArms(View v, System.Threading.CancellationToken ct)
        {
            try
            {
                float t = 0;
                for (; t < 0.8f && v.root != null; t += Time.deltaTime)
                {
                    // (lunge's own: wound up over 0.2 s, the stroke 0.14 s, back over 0.35 s.)
                    float hit = WindUp + StrokeTime;
                    float wind = Mathf.Clamp01(t / WindUp), stroke = Mathf.Clamp01((t - WindUp) / StrokeTime), back = Mathf.Clamp01((t - hit) / Back);
                    float swing = t < WindUp ? -Mathf.SmoothStep(0, 1, wind) : t < hit ? -1 + 2 * Mathf.SmoothStep(0, 1, stroke) : 1 - Mathf.SmoothStep(0, 1, back);
                    v.reachAim = t < hit ? wind : 1 - back;
                    v.reachGrab = Mathf.Abs(swing);
                    await Awaitable.NextFrameAsync(ct);
                }
            }
            catch (System.OperationCanceledException) { }
            v.reachAim = v.reachGrab = 0;
        }

        /// <summary>syntyFigures.ts setArm, blended in by `k`: the upper arm along `upper`, the forearm along `fore` (the figure's frame).</summary>
        static void FlingArm(Animator a, Transform root, HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones handBone, Vector3 upper, Vector3 fore, float k)
        {
            Transform shoulder = a.GetBoneTransform(upperBone), elbow = a.GetBoneTransform(lowerBone), hand = a.GetBoneTransform(handBone);
            if (shoulder == null || elbow == null || hand == null) return;
            var to = Quaternion.FromToRotation(elbow.position - shoulder.position, root.TransformDirection(upper.normalized));
            shoulder.rotation = Quaternion.Slerp(Quaternion.identity, to, k) * shoulder.rotation;
            to = Quaternion.FromToRotation(hand.position - elbow.position, root.TransformDirection(fore.normalized));
            elbow.rotation = Quaternion.Slerp(Quaternion.identity, to, k) * elbow.rotation;
        }
    }
}
