// Fase 10, the hex board (docs/FASE10.md §6, Arturo 16/10): 169 hexagonal
// cells, the core in the middle and 168 skills around it in seven rings, six
// sectors of 28, one for each experience line. The core starts unlocked; a
// skill can be bought, with a point, only beside one already unlocked
// (skills.ts unlockBlocker).
import type { AbilityId, ChargeId, PerkId, UnitMods } from './types';
import type { SkillId } from './skills';

/** A cell's id: 'core' or its skill's. */
export type CellId = string;
export const CORE: CellId = 'core';
export const BOARD_RINGS = 7;

/** The lines in the order of their sectors round the core (skills.ts SKILL_IDS). */
export const SECTOR_LINES: SkillId[] = ['shooting', 'melee', 'throwing', 'medicine', 'searching', 'fitness'];

export type CellKind = 'core' | 'stat' | 'passive' | 'active';
export const CELL_KINDS: Record<CellKind, string> = { core: 'Núcleo', stat: 'Mejora', passive: 'Pasiva', active: 'Activa' };

export interface BoardCell {
  id: CellId;
  /** Axial hex coordinates (q, r); the core is (0, 0). */
  q: number;
  r: number;
  /** 0 for the core, 1 to 7 outwards. */
  ring: number;
  /** 0 to 5, SECTOR_LINES' index; -1 for the core. */
  sector: number;
  line: SkillId | null;
  kind: CellKind;
  name: string;
  /** What it does, in the game's words. */
  effect: string;
  mods?: Partial<UnitMods>;
  /** Health and armour more. */
  hp?: number;
  armor?: number;
  /** A behaviour the rules know by its perk (and the ability it gives, if any). */
  perk?: PerkId;
  ability?: AbilityId;
  /** Uses per mission. */
  charges?: Partial<Record<ChargeId, number>>;
  /** One more use of what it already carries (nothing if it carries none). */
  spare?: Partial<Record<ChargeId, number>>;
}

type Skill = Omit<BoardCell, 'q' | 'r' | 'ring' | 'sector' | 'line'>;

const stat = (id: string, name: string, effect: string, extra: Partial<Skill> = {}): Skill => ({ id, kind: 'stat', name, effect, ...extra });
const passive = (id: string, name: string, effect: string, extra: Partial<Skill> = {}): Skill => ({ id, kind: 'passive', name, effect, ...extra });
const active = (id: string, name: string, effect: string, perk: PerkId, ability: AbilityId, extra: Partial<Skill> = {}): Skill => ({ id, kind: 'active', name, effect, perk, ability, ...extra });

/**
 * Each sector's 28 skills, ring by ring outwards (1 in the first ring, 2 in
 * the second… 7 in the seventh); in each ring the first lies on the sector's
 * spoke and the rest run towards the next sector. Names and texts, Lore's to
 * polish; the numbers, to be tuned playing.
 */
