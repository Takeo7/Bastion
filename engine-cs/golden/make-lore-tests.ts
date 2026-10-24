/**
 * Writes engine-cs/tests/LoreGoldenTests.cs: every text of lore.ts that the
 * C# Lore has, and every function of it called with a few cases, with what
 * lore.ts says in TypeScript as the expected value (docs/UNITY.md §3).
 *
 *   npx tsx engine-cs/golden/make-lore-tests.ts
 *
 * Each case is the C# expression and the same call in TypeScript; tables are
 * walked whole, so a text changed on one side and not the other fails. Every
 * expected text goes through text-fixes.ts first: what was fixed in C# only.
 */
import { mkdirSync, readFileSync, writeFileSync } from 'fs';
import * as L from '../../packages/engine/src/lore';
import { FOUND_WEAPONS } from '../../packages/engine/src/content';
import { SKILL_IDS } from '../../packages/engine/src/skills';
import * as Q from '../../packages/engine/src/quests';
import type { MissionOffer, MissionReward } from '../../packages/engine/src/campaign';
import { fixText, recapUrgent } from './text-fixes';
import { nextSteps } from '../../packages/engine/src/campaign';
import * as T from '../../packages/engine/src/transmissions';
import type { CampaignState } from '../../packages/engine/src/campaign';

const lit = (v: unknown): string => (v === null || v === undefined ? 'null' : typeof v === 'string' ? JSON.stringify(v) : String(v));
const lines: string[] = [];
const eq = (expected: unknown, cs: string) =>
  lines.push(
    typeof expected === 'boolean'
      ? `            Assert.${expected ? 'True' : 'False'}(${cs});`
      : expected === null || expected === undefined
        ? `            Assert.Null(${cs});`
        : `            Assert.Equal(${lit(typeof expected === 'string' ? fixText(expected) : expected)}, ${cs});`,
  );
const cap = (k: string) => `${k[0]!.toUpperCase()}${k.slice(1)}`;
const list = (xs: readonly string[]) => `new string[] { ${xs.map(lit).join(', ')} }`;
/** An expected list of texts, through text-fixes.ts as every expected text. */
const fixedList = (xs: readonly string[]) => list(xs.map(fixText));
const section = (name: string) => lines.push('', `            // ${name}`);

// ---------------------------------------------------------------- the head
section('head');
eq(L.PARTITURA_LEVEL, 'Lore.PartituraLevel');
eq(L.BUS.name, 'Lore.Bus.Name');
eq(L.BUS.to, 'Lore.Bus.To');
eq(L.BUS.of, 'Lore.Bus.Of');
eq(L.BUS.in, 'Lore.Bus.In');
for (const level of [1, 3, 4, 8]) {
  eq(L.doomName(level), `Lore.DoomName(${level})`);
  eq(L.doomSubject(level), `Lore.DoomSubject(${level})`);
  for (const doom of [0, 5, 12, 14]) eq(L.doomStatus(level, doom), `Lore.DoomStatus(${level}, ${doom})`);
  for (const delta of [-2, -1, 1, 3]) eq(L.doomChangeText(level, delta), `Lore.DoomChangeText(${level}, ${delta})`);
}

// ---------------------------------------------------------------- tables
section('enemy files');
for (const [id, f] of Object.entries(L.ENEMY_FILES)) {
  eq(f!.what, `Lore.EnemyFiles[${lit(id)}].What`);
  eq(f!.does, `Lore.EnemyFiles[${lit(id)}].Does`);
  eq(f!.tip, `Lore.EnemyFiles[${lit(id)}].Tip`);
}
eq(Object.keys(L.ENEMY_FILES).length, 'Lore.EnemyFiles.Count');

section('regions');
for (const [id, r] of Object.entries(L.REGION_LORE)) {
  eq(r.ally, `Lore.RegionLores[${lit(id)}].Ally`);
  eq(r.situation, `Lore.RegionLores[${lit(id)}].Situation`);
}
eq(Object.keys(L.REGION_LORE).length, 'Lore.RegionLores.Count');

section('searching');
for (const [id, s] of Object.entries(L.SEARCH_SPOTS)) eq(s!.name, `Lore.SearchSpots[${lit(id)}].Name`);
for (const [name, xs] of [['EmptyFinds', L.EMPTY_FINDS], ['MedicineFinds', L.MEDICINE_FINDS], ['FoodFinds', L.FOOD_FINDS], ['PartsFinds', L.PARTS_FINDS], ['MagFinds', L.MAG_FINDS]] as const) {
  lines.push(`            Assert.Equal(${fixedList(xs)}, Lore.${name});`);
}
for (const [id, v] of Object.entries(L.ITEM_FINDS)) eq(v, `Lore.ItemFinds[${lit(id)}]`);
eq(Object.keys(L.ITEM_FINDS).length, 'Lore.ItemFinds.Count');
for (const n of [1, 2, 7]) {
  eq(L.rations(n), `Lore.Rations(${n})`);
  eq(L.partsCount(n), `Lore.PartsCount(${n})`);
  eq(L.magsCount(n), `Lore.MagsCount(${n})`);
}
for (const d of ['lootWeapons', 'lootFood', 'wardrobe']) eq(L.boxTitle(d as never), `Lore.BoxTitle(${lit(d)})`);

section('people and outfits');
for (const [k, v] of Object.entries(L.NAMED_SEX)) eq(v, `Lore.NamedSex[${lit(k)}]`);
for (const [k, v] of Object.entries(L.OUTFIT_NAMES)) eq(v, `Lore.OutfitNames[${lit(k)}]`);
eq(Object.keys(L.OUTFIT_NAMES).length, 'Lore.OutfitNames.Count');

// ---------------------------------------------------------------- objective
section('objective');
eq(L.DEFAULT_ITEM, 'Lore.DefaultItem');
const items = ['la insulina', 'el registro de prisioneros', 'las notas', 'los cuadernos', 'el dron', 'la carga'];
for (const item of items) {
  eq(L.itemTag(item), `Lore.ItemTag(${lit(item)})`);
  eq(L.itemTakenFloat(item), `Lore.ItemTakenFloat(${lit(item)})`);
  eq(L.itemTakenLine('Ana', item), `Lore.ItemTakenLine("Ana", ${lit(item)})`);
  eq(L.itemDroppedFloat(item), `Lore.ItemDroppedFloat(${lit(item)})`);
  eq(L.itemDroppedLine(item), `Lore.ItemDroppedLine(${lit(item)})`);
  eq(L.itemPartnerLine('Ana', item), `Lore.ItemPartnerLine("Ana", ${lit(item)})`);
  for (const phase of ['loose', 'carried', 'aboard', 'out'] as const) {
    eq(L.itemObjective(item, phase), `Lore.ItemObjective(${lit(item)}, ${lit(phase)})`);
    eq(L.itemObjective(item, phase, 'Mateo'), `Lore.ItemObjective(${lit(item)}, ${lit(phase)}, "Mateo")`);
  }
}
const vips = ['Dra. Irina Sokolova', 'Ing. Kenji Mori', 'Alcaldesa Mara Velasco', 'Dr. Femi Adebayo', 'Agente Claire Dubois', 'Mei Takahara'];
for (const vip of vips) for (const phase of ['captive', 'loose', 'aboard', 'out'] as const) eq(L.vipObjective(vip, phase), `Lore.VipObjective(${lit(vip)}, ${lit(phase)})`);
for (const alarm of [undefined, 'police', 'ambulance', 'factory'] as const) {
  eq(L.alarmObjective(alarm), `Lore.AlarmObjective(${lit(alarm)})`);
  eq(L.alarmDoneLine('Ana', alarm), `Lore.AlarmDoneLine("Ana", ${lit(alarm)})`);
}
for (const [k, v] of Object.entries(L.OBJECTIVE_DONE)) eq(v, `Lore.ObjectiveDone[${lit(k)}]`);
for (const [k, v] of Object.entries(L.ALARMS)) {
  eq(v.name, `Lore.Alarms[${lit(k)}].Name`);
  eq(v.find, `Lore.Alarms[${lit(k)}].Find`);
}
for (const kind of ['elimination', 'recovery', 'sabotage', 'hack', 'rescue', 'scavenge'] as const) {
  eq(L.operationAim(kind), `Lore.OperationAim(${lit(kind)})`);
  eq(L.operationAim(kind, { item: 'la insulina', vip: 'Dra. Irina Sokolova', alarm: 'ambulance' }), `Lore.OperationAim(${lit(kind)}, item: "la insulina", vip: "Dra. Irina Sokolova", alarm: "ambulance")`);
  eq(L.operationAim(kind, { vip: 'Ing. Kenji Mori' }), `Lore.OperationAim(${lit(kind)}, vip: "Ing. Kenji Mori")`);
}
eq(L.operationAim('sabotage', { facility: true, alarm: 'factory' }), 'Lore.OperationAim("sabotage", alarm: "factory", facility: true)');
eq(L.operationAim('elimination', { final: true }), 'Lore.OperationAim("elimination", final: true)');
for (const id of Object.keys(L.REGION_LORE) as (keyof typeof L.REGION_LORE)[]) {
  eq(L.nestNoContactTip(id, 50), `Lore.NestNoContactTip(${lit(id)}, 50)`);
  eq(L.contactAim(id, 25), `Lore.ContactAim(${lit(id)}, 25)`);
}
// Not in lore.ts (Lore, 24/10, approved by Gameplay): the Mapa's «Nidos» head showed GLOSSARY.Nido, which names the
// Marea before level 4, when nobody knows of it. From level 4 it is the glossary's line; before, «la infestación».
for (const level of [1, 3, 4, 8])
  eq(level >= L.PARTITURA_LEVEL ? L.GLOSSARY.Nido : 'Sitio donde se juntan los zombis. Hace crecer la infestación; quemarlo la hace retroceder.', `Lore.NestTip(${level})`);
