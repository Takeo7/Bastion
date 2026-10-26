using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;

namespace Bastion.UI.Strategy
{
    /// <summary>What Soldados has open, which only this screen knows (screen.ts: soldier, picking).</summary>
    public sealed class SoldiersPick
    {
        /// <summary>The soldier whose sheet is open; null, the first of one's own (set to the one shown).</summary>
        public string Soldier;
        /// <summary>Personalizar open on that soldier: its draft, in place of the sheet; null, the sheet.</summary>
        public CustomDraft Draft;
    }

    /// <summary>Personalizar's draft (screen.ts custom): saved only when the player says so.</summary>
    public sealed class CustomDraft
    {
        public string Soldier;
        public string Name;
        public string Nickname;
        public Appearance Look;
        /// <summary>«Copiar a todos mis soldados» pressed once: the next press paints them.</summary>
        public bool CopyArmed;

        /// <summary>A look's copy, to change without touching the campaign's.</summary>
        public static Appearance Copy(Appearance a) => a == null ? null : new Appearance
        {
            Primary = a.Primary, Secondary = a.Secondary, Visor = a.Visor, Head = a.Head, Build = a.Build, Sex = a.Sex,
            Helmet = a.Helmet, Torso = a.Torso, Arms = a.Arms, Legs = a.Legs, Character = a.Character,
        };
    }

    public sealed partial class StrategyPresenter
    {
        // ------------------------------------------------------------ in missions

        /// <summary>Days counted in missions (screen.ts missionsFor): each takes DAYS_PER_MISSION, a day left is one more.</summary>
        static int MissionsFor(int days) => System.Math.Max(1, (int)System.Math.Ceiling((double)days / Campaign.DaysPerMission));

        /// <summary>«1 misión», «3 misiones».</summary>
        static string MissionsText(int n) => n == 1 ? "1 misión" : $"{n} misiones";

        /// <summary>How far off `day` is, for a chip: «ahora», «próxima», «en 3».</summary>
        static string AheadChip(CampaignState c, int day)
        {
            var left = day - c.Day;
            if (left <= 0) return "ahora";
            var n = MissionsFor(left);
            return n == 1 ? "próxima" : $"en {n}";
        }

        /// <summary>The same in words: «ya», «tras la próxima misión», «dentro de 3 misiones».</summary>
        static string AheadText(CampaignState c, int day)
        {
            var left = day - c.Day;
            return left <= 0 ? "ya" : Lore.InMissions(MissionsFor(left));
        }

        /// <summary>Whether this player gives orders to `owner`'s soldiers (commandsFor): its own, and the partner's while away.</summary>
        bool Commands(int owner) => Content.CommandsFor(Slot, owner, Connected);

        static bool Female(CampaignSoldier s) => s.Appearance?.Sex == "f";

        static float[] SkillsOf(CampaignSoldier s) => SkillIds.All.Select(id => (float)(s.Skills.TryGetValue(id, out var v) ? v : 0)).ToArray();

        /// <summary>Why spending the common stores waits, with the other one here (pendingWhy); '' otherwise.</summary>
        string PendingWhy(CampaignState c)
        {
            if (!Voting || c.Proposal == null) return "";
            return c.Proposal.By == Slot ? $"Esperando a que {Partner.Name} conteste." : "Hay una propuesta pendiente: contesta arriba.";
        }

        /// <summary>«Faltan 20 suministros», or '' when it is affordable (shortfall).</summary>
        static string Shortfall(CampaignState c, int cost)
        {
            var missing = cost - c.Credits;
            return missing > 0 ? $"Faltan {missing} suministros" : "";
        }

        static string First(params (bool on, string text)[] checks) => checks.FirstOrDefault(x => x.on && !string.IsNullOrEmpty(x.text)).text;

        // -------------------------------------------------------------- Soldados

