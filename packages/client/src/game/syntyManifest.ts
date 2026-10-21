import type { BuildId, HeadId, TemplateId } from '@bastion/engine';

// The Synty POLYGON assets the game uses, and for what: the single list.
// tools/synty/convert.ts reads it to convert exactly these FBX files to GLB
// (public/models/synty/<pack>/<name>.glb) and these atlases to WebP; the game
// reads it to know which model shows each unit, weapon, hat and prop. Add an
// asset here, run the converter, and the game can use it (README in
// tools/synty). The licence lets only converted models that the game uses
// into the repository, so keep this list to what is used.
//
// No three.js here: the converter runs in Node.

/** Metres to game units: a tile is 1.5 m, so a 1.8 m survivor stands 1.2 tall. */
export const SYNTY_SCALE = 1 / 1.5;

export type PackId = 'apocalypse' | 'cityZombies' | 'bossZombies' | 'town' | 'farm' | 'nature' | 'city' | 'baseLocomotion' | 'alpine' | 'snow' | 'idles' | 'office' | 'particleFx' | 'construction' | 'military' | 'icons' | 'bowCombat' | 'swordCombat' | 'goblinLocomotion' | 'woodland' | 'industrial' | 'plaza' | 'sidekickSurvivors' | 'sidekickZombies' | 'sidekickOutlaws' | 'sidekickCivilians' | 'sidekickPolice' | 'sidekickStarter';

/**
 * Where each pack lives under assets-src/synty/. A pack that only comes as a
 * Unity package is extracted next to it first (`extracted`): its paths are
 * then the package's own (Assets/Synty/…). `atlas`: a map pack painted from
 * another pack's atlas (its models share that one, nothing more to load).
 */
export const PACKS: Record<PackId, { dir: string; materialList?: string; unitypackage?: string; atlas?: AtlasId }> = {
  apocalypse: { dir: 'POLYGON_Apocalypse_SourceFiles_v4', materialList: 'MaterialList_PolygonApocalypse.txt' },
  cityZombies: { dir: 'POLYGON_City_Zombies_Extracted', unitypackage: 'POLYGON_City_Zombies_Unity_2022_3_v1_7_2.unitypackage' },
  bossZombies: { dir: 'POLYGON_BossZombies_SourceFiles_v4', materialList: 'MaterialList_PolygonBossZombies.txt' },
  town: { dir: 'POLYGON_Town_SourceFiles_v7', materialList: 'MaterialList_PolygonTown.txt' },
  farm: { dir: 'POLYGON_Farm_SourceFiles_v6', materialList: 'MaterialList_PolygonFarm.txt' },
  nature: { dir: 'POLYGON_Nature_SourceFiles_v3', materialList: 'MaterialList_PolygonNature.txt' },
  city: { dir: 'POLYGON_City_SourceFiles_v6', materialList: 'MaterialList_PolygonCity.txt' },
  // «ANIMATION – Base Locomotion» and «ANIMATION – Idles»: clips only, no atlas.
  baseLocomotion: { dir: 'ANIMATION_BaseLocomotion_SourceFiles' },
  idles: { dir: 'ANIMATION_Idles_SourceFiles' },
  // «ANIMATION – Bow Combat» and «ANIMATION – Sword Combat»: their deaths and hits (DEATHS), clips only.
  bowCombat: { dir: 'ANIMATION_BowCombat_SourceFiles' },
  swordCombat: { dir: 'ANIMATION_SwordCombat_SourceFiles' },
  // «ANIMATION – Goblin Locomotion»: how the dead walk and run (DEAD_MOTIONS).
  goblinLocomotion: { dir: 'ANIMATION_GoblinLocomotion_SourceFiles' },
  // The base (BASE_MODELS): «Nature Biomes – Alpine Mountain» and the snow kit.
  alpine: { dir: 'POLYGON_NatureBiomes_AlpineMountain_SourceFiles_v3', materialList: 'MaterialList_PNB_Alpine_Mountain.txt' },
  snow: { dir: 'POLYGON_Snow_SourceFiles', materialList: 'MaterialList_PolygonSnow.txt' },
  // «POLYGON – Office»: the radio's voices (strategy/portraits.ts).
  office: { dir: 'POLYGON_Office_SourceFiles' },
  // «POLYGON – Particle FX»: shapes and sprites for the effects (fx.ts).
  particleFx: { dir: 'POLYGON_ParticleFX_SourceFiles' },
  // «POLYGON – Icons»: a few of its pieces for the interface's renders (lab/renders).
  icons: { dir: 'POLYGON_Icons_SourceFiles_v4', materialList: 'MaterialList_PolygonIcons.txt' },
  // «POLYGON – Construction» and «POLYGON – Military»: building sites and checkpoints on the map (Arte-Mapas).
  construction: { dir: 'POLYGON_Construction_SourceFiles_v5', materialList: 'MaterialList_PolygonConstruction.txt' },
  military: { dir: 'POLYGON_Military_SourceFiles' },
  // «POLYGON Maps – Woodland Apocalypse»: the forest stop's own pieces, painted from Apocalypse's atlas (scenarios, #25).
  woodland: { dir: 'POLYGON_WoodlandApocalypseMap_SourceFiles', atlas: 'apocalypse' },
  // The unidentified sheds-and-military pack: skylights and the like, painted from Military's atlas (#25).
  industrial: { dir: 'POLYGON_SinIdentificar_NaveIndustrialYMilitar_SourceFiles', atlas: 'military' },
  // «POLYGON Maps – Shopping Plaza»: for now only its Generic atlas (a roller shutter of the sheds pack is painted from it).
  plaza: { dir: 'POLYGON_ShoppingPlazaMap_SourceFiles', atlas: 'generic' },
  // «SIDEKICK – Apocalypse Survivors» and «– Apocalypse Zombies» (the prototype, Arturo 21/10: SIDEKICK below): characters
  // of parts on a skeleton of their own, each with its presets (.sk) and the palette it is painted from.
  sidekickSurvivors: { dir: 'SIDEKICK_ApocalypseSurvivors_Extracted', unitypackage: 'SIDEKICK_Apocalypse_Survivors_Unity_2021_3_v1_0_4.unitypackage' },
  sidekickZombies: { dir: 'SIDEKICK_ApocalypseZombies_Extracted', unitypackage: 'SIDEKICK_Apocalypse_Zombies_Unity_2021_3_v1_0_3.unitypackage' },
  sidekickOutlaws: { dir: 'SIDEKICK_ApocalypseOutlaws_Extracted', unitypackage: 'SIDEKICK_Apocalypse_Outlaws_Unity_2021_3_v1_0_6.unitypackage' },
  sidekickCivilians: { dir: 'SIDEKICK_ModernCivilians_Extracted', unitypackage: 'SIDEKICK_Modern_Civilians_Unity_2021_3_v1_0_2.unitypackage' },
  sidekickPolice: { dir: 'SIDEKICK_ModernPolice_Extracted', unitypackage: 'SIDEKICK_Modern_Police_Unity_2021_3_v1_0_0.unitypackage' },
  sidekickStarter: { dir: 'SIDEKICK_Starter_Extracted', unitypackage: 'SIDEKICK_Starter_Unity_2021_3_v1_0_4.unitypackage' },
};

/**
 * Texture atlases, one per pack (Synty paints a whole pack from one image).
 * The game loads each once and every model of the pack shares it.
 * `albedo` is how the pack's MaterialList calls it.
 */
/**
 * A whole Sidekick character's palette (16 × 16 colours, grown to 256 without blending them: `palette`), made with it
 * of its parts' (`composed`: no source of its own, convert.ts).
 */
function sidekickAtlas(pack: PackId) {
  return { pack, src: '', albedo: '', size: 256, palette: true, composed: true };
}

export const ATLASES = {
  apocalypse: { pack: 'apocalypse', src: 'Textures/PolygonApocalypse_Texture_01_A.png', albedo: 'PolygonApocalypse_01_A.png', size: 2048 },
  // Apocalypse's vehicles are painted from a texture of their own: the muddy one, for La Lata (playtest 18/10, #45).
  apocalypseVehicle: { pack: 'apocalypse', src: 'Textures/Veh/PolygonApocalypse_Veh_Texture_01_Mud.png', albedo: 'Vehicle_01_Mud.png', size: 1024 },
  cityZombies: { pack: 'cityZombies', src: 'Assets/Synty/PolygonCityZombies/Textures/Alts/PolygonZombie_01_A.png', albedo: 'PolygonZombie_01_A.png', size: 1024 },
  bossZombies: { pack: 'bossZombies', src: 'Textures/PolygonZombieBoss_Texture_01_A.png', albedo: 'PolygonZombieBoss_01_A.png', size: 1024 },
  town: { pack: 'town', src: 'Textures/PolygonTown_01_A.png', albedo: 'PolygonTown_01_A.png', size: 2048 },
  farm: { pack: 'farm', src: 'Textures/PolygonFarm_Texture_01_A.png', albedo: 'PolygonFarm_01_A.png', size: 1024 },
  nature: { pack: 'nature', src: 'Textures/PolygonNature_01.png', albedo: 'PolygonNature_01.png', size: 1024 },
  city: { pack: 'city', src: 'Textures/PolygonCity_Texture_01_A.png', albedo: 'PolygonCity_01_A.png', size: 2048 },
  alpine: { pack: 'alpine', src: 'Textures/PolygonNatureBiomesS2_Alpine_Texture_01.png', albedo: 'PolygonNatureBiomesS2_Alpine_Texture_01.png', size: 512 },
  snow: { pack: 'snow', src: 'Textures/PolygonSnow_Texture_01_A.png', albedo: 'PolygonSnow_Texture_01_A.png', size: 512 },
  office: { pack: 'office', src: 'Textures/PolygonOffice_Texture_01_A.png', albedo: 'PolygonOffice_Texture_01_A', size: 1024 },
  construction: { pack: 'construction', src: 'Textures/PolygonConstruction_Texture_01_A.png', albedo: 'PolygonConstruction_Texture_01_A.png', size: 1024 },
  military: { pack: 'military', src: 'Textures/PolygonMilitary_Texture_01_A.png', albedo: 'PolygonMilitary_Texture_01_A.png', size: 1024 },
  particleFx: { pack: 'particleFx', src: 'Textures/PolygonParticles_Texture_01_A.png', albedo: 'PolygonParticles_Texture_01_A', size: 256 },
  icons: { pack: 'icons', src: 'Textures/PolygonIcons_Texture_01_A.png', albedo: 'PolygonIcons_01_A.png', size: 512 },
  // Woodland's shipping containers are painted from textures of their own, one per colour: three of the five.
  containerRed: { pack: 'woodland', src: 'Textures/Containers_01.png', albedo: 'Containers_01.png', size: 512 },
  containerBlue: { pack: 'woodland', src: 'Textures/Containers_03.png', albedo: 'Containers_03.png', size: 512 },
  containerOrange: { pack: 'woodland', src: 'Textures/Containers_05.png', albedo: 'Containers_05.png', size: 512 },
  // The light grey one (galvanised), for the sheds' sheet metal (PIECES sheetGrey).
  containerGrey: { pack: 'woodland', src: 'Textures/Containers_04.png', albedo: 'Containers_04.png', size: 512 },
  generic: { pack: 'plaza', src: 'Generic/Textures/Generic_01_A.png', albedo: 'Generic_01_A.png', size: 1024 },
  // Each whole Sidekick character's palette (CHARACTERS with `sidekick`), made with it of its parts' (convert.ts).
  skZombie1: sidekickAtlas('sidekickZombies'),
  skZombie2: sidekickAtlas('sidekickZombies'),
  skZombie3: sidekickAtlas('sidekickZombies'),
  skZombie4: sidekickAtlas('sidekickZombies'),
  skZombie5: sidekickAtlas('sidekickZombies'),
  skZombieCivil1: sidekickAtlas('sidekickCivilians'),
  skZombieCivil2: sidekickAtlas('sidekickCivilians'),
  skZombieCivil3: sidekickAtlas('sidekickCivilians'),
  skZombieCivil4: sidekickAtlas('sidekickCivilians'),
  skZombieCivil5: sidekickAtlas('sidekickCivilians'),
  skZombieCivil6: sidekickAtlas('sidekickCivilians'),
  skZombieCop1: sidekickAtlas('sidekickPolice'),
  skZombieCop2: sidekickAtlas('sidekickPolice'),
  skZombieCop3: sidekickAtlas('sidekickPolice'),
  skZombieCop4: sidekickAtlas('sidekickPolice'),
  skZombieSurv1: sidekickAtlas('sidekickSurvivors'),
  skZombieSurv2: sidekickAtlas('sidekickSurvivors'),
  skRaider1: sidekickAtlas('sidekickOutlaws'),
  skRaider2: sidekickAtlas('sidekickOutlaws'),
  skBoss: sidekickAtlas('sidekickOutlaws'),
  skHunter1: sidekickAtlas('sidekickOutlaws'),
  skHunter2: sidekickAtlas('sidekickOutlaws'),
} satisfies Record<string, { pack: PackId; src: string; albedo: string; size: number; palette?: boolean; composed?: boolean }>;

export type AtlasId = keyof typeof ATLASES;

/** Kinds of converted model; a model's file is `<kind>-<id>.glb` in its pack's folder. */
export type ModelKind = 'chr' | 'hat' | 'wep' | 'prop' | 'anim' | 'piece' | 'base' | 'fx' | 'ui' | 'ward';

/** Path of a converted file under public/ (the game prefixes Vite's base URL). */
export const syntyFile = (pack: PackId, file: string) => `models/synty/${pack}/${file}`;
export const modelFile = (pack: PackId, kind: ModelKind, id: string) => syntyFile(pack, `${kind}-${id}.glb`);
export const atlasFile = (atlas: AtlasId) => syntyFile(ATLASES[atlas].pack, `${atlas}.webp`);

// ------------------------------------------------------------- characters

/**
 * Bones the converter keeps (Synty's animation-pack names); the rest fold
 * into these. Their order is free: the game finds them by name.
 */
export const SYNTY_BONES = [
  'Hips', 'Spine_01', 'Spine_02', 'Spine_03', 'Neck', 'Head',
  'Clavicle_L', 'Shoulder_L', 'Elbow_L', 'Hand_L', 'Clavicle_R', 'Shoulder_R', 'Elbow_R', 'Hand_R',
  'UpperLeg_L', 'LowerLeg_L', 'Ankle_L', 'Ball_L', 'UpperLeg_R', 'LowerLeg_R', 'Ankle_R', 'Ball_R',
  // The fingers (Synty's hands: the thumb, the index and the other three as one), posed by the hand they belong to.
  'Thumb_01_L', 'Thumb_02_L', 'Thumb_03_L', 'Index_01_L', 'Index_02_L', 'Index_03_L', 'Index_04_L', 'Finger_01_L', 'Finger_02_L', 'Finger_03_L', 'Finger_04_L',
  'Thumb_01_R', 'Thumb_02_R', 'Thumb_03_R', 'Index_01_R', 'Index_02_R', 'Index_03_R', 'Index_04_R', 'Finger_01_R', 'Finger_02_R', 'Finger_03_R', 'Finger_04_R',
] as const;

export type SyntyBone = (typeof SYNTY_BONES)[number];

/** A rigid piece hung from a bone: hair, beard, a hat. `dye`: it takes the helmet colour. */
export interface Attachment {
  fbx: string;
  bone: SyntyBone;
  name: string;
  dye?: boolean;
  /**
   * Its turn about the vertical, in degrees, as the converter hangs it: −90
   * (the default) for packs that draw attachments forward along +X
   * (Apocalypse, the bosses); 0 for those drawn facing as the body does (Office).
   */
  turn?: number;
}

/**
 * A skinned character. `mesh` picks one body out of an FBX that holds many
 * (City Zombies). `hair` names the attachments a hat hides. `female` bodies
 * have smaller heads (hats shrink a little on them) and move with the
 * feminine animations. `scale` makes one bigger than the rest (brutes).
 */
export interface CharacterAsset {
  pack: PackId;
  fbx: string;
  mesh?: string;
  attach?: Attachment[];
  hair?: string[];
  female?: boolean;
  scale?: number;
  /**
   * A Sidekick character made whole (convert.ts): this preset's parts, not `fbx`'s; `drop`, its parts of these types
   * left off; `swap`, others in for some types (any pack's); `undead`, a living preset made one of the dead (the
   * zombies' head, face and bare body). Its palette is its atlas, the same id (sidekickAtlas).
   */
  sidekick?: string;
  drop?: string[];
  swap?: Record<string, string>;
  undead?: boolean;
  /** Cells of its palette painted another colour (`x,y` of its 16 × 16: a hex colour). */
  paint?: Record<string, string>;
}

/**
 * Characters stand this much taller than true scale (1.8 m would be 1.2
 * tiles): 1.35 tiles, so they read from the tactical camera. Props keep
 * their true size; weapons and hats grow with the hands and heads.
 */
export const CHARACTER_SCALE = 1.35 / 1.2;

const att = (fbx: string, name: string, dye = false, bone: SyntyBone = 'Head'): Attachment => ({ fbx: `SM_Chr_Attach_${fbx}`, bone, name, dye });
const apoc = (fbx: string, attach: Attachment[] = [], female = false): CharacterAsset => ({
  pack: 'apocalypse',
  fbx: `SK_Chr_${fbx}`,
  attach,
  hair: attach.filter((a) => a.name === 'hair').map((a) => a.name),
  female,
});
const zombie = (mesh: string, female = false): CharacterAsset => ({ pack: 'cityZombies', fbx: 'Zombies', mesh, female });
const office = (fbx: string, attach: Attachment[] = [], female = false): CharacterAsset => ({ pack: 'office', fbx: `SK_Chr_${fbx}`, attach, female });
// Headset_01 is a one-ear earpiece whose boom crosses the face at our scale; 02 is a proper radio headset.
// Office draws its attachments facing as the body does: no quarter turn, or the earpieces sit on the face.
const headset: Attachment = { fbx: 'SM_Chr_Attachment_Headset_02', bone: 'Head', name: 'headset', turn: 0 };

/** A whole Sidekick character: its pack, its preset, and how it differs from it. */
const sk = (pack: PackId, preset: string, more: Partial<CharacterAsset> = {}): CharacterAsset => ({ pack, fbx: '', sidekick: preset, ...more });
/** What hangs at a Sidekick character's back and hips' sides: left off who carries weapons there. */
const RIDES = ['AttachmentBack', 'AttachmentHipsLeft', 'AttachmentHipsRight'];