for (const zone of ['Bielsa', 'El Pantano Gris', 'Los Llanos de Ceniza']) eq(L.rumorAim(zone), `Lore.RumorAim(${lit(zone)})`);
for (const kind of ['arms', 'ammo', 'armor']) eq(L.craftAim(kind, 'un fusil de asalto'), `Lore.CraftAim(${lit(kind)}, "un fusil de asalto")`);

// ---------------------------------------------------------------- evacuate
section('evacuate');
const situations = [
  { clearing: true, ready: false, left: [] },
  { clearing: false, ready: false, left: ['Ana'] },
  { clearing: false, ready: true, left: ['Ana', 'Mateo', 'Chiara'] },
  { clearing: false, ready: true, left: [] },
];
for (const o of situations) {
  const cs = `new Lore.EvacSituation(${o.clearing}, ${o.ready}, ${list(o.left)})`.replace(/True|False/g, (b) => b.toLowerCase());
  const csO = cs.replace('(true', '(true').replace(/\b(true|false)\b/g, (b) => b);
  for (const partner of ['Ana', null]) {
    const p = L.evacPrompt(o, partner);
    eq(p.title, `Lore.EvacPrompt(${csO}, ${lit(partner)}).Title`);
    eq(p.text, `Lore.EvacPrompt(${csO}, ${lit(partner)}).Text`);
    eq(p.go, `Lore.EvacPrompt(${csO}, ${lit(partner)}).Go`);
    eq(p.wait, `Lore.EvacPrompt(${csO}, ${lit(partner)}).Wait`);
  }
  const a = L.evacAsk('Arturo', o);
  eq(a.title, `Lore.EvacAsk("Arturo", ${csO}).Title`);
  eq(a.text, `Lore.EvacAsk("Arturo", ${csO}).Text`);
  eq(a.accept, `Lore.EvacAsk("Arturo", ${csO}).Accept`);
  eq(a.refuse, `Lore.EvacAsk("Arturo", ${csO}).Refuse`);
}
eq(L.evacWaiting('Ana'), 'Lore.EvacWaiting("Ana")');
eq(L.evacRefusedLine('Ana'), 'Lore.EvacRefusedLine("Ana")');
for (const out of [[], ['Ana'], ['Ana', 'Mateo'], ['Ana', 'Mateo', 'Chiara']]) eq(L.evacDepartLine(out), `Lore.EvacDepartLine(${list(out)})`);
for (const [k, v] of Object.entries(L.EVAC_TEXT)) eq(v, `Lore.EvacText.${k[0]!.toUpperCase()}${k.slice(1)}`);
for (const [k, v] of Object.entries(L.TIME_TEXT)) eq(v, `Lore.TimeText.${k[0]!.toUpperCase()}${k.slice(1)}`);
for (const n of [5, 1]) eq(L.turnsLeftWarning(n), `Lore.TurnsLeftWarning(${n})`);
for (const mine of [true, false]) {
  eq(L.proposalClock('1:25', mine), `Lore.ProposalClock("1:25", ${mine})`);
  eq(L.evacClock('0:40', mine), `Lore.EvacClock("0:40", ${mine})`);
}
for (const [k, v] of Object.entries(L.EVAC_BAR)) eq(v, `Lore.EvacBar.${k[0]!.toUpperCase()}${k.slice(1)}`);
eq(L.evacBarNotice('Ana'), 'Lore.EvacBarNotice("Ana")');
eq(L.BITE_TAG, 'Lore.BiteTag');
eq(L.BITE_FLOAT, 'Lore.BiteFloat');
eq(L.BITE_NO_BOARD, 'Lore.BiteNoBoard');
for (const q of [true, false]) eq(L.biteNote(q), `Lore.BiteNote(${q})`);
eq(L.biteLossLine('Ana'), 'Lore.BiteLossLine("Ana")');
for (const [k, v] of Object.entries(L.ABANDON_TEXT)) eq(v, `Lore.AbandonText.${k[0]!.toUpperCase()}${k.slice(1)}`);
eq(L.abandonAsk('Arturo'), 'Lore.AbandonAsk("Arturo")');
eq(L.abandonBarNotice('Arturo'), 'Lore.AbandonBarNotice("Arturo")');
eq(L.abandonRefusedLine('Arturo'), 'Lore.AbandonRefusedLine("Arturo")');
eq(L.leftBehindLine('Ana'), 'Lore.LeftBehindLine("Ana")');

// ---------------------------------------------------------------- quit, settings, rubble
section('quit, settings, rubble');
for (const [k, v] of Object.entries(L.QUIT_TEXT)) eq(v, `Lore.QuitText.${k[0]!.toUpperCase()}${k.slice(1)}`);
eq(L.quitHostWarning('Ana'), 'Lore.QuitHostWarning("Ana")');
eq(L.quitGuestWarning('Arturo'), 'Lore.QuitGuestWarning("Arturo")');
for (const [k, v] of Object.entries(L.HOST_CLOSED)) eq(v, `Lore.HostClosed.${k[0]!.toUpperCase()}${k.slice(1)}`);
eq(L.hostClosedLine('Arturo'), 'Lore.HostClosedLine("Arturo")');
eq(L.hostClosedLine(), 'Lore.HostClosedLine()');
eq(L.REJOIN_LINE, 'Lore.RejoinLine');
for (const [k, v] of Object.entries(L.SETTINGS_TEXT)) eq(v, `Lore.SettingsText.${k[0]!.toUpperCase()}${k.slice(1)}`);
eq(L.planFollowedLine('Mateo'), 'Lore.PlanFollowedLine("Mateo")');
eq(L.planBlockedLine('Mateo'), 'Lore.PlanBlockedLine("Mateo")');
for (const [k, v] of Object.entries(L.RUBBLE_TEXT)) eq(v, `Lore.RubbleText.${k[0]!.toUpperCase()}${k.slice(1)}`);
for (const w of [1, 3]) eq(L.breachLine(w), `Lore.BreachLine(${w})`);
for (const n of [0, 1, 2, 3, 5]) eq(L.fallLine('Ana', n), `Lore.FallLine("Ana", ${n})`);