        /// <summary>
        /// Soldados (renderBarracks): one's own soldiers by their faces, the recruit's card, the partner's while away (to
        /// prepare them); on the right the picked one's sheet. Personalizar is the screen's own (CustomizerView), not here.
        /// </summary>
        public SoldiersView Soldiers(SoldiersPick pick)
        {
            pick = pick ?? new SoldiersPick();
            var c = State;
            // With a Camposanto the fallen rest there, out of this list.
            bool Listed(CampaignSoldier s) => s.Alive || Campaign.FacilityCount(c, "memorial") == 0;
            IEnumerable<CampaignSoldier> Order(IEnumerable<CampaignSoldier> list) =>
                list.OrderByDescending(s => s.Alive ? 1 : 0).ThenByDescending(s => s.Board.Count).ThenByDescending(s => Bars.TotalBars(s.Skills));
            var mine = Order(c.Soldiers.Where(s => s.Owner == Slot && Listed(s))).ToList();
            // The other player's only while they are away, to prepare them (Ana, 21/10 #30: each sees their own).
            var theirs = Order(c.Soldiers.Where(s => s.Owner != Slot && Listed(s) && Commands(s.Owner))).ToList();
            var picked = pick.Soldier;
            if (picked == null || !mine.Concat(theirs).Any(s => s.Id == picked))
                picked = mine.FirstOrDefault(s => s.Alive)?.Id ?? mine.FirstOrDefault()?.Id ?? theirs.FirstOrDefault()?.Id;
            pick.Soldier = picked;
            var v = new SoldiersView { MineHead = "Tus soldados" };
            foreach (var s in mine) v.Mine.Add(Tile(c, s, picked));
            if (theirs.Count > 0)
            {
                v.PartnerHead = $"De {PlayerName(theirs[0].Owner)}";
                v.PartnerNote = "no está: los preparas tú";
                foreach (var s in theirs) v.Partner.Add(Tile(c, s, picked));
            }
            v.Recruit = Recruit(c, mine.Count(s => s.Alive));
            var soldier = c.Soldiers.FirstOrDefault(s => s.Id == picked);
            if (soldier != null && pick.Draft != null && pick.Draft.Soldier == soldier.Id) v.Customizer = Customizer(soldier, pick.Draft);
            else if (soldier != null) v.Sheet = Sheet(c, soldier);
            return v;
        }

