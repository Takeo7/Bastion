# El sonido en Unity

Lo lleva Red (hito 3, docs/UNITY.md §5). El objetivo es que suene igual que la 0.10.1.

## 1. De dónde sale

Todo se genera a partir del sonido de la 0.10.1, que está en git:
- `packages/client/src/audio/`: las recetas sintetizadas (`sounds.ts`, `synth.ts`, `bank.ts`), la partitura (`music.ts`) y las camas de ambiente (`ambience.ts`);
- `packages/client/public/audio/`: los grabados (Opus, con su `index.json`) y los WAV de la interfaz.

Lo generado va a `unity/Bastion/Assets/Bastion/Client/Audio/Resources/Generado/`, que **no está en git**: son unos 580 MB de WAV. Unity los comprime al compilar la app.

| Carpeta | Qué | Cómo |
|---|---|---|
| `sintetizados/` | las 110 recetas de `sounds.ts`, con todas sus variantes, y los bucles de las que lo son (la sirena) | renderizadas como `bank.ts`: misma semilla por variante, misma normalización; mono, 44,1 kHz |
| `vivos/` | la partitura de la base y los menús (`musica.base`) y del Mapa (`musica.geo`), 8 min cada una; las cinco camas de ambiente (ciudad, campo, nave, base, globo), 4 min cada una | `music.ts` y `ambience.ts` tal cual, en un OfflineAudioContext con su reloj y sus temporizadores simulados y `Math.random` con semilla. Se quita el fundido de entrada y el final se funde en el principio (6 s, igual potencia): bucles sin costura. Estéreo, 44,1 kHz |
| `grabados/` | los 12 grupos grabados, con su nivel por toma | de Opus a WAV con ffmpeg, sin tocar nada más; 48 kHz, las dos camas de combate en estéreo |
| `interfaz/` | los 8 sonidos de interfaz | copiados |
| `manifiesto.json` | qué es cada cosa (bus, nivel, reverb, bucle, variantes) y el sha256 de cada archivo | |

En combate la partitura no suena: la sustituyen las camas grabadas, como en la 0.10.1. Por eso solo se renderizan `base` y `geo`.

## 2. Generarlo

```bash
unity/tools/sonido.sh
```

- Necesita Node, el `node_modules` de la raíz (esbuild y el Electron de `packages/desktop`) y el ffmpeg de Homebrew. No descarga nada.
- Tarda unos 40 s en el M2 Pro.
- `--salida <carpeta>` lo deja en otra carpeta, y `--comprobar` lo genera otra vez en una carpeta temporal y lo compara con la de ahora.
- La primera importación en Unity es larga (cientos de MB): se avisa a los demás antes, y se hace con el cerrojo de `unity/tools/unity.sh`.

## 3. Reproducible, con un bit de margen

- Los grabados y los de interfaz salen idénticos cada vez: `--comprobar` los compara byte a byte.
- Lo que renderiza Chromium (las recetas, la partitura y las camas) varía como mucho **1 LSB** (−90 dBFS) de una pasada a otra, en un tercio de los archivos. Es el redondeo de su render offline, no azar: no se puede evitar desde fuera. La 0.10.1 ya era así: cada partida renderizaba su banco con estas mismas diferencias. `--comprobar` compara esos archivos muestra a muestra y falla si alguno se aparta más de 1 LSB.
- El manifiesto lleva el hash de lo que hay en la carpeta. AppBuild lo usa para saber que `Generado/` está entero y es el que se generó.

## 4. Cuando se retire packages/

`sonido.sh` depende de `packages/client/src/audio` y `packages/client/public/audio`. Al retirar packages/ tras las releases de Unity, esos dos se quedan, o se mueven a `unity/tools/sonido/` con las rutas de `generar.mjs` cambiadas.

## 5. La mezcla en Unity