// ---------------------------------------------------------------- base and siege
section('base and siege');
L.VEHICLES.forEach((v, i) => {
  eq(v.name, `Lore.Vehicles[${i}].Name`);
  eq(v.the, `Lore.Vehicles[${i}].The`);
  eq(v.a, `Lore.Vehicles[${i}].A`);
  eq(v.female, `Lore.Vehicles[${i}].Female`);
  eq(v.line, `Lore.Vehicles[${i}].Line`);
});
for (const zone of ['Bielsa', 'El Pantano Gris', 'La Vega']) {
  eq(L.inZone(zone), `Lore.InZone(${lit(zone)})`);
  for (const tier of [1, 2, 3]) {
    eq(L.vehicleMissionName(tier, zone), `Lore.VehicleMissionName(${tier}, ${lit(zone)})`);
    eq(L.vehicleBriefing(tier, zone), `Lore.VehicleBriefing(${tier}, ${lit(zone)})`);
  }
}
for (const [tier, seats] of [[1, 6], [2, 7], [3, 8], [1, 12]]) eq(L.vehicleWonLine(tier!, seats!), `Lore.VehicleWonLine(${tier}, ${seats})`);
for (const who of ['Chiara Rossi', 'Ana']) {
  eq(L.fallenPackLine(who), `Lore.FallenPackLine(${lit(who)})`);
  eq(L.fallenPackMark(who), `Lore.FallenPackMark(${lit(who)})`);
  eq(L.fallenPackAction(who), `Lore.FallenPackAction(${lit(who)})`);
  eq(L.fallenPackTakenLine('Mateo', who, 'un fusil de asalto'), `Lore.FallenPackTakenLine("Mateo", ${lit(who)}, "un fusil de asalto")`);
}
eq(L.LIVING_TIP, 'Lore.LivingTip');
for (const [k, v] of Object.entries(L.BASE_SIEGE_TEXT)) eq(v, `Lore.BaseSiegeText.${k[0]!.toUpperCase()}${k.slice(1)}`);
lines.push(`            Assert.Equal(${fixedList(L.SIDES)}, Lore.Sides);`);
for (const [k, v] of Object.entries(L.SIEGE_TEXT)) eq(v, `Lore.SiegeText.${k[0]!.toUpperCase()}${k.slice(1)}`);
eq(L.siegeObjective(), 'Lore.SiegeObjective()');
eq(L.siegeObjectiveTip(11), 'Lore.SiegeObjectiveTip(11)');
for (const [turn, now] of [[3, 3], [4, 3], [7, 3], [2, 3]]) {
  eq(L.waveDueLine(turn!, now!), `Lore.WaveDueLine(${turn}, ${now})`);
  eq(L.waveDueShort(turn!, now!), `Lore.WaveDueShort(${turn}, ${now})`);
}
for (const [n, total] of [[2, 5], [5, 5]]) eq(L.waveEntersLine(n!, total!), `Lore.WaveEntersLine(${n}, ${total})`);
for (const kind of ['door', 'barricade'] as const) {
  eq(L.breakableTip(kind, 12, 18), `Lore.BreakableTip(${lit(kind)}, 12, 18)`);
  eq(L.GIVE_WAY_FLOAT[kind], `Lore.GiveWayFloat[${lit(kind)}]`);
  eq(L.giveWayLine(kind), `Lore.GiveWayLine(${lit(kind)})`);
  for (const side of [0, 1, 2, 3, 7]) eq(L.giveWayLine(kind, side), `Lore.GiveWayLine(${lit(kind)}, ${side})`);
}
for (const side of [0, 3]) eq(L.breachMark(side), `Lore.BreachMark(${side})`);

// ---------------------------------------------------------------- the clearing
section('the clearing');
for (const [id, t] of Object.entries(L.PIECE_TEXT) as [Parameters<typeof L.holdingLine>[0], (typeof L.PIECE_TEXT)[keyof typeof L.PIECE_TEXT]][]) {
  const at = `Lore.PieceTexts.All[${lit(id)}]`;
  eq(t.name, `${at}.Name`);
  eq(t.the, `${at}.The`);
  eq(t.a, `${at}.A`);
  eq(t.line, `${at}.Line`);
  lines.push(t.levels ? `            Assert.Equal(${fixedList(t.levels)}, ${at}.Levels);` : `            Assert.Null(${at}.Levels);`);
  for (const level of [1, 2, 3, 4]) {
    eq(L.pieceFigure(id, level), `Lore.PieceFigure(${lit(id)}, ${level})`);
    eq(L.pieceFigureShort(id, level), `Lore.PieceFigureShort(${lit(id)}, ${level})`);
    eq(L.upgradeLine(id, level), `Lore.UpgradeLine(${lit(id)}, ${level})`);
  }
  eq(L.holdingLine(id), `Lore.HoldingLine(${lit(id)})`);
  eq(L.placeHint(id, { supplies: 6 }), `Lore.PlaceHint(${lit(id)}, 6)`);
  eq(L.placeHint(id, { supplies: 15, parts: 1 }, true), `Lore.PlaceHint(${lit(id)}, 15, 1, true)`);
  eq(L.placeHint(id, { supplies: 6 }, false, 'Hay árboles: tala antes.'), `Lore.PlaceHint(${lit(id)}, 6, 0, false, "Hay árboles: tala antes.")`);
  eq(L.pieceHint(id, {}), `Lore.PieceHint(${lit(id)})`);
  eq(L.pieceHint(id, { hp: [5, 15] }), `Lore.PieceHint(${lit(id)}, (5, 15))`);
  eq(L.pieceHint(id, { hp: [5, 15], level: 2 }), `Lore.PieceHint(${lit(id)}, (5, 15), level: 2)`);
  for (const state of ['spent', 'silent', 'broken'] as const) eq(L.pieceHint(id, { hp: [5, 15], state, level: 2 }), `Lore.PieceHint(${lit(id)}, (5, 15), ${lit(state)}, 2)`);
}
// Fixed in C# only (Gameplay, 24/10): lore.ts's «Talar un árbol · 3 §» doesn't say whether it is paid or earned;
// «Cuesta», as placeHint. The rest of the line is lore.ts's.
for (const trees of [1, 2, 12]) eq(L.fellHint(trees).replace(/ · (\d+) §$/, ' · Cuesta $1 §'), `Lore.FellHint(${trees})`);
for (const [k, v] of Object.entries(L.NEW_BRIEF)) eq(v, `Lore.NewBrief[${lit(k)}]`);
for (const [k, v] of Object.entries(L.PIECE_STATE)) {
  eq(v, `Lore.PieceState.${cap(k)}`);
  eq(v, `Lore.PieceState.ByKey[${lit(k)}]`);
}
for (const [k, v] of Object.entries(L.BUILD_TEXT)) {
  if (typeof v === 'string') eq(v, `Lore.BuildText.${cap(k)}`);
  else for (const [k2, v2] of Object.entries(v)) eq(v2, `Lore.BuildText.${cap(k)}.${cap(k2)}`);
}
L.BASTION_LEVELS.forEach((b, i) => {
  eq(b.name, `Lore.BastionLevels[${i}].Name`);
  eq(b.the, `Lore.BastionLevels[${i}].The`);
  eq(b.a, `Lore.BastionLevels[${i}].A`);
});
for (const level of [-1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9]) {
  eq(L.bastionLevelName(level), `Lore.BastionLevelName(${level})`);
  eq(L.bastionLevelThe(level), `Lore.BastionLevelThe(${level})`);
  eq(L.bastionLevelA(level), `Lore.BastionLevelA(${level})`);
  eq(L.levelUpLine(level, 160), `Lore.LevelUpLine(${level}, 160)`);
  eq(L.timeHelp(level), `Lore.TimeHelp(${level})`);
  eq(L.needsLevelLine(level), `Lore.NeedsLevelLine(${level})`);
}
for (const [k, v] of Object.entries(L.LEVEL_TEXT)) eq(v, `Lore.LevelText.${cap(k)}`);
for (const n of [-1, 0, 1, 2, 7]) eq(L.outingsMark(n), `Lore.OutingsMark(${n})`);
for (const [hp, full] of [[12, 30], [0, 3]]) {
  eq(L.holdsLine(hp!, full!), `Lore.HoldsLine(${hp}, ${full})`);
  eq(L.usesLine(hp!, full!), `Lore.UsesLine(${hp}, ${full})`);
}
for (const action of ['repair', 'upgrade', 'rearm', 'dismantle'] as const)
  for (const [supplies, parts] of [[0, 0], [4, 0], [0, 1], [0, 3], [15, 2], [1, 1]]) eq(L.actionPrice(action, { supplies, parts }), `Lore.ActionPrice(${lit(action)}, ${supplies}, ${parts})`);