        /// <summary>
        /// Personalizar (screen.ts customizer): name and nickname; Ropa (Arte's wardrobe: «Al azar» and the outfits of the
        /// soldier's sex), Cabeza and Complexión; a colour for the whole armour, each piece (which follows the armour, or
        /// the trim for a beret, cap or hood, until it has its own: «↺ como…»), the trim, and the player's mark, always in
        /// their colour; Aleatorio, Restablecer, «Copiar a todos mis soldados» (asked twice), Guardar with its why.
        /// </summary>
        CustomizerView Customizer(CampaignSoldier s, CustomDraft d)
        {
            var c = State;
            var look = d.Look;
            var v = new CustomizerView
            {
                Kicker = $"Personalizar · {Campaign.VeterancyName(s)}",
                Title = Appearances.DisplayName(string.IsNullOrWhiteSpace(d.Name) ? s.Name : d.Name.Trim(), (d.Nickname ?? "").Trim()),
                Name = d.Name, NameMax = Appearances.NameMax, Nickname = d.Nickname, NicknameMax = Appearances.NicknameMax,
                ColorsHead = "Colores", ColorsOff = Tinted ? null : TintOff,
                CanSave = !string.IsNullOrWhiteSpace(d.Name), SaveWhy = "Ponle un nombre",
                CopyArmed = d.CopyArmed,
            };
            var others = c.Soldiers.Count(x => x.Owner == s.Owner && x.Alive && x.Id != s.Id);
            v.CopyCount = others + 1;
            v.CanCopy = v.CanSave && others > 0 && Tinted;
            // The clothes: among those of the soldier's sex; unset, the ones their id picks, the same in every screen.
            var worn = Client.Units.Dress.OutfitOf(look.Character);
            var outfit = new ChoiceGroupView { Id = "outfit", Title = "Ropa" };
            outfit.Choices.Add(new ChoiceView { Id = "", Label = "Al azar", On = worn == null });
            foreach (var id in Client.Units.Dress.OutfitsFor(Client.Units.Dress.SexOf(look, s.Id)))
                outfit.Choices.Add(new ChoiceView { Id = id, Label = Lore.OutfitNames.TryGetValue(id, out var n) ? n : id, On = worn == id });
            v.Groups.Add(outfit);
            var heads = new ChoiceGroupView { Id = "head", Title = "Cabeza" };
            foreach (var id in HeadIds.All) heads.Choices.Add(new ChoiceView { Id = id, Label = Appearances.Heads[id].Name, On = look.Head == id });
            v.Groups.Add(heads);
            var builds = new ChoiceGroupView { Id = "build", Title = "Complexión" };
            foreach (var id in BuildIds.All) builds.Choices.Add(new ChoiceView { Id = id, Label = Appearances.Builds[id].Name, On = look.Build == id });
            v.Groups.Add(builds);
            var swatches = Appearances.ArmorColors.ToList();
            var hat = Appearances.Heads.TryGetValue(look.Head ?? "", out var head) ? head.Paint : null;
            string Follows(string piece)
            {
                var bare = CustomDraft.Copy(look);
                SetPart(bare, piece, null);
                return Appearances.PartColor(bare, piece);
            }
            ColorRowView Piece(string piece, string label)
            {
                var leader = piece == ArmorParts.Helmet && hat == "trim" ? "los detalles" : "la armadura";
                var mine = Appearances.PartOf(look, piece);
                var own = mine != null && !string.Equals(mine, Follows(piece), System.StringComparison.OrdinalIgnoreCase);
                return new ColorRowView { Id = piece, Label = label, Color = mine ?? Follows(piece), Swatches = swatches, Reset = own ? $"↺ como {leader}" : null, Note = own ? null : $"como {leader}" };
            }
            v.Colors.Add(new ColorRowView { Id = "all", Label = "Toda la armadura", Color = look.Primary, Swatches = swatches, Note = "todas las piezas" });
            v.Colors.Add(hat != null ? Piece(ArmorParts.Helmet, hat == "armour" ? "Casco" : head.Name)
                : new ColorRowView { Id = ArmorParts.Helmet, Label = "Casco", Note = $"{head?.Name}: nada que pintar", Locked = true });
            v.Colors.Add(Piece(ArmorParts.Torso, "Torso"));
            v.Colors.Add(Piece(ArmorParts.Arms, "Brazos"));
            v.Colors.Add(Piece(ArmorParts.Legs, "Piernas"));
            v.Colors.Add(new ColorRowView { Id = "trim", Label = "Detalles", Color = look.Secondary, Swatches = swatches, Note = "capucha, boina y gorra" });
            v.Colors.Add(new ColorRowView { Id = "mark", Label = "Marca de jugador", Color = PlayerColor(s.Owner), Note = "siempre en tu color", Locked = true });
            return v;
        }

        /// <summary>The clothes take the colours (Ajustes' «Ropa con el color del jugador», off by default): else the rows wait, dimmed.</summary>
        static bool Tinted => App.GameSettings.SquadTint;

        /// <summary>Over the dimmed colours while the clothes don't take them (screen.ts TINT_OFF).</summary>
        const string TintOff = "Para teñir la ropa, activa «Ropa con el color del jugador» en Ajustes.";

        /// <summary>A piece's own colour set (null: it follows again).</summary>
        public static void SetPart(Appearance a, string piece, string color)
        {
            if (piece == ArmorParts.Helmet) a.Helmet = color;
            else if (piece == ArmorParts.Torso) a.Torso = color;
            else if (piece == ArmorParts.Arms) a.Arms = color;
            else if (piece == ArmorParts.Legs) a.Legs = color;
        }

        /// <summary>A soldier by the face (Ana, 22/10 #49): the name they go by, the rank, ▲ for points, what keeps them in.</summary>
        SoldierTileView Tile(CampaignState c, CampaignSoldier s, string picked)
        {
            var spent = s.Board.Count;
            var rank = Bars.VeterancyRank(spent);
            var t = new SoldierTileView
            {
                Id = s.Id, Name = string.IsNullOrEmpty(s.Nickname) ? s.Name.Split(' ')[0] : s.Nickname, Accent = PlayerColor(s.Owner),
                Skills = SkillsOf(s), Rank = rank, RankTip = RankTip(s), Dead = !s.Alive, Selected = s.Id == picked,
                Points = s.Owner == Slot && s.Alive && Campaign.CanSpendPoints(c, s), PointsTip = "Puntos por gastar en el tablero",
            };
            if (!s.Alive || s.Infection != null || s.WoundedDays > 0)
            {
                var (text, tone, tip) = Health(c, s, true);
                t.Tag = text;
                t.TagTone = tone;
                t.TagTip = tip;
            }
            return t;
        }