export const CHARACTERS = {
  // The nun: the radio's coro (strategy/portraits.ts).
  nun: apoc('Nun_Female_01', [att('Nun_Female_HipPouch_01', 'pouch', false, 'Hips')], true),
  // Corredores: the recently dead, still quick.
  zJock: zombie('Zombie_Jock_Male_01'),
  zCheerleader: zombie('Zombie_Cheerleader_Female_01', true),
  zHoodie: zombie('Zombie_Hoodie_Male_01'),
  zSchoolGirl: zombie('Zombie_SchoolGirl_Female_01', true),
  // Acorazados: police and soldiers, vest and helmet still on.
  zRiotCop: { ...zombie('Zombie_RiotCop_Male_01'), scale: 1.06 },
  zMilitary: { ...zombie('Zombie_Military_Male_01'), scale: 1.06 },
  zPolice: { ...zombie('Zombie_Police_Male_01'), scale: 1.06 },
  zFirefighter: { ...zombie('Zombie_Firefighter_Male_01'), scale: 1.06 },
  // Chillón: the screamer, a head taller than the rest.
  zWretch: { pack: 'bossZombies', fbx: 'SK_Chr_ZombieBoss_Wretch_01', attach: [{ fbx: 'SM_Chr_Attach_Wretch_Hair_01', bone: 'Head', name: 'hair' }], scale: 1.12 },
  // Hinchado: the bloated one that bursts.
  zBlobber: { pack: 'bossZombies', fbx: 'SK_Chr_ZombieBoss_Blobber_01', attach: [{ fbx: 'SM_Chr_Attach_Blobber_Hair_01', bone: 'Head', name: 'hair' }], scale: 1.08 },
  // The radio's voices, only in their portraits (strategy/portraits.ts says who is who).
  voiceMorse: office('Boss_Female_01', [headset], true),
  voiceTeo: office('Boss_Male_01'),
  voiceNwosu: office('Business_Female_03', [], true),
  voiceVoz: office('Business_Female_04', [headset], true),
  voiceSimon: { pack: 'city', fbx: 'SK_Character_Male_Jacket' },
  voiceHalvorsen: { pack: 'city', fbx: 'SK_Character_BusinessWoman', female: true },
  // Sidekick's (docs/SIDEKICK.md), whole: the dead, as Sidekick made them and the living made dead, in their clothes.
  skZombie1: sk('sidekickZombies', 'ApocalypseZombie_01', { female: true }),
  skZombie2: sk('sidekickZombies', 'ApocalypseZombie_02'),
  // (Without the chainsaw at its belt.)
  skZombie3: sk('sidekickZombies', 'ApocalypseZombie_03', { drop: ['AttachmentHipsFront'] }),
  skZombie4: sk('sidekickZombies', 'ApocalypseZombie_04'),
  skZombie5: sk('sidekickZombies', 'ApocalypseZombie_05'),
  skZombieCivil1: sk('sidekickCivilians', 'ModernCivilian_01', { undead: true, female: true }),
  skZombieCivil2: sk('sidekickCivilians', 'ModernCivilian_02', { undead: true, female: true }),
  skZombieCivil3: sk('sidekickCivilians', 'ModernCivilian_03', { undead: true, female: true }),
  skZombieCivil4: sk('sidekickCivilians', 'ModernCivilian_04', { undead: true }),
  skZombieCivil5: sk('sidekickCivilians', 'ModernCivilian_05', { undead: true, female: true }),
  skZombieCivil6: sk('sidekickCivilians', 'ModernCivilian_06', { undead: true }),
  skZombieCop1: sk('sidekickPolice', 'ModernPolice_01', { undead: true }),
  skZombieCop2: sk('sidekickPolice', 'ModernPolice_05', { undead: true }),
  skZombieCop3: sk('sidekickPolice', 'ModernPolice_07', { undead: true }),
  skZombieCop4: sk('sidekickPolice', 'ModernPolice_04', { undead: true, female: true }),
  skZombieSurv1: sk('sidekickSurvivors', 'ApocalypseSurvivor_04', { undead: true }),
  skZombieSurv2: sk('sidekickSurvivors', 'ApocalypseSurvivor_01', { undead: true, female: true }),
  // The Cuervos, as Sidekick's outlaws: raiders, their leader, hunters. Without what hangs at their back and their
  // hips' sides, where their weapons ride.
  skRaider1: sk('sidekickOutlaws', 'ApocalypseOutlaw_03', { drop: RIDES }),
  skRaider2: sk('sidekickOutlaws', 'ApocalypseOutlaw_05', { drop: RIDES }),
  // (Its left knee pad is a goblin's, from a pack we haven't: the right one's set.)
  skBoss: sk('sidekickOutlaws', 'ApocalypseOutlaw_02', { drop: RIDES, swap: { AttachmentKneeLeft: 'SK_APOC_OUTL_07_33AKNL_HU01' } }),
  // (Its antlered skull's eyes dark, not glowing cyan: Arturo, 21/10, too sci-fi.)
  skHunter1: sk('sidekickOutlaws', 'ApocalypseOutlaw_01', { drop: RIDES, paint: { '8,13': '2a2522' } }),
  skHunter2: sk('sidekickOutlaws', 'ApocalypseOutlaw_04', { drop: RIDES, female: true }),
} satisfies Record<string, CharacterAsset>;

export type CharacterId = keyof typeof CHARACTERS;

// ---------------------------------------------------------------- wardrobe

/**
 * The soldiers' wardrobe (docs/SIDEKICK.md): pieces of Sidekick characters that wardrobe.ts puts a soldier together
 * from at run time, by their look (Appearance): the clothes from the neck down (OUTFITS), a face, the hair, a beard,
 * what goes on the head. Each piece converts to one mesh on our skeleton (convert.py wardrobe) and keeps three shapes
 * (WARDROBE_SHAPES) that the soldier's sex and build set. `preset` is the Sidekick preset whose palette the piece is
 * measured on (what of it is cloth); an outfit's is its colours too, and without `parts` it wears its preset's, less
 * what OUTFIT_LEAVES leaves.
 */
export interface WardrobeAsset {
  pack: PackId;
  preset: string;
  parts?: string[];
}

/** The shapes each piece keeps, as Sidekick's sliders (0 to 1): a woman's body (none: a man's), slim, heavy (none: the middle build). */
export const WARDROBE_SHAPES = {
  feminine: { masculineFeminine: 1 },
  slim: { defaultSkinny: 0.7 },
  heavy: { defaultHeavy: 0.55, defaultBuff: 0.5 },
} satisfies Record<string, Record<string, number>>;

export type WardrobeShape = keyof typeof WARDROBE_SHAPES;

/** What an outfit leaves of its preset: the head and all on it (pieces of their own), the back and the hips' sides (where the weapons ride). */
export const OUTFIT_LEAVES = ['Head', 'Hair', 'EyebrowLeft', 'EyebrowRight', 'EyeLeft', 'EyeRight', 'EarLeft', 'EarRight', 'FacialHair', 'Nose', 'Teeth', 'Tongue', 'AttachmentHead', 'AttachmentFace', 'AttachmentBack', 'AttachmentHipsLeft', 'AttachmentHipsRight'];

/** A Sidekick palette's skin cells (x, y of its 16 × 16, from the top left): face, ears, nose, hands, bare arms and legs. */
export const SKIN_CELLS: readonly (readonly [number, number])[] = [
  [0, 10], [1, 10], [2, 10], [0, 11], [1, 11], [2, 11], [0, 12], [1, 12], [2, 12], [0, 13], [1, 13], [2, 13], [0, 14], [1, 14],
];

/** Its hair's: the eyebrows, the hair and the beard. */
export const HAIR_CELLS: readonly (readonly [number, number])[] = [[6, 10], [7, 10], [6, 11], [7, 11], [6, 12], [7, 12], [6, 14], [7, 14]];

const survivors = (preset: string) => ({ pack: 'sidekickSurvivors' as PackId, preset: `ApocalypseSurvivor_${preset}` });
const outlaws = (preset: string) => ({ pack: 'sidekickOutlaws' as PackId, preset: `ApocalypseOutlaw_${preset}` });
const police = (preset: string) => ({ pack: 'sidekickPolice' as PackId, preset: `ModernPolice_${preset}` });
const civilians = (preset: string) => ({ pack: 'sidekickCivilians' as PackId, preset: `ModernCivilian_${preset}` });

/** A Sidekick part's name: its set (APOC_SURV_04, MDRN_CIVL_03; HUMN_BASE_01 the bare body's) and its code; or the name itself (SK_…). */
const part = (set: string, code: string) => (set.startsWith('SK_') ? set : `SK_${set}_${code}_HU01`);

/** An outfit made of sets' parts: the torso, the upper and lower arms, the hands, the hips, the legs and the feet (both sides of each), and what more. */
const clothes = (c: { torso: string; upper: string; lower: string; hands: string; hips: string; legs: string; feet: string; more?: string[] }) => [
  part(c.torso, '10TORS'),
  part(c.upper, '11AUPL'), part(c.upper, '12AUPR'),
  part(c.lower, '13ALWL'), part(c.lower, '14ALWR'),
  part(c.hands, '15HNDL'), part(c.hands, '16HNDR'),
  part(c.hips, '17HIPS'),
  part(c.legs, '18LEGL'), part(c.legs, '19LEGR'),
  part(c.feet, '20FOTL'), part(c.feet, '21FOTR'),
  ...(c.more ?? []),
];

/**
 * The clothes a soldier can wear (the barracks' «Personaje»: Appearance.character), in the order the barracks shows
 * them, and who wears them: `sexes`, a woman (f), a man (m) or either, the first the one the clothes were made for
 * (Sidekick's parts fit either body). Winter clothes (the campaign is a winter's: playtest 21/10): long sleeves and
 * trousers, boots; survivors first, a few policemen. Sober, The Last of Us more than Borderlands (Arturo, 21/10): no
 * spikes, studs, bracers or colourful patches (the Cuervos' are). A mixed outfit names its parts; each keeps its own
 * colours.
 */
export const OUTFITS = {
  survivor1: { ...survivors('01'), sexes: 'fm', parts: clothes({ torso: 'APOC_SURV_01', upper: 'APOC_SURV_03', lower: 'APOC_SURV_03', hands: 'APOC_SURV_01', hips: 'APOC_SURV_01', legs: 'APOC_SURV_01', feet: 'APOC_SURV_03' }) },
  survivor2: { ...survivors('02'), sexes: 'mf' },
  survivor3: { ...survivors('03'), sexes: 'fm' },
  survivor4: { ...survivors('04'), sexes: 'mf' },
  survivor5: { ...survivors('05'), sexes: 'mf' },
  survivor6: { ...survivors('04'), sexes: 'mf', parts: clothes({ torso: 'APOC_SURV_03', upper: 'APOC_SURV_02', lower: 'APOC_SURV_03', hands: 'APOC_SURV_02', hips: 'APOC_SURV_06', legs: 'APOC_SURV_01', feet: 'APOC_SURV_02' }) },
  survivor7: { ...outlaws('05'), sexes: 'mf', parts: clothes({ torso: 'APOC_OUTL_07', upper: 'APOC_OUTL_07', lower: 'APOC_SURV_03', hands: 'APOC_SURV_01', hips: 'APOC_SURV_04', legs: 'APOC_SURV_01', feet: 'APOC_SURV_01' }) },
  survivor8: { ...survivors('01'), sexes: 'fm', parts: clothes({ torso: 'APOC_SURV_01', upper: 'MDRN_CIVL_09', lower: 'MDRN_CIVL_06', hands: 'APOC_SURV_02', hips: 'APOC_SURV_05', legs: 'APOC_SURV_02', feet: 'APOC_SURV_02' }) },
  civilian1: { ...civilians('01'), sexes: 'fm', parts: clothes({ torso: 'MDRN_CIVL_03', upper: 'MDRN_CIVL_03', lower: 'MDRN_CIVL_03', hands: 'APOC_SURV_01', hips: 'APOC_SURV_04', legs: 'APOC_SURV_01', feet: 'APOC_SURV_01' }) },
  civilian2: { ...civilians('06'), sexes: 'mf' },
  civilian3: { ...civilians('06'), sexes: 'mf', parts: clothes({ torso: 'APOC_SURV_04', upper: 'MDRN_CIVL_09', lower: 'MDRN_CIVL_06', hands: 'MDRN_CIVL_01', hips: 'MDRN_CIVL_04', legs: 'MDRN_CIVL_07', feet: 'MDRN_CIVL_08' }) },
  civilian4: { ...civilians('01'), sexes: 'fm', parts: clothes({ torso: 'APOC_SURV_05', upper: 'MDRN_CIVL_09', lower: 'MDRN_CIVL_02', hands: 'APOC_SURV_01', hips: 'APOC_SURV_04', legs: 'APOC_SURV_01', feet: 'APOC_SURV_01' }) },
  police1: { ...police('03'), sexes: 'mf' },
  police2: { ...police('08'), sexes: 'fm' },
  police3: { ...police('06'), sexes: 'mf' },
} satisfies Record<string, WardrobeAsset & { sexes: 'f' | 'm' | 'fm' | 'mf' }>;

export type OutfitId = keyof typeof OUTFITS;

const base = (n: number, part: string) => `SK_HUMN_BASE_${String(n).padStart(2, '0')}_${part}_HU01`;
const species = { pack: 'sidekickSurvivors' as PackId, preset: 'HumanSpecies_01' };

/** Faces: Sidekick's two heads, with their noses, ears and eyebrows (`n`: those of the parts numbered so). */
const face = (head: number, n: number) => ({ ...species, parts: [base(head, '01HEAD'), base(1, '05EYEL'), base(1, '06EYER'), base(n, '07EARL'), base(n, '08EARR'), base(n, '35NOSE'), base(n, '03EBRL'), base(n, '04EBRR')] });

export const FACES = {
  face1: face(1, 1),
  face2: face(1, 2),
  face3: face(1, 3),
  face4: face(1, 5),
  face5: face(2, 6),
  face6: face(2, 7),
  face7: face(2, 8),
  face8: face(2, 9),
} satisfies Record<string, WardrobeAsset>;

export type FaceId = keyof typeof FACES;

const hair = (pack: PackId, part: string, sexes: 'f' | 'm') => ({ pack, preset: species.preset, parts: [part], sexes });

/** Hair, a woman's or a man's, none punk (no mohawks, no shaved sides: Arturo, 21/10); HAIR_UNDER, what each wears under a helmet, a cap or a beret (none under a hood). */
export const HAIRS = {
  hairBun: hair('sidekickSurvivors', 'SK_APOC_SURV_01_02HAIR_HU01', 'f'),
  hairBangs: hair('sidekickSurvivors', base(1, '02HAIR'), 'f'),
  hairSidePony: hair('sidekickSurvivors', base(4, '02HAIR'), 'f'),
  hairCurls: hair('sidekickSurvivors', base(5, '02HAIR'), 'f'),
  hairPony: hair('sidekickSurvivors', base(7, '02HAIR'), 'f'),
  hairLong: hair('sidekickSurvivors', base(9, '02HAIR'), 'f'),
  hairTopBun: hair('sidekickSurvivors', base(10, '02HAIR'), 'f'),
  hairPonyBangs: hair('sidekickCivilians', 'SK_MDRN_CIVL_02_02HAIR_HU01', 'f'),
  hairStraight: hair('sidekickCivilians', 'SK_MDRN_CIVL_04_02HAIR_HU01', 'f'),
  hairHighBun: hair('sidekickCivilians', 'SK_MDRN_CIVL_05_02HAIR_HU01', 'f'),
  hairSwept: hair('sidekickCivilians', 'SK_MDRN_CIVL_06_02HAIR_HU01', 'f'),
  hairUnderF: hair('sidekickPolice', 'SK_MDRN_POLC_02_02HAIR_HU01', 'f'),
  hairFade: hair('sidekickSurvivors', base(2, '02HAIR'), 'm'),
  hairMessy: hair('sidekickSurvivors', base(3, '02HAIR'), 'm'),
  hairMedium: hair('sidekickSurvivors', base(1, '02HAIR'), 'm'),
  hairBack: hair('sidekickSurvivors', base(8, '02HAIR'), 'm'),
  hairShaggy: hair('sidekickCivilians', 'SK_MDRN_CIVL_03_02HAIR_HU01', 'm'),
  hairLongM: hair('sidekickSurvivors', base(9, '02HAIR'), 'm'),
  hairUnderM: hair('sidekickPolice', 'SK_MDRN_POLC_01_02HAIR_HU01', 'm'),
} satisfies Record<string, WardrobeAsset & { sexes: 'f' | 'm' }>;

export type HairId = keyof typeof HAIRS;

/** The hair under a helmet, a cap or a beret, by sex. */
export const HAIR_UNDER: Record<'f' | 'm', HairId> = { f: 'hairUnderF', m: 'hairUnderM' };

const beard = (pack: PackId, part: string) => ({ pack, preset: species.preset, parts: [part] });

/** Beards (men only, and not all of them). */
export const BEARDS = {
  beard1: beard('sidekickSurvivors', base(1, '09FCHR')),
  beard2: beard('sidekickSurvivors', base(5, '09FCHR')),
  beard3: beard('sidekickSurvivors', base(7, '09FCHR')),
  beard4: beard('sidekickSurvivors', base(8, '09FCHR')),
  beard5: beard('sidekickSurvivors', base(9, '09FCHR')),
  beard6: beard('sidekickSurvivors', base(10, '09FCHR')),
  beard7: beard('sidekickPolice', 'SK_MDRN_POLC_01_09FCHR_HU01'),
  beard8: beard('sidekickPolice', 'SK_MDRN_POLC_02_09FCHR_HU01'),
} satisfies Record<string, WardrobeAsset>;

export type BeardId = keyof typeof BEARDS;

/**
 * What each head of the barracks puts on (Appearance.head; «A cara descubierta», nothing): the helmet's colour dyes
 * it. `hair`: the hair under it (HAIR_UNDER), none (a hood) or the soldier's own (a mask).
 */
export const HEADWEAR = {
  helmet: { ...survivors('02'), parts: ['SK_APOC_SURV_06_22AHED_HU01'], hair: 'under' },
  hood: { ...survivors('03'), parts: ['SK_APOC_SURV_03_22AHED_HU01'], hair: 'none' },
  beret: { ...civilians('06'), parts: ['SK_MDRN_CIVL_14_22AHED_HU01'], hair: 'under' },
  cap: { ...survivors('02'), parts: ['SK_APOC_SURV_02_22AHED_HU01'], hair: 'under' },
  mask: { ...survivors('03'), parts: ['SK_APOC_SURV_01_23AFAC_HU01'], hair: 'own' },
} satisfies Record<Exclude<HeadId, 'bare'>, WardrobeAsset & { hair: 'under' | 'none' | 'own' }>;

export type WardrobeKind = 'outfit' | 'face' | 'hair' | 'beard' | 'headwear';

/** Every piece of the wardrobe by kind, for the converter and the loader (modelFile 'ward', `<kind>-<id>`: ward-outfit-police3.glb in its pack). */
export const WARDROBE: Record<WardrobeKind, Record<string, WardrobeAsset>> = { outfit: OUTFITS, face: FACES, hair: HAIRS, beard: BEARDS, headwear: HEADWEAR };
export const wardrobeFile = (kind: WardrobeKind, id: string) => modelFile(WARDROBE[kind][id]!.pack, 'ward', `${kind}-${id}`);