eq(L.actionPrice('repair', {}), 'Lore.ActionPrice("repair")');
for (const n of [-1, 0, 1, 12]) eq(L.dismantleBack(n), `Lore.DismantleBack(${n})`);
for (const [supplies, parts] of [[6, 0], [15, 1], [15, 2], [0, 0], [1, 0]]) eq(L.buildCostLine({ supplies: supplies!, parts }), `Lore.BuildCostLine(${supplies}, ${parts})`);
eq(L.buildCostLine({ supplies: 6 }), 'Lore.BuildCostLine(6)');
for (const [supplies, parts] of [[5, 0], [0, 1], [5, 2], [1, 0], [0, 2], [1, 1], [0, 0], [2, 1]]) eq(L.buildShortLine(supplies!, parts), `Lore.BuildShortLine(${supplies}, ${parts})`);
eq(L.buildShortLine(5), 'Lore.BuildShortLine(5)');
eq(L.rationsHelp(), 'Lore.RationsHelp()');
eq(L.rationsHelp(3), 'Lore.RationsHelp(3)');
for (const n of [0, 1]) eq(L.rationsLowLine(n), `Lore.RationsLowLine(${n})`);
for (const n of [0, 1, 2, 3, 5]) {
  eq(L.inMissions(n), `Lore.InMissions(${n})`);
  eq(L.siegeWarning(n), `Lore.SiegeWarning(${n})`);
  eq(L.siegeWarningShort(n), `Lore.SiegeWarningShort(${n})`);
}
const heardSides = [[0], [2], [0, 1], [2, 0, 2], [3, 1], [0, 1, 2], [1, 3, 2], [0, 1, 2, 3], [3, 2, 1, 0, 0]];
for (const count of [3, 7, 8, 14, 15, 24, 25, 35, 44, 45, 49, 50, 80])
  for (const sides of heardSides) eq(L.siegeWarning(3, { count, sides }), `Lore.SiegeWarning(3, ${count}, new[] { ${sides.join(', ')} })`);
eq(L.siegeWarning(1, { count: 20, sides: [1] }), 'Lore.SiegeWarning(1, 20, new[] { 1 })');

// ---------------------------------------------------------------- learning, weapons, Expediente, endings
section('learning, weapons, Expediente, endings');
for (const [k, v] of Object.entries(L.BOARD_ABOUT)) eq(v, `Lore.BoardAbout[${lit(k)}]`);
for (const [k, v] of Object.entries(L.WEAPON_LABELS)) eq(v, `Lore.WeaponLabels.${cap(k)}`);
for (const [k, v] of Object.entries(L.REACH)) {
  eq(v.name, `Lore.Reaches[${lit(k)}].Name`);
  eq(v.line, `Lore.Reaches[${lit(k)}].Line`);
}
for (const what of ['clip', 'actions', 'noise'] as const)
  for (const n of [0, 1, 4, 12]) for (const melee of [false, true]) eq(L.weaponFigure(what, n, melee), `Lore.WeaponFigure(${lit(what)}, ${n}, ${melee})`);
eq(L.weaponFigure('clip', 4), 'Lore.WeaponFigure("clip", 4)');
L.EXPEDIENTE.forEach((p, i) => {
  eq(p.title, `Lore.Expediente[${i}].Title`);
  eq(p.summary, `Lore.Expediente[${i}].Summary`);
  lines.push(`            Assert.Equal(${fixedList(p.paragraphs)}, Lore.Expediente[${i}].Paragraphs);`);
  eq(p.note ?? null, `Lore.Expediente[${i}].Note`);
});
eq(L.EXPEDIENTE.length, 'Lore.Expediente.Count');
eq(L.SIMON_CHAPTER, 'Lore.SimonChapter');
eq(L.NWOSU_CHAPTER, 'Lore.NwosuChapter');
for (const [k, v] of Object.entries(L.ENDING_TITLES)) eq(v, `Lore.EndingTitles[${lit(k)}]`);
for (const outcome of ['victory', 'defeat'] as const)
  for (const ending of ['break', 'record', null] as const)
    for (const doomFull of [false, true]) eq(L.epilogue(outcome, ending, doomFull), `Lore.Epilogue(${lit(outcome)}, ${lit(ending)}, ${doomFull})`);

// ---------------------------------------------------------------- backpack, scavenge, marks
section('backpack, scavenge, marks');
for (const who of ['Ana', 'Chiara Rossi'])
  for (const to of [null, 'Kofi'])
    for (const healed of [0, 3]) for (const cured of [false, true]) eq(L.medicineLine(who, to, healed, cured), `Lore.MedicineLine(${lit(who)}, ${lit(to)}, ${healed}, ${cured})`);
eq(L.CURED_FLOAT, 'Lore.CuredFloat');
for (const [k, v] of Object.entries(L.BELT_TEXT)) eq(v, `Lore.BeltText.${cap(k)}`);
for (const item of Object.keys(L.ITEM_FINDS) as Parameters<typeof L.beltLine>[1][]) eq(L.beltLine('Ana', item), `Lore.BeltLine("Ana", ${lit(item)})`);
lines.push(`            Assert.Equal(${fixedList(L.SCAVENGE_BRIEFINGS)}, Lore.ScavengeBriefings.All);`);
for (const [k, v] of Object.entries(L.SCAVENGE)) {
  // Fixed in C# only: since #49 a fallen soldier's backpack stays on the ground, while lore.ts (frozen) still says it is lost.
  if (k === 'carryingTip') {
    eq('Lo que llevan las mochilas ({n}) cuenta si sale en La Lata. La mochila de un caído se queda en el suelo y se puede registrar. Lo que lleva quien se queda fuera, se pierde.', 'Lore.Scavenge.CarryingTip');
    continue;
  }
  eq(v, `Lore.Scavenge.${cap(k)}`);
}
for (const text of [...L.SCAVENGE_BRIEFINGS, L.vehicleBriefing(2, 'Bielsa'), L.PREMISE.lines.join(' '), 'Sin marcas.', '**a** ==b== *c* =d='])
  eq(L.plainMarks(text), `Lore.PlainMarks(${lit(text)})`);

// ---------------------------------------------------------------- searching, loot, glossary, night
section('searching, loot, glossary, night');
type Find = Parameters<typeof L.lootName>[0];
/** A loot as C# builds it: `new Loot { Supplies = 12, Item = "medkit" }`, or null. */
const loot = (f: Find | null) =>
  f === null ? 'null' : `new Loot { ${Object.entries(f).map(([k, v]) => `${cap(k)} = ${lit(v)}`).join(', ')} }`;
