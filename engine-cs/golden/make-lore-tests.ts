/**
 * Writes engine-cs/tests/LoreGoldenTests.cs: every text of lore.ts that the
 * C# Lore has, and every function of it called with a few cases, with what
 * lore.ts says in TypeScript as the expected value (docs/UNITY.md §3).
 *
 *   npx tsx engine-cs/golden/make-lore-tests.ts
 *
 * Each case is the C# expression and the same call in TypeScript; tables are
 * walked whole, so a text changed on one side and not the other fails.
 */
import { writeFileSync } from 'fs';
import * as L from '../../packages/engine/src/lore';
import { FOUND_WEAPONS } from '../../packages/engine/src/content';
import { SKILL_IDS } from '../../packages/engine/src/skills';

const lit = (v: unknown): string => (v === null || v === undefined ? 'null' : typeof v === 'string' ? JSON.stringify(v) : String(v));
const lines: string[] = [];
const eq = (expected: unknown, cs: string) =>
  lines.push(
    typeof expected === 'boolean'
      ? `            Assert.${expected ? 'True' : 'False'}(${cs});`
      : expected === null || expected === undefined
        ? `            Assert.Null(${cs});`
        : `            Assert.Equal(${lit(expected)}, ${cs});`,
  );
const cap = (k: string) => `${k[0]!.toUpperCase()}${k.slice(1)}`;
const list = (xs: readonly string[]) => `new string[] { ${xs.map(lit).join(', ')} }`;
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
  lines.push(`            Assert.Equal(${list(xs)}, Lore.${name});`);
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
lines.push(`            Assert.Equal(${list(L.SIDES)}, Lore.Sides);`);
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
  lines.push(t.levels ? `            Assert.Equal(${list(t.levels)}, ${at}.Levels);` : `            Assert.Null(${at}.Levels);`);
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
  lines.push(`            Assert.Equal(${list(p.paragraphs)}, Lore.Expediente[${i}].Paragraphs);`);
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
lines.push(`            Assert.Equal(${list(L.SCAVENGE_BRIEFINGS)}, Lore.ScavengeBriefings.All);`);
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
lines.push(`            Assert.Equal(${list(L.NIGHT_TEXT.rules)}, Lore.NightText.Rules);`);
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
lines.push(`            Assert.Equal(${list(L.PREMISE.lines)}, Lore.Premise.Lines);`);
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