/** Who each enemy and objective is (for now the alien templates stand in for the dead). */
export const UNIT_BODIES: Partial<Record<TemplateId, CharacterId[]>> = {
  zombie: ['skZombie1', 'skZombie2', 'skZombie3', 'skZombie4', 'skZombie5', 'skZombieCivil1', 'skZombieCivil2', 'skZombieCivil3', 'skZombieCivil4', 'skZombieCivil5', 'skZombieCivil6', 'skZombieCop1', 'skZombieCop2', 'skZombieCop3', 'skZombieCop4', 'skZombieSurv1', 'skZombieSurv2'],
  xenoid: ['zJock', 'zCheerleader', 'zHoodie', 'zSchoolGirl'],
  mec: ['zRiotCop', 'zMilitary', 'zPolice', 'zFirefighter'],
  sectoid: ['zWretch'],
  bloater: ['zBlobber'],
  trooper: ['skRaider1', 'skRaider2'],
  officer: ['skBoss'],
  lancer: ['skHunter1', 'skHunter2'],
};

/** Templates that move like the dead: hunched, arms out, no weapon. */
export const DEAD_TEMPLATES: ReadonlySet<TemplateId> = new Set<TemplateId>(['zombie', 'xenoid', 'mec', 'sectoid', 'bloater']);

// -------------------------------------------------------------- animations

/**
 * Clips from «ANIMATION – Base Locomotion», in place (no root motion). Each
 * converts to a GLB with the pack's skeleton and the clip; the game plays it
 * on a proxy of that skeleton and turns the pose onto each character's own
 * bones (both Synty rigs), measured against the neutral T-pose. `fbx` is
 * the path in the pack: the same names exist for the Sidekick rig.
 */
const loco = (path: string) => ({ pack: 'baseLocomotion' as PackId, fbx: `Animations/Polygon/${path}` });
const idle = (path: string) => ({ pack: 'idles' as PackId, fbx: `Animations/Polygon/${path}` });
const bow = (path: string) => ({ pack: 'bowCombat' as PackId, fbx: `Animations/Polygon/${path}` });
const sword = (path: string) => ({ pack: 'swordCombat' as PackId, fbx: `Animations/Polygon/${path}` });
const goblin = (path: string) => ({ pack: 'goblinLocomotion' as PackId, fbx: `Animations/Polygon/Neutral/Locomotion/${path}` });

export const ANIMATIONS = {
  tpose: loco('Neutral/Additive/TPose/A_TPose_Neut'),
  idleMasc: loco('Masculine/Idle/A_Idle_Standing_Masc'),
  idleFemn: loco('Feminine/Idle/A_Idle_Standing_Femn'),
  crouchIdleMasc: loco('Masculine/Idle/A_Idle_Crouching_Masc'),
  crouchIdleFemn: loco('Feminine/Idle/A_Idle_Crouching_Femn'),
  walkMasc: loco('Masculine/Locomotion/Walk/A_Walk_F_Masc'),
  walkFemn: loco('Feminine/Locomotion/Walk/A_Walk_F_Femn'),
  runMasc: loco('Masculine/Locomotion/Run/A_Run_F_Masc'),
  runFemn: loco('Feminine/Locomotion/Run/A_Run_F_Femn'),
  // The dash (a move of two actions) and the Corredor (docs/ARTE.md, «Velocidades»).
  sprintMasc: loco('Masculine/Locomotion/Sprint/A_Sprint_F_Masc'),
  sprintFemn: loco('Feminine/Locomotion/Sprint/A_Sprint_F_Femn'),
  crouchWalkMasc: loco('Masculine/Locomotion/Crouch/A_Crouch_FwdStrafeF_Masc'),
  crouchWalkFemn: loco('Feminine/Locomotion/Crouch/A_Crouch_FwdStrafeF_Femn'),
  // «ANIMATION – Goblin Locomotion» (one for both builds): the dead's (DEAD_MOTIONS).
  goblinWalk: goblin('Walk/A_POLY_GBL_Walk_F_Neut'),
  goblinRun: goblin('Run/A_POLY_GBL_Run_F_Neut'),
  goblinSprint: goblin('Sprint/A_POLY_GBL_Sprint_F_Neut'),
  // «ANIMATION – Idles»: what a unit waiting does now and then.
  lookLMasc: idle('Masculine/Look/Actions/A_POLY_IDL_Look_L_Masc'),
  lookRMasc: idle('Masculine/Look/Actions/A_POLY_IDL_Look_R_Masc'),
  lookBehindMasc: idle('Masculine/Look/Actions/A_POLY_IDL_Look_L_Behind_Masc'),
  shiftLMasc: idle('Masculine/WeightShift/Actions/A_POLY_IDL_WeightShift_L_Masc'),
  shiftRMasc: idle('Masculine/WeightShift/Actions/A_POLY_IDL_WeightShift_R_Masc'),
  lookLFemn: idle('Feminine/Look/Actions/A_POLY_IDL_Look_L_Femn'),
  lookRFemn: idle('Feminine/Look/Actions/A_POLY_IDL_Look_R_Femn'),
  lookBehindFemn: idle('Feminine/Look/Actions/A_POLY_IDL_Look_R_Behind_Femn'),
  shiftLFemn: idle('Feminine/WeightShift/Actions/A_POLY_IDL_WeightShift_L_Femn'),
  shiftRFemn: idle('Feminine/WeightShift/Actions/A_POLY_IDL_WeightShift_R_Femn'),
  // How some stand waiting (STANCES).
  drunkMasc: idle('Masculine/Sway/Stances/A_POLY_IDL_Sway_Drunk_Loop_Masc'),
  drunkFemn: idle('Feminine/Sway/Stances/A_POLY_IDL_Sway_Drunk_Loop_Femn'),
  aggressiveMasc: idle('Masculine/Posture/Stances/A_POLY_IDL_Posture_Aggressive_Loop_Masc'),
  aggressiveFemn: idle('Feminine/Posture/Stances/A_POLY_IDL_Posture_Aggressive_Loop_Femn'),
  slumpedMasc: idle('Masculine/Posture/Stances/A_POLY_IDL_Posture_Slumped_Loop_Masc'),
  slumpedFemn: idle('Feminine/Posture/Stances/A_POLY_IDL_Posture_Slumped_Loop_Femn'),
  // «ANIMATION – Bow Combat» and «– Sword Combat»: how a unit dies (DEATHS).
  deathHeadFMasc: bow('Masculine/Death/A_POLY_BOW_Death_Head_F_Masc'),
  deathHeadLMasc: bow('Masculine/Death/A_POLY_BOW_Death_Head_L_Masc'),
  deathHeadRMasc: bow('Masculine/Death/A_POLY_BOW_Death_Head_R_Masc'),
  deathSpineMasc: bow('Masculine/Death/A_POLY_BOW_Death_Spine_B_Masc'),
  deathStomachMasc: bow('Masculine/Death/A_POLY_BOW_Death_Stomach_F_Masc'),
  deathHeadFFemn: bow('Feminine/Death/A_POLY_BOW_Death_Head_F_Femn'),
  deathHeadLFemn: bow('Feminine/Death/A_POLY_BOW_Death_Head_L_Femn'),
  deathHeadRFemn: bow('Feminine/Death/A_POLY_BOW_Death_Head_R_Femn'),
  deathSpineFemn: bow('Feminine/Death/A_POLY_BOW_Death_Spine_B_Femn'),
  deathStomachFemn: bow('Feminine/Death/A_POLY_BOW_Death_Stomach_F_Femn'),
  deathForward: sword('Death/A_Death_F_01_Sword'),
  // How a unit takes a hit (HITS): Bow Combat's for a hit, Sword Combat's (one for both builds) for a hard one.
  hitHeadFMasc: bow('Masculine/Hit/A_POLY_BOW_Hit_Head_F_React_Masc'),
  hitHeadLMasc: bow('Masculine/Hit/A_POLY_BOW_Hit_Head_L_React_Masc'),
  hitHeadRMasc: bow('Masculine/Hit/A_POLY_BOW_Hit_Head_R_React_Masc'),
  hitHeadBMasc: bow('Masculine/Hit/A_POLY_BOW_Hit_Head_B_React_Masc'),
  hitBackMasc: bow('Masculine/Hit/A_POLY_BOW_Hit_Back_B_React_Masc'),
  hitShoulderLMasc: bow('Masculine/Hit/A_POLY_BOW_Hit_Shoulder_L_React_Masc'),
  hitShoulderRMasc: bow('Masculine/Hit/A_POLY_BOW_Hit_Shoulder_R_React_Masc'),
  hitHeadFFemn: bow('Feminine/Hit/A_POLY_BOW_Hit_Head_F_React_Femn'),
  hitHeadLFemn: bow('Feminine/Hit/A_POLY_BOW_Hit_Head_L_React_Femn'),
  hitHeadRFemn: bow('Feminine/Hit/A_POLY_BOW_Hit_Head_R_React_Femn'),
  hitHeadBFemn: bow('Feminine/Hit/A_POLY_BOW_Hit_Head_B_React_Femn'),
  hitBackFemn: bow('Feminine/Hit/A_POLY_BOW_Hit_Back_B_React_Femn'),
  hitShoulderLFemn: bow('Feminine/Hit/A_POLY_BOW_Hit_Shoulder_L_React_Femn'),
  hitShoulderRFemn: bow('Feminine/Hit/A_POLY_BOW_Hit_Shoulder_R_React_Femn'),
  hardBack: sword('Hit/HitReact/A_Hit_B_React_Sword'),
  hardDouble: sword('Hit/HitReact/A_Hit_F_React_Sword'),
  hardLeft: sword('Hit/HitReact/A_Hit_L_React_Sword'),
  hardRight: sword('Hit/HitReact/A_Hit_R_React_Sword'),
} satisfies Record<string, { pack: PackId; fbx: string }>;

export type AnimationId = keyof typeof ANIMATIONS;

/** The clips a unit blends between; each character uses its build's set. */
export type Motion = 'idle' | 'crouchIdle' | 'walk' | 'run' | 'crouchWalk' | 'sprint';

export const MOTIONS: Record<'masc' | 'femn', Record<Motion, AnimationId>> = {
  masc: { idle: 'idleMasc', crouchIdle: 'crouchIdleMasc', walk: 'walkMasc', run: 'runMasc', crouchWalk: 'crouchWalkMasc', sprint: 'sprintMasc' },
  femn: { idle: 'idleFemn', crouchIdle: 'crouchIdleFemn', walk: 'walkFemn', run: 'runFemn', crouchWalk: 'crouchWalkFemn', sprint: 'sprintFemn' },
};

/**
 * The dead walk and run hunched, dragging their feet (Goblin Locomotion; Arturo,
 * 2026-10-14: «no se ve bien la animación de andar»); the rest as the living.
 */
export const DEAD_MOTIONS: Partial<Record<Motion, AnimationId>> = { walk: 'goblinWalk', run: 'goblinRun', sprint: 'goblinSprint' };

/** Played once now and then instead of the idle while a unit stands waiting: a look round, a shift of weight. */
export const IDLE_VARIATIONS: Record<'masc' | 'femn', AnimationId[]> = {
  masc: ['lookLMasc', 'lookRMasc', 'lookBehindMasc', 'shiftLMasc', 'shiftRMasc'],
  femn: ['lookLFemn', 'lookRFemn', 'lookBehindFemn', 'shiftLFemn', 'shiftRFemn'],
};

/**
 * How some units stand waiting instead of the plain idle (Arturo, 2026-10-14):
 * the dead sway as if drunk, the living enemies stand squared up for a fight,
 * a badly wounded soldier slumps.
 */
export type Stance = 'drunk' | 'aggressive' | 'slumped';

export const STANCE_ORDER: readonly Stance[] = ['drunk', 'aggressive', 'slumped'];

export const STANCES: Record<'masc' | 'femn', Record<Stance, AnimationId>> = {
  masc: { drunk: 'drunkMasc', aggressive: 'aggressiveMasc', slumped: 'slumpedMasc' },
  femn: { drunk: 'drunkFemn', aggressive: 'aggressiveFemn', slumped: 'slumpedFemn' },
};

/**
 * How a unit dies (Arturo, 2026-10-14: «al morir solo se desploman como si
 * fuese un tronco»): Synty's death clips and which way each body goes down,
 * `falls`, as an angle in the unit's frame (0 ahead, π/2 to its left; where
 * its hips end up, measured in Blender). A body falls away from the blow that
 * kills it (syntyFigures.ts die): shot from ahead it goes over backwards,
 * from behind onto its face, from one side towards the other. `head` is a
 * shot to the head (a critical picks among those); `close` doubles up round a
 * blow at arm's length (a blade, a bite), whichever way it comes.
 */
export interface Death {
  id: AnimationId;
  falls: number;
  /** How far from where it stood its head ends up, in metres: how much room the fall takes. */
  reach: number;
  head?: boolean;
  close?: boolean;
}

const deaths = (b: 'Masc' | 'Femn'): Death[] => [
  // Head_F: thrown back onto its back.
  { id: `deathHeadF${b}`, falls: -2.65, reach: 0.85, head: true },
  // Head_L and Head_R: spun round and down backwards, to the side away from the hit.
  { id: `deathHeadL${b}`, falls: -2.46, reach: 1.25, head: true },
  { id: `deathHeadR${b}`, falls: 2.34, reach: 1.41, head: true },
  // Spine_B: arches, the knees go and it sits back and down, about where it stood.
  { id: `deathSpine${b}`, falls: 3.0, reach: 0.73 },
  // Stomach_F: doubles over and goes down forwards.
  { id: `deathStomach${b}`, falls: 0.21, reach: 0.95, close: true },
  // Sword Combat's Death_F (one for both builds): pitches forward onto its face.
  { id: 'deathForward', falls: -0.14, reach: 1.58 },
];

export const DEATHS: Record<'masc' | 'femn', Death[]> = { masc: deaths('Masc'), femn: deaths('Femn') };

/**
 * How a unit takes a hit (Gameplay, 2026-10-14): Synty's hit reactions, over
 * the upper body only (the legs keep what they do: crouched in cover, it stays
 * crouched). `from` is the way the blow comes from in the unit's frame (0
 * ahead, π/2 from its left), measured in Blender by where the head is thrown;
 * `hard` ones (Sword Combat, a stagger) are for a critical, `head` ones go with
 * one too, `close` doubles up round a blow at arm's length.
 */
export interface Hit {
  id: AnimationId;
  from: number;
  hard?: boolean;
  head?: boolean;
  close?: boolean;
}

const hits = (b: 'Masc' | 'Femn'): Hit[] => [
  { id: `hitHeadF${b}`, from: 0, head: true },
  { id: `hitHeadL${b}`, from: 0.86, head: true },
  { id: `hitHeadR${b}`, from: -1.17, head: true },
  { id: `hitHeadB${b}`, from: Math.PI, head: true },
  { id: `hitBack${b}`, from: Math.PI },
  { id: `hitShoulderL${b}`, from: 1.25 },
  { id: `hitShoulderR${b}`, from: -1.57 },
  // Sword Combat's are named for the way the body goes: B thrown back, F doubled forward, L and R to that side.
  { id: 'hardBack', from: -0.22, hard: true },
  { id: 'hardDouble', from: Math.PI, hard: true, close: true },
  { id: 'hardLeft', from: -1.2, hard: true },
  { id: 'hardRight', from: 0.82, hard: true },
];

export const HITS: Record<'masc' | 'femn', Hit[]> = { masc: hits('Masc'), femn: hits('Femn') };

// ----------------------------------------------------------------- weapons

/** Guns and melee weapons, held in the right hand; the converter turns them barrel +Z, grip at the origin. */
/**
 * `fore`: where the support hand holds a two-handed gun, the middle of its
 * palm as [height, length] in the gun's frame (as converted: grip near the
 * origin, barrel +Z, up +Y; measured under each one's handguard, pump or
 * forend). One-handed and melee weapons have none.
 */
export const WEAPONS = {
  // `sight`: the top of the receiver, where a sight goes (WEAPON_PARTS; a red dot on the `reflex` ones);
  // `muzzle` where the converter's (the far end) is not the barrel's.
  rifle: { pack: 'apocalypse', fbx: 'SM_Wep_AssaultRifle_01', fore: [0.002, 0.2], sight: 0.098 },
  rifle2: { pack: 'apocalypse', fbx: 'SM_Wep_AssaultRifle_02', fore: [0.012, 0.21], sight: 0.096 },
  rifle3: { pack: 'apocalypse', fbx: 'SM_Wep_AssaultRifle_03', fore: [0.03, 0.27], sight: 0.1 },
  shotgun: { pack: 'apocalypse', fbx: 'SM_Wep_Shotgun_01', fore: [-0.014, 0.2], sight: 0.057, reflex: true },
  sniper: { pack: 'apocalypse', fbx: 'SM_Wep_SniperRifle_01', fore: [-0.02, 0.14], sight: 0.053 },
  // (Its bayonet reaches past the barrel.)
  sniper2: { pack: 'apocalypse', fbx: 'SM_Wep_SniperRifle_02', fore: [-0.022, 0.18], sight: 0.04, muzzle: [0, 0.026, 0.49] },
  machineGun: { pack: 'apocalypse', fbx: 'SM_Wep_MachineGun_01', fore: [0.018, 0.3], sight: 0.11, reflex: true },
  huntingRifle: { pack: 'apocalypse', fbx: 'SM_Wep_HuntingRifle_01', fore: [-0.04, 0.22], sight: 0.043 },
  smg: { pack: 'apocalypse', fbx: 'SM_Wep_SubMGun_01', fore: [-0.008, 0.12], sight: 0.071, reflex: true },
  baton: { pack: 'apocalypse', fbx: 'SM_Wep_Baton_01', melee: true },
  // A campaign soldier's own (docs/FASE8.md §A): the pistol, alone in the hand or in its holster;
  // the knife or the machete at the belt, in the hand for the Tajo; and the Armería's good ones.
  pistol: { pack: 'apocalypse', fbx: 'SM_Wep_Pistol_01', pistol: true },
  // (Synty's blades are long beside its guns: `size` brings them down.)
  knife: { pack: 'apocalypse', fbx: 'SM_Wep_Melee_HuntingKnife_01', melee: true, size: 0.8 },
  machete: { pack: 'apocalypse', fbx: 'SM_Wep_Melee_Machete_01', melee: true, size: 0.72 },
  boltRifle: { pack: 'apocalypse', fbx: 'SM_Wep_HuntingRifle_Clean_01', fore: [-0.04, 0.22], sight: 0.043 },
  machineGun2: { pack: 'apocalypse', fbx: 'SM_Wep_MachineGun_02', fore: [0.018, 0.3], sight: 0.122, reflex: true },
} satisfies Record<string, WeaponAsset>;