const finds: Find[] = [
  {},
  { supplies: 12 },
  { supplies: 1 },
  ...Object.keys(L.ITEM_FINDS).map((item) => ({ item }) as Find),
  { medicines: 1 },
  { medicines: 3 },
  { food: 1 },
  { food: 4 },
  { parts: 1 },
  { parts: 2 },
  ...Object.keys(FOUND_WEAPONS).map((weapon) => ({ weapon }) as Find),
  { mags: 1 },
  { mags: 3 },
  { supplies: 12, parts: 1 },
  { food: 2, medicines: 2, mags: 2 },
];
for (const seed of [0, 7, 13]) {
  for (const decor of [...Object.keys(L.SEARCH_SPOTS), 'crate'])
    for (const f of [null, ...finds]) eq(L.searchLine('Ana', decor as never, f, seed), `Lore.SearchLine("Ana", ${lit(decor)}, ${loot(f)}, ${seed})`);
  for (const spot of ['home', 'office', 'shop', 'store', 'other'])
    for (const f of [null, ...finds]) eq(L.roomSearchLine('Ana', spot as never, f, seed), `Lore.RoomSearchLine("Ana", ${lit(spot)}, ${loot(f)}, ${seed})`);
}
for (const f of [null, ...finds]) eq(L.findLabel(f), `Lore.FindLabel(${loot(f)})`);
for (const f of finds) {
  eq(L.lootName(f), `Lore.LootName(${loot(f)})`);
  eq(L.lootDroppedLine('Ana', f), `Lore.LootDroppedLine("Ana", ${loot(f)})`);
  eq(L.lootTakenLine('Ana', f), `Lore.LootTakenLine("Ana", ${loot(f)})`);
  eq(L.lootGivenLine('Ana', 'Kofi', f), `Lore.LootGivenLine("Ana", "Kofi", ${loot(f)})`);
  eq(L.packFullLine(f), `Lore.PackFullLine(${loot(f)})`);
}
for (const kind of Object.keys(FOUND_WEAPONS) as Parameters<typeof L.wornLine>[1][]) {
  for (const broken of [false, true]) eq(L.wornLine('Ana', kind, broken), `Lore.WornLine("Ana", ${lit(kind)}, ${broken})`);
  eq(L.wornLine('Kofi', kind), `Lore.WornLine("Kofi", ${lit(kind)})`);
  eq(L.weaponEquippedLine('Ana', kind), `Lore.WeaponEquippedLine("Ana", ${lit(kind)})`);
}
const itemIds = Object.keys(L.ITEM_FINDS) as Parameters<typeof L.lootSummary>[1][number][];
const weaponIds = Object.keys(FOUND_WEAPONS) as Parameters<typeof L.lootSummary>[5] & string[];
const summaries: [number, string[], number, number, number, string[]][] = [
  [0, [], 0, 0, 0, []],
  [34, [], 0, 0, 0, []],
  [34, itemIds.slice(0, 1), 2, 3, 0, weaponIds.slice(0, 1)],
  [0, itemIds.slice(1, 3), 1, 1, 1, []],
  [5, [], 0, 0, 4, weaponIds.slice(0, 2)],
  [0, [], 0, 0, 0, weaponIds.slice(2, 3)],
];
for (const [supplies, items, medicines, food, parts, weapons] of summaries)
  eq(L.lootSummary(supplies, items as never, medicines, food, parts, weapons as never), `Lore.LootSummary(${supplies}, ${list(items)}, ${medicines}, ${food}, ${parts}, ${list(weapons)})`);
eq(L.lootSummary(34, [], 2), 'Lore.LootSummary(34, new string[] { }, 2)');
for (const [k, v] of Object.entries(L.GLOSSARY)) eq(v, `Lore.Glossary[${lit(k)}]`);
eq(Object.keys(L.GLOSSARY).length, 'Lore.Glossary.Count');
for (const k of ['label', 'briefing', 'report'] as const) eq(L.NIGHT_TEXT[k], `Lore.NightText.${cap(k)}`);
lines.push(`            Assert.Equal(${fixedList(L.NIGHT_TEXT.rules)}, Lore.NightText.Rules);`);
eq(L.litNote(), 'Lore.LitNote()');
for (const turns of [0, 1, 3]) eq(L.litNote(turns), `Lore.LitNote(${turns})`);

// ---------------------------------------------------------------- learning lines
section('learning lines');
type DeedsTs = Parameters<typeof L.learnedLine>[2];
const deeds = (d: DeedsTs) => `new Deeds { ${Object.entries(d).map(([k, v]) => `[${lit(k)}] = ${v}`).join(', ')} }`;
const deedCases: DeedsTs[] = [
  {},
  { shot: 1 },
  { shot: 3, hit: 2, gunKill: 1 },
  { hit: 0, gunKill: 2 },
  { swing: 1, bladeKill: 12 },
  { blow: 4 },
  { throw: 1, throwHit: 1 },
  { healOther: 1 },
  { healSelf: 2, healOther: 3 },
  { search: 2, fix: 1 },
  { hunker: 1, run: 11 },
  { shot: 2, swing: 1, search: 1, run: 1 },
];
for (const skill of SKILL_IDS) {
  for (const d of deedCases) for (const gained of [0, 1, 3, 4, 9, 40]) eq(L.learnedLine(skill, gained, d), `Lore.LearnedLine(${lit(skill)}, ${gained}, ${deeds(d)})`);
  for (const growth of [[], [7], [5, 10], [5, 6, 7], [5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5]])
    eq(L.barLine('Ana', skill, growth), `Lore.BarLine("Ana", ${lit(skill)}, new int[] { ${growth.join(', ')} })`);
  eq(L.barFillFloat(skill, 7), `Lore.BarFillFloat(${lit(skill)}, 7)`);
  const r = L.barReward(skill);
  eq(r.short, `Lore.BarReward(${lit(skill)}).Short`);
  eq(r.long, `Lore.BarReward(${lit(skill)}).Long`);
}
for (const rank of [-2, -1, 0, 1, 2, 3, 4, 5]) {
  for (const sex of [undefined, 'f', 'm'] as const) {
    const r = L.rankReward(rank, sex);
    const cs = `Lore.RankReward(${rank}${sex ? `, ${lit(sex)}` : ''})`;
    if (!r) eq(null, cs);
    else {
      eq(r.short, `${cs}!.Short`);
      eq(r.long, `${cs}!.Long`);
    }
  }
}

// ---------------------------------------------------------------- premise and valley
section('premise and valley');
eq(L.PREMISE.title, 'Lore.Premise.Title');
lines.push(`            Assert.Equal(${fixedList(L.PREMISE.lines)}, Lore.Premise.Lines);`);
for (const [k, v] of Object.entries(L.ALARM_NAMES)) eq(v, `Lore.AlarmNames[${lit(k)}]`);
for (const biome of ['city', 'wilds', 'facility'] as const) for (const seed of [0, 3, 17, -5, 2147483647]) eq(L.missionName(biome, 'El Pantano Gris', seed), `Lore.MissionName(${lit(biome)}, "El Pantano Gris", ${seed})`);
for (const site of ['roadside', 'industrial'] as const) for (const seed of [1, 6]) eq(L.missionName('city', 'Tarbes', seed, site), `Lore.MissionName("city", "Tarbes", ${seed}, ${lit(site)})`);
for (const [k, v] of Object.entries(L.LANDMARKS)) eq(v, `Lore.Landmarks[${lit(k)}]`);
for (const zone of ['Bielsa', 'El Pantano Gris', 'Los Llanos de Ceniza']) {
  eq(L.toZone(zone), `Lore.ToZone(${lit(zone)})`);
  eq(L.ofZone(zone), `Lore.OfZone(${lit(zone)})`);
  for (const level of [2, 5]) for (const reached of [true, false]) {
    const a = L.mareaAlert(level, zone, reached);
    eq(a.kicker, `Lore.MareaAlert(${level}, ${lit(zone)}, ${reached}).Kicker`);
    eq(a.title, `Lore.MareaAlert(${level}, ${lit(zone)}, ${reached}).Title`);
    eq(a.text, `Lore.MareaAlert(${level}, ${lit(zone)}, ${reached}).Text`);
  }
}
for (const level of [2, 5]) {
  eq(L.floodedContactReason(level, 'Tarbes'), `Lore.FloodedContactReason(${level}, "Tarbes")`);
  eq(L.floodedListLabel(level, ['Tarbes']), `Lore.FloodedListLabel(${level}, new[] { "Tarbes" })`);
  eq(L.floodedListLabel(level, ['Tarbes', 'Koper', 'Rudnik']), `Lore.FloodedListLabel(${level}, new[] { "Tarbes", "Koper", "Rudnik" })`);
  eq(L.floodedZoneNote(level), `Lore.FloodedZoneNote(${level})`);
}
for (const via of [[], ['Bielsa'], ['Bielsa', 'La Vega']]) eq(L.noRoadReason(via), `Lore.NoRoadReason(${list(via)})`);