const SECTORS: Record<SkillId, Skill[]> = {
  shooting: [
    stat('steadyHand', 'Pulso firme', '+3 de puntería con armas de fuego.', { mods: { gunAim: 3 } }),
    stat('lightTrigger', 'Dedo ligero', '+5 de crítico con armas de fuego.', { mods: { gunCrit: 5 } }),
    stat('sidearm', 'Pistolero', '+5 de puntería con la pistola.', { mods: { pistolAim: 5 } }),
    active('aimedShot', 'A la cabeza', 'Un disparo con −25 de puntería que hace la mitad más de daño. Enfriamiento 3.', 'aimedShot', 'aimedShot'),
    stat('closeIn', 'A quemarropa', '+10 de puntería a 4 casillas o menos.', { mods: { closeRangeAim: 10 } }),
    stat('fineTrigger', 'Gatillo fino', '+10 de crítico con la pistola.', { mods: { pistolCrit: 10 } }),
    active('rapidFire', 'Ráfaga', 'Dos disparos seguidos, cada uno con −15 de puntería. Gasta dos balas. Enfriamiento 3.', 'rapidFire', 'rapidFire'),
    stat('flanker', 'Por el costado', '+1 de daño a los enemigos flanqueados.', { mods: { flankDamage: 1 } }),
    stat('rooftop', 'Desde el tejado', '+10 de puntería disparando desde más alto.', { mods: { heightAim: 10 } }),
    stat('coldBlood', 'Sangre fría', 'Los disparos de reacción pierden la tercera parte de puntería que antes.', { mods: { reactionKeep: 20 } }),
    active('chainShot', 'Disparo en cadena', 'Un disparo con −15 de puntería; si da, otro. Enfriamiento 3.', 'chainShot', 'chainShot'),
    stat('hollowPoint', 'Punta hueca', '+1 de daño en los críticos.', { mods: { critDamage: 1 } }),
    stat('bigBore', 'Calibre gordo', '+1 de daño con la pistola.', { mods: { pistolDamage: 1 } }),
    passive('sentryCrit', 'Vigía certero', 'Los disparos de reacción también pueden ser críticos.', { perk: 'guardCrit' }),
    passive('deathFromAbove', 'Sin bajar', 'Abatir con un arma de fuego desde más alto devuelve una acción, una vez por turno.', { perk: 'deathFromAbove' }),
    active('lightningHands', 'Manos rápidas', 'Sin gastar acción, un disparo de pistola. Enfriamiento 4.', 'lightningHands', 'lightningHands'),
    stat('apRounds', 'Al hueco', 'Los disparos ignoran 1 de blindaje.', { mods: { pierce: 1 } }),
    stat('shredder', 'Desgarro', 'Cada impacto quita 1 de blindaje más.', { mods: { shred: 1 } }),
    passive('serenity', 'Sin roces', 'Con armas de fuego, un impacto nunca se queda en roce.', { perk: 'serenity' }),
    passive('coveringFire', 'Fuego de cobertura', 'En vigilancia, dispara también a quien ataque a la vista.', { perk: 'coveringFire' }),
    stat('noEscape', 'Sin escapatoria', '+10 de crítico a 4 casillas o menos.', { mods: { closeRangeCrit: 10 } }),
    active('killZone', 'Nadie pasa', 'Vigilancia que dispara a cada enemigo que se mueva a la vista, una vez a cada uno. Enfriamiento 4.', 'killZone', 'killZone'),
    active('faceoff', 'Cara a cara', 'Un disparo de pistola a cada enemigo a la vista. Enfriamiento 4.', 'faceoff', 'faceoff'),
    active('rupture', 'Herida abierta', 'Un disparo con +3 de daño; el objetivo recibe 3 más de cada ataque. Enfriamiento 4.', 'rupture', 'rupture'),
    active('suppression', 'Supresión', 'Inmoviliza al objetivo: −50 de puntería hasta tu turno, y si se mueve, le disparas. Gasta dos balas.', 'suppression', 'suppression'),
    passive('marker', 'Marcar', 'Lo que alcanza con el arma principal queda marcado: +15 de puntería para todos contra él.', { perk: 'holoTargeting' }),
    stat('heavyHand', 'Mano dura', '+1 de daño con el arma principal.', { mods: { damage: 1 } }),
    stat('eagleEye', 'Ojo de águila', '+8 de puntería y +5 de crítico con armas de fuego.', { mods: { gunAim: 8, gunCrit: 5 } }),
  ],
  melee: [
    stat('grip', 'Buen agarre', '+3 de puntería cuerpo a cuerpo.', { mods: { meleeAim: 3 } }),
    stat('edge', 'Filo', '+5 de crítico cuerpo a cuerpo.', { mods: { meleeCrit: 5 } }),
    stat('sidestep', 'Paso al lado', '+5 de esquiva.', { mods: { dodge: 5 } }),
    active('elbowRoom', 'Hacerse sitio', 'Un Tajo a cada enemigo que tenga al lado. Enfriamiento 3.', 'elbowRoom', 'elbowRoom'),
    stat('bladeCrit', 'Golpe certero', '+10 de crítico cuerpo a cuerpo.', { mods: { meleeCrit: 10 } }),
    stat('closeRanks', 'Cerrar filas', '+5 de defensa.', { mods: { defense: 5 } }),
    passive('implacable', 'Uno tras otro', 'Abatir con el Tajo devuelve una acción, una vez por turno.', { perk: 'implacable' }),
    stat('heavyBlow', 'Golpe pesado', '+1 de daño cuerpo a cuerpo.', { mods: { meleeDamage: 1 } }),
    stat('parry', 'Ir agachado', 'Los disparos de reacción contra él, −10 de puntería.', { mods: { reactionDefense: 10 } }),
    stat('grit', 'Aguante', '+10 de voluntad.', { mods: { will: 10 } }),
    passive('bladeMaster', 'Mano de machete', 'Con el machete, el Tajo hace +2 de daño y gana +15 de puntería.', { perk: 'bladeMaster' }),
    passive('earBlow', 'Golpe al oído', 'Si el Tajo da, desorienta.', { mods: { slashDisorient: 1 } }),
    stat('butcher', 'Matarife', '+8 de puntería cuerpo a cuerpo.', { mods: { meleeAim: 8 } }),
    passive('bladeWall', 'Nadie se acerca', 'La vigilancia cuerpo a cuerpo golpea a cada enemigo que se le ponga al lado, una vez a cada uno.', { perk: 'bladeWall' }),
    stat('backstab', 'Por la espalda', '+15 de crítico cuerpo a cuerpo.', { mods: { meleeCrit: 15 } }),
    passive('steelNerves', 'Nervios de acero', 'El grito no le hace entrar en pánico.', { mods: { psiGuard: 1 } }),
    stat('cleanCut', 'Tajo limpio', '+1 de daño en los críticos.', { mods: { critDamage: 1 } }),
    stat('waist', 'Cintura', '+10 de esquiva.', { mods: { dodge: 10 } }),
    stat('onGuard', 'En guardia', '+10 de defensa en vigilancia.', { mods: { watchDefense: 10 } }),
    stat('hardHead', 'Cabeza dura', '+1 de salud.', { hp: 1 }),
    stat('sweep', 'Medir la distancia', '+10 de puntería cuerpo a cuerpo.', { mods: { meleeAim: 10 } }),
    passive('secondWind', 'Segundo aliento', 'Abatir cuerpo a cuerpo cura 1 de salud.', { perk: 'secondWind' }),
    stat('cleaver', 'Hachazo', '+2 de daño cuerpo a cuerpo.', { mods: { meleeDamage: 2 } }),
    stat('slippery', 'Escurridizo', '+15 de esquiva.', { mods: { dodge: 15 } }),
    stat('duelist', 'Uno contra uno', '+10 de crítico y +5 de puntería cuerpo a cuerpo.', { mods: { meleeCrit: 10, meleeAim: 5 } }),
    stat('rampart', 'Muralla', '+1 de blindaje.', { armor: 1 }),
    stat('bladeDance', 'Guardia alta', '+10 de defensa.', { mods: { defense: 10 } }),
    stat('killerInstinct', 'Sin dudar', '+15 de puntería cuerpo a cuerpo.', { mods: { meleeAim: 15 } }),
  ],
  throwing: [
    stat('looseArm', 'Brazo suelto', '+1 casilla de alcance al lanzar.', { mods: { grenadeRange: 1 } }),
    stat('sharpStone', 'Canto afilado', 'La piedra hace +1 de daño.', { mods: { stoneDamage: 1 } }),
    stat('curtain', 'Cortina', 'Un humo más, si lleva humo.', { spare: { smoke: 1 } }),
    passive('salvo', 'Sin pausa', 'Lanzar una granada no acaba el turno.', { perk: 'salvo' }),
    stat('bigCharge', 'Carga gorda', 'Los explosivos hacen +1 de daño.', { mods: { grenadeDamage: 1 } }),
    stat('decoy', 'Señuelo', 'La piedra se oye 4 casillas más lejos.', { mods: { stoneNoise: 4 } }),
    passive('goodArm', 'Buen brazo', '+3 casillas de alcance al lanzar, y una granada más en cada accesorio de granadas.', { perk: 'goodArm', mods: { grenadeRange: 3 } }),
    stat('shrapnel', 'Metralla', 'Los explosivos quitan 1 de blindaje más.', { mods: { grenadeShred: 1 } }),
    stat('dazzle', 'Deslumbrar', 'Una aturdidora más, si lleva aturdidoras.', { spare: { flashbang: 1 } }),
    stat('overhand', 'Por encima', '+2 casillas de alcance al lanzar.', { mods: { grenadeRange: 2 } }),
    passive('safeBlast', 'Con cuidado', 'Sus granadas no hacen daño a los compañeros.', { perk: 'safeBlast' }),
    stat('blastWave', 'Esquirlas', 'El radio de los explosivos crece una casilla.', { mods: { grenadeRadius: 1 } }),
    stat('bandolier', 'Bandolera', 'Una granada más, si lleva granadas.', { spare: { grenade: 1 } }),
    stat('boulder', 'Pedrusco', 'La piedra hace +1 de daño.', { mods: { stoneDamage: 1 } }),
    stat('demolition', 'Demolición', 'Los explosivos hacen +1 de daño y su radio crece una casilla.', { mods: { grenadeDamage: 1, grenadeRadius: 1 } }),
    stat('thrower', 'Brazo largo', '+3 casillas de alcance al lanzar.', { mods: { grenadeRange: 3 } }),
    passive('smokeEyes', 'Ojos en el humo', 'El humo no protege a los enemigos de sus disparos.', { mods: { smokeSight: 1 } }),
    stat('doubleCharge', 'Carga doble', 'Los explosivos hacen +1 de daño.', { mods: { grenadeDamage: 1 } }),
    stat('crater', 'Cráter', 'Los explosivos quitan 1 de blindaje más.', { mods: { grenadeShred: 1 } }),
    stat('fog', 'Niebla', 'Un humo más, si lleva humo.', { spare: { smoke: 1 } }),
    stat('stoning', 'Pedrada', '+2 casillas de alcance al lanzar, y la piedra hace +1 de daño.', { mods: { grenadeRange: 2, stoneDamage: 1 } }),
    stat('blaster', 'Artificiero', 'Los explosivos hacen +2 de daño.', { mods: { grenadeDamage: 2 } }),
    stat('shockwave', 'Onda expansiva', 'El radio de los explosivos crece una casilla.', { mods: { grenadeRadius: 1 } }),
    stat('cartridge', 'Cartuchera', 'Una granada más, si lleva granadas.', { spare: { grenade: 1 } }),
    stat('flares', 'Destellos', 'Una aturdidora más, si lleva aturdidoras.', { spare: { flashbang: 1 } }),
    stat('lure', 'Reclamo', 'La piedra se oye 4 casillas más lejos.', { mods: { stoneNoise: 4 } }),
    stat('ironArm', 'Brazo de hierro', '+3 casillas de alcance al lanzar.', { mods: { grenadeRange: 3 } }),
    stat('sling', 'Honda', 'La piedra hace +2 de daño.', { mods: { stoneDamage: 2 } }),
  ],
  medicine: [
    stat('bandage', 'Vendaje', 'El botiquín y las medicinas curan +1.', { mods: { heal: 1 } }),
    passive('calmVoice', 'Voz tranquila', 'Los compañeros a 4 casillas o menos tienen +10 de voluntad.', { mods: { calmAura: 1 } }),
    active('revival', 'Espabilar', 'Quita a un compañero de al lado el aturdimiento, la desorientación o el pánico. Sin botiquín.', 'revivalProtocol', 'revival'),
    passive('bridge', 'Entre los dos', 'El botiquín cura 2 más a los soldados del otro jugador.', { perk: 'bridge' }),
    stat('bigKit', 'Botiquín grande', 'Un uso más del botiquín, si lo lleva.', { spare: { firstAid: 1 } }),
    stat('firstAid', 'Arrastrar a cubierto', 'Quien recibe su botiquín tiene +10 de defensa hasta su próximo turno.', { mods: { aid: 10 } }),
    passive('lightHands', 'Manos de enfermero', 'Las medicinas también paran el contagio de un mordido.', { perk: 'lightHands' }),
    stat('suture', 'Sutura', 'El botiquín y las medicinas curan +1.', { mods: { heal: 1 } }),
    stat('bedside', 'A la cabecera', '+10 de voluntad.', { mods: { will: 10 } }),
    stat('toughSkin', 'Piel curtida', '+1 de salud.', { hp: 1 }),
    active('restoration', 'Pasar visita', 'Cura 2 a cada compañero a 4 casillas o menos y les quita los estados alterados. Una vez por misión.', 'restoration', 'restoration', { charges: { restoration: 1 } }),
    stat('surgeon', 'Cirujano', 'El botiquín y las medicinas curan +1.', { mods: { heal: 1 } }),
    passive('lullaby', 'Palabras justas', 'Los compañeros a 4 casillas o menos tienen 10 más de voluntad.', { mods: { calmAura: 1 } }),
    stat('spares', 'Repuestos', 'Un uso más del botiquín, si lo lleva.', { spare: { firstAid: 1 } }),
    stat('triage', 'Cubrir al herido', 'Quien recibe su botiquín tiene 10 más de defensa hasta su próximo turno.', { mods: { aid: 10 } }),
    stat('rounds', 'Ronda', 'Una vez más por misión, Pasar visita.', { charges: { restoration: 1 } }),
    stat('fieldMedic', 'Médico de campaña', 'El botiquín y las medicinas curan +2.', { mods: { heal: 2 } }),
    stat('ironBlood', 'Hierro en la sangre', '+1 de salud.', { hp: 1 }),
    passive('cheer', 'Ánimo', 'Los compañeros a 4 casillas o menos tienen 10 más de voluntad.', { mods: { calmAura: 1 } }),
    stat('courage', 'Valor', '+15 de voluntad.', { mods: { will: 15 } }),
    stat('kitBag', 'Maletín', 'Un uso más del botiquín, si lo lleva.', { spare: { firstAid: 1 } }),
    stat('miracles', 'Manos expertas', 'El botiquín y las medicinas curan +2.', { mods: { heal: 2 } }),
    passive('presence', 'Presencia', 'Los compañeros a 4 casillas o menos tienen 10 más de voluntad.', { mods: { calmAura: 1 } }),
    stat('vigor', 'Vigor', '+2 de salud.', { hp: 2 }),
    passive('serenityMind', 'Serenidad', 'Nada le entra en la cabeza: el grito no le hace nada.', { mods: { psiGuard: 2 } }),
    stat('warKit', 'Botiquín de guerra', 'Un uso más del botiquín, si lo lleva.', { spare: { firstAid: 1 } }),
    stat('fieldHospital', 'Hospital de campaña', 'El botiquín y las medicinas curan +1, y quien recibe su botiquín tiene 10 más de defensa hasta su próximo turno.', { mods: { heal: 1, aid: 10 } }),
    stat('goodHand', 'Buena mano', 'El botiquín y las medicinas curan +1, y +10 de voluntad.', { mods: { heal: 1, will: 10 } }),
  ],
  searching: [
    stat('tinker', 'Manitas', '+5 al arreglar.', { mods: { hack: 5 } }),
    stat('lookout', 'Buena vista', '+1 casilla de visión.', { mods: { sight: 1 } }),
    passive('quiet', 'Sin hacer ruido', 'Registrar, abrir cajas y forzar no hacen ruido.', { mods: { quiet: 1 } }),
    active('patchUp', 'Apaños', 'Arregla 40 de estado del arma propia o de un compañero de al lado. Una vez por misión.', 'patchUp', 'patchUp', { charges: { patchUp: 1 } }),
    stat('deft', 'Dedos hábiles', '+10 al arreglar.', { mods: { hack: 10 } }),
    stat('crossfire', 'Fuego cruzado', '+10 de puntería contra los enemigos a 4 casillas o menos de un soldado del otro jugador.', { mods: { spotterAim: 10 } }),
    passive('care', 'Mantenimiento', 'Sus armas se gastan la mitad.', { mods: { care: 1 } }),
    stat('sideBySide', 'Codo con codo', '+10 de puntería con un soldado del otro jugador a 4 casillas o menos.', { mods: { pairAim: 10 } }),
    stat('parapet', 'Parapeto', '+10 de defensa con un soldado del otro jugador a 4 casillas o menos.', { mods: { pairDefense: 10 } }),
    stat('picklock', 'Ganzúa', '+10 al arreglar.', { mods: { hack: 10 } }),
    stat('morePatch', 'Más apaños', 'Una vez más por misión, Apaños.', { charges: { patchUp: 1 } }),
    stat('binoculars', 'Prismáticos', '+1 casilla de visión.', { mods: { sight: 1 } }),
    stat('coolHead', 'Cabeza fría', '+10 de voluntad.', { mods: { will: 10 } }),
    stat('shoulder', 'Hombro con hombro', '+5 de puntería y +5 de defensa con un soldado del otro jugador a 4 casillas o menos.', { mods: { pairAim: 5, pairDefense: 5 } }),
    stat('pointOut', 'Señalar', '+10 de puntería contra los enemigos a 4 casillas o menos de un soldado del otro jugador.', { mods: { spotterAim: 10 } }),
    stat('engineer', 'Ingeniero', '+15 al arreglar.', { mods: { hack: 15 } }),
    stat('magnifier', 'Lupa', '+5 de crítico.', { mods: { crit: 5 } }),
    stat('weakSpot', 'Punto débil', '+1 de daño en los críticos.', { mods: { critDamage: 1 } }),
    passive('temperance', 'Templanza', 'El grito no le hace entrar en pánico.', { mods: { psiGuard: 1 } }),
    stat('lynx', 'Vista de lince', '+1 casilla de visión.', { mods: { sight: 1 } }),
    stat('bodge', 'Chapuzas', 'Una vez más por misión, Apaños.', { charges: { patchUp: 1 } }),
    stat('mastermind', 'Mecánico', '+20 al arreglar.', { mods: { hack: 20 } }),
    stat('inseparable', 'Uña y carne', '+10 de puntería y +10 de defensa con un soldado del otro jugador a 4 casillas o menos.', { mods: { pairAim: 10, pairDefense: 10 } }),
    stat('watchman', 'Te cubro', '+15 de puntería contra los enemigos a 4 casillas o menos de un soldado del otro jugador.', { mods: { spotterAim: 15 } }),
    stat('anatomy', 'Anatomía', '+10 de crítico.', { mods: { crit: 10 } }),
    stat('watchtower', 'Mirador', '+2 casillas de visión.', { mods: { sight: 2 } }),
    stat('shadow', 'Sombra', '+5 de defensa y +5 de esquiva.', { mods: { defense: 5, dodge: 5 } }),
    stat('ingenuity', 'Inventiva', 'Dos veces más por misión, Apaños.', { charges: { patchUp: 2 } }),
  ],
  fitness: [
    stat('stamina', 'Fondo', '+1 de salud.', { hp: 1 }),
    stat('crouch', 'Agacharse', '+5 de defensa.', { mods: { defense: 5 } }),
    stat('quickReflex', 'Reflejos', '+5 de esquiva.', { mods: { dodge: 5 } }),
    stat('sprinter', 'Velocista', '+1 casilla de movimiento por acción.', { mods: { mobility: 1 } }),
    stat('lungs', 'Pulmones', '+1 de salud.', { hp: 1 }),
    stat('dodgeFire', 'Esquinazo', 'Los disparos de reacción contra él, −10 de puntería.', { mods: { reactionDefense: 10 } }),
    active('runAndGun', 'Correr y disparar', 'Sin gastar acción: una acción más este turno. Enfriamiento 4.', 'runAndGun', 'runAndGun'),
    stat('sturdy', 'Robusto', '+1 de salud.', { hp: 1 }),
    stat('temper', 'Temple', '+10 de voluntad.', { mods: { will: 10 } }),
    stat('fixedPost', 'Puesto fijo', '+10 de defensa en vigilancia.', { mods: { watchDefense: 10 } }),
    passive('shadowstep', 'Sin rastro', 'Nadie le dispara en vigilancia mientras se mueve.', { perk: 'shadowstep' }),
    stat('leathery', 'Correoso', '+1 de salud.', { hp: 1 }),
    stat('eel', 'Anguila', '+10 de esquiva.', { mods: { dodge: 10 } }),
    stat('vest', 'Duro de pelar', '+1 de blindaje.', { armor: 1 }),
    stat('dugIn', 'Atrincherado', '+10 de defensa.', { mods: { defense: 10 } }),
    stat('marathon', 'Maratón', '+1 casilla de movimiento por acción.', { mods: { mobility: 1 } }),
    stat('ironBody', 'De hierro', '+2 de salud.', { hp: 2 }),
    passive('poise', 'Aplomo', 'El grito no le hace entrar en pánico.', { mods: { psiGuard: 1 } }),
    stat('feline', 'Felino', 'Los disparos de reacción contra él, −15 de puntería.', { mods: { reactionDefense: 15 } }),
    stat('ironWill', 'Voluntad de hierro', '+15 de voluntad.', { mods: { will: 15 } }),
    stat('agile', 'Ágil', '+10 de esquiva.', { mods: { dodge: 10 } }),
    stat('titan', 'Corpulento', '+3 de salud.', { hp: 3 }),
    stat('longRunner', 'Fondista', '+1 de salud y +5 de esquiva.', { hp: 1, mods: { dodge: 5 } }),
    stat('rock', 'Roca', '+1 de blindaje.', { armor: 1 }),
    stat('untouchable', 'Esquivo', '+15 de esquiva y +5 de defensa.', { mods: { dodge: 15, defense: 5 } }),
    stat('bulwark', 'Baluarte', '+15 de defensa.', { mods: { defense: 15 } }),
    stat('lastStand', 'Hasta el final', '+20 de voluntad y +1 de salud.', { hp: 1, mods: { will: 20 } }),
    stat('tireless', 'Incansable', '+1 casilla de movimiento por acción.', { mods: { mobility: 1 } }),
  ],
};

