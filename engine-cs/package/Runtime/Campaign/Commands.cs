#nullable enable
// campaign.ts: the commands (executeCampaignCommand): the shared decisions, proposed to the partner or applied at once
// (proposalText, validateAction, applyAction, propose), and what each commander does alone. Texts fixed as
// text-fixes.ts has them (Lore, 24/10): the time in missions, the reasons that agree with a soldier who is a woman. Not in
// TypeScript: calling off the mission chosen is proposed too (CancelMissionAction; Arturo, 26/10).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Campaign
    {
        // ----------------------------------------------------------------- proposals

        private static string ProposalText(CampaignState c, ProposalAction action)
        {
            switch (action)
            {
                case PlaceAction a:
                    return $"construir {Lore.PieceTexts.All[a.Piece].A}";
                case RaiseAction a:
                {
                    var p = c.Bastion.Pieces.FirstOrDefault((x) => x.Id == a.Piece);
                    return p != null ? $"mejorar {Lore.PieceTexts.All[p.Piece].The} a nivel {(p.Level ?? 1) + 1}" : "mejorar…";
                }
                case PlaceLineAction a:
                    return $"construir {a.Cells.Count} × {Lore.PieceTexts.All[a.Piece].Name.ToLowerInvariant()}";
                case FellAction a:
                    return a.Tiles.Count == 1 ? "talar un árbol" : $"talar {a.Tiles.Count} árboles";
                case MendAction _:
                case RearmAction _:
                case DismantleAction _:
                {
                    var id = action is MendAction m ? m.Piece : action is RearmAction r ? r.Piece : ((DismantleAction)action).Piece;
                    var p = c.Bastion.Pieces.FirstOrDefault((x) => x.Id == id);
                    var what = p != null ? Lore.PieceTexts.All[p.Piece].The : "la pieza";
                    return $"{(action is MendAction ? "reparar" : action is RearmAction ? "reponer" : "desmontar")} {what}";
                }
                case LevelUpAction _:
                    return $"subir el Bastión a {Lore.BastionLevelThe(Progression.BastionLevel(c) + 1)}";
                case ChooseMissionAction a:
                    return $"ir a la misión {c.Offers.FirstOrDefault((o) => o.Id == a.Id)?.Name ?? ""}";
                case CancelMissionAction a:
                {
                    var name = c.Offers.FirstOrDefault((o) => o.Id == a.Id)?.Name;
                    return name != null ? $"cancelar la misión «{name}»" : "cancelar la misión";
                }
                case ContactAction a:
                    return $"contactar con {Regions[a.Region].Name}";
                case EventAction a:
                {
                    var def = EventDef(c);
                    return $"responder «{(def != null ? def.Options[a.Option].Label : "")}»";
                }
                case InvestigateAction a:
                {
                    var rumor = c.Rumors.FirstOrDefault((r) => r.Id == a.Id);
                    return $"escuchar el rumor {(rumor != null ? Lore.OfZone(Regions[rumor.Region].Name) : "de …")}";
                }
                case CraftAction a:
                    return $"hacer en el Taller: {Progression.Crafts[a.Project].Name.ToLowerInvariant()}";
                case RecruitAction _:
                    return $"reclutar un soldado ({RecruitCost(c)} §)";
                case BuyAction a:
                    return $"comprar {Content.Items[a.Item].Name.ToLowerInvariant()} ({ShopPrice(c, a.Item)} §)";
                default:
                    // The base of before's: nothing proposes them now.
                    return "";
            }
        }

        /// <summary>`JSON.parse(JSON.stringify(base))`: a base to try pieces on.</summary>
        private static BaseState CopyBase(BaseState b)
        {
            return new BaseState
            {
                Width = b.Width,
                Height = b.Height,
                Trees = new List<int>(b.Trees),
                Felled = b.Felled != null ? new List<int>(b.Felled) : null,
                Pieces = b.Pieces.Select((p) => new BasePiece { Id = p.Id, Piece = p.Piece, X = p.X, Y = p.Y, Rot = p.Rot, Size = p.Size != null ? (int[])p.Size.Clone() : null, Hp = p.Hp, Level = p.Level, Charges = p.Charges }).ToList(),
                NextPiece = b.NextPiece,
                Level = b.Level,
            };
        }

        private static string? ValidateAction(CampaignState c, ProposalAction action)
        {
            switch (action)
            {
                case PlaceAction a:
                {
                    var def = Base.Pieces[a.Piece];
                    if (Progression.BastionLevel(c) < def.Level) return Lore.NeedsLevelLine(def.Level);
                    return Base.PlaceBlocker(c.Bastion, a.Piece, a.X, a.Y, a.Rot) ?? CostBlocker(c, def.Cost.Supplies, def.Cost.Parts ?? 0);
                }
                case RaiseAction a:
                {
                    var p = c.Bastion.Pieces.FirstOrDefault((x) => x.Id == a.Piece);
                    if (p == null) return "Eso ya no está.";
                    var next = RaiseCost(p);
                    if (next.Error != null) return next.Error;
                    if (Progression.BastionLevel(c) < next.Level) return Lore.NeedsLevelLine(next.Level);
                    if (!BuildingWorks(p)) return "No se mejora con daños: repara antes.";
                    return CostBlocker(c, next.Cost, 0);
                }
                case PlaceLineAction a:
                {
                    var def = Base.Pieces[a.Piece];
                    if (Progression.BastionLevel(c) < def.Level) return Lore.NeedsLevelLine(def.Level);
                    // Each in its place, the ones before it already standing.
                    var trial = CopyBase(c.Bastion);
                    foreach (var cell in a.Cells)
                    {
                        var blocked = Base.PlaceBlocker(trial, a.Piece, cell.X, cell.Y, a.Rot);
                        if (blocked != null) return blocked;
                        Base.PlacePiece(trial, a.Piece, cell.X, cell.Y, a.Rot);
                    }
                    return CostBlocker(c, def.Cost.Supplies * a.Cells.Count, (def.Cost.Parts ?? 0) * a.Cells.Count);
                }
                case FellAction a:
                {
                    foreach (var t in a.Tiles)
                    {
                        var blocked = Base.FellBlocker(c.Bastion, t.X, t.Y);
                        if (blocked != null) return blocked;
                    }
                    return CostBlocker(c, Base.FellCost * a.Tiles.Count, 0);
                }
                case LevelUpAction _:
                    return LevelUpBlocker(c);
                case MendAction a:
                {
                    var p = c.Bastion.Pieces.FirstOrDefault((x) => x.Id == a.Piece);
                    if (p == null) return "Eso ya no está.";
                    var cost = Base.RepairCost(p);
                    if (cost.Supplies == 0) return "No le falta nada.";
                    return CostBlocker(c, cost.Supplies, cost.Parts ?? 0);
                }
                case RearmAction a:
                {
                    var p = c.Bastion.Pieces.FirstOrDefault((x) => x.Id == a.Piece);
                    if (p == null) return "Eso ya no está.";
                    var parts = Base.RearmCost(p);
                    // «Está lista.» (text-fixes.ts, Lore 24/10): said of «el cepo» or «el foso» too.
                    if (parts == 0) return "No hay nada que reponer.";
                    return (c.Parts ?? 0) < parts ? "No hay piezas suficientes." : null;
                }
                case DismantleAction a:
                {
                    var p = c.Bastion.Pieces.FirstOrDefault((x) => x.Id == a.Piece);
                    return p != null ? Base.DismantleBlocker(p) : "Eso ya no está.";
                }
                case ChooseMissionAction a:
                    // Another operation may take the place of the one chosen, until it takes off.
                    if (c.ActiveMission == a.Id) return "Esa misión ya está elegida.";
                    if (!c.Offers.Any((o) => o.Id == a.Id)) return "Esa misión ya no está disponible.";
                    return null;
                case CancelMissionAction a:
                    if (c.ActiveMission == null) return "No hay misión elegida.";
                    if (ActiveOffer(c)?.Siege == true) return SiegeNow;
                    // Another one chosen while the vote waited (alone, the partner away): the vote was about the one before.
                    if (c.ActiveMission != a.Id) return "Esa misión ya no está elegida.";
                    return null;
                case ContactAction a:
                    return ContactBlocker(c, a.Region);
                case EventAction a:
                    if (EventDef(c) == null) return "No hay ningún acontecimiento pendiente.";
                    if (!EventOptionAvailable(c, a.Option)) return "No podemos permitírnoslo.";
                    return null;
                case InvestigateAction a:
                    if (!c.Rumors.Any((r) => r.Id == a.Id)) return "Ese rumor ya se ha enfriado.";
                    if (c.Listening == a.Id) return "Ya estamos escuchando ese rumor.";
                    return null;
                case CraftAction a:
                    return CraftBlocker(c, a.Project);
                case RecruitAction a:
                {
                    var mine = c.Soldiers.Count((s) => s.Owner == a.For && s.Alive);
                    if (mine >= SoldierCap(c)) return $"Máximo {SoldierCap(c)} soldados por jugador.";
                    if (BedsFree(c) <= 0) return "No quedan camas: para reclutar más, construid un Barracón.";
                    return CanAfford(c, RecruitCost(c)) ? null : "No hay suministros suficientes.";
                }
                case BuyAction a:
                    // The special rounds are the weapons' mods now (docs/FASE9.md §4): nowhere to put them.
                    if (Content.Items[a.Item].Slot != ItemSlots.Utility) return "Eso ya no se vende: la munición especial se hace en el Taller.";
                    if (!Progression.ItemUnlocked(c, a.Item)) return Lore.NeedsLevelLine(Content.Items[a.Item].Level);
                    return CanAfford(c, ShopPrice(c, a.Item)) ? null : "No hay suministros suficientes.";
                default:
                    // The base of before's: TypeScript's switch has no case for them, and says nothing.
                    return null;
            }
        }

        private static void ApplyAction(CampaignState c, ProposalAction action, CampaignContext ctx)
        {
            var rng = ctx.Rng;
            switch (action)
            {
                case PlaceAction a:
                {
                    var def = Base.Pieces[a.Piece];
                    Pay(c, def.Cost.Supplies);
                    c.Parts = (c.Parts ?? 0) - (def.Cost.Parts ?? 0);
                    Base.PlacePiece(c.Bastion, a.Piece, a.X, a.Y, a.Rot);
                    Log(c, $"Construido: {Lore.PieceTexts.All[a.Piece].Name}.", LogTones.Good);
                    break;
                }
                case RaiseAction a:
                {
                    var p = c.Bastion.Pieces.First((x) => x.Id == a.Piece);
                    var next = RaiseCost(p);
                    Pay(c, next.Cost);
                    p.Level = (p.Level ?? 1) + 1;
                    // One built before the base grew takes its size now, if it fits where it stands (docs/MAPAS.md phase 6).
                    if (Base.GrownFits(c.Bastion, p)) p.Size = null;
                    var lines = Lore.PieceTexts.All[p.Piece].Levels;
                    var line = lines != null && p.Level.Value - 1 < lines.Count ? lines[p.Level.Value - 1] : null;
                    Log(c, $"{Lore.PieceTexts.All[p.Piece].Name}, nivel {p.Level}.{(!string.IsNullOrEmpty(line) ? $" {line}" : "")}", LogTones.Good);
                    break;
                }
                case PlaceLineAction a:
                {
                    var def = Base.Pieces[a.Piece];
                    Pay(c, def.Cost.Supplies * a.Cells.Count);
                    c.Parts = (c.Parts ?? 0) - (def.Cost.Parts ?? 0) * a.Cells.Count;
                    foreach (var cell in a.Cells) Base.PlacePiece(c.Bastion, a.Piece, cell.X, cell.Y, a.Rot);
                    Log(c, $"Construido: {a.Cells.Count} × {Lore.PieceTexts.All[a.Piece].Name.ToLowerInvariant()}.", LogTones.Good);
                    break;
                }
                case FellAction a:
                {
                    var felled = a.Tiles.Sum((t) => Base.FellTree(c.Bastion, t.X, t.Y));
                    Pay(c, Base.FellCost * felled);
                    Log(c, felled == 1 ? $"Talado un árbol: −{Base.FellCost} §." : $"Talados {felled} árboles: −{Base.FellCost * felled} §.");
                    break;
                }
                case MendAction a:
                {
                    var p = c.Bastion.Pieces.First((x) => x.Id == a.Piece);
                    var cost = Base.RepairCost(p);
                    Pay(c, cost.Supplies);
                    c.Parts = (c.Parts ?? 0) - (cost.Parts ?? 0);
                    p.Hp = Base.Pieces[p.Piece].Hp;
                    Log(c, $"Reparado: {Lore.PieceTexts.All[p.Piece].Name}.", LogTones.Good);
                    break;
                }
                case RearmAction a:
                {
                    var p = c.Bastion.Pieces.First((x) => x.Id == a.Piece);
                    c.Parts = (c.Parts ?? 0) - Base.RearmCost(p);
                    p.Charges = Base.Pieces[p.Piece].Charges;
                    Log(c, $"Repuesto: {Lore.PieceTexts.All[p.Piece].Name}.", LogTones.Good);
                    break;
                }
                case DismantleAction a:
                {
                    var p = c.Bastion.Pieces.First((x) => x.Id == a.Piece);
                    var back = Base.DismantleRefund(p);
                    c.Bastion.Pieces = c.Bastion.Pieces.Where((x) => x != p).ToList();
                    c.Credits += back;
                    Log(c, back != 0 ? $"Desmontado: {Lore.PieceTexts.All[p.Piece].Name}, +{back} §." : $"Desmontado: {Lore.PieceTexts.All[p.Piece].Name}.");
                    break;
                }
                case LevelUpAction _:
                {
                    var level = Progression.BastionLevel(c) + 1;
                    Pay(c, Progression.LevelCost[level - 1]);
                    c.Level = level;
                    c.Bastion.Level = level;
                    ReachedLevel(c, level, ctx.Rng);
                    break;
                }
                case ChooseMissionAction a:
                {
                    c.ActiveMission = a.Id;
                    // The squad comes ready, like in XCOM: players only swap who they want.
                    c.Squad = DefaultSquad(c, ctx.Connected);
                    c.LaunchReady = new bool[2];
                    Log(c, $"Misión elegida: {c.Offers.First((o) => o.Id == a.Id).Name}. Revisad la escuadra.");
                    break;
                }
                case CancelMissionAction _:
                    c.ActiveMission = null;
                    c.Squad = new List<string>();
                    c.LaunchReady = new bool[2];
                    Log(c, "Misión cancelada.");
                    break;
                case ContactAction a:
                {
                    Pay(c, ContactCost(c));
                    var region = c.Regions.First((r) => r.Id == a.Region);
                    region.Contacted = true;
                    Log(c, $"Contacto establecido con {Regions[region.Id].Name}.", LogTones.Good);
                    if (region.Facility != null && !c.Offers.Any((o) => o.Facility == region.Id))
                    {
                        c.Offers.Add(MakeFacilityOffer(c, region.Id, rng));
                        Log(c, $"Ya podemos quemar el Nido {Lore.OfZone(Regions[region.Id].Name)}.", LogTones.Good);
                    }
                    break;
                }
                case EventAction a:
                {
                    var def = EventDef(c)!;
                    var option = def.Options[a.Option];
                    option.Apply(c, rng);
                    Log(c, $"{def.Title}: {char.ToLowerInvariant(option.Label[0])}{option.Label.Substring(1)}.");
                    // A side quest may have moved on to its next decision: refreshNarrative shows it.
                    c.Event = null;
                    c.QuestEvent = null;
                    if (c.Doom >= DoomMax) c.Outcome = Outcomes.Defeat;
                    RefreshNarrative(c, rng);
                    break;
                }
                case InvestigateAction a:
                {
                    var rumor = c.Rumors.First((r) => r.Id == a.Id);
                    c.Listening = rumor.Id;
                    // «faltan N días de escucha» (text-fixes.ts, Lore 24/10): in missions, as the Mapa says it.
                    Log(c, $"Escuchamos el rumor {Lore.OfZone(Regions[rumor.Region].Name)}: se sabrá {Lore.InMissions(rumor.DaysLeft)}.");
                    break;
                }
                case CraftAction a:
                {
                    // A work like any other: a crew at the Taller, paid in suministros and parts.
                    var def = Progression.Crafts[a.Project];
                    var shop = WorkshopPiece(c)!;
                    var days = OldDaysToNow(WorkDays(c, def.Days));
                    Pay(c, def.Cost);
                    c.Parts = (c.Parts ?? 0) - def.Parts;
                    c.Base.Works.Add(new Construction { Plot = shop.Id, Kind = ConstructionKinds.Craft, Facility = FacilityIds.Workshop, DaysLeft = days, Total = days, Project = def.Id });
                    // «: N días» (text-fixes.ts, Lore 24/10): in missions, as the Armería says it.
                    Log(c, $"El Taller se pone con {def.The}: sale {Lore.InMissions(days)}.");
                    break;
                }
                case RecruitAction a:
                {
                    var place = a.Squad == true && c.ActiveMission != null ? SquadPlace(c, a.For, ctx.Connected) : null;
                    Pay(c, RecruitCost(c));
                    var s = AddSoldier(c, a.For, rng);
                    // The training centre sends them in with something more learnt: a level for each of its own (its places in the
                    // squad are La Lata's now: 18/10 #45).
                    var trained = FacilityLevel(c, FacilityIds.Training);
                    if (trained != 0) Seasoned(s, trained, rng);
                    Log(c, $"Llega a la escuadra: {s.Name} ({VeterancyName(s)}).");
                    if (place != null && place.Error == null)
                    {
                        JoinSquad(c, s, place);
                        c.LaunchReady = new bool[2];
                    }
                    break;
                }
                case BuyAction a:
                {
                    Pay(c, ShopPrice(c, a.Item));
                    c.Inventory[a.Item] = (c.Inventory.TryGetValue(a.Item, out var n) ? n : 0) + 1;
                    Log(c, $"Comprado: {Content.Items[a.Item].Name}.");
                    break;
                }
            }
        }

        /// <summary>Shared decisions: applied directly when alone, otherwise proposed to the partner.</summary>
        private static CampaignResult Propose(CampaignState c, ProposalAction action, CampaignContext ctx)
        {
            var error = ValidateAction(c, action);
            if (error != null) return CampaignResult.Failure(error);
            var partnerHere = ctx.Connected.Any((s) => s != ctx.Slot) && c.Players.Count > 1;
            if (!partnerHere)
            {
                ApplyAction(c, action, ctx);
                return CampaignResult.Success();
            }
            if (c.Proposal != null) return CampaignResult.Failure("Ya hay una propuesta pendiente.");
            c.Proposal = new Proposal { By = ctx.Slot, Action = action, Text = ProposalText(c, action) };
            return CampaignResult.Success();
        }

        // ----------------------------------------------------------------- commands

        public static CampaignResult ExecuteCampaignCommand(CampaignState c, CampaignCommand cmd, CampaignContext ctx)
        {
            var result = RunCampaignCommand(c, cmd, ctx);
            if (result.Ok)
            {
                SyncPads(c);
                // An answer to an event, a Nido burnt: the activity may have moved the Marea.
                SyncMarea(c);
            }
            return result;
        }

        private static CampaignResult RunCampaignCommand(CampaignState c, CampaignCommand cmd, CampaignContext ctx)
        {
            if (c.Outcome != null) return CampaignResult.Failure("La campaña ha terminado.");
            var slot = ctx.Slot;

            switch (cmd)
            {
                // Everything on the base goes to a vote with the other one here (Arturo, 18/10 #55: «todo lo de la base»).
                case PlaceCommand m:
                    return Propose(c, new PlaceAction { Piece = m.Piece, X = m.X, Y = m.Y, Rot = m.Rot }, ctx);
                case RaiseCommand m:
                    return Propose(c, new RaiseAction { Piece = m.Piece }, ctx);
                case LevelUpCommand _:
                    return Propose(c, new LevelUpAction(), ctx);
                case PlaceLineCommand m:
                    return Propose(c, new PlaceLineAction { Piece = m.Piece, Cells = m.Cells, Rot = m.Rot }, ctx);
                case FellCommand m:
                {
                    // A tree listed twice is one tree: counted twice, it was paid (and proposed) as two.
                    var tiles = m.Tiles.Where((t, i) => m.Tiles.FindIndex((o) => o.X == t.X && o.Y == t.Y) == i).ToList();
                    return Propose(c, new FellAction { Tiles = tiles }, ctx);
                }
                case MendCommand m:
                    return Propose(c, new MendAction { Piece = m.Piece }, ctx);
                case RearmCommand m:
                    return Propose(c, new RearmAction { Piece = m.Piece }, ctx);
                case DismantleCommand m:
                    return Propose(c, new DismantleAction { Piece = m.Piece }, ctx);
                case ChooseMissionCommand m:
                    if (ActiveOffer(c)?.Siege == true) return CampaignResult.Failure(SiegeNow);
                    return Propose(c, new ChooseMissionAction { Id = m.Id }, ctx);
                case ContactCommand m:
                    return Propose(c, new ContactAction { Region = m.Region }, ctx);
                case AnswerEventCommand m:
                    return Propose(c, new EventAction { Option = m.Option }, ctx);
                case InvestigateCommand m:
                    return Propose(c, new InvestigateAction { Id = m.Id }, ctx);
                case CraftCommand m:
                    return Propose(c, new CraftAction { Project = m.Project }, ctx);

                case AssignWeaponCommand m:
                {
                    // Personal, like equipping: each commander arms their own soldiers from the Armería. Anyone takes any
                    // weapon (docs/FASE8.md §A): one in the hand, one at the belt.
                    var w = (c.FoundWeapons ?? new List<FoundWeapon>()).FirstOrDefault((x) => x.Id == m.Weapon);
                    if (w == null) return CampaignResult.Failure("Esa arma no está en la Armería.");
                    var holder = !string.IsNullOrEmpty(w.Holder) ? c.Soldiers.FirstOrDefault((s) => s.Id == w.Holder) : null;
                    if (holder != null && !Content.CommandsFor(slot, holder.Owner, ctx.Connected)) return CampaignResult.Failure($"La lleva {holder.Name}, de la escuadra del otro jugador.");
                    if (m.Soldier == null)
                    {
                        w.Holder = null;
                        return CampaignResult.Success();
                    }
                    var soldier = c.Soldiers.FirstOrDefault((x) => x.Id == m.Soldier && x.Alive);
                    if (soldier == null || !Content.CommandsFor(slot, soldier.Owner, ctx.Connected)) return CampaignResult.Failure("Solo puedes armar a tus soldados.");
                    var hand = Content.FoundWeapons[w.Kind].Slot;
                    foreach (var other in c.FoundWeapons ?? new List<FoundWeapon>())
                        if (other.Holder == soldier.Id && Content.FoundWeapons[other.Kind].Slot == hand) other.Holder = null;
                    w.Holder = soldier.Id;
                    return CampaignResult.Success();
                }

                case RepairCommand m:
                {
                    var w = (c.FoundWeapons ?? new List<FoundWeapon>()).FirstOrDefault((x) => x.Id == m.Weapon);
                    var blocked = RepairBlocker(c, slot, m.Weapon, ctx.Connected);
                    if (blocked != null || w == null) return CampaignResult.Failure(blocked ?? "Esa arma no está en la Armería.");
                    var parts = Math.Min(c.Parts ?? 0, RepairParts(w));
                    c.Parts = (c.Parts ?? 0) - parts;
                    w.Condition = Math.Min(100, (w.Condition ?? 100) + parts * Content.RepairPerPart);
                    if (w.Condition >= 100) w.Condition = null;
                    return CampaignResult.Success();
                }

                case ModCommand m:
                {
                    var w = (c.FoundWeapons ?? new List<FoundWeapon>()).FirstOrDefault((x) => x.Id == m.Weapon);
                    var blocked = ModBlocker(c, slot, m.Weapon, m.Mod, m.On, ctx.Connected);
                    if (blocked != null || w == null) return CampaignResult.Failure(blocked ?? "Esa arma no está en la Armería.");
                    var stock = c.WeaponMods ??= new Dictionary<string, int>();
                    if (m.On)
                    {
                        w.Mods = (w.Mods ?? new List<string>()).Append(m.Mod).ToList();
                        stock[m.Mod] = (stock.TryGetValue(m.Mod, out var n) ? n : 0) - 1;
                    }
                    else
                    {
                        w.Mods = (w.Mods ?? new List<string>()).Where((x) => x != m.Mod).ToList();
                        if (w.Mods.Count == 0) w.Mods = null;
                        stock[m.Mod] = (stock.TryGetValue(m.Mod, out var n) ? n : 0) + 1;
                    }
                    return CampaignResult.Success();
                }

                case VoteCommand m:
                {
                    var p = c.Proposal;
                    if (p == null) return CampaignResult.Failure("No hay ninguna propuesta.");
                    if (p.By == slot) return CampaignResult.Failure("No puedes votar tu propia propuesta.");
                    c.Proposal = null;
                    if (!m.Accept)
                    {
                        Log(c, $"Propuesta rechazada: {p.Text}.", LogTones.Bad);
                        return CampaignResult.Success();
                    }
                    // What no longer goes through (the supplies spent since) is dropped and said so: a refusal here would leave
                    // both screens showing a proposal the campaign no longer has, and nothing new could be proposed.
                    var error = ValidateAction(c, p.Action);
                    if (error != null)
                    {
                        Log(c, $"Propuesta aceptada, pero ya no se puede: {p.Text}. {error}", LogTones.Bad);
                        return CampaignResult.Success();
                    }
                    ApplyAction(c, p.Action, ctx);
                    return CampaignResult.Success();
                }

                case WithdrawCommand _:
                    if (c.Proposal?.By != slot) return CampaignResult.Failure("No tienes ninguna propuesta pendiente.");
                    c.Proposal = null;
                    return CampaignResult.Success();

                // Calling it off goes to a vote with the other one here, as choosing it (Arturo, 26/10); TypeScript called it
                // off at once. Alone, at once, as before.
                case CancelMissionCommand _:
                    return Propose(c, new CancelMissionAction { Id = c.ActiveMission ?? "" }, ctx);

                case PostCommand m:
                {
                    var blocked = PostBlocker(c, slot, m.Soldier, m.At);
                    if (blocked != null) return CampaignResult.Failure(blocked);
                    var posts = c.SiegePosts ??= new Dictionary<string, Vec2>();
                    if (m.At != null) posts[m.Soldier] = new Vec2 { X = m.At.X, Y = m.At.Y };
                    else posts.Remove(m.Soldier);
                    c.LaunchReady = new bool[2];
                    return CampaignResult.Success();
                }

                case SquadCommand m:
                {
                    if (c.ActiveMission == null) return CampaignResult.Failure("Primero elegid una misión.");
                    // In a siege every soldier fit to fight defends (docs/FASE12.md §11.5).
                    if (ActiveOffer(c)?.Siege == true) return CampaignResult.Failure(Lore.BaseSiegeText.Defenders);
                    var s = c.Soldiers.FirstOrDefault((x) => x.Id == m.Soldier);
                    // Each manages their own, the other here or not (#43).
                    if (s == null || s.Owner != slot) return CampaignResult.Failure("Solo puedes elegir a tus soldados.");
                    if (!m.On)
                    {
                        c.Squad = c.Squad.Where((id) => id != s.Id).ToList();
                    }
                    else if (!c.Squad.Contains(s.Id))
                    {
                        // «hay que tratarle antes» (text-fixes.ts, Lore 24/10): leísmo, and as the Escuadra's tooltip says it.
                        if (s.Infection != null) return CampaignResult.Failure($"{s.Name} tiene fiebre: necesita el tratamiento antes de salir.");
                        if (!SoldierReady(s)) return CampaignResult.Failure($"{s.Name} no está en condiciones.");
                        var place = SquadPlace(c, slot, ctx.Connected);
                        if (place.Error != null) return CampaignResult.Failure(place.Error);
                        JoinSquad(c, s, place);
                    }
                    c.LaunchReady = new bool[2];
                    return CampaignResult.Success();
                }

                case TreatCommand m:
                {
                    // Two keys, like launching: one proposes, the other confirms (alone, one is enough).
                    var s = c.Soldiers.FirstOrDefault((x) => x.Id == m.Soldier && x.Alive);
                    // «No está contagiado.» (text-fixes.ts, Lore 24/10): it agrees with a soldier who is a woman too.
                    if (s?.Infection == null) return CampaignResult.Failure("No tiene ningún mordisco.");
                    if (!m.Ready)
                    {
                        if (c.Treatment?.Soldier == s.Id) c.Treatment = null;
                        return CampaignResult.Success();
                    }
                    if ((c.Medicines ?? 0) < TreatCost(c)) return CampaignResult.Failure("No hay medicinas suficientes.");
                    if (c.Treatment != null && c.Treatment.Soldier != s.Id) return CampaignResult.Failure("Ya hay otro tratamiento propuesto: confirmadlo o rechazadlo antes.");
                    c.Treatment ??= new Treatment { Soldier = s.Id, Ready = new bool[2] };
                    c.Treatment.Ready[slot] = true;
                    if (EveryoneReady(c.Treatment.Ready, ctx.Connected))
                    {
                        c.Medicines = (c.Medicines ?? 0) - TreatCost(c);
                        s.Infection = null;
                        c.Treatment = null;
                        Log(c, $"{s.Name} recibe el tratamiento: le baja la fiebre.", LogTones.Good);
                    }
                    return CampaignResult.Success();
                }

                case MedicateCommand m:
                {
                    var s = c.Soldiers.FirstOrDefault((x) => x.Id == m.Soldier && x.Alive);
                    // «No está herido.» (text-fixes.ts, Lore 24/10): the same.
                    if (s == null || s.WoundedDays <= 0) return CampaignResult.Failure("No tiene heridas.");
                    if ((c.Medicines ?? 0) < MedicateCost) return CampaignResult.Failure("No hay medicinas.");
                    c.Medicines = (c.Medicines ?? 0) - MedicateCost;
                    s.WoundedDays = Math.Max(0, s.WoundedDays - MedicateDays);
                    // «N días menos de recuperación» (text-fixes.ts, Lore 24/10): in missions, as the Curar button.
                    Log(c, s.WoundedDays != 0 ? $"{s.Name} recibe medicinas: {(MedicateDays == 1 ? "vuelve una misión antes." : $"vuelve {MedicateDays} misiones antes.")}" : $"{s.Name} recibe medicinas y se ha recuperado.", LogTones.Good);
                    return CampaignResult.Success();
                }

                case LaunchCommand m:
                {
                    if (c.ActiveMission == null) return CampaignResult.Failure("Primero elegid una misión.");
                    if (m.Ready && c.Squad.Count == 0) return CampaignResult.Failure("La escuadra está vacía.");
                    if (m.Ready && c.Event != null) return CampaignResult.Failure("Antes hay que decidir qué hacer con el acontecimiento.");
                    c.LaunchReady[slot] = m.Ready;
                    if (m.Ready && EveryoneReady(c.LaunchReady, ctx.Connected))
                    {
                        c.LaunchReady = new bool[2];
                        return CampaignResult.Success(true);
                    }
                    return CampaignResult.Success();
                }

                case AdvanceCommand m:
                {
                    // Waiting a day at the Bastión (Ana and Arturo, 22/10): both say so, and a day goes by as on a mission's
                    // (rations eaten, wounds healing, the Taller's work, the Marea), without going out. Nobody able to go out, the
                    // days go by till someone can (docs/FASE12.md §11.6).
                    if (m.Ready && c.Event != null) return CampaignResult.Failure("Antes hay que decidir qué hacer con el acontecimiento.");
                    if (m.Ready && ActiveOffer(c)?.Siege == true) return CampaignResult.Failure(SiegeNow);
                    c.AdvanceReady[slot] = m.Ready;
                    if (m.Ready && EveryoneReady(c.AdvanceReady, ctx.Connected))
                    {
                        c.AdvanceReady = new bool[2];
                        if (MustWait(c)) for (var i = 0; i < 60 && MustWait(c) && c.Event == null; i++) PassDay(c, ctx.Rng);
                        else PassDay(c, ctx.Rng);
                        SyncMarea(c);
                    }
                    return CampaignResult.Success();
                }

                case RecruitCommand m:
                {
                    // With the other one here, the shared supplies go to a vote (Ana, 21/10 #31); alone, at once.
                    var place = m.Squad == true ? (c.ActiveMission != null ? SquadPlace(c, slot, ctx.Connected) : new SquadPlaceResult { Error = "Primero elegid una misión." }) : null;
                    if (place?.Error != null) return CampaignResult.Failure(place.Error);
                    return Propose(c, new RecruitAction { For = slot, Squad = m.Squad == true ? true : (bool?)null }, ctx);
                }

                case CustomizeCommand m:
                {
                    var s = c.Soldiers.FirstOrDefault((x) => x.Id == m.Soldier);
                    if (s == null || !Content.CommandsFor(slot, s.Owner, ctx.Connected) || !s.Alive) return CampaignResult.Failure("Solo puedes personalizar a tus soldados.");
                    var name = Js.Trim(m.Name);
                    var nickname = Js.Trim(m.Nickname);
                    if (name.Length == 0 || name.Length > Appearances.NameMax) return CampaignResult.Failure($"El nombre debe tener entre 1 y {Appearances.NameMax} letras.");
                    if (nickname.Length > Appearances.NicknameMax) return CampaignResult.Failure($"El apodo puede tener hasta {Appearances.NicknameMax} letras.");
                    if (!Appearances.IsAppearance(m.Appearance)) return CampaignResult.Failure("Ese aspecto no es válido.");
                    s.Name = name;
                    s.Nickname = nickname;
                    // The sex comes with the name: a look sent without it keeps the soldier's.
                    var look = CopyAppearance(m.Appearance);
                    look.Sex = m.Appearance.Sex ?? s.Appearance.Sex;
                    s.Appearance = Appearances.NormalizeAppearance(look);
                    return CampaignResult.Success();
                }

                case UnlockCommand m:
                {
                    var s = c.Soldiers.FirstOrDefault((x) => x.Id == m.Soldier);
                    // Only its own commander, the other here or not (Arturo, 18/10 #42).
                    if (s == null || s.Owner != slot || !s.Alive) return CampaignResult.Failure("Solo puedes elegir las habilidades de tus soldados.");
                    var blocked = Bars.UnlockBlocker(s.Skills, s.Board, m.Cell);
                    if (blocked != null) return CampaignResult.Failure(blocked);
                    s.Board.Add(m.Cell);
                    Log(c, $"{s.Name} aprende {Board.BoardCells[m.Cell].Name}.", LogTones.Good);
                    return CampaignResult.Success();
                }

                case BuyCommand m:
                    return Propose(c, new BuyAction { Item = m.Item }, ctx);

                case EquipCommand m:
                {
                    var s = c.Soldiers.FirstOrDefault((x) => x.Id == m.Soldier);
                    if (s == null || !Content.CommandsFor(slot, s.Owner, ctx.Connected) || !s.Alive) return CampaignResult.Failure("Solo puedes equipar a tus soldados.");
                    var now = m.Slot == ItemSlots.Utility ? s.Loadout.Utility : s.Loadout.Ammo;
                    if (!string.IsNullOrEmpty(m.Item))
                    {
                        // The belt (docs/FASE9.md §4): two places for any accessory; the special rounds are the weapons' mods now.
                        if (Content.Items[m.Item!].Slot != ItemSlots.Utility) return CampaignResult.Failure("Eso no va en el cinturón.");
                        if (now != m.Item && ItemsFree(c, m.Item!) <= 0) return CampaignResult.Failure("No quedan unidades libres de ese objeto.");
                    }
                    if (m.Slot == ItemSlots.Utility) s.Loadout.Utility = m.Item;
                    else s.Loadout.Ammo = m.Item;
                    return CampaignResult.Success();
                }

                default:
                    // An order of an older version (the plots of before) or a mistyped one: refused, never a crash.
                    return CampaignResult.Failure("Esa orden no existe.");
            }
        }
    }
}