Un mezclador propio en C# (`Assets/Bastion/Client/Audio/Mix/`), sin el AudioMixer de Unity. Mezcla como `engine.ts` en Chromium:
- **Los buses:** efectos, interfaz, radio, música y ambiente, cada uno con su parte de la mezcla (MIX de engine.ts) por su volumen. La radio sigue el volumen de la interfaz.
- **El duck:** baja la música en los momentos fuertes y mientras habla la radio (`duck`, `holdDuck`).
- **La reverb compartida:** la misma convolución (2,4 s, decay 2,8), normalizada como la normaliza Chromium. Cada bus manda a la reverb según su volumen.
- **El compresor:** el de Chromium (`DynamicsCompressorKernel`), portado tal cual: 6 ms de anticipación, la curva de la rodilla, el release adaptativo y la ganancia de compensación.
- **Las voces:** como las toca Chromium:
  - la velocidad se lee cada 128 muestras;
  - interpolación lineal;
  - el inicio se redondea hacia arriba y se compensa la fracción de muestra;
  - al final, la última muestra se extrapola en línea recta;
  - un sonido mono sin paneo suena entero en los dos oídos; con paneo, a igual potencia.

Cómo suena en la app:
- **La salida:** `SoundPlayer` crea al arrancar un objeto «Sonido» que no se destruye entre escenas. Lleva el único AudioListener (apaga los de las escenas) y la mezcla en su filtro. Todo se coloca por paneo, como en la 0.10.1.
- **Los efectos** (las recetas, los grabados cortos y la interfaz) se leen una vez a memoria en 16 bits. Se van leyendo poco a poco al arrancar, los más usados primero, como el `prerender` de bank.ts.
- **La música, los ambientes y las camas de combate** se leen del disco mientras suenan. Cada uno tiene un AudioSource con un `Tap` que le pasa su sonido al mezclador.
- **En Unity**, los efectos van en Vorbis descomprimido al cargar, y la música y las camas en streaming (`SoundImport`). En la app ocupan 41 MB.
- **AppBuild se para** si `Generado/` no está entero o no es el que se generó (los hashes del manifiesto). `-sinSonido` compila sin comprobarlo.

## 6. Igual que la 0.10.1

```bash
node unity/tools/sonido/mezcla.mjs
```

Toca los guiones de `unity/tools/sonido/guiones/` (sonidos, paradas, la sirena en bucle, duck, Morse, música, volúmenes, silencio) con `engine.ts` en Chromium, offline (`mezcla.ts`), y con el mezclador de Unity en dotnet (`mezcla/`). Usan los mismos archivos y los mismos números aleatorios, y compara medio segundo a medio segundo. El 26/10:

| Guion | La diferencia, por debajo del sonido |
|---|---|
| todo (12 s, bloques de 128) | 81 dB |
| bloques-1024 (los bloques de Unity) | 81 dB |
| retrasos (inicios entre dos muestras) | 102 dB |
| a-44100 | 78 dB |

- Lo que más se aparta es el Morse: Chromium hace el seno con una tabla.
- A 44,1 kHz, el render de Chromium del guion completo se rompe pasados 5,9 s (valores enormes y NaN, en las dos pasadas). Por eso `a-44100` dura 5,5 s.

**La prueba en la app:** `BASTION_SONIDO_GUION=<guion.json>` toca un guion en silencio y guarda lo que sale en `BASTION_DATOS/sonido.f32`, con lo que costó la mezcla, y la app se cierra. Va solo en 127.0.0.1, sin cuadros de macOS.
- Con `BASTION_SONIDO_WAV=<Generado>`, lee los WAV: es solo la mezcla.
- Con `BASTION_SONIDO_CLIPS=1`, lee los clips del juego, en Vorbis.

En el M2 Pro, a 48 kHz y en bloques de 1024:
- **Con WAV:** igual que Chromium, a 83 dB.
- **Con Vorbis:** la forma de onda se aparta (hasta −23 dB en medio segundo con disparos). En nivel y en espectro queda igual:
  - menos de 0,03 dB por banda hasta 6 kHz;
  - −0,2 dB entre 6 y 12 kHz;
  - −0,6 dB por encima de 12 kHz;
  - 0,05 dB como mucho de nivel por medio segundo.