/** A weapon's model and how it is held and dressed (WEAPONS). */
export interface WeaponAsset {
  pack: PackId;
  fbx: string;
  melee?: boolean;
  pistol?: boolean;
  fore?: [number, number];
  size?: number;
  sight?: number;
  reflex?: boolean;
  muzzle?: [number, number, number];
}

export type WeaponId = keyof typeof WEAPONS;

/**
 * What is mounted on a gun (docs/FASE9.md §5; SyntyFigure puts them on): the
 * modular parts of Apocalypse's guns, converted as weapons are (the barrel
 * along +z), each on its own origin, where it fits on the gun. A red dot, a
 * telescopic sight and the home-made silencer, a tube wrapped in tape.
 */
export const WEAPON_PARTS = {
  scope2: { pack: 'apocalypse', fbx: 'SM_Wep_Mod_Attach_Scope_02' },
  scope7: { pack: 'apocalypse', fbx: 'SM_Wep_Mod_Attach_Scope_07' },
  silencer2: { pack: 'apocalypse', fbx: 'SM_Wep_Mod_Attach_Silencer_02' },
} satisfies Record<string, { pack: PackId; fbx: string }>;

export type WeaponPartId = keyof typeof WEAPON_PARTS;

/** The weapon each template carries, by weapon tier (1–3); none for the unarmed. */
export const UNIT_WEAPONS: Partial<Record<TemplateId, [WeaponId, WeaponId, WeaponId]>> = {
  rookie: ['rifle', 'rifle2', 'rifle3'],
  specialist: ['rifle', 'rifle2', 'rifle3'],
  assault: ['shotgun', 'shotgun', 'shotgun'],
  sharpshooter: ['sniper', 'sniper2', 'sniper2'],
  grenadier: ['machineGun', 'machineGun', 'machineGun'],
  trooper: ['huntingRifle', 'huntingRifle', 'huntingRifle'],
  officer: ['smg', 'smg', 'smg'],
  lancer: ['baton', 'baton', 'baton'],
};

// ----------------------------------------------------------------- effects

/**
 * Particle FX's shapes (fx.ts): Synty builds its effects from
 * faceted puffs, chunks and flat polygon sprites, tinted by the effect, and
 * so do we. The meshes keep only their shape (each effect gives its colour);
 * the gore takes the pack's dark reds from its atlas.
 */
export const FX_MODELS = {
  smokePuff: { pack: 'particleFx', fbx: 'FX_Sphere_Puff_01' },
  goreChunk1: { pack: 'particleFx', fbx: 'SM_GoreChunk_01' },
  goreChunk2: { pack: 'particleFx', fbx: 'SM_GoreChunk_02' },
  goreChunk3: { pack: 'particleFx', fbx: 'SM_GoreChunk_04' },
  flame: { pack: 'particleFx', fbx: 'SM_Flame_FX' },
  medCross: { pack: 'particleFx', fbx: 'FX_Healthcube_01' },
  woodChip: { pack: 'particleFx', fbx: 'FX_WoodChip_01' },
} satisfies Record<string, { pack: PackId; fbx: string }>;

export type FxModelId = keyof typeof FX_MODELS;

/** Particle FX's sprites, white on transparent, tinted by the effect (WebP with alpha; `size` caps the width). */
export const FX_TEXTURES = {
  polyDisc: { src: 'Textures/PolygonParticles_Circle_01.png', size: 128 },
  sparkle: { src: 'Textures/PolygonParticles_Sparkle.png', size: 256 },
  shockRing: { src: 'Textures/PolygonParticles_Ring_02.png', size: 256 },
  groundCrack: { src: 'Textures/PolygonParticles_GroundBreak.png', size: 512 },
  lightning: { src: 'Textures/PolygonParticles_Lightning_02.png', size: 256 },
} satisfies Record<string, { src: string; size: number }>;

export type FxTextureId = keyof typeof FX_TEXTURES;

export const fxTextureFile = (id: FxTextureId) => syntyFile('particleFx', `${id}.webp`);

/**
 * Models only the interface's renders use (lab/renders: items and the like,
 * shot to images under public/ui/synty/renders); the game never loads them.
 */
export const UI_MODELS = {
  // A ration: food tins, a tall one and two flat.
  tinTall: { pack: 'apocalypse', fbx: 'SM_Item_Can_03' },
  tinFlat: { pack: 'apocalypse', fbx: 'SM_Item_Can_09' },
  tinFlat2: { pack: 'apocalypse', fbx: 'SM_Item_Can_07' },
  // «Piezas»: scrap metal, gears, a bolt and a battery (Apocalypse's wire is too thin to read as an icon).
  scrapSheet: { pack: 'apocalypse', fbx: 'SM_Prop_Iron_Sheet_02' },
  battery: { pack: 'apocalypse', fbx: 'SM_Item_Battery_01' },
  gears: { pack: 'icons', fbx: 'SM_Icon_Gears_01' },
  boltIcon: { pack: 'icons', fbx: 'SM_Icon_Crafting_Bolt_01' },
} satisfies Record<string, { pack: PackId; fbx: string }>;

// ------------------------------------------------------------------- props

/**
 * A static prop, normalised to the map's tiles: scaled so its longest side
 * is `fit` tiles (none: true size), only along that side with `stretch` (a
 * counter or a fence keeps its height), its height and depth times `thick`;
 * turned `yaw` quarter turns (counter-clockwise from above) so its front
 * looks north (−Z) like the game's props; then shifted `shift` tiles along
 * its own axes. Town furniture comes facing south (yaw 2, but desks
 * north), Apocalypse furniture and barricades facing east (yaw 1).
 */
export interface PropAsset {
  pack: PackId;
  fbx: string;
  /** Reuses a converted base model as tile furniture, without another GLB. */
  base?: BaseModelId;
  /** Its GLB is made by a tool of its own (`fbx` says where it comes from): the converter leaves it alone. */
  baked?: string;
  fit?: number;
  stretch?: boolean;
  thick?: number;
  yaw?: number;
  shift?: [number, number];
  /** Stands on its own origin, not its middle: a pole with an arm, a power line from its pole. */
  origin?: boolean;
  /** Painted from another atlas than its pack's (a texture of its own). */
  atlas?: AtlasId;
  /** Cut to this box first, in the FBX's metres (a guard tower without its stairs). */
  keep?: [[number, number, number], [number, number, number]];
  /** And these boxes taken out of it (the guard tower's hut off its deck). */
  hole?: [[number, number, number], [number, number, number]][];
  /** A deck one stands on at this height in the FBX (metres): the game sets it one STOREY up, however that stretches it. */
  deck?: number;
}

const prop = (pack: PackId, fbx: string, o: Omit<PropAsset, 'pack' | 'fbx'> = {}): PropAsset => ({ pack, fbx, ...o });