        /// <summary>The rank's name and the points spent (veterancyBadge's title).</summary>
        static string RankTip(CampaignSoldier s)
        {
            var spent = s.Board.Count;
            var def = Bars.Veterancy[Bars.VeterancyRank(spent)];
            return $"{(Female(s) ? def.Female : def.Name)} · {spent} {(spent == 1 ? "punto gastado" : "puntos gastados")} en el tablero";
        }

        /// <summary>
        /// How a soldier is (healthTag): fallen, with a fever from a bite (and when it turns), wounded, or ready; `brief`
        /// for the tile. In the feminine for a woman (Lore, C2).
        /// </summary>
        (string text, string tone, string tip) Health(CampaignState c, CampaignSoldier s, bool brief)
        {
            if (!s.Alive) return (s.FellCause == "turned" ? Campaign.TurnedCause : Female(s) ? "Caída" : "Caído", "bad", null);
            if (s.Infection != null)
            {
                var day = Campaign.TurnDay(c, s) ?? c.Day;
                return (brief ? $"Fiebre · {AheadChip(c, day)}" : $"Mordisco · se convierte {AheadText(c, day)}", "bad", "Un mordisco: tiene fiebre. Sin el tratamiento, se convierte.");
            }
            if (s.WoundedDays > 0) return (brief ? MissionsText(MissionsFor(s.WoundedDays)) : $"Heridas · {MissionsText(MissionsFor(s.WoundedDays))}", "warn", null);
            return (Female(s) ? "Lista" : "Listo", brief ? null : "good", null);
        }

        /// <summary>Recruiting, on its own card: what it is, what it costs, what it needs, and why not (renderBarracks).</summary>
        RecruitView Recruit(CampaignState c, int count)
        {
            var cap = Campaign.SoldierCap(c);
            var price = Campaign.RecruitCost(c);
            var free = Campaign.BedsFree(c);
            var why = First(
                (count >= cap, $"Ya tienes tus {cap} soldados: es el máximo por jugador"),
                (free <= 0, "No quedan camas: para reclutar más, construid un Barracón."),
                (true, PendingWhy(c)),
                (true, Shortfall(c, price)));
            var v = new RecruitView
            {
                Title = "Reclutar un soldado",
                Blurb = "Un recluta nuevo: sale con cuchillo y pistola y con algo de práctica en una o dos habilidades. Llega al momento.",
                Button = $"Reclutar · {Campaign.CostText(price)}",
                Enabled = why == null,
                Why = why,
            };
            v.Chips.Add(new ChipView { Text = Campaign.CostText(price) });
            v.Chips.Add(new ChipView { Text = $"Tus soldados {count}/{cap}", Tip = $"Como mucho {cap} soldados por jugador" });
            // Lore, C4: the caravan's eight beds, not the old bunker's.
            v.Chips.Add(new ChipView { Text = $"Camas libres {System.Math.Max(0, free)}", Tone = free <= 0 ? "warn" : null, Tip = "Cada soldado necesita una cama: la caravana tiene 8, y cada Barracón suma más." });
            return v;
        }