## 7. Cómo se llama desde el juego

En el namespace `Bastion.Client.Audio`, con los nombres de la 0.10.1:

| Unity | 0.10.1 |
|---|---|
| `Sound.Play("shot.rifle.1", gain, rate, pan, delay, reverb, spread, bus)` | `sound()` de bank.ts |
| `Sound.Recorded("zombie.growl", …)` y `Sound.HasRecorded(…)` | `playRecorded()` y `hasRecorded()` |
| `Sound.Ui(UiKind.Key…)` | `uiSound()` |
| `Sound.Loop("siren.police", gain, reverb)` → `.Set(nivel, sitio, apagado)` y `.Stop(windDown)` | `audio.loop()` sobre `loopBuffer()` |
| `Sound.Duck(…)`, `Sound.HoldDuck()`, `Sound.Morse(…)` | `audio.duck()`, `audio.holdDuck()`, `morse()` |
| `Music.Play("base" \| "geo" \| "calm" \| "tense" \| "alien" \| null, fade)` | `music.play()` |
| `Ambience.Play("city" \| "wilds" \| "facility" \| "base" \| "geo" \| null, fade)` | `ambience.play()` |

Con el sonido en silencio (M) no suena nada nuevo, como con `live()`. Los volúmenes los lee de `Volumes`.

La interfaz va por `UiSounds`:
- **`Install`:** los clics y las teclas de todo el panel, como `installUiSounds`, con las clases de Unity. Las pestañas de la base (`strat-tab`, `armory-tab`) dan página; `btn-primary` y `squad-launch` dan confirmar; un botón desactivado da error. Las celdas del tablero llevan sus propios sonidos.
- **`Play(nombre)`:** lo que piden las pantallas por `HudRoot.Sound` y `StrategyRoot.Sound`:
  - `stamp`, el cartel (0,55), y `stamp.outcome`, el parte (0,25 s después);
  - `paper`, `confirm`, `bell`;
  - las celdas: `cellHover` y `cellBuy`; `cellHold` suena hasta que se suelta (`cellHold.stop`) o se compra (`cellHold.done`);
  - la radio: `radio.open`, `radio.static`, `radio.key`, `radio.bells`, `radio.morse`, `radio.close`, `radio.end`, con la música abajo mientras habla;
  - `notice`, el aviso del compañero;
  - `tick`, un día que pasa.
- **`Lobby()`:** la música del inicio y de la sala.
- **`View(vista)`:** la música y el ambiente de la base, o del Mapa.

El sonido del combate es `CombatSound`, el `Sfx` de `game/sfx.ts` con los mismos métodos (Shot, Impact, Melee, Explosion, Step, Hurt, Death, Contact, Turn, Outcome, Start, Mood, Siren…):
- cada sonido se coloca por su sitio en pantalla y su distancia al punto que mira la cámara, en casillas como en la 0.10.1 (`Attach` recibe `MapSpace.Tile`);
- el gruñido de los muertos al andar;
- la sirena del sabotaje;
- la música según el momento del combate;
- el ambiente del bioma.

Lo llaman el EventPlayer y el CombatController de UI, en los mismos momentos en que `game.ts` llamaba a `sfx`.

**Al arrancar** se leen los 399 efectos en unos 4 s, a unos 4 ms por fotograma: primero los de la interfaz y luego los más comunes. La pantalla de inicio se puede usar mientras tanto; la lectura más lenta es de 11 ms. Un sonido pedido antes de su turno se lee en ese momento y suena.

## 8. Lo que cuesta

La mezcla va en el hilo de audio, no en el del juego. En la app, en el M2 Pro:
- **De media:** 1,1 ms por bloque de 21,3 ms, el 5 % de ese hilo.
- **Como mucho:** 6–7 ms, cuando la cola de la reverb calcula su bloque de 4096.

