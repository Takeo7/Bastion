using System.Linq;
using Bastion.Client.Map;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client
{
    /// <summary>
    /// The soldier looked at in Soldados and Personalizar (0.10.1's clearingArt.ts setShowcase), standing on the clearing
    /// by the barracks (CampPlaces), dressed as they look and with the Armería's weapon in their hands, in their idle,
    /// facing its camera and turning a little to either side and back; the fallen, their cross and candle where they
    /// would stand. Built again only when what it shows changed; shown on Soldados alone (SetView).
    /// </summary>
    public sealed partial class Settlement
    {
        Transform showcase;
        string showcaseKey = "";
        Transform showFigure;
        Animator showAnimator;
        FigureAnimator showAnim;
        Transform showGun;
        /// <summary>Whether that is the pistol (their only gun), held by the thigh.</summary>
        bool showPistol;
        Quaternion? showGrip;
        /// <summary>Their pistol at rest kept out of the thigh (UnitViews' PistolSpread), made again with each figure.</summary>
        PistolSpread showSpread = new PistolSpread();

        /// <summary>For the labs: the soldier facing their camera, without their sway (to set beside 0.10.1's at the same turn).</summary>
        public static bool SwayOff;

        /// <summary>For the labs: how far their pistol arm is turned out of the thigh (radians).</summary>
        public float ShowcaseSpread => showSpread.Out;
        float showTime;
        /// <summary>
        /// The player's turn of the soldier, dragging (Ana's #5: TurnShowcase), and where it is going (radians; its front to
        /// the right on the screen); its sway, faded out once they turn it; and whose turn it is (another soldier, none).
        /// </summary>
        float showTurn, showTurnTo, showSway, showSwayK = 1;
        string showTurnOf;

        /// <summary>
        /// A campaign's soldier as Soldados shows them: `look` (Personalizar's draft) or their own, the Armería's weapon in
        /// the hand (Campaign.SoldierArms), fallen if they are dead; null (or unknown), nobody.
        /// </summary>
        public void Showcase(CampaignState campaign, string soldierId, Appearance look = null)
        {
            var s = soldierId == null ? null : campaign?.Soldiers?.FirstOrDefault(x => x.Id == soldierId);
            if (s == null)
            {
                Showcase((string)null, null, null, false);
                return;
            }
            Showcase(s.Id, look ?? s.Appearance, s.Alive ? Campaign.SoldierArms(campaign, s).Primary : null, !s.Alive, s.Name, SquadTint.ColourOf(s.Owner));
        }

        /// <summary>
        /// The soldier `soldierId` as `look` (who has none, a body of their sex by `name`), `weapon` (FoundWeaponIds) in the
        /// hands (none: the pistol, at rest by the thigh); `fallen`, their grave. A null id, nobody.
        /// </summary>
        public void Showcase(string soldierId, Appearance look, string weapon, bool fallen, string name = null, string band = null)
        {
            string key = soldierId == null ? "" : Newtonsoft.Json.JsonConvert.SerializeObject(new object[] { soldierId, look, weapon, fallen, name, band });
            if (key == showcaseKey) return;
            showcaseKey = key;
            // Turned by the player, they stay so while it is them (Personalizar's draft changes the figure, not them).
            if (soldierId != showTurnOf)
            {
                showTurnOf = soldierId;
                showTurn = showTurnTo = 0;
                showSwayK = 1;
            }
            ClearShowcase();
            if (soldierId == null) return;
            if (showcase == null)
            {
                showcase = new GameObject("Soldado de Soldados").transform;
                showcase.SetParent(transform, false);
            }
            showcase.position = World(new Vector3(soldierAt.x, 0, soldierAt.y));
            showcase.rotation = Quaternion.identity;
            // Posed while it is on (an Animator off writes no pose), then shown only on Soldados.
            showcase.gameObject.SetActive(true);
            if (fallen)
            {
                Grave(showcase, showcase.position, Mathf.Atan2(soldierView.x, soldierView.y));
                showcase.gameObject.SetActive(view == Views.Barracks);
                return;
            }
            string sex = look == null || string.IsNullOrEmpty(look.Sex) ? (name != null ? Content.SexOfName(name) : null) : null;
            var dress = Dress.Of(look, soldierId, sex);
            GameObject figure;
            try
            {
                figure = SoldierFigure.Make(dress, "Soldado", true);
                // In their colours and with their player's band if Ajustes says so (SquadTint).
                SquadTint.Paint(figure, look, band);
                FaceLit(figure);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[settlement] {soldierId}: no figure ({e.Message})");
                return;
            }
            if (figure == null) return;
            showFigure = new GameObject("figura").transform;
            showFigure.SetParent(showcase, false);
            figure.transform.SetParent(showFigure, false);
            figure.transform.localPosition = Vector3.zero;
            figure.transform.localRotation = Quaternion.identity;
            figure.transform.localScale = Vector3.one * Figures.CharacterScale;
            showAnimator = figure.GetComponentInChildren<Animator>();
            var catalog = FigureCatalog.Load();
            string build = dress.sex == "f" ? "femn" : "masc";
            string rest = null;
            if (showAnimator != null && showAnimator.isHuman && catalog != null)
            {
                showGun = weapon == null ? UnitViews.Pistol(showFigure, catalog) : UnitViews.Gun(Anims.Weapon(Anims.FoundModel(weapon)), showFigure, catalog, out _);
                showPistol = weapon == null && showGun != null;
                bool armed = showGun != null && !showPistol;
                rest = UnitViews.RestFor(soldierId);
                // As the battle's (UnitViews.Make): a gun in the hands, Mixamo's armed idle where there is one; the pistol
                // by the thigh, its way of waiting (PISTOL_RESTS) the body's.
                string still = showPistol ? Anims.Stance(build, UnitViews.PistolStance(rest)) : null;
                string ArmedId(string m) => !armed ? null : Anims.Armed("rifle", m) is string a && a != "own" ? a : null;
                showAnim = new FigureAnimator(showAnimator, m => catalog.Clip(ArmedId(m) ?? (m == "idle" && still != null ? still : Anims.Motion(build, m, false))), null, m => ArmedId(m) != null);
                if (armed) showAnim.Rest(catalog.Clip(rest), 0);
                if (showPistol) showAnim.ArmRest(catalog.Clip("pistolRest"), UnitViews.PistolHold, 0);
            }
            showTime = 0;
            // Into its pose at once (the clips blend in over a few frames), as it would stand.
            StepShowcase(1.2f);
            showcase.gameObject.SetActive(view == Views.Barracks);
        }

        /// <summary>
        /// The soldier of Soldados and Personalizar turned `degrees` more, as the player drags across them (UI reads the
        /// drag; Ana's #5): positive turns their front to the right on the screen, following a drag to the right. They
        /// follow it smoothly and stay as left, without their sway (taken in where it was), until another soldier is shown.
        /// </summary>
        public void TurnShowcase(float degrees)
        {
            if (showSwayK > 0)
            {
                showTurn += showSway;
                showTurnTo += showSway;
                showSwayK = 0;
            }
            showTurnTo += degrees * Mathf.Deg2Rad;
        }

        /// <summary>
        /// Personalizar open on `soldierId` (with `draft` as it stands; null, their own look): the other outfits of their sex,
        /// in the draft's head and build, made in the background a step a frame, the nearest in the list to the one worn
        /// first, so that the pointer over one shows it at once. Again when the head or the build changes.
        /// </summary>
        public void Prepare(CampaignState campaign, string soldierId, Appearance draft = null)
        {
            var s = soldierId == null ? null : campaign?.Soldiers?.FirstOrDefault(x => x.Id == soldierId);
            if (s == null || !s.Alive) return;
            var look = draft ?? s.Appearance ?? new Appearance();
            string named = string.IsNullOrEmpty(look.Sex) ? Content.SexOfName(s.Name) : null;
            string sex = Dress.SexOf(look, s.Id, named);
            var outfits = Dress.OutfitsFor(sex);
            int worn = outfits.ToList().IndexOf(Dress.Of(look, s.Id, named).outfit);
            var order = outfits.Select((o, i) => (o, d: worn < 0 ? i : Mathf.Abs(i - worn))).OrderBy(x => x.d).Select(x => x.o);
            var dresses = order.Select(outfit =>
            {
                var copy = Newtonsoft.Json.JsonConvert.DeserializeObject<Appearance>(Newtonsoft.Json.JsonConvert.SerializeObject(look));
                copy.Character = outfit;
                return Dress.Of(copy, s.Id, named);
            }).ToList();
            SoldierFigure.Prepare(dresses, true);
        }

        void ClearShowcase()
        {
            showAnim?.Dispose();
            showAnim = null;
            showAnimator = null;
            showGun = null;
            showPistol = false;
            showGrip = null;
            showSpread = new PistolSpread();
            showFigure = null;
            if (showcase != null)
                foreach (Transform child in showcase.Cast<Transform>().ToList()) DestroyNow(child.gameObject);
        }

        /// <summary>Its turn to the camera and a little either way (clearingArt.ts showcaseYaw: «turning round and round, it stood with its back to Personalizar half the time»), its pose, the gun in its hands.</summary>
        void StepShowcase(float dt)
        {
            if (showFigure == null) return;
            showTime += dt;
            showSway = SwayOff ? 0 : Mathf.Sin(Time.realtimeSinceStartup * 0.4f) * 0.45f * showSwayK;
            showTurn += (showTurnTo - showTurn) * (1 - Mathf.Exp(-14 * dt));
            showFigure.localRotation = Quaternion.Euler(0, (Mathf.Atan2(soldierView.x, -soldierView.y) - showSway - showTurn) * Mathf.Rad2Deg, 0);
            if (showAnim != null)
            {
                if (dt <= 0) showAnim.Step(0);
                for (float left = dt; left > 1e-5f; left -= 0.1f) showAnim.Step(Mathf.Min(0.1f, left));
                if (showPistol)
                {
                    UnitViews.HoldPistol(showAnimator, showGun, showFigure, 0, ref showGrip);
                    showSpread.Apply(showAnimator, showAnim, showGun, dt);
                }
                else UnitViews.Hold(showAnimator, showGun, showFigure, false, ref showGrip);
            }
        }

        /// <summary>
        /// The fallen's cross and candle (clearingArt.ts grave) under `parent` at `at` (Unity's world), its arms shown the
        /// way `facing` looks (0.10.1's radians from +z, its z south).
        /// </summary>
        Transform Grave(Transform parent, Vector3 at, float facing)
        {
            var grave = new GameObject("tumba").transform;
            grave.SetParent(parent, false);
            grave.position = at;
            var art = MapArt.Prop("bCross");
            string model = MapArt.ModelKey(art);
            var prefab = model != null ? prefabOf?.Invoke(model) : null;
            if (prefab != null)
            {
                var holder = new GameObject("cruz").transform;
                holder.SetParent(grave, false);
                var cross = Instantiate(prefab, holder, false);
                var renderers = cross.GetComponentsInChildren<Renderer>();
                // Its height from foot to top, and its foot on the ground: Synty's prefab stands on its middle (measured to
                // the top from there, it came out twice the size and half underground; Ana, 28/10: «muy grandes»).
                float bottom = renderers.Length > 0 ? renderers.Min(r => r.bounds.min.y) - holder.position.y : 0;
                float top = renderers.Length > 0 ? renderers.Max(r => r.bounds.max.y) - holder.position.y : 1;
                float k = 0.95f * MapSpace.Tile / Mathf.Max(top - bottom, 1e-3f);
                holder.localScale = Vector3.one * k;
                holder.localPosition = Vector3.up * (-bottom * k);
                // Synty's cross has its arms along z: turned a quarter, it shows them to the camera (not edge on, a bare post).
                holder.localRotation = Quaternion.Euler(0, -(facing + Mathf.PI / 2) * Mathf.Rad2Deg + 90 * MapArt.UnityTurn(art), 0);
            }
            var candle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            candle.name = "vela";
            DestroyNow(candle.GetComponent<Collider>());
            candle.transform.SetParent(grave, false);
            candle.transform.localScale = new Vector3(0.07f, 0.12f, 0.07f) * MapSpace.Tile;
            candle.transform.localPosition = new Vector3(Mathf.Sin(facing) * 0.25f, 0.06f, -Mathf.Cos(facing) * 0.25f) * MapSpace.Tile;
            candle.GetComponent<Renderer>().sharedMaterial = CandleMaterial();
            return grave;
        }

        static Material candleMaterial;

        /// <summary>clearingArt.ts CANDLE: dark wax, its flame's glow (0xffb050 at 2.2).</summary>
        static Material CandleMaterial()
        {
            if (candleMaterial != null) return candleMaterial;
            candleMaterial = KitMaterials.New(KitMaterials.Glowing, "Vela");
            if (candleMaterial == null) return null;
            candleMaterial.SetColor("_BaseColor", new Color(0x2a / 255f, 0x1a / 255f, 0));
            candleMaterial.SetColor("_EmissionColor", new Color(1, 0xb0 / 255f, 0x50 / 255f).linear * 2.2f);
            return candleMaterial;
        }
    }
}