        /// <summary>The picked soldier's sheet (renderBarracks, skillSheet.ts skillsBlock, arms.ts weaponPick).</summary>
        SoldierSheetView Sheet(CampaignState c, CampaignSoldier s)
        {
            var own = s.Owner == Slot;
            var isMine = Commands(s.Owner);
            var stats = Campaign.SquadMember(c, s);
            var aim = Content.Templates[TemplateIds.Rookie].Aim;
            var v = new SoldierSheetView
            {
                // Lore, 26/10: «tuya» for a woman, as the rank already agrees («Fogueada · tuya»).
                Kicker = $"{Campaign.VeterancyName(s)} · {(own ? (Female(s) ? "tuya" : "tuyo") : $"de {PlayerName(s.Owner)}")}",
                Name = Appearances.DisplayName(s.Name, s.Nickname),
                Skills = SkillsOf(s),
                NameTip = $"Misiones: {s.Missions} · Bajas: {s.Kills}",
                Subtitle = Campaign.WeaponLabel(c, s),
                Note = !own && isMine && s.Alive ? $"{PlayerName(s.Owner)} no está: {(Female(s) ? "la" : "lo")} preparas tú hasta que vuelva." : null,
                Customize = isMine && s.Alive ? "Personalizar" : null,
                ArmsHead = isMine && s.Alive ? "Arma y armadura · se eligen aquí" : "Arma y armadura",
                BeltHead = "Cinturón · se elige aquí",
            };
            // Both aims, the soldier's own without the weapon: the base and what the bars and the board add (playtest 17/10).
            v.Stats.Add(new SheetStatView { Icon = "stat:hp", Value = stats.MaxHp.ToString(), Color = "#8fd05a", Tip = "Salud" });
            v.Stats.Add(new SheetStatView { Icon = "stat:aim", Value = (aim + stats.Mods.Aim + stats.Mods.GunAim).ToString(), Color = "#ffffff", Tip = $"Puntería con armas de fuego: {Bars.BaseAim} de base, más lo que suman las barras de Disparo y el tablero" });
            v.Stats.Add(new SheetStatView { Icon = "stat:melee", Value = (aim + stats.Mods.Aim + stats.Mods.MeleeAim).ToString(), Color = "#ffffff", Tip = $"Puntería cuerpo a cuerpo: {Bars.BaseAim} de base, más lo que suman las barras de Cuerpo a cuerpo y el tablero" });
            v.Stats.Add(new SheetStatView { Icon = "stat:ap", Value = stats.Will.ToString(), Color = "#a678ff", Tip = "Voluntad" });
            var (health, tone, tip) = Health(c, s, false);
            v.Health = health;
            v.HealthTone = tone;
            v.HealthTip = tip;
            v.Care = Care(c, s);
            foreach (var t in s.Traits ?? new List<string>())
                if (Quests.Traits.TryGetValue(t, out var def)) v.Traits.Add(new ChipView { Text = def.Name, Tip = def.Description });
            v.Arms.Add(WeaponPick(c, s, false, isMine && s.Alive));
            v.Arms.Add(WeaponPick(c, s, true, isMine && s.Alive));
            // The armour the Taller has made is for everyone (docs/FASE9.md §5); what the next one waits for, on its chip.
            var tier = Progression.ArmorTier(c, s);
            var blocker = s.Alive ? Progression.NextTierBlocker(c, s, "armor") : null;
            var next = tier < Progression.ArmorTiers.Count ? Progression.ArmorTiers[tier] : null;
            v.Armor = new ArmorView
            {
                Tier = tier, Name = Progression.ArmorTiers[tier - 1].Name,
                Figures = $"· {stats.MaxHp} de salud{(stats.Armor != 0 ? $" · {stats.Armor} de blindaje" : "")}",
                Next = blocker != null && next != null ? $"→ {next.Name}" : null, NextTip = blocker,
            };
            v.Belt.Add(BeltPick(c, s, "utility", "Hueco 1", "slot0", isMine && s.Alive));
            v.Belt.Add(BeltPick(c, s, "ammo", "Hueco 2", "slot1", isMine && s.Alive));
            v.Experience = Experience(s, own && s.Alive);
            return v;
        }