// ---------------------------------------------------------------- story
section('story');
/** A reward's fields, each against the C# one (`recruit` is an object there: true, or a class's id in old saves). */
const rewardEq = (r: MissionReward | undefined, at: string) => {
  if (!r) return lines.push(`            Assert.Null(${at});`);
  eq(r.credits, `${at}.Credits`);
  lines.push(r.recruit ? `            Assert.True(${at}.Recruit is true);` : `            Assert.Null(${at}.Recruit);`);
  for (const k of ['doom', 'medicines', 'food', 'parts', 'vehicle'] as const) eq(r[k], `${at}.${cap(k)}`);
};
L.STORY.forEach((c, i) => {
  const at = `Lore.Story[${i}]`;
  eq(c.title, `${at}.Title`);
  eq(c.kind, `${at}.Kind`);
  eq(c.biome, `${at}.Biome`);
  eq(c.region, `${at}.Region`);
  eq(c.difficulty, `${at}.Difficulty`);
  rewardEq(c.reward, `${at}.Reward`);
  eq(c.prize, `${at}.Prize`);
  eq(c.briefing, `${at}.Briefing`);
  eq(c.signature, `${at}.Signature`);
  eq(c.living, `${at}.Living`);
  eq(c.item, `${at}.Item`);
  eq(c.vip, `${at}.Vip`);
  eq(c.after, `${at}.After`);
});
eq(L.STORY.length, 'Lore.Story.Count');
eq(L.SIMON_CHAPTER, 'Lore.SimonChapter');
eq(L.NWOSU_CHAPTER, 'Lore.NwosuChapter');
const SIMONS: L.SimonChoice[] = [null, 'trusted', 'refused'];
for (let chapter = 1; chapter <= L.STORY.length; chapter++)
  for (const simon of SIMONS) {
    eq(L.storyBriefing(chapter, simon), `Lore.StoryBriefing(${chapter}, ${lit(simon)})`);
    eq(L.storyVip(chapter, simon), `Lore.StoryVip(${chapter}, ${lit(simon)})`);
  }

// ---------------------------------------------------------------- briefings
section('briefings');
type Offer = Pick<MissionOffer, 'id' | 'kind' | 'region' | 'map' | 'final' | 'facility' | 'story' | 'quest'> & Partial<Pick<MissionOffer, 'night' | 'vip' | 'alarm'>>;
/** An offer as C# builds it, with what the briefings read. */
const offerCs = (o: Offer) => {
  const map = o.map.kind === 'generated' ? `MapSource.Generated(${lit(o.map.biome)}, ${o.map.seed})` : o.map.kind === 'handmade' ? `MapSource.Handmade(${lit(o.map.id)})` : 'new MapSource { Kind = "base" }';
  const parts = [`Id = ${lit(o.id)}`, `Kind = ${lit(o.kind)}`, `Region = ${lit(o.region)}`, `Map = ${map}`];
  if (o.final) parts.push('Final = true');
  if (o.facility) parts.push(`Facility = ${lit(o.facility)}`);
  if (o.story) parts.push(`Story = ${o.story}`);
  if (o.quest) parts.push(`Quest = ${lit(o.quest)}`);
  if (o.night) parts.push('Night = true');
  if (o.vip) parts.push(`Vip = ${lit(o.vip)}`);
  if (o.alarm) parts.push(`Alarm = ${lit(o.alarm)}`);
  return `new MissionOffer { ${parts.join(', ')} }`;
};
const offers: Offer[] = [];
const plain = (id: string, kind: MissionOffer['kind'], region: MissionOffer['region'], map: MissionOffer['map'], more: Partial<Offer> = {}): Offer => ({ id, kind, region, map, final: false, facility: null, story: null, quest: null, ...more });
const KINDS = ['elimination', 'recovery', 'sabotage', 'hack', 'rescue', 'scavenge'] as const;
// Every kind in every zone, with ids and seeds enough for each of its reports (seed % n), by day and by night.
for (const region of Object.keys(L.REGION_LORE) as MissionOffer['region'][])
  for (const kind of KINDS)
    for (let i = 1; i <= 4; i++) offers.push(plain(`m${i}`, kind, region, { kind: 'generated', biome: 'city', seed: i * 7919 }, { night: i % 2 === 0 }));
// Hand-made maps (no seed) and seeds at the edge of 32 bits: the sum is past them in TypeScript.
for (const kind of KINDS)
  for (const map of [{ kind: 'handmade', id: 'clinic' }, { kind: 'generated', biome: 'wilds', seed: 0 }, { kind: 'generated', biome: 'facility', seed: 2147483647 }] as MissionOffer['map'][])
    for (const id of ['m9', 'm10', 'offer-37']) offers.push(plain(id, kind, 'eas', map));
// Who is held, with and without a title, and an ally with «el» (Bielsa's).
for (const vip of [undefined, 'Otto Brandt', 'Dr. Femi Adebayo', 'Dra. Irina Sokolova', 'Alcaldesa Mara Velasco', 'Agente Claire Dubois', 'Ing. Kenji Mori'])
  for (const region of ['weu', 'oce'] as const) for (let i = 1; i <= 3; i++) offers.push(plain(`m${i}`, 'rescue', region, { kind: 'generated', biome: 'city', seed: i }, { vip }));
// What sounds in a sabotage.
for (const alarm of [undefined, 'police', 'ambulance', 'factory'] as const) for (const region of ['weu', 'na'] as const) offers.push(plain('m3', 'sabotage', region, { kind: 'generated', biome: 'wilds', seed: 5 }, { alarm }));
// The story, the final mission, the Nidos and a quest's step.
for (let story = 1; story <= L.STORY.length; story++) for (const night of [false, true]) offers.push(plain(`s${story}`, L.STORY[story - 1]!.kind, L.STORY[story - 1]!.region, { kind: 'generated', biome: 'city', seed: 11 }, { story, night }));
for (const night of [false, true]) offers.push(plain('final', 'elimination', 'na', { kind: 'generated', biome: 'facility', seed: 3 }, { final: true, night }));
offers.push(plain('nest', 'sabotage', 'me', { kind: 'generated', biome: 'facility', seed: 4 }, { facility: 'me', alarm: 'factory' }));
for (const quest of ['sunken', 'faces'] as const) offers.push(plain('q1', 'recovery', 'oce', { kind: 'generated', biome: 'wilds', seed: 8 }, { quest }));
for (const o of offers) {
  const cs = offerCs(o);
  const offer = o as MissionOffer;
  for (const level of o.facility ? [3, 4] : [3]) {
    eq(L.missionBriefing(offer, level), `Lore.MissionBriefing(${cs}, ${level})`);
    if (o.story === L.SIMON_CHAPTER) for (const simon of SIMONS) eq(L.missionBriefing(offer, level, simon), `Lore.MissionBriefing(${cs}, ${level}, ${lit(simon)})`);
  }
  eq(L.briefingSignature(offer), `Lore.BriefingSignature(${cs})`);
  eq(L.recoveryItem(offer), `Lore.RecoveryItem(${cs})`);
  eq(L.recoveryOutdoors(offer), `Lore.RecoveryOutdoors(${cs})`);
}
// How many of each kind's reports came out (lore.ts BRIEFINGS: 3, 3, 2, 3, 3 and 3): said when it runs.
const reports = (kind: string) =>
  new Set(offers.filter((o) => o.kind === kind && !o.story && !o.final && !o.facility && !o.vip && !o.alarm).map((o) => L.missionBriefing({ ...o, night: false } as MissionOffer, 3).replace(/^Aviso [^.]*\. /, ''))).size;
