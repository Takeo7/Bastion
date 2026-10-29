# Sonidos grabados

Sonidos y música de fuera del juego: solo CC0 (dominio público, sin atribución obligatoria), de Freesound y OpenGameArt (Arturo, 22/10). Cada archivo, con su origen. Lo demás del juego se hace en código (`client/src/audio/`), y la interfaz usa grabaciones de Kenney (CC0, `public/audio/ui/`).

- **Originales:** fuera del repositorio, en `assets-src/sonido/` (no se sube, como los de Synty).
- **En el repositorio:** solo lo recortado y convertido, en `packages/client/public/audio/`.
- **De Freesound:** se usa la previsualización de alta calidad (OGG), que se descarga sin cuenta. La licencia CC0 cubre la obra entera, previsualización incluida.

**Estado: en el juego (22/10).** Licencias comprobadas una a una en la página de cada sonido; los 37 originales, bajados solo de estas direcciones y comprobados por duración frente a su página.

## Cómo se rehace

```bash
node packages/client/tools/sound/convert.mjs --bajar
```

Sin `--bajar`, solo convierte lo que ya esté en `assets-src/sonido/`. El script ([convert.mjs](../packages/client/tools/sound/convert.mjs)) hace esto:

- separa cada grabación en tomas por sus silencios;
- las pasa a mono, con un volumen parejo y fundidas en los extremos;
- deja los fondos enteros en estéreo, más bajos;
- convierte todo a Opus en OGG: 64 kbps (el ffmpeg de Homebrew no trae libvorbis, y Chromium lee Opus);
- escribe `public/audio/recorded/index.json`, que lee el juego (`client/src/audio/recorded.ts`).

## Qué suena dónde

| Grupo | Cuándo | Tomas | Peso |
|---|---|---|---|
| `zombie.growl` | un muerto andando, a la vista: como mucho uno cada 4 s, el más cercano a donde mira la cámara, la mitad de las veces | 11 | 204 KB |
| `zombie.alert` | los muertos ven a la escuadra (con el golpe de tensión) | 4 | 92 KB |
| `zombie.attack`, `zombie.bite` | la zarpa de un muerto acierta | 2 y 6 | 104 KB |
| `zombie.eat` | uno de los nuestros muere a manos de un muerto (no de la infección) | 2 | 88 KB |
| `soldier.scream.m/f` | mordido, o muere; según su sexo | 3 y 4 | 104 KB |
| `soldier.pain.m` | herido sin mordisco (las mujeres, un grito más bajo) | 2 | 12 KB |
| `melee.blade` | cuchillo o machete que acierta | 8 | 72 KB |
| `sting` | contacto | 2 | 72 KB |
| `bed.ambience` | fondo del combate: oculta, a 0,8; tras el contacto, a 0,6; en el turno enemigo, a 0,3 | 4 | 5,4 MB |
| `bed.tension` | tras el contacto, a 0,6; en el turno enemigo, a 0,85 | 3 | 2,1 MB |

Ninguna toma se repite dos veces seguidas; cada fondo se elige al azar entre los de su grupo. La base, el valle y el menú siguen con la música de siempre. Para ver qué ha sonado, con `?debug`: `window.__recorded`.

## Para qué (notas de Ana del 22/10)

- **#20:** el soldado grita cuando un zombi le muerde, y cuando muere devorado.
- **#23:** en combate, más ambiente y tensión que melodía (como *The Walking Dead: Saints & Sinners* o el primer *Tomb Raider*).
- **#24:** zombis que se oyen: gruñidos al andar, al ver a alguien y al morder. El oyente ya está donde mira la cámara (`sfx.ts`, `rig.focusPoint`).
- **#53:** el navajazo sonaba a campana. La receta en código ya no lleva metal; con estas grabaciones, tajo y carne de verdad.

## Zombis