        /// <summary>
        /// Medicines on a sheet (careActions; docs/FASE3.md): treating a bite takes both commanders, as launching; healing a
        /// wound faster is one click. «Mordisco», not «Mordido» (Lore, C2).
        /// </summary>
        CareView Care(CampaignState c, CampaignSoldier s)
        {
            if (!s.Alive) return null;
            var have = c.Medicines ?? 0;
            if (s.Infection != null)
            {
                var cost = Campaign.TreatCost(c);
                var proposal = c.Treatment;
                var partner = Partner;
                var shared = partner != null && partner.Connected;
                var mine = proposal != null && proposal.Soldier == s.Id && proposal.Ready[Slot];
                var theirs = proposal != null && proposal.Soldier == s.Id && partner != null && proposal.Ready[partner.Slot];
                var other = proposal != null && proposal.Soldier != s.Id ? c.Soldiers.FirstOrDefault(x => x.Id == proposal.Soldier) : null;
                var tooFew = have < cost;
                var v = new CareView
                {
                    Title = "Mordisco",
                    Line = shared ? $"Las medicinas no llegan para todos: el tratamiento lo decidís los dos. Tenéis {have} ✚." : $"El tratamiento le quita la fiebre. Tenéis {have} ✚.",
                };
                if (theirs && !mine)
                {
                    v.Buttons.Add($"Confirmar el tratamiento ({cost} ✚)");
                    v.Enabled.Add(!tooFew);
                    v.Buttons.Add("Rechazar");
                    v.Enabled.Add(true);
                }
                else if (mine)
                {
                    v.Buttons.Add("Retirar la propuesta");
                    v.Enabled.Add(true);
                    v.Waiting = $"Esperando a {partner?.Name ?? "el otro jugador"}";
                }
                else
                {
                    v.Buttons.Add(shared ? $"Proponer el tratamiento ({cost} ✚)" : $"Tratar ({cost} ✚)");
                    v.Enabled.Add(!tooFew && other == null);
                    v.Why = tooFew ? $"Hacen falta {cost} ✚ y tenéis {have}." : other != null ? $"Ya hay un tratamiento propuesto para {other.Name}: confirmadlo o rechazadlo antes." : null;
                }
                return v;
            }
            if (s.WoundedDays > 0)
            {
                var v = new CareView();
                v.Buttons.Add($"Curar ({Campaign.MedicateCost} ✚): vuelve antes");
                v.Enabled.Add(have >= Campaign.MedicateCost);
                v.Tip = have < Campaign.MedicateCost ? "No quedan medicinas." : $"Gasta {Campaign.MedicateCost} ✚ de las {have} que tenéis.";
                return v;
            }
            return null;
        }

        /// <summary>
        /// The order behind the care card's button `index`, as Care lays them out (screen.ts careBlock): the bite's
        /// treatment proposed, confirmed, rejected or withdrawn (treat, ready or not), or a wound's medicine; null, none.
        /// </summary>
        public CampaignCommand CareOrder(string soldier, int index)
        {
            var c = State;
            var s = c.Soldiers.FirstOrDefault(x => x.Id == soldier);
            if (s == null) return null;
            if (s.Infection != null)
            {
                var proposal = c.Treatment;
                var partner = Partner;
                var mine = proposal != null && proposal.Soldier == s.Id && proposal.Ready[Slot];
                var theirs = proposal != null && proposal.Soldier == s.Id && partner != null && proposal.Ready[partner.Slot];
                // Confirmar or Rechazar; Retirar la propuesta; Tratar (or Proponer).
                return new TreatCommand { Soldier = s.Id, Ready = theirs && !mine ? index == 0 : !mine };
            }
            return s.WoundedDays > 0 && index == 0 ? new MedicateCommand { Soldier = s.Id } : null;
        }

        // --------------------------------------------------------------- the arms

        /// <summary>A weapon in a line (arms.ts weaponLine): its damage and, for a firearm, its reach.</summary>
        static string WeaponLine(WeaponProfile p)
        {
            var damage = p.Damage.Min == p.Damage.Max ? p.Damage.Min.ToString() : $"{p.Damage.Min}–{p.Damage.Max}";
            return $"{Lore.WeaponLabels.Damage} {damage}" + (p.Melee ? "" : $" · {Lore.WeaponLabels.Reach} {Lore.Reaches[p.Reach].Name.ToLowerInvariant()}");
        }