export const PROPS = {
  // Streets.
  carWreck: prop('apocalypse', 'SM_Prop_Car_Wrecked_01', { fit: 1.95, yaw: 1 }),
  carRusted: prop('apocalypse', 'SM_Prop_Car_Wrecked_Rusted_01', { fit: 1.95, yaw: 1 }),
  carSuv: prop('apocalypse', 'SM_Prop_Car_Wrecked_SUV_01', { fit: 1.95, yaw: 1 }),
  carOpen: prop('apocalypse', 'SM_Prop_Car_Wrecked_OpenBoot_01', { fit: 1.95, yaw: 1 }),
  planter: prop('city', 'SM_Prop_Planter_01', { fit: 0.9 }),
  dumpster: prop('apocalypse', 'SM_Prop_Dumpster_01', { fit: 0.95 }),
  rubbishBin: prop('apocalypse', 'SM_Prop_RubbishBin_01'),
  streetPot: prop('apocalypse', 'SM_Prop_Pot_Street_Plant_01', { fit: 0.9 }),
  vending: prop('town', 'SM_Prop_VendingMachine_01', { yaw: 2 }),
  statue: prop('apocalypse', 'SM_Prop_Statue_01', { fit: 0.95 }),
  // The squares (playtest 18/10, #12): the fountain whole over its 3 × 3 (a 2 × 2 one smaller), a park bench along its
  // two tiles, a broken one where half of it is shot away or the town has had the worst of it.
  fountainWhole: prop('apocalypse', 'SM_Prop_Fountain_01', { fit: 3 }),
  parkBench: prop('city', 'SM_Prop_ParkBench_01', { fit: 1.6, yaw: 2 }),
  parkBenchBroken: prop('apocalypse', 'SM_Prop_ParkBench_Broken_01', { fit: 1.6, yaw: 1 }),
  concreteBarrier: prop('apocalypse', 'SM_Prop_Barrier_Concrete_01', { fit: 1 }),
  barricade1: prop('apocalypse', 'SM_Prop_Barricade_01', { fit: 1, stretch: true, yaw: 1 }),
  barricade2: prop('apocalypse', 'SM_Prop_Barricade_03', { fit: 1, stretch: true, yaw: 1 }),
  barricadeTall: prop('apocalypse', 'SM_Prop_Barricade_05', { fit: 1, stretch: true, yaw: 1 }),
  barricadePallet: prop('apocalypse', 'SM_Prop_Barricade_Pallet_01', { fit: 1, stretch: true, yaw: 1 }),
  sandbags: prop('apocalypse', 'SM_Prop_Sandbag_Wall_02', { fit: 1, stretch: true, yaw: 1 }),
  // The siege's base (docs/FASE12.md §10): a loose sandbag of a barricade knocked about, a gate's leaf down, Military's
  // guard tower as a deck on legs with its rail (its hut and its stairs taken off: the map's ladder climbs to it), the
  // Sala de radio's mast and its head.
  sandbag: prop('apocalypse', 'SM_Prop_Sandbag_01', { fit: 0.45 }),
  gateLeafDown: prop('apocalypse', 'SM_Prop_Wall_Quarantine_GateDoor_01', { fit: 1.25 }),
  guardTower: prop('military', 'SM_Bld_GuardTower_02', { fit: 3, deck: 3.58, keep: [[-2.2, -2.2, -0.1], [2.2, 2.06, 4.6]], hole: [[[-1.07, -1.07, 3.62], [1.07, 1.07, 6]]] }),
  radioMast: prop('apocalypse', 'SM_Bld_RadioTower_01', { origin: true }),
  radioMastTop: prop('apocalypse', 'SM_Bld_RadioTower_Top_01', { origin: true }),
  // The new base (docs/FASE12.md §11, basePieces.ts): the caravan, and what is built by hand. A palisade's or a gate's
  // stakes run north–south as modelled; a half one from the post to the north edge, like a fence's.
  caravan: prop('apocalypse', 'SK_Veh_Caravan_01', { fit: 3.9 }),
  // What the caravan grows into with the Bastion's level (playtest 18/10, #40; basePieces.ts drawBastion): a mobile
  // home on its skirt, its porch; Military's plywood barracón on stilts; a house with its porch and chimney. Each on the
  // FBX's own origin (its glass and its skirt are models of their own, made to fit it), at its own size.
  mobileHome: prop('apocalypse', 'SM_Bld_Trailer_01', { origin: true }),
  mobileHomeGlass: prop('apocalypse', 'SM_Bld_Trailer_Glass_01', { origin: true }),
  mobileHomeWreck: prop('apocalypse', 'SM_Bld_Trailer_Damaged_01', { origin: true }),
  mobileHomeWreckGlass: prop('apocalypse', 'SM_Bld_Trailer_Damaged_Glass_01', { origin: true }),
  mobileHomeSkirt: prop('apocalypse', 'SM_Bld_Trailer_Skirt_01', { origin: true }),
  trailerDeck: prop('apocalypse', 'SM_Prop_Trailer_Deck_01', { origin: true }),
  trailerSteps: prop('apocalypse', 'SM_Prop_Trailer_Steps_01', { origin: true }),
  plywoodShed: prop('military', 'SM_Bld_Plywood_Shed_01', { origin: true }),
  plywoodShedRuin: prop('military', 'SM_Bld_Plywood_Shed_Destroyed_01', { origin: true }),
  bastionHouse: prop('apocalypse', 'SM_Bld_House_03', { origin: true }),
  bastionHouseGlass: prop('apocalypse', 'SM_Bld_House_03_Glass_01', { origin: true }),
  // What a stage's second level adds to the Bastion (playtest 20/10 #13): the caravan's awning (a tarp on poles), the
  // barracón's water tower, the house's armour on its windows and its tank traps.
  bAwning: prop('apocalypse', 'SM_Bld_Junk_Shelter_06', { origin: true }),
  bAwningTarp: prop('apocalypse', 'SM_Bld_Junk_Shelter_06_Tarp', { origin: true }),
  bWaterTower: prop('military', 'SM_Bld_WaterTank_01', { origin: true }),
  bIronSheet: prop('apocalypse', 'SM_Prop_Iron_Sheet_01', { origin: true }),
  bPlank: prop('apocalypse', 'SM_Prop_Plank_01', { origin: true }),
  bTankTrap: prop('apocalypse', 'SM_Prop_TankTrap_01', { origin: true }),
  palisade: prop('apocalypse', 'SM_Prop_SpikeWall_01', { fit: 0.5, stretch: true, shift: [0, -0.25] }),
  gateWood: prop('apocalypse', 'SM_Prop_Fence_Wood_Gate_01', { fit: 1, stretch: true }),
  gateMetal: prop('apocalypse', 'SM_Prop_Wall_Quarantine_GateDoor_01', { fit: 1, stretch: true, yaw: 1 }),
  woodStack: prop('farm', 'SM_Prop_Wood_Stack_01', { fit: 0.95, stretch: true, thick: 1.2 }),
  floodlight: prop('construction', 'SM_Prop_Spotlight_01'),
  wireCoil: prop('apocalypse', 'SM_Prop_Wire_03', { fit: 0.95 }),
  towerLadder: prop('apocalypse', 'SM_Prop_Ladder_01'),
  // The base's turret (syntyFigures.ts TurretRig): Apocalypse's AA gun in its parts, each on its own origin (the
  // tripod's post, the swivel on it, the gun on the swivel, its drum on the gun), so the gun turns to aim.
  turretTripod: prop('apocalypse', 'SM_Wep_AAGun_01', { origin: true }),
  turretSwivel: prop('apocalypse', 'SM_Wep_AAGun_Swivel_01', { origin: true }),
  turretGun: prop('apocalypse', 'SM_Wep_AAGun_Body_01', { origin: true }),
  turretDrum: prop('apocalypse', 'SM_Wep_AAGun_Mag_01', { origin: true }),
  // Its traps: a mine half buried, a crater where one went off (and a spike pit's hole), the pit's stakes, a bear trap
  // set and its two jaws, which close on their hinge once it has sprung.
  mine: prop('military', 'SM_Prop_Mine_01'),
  crater: prop('military', 'SM_Env_Crater_01'),
  spikeStake: prop('apocalypse', 'SM_Prop_Spike_01', { origin: true }),
  bearTrap: prop('apocalypse', 'SM_Prop_Bear_Trap_01'),
  bearJaw: prop('apocalypse', 'SM_Prop_Bear_Trap_Jaw_01', { origin: true }),
  bearJaw2: prop('apocalypse', 'SM_Prop_Bear_Trap_Jaw_02', { origin: true }),
  // Its buildings (basePieces.ts BUILDINGS): the base's own models (BASE_MODELS) as tile furniture, no GLB of their own;
  // the quarantine a cage of Military's mesh panels.
  cagePanel: prop('military', 'SM_Bld_Door_Cage_01'),
  bArmyTent: prop('apocalypse', 'SM_Bld_Military_Tent_01', { base: 'armyTent' }),
  bFieldTent: prop('apocalypse', 'SM_Prop_Tent_Long_01', { base: 'fieldTent' }),
  bOpenTent: prop('apocalypse', 'SM_Bld_Military_Tent_02', { base: 'openTent' }),
  bQuarantineTent: prop('apocalypse', 'SM_Bld_Quarantine_Tent_01', { base: 'quarantineTent' }),
  bShipContainer: prop('apocalypse', 'SM_Prop_Shipping_Container_02', { base: 'shipContainer' }),
  bGarageShed: prop('town', 'SM_Bld_House_Preset_Garage_01', { base: 'garageShed' }),
  bGardenShed: prop('town', 'SM_Bld_GardenShed_01', { base: 'gardenShed' }),
  bLeanTo: prop('farm', 'SM_Bld_Shelter_01', { base: 'leanTo' }),
  bShackFrame: prop('apocalypse', 'SM_Bld_Junk_Shelter_01', { base: 'shackFrame' }),
  bShackTarp: prop('apocalypse', 'SM_Bld_Junk_Shelter_01_Tarp', { base: 'shackTarp' }),
  bAerial: prop('apocalypse', 'SM_Prop_Trailer_Aerial_01', { base: 'aerial' }),
  bMast: prop('apocalypse', 'SM_Prop_Trailer_Aerial_03', { base: 'mast' }),
  bDish: prop('apocalypse', 'SM_Prop_Trailer_Dish_01', { base: 'dish' }),
  bSolarPanel: prop('apocalypse', 'SM_Prop_Solar_Panel_01', { base: 'solarPanel' }),
  bPowerGenerator: prop('apocalypse', 'SM_Prop_Generator_01', { base: 'powerGenerator' }),
  bPowerBoxes: prop('apocalypse', 'SM_Prop_PowerBoxes_01', { base: 'powerBoxes' }),
  bFirePit: prop('apocalypse', 'SM_Prop_FirePit_01', { base: 'firePit' }),
  bWashingLine: prop('apocalypse', 'SM_Prop_WashingLine_01', { base: 'washingLine' }),
  bCampChair: prop('apocalypse', 'SM_Prop_Camp_Chair_01', { base: 'campChair' }),
  bLongTable: prop('apocalypse', 'SM_Prop_Table_02', { base: 'longTable' }),
  bBlackboard: prop('apocalypse', 'SM_Prop_Blackboard_01', { base: 'blackboard' }),
  bRadioSet: prop('apocalypse', 'SM_Prop_Radio_01', { base: 'radioSet' }),
  bWorkbench: prop('apocalypse', 'SM_Prop_Workbench_01', { base: 'workbench' }),
  bVice: prop('apocalypse', 'SM_Prop_Vice_01', { base: 'vice' }),
  bToolbox: prop('apocalypse', 'SM_Prop_ToolBox_01', { base: 'toolbox' }),
  bTyres: prop('apocalypse', 'SM_Prop_Tire_Pile_01', { base: 'tyres' }),
  bOilDrum: prop('apocalypse', 'SM_Prop_Barrel_01', { base: 'oilDrum' }),
  bJerrycan: prop('apocalypse', 'SM_Prop_GasCan_01', { base: 'jerrycan' }),
  bBatteryTable: prop('apocalypse', 'SM_Prop_CarBattery_Table_01', { base: 'batteryTable' }),
  bFuelPump: prop('apocalypse', 'SM_Prop_Gaspump_01', { base: 'fuelPump' }),
  bRustyCar: prop('apocalypse', 'SM_Prop_Car_Wrecked_Rusted_01', { base: 'rustyCar' }),
  bDeconShower: prop('apocalypse', 'SM_Prop_Decontamination_Shower_01', { base: 'deconShower' }),
  bMedKit: prop('apocalypse', 'SM_Prop_MedicalBox_02', { base: 'medKit' }),
  bTarget: prop('apocalypse', 'SM_Prop_Target_01', { base: 'target' }),
  bTargetBoard: prop('apocalypse', 'SM_Prop_Target_02', { base: 'targetBoard' }),
  bBottleRange: prop('apocalypse', 'SM_Prop_ShootingRange_01', { base: 'bottleRange' }),
  bTyreWall: prop('apocalypse', 'SM_Prop_TireWall_01', { base: 'tyreWall' }),
  bBalanceBeam: prop('apocalypse', 'SM_Prop_Plank_Bench_01', { base: 'balanceBeam' }),
  bSandbagWall: prop('apocalypse', 'SM_Prop_Sandbag_Wall_02', { base: 'sandbagWall' }),
  bClimbFrame: prop('apocalypse', 'SM_Bld_Junk_Shelter_02', { base: 'climbFrame' }),
  bLookout: prop('alpine', 'Props/SM_Prop_Lookout_01', { base: 'lookout' }),
  bCross: prop('apocalypse', 'SM_Prop_Cross_01', { base: 'cross' }),
  bStoneCross: prop('apocalypse', 'SM_Prop_Cross_02', { base: 'stoneCross' }),
  bGraveStick: prop('apocalypse', 'SM_Prop_GraveStick_01', { base: 'graveStick' }),
  bPicketFence: prop('apocalypse', 'SM_Prop_Fence_Wood_Straight_01', { base: 'picketFence' }),
  bBench: prop('alpine', 'Props/SM_Prop_Bench_01', { base: 'bench' }),
  bLampPost: prop('apocalypse', 'SM_Prop_LightPole_01', { base: 'lampPost' }),
  bGardenBed: prop('farm', 'SM_Prop_PlantBox_Large_01', { base: 'gardenBed' }),
  bGardenBarrel: prop('apocalypse', 'SM_Prop_Barrel_Water_Open_01', { base: 'gardenBarrel' }),
  bGardenPlant: prop('farm', 'SM_Prop_Plant_Ground_01_L', { base: 'gardenPlant' }),
  bGardenPlant2: prop('farm', 'SM_Prop_Plant_Ground_02_L', { base: 'gardenPlant2' }),
  bScarecrow: prop('farm', 'SM_Prop_Scarecrow_01', { base: 'scarecrow' }),
  bJersey: prop('apocalypse', 'SM_Prop_Barrier_Long_01', { base: 'jersey' }),
  bBurntHeap: prop('apocalypse', 'SM_Prop_BurnPile_01', { base: 'burntHeap' }),
  bJunkHeap: prop('apocalypse', 'SM_Prop_TrashPile_01', { base: 'junkHeap' }),
  bRubbleHeap: prop('apocalypse', 'SM_Prop_Rubble_Concrete_01', { base: 'rubbleHeap' }),
  bFloodlights: prop('apocalypse', 'SM_Prop_Floodlights_01', { base: 'floodlights' }),
  bWoodPile: prop('alpine', 'Props/SM_Prop_Wood_Pile_01', { base: 'woodPile' }),
  bAnvil: prop('apocalypse', 'SM_Prop_Anvil_01', { base: 'anvil' }),
  bSawhorse: prop('construction', 'SM_Prop_Saw_Horse_01', { base: 'sawhorse' }),
  bPlankRack: prop('construction', 'SM_Prop_StorageShelf_Plank_01', { base: 'plankRack' }),
  bTrestle: prop('construction', 'SM_Prop_Trestle_Table_01', { base: 'trestle' }),
  bStump: prop('nature', 'SM_Tree_Stump_01', { base: 'stump' }),
  bSupplyCrate: prop('apocalypse', 'SM_Prop_Crate_01', { base: 'supplyCrate' }),
  pineDead: prop('nature', 'SM_Tree_Pine_Dead_01', { base: 'pineDead' }),
  crate: prop('apocalypse', 'SM_Prop_Crate_01'),
  crates: prop('apocalypse', 'SM_Prop_Crate_Large_02'),
  barrels: prop('apocalypse', 'SM_Prop_BarrelStack_02', { fit: 0.95 }),
  generator: prop('apocalypse', 'SM_Prop_Generator_01', { fit: 0.9 }),
  gasTank: prop('farm', 'SM_Prop_Gastank_01', { fit: 0.95 }),
  pipes: prop('apocalypse', 'SM_Prop_Pipe_Large_01', { fit: 1, stretch: true, thick: 0.5 }),
  loudSpeaker: prop('apocalypse', 'SM_Prop_Loud_Speaker_01', { fit: 0.7 }),
  // The factory (playtest 15/10 #17): drums, the hall's machines, silos over a block of 3×3 (mapView scales them to it) (the pallets are `pallet`, stacked).
  drums: prop('apocalypse', 'SM_Prop_BarrelStack_01', { fit: 0.95 }),
  drumsTarp: prop('apocalypse', 'SM_Prop_BarrelStack_01_Tarp', { fit: 0.95 }),
  machine: prop('construction', 'SM_Prop_Generator_Large_01', { fit: 0.95 }),
  silo2: prop('construction', 'SM_Bld_WaterTank_02', { fit: 1.9 }),
  silo3: prop('construction', 'SM_Bld_WaterTank_03', { fit: 1.9 }),
  // The sabotage's alarm (playtest 15/10 #14; syntyFigures.ts SirenRig): a patrol car or an ambulance, two tiles long.
  sirenPolice: prop('city', 'SM_Veh_Car_Police_01', { fit: 1.95 }),
  sirenAmbulance: prop('apocalypse', 'SK_Veh_Ambulance_01', { fit: 1.95 }),
  rubble1: prop('apocalypse', 'SM_Prop_Rubble_Concrete_01', { fit: 0.95 }),
  rubble2: prop('apocalypse', 'SM_Prop_Rubble_Concrete_02', { fit: 0.95 }),
  rubbleWall: prop('apocalypse', 'SM_Prop_Rubble_Concrete_Frame_01', { fit: 0.95 }),
  // Fences and farms. A fence piece runs from the post to the north edge (half a tile).
  fencePost: prop('farm', 'SM_Prop_Fence_Wood_Pole_01'),
  fencePanel: prop('farm', 'SM_Prop_Fence_Wood_01', { fit: 0.5, stretch: true, yaw: 1, shift: [0, -0.25] }),
  fenceWire: prop('farm', 'SM_Prop_Fence_Wire_01', { fit: 0.5, stretch: true, yaw: 1, shift: [0, -0.25] }),
  gatePost: prop('farm', 'SM_Prop_Fence_Fancy_Pole_01'),
  hayBale: prop('farm', 'SM_Prop_Hay_Bale_Square_01', { fit: 0.9 }),
  hayRound: prop('farm', 'SM_Prop_Hay_Bale_Round_01', { fit: 0.95 }),
  // Indoors.
  shelfLow: prop('apocalypse', 'SM_Prop_ShopShelf_Wall_01', { fit: 1, yaw: 1 }),
  // A supermarket's shelving (playtest 20/10 #18): Town's gondola, goods both ways, and its wall shelf; City's low
  // display for the end of a row.
  gondola: prop('town', 'SM_Prop_ShopShelf_02', { fit: 1, yaw: 2 }),
  wallShelf: prop('town', 'SM_Prop_ShopShelf_01', { fit: 1, yaw: 2 }),
  endDisplay: prop('city', 'SM_Prop_ShopInterior_Shelf_04', { fit: 1, yaw: 2 }),
  shelfHigh: prop('apocalypse', 'SM_Prop_Shop_Shelf_01', { fit: 1, stretch: true, yaw: 1 }),
  bookshelf: prop('town', 'SM_Prop_Bookshelf_01', { fit: 1, stretch: true, yaw: 2 }),
  bookshelf2: prop('town', 'SM_Prop_Bookshelf_02', { fit: 1, stretch: true, yaw: 2 }),
  // What a house's rooms have besides their cover (playtest 18/10, #36; homeFurnishing.ts): rugs, chairs at the
  // table, a lamp, plants, the bedside table, a desk chair, the towels; small things on tables and counters.
  // Cosmetic, never cover: one style of each for a house, at their true size (a tile is 1.5 m; `fit` is the longest
  // side across, not the height). Town's furniture faces south as modelled (yaw 2).
  rug: prop('town', 'SM_Prop_Rug_01', { fit: 1.3, yaw: 2 }),
  rug2: prop('town', 'SM_Prop_Rug_02', { fit: 1.4, yaw: 2 }),
  rug3: prop('town', 'SM_Prop_Rug_03', { fit: 1.2, yaw: 2 }),
  bathRug: prop('town', 'SM_Prop_BathroomRug_01', { fit: 0.75, yaw: 2 }),
  coffeeTable: prop('town', 'SM_Prop_CoffeeTable_01', { fit: 0.75, yaw: 2 }),
  armchair: prop('apocalypse', 'SM_Prop_Chair_Soft_01', { fit: 0.68, yaw: 1 }),
  armchairWorn: prop('apocalypse', 'SM_Prop_Chair_Soft_Damaged_01', { fit: 0.68, yaw: 1 }),
  diningChair: prop('town', 'SM_Prop_DiningChair_01', { fit: 0.45, yaw: 2 }),
  diningChair2: prop('town', 'SM_Prop_DiningChair_02', { fit: 0.45, yaw: 2 }),
  floorLamp: prop('town', 'SM_Prop_LampStanding_01', { fit: 0.3, yaw: 2 }),
  floorLamp2: prop('town', 'SM_Prop_LampStanding_02', { fit: 0.36, yaw: 2 }),
  tableLamp: prop('town', 'SM_Prop_Lamp_01', { fit: 0.22, yaw: 2 }),
  tableLamp2: prop('town', 'SM_Prop_Lamp_02', { fit: 0.22, yaw: 2 }),
  pottedPlant: prop('town', 'SM_Prop_PotPlant_01', { fit: 0.32, yaw: 2 }),
  pottedPlant2: prop('town', 'SM_Prop_PotPlant_02', { fit: 0.34, yaw: 2 }),
  pottedPlant3: prop('town', 'SM_Prop_PotPlant_04', { fit: 0.24, yaw: 2 }),
  bedsideTable: prop('town', 'SM_Prop_BedsideTable_01', { fit: 0.3, yaw: 2 }),
  deskChair: prop('town', 'SM_Prop_DeskChair_01', { fit: 0.5, yaw: 2 }),
  towelRack: prop('town', 'SM_Prop_TowelRack_01', { fit: 0.5, yaw: 2 }),
  bookPile: prop('town', 'SM_Prop_Book_Group_01', { fit: 0.25, yaw: 2 }),
  bookPile2: prop('town', 'SM_Prop_Book_Group_04', { fit: 0.2, yaw: 2 }),
  breadBin: prop('town', 'SM_Prop_BreadBin_01', { fit: 0.25, yaw: 2 }),
  coffeeMachine: prop('town', 'SM_Prop_CoffeeMachine_01', { fit: 0.2, yaw: 2 }),
  coatRack: prop('office', 'SM_Prop_CoatRack_01', { fit: 0.45 }),
  // And a shop's, by its trade (homeFurnishing.ts furnishShops): a café's tables and chairs and the stools at its
  // bar; a grocer's drinks fridge and cooler; a bookshop's books on its counter.
  cafeTable: prop('apocalypse', 'SM_Prop_Cafe_Table_01', { fit: 0.75, yaw: 1 }),
  cafeChair: prop('apocalypse', 'SM_Prop_Cafe_Chair_01', { fit: 0.38, yaw: 1 }),
  barStool: prop('town', 'SM_Prop_Stool_01', { fit: 0.27, yaw: 2 }),
  drinksCooler: prop('town', 'SM_Prop_Cooler_01', { fit: 0.4, yaw: 2 }),
  drinksFridge: prop('town', 'SM_Prop_FridgeDrinks_01', { fit: 0.52, yaw: 2 }),
  bookPile3: prop('town', 'SM_Prop_Book_Group_06', { fit: 0.2, yaw: 2 }),
  // A barn's (playtest 18/10, #53): its old tractor over the 2×4 block the generator leaves for it, the tools against
  // its walls, a wheelbarrow, buckets.
  tractor: prop('farm', 'SM_Veh_TractorOld_01', { fit: 2.6, yaw: 2 }),
  pitchfork: prop('farm', 'SM_Prop_Tool_Pitchfork_01', { fit: 0.2 }),
  rake: prop('farm', 'SM_Prop_Tool_Rake_01', { fit: 0.35 }),
  spade: prop('farm', 'SM_Prop_Tool_Spade_01', { fit: 0.17 }),
  wheelbarrow: prop('farm', 'SM_Prop_Wheelbarrow_01', { fit: 1 }),
  bucket: prop('farm', 'SM_Prop_Tool_Bucket_01', { fit: 0.24 }),
  counter: prop('town', 'SM_Prop_Kitchen_Counter_01', { fit: 1, stretch: true, yaw: 2 }),
  shopCounter: prop('apocalypse', 'SM_Prop_ShopCounter_01', { fit: 1, stretch: true, yaw: 1 }),
  table: prop('town', 'SM_Prop_DiningTable_01', { fit: 1, stretch: true }),
  sofa: prop('town', 'SM_Prop_Couch_01', { fit: 1, stretch: true, yaw: 2 }),
  sofaDirty: prop('apocalypse', 'SM_Prop_Couch_01', { fit: 1, stretch: true, yaw: 1 }),
  bed: prop('town', 'SM_Prop_Bed_Single_01', { fit: 1, yaw: 2 }),
  // A double bed over its 2 × 2 (docs/MAPAS.md, phase 1), never on a tile of its own: 2.61 m long as modelled, × 1.125.
  bedDouble: prop('town', 'SM_Prop_Bed_Double_01', { fit: 1.95, yaw: 2 }),
  // A house's rooms (playtest 18/10, #15): the kitchen's fridge and cooker, the bathroom's bath, washbasin and toilet,
  // the living room's television on its stand.
  fridge: prop('town', 'SM_Prop_Kitchen_Fridge_01', { fit: 0.65, yaw: 2 }),
  stove: prop('town', 'SM_Prop_Oven_01', { fit: 0.75, yaw: 2 }),
  bathtub: prop('apocalypse', 'SM_Prop_Bathtub_01', { fit: 1, stretch: true }),
  washbasin: prop('town', 'SM_Prop_BathroomSink_02', { fit: 0.6, yaw: 2 }),
  wc: prop('town', 'SM_Prop_Toilet_01', { fit: 0.55, yaw: 2 }),
  tvStand: prop('town', 'SM_Prop_TVCabinet_01', { fit: 0.95, stretch: true, yaw: 2 }),
  television: prop('town', 'SM_Prop_Television_Flat_01', { fit: 0.75, yaw: 2 }),
  // The Armería by the caravan (clearingArt.ts): Military's gun rack, its rifles the soldiers' own.
  gunRack: prop('military', 'SM_Prop_Gunrack_01'),
  // Static poses baked from existing licensed characters; see tools/synty/bake-medical-corpses.mjs.
  corpseCivilian: prop('apocalypse', 'SK_Chr_Business_Male_01', { fit: 0.92, baked: 'tools/synty/bake-medical-corpses.mjs' }),
  corpseStaff: prop('apocalypse', 'SK_Chr_Waitress_Female_01', { fit: 0.92, baked: 'tools/synty/bake-medical-corpses.mjs' }),
  hospitalBed: prop('apocalypse', 'Rigged/SK_Prop_Bed_Hospital_01', { base: 'hospitalBed', fit: 1.15, yaw: 1 }),
  medicalShelf: prop('apocalypse', 'SM_Prop_Medical_Shelf_01', { base: 'medShelf', fit: 0.9, yaw: 1 }),
  gurney: prop('apocalypse', 'Rigged/SK_Prop_Bed_Gurney_01', { base: 'gurney', fit: 0.95, yaw: 1 }),
  ivStand: prop('apocalypse', 'SM_Prop_Medical_IVStand_01', { base: 'ivStand' }),
  clinicalKit: prop('apocalypse', 'SM_Prop_MedicalBox_02', { base: 'medKit', fit: 0.22 }),
  wardrobe: prop('town', 'SM_Prop_Wardrobe_01_Combined', { fit: 0.95, yaw: 2 }),
  desk: prop('town', 'SM_Prop_Desk_01', { fit: 1, stretch: true }),
  cabinet: prop('town', 'SM_Prop_Cabinet_01', { yaw: 2 }),
  workShelf: prop('apocalypse', 'SM_Prop_WorkShelf_01', { fit: 1, stretch: true, yaw: 1 }),
  // The wilds. Trees at their size (19/10, MAPAS.md §4.7: a crown 4–6 m across and 6–9 m high, seen; they were a tile
  // and a half across, a street's tree 3 m high): their trunk on its tile, their crown over the ones round it, fading
  // away over what the player looks at (treeFade.ts).
  tree: prop('nature', 'SM_Tree_01', { fit: 2.73 }),
  // (Nature's needle pines take their leaves from a texture of their own, with holes: the solid ones instead.)
  treePine: prop('nature', 'SM_Tree_PolyPine_01', { fit: 2.13 }),
  treeDead: prop('apocalypse', 'SM_Env_Tree_Dead_01', { fit: 3.17 }),
  streetTree: prop('apocalypse', 'SM_Env_Tree_01', { fit: 3.47 }),
  bush: prop('nature', 'SM_Plant_Bush_01', { fit: 0.9, thick: 0.6 }),
  rock: prop('nature', 'SM_Rock_01', { fit: 0.95 }),
  boulder: prop('nature', 'SM_Rock_Boulder_01', { fit: 0.95, thick: 1.4 }),
  log: prop('nature', 'SM_Tree_Log_01', { fit: 1, stretch: true, thick: 2.5 }),
  // On roofs (buildings.ts): drawn at the kit's scale (0.8 tiles a metre).
  roofAircon: prop('city', 'SM_Prop_Roof_Aircon_01', { fit: 0.9 }),
  roofAirconLong: prop('city', 'SM_Prop_Roof_Aircon_02', { fit: 1.6 }),
  roofVent: prop('town', 'SM_Prop_Airvent_01', { fit: 0.65 }),
  roofSkylight: prop('town', 'SM_Bld_Skylight_01', { fit: 1.3 }),
  roofAccess: prop('city', 'SM_Bld_Roof_Access_01', { fit: 2.3 }),
  roofDish: prop('apocalypse', 'SM_Prop_Roof_Satellite_Dish_01', { fit: 0.75 }),
  chimney: prop('town', 'SM_Bld_Chimney_01', { fit: 0.8 }),
  // Ground cover (ground.ts): cosmetic, scattered off the grid.
  tuft: prop('apocalypse', 'SM_Env_Grass_Tuft_01', { fit: 0.3 }),
  tuftLow: prop('apocalypse', 'SM_Env_Grass_Tuft_02', { fit: 0.3 }),
  tuftWide: prop('apocalypse', 'SM_Env_Grass_Tuft_03', { fit: 0.45 }),
  flowers: prop('apocalypse', 'SM_Env_Flowers_02', { fit: 0.6 }),
  pebbles: prop('apocalypse', 'SM_Env_Rubble_Pebbles_02', { fit: 0.6 }),
  leaves: prop('apocalypse', 'SM_Env_GroundLeaves_02', { fit: 1.5 }),
  grassBlob: prop('apocalypse', 'SM_Env_GrassBlob_01', { fit: 0.9 }),
  papers: prop('apocalypse', 'SM_Prop_Papers_01', { fit: 0.7 }),
  trash: prop('apocalypse', 'SM_Prop_Trash_02', { fit: 1 }),
  manhole: prop('town', 'SM_Prop_Manhole_01', { fit: 0.75 }),
  drain: prop('town', 'SM_Prop_Drain_01', { fit: 0.5 }),
  // Searched furniture (mapView.ts): opened, and its contents on the floor.
  crateOpen: prop('apocalypse', 'SM_Prop_Crate_Open_01'),
  crateLid: prop('apocalypse', 'SM_Prop_Crate_Open_Lid_01'),
  drawer: prop('apocalypse', 'SM_Prop_Dresser_03_Drawer_01', { fit: 0.45 }),
  laundry: prop('town', 'SM_Prop_Laundry_Pile_01', { fit: 0.4 }),
  cardboard: prop('apocalypse', 'SM_Prop_CardboardBox_01', { fit: 0.45 }),
  // The special loot boxes (docs/FASE8.md §B; mapView.ts lootBox): each its own shape and colour from afar, shut and opened.
  lootWeapons: prop('military', 'SM_Prop_Crate_Wood_01', { fit: 0.92 }),
  lootWeaponsLid: prop('military', 'SM_Prop_Crate_Wood_Lid_01', { fit: 0.92 }),
  lootMedical: prop('apocalypse', 'SM_Prop_Medical_Container_01', { fit: 0.66 }),
  lootMedicalKit: prop('apocalypse', 'SM_Prop_MedicalBox_02', { fit: 0.36 }),
  lootPills: prop('apocalypse', 'SM_Item_Pills_01', { fit: 0.1 }),
  lootAmmo: prop('military', 'SM_Prop_Crate_Ammo_01', { fit: 0.72 }),
  lootAmmoLid: prop('military', 'SM_Prop_Crate_Ammo_Lid_01', { fit: 0.72 }),
  lootAmmoCan: prop('apocalypse', 'SM_Prop_Ammo_Box_01', { fit: 0.3 }),
  lootAmmoCanOpen: prop('apocalypse', 'SM_Prop_Ammo_Box_Open_01', { fit: 0.3 }),
  lootFood: prop('apocalypse', 'SM_Prop_CardboardBox_03', { fit: 0.62 }),
  lootFoodTop: prop('apocalypse', 'SM_Prop_CardboardBox_02', { fit: 0.42 }),
  lootCan: prop('apocalypse', 'SM_Item_Can_01', { fit: 0.08 }),
  lootCan2: prop('apocalypse', 'SM_Item_Can_04', { fit: 0.08 }),
  lootRice: prop('apocalypse', 'SM_Item_Rice_01', { fit: 0.14 }),
  // What is left on the floor (docs/FASE8.md §C, dropped.ts): a bag, unless it is a weapon or a medicine.
  dropBag: prop('apocalypse', 'SM_Item_Duffle_Bag_01', { fit: 0.44 }),
  // A fallen soldier's things (playtest 18/10, #49): Military's rucksack, the one the soldiers carry.
  fallenPack: prop('military', 'SM_Chr_Attach_Backpack_01', { fit: 0.5 }),
  // The scenarios (#25). The forest stop, from «Woodland Apocalypse»: the sawmill's log piles, wrecked lorries (nose
  // north, a block of 2×4), the lumberjack on his plinth, the roadside signs and broken fences; Nature's solid pines
  // (Woodland's own are painted from an atlas we don't have).
  pineLarge: prop('nature', 'SM_Tree_PolyPine_02', { fit: 1.7 }),
  pineLarge2: prop('nature', 'SM_Tree_PolyPine_03', { fit: 1.6 }),
  logPile: prop('woodland', 'SM_Prop_Log_Pile_02', { fit: 1.9 }),
  truckWreck: prop('woodland', 'SM_Prop_Truck_Wrecked_01', { fit: 3.9, yaw: 2 }),
  truckWreck2: prop('woodland', 'SM_Prop_Truck_Wrecked_02', { fit: 3.9, yaw: 2 }),
  lumberjack: prop('woodland', 'SM_Prop_Statue_Lumberjack_01', { fit: 0.9 }),
  lumberjackBase: prop('woodland', 'SM_Prop_Statue_Base_01', { fit: 0.95 }),
  baitSign: prop('woodland', 'SM_Prop_Bait_Sign_01', { fit: 0.9 }),
  baitSign2: prop('woodland', 'SM_Prop_Bait_Sign_Alt_01', { fit: 0.9 }),
  gasSign: prop('woodland', 'SM_Prop_Gas_Sign_01', { fit: 1.1, yaw: 2 }),
  natureSign: prop('woodland', 'SM_Prop_Nature_Sign_01', { fit: 0.9 }),
  brochures: prop('woodland', 'SM_Prop_Brochure_Stand_01', { fit: 0.5 }),
  woodFence: prop('woodland', 'SM_Prop_Fence_Wood_Damaged_01', { fit: 1 }),
  woodFence2: prop('woodland', 'SM_Prop_Fence_Wood_Damaged_02', { fit: 1 }),
  chainFence: prop('woodland', 'SM_Prop_Chain_Fence_Broken_01', { fit: 1 }),
  // A shipping container (a block of 2×5, its doors north), in three of Woodland's colours.
  container: prop('woodland', 'SM_Prop_Shipping_Container_01', { fit: 4.85, yaw: 2, atlas: 'containerRed' }),
  containerBlue: prop('woodland', 'SM_Prop_Shipping_Container_01', { fit: 4.85, yaw: 2, atlas: 'containerBlue' }),
  containerOrange: prop('woodland', 'SM_Prop_Shipping_Container_01', { fit: 4.85, yaw: 2, atlas: 'containerOrange' }),
  waterTower: prop('apocalypse', 'SM_Bld_WaterTower_01', { fit: 2.3 }),
  razorWire: prop('military', 'SM_Prop_RazorWire_01', { fit: 1, stretch: true }),
  /** Round the sides of hills (buildings.ts stretches it to a storey). */
  cliff: prop('nature', 'SM_Rock_Wall_01', { fit: 1.4 }),
  cliff2: prop('nature', 'SM_Rock_Wall_02', { fit: 1.4 }),
  // Dressing (dressing.ts): what makes a street look lived in and left, as in Synty's captures.
  // Low cover too: piles of rubbish, junk and tyres (cityLow).
  trashPile: prop('apocalypse', 'SM_Prop_TrashPile_02', { fit: 1.05 }),
  trashPile2: prop('apocalypse', 'SM_Prop_TrashPile_03', { fit: 1.05 }),
  junkPile: prop('apocalypse', 'SM_Prop_JunkPile_01', { fit: 1 }),
  tirePile: prop('apocalypse', 'SM_Prop_Tire_Pile_01', { fit: 1 }),
  // Street furniture: slim, at the kerb's corners.
  streetLight: prop('apocalypse', 'SM_Prop_LightPole_Base_01', { origin: true }),
  powerPole: prop('apocalypse', 'SM_Prop_Powerpole_Single_01', { origin: true }),
  powerLine: prop('apocalypse', 'SM_Prop_Powerpole_Single_Line_01', { origin: true }),
  hydrant: prop('apocalypse', 'SM_Prop_Hydrant_01'),
  mailbox: prop('apocalypse', 'SM_Prop_Mailbox_01'),
  stopSign: prop('apocalypse', 'SM_Prop_Sign_Stop_01', { origin: true }),
  cone: prop('apocalypse', 'SM_Prop_Cone_01'),
  // Lying about: flat enough not to read as cover.
  trashBag: prop('apocalypse', 'SM_Prop_TrashBag_01'),
  trashBag2: prop('apocalypse', 'SM_Prop_TrashBag_02'),
  trashBag3: prop('apocalypse', 'SM_Prop_TrashBag_04'),
  tire: prop('apocalypse', 'SM_Prop_Tire_01'),
  pallet: prop('apocalypse', 'SM_Prop_Pallet_01'),
  bike: prop('apocalypse', 'SM_Prop_Bike_Broken_01'),
  // What else lies about a street (playtest 20/10 #20, Ana: not only rubbish): luggage dropped, a holdall, a trolley
  // pushed over, boxes.
  luggage: prop('apocalypse', 'SM_Prop_Luggage_01'),
  luggageRetro: prop('apocalypse', 'SM_Prop_Luggage_Retro_01'),
  luggageOpen: prop('apocalypse', 'SM_Prop_Luggage_Open_01'),
  holdall: prop('apocalypse', 'SM_Item_Duffle_Bag_01'),
  trolley: prop('apocalypse', 'SM_Prop_ShoppingCart_01'),
  trolleyBroken: prop('apocalypse', 'SM_Prop_ShoppingCart_Broken_01'),
  cardboard2: prop('apocalypse', 'SM_Prop_CardboardBox_02', { fit: 0.45 }),
  cardboard4: prop('apocalypse', 'SM_Prop_CardboardBox_04', { fit: 0.45 }),
  // The Armería's bench (playtest 20/10 #29, Ana: a table and boxes with what there is): its grenades, its ammunition.
  armGrenade: prop('military', 'SM_Chr_Attach_Grenade_01'),
  armFlashbang: prop('military', 'SM_Chr_Attach_Grenade_Flash_01'),
  armSmoke: prop('military', 'SM_Chr_Attach_Grenade_Smoke_01'),
  armAmmo556: prop('apocalypse', 'SM_Item_Ammo_556_01'),
  armAmmo762: prop('apocalypse', 'SM_Item_Ammo_762_Open_01'),
  armAmmo12g: prop('apocalypse', 'SM_Item_Ammo_12G_01'),
  armAmmoCrate: prop('apocalypse', 'SM_Item_Ammo_Crate_01'),
  armAmmoCrateLid: prop('apocalypse', 'SM_Item_Ammo_Crate_Lid_01'),
  // On shop fronts, over the door.
  signShop: prop('apocalypse', 'SM_Prop_Sign_Shop_01'),
  signFood: prop('apocalypse', 'SM_Prop_Sign_Food_01'),
  signMarket: prop('apocalypse', 'SM_Prop_Sign_Market_01'),
  signDiner: prop('apocalypse', 'SM_Prop_Sign_Diner_01'),
  signPets: prop('apocalypse', 'SM_Prop_Sign_Pets_01'),
  // Offices (POLYGON Office): desks, a chair at each, filing cabinets.
  officeDesk: prop('office', 'SM_Prop_Desk_01', { fit: 0.95, thick: 1.4 }),
  officeDesk2: prop('office', 'SM_Prop_Desk_03', { fit: 0.95, thick: 1.4 }),
  officeChair: prop('office', 'SM_Prop_Chair_01'),
  officeCabinet: prop('office', 'SM_Prop_Cabinets_01'),
  // Building sites (POLYGON Construction): barriers, stacks of bricks and pallets, sacks of cement, a portaloo.
  siteBarrier: prop('construction', 'SM_Prop_Barrier_Plastic_01', { fit: 1, stretch: true }),
  brickStack: prop('construction', 'SM_Prop_Brick_Stack_01', { fit: 0.95 }),
  palletStack: prop('construction', 'SM_Prop_PalletStack_01', { fit: 0.95 }),
  cementBags: prop('construction', 'SM_Prop_ConcreteBag_Stack_01', { fit: 0.9 }),
  portaloo: prop('construction', 'SM_Prop_Portaloo_01', { fit: 0.95 }),
  // Checkpoints (POLYGON Military): concrete barriers, tall ones, ammunition crates.
  milBarrier: prop('military', 'SM_Prop_Barrier_01', { fit: 1, stretch: true }),
  milBarrierTall: prop('military', 'SM_Prop_Barrier_Tall_01', { fit: 1 }),
  milCrate: prop('military', 'SM_Prop_Crate_Cube_01', { fit: 0.8 }),
  // What shuts the map's edges where there is no street (dressing.ts dressEdges, playtest 18/10, #32): a block
  // come down in heaps, Construction's sheet-metal hoardings and precast walls; at their own size, along their x.
  debrisPile: prop('military', 'SM_Prop_DebrisPile_01'),
  debrisPile2: prop('military', 'SM_Prop_DebrisPile_03'),
  ruinPile: prop('military', 'SM_Bld_Destroyed_02'),
  sheetFence: prop('construction', 'SM_Prop_Fence_MetalSheet_01'),
  sheetFence2: prop('construction', 'SM_Prop_Fence_MetalSheet_02'),
  concreteFence: prop('construction', 'SM_Prop_Fence_Concrete_01'),
  // A filling station's pumps (town `tank` hints), a motel's sign by the road, billboards on roofs.
  pump: prop('apocalypse', 'SM_Prop_Gaspump_01'),
  pump2: prop('apocalypse', 'SM_Prop_Gaspump_02'),
  // The canopy over a filling station's pumps, and their island (mapView: one over each pair of pumps).
  pumpCanopy: prop('apocalypse', 'SM_Prop_Gaspump_Cover_01', { origin: true }),
  pumpCanopyBurnt: prop('apocalypse', 'SM_Prop_Gaspump_Cover_Burnt_01', { origin: true }),
  pumpIsland: prop('apocalypse', 'SM_Prop_Gaspump_Base_01'),
  // A shed's roof (#25): flat skylights in a row; a roller shutter on its walls (Generic's atlas).
  skylight: prop('industrial', 'SM_Bld_Skylight_Flat_01', { origin: true }),
  rollerShutter: prop('industrial', 'SM_Bld_Door_Roller_Large_01', { origin: true, atlas: 'generic' }),
  motelSign: prop('apocalypse', 'SM_Bld_Motel_Sign_01', { origin: true }),
  billboard: prop('apocalypse', 'SM_Prop_Billboard_Roof_01'),
  billboardSign: prop('apocalypse', 'SM_Prop_Billboard_Sign_01'),
  billboardSign2: prop('apocalypse', 'SM_Prop_Billboard_Sign_03'),
  // Beyond the map's edge, where nobody walks: the city goes on.
  bus: prop('apocalypse', 'SM_Prop_Car_Wrecked_Bus_01'),
  van: prop('apocalypse', 'SM_Prop_Car_Wrecked_Van_01'),
} satisfies Record<string, PropAsset>;