La reverb va partida: los primeros 4096 frames en bloques de 512 y el resto en bloques de 4096, calculados un bloque antes. Hecha toda en bloques de 512, la mezcla se llevaba el 23 %.

## 9. Los enganches

Dónde llamaba la 0.10.1 a cada sonido y dónde va en Unity: una línea en cada sitio, en archivos de UI (y uno de Arte). Las líneas son las del 26/10. **Puestos el 26/10**, todos: los del combate, los de fuera del combate y los pasos. Los pasos usan el `onStep` que Arte añadió a `UnitViews.MoveAlong`. EP es `UI/Scripts/Combat/EventPlayer.cs`.
- **Ayudas de EP:**
  - `Feet(id)`, `Chest(id)`, `Head(id)`;
  - `units.Muzzle(id)`, `units.Hand(id)`, `units.ThrowHand(id)`;
  - `Board.TileToWorld(v, pos, metros)`.
- **Alturas:** 1 casilla de la 0.10.1 son 1,5 m.
- **La instancia:** `CombatSound` va en un campo del CombatController (`Sfx`), que EP alcanza por `control`.

**El combate: inicio y final**

| 0.10.1 | Unity | Llamada |
|---|---|---|
| game.ts:892 attach | CombatSession.Start, tras `control.Rig = rig` | `Sfx.Attach(cam, () => rig.Target, MapSpace.Tile, (s, p) => MapSpace.TileToWorld(s, p, MapSpace.Tile))` |
| game.ts:1036 start | CombatSession.Load, tras `player.Load(state)` | `if (state.Outcome == null) Sfx.Start(state.Biome, !state.Concealed)` |
| game.ts:1037 siren | ídem | `Sfx.Siren(() => control.View)` |
| game.ts:1302 siren(null) | CombatSession.OnDestroy | `Sfx.Siren(null)` |
| game.ts:1239 mark | CombatController.Plans `OnPing`, tras `AimMarks?.Ping` | `Sfx.Mark(at)` |
| M y el menú | ya hecho (`Volumes.ToggleMute`) | — |

**Los hechos (EP)**

