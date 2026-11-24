using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.UI.Hud;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// The windows over the battle (game.ts openSheet, showEnemyFile), only to look at: no action spent, no command
    /// passed. A soldier's sheet (H, or a click on its name on the panel): its figures, what it carries and its skills,
    /// and in a campaign its board, as in the Cuartel. An enemy's file (right click on one in sight): the Atalaya's words.
    /// </summary>
    public sealed partial class CombatController
    {
        /// <summary>The campaign as the squad took off (CombatSession's): its soldiers' boards for the sheet; null in a skirmish.</summary>
        public Func<CampaignState> Campaign;

        /// <summary>A soldier's arms in words (hud.ts setSelected): what is in the hand (the pistol if nothing), and what else it carries.</summary>
        static (string weapon, string[] extras) ArmsOf(Unit u)
        {
            var t = Content.Templates[u.Template];
            var arms = u.Arms;
            var pistol = Content.Weapons[WeaponIds.Pistol].Name;
            // With no pistol (where the Armería counts them: Arturo, 01/11 #10) and nothing in the hand, the knife or the machete is all.
            var hasPistol = Content.HasPistol(u);
            // Nothing in the belt (the knife in the pack or dropped: Gameplay, 24/11) says so; it strikes as with the knife.
            var blade = Content.BareHanded(u) ? HudText.Bare.Name
                : arms != null && !string.IsNullOrEmpty(arms.Melee) ? Content.FoundWeapons[arms.Melee].Name : Content.Weapons[WeaponIds.Knife].Name;
            var bare = arms != null && string.IsNullOrEmpty(arms.Primary) && !hasPistol;
            // The unit's own name for its weapon only with one in the hand: it said «Cuchillo» still with a pistol put on (0.11.5).
            var held = arms == null || !string.IsNullOrEmpty(arms.Primary);
            var weapon = (held ? u.WeaponName : null) ?? (arms != null ? (!string.IsNullOrEmpty(arms.Primary) ? Content.FoundWeapons[arms.Primary].Name : hasPistol ? pistol : blade) : Content.Weapons[t.Weapon].Name);
            var extras = arms != null
                ? new[] { !string.IsNullOrEmpty(arms.Primary) && hasPistol ? pistol : "", bare ? "" : blade }
                : new[] { t.Secondary != null ? Content.Weapons[t.Secondary].Name : "", t.MeleeWeapon != null ? Content.Weapons[t.MeleeWeapon].Name : "" };
            return (weapon, extras.Where(x => !string.IsNullOrEmpty(x)).ToArray());
        }

        /// <summary>The soldier's sheet over the battle (game.ts openSheet), or closed if it was this one's already.</summary>
        void OpenSheet(string id)
        {
            var u = UnitById(Shown, id);
            // An enemy's (the boss's bar, H6): its file, if in sight.
            if (u != null && u.Team == Teams.Alien)
            {
                OnEnemyFile(id);
                return;
            }
            if (u == null || u.Team != Teams.Xcom || Hud == null) return;
            var t = Content.Templates[u.Template];
            var soldier = u.Template == TemplateIds.Rookie;
            var step = soldier && u.Rank < Bars.Veterancy.Count ? Bars.Veterancy[u.Rank] : null;
            var v = new SoldierSheetView
            {
                Name = Appearances.DisplayName(u.Name, u.Nickname),
                Rank = step != null ? (u.Appearance?.Sex == "f" ? step.Female : step.Name) : t.Name,
            };
            var owner = u.Owner != null && u.Owner != MySlot ? Players.FirstOrDefault(p => p.Slot == u.Owner) : null;
            if (owner != null)
            {
                v.Whose = owner.Name;
                v.WhoseColor = PlayerColor(owner.Slot);
            }
            void Stat(string key, string value, string tip = null) => v.Stats.Add(new SheetRowView { Key = key, Value = value, Tip = tip });
            Stat("Salud", $"{u.Hp} de {u.MaxHp}");
            Stat("Acciones", $"{u.Ap} de {Content.ApPerTurnOf(u)}");
            if (Paths.FullMoves(Shown, u) > 0) Stat(HudText.Moves.Stat, $"{HudText.Moves.Tiles(Paths.MoveBudget(Shown, u))} de {HudText.Moves.Tiles(Paths.FullMoves(Shown, u))} puntos");
            Stat("Puntería al disparar", (t.Aim + u.Mods.Aim + u.Mods.GunAim).ToString());
            Stat("Puntería cuerpo a cuerpo", (t.Aim + u.Mods.Aim + u.Mods.MeleeAim).ToString());
            Stat("Voluntad", u.Will.ToString());
            // What Forma física's bars grow (Arturo, 6/11), as the shot counts it (Aim.PreviewShot), without hunkering.
            // With what each does under the pointer, and the defence beside it (M6, Lore.GuardText): it wasn't shown.
            var dodge = t.Dodge + u.Mods.Dodge;
            var defense = t.Defense + u.Mods.Defense;
            Stat("Esquiva", dodge.ToString(), Lore.GuardText.Dodge(dodge) + "\n" + Lore.GuardText.Against);
            Stat("Defensa", defense.ToString(), Lore.GuardText.Defense(defense) + "\n" + Lore.GuardText.Against);
            if (u.Armor > 0) Stat("Blindaje", u.Armor.ToString());
            void Carry(string key, string value, string tip = null) => v.Carries.Add(new SheetRowView { Key = key, Value = value, Tip = tip });
            var (weapon, extras) = ArmsOf(u);
            Carry("Armas", string.Join(" · ", new[] { weapon }.Concat(extras)), Content.BareHanded(u) ? HudText.Bare.Tip : null);
            var belt = u.Gear.Select(g =>
            {
                var charge = Content.Items[g].Charges?.Keys.FirstOrDefault();
                return charge != null ? $"{Content.Items[g].Name} ({(u.Charges.TryGetValue(charge, out var n) ? n : 0)})" : Content.Items[g].Name;
            }).ToList();
            Carry("Cinturón", belt.Count > 0 ? string.Join(" · ", belt) : "Nada");
            if (u.Worn?.Count > 0) Carry("Puesto", string.Join(" · ", u.Worn.Select(w => Content.Items[w].Name)));
            // A run's soldier keeps its pack in Unit.Pack (RunPack), not in the old Backpack: it said «Vacía» with it full (24/11).
            var pack = u.Pack != null ? u.Pack.Items.Select(p => Lore.GearName(p.Gear.Kind, p.Gear.Type, p.Gear.Count)).ToList()
                : (u.Backpack ?? new List<Loot>()).Select(Lore.LootName).ToList();
            Carry("Mochila", pack.Count > 0 ? string.Join(" · ", pack) : "Vacía");
            v.Skills.AddRange(u.Perks.Select(p => Bastion.Engine.Board.Cells.FirstOrDefault(c => c.Perk == p)?.Name).Where(n => !string.IsNullOrEmpty(n)));
            // In a campaign, its board as in the Cuartel, not to be touched.
            var cs = !string.IsNullOrEmpty(u.CampaignId) ? Campaign?.Invoke()?.Soldiers.FirstOrDefault(x => x.Id == u.CampaignId) : null;
            if (cs != null)
            {
                v.Board = Bastion.UI.Strategy.StrategyPresenter.BoardOf(cs, false, Campaign?.Invoke());
                // Its bust for the board's head: nobody asks for it in a battle (the base does, BaseController).
                Bastion.Client.Portraits.Know(cs.Id, cs.Appearance, cs.Name);
            }
            Hud.ShowSheet(v);
        }

        /// <summary>An enemy's file by id (tests: PackSteps «file=a49»); one not in sight, none.</summary>
        public void OnEnemyFile(string id)
        {
            var u = UnitById(Shown, id);
            if (u != null && u.Alive && u.Team == Teams.Alien && Visible(u)) ShowEnemyFile(u);
        }

        /// <summary>The Bastion's file on an enemy in sight (game.ts showEnemyFile): what it is, its figures, what it does, how to beat it.</summary>
        void ShowEnemyFile(Unit u)
        {
            if (Hud == null || !Lore.EnemyFiles.TryGetValue(u.Template, out var file)) return;
            var t = Content.Templates[u.Template];
            // Its dodge too when it has some (M6); what either does under the pointer, never their zero's lines, which are a
            // soldier's (Lore, 23/11).
            var dodge = t.Dodge + u.Mods.Dodge;
            var stats = new[] { $"Salud {u.Hp}/{u.MaxHp}", u.Armor > 0 ? $"Blindaje {u.Armor}" : "", $"Puntería {t.Aim}", t.Defense > 0 ? $"Defensa {t.Defense}" : t.Defense < 0 ? $"Defensa −{-t.Defense}" : "", dodge != 0 ? $"Esquiva {dodge}" : "" };
            var guard = new[] { t.Defense != 0 ? Lore.GuardText.Defense(t.Defense) : null, dodge != 0 ? Lore.GuardText.Dodge(dodge) : null }.Where(x => x != null).ToList();
            // Its own name: one of ours, risen, keeps it («Zombi · Clara Bosch»).
            Hud.ShowEnemyFile(new EnemyFileView
            {
                Name = u.Name, Stats = string.Join(" · ", stats.Where(s => s != "")), What = file.What, Does = file.Does, Tip = file.Tip,
                StatsTip = guard.Count > 0 ? string.Join("\n", guard) + "\n" + Lore.GuardText.Against : null,
            });
        }
    }
}