export type PropId = keyof typeof PROPS;

/**
 * How long a prop is drawn over an object that takes several tiles (docs/MAPAS.md, phase 1; mapView.ts), in tiles
 * along its longest side: its model's size × CHARACTER_SCALE (1.125: beside a survivor as Synty made them; sizes
 * measured 19/10, in metres), or as long as its tiles, 0.05 short, where that is more. `thin`: its other sides
 * times that, where they would overflow its tiles (Synty's single bed is 1.7 m wide). The fit above stays what one
 * on a tile of its own takes, or on the blocks of before: a mission saved before phase 1, the street's wrecks
 * beyond the map's edge. A stretched prop takes its other sides × 1.125 too.
 */
export const FOOTPRINT_FIT: Partial<Record<PropId, { fit: number; thin?: number }>> = {
  // A car, 1 × 3: 5.16 m (the SUV 4.97 m) would be 3.87 tiles; 0.83 wide.
  carWreck: { fit: 2.95 },
  carRusted: { fit: 2.95 },
  carSuv: { fit: 2.95 },
  carOpen: { fit: 2.95 },
  // The sabotage's patrol car (5.25 m), 1 × 3; the ambulance (6.45 m, 4.84 tiles), 2 × 4: 1.64 wide.
  sirenPolice: { fit: 2.95 },
  sirenAmbulance: { fit: 3.9 },
  // A barn's tractor, 2 × 3: 3.3 m, 2.48 tiles (1.58 wide).
  tractor: { fit: 2.48 },
  // A lorry, 2 × 6: 9.52 m (7.14 tiles); the other is 3.27 m wide, so as long as two tiles wide lets it be.
  truckWreck: { fit: 5.9 },
  truckWreck2: { fit: 4.7 },
  // A single bed, 1 × 2: 2.69 m, 2.02 tiles; 1.22 wide narrowed to 0.95.
  bed: { fit: 1.95, thin: 0.78 },
  bedDouble: { fit: 1.95 },
  // A sofa, a table, a bath (2 × 1): 1.83, 2.1 and 2.07 m long; the worn sofa 2.8 m, as long as its tiles.
  sofa: { fit: 1.37 },
  sofaDirty: { fit: 1.95 },
  table: { fit: 1.58 },
  bathtub: { fit: 1.55 },
  // A hospital bed and a gurney, 1 × 2: 2.51 and 2.33 m; the bed's 1.44 m (rails) narrowed to 0.8 tiles, room for a screen.
  hospitalBed: { fit: 1.88, thin: 0.74 },
  gurney: { fit: 1.75 },
};

/**
 * The Synty props each of the map's prop sets draws (mapView.ts), one picked
 * per tile. Low cover stands about half a survivor tall, high cover over
 * their head.
 */