        /// <summary>A soldier's hand (or belt, `melee`): a weapon of the Armería, or the pistol or the knife (carriedProfile).</summary>
        static WeaponProfile Carried(CampaignState c, CampaignSoldier s, bool melee)
        {
            var w = melee ? Progression.MeleeWeaponOf(c, s) : Progression.FoundWeaponOf(c, s);
            return w != null ? Aim.WeaponProfileOf(w.Kind, w.Condition ?? 100, w.Mods) : Aim.BaseWeaponProfile(melee ? WeaponIds.Knife : WeaponIds.Pistol);
        }

        /// <summary>A weapon's condition in words (conditionEl): «Estado 64», «Gastada · 22», «Rota».</summary>
        static string ConditionText(double condition) =>
            condition <= 0 ? "Rota" : condition < Content.WornBelow ? $"Gastada · {Js.Round(condition)}" : $"Estado {Js.Round(condition)}";

        /// <summary>
        /// A soldier's hand or belt as a picker (arms.ts weaponPick; Arturo, 17/10): the Armería's weapons for that place,
        /// free first, then one's own soldiers' (taking it from them), then the partner's (off), and none first of all.
        /// </summary>
        PickView WeaponPick(CampaignState c, CampaignSoldier s, bool melee, bool editable)
        {
            var current = melee ? Progression.MeleeWeaponOf(c, s) : Progression.FoundWeaponOf(c, s);
            var none = melee ? Content.Weapons[WeaponIds.Knife].Name : Content.Weapons[WeaponIds.Pistol].Name;
            var v = new PickView { Id = melee ? "belt" : "hand", Label = melee ? "Cinto" : "Mano", Value = current != null ? Content.FoundWeapons[current.Kind].Name : none, Editable = editable };
            v.Lines.Add(WeaponLine(Carried(c, s, melee)));
            if (!melee) v.Lines.Add(current != null ? "y la pistola al cinto" : "sin arma: la pistola");
            if (current != null && !melee)
            {
                var condition = current.Condition ?? 100;
                v.Condition = (int)System.Math.Max(0, System.Math.Min(100, condition));
                v.ConditionText = ConditionText(condition);
                foreach (var m in current.Mods ?? new List<string>()) v.Tags.Add(Content.WeaponMods[m].Name);
            }
            if (!editable) return v;
            v.Options.Add(new PickOptionView { Id = "none", Name = melee ? "Sin arma: el cuchillo" : "Sin arma: la pistola", Note = current != null ? $"{Content.FoundWeapons[current.Kind].Name} vuelve a la Armería" : null, Current = current == null });
            CampaignSoldier HolderOf(FoundWeapon w) => w.Holder != null ? c.Soldiers.FirstOrDefault(x => x.Id == w.Holder) : null;
            bool Theirs(FoundWeapon w) => HolderOf(w) is CampaignSoldier h && !Content.CommandsFor(Slot, h.Owner, Connected);
            int Rank(FoundWeapon w) => w.Holder == null ? 0 : Theirs(w) ? 2 : 1;
            var options = (c.FoundWeapons ?? new List<FoundWeapon>()).Where(w => w.Id != current?.Id && (Content.FoundWeapons[w.Kind].Slot == "melee") == melee).OrderBy(Rank);
            foreach (var w in options)
            {
                var holder = HolderOf(w);
                var theirs = Theirs(w);
                var note = holder == null ? "En la Armería" : theirs ? $"La lleva {holder.Name}, de {PlayerName(holder.Owner)}" : $"La lleva {holder.Name}: se la quitas";
                v.Options.Add(new PickOptionView { Id = w.Id, Name = Content.FoundWeapons[w.Kind].Name, Note = note, Enabled = !theirs });
            }
            return v;
        }

        /// <summary>
        /// A belt's place (screen.ts loadout): either takes any accessory (docs/FASE9.md §4); the choices are those with
        /// copies free, and the one there.
        /// </summary>
        static PickView BeltPick(CampaignState c, CampaignSoldier s, string slot, string title, string id, bool editable)
        {
            var current = slot == "utility" ? s.Loadout.Utility : s.Loadout.Ammo;
            var v = new PickView { Id = id, Label = title, Value = current != null ? Content.Items[current].Name : editable ? "Nada" : "—", Editable = editable };
            if (!editable) return v;
            v.Options.Add(new PickOptionView { Id = "none", Name = "Nada", Current = current == null });
            foreach (var item in Content.Items.Values.Where(i => i.Slot == ItemSlots.Utility))
            {
                var free = Campaign.ItemsFree(c, item.Id) + (current == item.Id ? 1 : 0);
                if (free <= 0 && current != item.Id) continue;
                v.Options.Add(new PickOptionView { Id = item.Id, Name = item.Name, Note = current == item.Id ? null : $"{free} libres", Current = current == item.Id });
            }
            return v;
        }