// ---------------------------------------------------------------- geometry

/** The six axial directions, in the order a ring is walked. */
const DIRS: [number, number][] = [
  [1, 0],
  [1, -1],
  [0, -1],
  [-1, 0],
  [-1, 1],
  [0, 1],
];

/**
 * The 169 cells: the core, then ring by ring. Ring `k` is walked from
 * (−k, k), `k` steps along each direction; the cells of side `i` are sector
 * `i`'s.
 */
function build(): BoardCell[] {
  const cells: BoardCell[] = [{ id: CORE, q: 0, r: 0, ring: 0, sector: -1, line: null, kind: 'core', name: 'Instinto', effect: 'El punto de partida: todo lo demás se abre desde aquí.' }];
  for (let k = 1; k <= BOARD_RINGS; k++) {
    let q = -k;
    let r = k;
    for (let side = 0; side < 6; side++) {
      const line = SECTOR_LINES[side]!;
      for (let j = 0; j < k; j++) {
        const skill = SECTORS[line][(k * (k - 1)) / 2 + j]!;
        cells.push({ ...skill, q, r, ring: k, sector: side, line });
        q += DIRS[side]![0];
        r += DIRS[side]![1];
      }
    }
  }
  return cells;
}

export const BOARD: BoardCell[] = build();
export const BOARD_CELLS: Record<CellId, BoardCell> = Object.fromEntries(BOARD.map((c) => [c.id, c]));
const AT = new Map(BOARD.map((c) => [`${c.q},${c.r}`, c.id]));

/** The cells beside `id` on the board. */
export function boardNeighbors(id: CellId): CellId[] {
  const c = BOARD_CELLS[id];
  if (!c) return [];
  return DIRS.map(([dq, dr]) => AT.get(`${c.q + dq},${c.r + dr}`)).filter((n): n is CellId => !!n);
}

/** Whether `id` lies beside the core or a cell of `board` (the skills unlocked). */
export function withinReach(board: readonly CellId[], id: CellId): boolean {
  return boardNeighbors(id).some((n) => n === CORE || board.includes(n));
}

/** The ability each perk of the board gives (abilities.ts abilitiesOf). */
export const PERK_ABILITY: Partial<Record<PerkId, AbilityId>> = Object.fromEntries(BOARD.filter((c) => c.perk && c.ability).map((c) => [c.perk!, c.ability!]));