export const PROP_SETS = {
  cityLow: ['crates', 'barricade1', 'concreteBarrier', 'dumpster', 'barrels', 'trashPile', 'junkPile', 'trashPile2', 'tirePile', 'brickStack', 'siteBarrier'],
  cityHigh: ['vending', 'barricadeTall', 'gasTank', 'cabinet', 'portaloo', 'milBarrierTall'],
  wildsLow: ['rock', 'log', 'bush', 'rock'],
  wildsHigh: ['tree', 'treePine', 'treeDead', 'boulder'],
  facilityLow: ['crate', 'barricadePallet', 'generator', 'milBarrier', 'milCrate'],
  facilityHigh: ['workShelf', 'cabinet', 'gasTank', 'workShelf'],
  console: ['desk'],
  carFront: ['carWreck', 'carRusted', 'carSuv', 'carOpen'],
  planter: ['planter'],
  bin: ['dumpster', 'rubbishBin'],
  streetTree: ['streetPot'],
  kiosk: ['vending'],
  statue: ['statue'],
  bench: ['parkBenchBroken'],
  // The scenarios' own (#25; mapView draws the blocks whole): log piles, roadside signs, a fallen chain-link fence, razor wire.
  logPile: ['logPile'],
  truck: ['truckWreck', 'truckWreck2'],
  container: ['container', 'containerBlue', 'containerOrange'],
  roadSign: ['gasSign'],
  chainFence: ['chainFence'],
  razorWire: ['razorWire'],
  pines: ['pineLarge', 'pineLarge2', 'treePine', 'pineLarge'],
  // The base's pinar (basePieces.ts): a dead one now and then.
  basePines: ['pineLarge', 'treePine', 'pineLarge2', 'pineLarge', 'treePine', 'pineLarge2', 'pineLarge', 'treePine', 'pineLarge2', 'pineLarge', 'treePine', 'pineLarge2', 'pineLarge', 'treePine', 'pineLarge2', 'pineLarge', 'treePine', 'pineLarge2', 'pineLarge', 'pineDead'],
  woodFence: ['woodFence'],
  fencePost: ['fencePost'],
  fenceRails: ['fenceWire'],
  fenceBoards: ['fencePanel'],
  gatePost: ['gatePost'],
  hayBale: ['hayBale'],
  hayStack: ['hayRound'],
  shelfLow: ['shelfLow'],
  shelfHigh: ['shelfHigh', 'bookshelf'],
  counter: ['counter', 'shopCounter'],
  table: ['table'],
  fridge: ['fridge'],
  stove: ['stove'],
  bathtub: ['bathtub'],
  washbasin: ['washbasin'],
  wc: ['wc'],
  tvStand: ['tvStand'],
  sofa: ['sofa', 'sofaDirty'],
  bed: ['bed'],
  hospitalBed: ['hospitalBed'],
  medicalShelf: ['medicalShelf'],
  gurney: ['gurney'],
  desk: ['desk', 'officeDesk', 'officeDesk2'],
  labBench: ['officeDesk2'],
  wardrobe: ['wardrobe'],
  cabinet: ['cabinet', 'bookshelf', 'officeCabinet'],
  rack: ['workShelf'],
  crates: ['crates', 'crate', 'palletStack', 'cementBags'],
  lootWeapons: ['lootWeapons'],
  lootMedical: ['lootMedical'],
  lootAmmo: ['lootAmmo'],
  lootFood: ['lootFood'],
  techCrate: ['crate'],
  // The factory (playtest 15/10 #17; mapView.ts stacks the pallets and stands the machine taller).
  pallets: ['pallet'],
  drums: ['drums', 'barrels', 'drumsTarp'],
  machine: ['machine'],
  silo: ['silo3', 'silo2'],
  barricade: ['barricade1', 'barricade2', 'barricadePallet', 'sandbags', 'milBarrier', 'siteBarrier'],
  sandbags: ['sandbags'],
  techBarrier: ['concreteBarrier', 'milBarrier'],
  pipe: ['pipes'],
  tank: ['gasTank'],
  pump: ['pump', 'pump2'],
  portaloo: ['portaloo'],
  pylon: ['cabinet'],
  tree: ['streetTree', 'tree'],
  forest: ['tree', 'treePine', 'tree'],
  bush: ['bush'],
  rock: ['rock'],
  spire: ['boulder'],
  logs: ['log'],
  rubble: ['rubble1', 'rubble2'],
  wallStub: ['rubbleWall'],
} satisfies Record<string, PropId[]>;

/** A set of props the map picks from for a tile of cover (mapView.ts). */
export type PropSet = keyof typeof PROP_SETS;

// --------------------------------------------------------------- buildings

/**
 * Synty's modular kits are drawn on a 2.5 m grid and a tile is 1.5 m, so the
 * map draws building pieces this much bigger than SYNTY_SCALE: a module is
 * then two tiles wide, a wall one storey tall (STOREY, 3.5 m) and a door
 * 1.4 m wide, about a survivor's 2 m. Each wall tile takes the middle half of
 * a module (KIT_TILE metres): a door or window module puts its opening right
 * in the tile where the rules have it, wherever that is along the wall.
 */
export const KIT_SCALE = 1.2;
export const KIT_TILE = 1.5 / KIT_SCALE;

/**
 * A piece of a modular building kit. The converter keeps the box `keep` of
 * the FBX (metres and axes as in the FBX, Z up; none: all of it), keeps its
 * meshes apart with `parts` (`leaf` is the one that swings open) and leaves
 * out the meshes named in `drop` (glass: the map draws its own, so it can
 * break). `centre` is the middle of the module in the FBX, along its run (x,
 * or y with `run: 'y'`) and across it (the wall's middle plane); `out` the
 * way across the run its outer face looks (Town: −y). `unit` is metres per
 * unit of the FBX (City's come in hundreds of metres: 100) and `storey` the
 * kit's floor-to-ceiling height, which the game stretches to one STOREY. The
 * rest (turning it to its side of the building, KIT_SCALE) is done in the game.
 */
export interface PieceAsset {
  pack: PackId;
  fbx: string;
  centre: [number, number];
  run?: 'x' | 'y';
  out?: 1 | -1;
  unit?: number;
  storey?: number;
  /** Height of the piece's foot in the FBX, when it isn't 0 (a door leaf modelled round its middle). */
  base?: number;
  keep?: [[number, number, number], [number, number, number]];
  parts?: boolean;
  leaf?: string;
  /** Narrower along its run than the kit's scale (a gate's leaf, one to a tile). */
  squeeze?: number;
  /** Its leaf turns on its far end along the run, not its near one (a gate's second leaf). */
  hingeFar?: boolean;
  drop?: string[];
  /** Painted from another atlas than its pack's (a texture of its own). */
  atlas?: AtlasId;
}

interface PieceOptions {
  run?: 'x' | 'y';
  out?: 1 | -1;
  unit?: number;
  storey?: number;
  base?: number;
  squeeze?: number;
  hingeFar?: boolean;
  /** Keep only this much of the module across its run, from its middle plane out (and as much in): the outer face alone. */
  depth?: number;
  /** And nothing above this height (FBX units): a lid. */
  top?: number;
  /** Keep only what is above this height (metres; the foundation below the floor goes). */
  floor?: number;
  parts?: boolean;
  leaf?: string;
  drop?: string[];
  atlas?: AtlasId;
}

/** A piece cut to one tile of its module: KIT_TILE along the run about `centre`, from `floor` up. */
function strip(pack: PackId, fbx: string, centre: [number, number], o: PieceOptions = {}): PieceAsset {
  const along = o.run === 'y' ? 1 : 0;
  const unit = o.unit ?? 1;
  const lo: [number, number, number] = [-50, -50, o.floor ?? -0.02 / unit];
  const hi: [number, number, number] = [50, 50, o.top ?? 50];
  lo[along] = centre[0] - KIT_TILE / 2 / unit;
  hi[along] = centre[0] + KIT_TILE / 2 / unit;
  if (o.depth !== undefined) {
    lo[1 - along] = centre[1] - o.depth;
    hi[1 - along] = centre[1] + o.depth;
  }
  const { depth: _depth, top: _top, ...rest } = o;
  return whole(pack, fbx, centre, { ...rest, keep: [lo, hi] });
}

/** A piece kept whole (a door, a window, a corner post), or cut to `keep`. */
function whole(pack: PackId, fbx: string, centre: [number, number], o: PieceOptions & { keep?: PieceAsset['keep'] } = {}): PieceAsset {
  const { floor, ...rest } = o;
  const keep = rest.keep ?? (floor !== undefined ? [[-50, -50, floor], [50, 50, 50]] : undefined);
  return { pack, fbx, centre, run: o.run ?? 'x', out: o.out ?? -1, ...rest, keep };
}

/**
 * City's apartment blocks: a 5 m cube with its windows on the −y face and plain brick on the −x face, each
 * cut to the tile-wide strip in the middle of that face (`run`: x the windows, y the plain side).
 */
const apartment = (fbx: string, run: 'x' | 'y') =>
  strip('city', fbx, [0.025, 0], { run, unit: 100, storey: 3, depth: 0.004, top: 0.0302, floor: -0.0002 });

/**
 * A sheet-metal skin: a tile-wide strip of the +x side of Woodland's container (x = 0.327; 2.59 m tall, lid
 * and floor cut off), near its door end, clear of the logo both sides carry; set so the side lies on a
 * bunker wall's outer face; from `floor` up over a door.
 */
const sheet = (atlas: AtlasId, floor = 0.03, top = 2.56) =>
  strip('woodland', 'SM_Prop_Shipping_Container_01', [2.3, 0.212], { run: 'y', out: 1, storey: 2.59, depth: 0.2, top, floor, atlas });

/** Apocalypse's bunker kit: modules from y = 0 to 2.5, walls from x = −0.125 to 0.125. */
const bunker = (fbx: string, o: PieceOptions = {}) => strip('apocalypse', fbx, [1.25, 0], { run: 'y', out: 1, storey: 3, ...o });

/** Town's house kit: modules from x = 0 to 2.5, walls from y = −0.2 to 0, siding on the −y face. */
const house = (fbx: string, { at = 1.25, ...o }: PieceOptions & { at?: number } = {}) => strip('town', `SM_Bld_House_${fbx}`, [at, -0.1], o);

export const PIECES = {
  // Houses: siding outside, painted plaster inside.
  houseWall: house('ExteriorWall_GroundFloor_01'),
  houseDoorWall: house('ExteriorWall_GroundFloor_Door_01'),
  houseWindowWall: house('ExteriorWall_GroundFloor_Window_01', { at: 3.75 }),
  /** Over an opening with no door (a loading bay). */
  houseLintel: house('ExteriorWall_GroundFloor_01', { floor: 2.35 }),
  houseCorner: whole('town', 'SM_Bld_House_ExteriorWall_GroundFloor_Corner_01', [-0.12, -0.12], { floor: -0.02 }),
  houseDoor: whole('town', 'SM_Bld_House_Door_01', [1.25, -0.1], { parts: true, leaf: 'SM_Bld_House_Door' }),
  houseWindow: whole('town', 'SM_Bld_House_Window_01', [1.25, -0.1], { drop: ['Glass'] }),
  // A shop's front at street level (playtest 20/10 #18): Office's glass walls, 2.5 m modules 3 m tall (as the bunker
  // kit's): a shop window from floor to lintel (its pane the kit's), and a glass door beside a pane, seen through
  // (their glass, painted from the atlas, came out brown).
  shopWindow: whole('office', 'SM_Bld_Wall_Glass_01', [1.25, -0.075], { drop: ['01_Glass'] }),
  shopDoor: whole('office', 'SM_Bld_Wall_Glass_Door_01', [1.25, -0.075], { parts: true, leaf: 'SM_Bld_Glass_Door_01', drop: ['Door_01_Glass'] }),
  // Walls between rooms.
  roomWall: house('InteriorWall_01'),
  roomDoorWall: house('InteriorWall_Door_01', { parts: true, leaf: 'SM_Bld_House_InteriorWall_Door_02' }),
  roomLintel: house('InteriorWall_01', { floor: 2.35 }),
  // Raised blocks (flats): City's brick apartments, one window a tile. Their FBX are in hundreds of metres,
  // modules 5 m wide and 3 m a storey; only the outer face is kept (the block is solid).
  blockWindow: apartment('SM_Bld_Apartment_01', 'x'),
  blockPlain: apartment('SM_Bld_Apartment_01', 'y'),
  /** The top storey: arched windows under a cornice. */
  blockTopWindow: apartment('SM_Bld_Apartment_02', 'x'),
  blockTopPlain: apartment('SM_Bld_Apartment_02', 'y'),
  /** Up the side of a raised block or a hill (3 m, stretched to a storey). */
  ladder: whole('apocalypse', 'SM_Prop_Ladder_01', [0, 0.035], { run: 'y', out: 1, storey: 3 }),
  // Labs and the walls of warehouses (Apocalypse's bunker kit: 2.5 m modules along y, 3 m tall, concrete both sides).
  bunkerWall: bunker('SM_Bld_Bunker_Wall_Concrete_x1_01'),
  /** Over a door. */
  bunkerLintel: bunker('SM_Bld_Bunker_Wall_Concrete_x1_01', { floor: 2.1 }),
  /** A reinforced window: a steel frame round an opening above a sill. */
  bunkerWindow: bunker('SM_Bld_Bunker_Wall_Metal_Door_x1_01'),
  /**
   * Its sill (19/10, MAPAS.md: the frame is a door's, open from 0.47 m): the foot of the wall under it, a metre high
   * (1.15 m drawn), so that the window opens at a window's height; with the brick or the sheet metal over it outside.
   */
  bunkerSill: bunker('SM_Bld_Bunker_Wall_Concrete_x1_01', { top: 1 }),
  metalDoor: whole('apocalypse', 'SM_Bld_Metal_Door_01', [0.47, 0], { storey: 3, base: -1.07, parts: true, leaf: 'SM_Bld_Metal_Door_01' }),
  /** Warehouses: the brick of the Apocalypse warehouse, a skin outside a bunker wall (its panels have one face). */
  brickFace: strip('apocalypse', 'SM_Bld_Warehouse_Brick_01', [-1.83, -4.915], { storey: 3, depth: 0.3, top: 2.95 }),
  brickLintel: strip('apocalypse', 'SM_Bld_Warehouse_Brick_01', [-1.83, -4.915], { storey: 3, depth: 0.3, top: 2.95, floor: 2.1 }),
  brickSill: strip('apocalypse', 'SM_Bld_Warehouse_Brick_01', [-1.83, -4.915], { storey: 3, depth: 0.3, top: 1 }),
  // A facility's perimeter: the quarantine wall (5 m panels, cut to a storey), its columns at gates and corners.
  fenceWall: strip('apocalypse', 'SM_Prop_Wall_Quarantine_Bottom_01', [-2.5, -0.47], { storey: 3, top: 2.95 }),
  fenceColumn: whole('apocalypse', 'SM_Prop_Wall_Quarantine_Column_Tall_01', [0, 0], { storey: 3, keep: [[-5, -5, -0.02], [5, 5, 3.4]] }),
  barbedWire: whole('apocalypse', 'SM_Prop_Wall_Quarantine_BarbedWire_01', [0, 0], { storey: 3 }),
  // A siege's gate (docs/FASE12.md §10): Quarantine's sheet-metal leaves, a tile each, each hinged at its end of the gate.
  gateLeaf: whole('apocalypse', 'SM_Prop_Wall_Quarantine_GateDoor_02', [0.87, 0.08], { storey: 3.4, squeeze: 0.68, parts: true, leaf: 'SM_Prop_Wall_Quarantine_GateDoor_02' }),
  gateLeafFar: whole('apocalypse', 'SM_Prop_Wall_Quarantine_GateDoor_01', [-0.87, 0.08], { storey: 3.4, squeeze: 0.68, hingeFar: true, parts: true, leaf: 'SM_Prop_Wall_Quarantine_GateDoor_01' }),
  /** Loading platforms: shipping-container sides. */
  containerWall: bunker('SM_Bld_Bunker_Wall_Container_x1_01'),
  // Sheds (#25): sheet metal, a skin outside a bunker wall as the warehouses' brick is: a tile of the side of
  // Woodland's container (its ribs are painted), in three of its colours; over a door, its top.
  sheetGrey: sheet('containerGrey'),
  sheetRed: sheet('containerRed'),
  sheetBlue: sheet('containerBlue'),
  sheetGreyLintel: sheet('containerGrey', 1.81),
  sheetRedLintel: sheet('containerRed', 1.81),
  sheetBlueLintel: sheet('containerBlue', 1.81),
  /** Under a shed's window, as its sill (the container's storey is 2.59 m: 0.86 of it is the bunker's metre). */
  sheetGreySill: sheet('containerGrey', 0.03, 0.86),
  sheetRedSill: sheet('containerRed', 0.03, 0.86),
  sheetBlueSill: sheet('containerBlue', 0.03, 0.86),
  /** A wardrobe someone has searched: its doors and drawers apart, to open them (mapView.ts). */
  wardrobeOpen: whole('town', 'SM_Prop_Wardrobe_01', [0, 0.03], { parts: true, storey: 2.9 }),
} satisfies Record<string, PieceAsset>;

export type PieceId = keyof typeof PIECES;

// ----------------------------------------------------------------- terrain

/**
 * Tiling ground textures (ground.ts): asphalt from Town's roads, grass and
 * dirt from Nature Biomes – Alpine Mountain. `src` is under assets-src/synty/;
 * each is converted to WebP at `size` in public/models/synty/terrain/.
 */
export const TERRAIN = {
  asphalt: { src: 'POLYGON_Town_SourceFiles_v7/Textures/Road_01.png', size: 512 },
  grass: { src: 'POLYGON_NatureBiomes_AlpineMountain_SourceFiles_v3/Textures/Grass_Texture_01.png', size: 1024 },
  dirt: { src: 'POLYGON_NatureBiomes_AlpineMountain_SourceFiles_v3/Textures/Dirt_Texture_01.png', size: 1024 },
} satisfies Record<string, { src: string; size: number }>;

export type TerrainId = keyof typeof TERRAIN;

export const terrainFile = (id: TerrainId) => `models/synty/terrain/${id}.webp`;

// -------------------------------------------------------------------- base

/**
 * The Bastion on the surface (strategy/bastion.ts): Atalaya, a civil-defence
 * station in the snow. Its buildings and props, the pines, rocks and
 * mountains round it, the hangar and La Mula. Each converts whole, like a
 * building piece, to `base-<id>.glb`: `parts` keeps its meshes apart (the
 * bus's doors and wheels move), `drop` leaves some out. The base sizes,
 * turns and places them itself, so changing that needs no reconversion.
 * `fbx` is a path in the pack: Apocalypse keeps a collision twin of many
 * buildings in FBX/Collision under the same name, which a bare name may find.
 */
export interface BaseAsset {
  pack: PackId;
  fbx: string;
  parts?: boolean;
  drop?: string[];
  /** Painted from another atlas than its pack's (Apocalypse's vehicles). */
  atlas?: AtlasId;
}

const apocalypseAt = (name: string): BaseAsset => ({ pack: 'apocalypse', fbx: `FBX/${name}` });
const at = (pack: PackId, fbx: string, o: Omit<BaseAsset, 'pack' | 'fbx'> = {}): BaseAsset => ({ pack, fbx: `FBX/${fbx}`, ...o });