| 0.10.1 | Hecho, momento | EP | Llamada |
|---|---|---|---|
| 1918 step | `moved`, uno de cada dos pasos acabados | `MoveAlong` no avisa paso a paso: hace falta un `onStep` en `UnitViews.Walk` (Arte) | `Sfx.Step(u, pie)` |
| 1965 hurt | `damaged`, uno de los nuestros, sin morir | tras el `Float` del daño (960) | `if (d.Amount > 0 && target.Team == Teams.Xcom && target.Hp > d.Amount) Sfx.Hurt(target, chest, !bite && from != null && Content.Templates[from.Template].Undead)` |
| 1968 impact armor | `damaged` con blindaje | con el `Float` de BLINDAJE (961) | `Sfx.Impact("armor", chest)` |
| 1993 death | `died`, visto, antes de caer | tras la cámara de la baja (984) | `Sfx.Death(u, Chest(u.Id), killer)` |
| 2024 stun | `stunned`, con las chispas | tras `fx?.Sparks` (1017) | `Sfx.Stun(Chest(u.Id))` |
| 2050 stoneThrow | la piedra sale de la mano | tras `units.TossStone` (1044) | `Sfx.StoneThrow(hand)` |
| 2052 stoneLand | la piedra cae | tras `fx.Stone` (1045) | `Sfx.StoneLand(to, struck != null)` |
| 2063/2064 | hunker / overwatch, con su rótulo | tras el `Float` (1056) | `if (e is HunkerEvent) Sfx.Hunker(Feet(u.Id)); else Sfx.Overwatch(Chest(u.Id))` |
| 2078 psi | grito (mindspin) | tras «¡GRITA!» (1074) | `Sfx.Psi(Chest(u.Id))` |
| 2115 shot | `suppressed`, cada destello | tras `fx.MuzzleFlash` (1117) | `Sfx.Shot(u, Content.AttackWeapon(u).Id, from)` |
| 2182/2183 | `reanimated` | tras la espera (1182) | `if (u != null) Sfx.Psi(Chest(u.Id)); Sfx.Moan(place)` |
| 2212 reload | `reload` | tras «RECARGANDO» (1212) | `Sfx.Reload(Chest(u.Id))` |
| 2228/2238 heal | `healed` | tras `fx?.Heal` (1233) y tras la mirada del médico (1243) | `Sfx.Heal(Chest(target.Id))` |
| 2243 shield | `aided` | tras `fx?.Shield` (1250) | `Sfx.Shield(Chest(target.Id))` |
| 2255/2258 | `roomSearched` | tras `Look` (1261); y con el hallazgo (1264) | `Sfx.Search(place)`; `if (room.Loot != null) Sfx.Found(place)` |
| 2330 reload | `weaponEquipped` | antes del `Wait(1f)` (1345) | `Sfx.Reload(Feet(u.Id))` |
| 2427 data | `itemPicked` | tras el rótulo (1445) | `Sfx.Data()` |
| 2457 smash | `smashed`, visto | tras `fx?.Smash` (1473) | `Sfx.Smash(place, door ? "door" : "barricade", gone)` |
| 2473 door | puerta abierta, casilla visible | 1491 | `Sfx.Door(Board.TileToWorld(v, tc.Pos, Board.Tile))` |
| 2480 glass | ventana rota, visible | tras `fx.Debris` (1494) | `Sfx.Glass(Board.TileToWorld(v, tc.Pos, 1.95f))` |
| 2504/2509 | `hacked` | antes de las chispas (1519); tras el registro (1522) | `Sfx.Hack(screen)`; `Sfx.Hacked(hack.Success, screen)` |
| 2522 rescue | `vipRescued` | tras el registro (1537) | `Sfx.Rescue()` |
| 2546 flare | refuerzos sin horda | tras el registro (1558) | `Sfx.Flare(Board.TileToWorld(v, ri.R.Pos))` |
| 2629 land | `fell`, al tocar suelo | tras `units.Fall` (1592) | `Sfx.Land(Board.TileToWorld(v, fell.To))` |
| 2638/2642 | `searched` | tras `Look` (1601); con el hallazgo (1604) | `Sfx.Search(place)`; `if (searched.Loot != null) Sfx.Found(place)` |
| 2676 contact | `podActivated` | `PlayContact`, tras los «!» (1960) | `Sfx.Contact(centre, members.Any(m => Content.Templates[m.Template].Undead))` |
| 2686 reveal | `concealmentBroken` | tras `exposure = cb` (1614) | `Sfx.Reveal()` |
| 2711 turn | `turnBegan` | antes del cartel (1644) | `Sfx.Turn(turn.Team == Teams.Alien, !v.Concealed)` |
| 2730 command | `commandPassed` | al empezar (1658) | `Sfx.Command()` |
| 2741 outcome | `missionEnded` | antes del `Wait(0.8f)` (1671) | `Sfx.Outcome(end.Outcome == "victory")` |
| 2781 alert | disparo de reacción | con su `Float` (1721) | `Sfx.Alert()` |
| 2786 melee swing | ataque cuerpo a cuerpo | tras la espera (1732) | `Sfx.Melee("swing", Chest(shooter.Id))` |
| 2794/2795 | el golpe acierta | en el `onHit` del `Lunge`, tras `fx.Slash` (1737) | `Sfx.Melee(stun ? "stun" : e.Ability == AbilityIds.Claw ? "claw" : "sword", chest); Sfx.Impact(CombatSound.Body(target), chest, e.Result == "crit")` |
| 2821 shot | cada ronda, con el destello | tras `fx.MuzzleFlash` (1763) | `Sfx.Shot(shooter, weapon.Id, muzzle)` |
| 2829 impact | la última ronda, primer perdigón, al llegar | la bala de 1769 con `Then(...)` | `Sfx.Impact(hit ? CombatSound.Body(target) : "miss", dest, e.Result == "crit")` |
| 2963 explosion | `explosive` (Hinchado) | tras el registro (1822) | `Sfx.Explosion(to)` |
| 2984 missile | misiles | antes de `fx.Missiles` (1851) | `Sfx.Missile(from)` |
| 2991/2993 | lanzagranadas | con el destello (1858); al caer (1859) | `Sfx.Launch(from)`; `Sfx.Land(to)` |
| 2996/2999 | lanzar | al empezar el gesto (1865); al caer (1867) | `Sfx.Throw(Chest(u.Id))`; `Sfx.Land(to)` |
| 3002/3008/3015 | humo, aturdidora, fragmentación | antes de `fx.Puffs` (1871); 1877; tras el radio (1883) | `Sfx.Explosion(to, "smoke" / "flash" / "frag")` |
| 4749 vehicle | La Lata se va | `LataLeaves`, antes de `LataDeparts` (614) | `if (map?.LataAt is Vector3 at) Sfx.Vehicle(at)` |
| arrivals.ts | llegadas: gemido de los muertos, motor de los vivos | Unity no anima las llegadas: en `PlaySpawned` (1918) | `Sfx.Moan(…)` si llegan muertos; `Sfx.Vehicle(…)` si vivos |