console.log(KINDS.map((k) => `${k} ${reports(k)}`).join(', '));

// ---------------------------------------------------------------- quests
section('quests');
lines.push(`            Assert.Equal(${fixedList(Object.keys(Q.QUESTS))}, Quests.QuestDefs.Keys);`);
lines.push(`            Assert.Equal(${fixedList(Object.keys(Q.QUESTS))}, QuestIds.All);`);
for (const q of Object.values(Q.QUESTS)) {
  const at = `Quests.QuestDefs[${lit(q.id)}]`;
  eq(q.id, `${at}.Id`);
  eq(q.title, `${at}.Title`);
  eq(q.signature, `${at}.Signature`);
  eq(q.hook, `${at}.Hook`);
  eq(q.prize, `${at}.Prize`);
  eq(q.region, `${at}.Region`);
  eq(q.rumor, `${at}.Rumor`);
  eq(q.steps.length, `${at}.Steps.Count`);
  q.steps.forEach((st, i) => {
    eq(st.type, `${at}.Steps[${i}].Type`);
    if (st.type === 'event') {
      const s = `((QuestDecision)${at}.Steps[${i}])`;
      eq(st.title, `${s}.Title`);
      eq(st.text, `${s}.Text`);
      eq(st.options.length, `${s}.Options.Count`);
      st.options.forEach((o, j) => {
        eq(o.label, `${s}.Options[${j}].Label`);
        eq(o.effect, `${s}.Options[${j}].Effect`);
        eq(o.then, `${s}.Options[${j}].Then`);
        eq(o.clue, `${s}.Options[${j}].Clue`);
      });
    } else {
      const s = `((QuestMission)${at}.Steps[${i}])`;
      eq(st.name, `${s}.Name`);
      eq(st.kind, `${s}.Kind`);
      eq(st.biome, `${s}.Biome`);
      eq(st.difficulty, `${s}.Difficulty`);
      eq(st.days, `${s}.Days`);
      eq(st.briefing, `${s}.Briefing`);
      eq(st.item, `${s}.Item`);
      eq(st.outdoors, `${s}.Outdoors`);
      eq(st.vip, `${s}.Vip`);
      if (st.leader) {
        eq(st.leader.template, `${s}.Leader!.Template`);
        eq(st.leader.name, `${s}.Leader!.Name`);
      } else lines.push(`            Assert.Null(${s}.Leader);`);
      eq(st.living, `${s}.Living`);
      rewardEq(st.reward, `${s}.Reward`);
    }
  });
  lines.push(`            Assert.Equal(${fixedList(Object.keys(q.outcomes))}, ${at}.Outcomes.Keys);`);
  for (const [key, o] of Object.entries(q.outcomes)) {
    const s = `${at}.Outcomes[${lit(key)}]`;
    eq(o.text, `${s}.Text`);
    eq(o.tone, `${s}.Tone`);
    for (const k of ['credits', 'doom', 'income', 'contacts', 'tech', 'trait', 'enemy', 'delay', 'retry'] as const) eq(o[k], `${s}.${cap(k)}`);
    if (o.items) {
      eq(Object.keys(o.items).length, `${s}.Items!.Count`);
      for (const [item, n] of Object.entries(o.items)) eq(n, `${s}.Items![${lit(item)}]`);
    } else lines.push(`            Assert.Null(${s}.Items);`);
    if (o.recruit) {
      eq(o.recruit.name, `${s}.Recruit!.Name`);
      eq(o.recruit.rank, `${s}.Recruit!.Rank`);
      eq(o.recruit.trait, `${s}.Recruit!.Trait`);
    } else lines.push(`            Assert.Null(${s}.Recruit);`);
  }
  if (q.clues) {
    eq(q.clues.guilty, `${at}.Clues!.Guilty`);
    eq(q.clues.innocent, `${at}.Clues!.Innocent`);
  } else lines.push(`            Assert.Null(${at}.Clues);`);
}
lines.push(`            Assert.Equal(${fixedList(Object.keys(Q.TRAITS))}, Quests.Traits.Keys);`);
lines.push(`            Assert.Equal(${fixedList(Object.keys(Q.TRAITS))}, TraitIds.All);`);
for (const [id, t] of Object.entries(Q.TRAITS)) {
  eq(t.name, `Quests.Traits[${lit(id)}].Name`);
  eq(t.description, `Quests.Traits[${lit(id)}].Description`);
  eq(t.aim, `Quests.Traits[${lit(id)}].Aim`);
  eq(t.will, `Quests.Traits[${lit(id)}].Will`);
}
eq(Q.RELATIVES.length, 'Quests.Relatives.Count');
Q.RELATIVES.forEach((r, i) => {
  eq(r.name, `Quests.Relatives[${i}].Name`);
  eq(r.relation, `Quests.Relatives[${i}].Relation`);
  eq(r.sex, `Quests.Relatives[${i}].Sex`);
});
lines.push(`            Assert.Equal(${fixedList(Q.SUSPECTS)}, Quests.Suspects);`);
lines.push(`            Assert.Equal(${fixedList(Q.RUMOR_LINES)}, Quests.RumorLines);`);
/** `names` as C# writes it. */
const namesCs = (names: Record<string, string>) => `new System.Collections.Generic.Dictionary<string, string> { ${Object.entries(names).map(([k, v]) => `[${lit(k)}] = ${lit(v)}`).join(', ')} }`;
const NAMES: Record<string, string>[] = [{}, { soldier: 'Ana Ruiz', relation: 'su hermana', relative: 'Mariam' }, { a: 'Pavel Lanz, el electricista', b: 'Rosa Alves, la cocinera', clue: 'Vigilamos a Rosa.', name: 'Rosa', culprit: 'Rosa Alves' }];
const texts = [
  ...Object.values(Q.QUESTS).flatMap((q) => [q.hook, q.prize, ...q.steps.flatMap((st) => (st.type === 'event' ? [st.title, st.text, ...st.options.flatMap((o) => [o.label, o.effect])] : [st.name, st.briefing, st.vip ?? ''])), ...Object.values(q.outcomes).map((o) => o.text)]),
  '{a}{a} y {b}', '{}', '{niño} y {a_1}', 'sin nada',
];
// The C# tables' texts, fixed (text-fixes.ts), into Fill.
for (const names of NAMES) for (const text of new Set(texts.map(fixText))) eq(Q.fill(text, names), `Quests.Fill(${lit(text)}, ${namesCs(names)})`);


// ---------------------------------------------------------------- transmissions
section('transmissions');
lines.push(`            Assert.Equal(${fixedList(Object.keys(T.SPEAKERS))}, SpeakerIds.All);`);
lines.push(`            Assert.Equal(${fixedList(Object.keys(T.SPEAKERS))}, Transmissions.Speakers.Keys);`);
for (const [id, sp] of Object.entries(T.SPEAKERS)) {
  eq(sp.name, `Transmissions.Speakers[${lit(id)}].Name`);
  eq(sp.role, `Transmissions.Speakers[${lit(id)}].Role`);
}
lines.push(`            Assert.Equal(${fixedList([...T.SCENES.map((s) => s.id), T.RECAP.id])}, SceneIds.All);`);
eq(T.SCENES.length, 'Transmissions.Scenes.Count');
T.SCENES.forEach((s, i) => {
  const at = `Transmissions.Scenes[${i}]`;
  eq(s.id, `${at}.Id`);
  eq(s.title, `${at}.Title`);
  eq(s.channel, `${at}.Channel`);
  eq(s.lines.length, `${at}.Lines.Count`);
  s.lines.forEach((l, j) => {
    eq(l.who, `${at}.Lines[${j}].Who`);
    eq(l.text, `${at}.Lines[${j}].Text`);
    eq(l.rule, `${at}.Lines[${j}].Rule`);
    eq(!!l.if, `${at}.Lines[${j}].If != null`);
  });
});
eq(T.RECAP.id, 'Transmissions.Recap.Id');
eq(T.RECAP.title, 'Transmissions.Recap.Title');
eq(T.RECAP.channel, 'Transmissions.Recap.Channel');
eq(T.RECAP.lines.length, 'Transmissions.Recap.Lines.Count');