export const BASE_MODELS = {
  // The bunker and the command post round it.
  bunker: apocalypseAt('SM_Bld_Bunker_Entrance_01'),
  hatch: apocalypseAt('SM_Bld_Bunker_Hatch_01'),
  mast: apocalypseAt('SM_Prop_Trailer_Aerial_03'),
  aerial: apocalypseAt('SM_Prop_Trailer_Aerial_01'),
  dish: apocalypseAt('SM_Prop_Trailer_Dish_01'),
  floodlights: apocalypseAt('SM_Prop_Floodlights_01'),
  sandbagWall: apocalypseAt('SM_Prop_Sandbag_Wall_02'),
  sandbagArc: apocalypseAt('SM_Prop_Sandbag_Wall_03'),
  // Buildings and shelters.
  leanTo: at('farm', 'SM_Bld_Shelter_01'),
  trailer: apocalypseAt('SM_Bld_Trailer_01'),
  brickHut: apocalypseAt('SM_Bld_Industrial_Small_01'),
  armyTent: apocalypseAt('SM_Bld_Military_Tent_01'),
  openTent: apocalypseAt('SM_Bld_Military_Tent_02'),
  fieldTent: apocalypseAt('SM_Prop_Tent_Long_01'),
  quarantineTent: apocalypseAt('SM_Bld_Quarantine_Tent_01'),
  shipContainer: apocalypseAt('SM_Prop_Shipping_Container_02'),
  garageShed: at('town', 'SM_Bld_House_Preset_Garage_01'),
  gardenShed: at('town', 'SM_Bld_GardenShed_01'),
  radioTower: apocalypseAt('SM_Bld_RadioTower_01'),
  radioTowerTop: apocalypseAt('SM_Bld_RadioTower_Top_01'),
  shackFrame: apocalypseAt('SM_Bld_Junk_Shelter_01'),
  shackTarp: apocalypseAt('SM_Bld_Junk_Shelter_01_Tarp'),
  climbFrame: apocalypseAt('SM_Bld_Junk_Shelter_02'),
  waterTower: apocalypseAt('SM_Bld_WaterTower_01'),
  lookout: at('alpine', 'Props/SM_Prop_Lookout_01'),
  // Furniture, tools and stores.
  bunkBed: apocalypseAt('SM_Prop_Bed_Bunk_01'),
  firePit: apocalypseAt('SM_Prop_FirePit_01'),
  washingLine: apocalypseAt('SM_Prop_WashingLine_01'),
  campChair: apocalypseAt('SM_Prop_Camp_Chair_01'),
  foldingChair: apocalypseAt('SM_Prop_Chair_Folding_01'),
  longTable: apocalypseAt('SM_Prop_Table_02'),
  blackboard: apocalypseAt('SM_Prop_Blackboard_01'),
  radioSet: apocalypseAt('SM_Prop_Radio_01'),
  solarPanel: apocalypseAt('SM_Prop_Solar_Panel_01'),
  powerBoxes: apocalypseAt('SM_Prop_PowerBoxes_01'),
  transformer: apocalypseAt('SM_Prop_Transformer_01'),
  powerGenerator: apocalypseAt('SM_Prop_Generator_01'),
  workbench: apocalypseAt('SM_Prop_Workbench_01'),
  vice: apocalypseAt('SM_Prop_Vice_01'),
  anvil: apocalypseAt('SM_Prop_Anvil_01'),
  toolbox: apocalypseAt('SM_Prop_ToolBox_01'),
  toolShelf: apocalypseAt('SM_Prop_WorkShelf_01'),
  fuelTable: apocalypseAt('SM_Prop_Gas_Table_01'),
  batteryTable: apocalypseAt('SM_Prop_CarBattery_Table_01'),
  tyres: apocalypseAt('SM_Prop_Tire_Pile_01'),
  oilDrum: apocalypseAt('SM_Prop_Barrel_01'),
  drums: apocalypseAt('SM_Prop_BarrelStack_02'),
  jerrycan: apocalypseAt('SM_Prop_GasCan_01'),
  supplyCrate: apocalypseAt('SM_Prop_Crate_01'),
  bigCrate: apocalypseAt('SM_Prop_Crate_Large_02'),
  ammoCrate: apocalypseAt('SM_Prop_Ammo_Box_01'),
  fuelPump: apocalypseAt('SM_Prop_Gaspump_01'),
  rustyCar: apocalypseAt('SM_Prop_Car_Wrecked_Rusted_01'),
  // The infirmary.
  ivStand: apocalypseAt('SM_Prop_Medical_IVStand_01'),
  medShelf: apocalypseAt('SM_Prop_Medical_Shelf_01'),
  medKit: apocalypseAt('SM_Prop_MedicalBox_02'),
  deconShower: apocalypseAt('SM_Prop_Decontamination_Shower_01'),
  hospitalBed: apocalypseAt('Rigged/SK_Prop_Bed_Hospital_01'),
  gurney: apocalypseAt('Rigged/SK_Prop_Bed_Gurney_01'),
  // The range, the training ground and the craft shed.
  target: apocalypseAt('SM_Prop_Target_01'),
  targetBoard: apocalypseAt('SM_Prop_Target_02'),
  bottleRange: apocalypseAt('SM_Prop_ShootingRange_01'),
  tyreWall: apocalypseAt('SM_Prop_TireWall_01'),
  balanceBeam: apocalypseAt('SM_Prop_Plank_Bench_01'),
  woodPile: at('alpine', 'Props/SM_Prop_Wood_Pile_01'),
  bench: at('alpine', 'Props/SM_Prop_Bench_01'),
  // The Camposanto.
  cross: apocalypseAt('SM_Prop_Cross_01'),
  stoneCross: apocalypseAt('SM_Prop_Cross_02'),
  graveStick: apocalypseAt('SM_Prop_GraveStick_01'),
  picketFence: apocalypseAt('SM_Prop_Fence_Wood_Straight_01'),
  // Works: building, raising a level, clearing a plot.
  roadCone: apocalypseAt('SM_Prop_Cone_01'),
  // Ruins of the old station.
  brokenWall: apocalypseAt('SM_Prop_Wall_Brick_Collasped_01'),
  brickPile: apocalypseAt('SM_Prop_Wall_Brick_Pile_01'),
  rubbleHeap: apocalypseAt('SM_Prop_Rubble_Concrete_01'),
  burntHeap: apocalypseAt('SM_Prop_BurnPile_01'),
  junkHeap: apocalypseAt('SM_Prop_TrashPile_01'),
  // The mountain: pines, rocks, drifts and the range behind.
  pine: at('nature', 'SM_Tree_PolyPine_01'),
  pineSmall: at('nature', 'SM_Tree_PolyPine_02'),
  pineTall: at('nature', 'SM_Tree_PolyPine_Sparse_01'),
  pineDead: at('nature', 'SM_Tree_Pine_Dead_01'),
  stump: at('nature', 'SM_Tree_Stump_01'),
  crag: at('alpine', 'Environment/SM_Env_Rock_01'),
  crag2: at('alpine', 'Environment/SM_Env_Rock_03'),
  crag3: at('alpine', 'Environment/SM_Env_Rock_05'),
  crag4: at('alpine', 'Environment/SM_Env_Rock_07'),
  stone: at('alpine', 'Environment/SM_Env_Rock_Small_01'),
  drift: at('alpine', 'Environment/SM_Env_Snow_Mound_01'),
  range1: at('alpine', 'Environment/SM_Env_MountainRange_01'),
  range2: at('alpine', 'Environment/SM_Env_MountainRange_02'),
  range3: at('alpine', 'Environment/SM_Env_MountainRange_03'),
  range4: at('alpine', 'Environment/SM_Env_MountainRange_04'),
  // The perimeter, the road and the valley.
  jersey: apocalypseAt('SM_Prop_Barrier_Long_01'),
  boomPost: apocalypseAt('SM_Prop_Road_Barrier_01'),
  boomArm: apocalypseAt('SM_Prop_Road_Barrier_Arm_01'),
  lampPost: apocalypseAt('SM_Prop_LightPole_01'),
  pylon: apocalypseAt('SM_Bld_Pylon_01'),
  boardwalk: apocalypseAt('SM_Prop_WoodSheet_01'),
  // The hangar and La Mula: a city bus plated with sheet metal.
  hangarWall: apocalypseAt('SM_Bld_Bunker_Wall_Concrete_x2_01'),
  hangarPillar: apocalypseAt('SM_Bld_Bunker_Beam_Concrete_01'),
  hangarRoof: apocalypseAt('SM_Bld_Bunker_Ceiling_Corrugated_01'),
  hangarGate: apocalypseAt('SM_Bld_Warehouse_Concrete_Door_01'),
  wallLamp: apocalypseAt('SM_Prop_Light_Industrial_Wall_01'),
  mulaBus: at('town', 'SM_Veh_Bus_01', { parts: true }),
  plateSlit: apocalypseAt('SM_Veh_Attach_Plating_10'),
  plate: apocalypseAt('SM_Veh_Attach_Plating_01'),
  plateRoof: apocalypseAt('SM_Veh_Attach_Plating_05'),
  // La Lata before it is the bus (playtest 18/10, #45): Apocalypse's vehicles with their own armour, bars and racks
  // (each a model of its own, in place round the vehicle's origin); the bodies keep their wheels and doors apart.
  lataCar: at('apocalypse', 'Vehicles/SK_Veh_Car_Medium_01', { parts: true, atlas: 'apocalypseVehicle' }),
  lataCarArmour: at('apocalypse', 'Vehicles/SK_Veh_Car_Medium_Armour_01', { atlas: 'apocalypseVehicle' }),
  lataCarBars: at('apocalypse', 'Vehicles/SK_Veh_Car_Medium_Glass_Armour_01', { atlas: 'apocalypseVehicle' }),
  lataVan: at('apocalypse', 'Vehicles/SK_Veh_NewsVan_01', { parts: true, atlas: 'apocalypseVehicle' }),
  lataVanArmour: at('apocalypse', 'Vehicles/SK_Veh_NewsVan_Armour_01', { atlas: 'apocalypseVehicle' }),
  lataVanRoof: at('apocalypse', 'Vehicles/SK_Veh_NewsVan_Roof_Props_01', { atlas: 'apocalypseVehicle' }),
  lataVanBars: at('apocalypse', 'Vehicles/SK_Veh_NewsVan_Glass_Armour_01', { atlas: 'apocalypseVehicle' }),
  lataTruck: at('apocalypse', 'Vehicles/SK_Veh_Army_Truck_01', { parts: true, atlas: 'apocalypseVehicle' }),
  lataTruckArmour: at('apocalypse', 'Vehicles/SK_Veh_Army_Truck_Armour_01', { atlas: 'apocalypseVehicle' }),
  lataTruckCanopy: at('apocalypse', 'Vehicles/SK_Veh_Army_Truck_Canopy_01', { atlas: 'apocalypseVehicle' }),
  lataTruckBars: at('apocalypse', 'Vehicles/SK_Veh_Army_Truck_Glass_Armour_01', { atlas: 'apocalypseVehicle' }),
  snowmobile: at('snow', 'SM_Veh_Snowmobile_01'),
  sled: at('snow', 'SM_Prop_Sled_01'),
  // Building sites, the works brigade and the trades workshop (POLYGON Construction).
  siteDozer: at('construction', 'SM_Veh_Bulldozer_01'),
  siteSpoil: at('construction', 'SM_Env_Dirt_Pile_03'),
  siteBarricade: at('construction', 'SM_Prop_Barrier_Plastic_01'),
  siteLamp: at('construction', 'SM_Prop_Light_Portable_01'),
  siteScaffold: at('construction', 'SM_Prop_Scaffold_02'),
  siteScaffoldFull: at('construction', 'SM_Prop_Scaffold_Preset_01'),
  siteMixer: at('construction', 'SM_Prop_Concrete_Mixer_01'),
  sitePortaloo: at('construction', 'SM_Prop_Portaloo_01'),
  siteBricks: at('construction', 'SM_Prop_Brick_Stack_01'),
  sitePallets: at('construction', 'SM_Prop_PalletStack_01'),
  siteCement: at('construction', 'SM_Prop_ConcreteBag_Stack_01'),
  sitePlanks: at('construction', 'SM_Prop_Plank_Stack_01'),
  siteRebar: at('construction', 'SM_Bld_ConcreteRebar_Pillar_01'),
  siteSlab: at('construction', 'SM_Bld_Concrete_Floor_01'),
  siteOffice: at('construction', 'SM_Bld_Portable_Office_01'),
  siteBarrow: at('construction', 'SM_Prop_Wheelbarrow_01'),
  sawhorse: at('construction', 'SM_Prop_Saw_Horse_01'),
  trestle: at('construction', 'SM_Prop_Trestle_Table_01'),
  tableSaw: at('construction', 'SM_Prop_Table_Saw_01'),
  plankRack: at('construction', 'SM_Prop_StorageShelf_Plank_01'),
  woodPallet: at('construction', 'SM_Prop_Pallet_01'),
  tyreStack: at('construction', 'SM_Prop_TireStack_01'),
  // The Huerto: raised beds and rain barrels, then a greenhouse (its glass tinted apart), then drip lines, seed trays and a scarecrow.
  gardenBed: at('farm', 'SM_Prop_PlantBox_Large_01'),
  gardenBarrel: apocalypseAt('SM_Prop_Barrel_Water_Open_01'),
  gardenBarrelShut: apocalypseAt('SM_Prop_Barrel_Water_01'),
  gardenLettuce: at('farm', 'SM_Prop_Lettuce_01_L'),
  gardenCabbage: at('farm', 'SM_Prop_Cabbage_01_L'),
  gardenPlant: at('farm', 'SM_Prop_Plant_Ground_01_L'),
  gardenPlant2: at('farm', 'SM_Prop_Plant_Ground_02_L'),
  gardenCan: at('farm', 'SM_Prop_Watering_Can_01'),
  greenhouse: at('farm', 'SM_Bld_Greenhouse_01', { parts: true }),
  gardenHose: at('farm', 'SM_Prop_Sprinkler_Hose_01'),
  gardenTable: at('farm', 'SM_Prop_Table_01'),
  gardenSeedlings: at('farm', 'SM_Env_Vege_Rows_02'),
  gardenSeeds: at('farm', 'SM_Prop_SeedPacket_01'),
  scarecrow: at('farm', 'SM_Prop_Scarecrow_01'),
  // The valley of Kessra (strategy/geoscape.ts), in miniature: farms, villages, the dead cities, the power station, clouds.
  mapBarn: at('farm', 'SM_Bld_Barn_01'),
  mapSilo: at('farm', 'SM_Bld_Silo_01'),
  mapCrops: at('farm', 'SM_Env_Vege_Rows_01'),
  mapField: at('farm', 'SM_Env_Dirt_Rows_01'),
  mapTrees: at('farm', 'SM_Generic_Tree_Patch_01'),
  mapTreesWide: at('farm', 'SM_Generic_Tree_Patch_02'),
  mapTree: at('farm', 'SM_Generic_Tree_01'),
  mapHouse: at('town', 'SM_Bld_House_Preset_01'),
  mapHouseSquare: at('town', 'SM_Bld_House_Preset_02'),
  mapHouseL: at('town', 'SM_Bld_House_Preset_03'),
  mapChurch: at('town', 'SM_Bld_Church_01'),
  mapShop: apocalypseAt('SM_Bld_Shop_Small_01'),
  mapOffice: apocalypseAt('SM_Bld_Commercial_Small_01'),
  mapFactory: apocalypseAt('SM_Bld_Industrial_Medium_01'),
  mapWarehouse: apocalypseAt('SM_Bld_Warehouse_Concrete_01'),
  mapCooling: apocalypseAt('SM_Bld_Cooling_Tower_01'),
  mapChimney: apocalypseAt('SM_Bld_SmokeStack_01'),
  mapLighthouse: apocalypseAt('SM_Bld_Lighthouse_01'),
  mapFlats: apocalypseAt('SM_Bld_Apartment_01'),
  mapMotel: apocalypseAt('SM_Bld_Motel_01'),
  mapCloud: at('construction', 'SM_Generic_Cloud_01'),
  mapCloud2: at('construction', 'SM_Generic_Cloud_02'),
  mapCloud3: at('construction', 'SM_Generic_Cloud_03'),
  mapMine: at('construction', 'SM_Prop_Pipe_Concrete_Huge_01'),
  // Its dead cities, towns and farms, roads, bridges and woods (the second pass, «el mapa del valle es pobre»).
  mapHouseA: apocalypseAt('SM_Bld_House_01'),
  mapHouseC: apocalypseAt('SM_Bld_House_03'),
  mapRuin: apocalypseAt('SM_Bld_HighRise_Medium_01_Stack_Destroyed_01'),
  mapShopL: apocalypseAt('SM_Bld_Shop_Large_01'),
  mapShopM: apocalypseAt('SM_Bld_Shop_Medium_01'),
  mapShopS: apocalypseAt('SM_Bld_Shop_Small_02'),
  mapBlockM: apocalypseAt('SM_Bld_Commercial_Medium_01'),
  mapTower: apocalypseAt('SM_Bld_Commercial_Large_01'),
  mapMarket: apocalypseAt('SM_Bld_Market_Medium_01'),
  mapDiner: apocalypseAt('SM_Bld_Diner_01'),
  mapGarage: apocalypseAt('SM_Bld_AutoRepair_01'),
  mapBrickShed: apocalypseAt('SM_Bld_Warehouse_Brick_01'),
  mapWorks: apocalypseAt('SM_Bld_Industrial_Small_01'),
  mapTrailer: apocalypseAt('SM_Bld_Trailer_Damaged_01'),
  mapTankTower: apocalypseAt('SM_Bld_WaterTower_01'),
  mapCrane: apocalypseAt('SM_Bld_Crane_01'),
  mapBridge: apocalypseAt('SM_Env_Bridge_Clean_01'),
  mapWreckVan: apocalypseAt('SM_Prop_Car_Wrecked_Van_01'),
  mapWreckSuv: apocalypseAt('SM_Prop_Car_Wrecked_SUV_01'),
  mapWreckBus: apocalypseAt('SM_Prop_Car_Wrecked_Bus_01'),
  mapSign: apocalypseAt('SM_Prop_Sign_01'),
  mapSignStop: apocalypseAt('SM_Prop_Sign_Stop_01'),
  mapBarrier: apocalypseAt('SM_Prop_Barrier_Plastic_01'),
  mapHeli: apocalypseAt('SM_Prop_Helicopter_Crashed_01'),
  mapTreeA: apocalypseAt('SM_Env_Tree_01'),
  mapTreeBig: apocalypseAt('SM_Env_Tree_Large_01'),
  mapGrove: apocalypseAt('SM_Env_Tree_Cluster_01'),
  mapPines: apocalypseAt('SM_Env_Tree_Pine_Cluster_01'),
  mapTreeDead: apocalypseAt('SM_Env_Tree_Dead_01'),
  mapBushes: apocalypseAt('SM_Env_Bushes_01'),
  mapFence: at('farm', 'SM_Prop_Fence_Wood_01'),
  mapHay: at('farm', 'SM_Prop_Hay_Bale_Round_Plastic_01'),
  mapWindmill: at('farm', 'SM_Prop_Windmill_01'),
  mapFarmShed: at('farm', 'SM_Bld_Garage_01'),
} satisfies Record<string, BaseAsset>;

export type BaseModelId = keyof typeof BASE_MODELS;

/** The base's tiling ground (snow), converted to WebP at `size` in public/models/synty/base/. */
export const BASE_TEXTURES = {
  snowGround: { src: 'POLYGON_NatureBiomes_AlpineMountain_SourceFiles_v3/Textures/Snow_01.png', size: 512 },
} satisfies Record<string, { src: string; size: number }>;

export type BaseTextureId = keyof typeof BASE_TEXTURES;

export const baseTextureFile = (id: BaseTextureId) => `models/synty/base/${id}.webp`;