        /// <summary>
        /// The skill board of a soldier (skillSheet.ts treePanel): who, the veterancy, the points left; only its own
        /// commander buys, the partner here or not (Arturo, 18/10 #42). Null for an unknown soldier.
        /// </summary>
        public SkillBoardView SkillBoard(string soldier)
        {
            var s = State.Soldiers.FirstOrDefault(x => x.Id == soldier);
            return s == null ? null : BoardOf(s, s.Owner == Slot && s.Alive);
        }

        /// <summary>A campaign soldier's board as the window shows it; `editable`, its cells can be bought (the battle's sheet: never).</summary>
        public static SkillBoardView BoardOf(CampaignSoldier s, bool editable)
        {
            var spent = s.Board.Count;
            var rank = Bars.VeterancyRank(spent);
            var def = Bars.Veterancy[rank];
            var reward = Lore.RankReward(rank, Female(s) ? "f" : "m");
            var name = Female(s) ? def.Female : def.Name;
            var v = new SkillBoardView
            {
                Kicker = $"Tablero de habilidades · {name}", SoldierId = s.Id, Name = Appearances.DisplayName(s.Name, s.Nickname), Rank = name,
                RankFilled = reward != null ? spent - def.From : Bars.RankStep, RankTotal = Bars.RankStep, Will = reward?.Short ?? "Rango máximo",
                Points = Bars.PointsLeft(s.Skills, s.Board), Editable = editable,
            };
            v.Board.AddRange(s.Board);
            return v;
        }

        // ------------------------------------------------------------ experience

        /// <summary>
        /// The experience (skillSheet.ts skillsBlock): the six bars, the veterancy and the board's button, which calls for
        /// attention while there are points to spend if the soldier is this player's.
        /// </summary>
        static SkillsView Experience(CampaignSoldier s, bool mine)
        {
            var points = Bars.PointsLeft(s.Skills, s.Board);
            var spent = s.Board.Count;
            var rank = Bars.VeterancyRank(spent);
            var def = Bars.Veterancy[rank];
            var reward = Lore.RankReward(rank, Female(s) ? "f" : "m");
            var v = new SkillsView
            {
                Head = "Experiencia",
                Rank = Female(s) ? def.Female : def.Name,
                RankFilled = reward != null ? spent - def.From : Bars.RankStep,
                RankTotal = Bars.RankStep,
                Will = reward?.Short ?? "Rango máximo",
                WillTip = reward?.Long ?? "No hay rango más alto.",
                RankLine = def.Line,
                Gives = $"Voluntad {Bars.VeteranWill(rank)} · {(rank >= Content.ThirdActionRank ? 3 : 2)} acciones por turno",
                Board = points > 0 ? $"Tablero de habilidades · {(points == 1 ? "un punto" : $"{points} puntos")} por gastar" : "Tablero de habilidades",
                BoardPulse = points > 0 && mine,
            };
            for (var i = 0; i < SkillIds.All.Count; i++)
            {
                var id = SkillIds.All[i];
                var skill = Bars.SkillDefs[id];
                var bars = Bars.BarsOf(s.Skills.TryGetValue(id, out var xp) ? xp : 0);
                var grown = s.Growth != null && s.Growth.TryGetValue(id, out var g) ? g : 0;
                var r = Lore.BarReward(id);
                v.Rows.Add(new SkillRowView
                {
                    Line = i, Name = skill.Name, Tip = skill.Line, Growth = grown != 0 ? $"+{grown} %" : null, GrowthTip = skill.Stat,
                    Filled = bars.Into, Total = bars.Need, Reward = r.Short, RewardTip = r.Long,
                });
            }
            return v;
        }
    }
}