/**
 * The scenes on campaigns (golden/lore/scenes.json, read by TransmissionsGoldenTests): the states recorded from
 * TypeScript (golden/campaign), UI's sample and states made from them to reach every scene's condition (a patch: a
 * path and its new value, or one more item for a list). For each, what unlockedScenes, nextScene and sceneLines say.
 */
type Patch = [path: string, value: unknown, push?: true];
const campaignDir = new URL('./campaign/', import.meta.url);
const recorded = (JSON.parse(readFileSync(new URL('states.json', campaignDir), 'utf8')) as { states: { name: string; campaign: CampaignState }[] }).states;
const sample = (JSON.parse(readFileSync(new URL('muestra.json', campaignDir), 'utf8')) as { campaign: CampaignState }).campaign;
const baseOf = (base: string) => JSON.parse(JSON.stringify(base === 'muestra' ? sample : recorded.find((s) => s.name === base)!.campaign)) as CampaignState;
const patched = (base: string, patch: Patch[]) => {
  const c = baseOf(base) as unknown as Record<string, unknown>;
  for (const [path, value, push] of patch) {
    const keys = path.split('.');
    let at: Record<string, unknown> = c;
    for (const k of keys.slice(0, -1)) at = at[k] as Record<string, unknown>;
    const last = keys[keys.length - 1]!;
    if (push) (at[last] as unknown[]).push(value);
    else at[last] = value;
  }
  return c as unknown as CampaignState;
};
// Scenes C# can't tell yet, left out of what TypeScript says here (none since Campaign.TreatCost came, 24/10).
const WAITING = new Set<string>([]);
const cases: { name: string; base: string; patch: Patch[] }[] = [
  ...recorded.map((s) => ({ name: s.name, base: s.name, patch: [] as Patch[] })),
  { name: 'muestra', base: 'muestra', patch: [] },
  { name: 'day 32', base: 'duo-new', patch: [['day', 32]] },
  { name: 'chapter 1 at level 2', base: 'duo-new', patch: [['story.done', 1], ['level', 2]] },
  { name: 'chapter 2 offered', base: 'duo-new', patch: [['offers.0.story', 2]] },
  { name: 'chapter 3 offered', base: 'duo-new', patch: [['offers.0.story', 3]] },
  ...[2, 3, 5, 6, 7].map((done) => ({ name: `chapter ${done} won`, base: 'duo-new', patch: [['story.done', done]] as Patch[] })),
  ...[4, 6, 7].map((level) => ({ name: `level ${level}`, base: 'duo-new', patch: [['level', level]] as Patch[] })),
  { name: 'flooded', base: 'duo-new', patch: [['flooded', ['na']]] },
  { name: 'Simão at the door', base: 'duo-new', patch: [['event', 'simon']] },
  { name: 'Simão trusted', base: 'duo-new', patch: [['story.simon', 'trusted'], ['story.done', 5]] },
  { name: 'Simão refused', base: 'duo-new', patch: [['story.simon', 'refused'], ['story.done', 5]] },
  { name: 'echoes', base: 'duo-new', patch: [['bastion.pieces', { id: 'echoes-1', piece: 'echoes', x: 0, y: 0, rot: 0 }, true]] },
  { name: 'bitten', base: 'duo-new', patch: [['soldiers.0.infection', { since: 1 }]] },
  { name: 'bitten, no medicines', base: 'duo-new', patch: [['soldiers.0.infection', { since: 1 }], ['medicines', 0]] },
  { name: 'turned', base: 'duo-new', patch: [['soldiers.0.alive', false], ['soldiers.0.fellCause', 'turned']] },
  { name: 'fallen', base: 'duo-new', patch: [['soldiers.0.alive', false], ['soldiers.0.fellCause', 'killed']] },
  { name: 'hungry', base: 'duo-new', patch: [['hungry', true]] },
  { name: 'a good weapon', base: 'duo-new', patch: [['foundWeapons', [{ id: 'fw1', kind: 'huntingRifle', holder: null }]]] },
  { name: 'a plain weapon', base: 'duo-new', patch: [['foundWeapons', [{ id: 'fw1', kind: 'assaultRifle', holder: null }]]] },
  { name: 'the final mission', base: 'duo-new', patch: [['offers.0.final', true]] },
  { name: 'the finale', base: 'duo-new', patch: [['event', 'finale']] },
  { name: 'ending break, trusted', base: 'duo-new', patch: [['ending', 'break'], ['story.simon', 'trusted'], ['story.done', 7], ['level', 8]] },
  { name: 'ending record', base: 'duo-new', patch: [['ending', 'record'], ['story.done', 7], ['level', 8]] },
  { name: 'kilometre zero', base: 'duo-new', patch: [['outcome', 'defeat'], ['doom', 12]] },
  { name: 'lost, not to the Marea', base: 'duo-new', patch: [['outcome', 'defeat'], ['doom', 5]] },
];
const reached = new Set<string>();
const sceneCases = cases.map((k) => {
  const c = patched(k.base, k.patch);
  const unlocked = T.unlockedScenes(c).map((s) => s.id).filter((id) => !WAITING.has(id));
  for (const id of unlocked) reached.add(id);
  const seens: string[][] = [[], unlocked.slice(0, 1), unlocked];
  const next = seens.flatMap((seen) =>
    [false, true].map((moment) => ({ seen, moment, scene: T.nextScene(c, new Set([...seen, ...WAITING]) as ReadonlySet<T.SceneId>, moment)?.id ?? null })),
  );
  // Only two scenes have a line that depends on the campaign (Simão's, if he was let in).
  const linesOf = Object.fromEntries(
    T.SCENES.filter((s) => s.lines.some((l) => l.if)).map((s) => [s.id, T.sceneLines(s, c).map((l) => [l.who, fixText(l.text)])]),
  );
  // The recap for each seat: lore.ts's lines through the fixes, the last one as C# says it (recapUrgent).
  const recap = Object.fromEntries(
    ([0, 1] as const).map((slot) => {
      const lines = T.recapLines(c, slot).map((l) => [l.who, fixText(l.text)]);
      const first = nextSteps(c, slot)[0];
      if (first) lines[lines.length - 1] = ['morse', recapUrgent(fixText(first.label), c.soldiers.map((s) => s.name))];
      return [String(slot), lines];
    }),
  );
  return { name: k.name, base: k.base, patch: k.patch, unlocked, next, lines: linesOf, recap };
});
const unreached = T.SCENES.map((s) => s.id).filter((id) => !reached.has(id) && !WAITING.has(id));
if (unreached.length) throw new Error(`scenes no case reaches: ${unreached.join(', ')}`);
mkdirSync(new URL('./lore/', import.meta.url), { recursive: true });
writeFileSync(new URL('./lore/scenes.json', import.meta.url), JSON.stringify({ cases: sceneCases }, null, 1) + '\n');
console.log(`${sceneCases.length} campaigns for the scenes`);

const out = `// Written by engine-cs/golden/make-lore-tests.ts from packages/engine/src/lore.ts: do not edit by hand.
using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    public class LoreGoldenTests
    {
        [Fact]
        public void EveryTextAsLoreTsSaysIt()
        {${lines.join('\n')}
        }
    }
}
`;
writeFileSync(new URL('../tests/LoreGoldenTests.cs', import.meta.url), out);
console.log(`${lines.filter((l) => l.includes('Assert')).length} asserts`);