- **No van:** los cuatro de `rtAttack` (game.ts:2896–2914), porque Unity no tiene el modo en tiempo real.
- **Las ramas sin figuras** (el laboratorio) no suenan, igual que no dibujan.

**Fuera del combate (UI)**

- **Pulsar** (ui.ts `installUiSounds`):
  - un botón: `Sound.Ui(UiKind.Key, spread: 0.08f)`;
  - una pestaña: `UiKind.Page`;
  - el botón principal, uno grande o «fuego»: `UiKind.Confirm`;
  - uno desactivado: `UiKind.Error`.
- **Escribir:** Intro es `Bell`; espacio, `Space`; una tecla o borrar, `Key` con spread 0,1.
- **El HUD** (hud.ts):
  - un cartel: `Ui(Stamp, 0.55f)`;
  - cerrar el cartel o la tarjeta de llegada: `Confirm`;
  - volver a abrir la tarjeta, el menú Esc o un diálogo de confirmación: `Paper`;
  - el resultado de la misión: `Stamp`, con 0,25 s de retraso (hud.ts:1660);
  - el aviso de un momento del compañero («Ver (F)»): `Sound.Play("radio.static", 0.6f)`;
  - una llamada con respuesta (la evacuación, abandonar…): `Ui(Bell)`, una vez por llamada.
- **La base y los menús:**
  - entrar en la sala o en el inicio: `Music.Play("base"); Ambience.Play(null)` (main.ts:85);
  - en la base, el Mapa pone `Music.Play("geo"); Ambience.Play("geo")` y lo demás, `"base"` en los dos (stage.ts:215);
  - el compañero dice «a punto»: `Ui(Bell)`;
  - pasa un día: `Sound.Play("ui.tick")`;
  - se abre un modal: `Ui(Paper)`.
- **El tablero de habilidades** (hexBoard.ts):
  - mantener pulsado: `var stop = Sound.Ui(UiKind.CellHold)`, y se llama a `stop()` al soltar antes de comprar;
  - comprar: `CellBuy`;
  - pasar por encima: `CellHover`.
- **La radio** (transmissions.ts RadioSound):
  - al abrir: `Sound.Play("radio.static")` y, una sola vez, `release = Sound.HoldDuck(0.4f)`;
  - cada cambio de voz: `radio.static`;
  - unas letras sí y otras no: `Sound.Play("radio.key", spread: 0.15f)`;
  - bajo la voz de la Congregación: `Sound.Play("radio.bells", 0.35f)`;
  - la primera línea de la operadora: `Sound.Morse()`;
  - al cerrar: `radio.close` y `release()`.