| Uso | Sonido | Autor | Duración | Tamaño |
|---|---|---|---|---|
| Gruñido | [zombie growl](https://freesound.org/people/adharca/sounds/323819/) | adharca | 4,2 s | 26 KB |
| Gruñido | [Zombie Growl 5](https://freesound.org/people/tonsil5/sounds/555417/) | tonsil5 | 2,6 s | 35 KB |
| Gruñido | [Zombie Growl 02](https://freesound.org/people/Ezcah/sounds/204820/) | Ezcah | 3,5 s | 44 KB |
| Gruñidos (varios, para recortar) | [zombie-growls](https://freesound.org/people/Xfrtrex/sounds/769200/) | Xfrtrex | 31 s | 559 KB |
| Gorgoteo al morir | [Infected zombie man dying growl](https://freesound.org/people/spootyskater/sounds/612699/) | spootyskater | 14 s | 256 KB |
| Al ver a alguien | [Zombie Demon Scream](https://freesound.org/people/Wakerone/sounds/435651/) | Wakerone | 2,2 s | 49 KB |
| Al ver a alguien | [Monster Zombie Scream](https://freesound.org/people/lolamadeus/sounds/179345/) | lolamadeus | 2,7 s | 65 KB |
| Al ver a alguien | [ZAMN Scream](https://freesound.org/people/Beetlemuse/sounds/703317/) | Beetlemuse | 2,7 s | 55 KB |
| Al ver a alguien | [snarl](https://freesound.org/people/SmallQuadruped/sounds/42201/) | SmallQuadruped | 4,1 s | 49 KB |
| Ataque | [Zombie Attack](https://freesound.org/people/soykevin/sounds/213509/) | soykevin | 8 s | 149 KB |
| Mordisco | [Zombie Flesh Bites](https://freesound.org/people/MrPokephile/sounds/155973/) | MrPokephile | 14 s | 330 KB |
| Mordisco | [Biting on bones](https://freesound.org/people/Breviceps/sounds/445987/) | Breviceps | 0,6 s | 19 KB |
| Mordisco | [monster bite](https://freesound.org/people/LucasDuff/sounds/467701/) | LucasDuff | 2 s | 44 KB |
| Devorando | [Zombies eating](https://freesound.org/people/Breviceps/sounds/448089/) | Breviceps | 5,8 s | 172 KB |
| Devorando | [ZombieEatingFlesh](https://freesound.org/people/Sclolex/sounds/236331/) | Sclolex | 4,6 s | 128 KB |

## Soldados

| Uso | Sonido | Autor | Duración | Tamaño |
|---|---|---|---|---|
| Grito, hombre | [Wounded man scream](https://freesound.org/people/joseppujol/sounds/221544/) | joseppujol | 2 s | 27 KB |
| Grito, hombre | [Man screaming being murdered](https://freesound.org/people/EricSueiro/sounds/220819/) | EricSueiro | 1 s | 27 KB |
| Grito, hombre | [Victim Screaming](https://freesound.org/people/qubodup/sounds/813310/) | qubodup | 0,9 s | 14 KB |
| Dolor, hombre | [Grunt1 - Death Pain](https://freesound.org/people/tonsil5/sounds/416839/) | tonsil5 | 1 s | 14 KB |
| Dolor, hombre | [Grunt2 - Death Pain](https://freesound.org/people/tonsil5/sounds/416838/) | tonsil5 | 1,5 s | 19 KB |
| Grito, mujer | [Pain scream girl](https://freesound.org/people/annatabernero/sounds/219659/) | annatabernero | 2 s | 24 KB |
| Grito, mujer | [Woman Screaming](https://freesound.org/people/JalynCatbtg/sounds/618193/) | JalynCatbtg | 1,7 s | 42 KB |
| Grito, mujer | [Woman Scream](https://freesound.org/people/courtneyeck/sounds/673306/) | courtneyeck | 4,5 s | 75 KB |
| Grito, mujer | [Woman Screaming](https://freesound.org/people/DanJFilms/sounds/845164/) | DanJFilms | 3,6 s | 44 KB |

## Cuerpo a cuerpo (cuchillo y machete)

Tajo y carne, sin metal que resuene. Para el machete no sirven los golpes de «machete» de Freesound, que son contra metal; el tajo en un melón es el de siempre en el cine para un golpe en un cuerpo.

| Uso | Sonido | Autor | Duración | Tamaño |
|---|---|---|---|---|
| Tajo | [Slicing through flesh](https://freesound.org/people/NeoSpica/sounds/504615/) | NeoSpica | 0,9 s | 28 KB |
| Tajo | [slashkut](https://freesound.org/people/Abyssmal/sounds/35213/) | Abyssmal | 0,5 s | 14 KB |
| Navajazo (varios, para recortar) | [STABBING STAB KNIFE](https://freesound.org/people/Podcapocalipsis/sounds/521030/) | Podcapocalipsis | 8,5 s | 172 KB |
| Machetazo (varios, para recortar) | [melon-chop-take1](https://freesound.org/people/gagglepod/sounds/539535/) | gagglepod | 16,8 s | 265 KB |

## Ambiente y tensión (en lugar de la música melódica en combate)

| Uso | Pieza | Autor | Origen | Duración | Tamaño |
|---|---|---|---|---|---|
| Fondo del combate (bucle) | [Post Apocalyptic Wastelands (Loop Ready)](https://opengameart.org/content/horror-atmosphere) | Juhani Junkala | OpenGameArt, CC0 | 5:23 | 14,2 MB |
| Fondo de la ciudad | [EmptyCity](https://opengameart.org/content/emptycity-background-music) | yd | OpenGameArt, CC0 | — | 2 MB |
| Tensión tras el contacto | [Bleeding out](https://opengameart.org/content/bleeding-out) | Brandon Morris | OpenGameArt, OGA-BY 3.0 o CC0 (se toma CC0) | — | 1,9 MB |
| Capa de tensión | [Dark Tension Drone](https://freesound.org/people/HarmonicMess/sounds/826597/) | HarmonicMess | Freesound, CC0 | 19 s | 318 KB |
| Capa de tensión | [TRUBBLE - Horror Tension Drone](https://freesound.org/people/zawert/sounds/871722/) | zawert | Freesound, CC0 | 89 s | 1,2 MB |
| Viento | [Dystopian Winds](https://freesound.org/people/Alien_I_Trust/sounds/785818/) | Alien_I_Trust | Freesound, CC0 | 69 s | 1,3 MB |
| Edificio abandonado | [abandoned building ext (smoketown)](https://freesound.org/people/cyntrix/sounds/686762/) | cyntrix | Freesound, CC0 | 5:01 | 6,7 MB |
| Golpe al contacto | [HorrorSting1](https://freesound.org/people/shelbyshark/sounds/513332/) | shelbyshark | Freesound, CC0 | 27 s | 484 KB |
| Golpe al contacto | [Plucked Horror Stinger](https://freesound.org/people/zombieallen/sounds/725225/) | zombieallen | Freesound, CC0 | 7 s | 129 KB |

Descartadas por licencia (salían en la búsqueda de CC0 de OpenGameArt, pero piden atribución o compartir igual): *Dark ambiance* (CC-BY-SA), *Dark Fallout* (CC-BY-SA), *Post Apocalypse* de Alexandr Zhelanov (CC-BY), *Desolate* de Matthew Pablo (CC-BY), *Horror Soundtrack: The Hangar* (CC-BY).

**En total:** 37 originales, 33 MB, en `assets-src/sonido/`. En el repositorio, 51 tomas y fondos: 8,1 MB, en `public/audio/recorded/`.

## Desde la 0.10.1: los de Unity (29/10)

Con `packages/` congelado, los sonidos grabados nuevos van por la parte de Unity:
- **La herramienta:** `unity/tools/sonido/grabados.mjs`, que recorta igual que `convert.mjs`.
- **Lo recortado:** `unity/tools/sonido/grabados/`, en git, con su `index.json`.
- **En el juego:** `generar.mjs` los añade a los de la 0.10.1 (docs/UNITY-SONIDO.md).

Los originales van a `assets-src/sonido/`, como siempre.

```bash
node unity/tools/sonido/grabados.mjs --bajar
```

Son para las notas de Ana del 29/10 (docs/PRUEBA-2026-10-29.md):
- **#1:** el Hinchado, con su voz, sus pasos y su muerte; hasta ahora usaba los del zombi común.
- **#3:** su estallido, más fuerte.
- **#10:** las armas cuerpo a cuerpo nuevas.

Arturo dio el «sí» a la descarga el 29/10. Cada licencia está comprobada en la página del sonido, y cada original, por su duración frente a la página.

| Grupo | Cuándo | Tomas |
|---|---|---|
| `bloater.voice` | el Hinchado andando, a la vista (como el gruñido del zombi); más grave (0,75) | 7 |
| `bloater.step` | sus pasos | 6 |
| `bloater.death` | su muerte, justo antes de reventar; algo más grave (0,85) | 3 |
| `bloater.burst` | la salpicadura, sobre la explosión (2 dB más fuerte y más grave que la de la granada) | 2 |
| `melee.blunt` | el golpe en el cuerpo de la sartén, el bate y la palanca | 6 |
| `melee.pan`, `melee.bat`, `melee.crowbar` | el toque de cada una, por encima (a 0,6, 0,5 y 0,6) | 2, 1 y 3 |
| `melee.chainsaw.rev` | la motosierra, al blandirla | 1 |
| `melee.chainsaw.cut` | la motosierra, al acertar | 5 |

| Uso | Sonido | Autor | Duración | Tamaño |
|---|---|---|---|---|
| Hinchado, voz | [Abomination Monster Vocalizations](https://freesound.org/people/marlonnnnnn/sounds/830859/) | marlonnnnnn | 13,5 s | 164 KB |
| Hinchado, voz | [CREAHmn_Zombie01](https://freesound.org/people/InMotionAudio/sounds/653581/) | InMotionAudio | 51 s | 955 KB |
| Hinchado, muerte | [horrible gurgling monster](https://freesound.org/people/Darsycho/sounds/653515/) | Darsycho | 54,6 s | 1,2 MB |
| Hinchado, pasos | [Walking on flesh and bones](https://freesound.org/people/DeathRowBot/sounds/676531/) | DeathRowBot | 14,4 s | 187 KB |
| Hinchado, salpicadura | [Splatter - body falling apart](https://freesound.org/people/Aegersum/sounds/344904/) | Aegersum | 2,2 s | 61 KB |
| Hinchado, salpicadura | [Gib sound](https://freesound.org/people/RoozyDB/sounds/504629/) | RoozyDB | 0,5 s | 9 KB |
| Golpe en el cuerpo | [Smacks And Thwacks (m)](https://freesound.org/people/BarryTheWhite/sounds/396345/) | BarryTheWhite | 37 s | 528 KB |
| Sartén | [Hit with frying pan](https://freesound.org/people/Whiprealgood/sounds/529567/) | Whiprealgood | 0,7 s | 17 KB |
| Sartén | [Frying Pan Hit](https://freesound.org/people/tbone1999/sounds/401706/) | tbone1999 | 0,4 s | 12 KB |
| Bate | [Hit by metal baseball bat](https://freesound.org/people/trader_one/sounds/684607/) | trader_one | 0,7 s | 13 KB |
| Palanca | [Crowbar](https://freesound.org/people/damsur/sounds/443243/) | damsur | 8,6 s | 184 KB |
| Motosierra, acelerón | [chainsaw start and idle](https://freesound.org/people/kyles/sounds/453259/) | kyles | 10,9 s | 314 KB |
| Motosierra, corte | [Chainsaw Cutting](https://freesound.org/people/lonemonk/sounds/185579/) | lonemonk | 1:37 | 2,0 MB |
| Motosierra, corte | [Small Chainsaw 4](https://freesound.org/people/wrinkledsack/sounds/810392/) | wrinkledsack | 2:02 | 1,4 MB |

Medido, sin poder oírlo:
- **El bate de aluminio** suena como una campana: 460 ms a 1,9 kHz. Solo se usan sus primeros 0,2 s, por debajo del golpe en el cuerpo.
- **La sartén** suena 190 ms, como los cuchillazos grabados.
- **La motosierra:** «Chainsaw Cutting» lleva una sirena de ambulancia al principio. Los cortes se toman pasado el segundo 46, cortando a todo gas, y en ellos no hay ningún tono sostenido.
- **Los golpes nuevos** quedan a ±2 dB de los del cuchillo en la mezcla de Unity.

Se bajaron también tres que se quedan fuera:
- [gore_gory_blood_smash_flesh_murder](https://freesound.org/people/EricsSoundschmiede/sounds/513309/) (EricsSoundschmiede, 3:24, 2,6 MB): sus tomas eran casi todo siseo, con el brillo en 7 kHz y la media en −32 dB.
- [Squelching Footsteps](https://freesound.org/people/Adielees9/sounds/369579/) (Adielees9, 11,5 s, 322 KB): está grabado a −50 dB.
- [Crowbar hits and dings](https://freesound.org/people/lreeve/sounds/862892/) (lreeve, 26 s, 383 KB): golpes de 0,05 s y tintineos que suenan a campana.

**En total:** 17 originales, 10,4 MB, en `assets-src/sonido/`. En el repositorio, 36 tomas: 552 KB, en `unity/tools/sonido/grabados/`.
