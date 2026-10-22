# Dirección de arte

Estado al 2026-10-14, con la versión 0.2.1. Lo lleva la sesión **Arte**, con los archivos de [EQUIPO.md](EQUIPO.md). La etapa anterior (laboratorio de estilos, vóxel, Expediente, Holotáctico, el Canto de Velmar) está en [archive/ARTE-historial.md](archive/ARTE-historial.md).

![El juego hoy: la ciudad de día y de noche, el campo, una instalación, la base al empezar y el valle](arte/juego-hoja.jpg)

## Normas

- **De dónde sale el arte:** solo Synty POLYGON (con la suscripción de Arturo), Kenney (CC0) o lo hecho por nosotros. Tipografías OFL, como mucho dos familias. Nada de otros packs, aunque sean gratuitos.
- **El aspecto objetivo** son las capturas promocionales de Synty, en `assets-src/referencias/aspecto-synty/`.
- **Licencia:**
  - los originales viven en `assets-src/synty/` y no se suben;
  - al repositorio solo van los modelos convertidos que usa el juego, y por eso `Takeo7/Bastion` es siempre privado.
- **Rendimiento:**
  - ≤4,2 ms por fotograma en «alto» en el PC de referencia (Windows, RTX 3090), y «medio» a ≥30 fps en el Mac;
  - el aspecto no se recorta por el Mac: para eso están los ajustes;
  - se mide el tiempo de fotograma, no los fps.
- **Ritmo:** cada acción se ve entera antes de pasar a la siguiente, y la cámara nunca pelea con el jugador.
- **Lo rechazado no se vuelve a proponer:** vóxel, Expediente, Holotáctico, Viñeta, CRT y el resto del laboratorio de estilos.

## Los packs

Todos en `assets-src/synty/`. Las rutas de cada uno están en `PACKS` de `syntyManifest.ts`.

| Pack | Para qué |
|---|---|
| POLYGON Apocalypse | Supervivientes, saqueadores, rescatados, dos de los muertos, las armas y casi todo el decorado de la calle |
| POLYGON City Zombies | Los muertos comunes, los corredores y los acorazados. Solo viene como `.unitypackage`: el conversor lo desempaqueta una vez |
| POLYGON Boss Zombies | El Chillón y el Hinchado |
| POLYGON Town, City, Farm, Nature, Construction y Military | Los mapas de combate: edificios modulares, calles, campo, obras y controles militares |
| POLYGON Office, y dos de City | Las voces de la radio (`strategy/portraits.ts`) |
| Nature Biomes – Alpine Mountain y POLYGON Snow | La base en la sierra |
| ANIMATION – Base Locomotion e Idles | Las animaciones de todos los personajes |
| ANIMATION – Bow Combat y Sword Combat | Las muertes (`DEATHS`) |
| ANIMATION – Goblin Locomotion | Cómo andan y corren los muertos (`DEAD_MOTIONS`) |
| POLYGON Particle FX | Los efectos del combate y del Oficio |
| POLYGON Icons | Piezas para los renders de la interfaz |
| INTERFACE Apocalypse HUD, Modern Menus y Military Combat HUD | La interfaz (los lleva UX/UI) |

## La tubería

La explica [tools/synty/README.md](../packages/client/tools/synty/README.md).

- Una sola lista dice qué se usa y para qué: `src/game/syntyManifest.ts`. El conversor (`tools/synty/convert.ts`, con Blender) convierte exactamente eso a GLB en `public/models/synty/<pack>/`. Hoy son unos 12 MB.
- **Siempre con ids explícitos:**

  ```bash
  npx tsx packages/client/tools/synty/convert.ts hunter sofa
  ```

  Sin ids, o con `--force`, borra lo que no está en el manifiesto y podría llevarse lo nuevo de otra sesión.
- **Escala:** una casilla son 1,5 m (`SYNTY_SCALE`). Los kits modulares van en módulos de 2,5 m y se dibujan 1,2 veces más grandes (`KIT_SCALE`).
- **Materiales:** un atlas por pack, cargado una vez y compartido (`ATLASES`, `game/synty.ts`). Los ocho atlas del mapa van además en una sola textura (`game/mapAtlas.ts`).
- **Personajes:**
  - 22 huesos con los nombres de los packs de animación;
  - pelo, barba y sombrero rígidos, cada uno en su hueso;
  - un segundo mapa UV (`part`) dice qué se puede teñir.
- **Ajustes sin reconvertir:** el giro, el tamaño y el sitio de los objetos (`fit`, `yaw`, `shift` en `PROPS`) se aplican en el juego.

## Qué hay y dónde

### Personajes (`game/syntyFigures.ts`, `syntyAnims.ts`, `unitViews.ts`)

- **Manos y codos** (Arturo 18/10: «todos tienen las manos abiertas y meten los brazos dentro de los cuerpos»):
  - **los dedos:** los modelos de Synty traen 22 huesos de dedos (pulgar de tres, índice de cuatro, y los otros tres juntos de cuatro), y el conversor ya no los pliega en la mano. Los nombres de cada fuente se unifican como `Thumb_01_L`, `Index_01_L`, `Finger_01_L` (`convert.py`: `thumb_01_l`, `Thumb_01`/`Thumb_01_1`, City Zombies `Thumb_01.001`);
  - **la postura de cada mano** (`HAND_POSES`, `hands()` sobre la mano ya colocada por el IK): gatillo con el índice estirado en la empuñadura; empuñadura cerrada en el cuchillo, el machete y la escalera; apoyo bajo el guardamanos; puño con la piedra y la granada; abierta al registrar; garra en los muertos; relajada en el resto. Se pasa de una a otra suave (`HAND_EASE`). Las pistas de dedos de los clips se quitan al cargar: los pone la mano;
  - **los codos:** el pecho de cada personaje se mide en su propia malla (armadura incluida, `torsoOf`). Si el codo o la mitad del brazo o del antebrazo quedan dentro (en el marco del pecho, que se inclina y gira), el codo gira alrededor de la línea hombro–mano, sin mover la mano, hasta salir (`clearElbow`);
  - **para comparar:** `__weapons.HANDS.fingers` / `.elbows` en el laboratorio de armas;
  - **coste:** los dedos doblaban los huesos (de 22 a 44). Ahora los nodos del modelo no se recalculan solos: cada hueso hace su matriz al escribirlo, y el modelo solo arrastra a sus huesos cuando se mueve (`stillUnlessMoved`). En Ensanche, pantalla de móvil emulada y sin tope, la CPU baja a 1,3 ms por fotograma: sin dedos estaba en 1,4 y con dedos sin el ajuste, en 1,7–1,8.
- **Supervivientes:** 16 personajes para elegir en el Cuartel (`SURVIVOR_CHOICES`). Sin elección, el soldado sale de su complexión y de su sexo (`SQUAD_BODIES`), y es el mismo en todas partes.
- **Los muertos:** comunes (10 modelos), corredores (4), acorazados (4, un poco más grandes), el Chillón y el Hinchado (`UNIT_BODIES`). Cada uno sale de su id, así que una horda es de todo tipo. Hasta el 15/10 salían del nombre, que comparten («Zombi»), y todos los de una misión eran el mismo cuerpo.
- **Animación,** por capas:
  - los clips de Base Locomotion e Idles se miden contra la pose en T del pack y se pasan a los huesos de cada personaje, así que el mismo clip sirve para los dos rigs de Synty;
  - encima, el torso (encogerse, impactos, retroceso, lanzar, tajo, mordisco, gritar) y los brazos: el arma en la mano derecha y la izquierda en el guardamanos, las dos con cinemática inversa;
  - la espera: la normal, con un vistazo o un cambio de peso de vez en cuando, o una postura propia (`STANCES`): los muertos se tambalean como borrachos, los Cuervos están en guardia y el soldado con la mitad de la vida o menos se encorva.
- **Velocidades** (`GAITS` en `gaits.ts`, la tabla que se afina; los segundos los decide Gameplay): cada plantilla tiene su paso, y el clip va a la velocidad que deja los pies quietos en el suelo (`clipSpeed`, medido: el pie apoyado no se desliza más de un ±8 %). Los muertos andan y corren con Goblin Locomotion, encorvados y arrastrando los pies.
- **Hacia dónde miran los muertos** (Arturo 17/10: ven en un cono de 120°): el modelo mira hacia `Unit.facing` del motor, que es `atan2(dy, dx)` en casillas; en el modelo, el giro es `π/2 − facing`. Lo hace desde el principio de la misión y cada vez que está quieto. Si la orientación cambia sin andar (un ruido), gira en el sitio en algo más de medio segundo (`TURN_ON_SPOT`). Al andar o al morder, termina en el mismo ángulo que el motor. `UnitViews.facingNow(id)` da el ángulo del modelo en cada momento, para que el cono de UI gire con él.

  | Quién | Un movimiento (azul) | Dos (amarilla) |
  |---|---|---|
  | Los vivos (escuadra, Cuervos, VIP) | correr ×1,48 · 0,375 s | sprint ×0,93 · 0,24 s |
  | Agachado | andar agachado ×1,27 · 0,71 s | |
  | Muerto común | andar ×0,91 · 0,82 s, arrastrando un pie | andar ×1,20 · 0,62 s |
  | Corredor | correr ×0,87 · 0,52 s, encorvado | sprint ×1,19 · 0,20 s |
  | Acorazado | andar ×0,93 · 0,80 s, pesado | andar ×1,24 · 0,60 s |
  | El Chillón | andar ×0,99 · 0,75 s | andar ×1,33 · 0,56 s |
  | El Hinchado | andar ×0,87 · 0,86 s, bamboleándose | andar ×1,16 · 0,64 s |

  Con un arma en las manos, la escuadra se mueve con los clips de Mixamo (22/10 #15, `ARMED_MOTIONS`):
  - con fusil (cualquier arma a dos manos), andar, correr y esprintar con el arma baja y cruzada; son clips lentos de zancada larga, que van a ×1,9 corriendo y esprintando, unos 3 y 4 pasos por segundo;
  - agachado sigue con las piernas de Synty y el fusil en reposo, porque el de Mixamo es un paso sigiloso a un séptimo de la velocidad;
  - con pistola, los de Synty, con la pistola baja en la mano que se balancea: el de Mixamo para andar apunta, el de correr la lleva por delante a una mano y los de esprintar y agachado son sin arma.

  Medido moviendo el muñeco a la velocidad del juego, el pie apoyado se desliza entre un −3 % y un +2 %. El sprint de Synty estaba en 6,9 m/s y el pie se iba un 14 % hacia delante; ahora está en 5,95.

  Los que andan, un quinto más deprisa desde la prueba del 16/10 (#6). Son segundos por casilla; una diagonal tarda 1,4 veces más, una escalera de mano 1,2 s por planta y un tramo de escalera 1,6 veces la casilla. Todo va a la «Velocidad del combate» (`timeRate()` de `tween.ts`), los clips también.
- **Impactos** (`HITS` en `syntyManifest.ts`): las reacciones de Bow Combat (siete por complexión) para un golpe y las de Sword Combat (cuatro, con traspié) para un crítico, según de dónde venga, medido por hacia dónde va la cabeza. Un mordisco o un tajo lo doblan hacia delante. Mueven solo de la cintura para arriba: quien está agachado tras la cobertura sigue agachado. El combate espera 0,4 s, como antes, y la cola de la reacción sigue mientras pasa lo siguiente; si muere, la caída arranca desde la pose de la reacción (0,2 s de fundido).
- **Muertes** (`DEATHS` en `syntyManifest.ts`): seis clips por complexión, de Bow Combat y Sword Combat. El cuerpo cae lejos del golpe que lo mata: de un disparo de frente, de espaldas; por la espalda, de bruces; desde un lado, hacia el otro. Un crítico es un tiro a la cabeza, y un golpe cuerpo a cuerpo puede doblarlo sobre sí mismo. Entre las que encajan, cada unidad elige siempre la misma, y se queda con una que tenga sitio: ni pared ni cobertura alta en el camino, del ancho de los hombros, hasta donde llega la cabeza (`reach`). Si ninguna lo tiene, cae la que menos sitio ocupa. El clip se ve entero (de 0,9 a 1,5 s) y deja la pose final; el arma se suelta y queda en el suelo. Levantarse (Último verso, el zombi que sale del cadáver) es la muerte al revés.
- **Las armas de cada soldado** ([FASE8.md](FASE8.md) §A; `armamentOf` en `syntyFigures.ts`): en la campaña salen de `Unit.arms`, en la escaramuza de la plantilla.
  - En la mano, el arma de la Armería: fusil de asalto (por nivel, los tres de Apocalypse), escopeta, fusil de precisión, ametralladora; las buenas con modelo propio, el fusil de cerrojo limpio y la ametralladora ligera (la escopeta recortada y el fusil con mira usan la escopeta y el segundo fusil de precisión). Sin arma principal, la pistola, a dos manos: abajo en reposo y con los brazos estirados al apuntar.
  - Junto a un arma principal, la pistola va en su funda del muslo derecho. Al disparar con ella (`firesSidearm`), el arma principal pasa a la espalda, cruzada, y la pistola sale a la mano; al bajar el arma, vuelven a su sitio.
  - En la cadera izquierda, el cuchillo o el machete (un 80 % y un 72 % de su tamaño en Synty, que los hace largos al lado de los fusiles). Para el Tajo sale a la mano y el arma principal va a la espalda; luego vuelve.
  - Cambiar de arma en la misión (`weaponEquipped`): el arma se gira y baja como en una recarga, la mano izquierda va a la mochila y a mitad aparece la nueva (0,7 s).
  - En la base, el Cuartel y la fila de la misión muestran las mismas armas (`Gear.arms`).
- **Saltos y caídas** (`unitViews.ts`): saltar una ventana es subir agachado al alféizar y bajar o caer al otro lado; la caída dura más cuanto más alta es. Por un forjado roto, `fall()`: sin saltito, con tambaleo, agachado al tocar suelo.
- **Andar juntos** (`walkPlan.ts`, `arrivals.ts`; Gameplay, 19/10): los muertos de un grupo y los que llegan por el borde andan a la vez sin atravesarse. El grupo tarda como mucho 0,8 s (o un 25 %) más que su camino más largo.
  - Si con las esperas no cabe en ese tiempo, el grupo entero anda más deprisa, hasta 1,4 veces (`planHasted`). Solo si ni así cabe, el que se queda sin tiempo pasa al lado de otro, apartándose. Antes seguía esperando, y el grupo se pasaba del tope hasta 3 s.
  - Los que llegan por el borde rodean las casillas donde se quedarán los otros, si pueden, y se apartan al pasar junto a otro, como al andar (`arrive`, con los `passes` del plan).
- **Apuntar y bajar el arma:** cada vez que apunta cuenta (`aims`), y bajar el arma un rato después (`game.ts`) solo baja la suya. Antes, si volvía a apuntar en 1,45 s, la bajada pendiente le bajaba el arma nueva y le quitaba el paso atrás a quemarropa. Al morir se borra todo.
- **Coste:** la pose se actualiza a 30 Hz en reposo o lejos, a 8 Hz fuera de pantalla y nada en la niebla o muertos. Solo los 24 enemigos más cercanos dan sombra. Las unidades van en su propia capa (`UNIT_LAYER`), fuera de la oclusión ambiental.
- **La multitud** (`crowd.ts`): un muerto en calma (andando o quieto) no se dibuja con su figura, sino con una malla instanciada para todos los de su cuerpo, con la animación horneada en una textura. Se hornea el andar y la postura, con todo lo que añade la figura (encorvado, brazos al frente, balanceo), desde una figura aparte que nadie ve, unos pocos fotogramas en cada tick. En cuanto hace otra cosa (le dan, ataca, muere, sube, se gira), vuelve a su figura; cuando se calma, a la multitud. Cada cambio se hace en el mismo punto del paso: la figura entra con la fase de su clip y sale con la de la multitud, y los balanceos de los muertos (giro, cadera, brazos) siguen los bucles de los clips, como en lo horneado (prueba del 15/10, #19: el cuerpo saltaba a otro punto del paso). Un zombi que mueve dos veces en su turno anda sus dos caminos seguidos, aunque el grupo se juegue a la vez (`unitViews.moveAlong`): antes los dos tiraban de él a la vez y «iba y volvía». Medido en el Mac con el laboratorio de hordas: 45 zombis, de 1,7 a 0,6 ms de CPU y de 86 a 41 llamadas de dibujo; 90, de 3,1 a 0,5 ms y de 131 a 41. Sigue el corte de los edificios como las figuras.
- **El soldado que se levanta** (prueba del 16/10, #7; `Unit.risenFrom`): el zombi en que se convierte un soldado caído se dibuja como él. Lleva su superviviente (por su misma semilla), su ropa, su casco y sus colores, la piel gris verdosa y los andares de los muertos, sin el brazalete del jugador ni armas. Siempre con su propia figura, nunca en la multitud horneada, cuyos cuerpos no llevan ni colores ni casco.
- **La piedra** (docs/FASE10.md §4; el evento `stoneThrown`): un lanzamiento corto por abajo con la mano libre, sin soltar el arma, distinto del de la granada, que va por arriba (`units.tossStone`, la postura `toss`). La piedra es nuestra (un trozo facetado, gris) y vuela en un arco bajo y rápido (`fx.stone`). Donde cae, un golpe seco: algo de polvo, un saltito, y se queda en el suelo un momento. Si da a alguien, rebota en su pecho y cae a sus pies. Ni explosión ni humo.
- **Vigilancia cuerpo a cuerpo** (`Unit.watchMelee`): el soldado saca la hoja del cinturón y se queda en guardia, con ella delante a la altura del pecho y la punta un poco arriba; el arma, colgada. Cuando acaba la vigilancia, vuelve a su arma. El tajo de reacción es el de siempre.
- **La pistola** (prueba del 16/10, #3): en reposo, «listo bajo»: las dos manos sobre ella delante del vientre, los codos doblados y la boca hacia el suelo de delante. Apuntando, de frente al blanco (casi sin girar el pecho), con los dos brazos casi rectos y la pistola en el centro a la altura de los ojos. Se coloca respecto al frente del cuerpo, no al del pecho: los clips giran el pecho como para un fusil y se la llevaban al hombro derecho, lejos de la otra mano.
- **Las mejoras y el estado del arma** (docs/FASE9.md §5; `Arms.mods` y `Arms.condition`, en misión y en el Cuartel): en el arma de la mano de un soldado de campaña.
  - **Mira:** una telescópica de Apocalypse (`scope7`) sobre el cajón de los fusiles y las armas de precisión; un punto rojo (`scope2`) en la escopeta, el subfusil y las ametralladoras. La altura del cajón de cada arma está en `WEAPONS` (`sight`).
  - **Silenciador:** el tubo envuelto en cinta (`silencer2`), el «casero» de Lore, en la boca. El fogonazo sale por su punta.
  - **Culata y cargador ampliado:** no se ven; las armas ya llevan los suyos.
  - **Gastada** (por debajo de 30): más oscura y mate. **Rota** (0): más oscura todavía, con dos tiras de cinta americana a los lados de la mano de apoyo.
  - Las piezas son `WEAPON_PARTS` en el manifiesto, convertidas como armas (el cañón por +z). `sniper2` lleva bayoneta: su boca está puesta a mano (`muzzle`) donde acaba el cañón.
  - En `/armas.html`: `__weapons.sheet(nombre, 'aim', cuerpos, __weapons.MOD_CASES)`.
- **Sin clases** (docs/FASE9.md): la ropa y el aspecto salen del `Appearance` del soldado. Sin él (partidas viejas), casco y complexión normal; la clase ya no elige capucha ni corpulencia. El dron del Grillo ya no se dibuja: el Grillo no sigue en la 0.3.0. `UnitRig.drone` queda a null mientras `game.ts` conserve sus vuelos.
- **La sirena del sabotaje** (`SirenRig` en `syntyFigures.ts`; prueba del 15/10, #14): la unidad objetivo según `s.mission.alarm`, o según el bioma si falta (ciudad, policía; afueras, ambulancia; fábrica, motor), con la misma regla que el sonido de Red (`sfx.ts`).
  - **Coche patrulla** (City) y **ambulancia** (Apocalypse): dos casillas de largo. La unidad va en la de atrás y el morro sobre la casilla `sirenCar`, que bloquea el motor (`unitViews.sirenOf` la busca; `mapView` no dibuja nada en ella). Dos rotativos, azul y rojo, que barren el aire y, más anchos y tenues, el suelo.
  - **Fábrica:** un poste con la sirena de motor, que es un tambor amarillo con su rotor y sus rejillas hecho por nosotros, y un rotativo naranja encima.
  - **Al callarla** (desde el primer instante de su caída): el coche y la ambulancia se apagan de golpe con un chispazo; la de motor va perdiendo giro y luz en 2,5 s, al ritmo del sonido. Nunca se gira hacia nadie ni se cae.
  - Sin luces de verdad: lentes emisivas y abanicos aditivos con una textura nuestra, para no cargar el fotograma.
- **Retratos de la radio:** personajes de Synty en su panel (`strategy/portraits.ts`).
- **Sin reserva de primitivas:** `models.ts` y `figures.ts` solo guardan el contrato que mueven las vistas (`UnitRig`, `FigureRig`, `FigurePose`, `Gear`). Una plantilla sin personaje en el manifiesto sale como recluta y avisa en la consola.

### Mapas de combate (`game/mapView.ts`, `buildings.ts`, `ground.ts`, `dressing.ts`, `batch.ts`)

- **El contorno** ([MAPAS.md](MAPAS.md) §5.10, fase 3; `frameArt.ts`, `outline.ts`): lo de fuera son casillas `'wall'` con suelo 4, y `GameState.frame` dice qué es cada trozo. Se dibuja hasta el borde y sigue más allá (`BEYOND`, 6 casillas); la niebla no lo tapa (`FogOfWar.setOpen`) y la cámara se queda en lo jugable (`playBounds`, sin las tiras de las entradas).
  - **Manzanas cerradas** (`block`): son edificios del plano (`BuildingPlan.frameBlocks`, `Building.frame`), con sus plantas, su contorno de pared y el interior tapado (`plan.shut`). Fachada y ventanas en las plantas de arriba, como cualquier fachada, y azotea con sus cosas. Nadie entra: el corte solo las abre si tapan a alguien (`behind`). Las tiras de menos de 3 de ancho se cierran con muro, como un solar;
  - **en la línea donde empieza lo jugable**, lo de `dressing.ts` (`fenceOff` y `shut`): valla de chapa o muro prefabricado en los solares, chapa en las obras, montones de escombro en las ruinas, la barricada de una calle cerrada (`street`), los muros en T de la barrera y un pretil con barandilla en el río. En las esquinas del contorno, la línea dobla hasta la siguiente;
  - **dentro**: hierbas, basura, coches quemados y algún árbol seco en los solares; montones de escombro en las ruinas; palés, cemento, ladrillo, tubos y un retrete en las obras; la calle cerrada, con sus coches abandonados hasta fuera del mapa; la barrera, en filas: concertina, sacos terreros, una garita cada 14 casillas y cajas;
  - **río, vías y autovía** (`bandOf`): una franja recta a lo largo de su lado, tan honda como su tramo más estrecho; lo que sobra son orillas, taludes y arcenes. El canal: un tipo de suelo de agua (`FLOOR.WATER`), sin losa y sin suelo en el sombreador, hasta la otra orilla (`FAR_BANK`, 5 casillas fuera del mapa); el agua abajo y los muros de hormigón. Las vías: terraplén con balasto, traviesas y carriles, postes de la catenaria y un tren de contenedores parado, con algunas plataformas vacías. La autovía: tablero a dos plantas sobre pilas, barreras, carriles y algún coche abandonado. Todo es geometría nuestra en colores planos (`SHAPE_GEOMETRY`: caja, cuña y agua), en el lote fundido;
  - **coste**, en el mapa de 64 del canal: construirlo pasa de 150 a 220 ms (una vez por misión); abrir una puerta, de 1,7–2,2 a 2,0–3,6 ms;
  - **el campo y el polígono** (fase 5):
    - **bosque** (`forest`): en la línea, dos filas de árboles con matorral (`fenceOff` 5) y a veces troncos caídos (7); dentro, árboles juntos (casi uno de cada dos casillas), matorral, algún tronco y alguna piedra. Pinos en la fábrica y en la carretera del bosque; mezclado en el resto. Solo la primera fila de dentro da sombra (`Dressing.cast`, que `mapView` ya respeta);
    - **rocas** (`rocks`): pared de roca con peñascos delante (`fenceOff` 6); dentro, peñascos, crestas, piedras y algún árbol;
    - **barranco y agua** (`ravine`, `water`): van a lo largo de un lado, como el río, así que también son franja (`bandOf` les busca el lado y el tramo que ocupan, `from`–`to`) y el suelo se abre (`SUNKEN`). El barranco: paredes de roca de 5 casillas de hondo, crestas a lo largo del borde, suelo de piedra con un arroyo y el bosque en la otra orilla. El agua: orillas de tierra en talud hasta el agua y la otra orilla con su bosque. En la línea, poco alto para que se vea por encima (`brink`): peñascos y matorral en el borde del barranco; juncos, matorral y alguna piedra en la orilla. Si llegan a una esquina del mapa, siguen más allá; si no, se cierran con una pared o un talud;
    - **contenedores** (`containers`, polígono y fábrica): en la línea, contenedores uno tras otro (`fenceOff` 8); dentro, pilas de 1 a 3 en filas, a tres casillas o más de lo jugable. Uno que atravesaría otro (dos lados que se juntan en una esquina, o el autobús que cierra una calle) no se pone (`apart`, con las tiradas más largas primero);
    - **la nave de la fábrica** (`factory`, `mainHall`, §4.2): 24–30 a lo largo de sus muelles y 16–20 de fondo, mirando al carril donde mejor cabe, con 3–4 muelles y un camión de 2 × 6 en cada uno (menos uno vacío). Los silos, de 3 × 3, separados una casilla; el cliente escala el modelo a su bloque;
    - **medido** en el Mac (M2 Pro, 1920 × 1080), el campo con más árboles (wilds/9, 1.813 en el contorno): 60 fps en «alto» y en «medio», GPU 16–19 ms, CPU 2,6 ms, 1,8 M triángulos. Falta medirlo en el PC de referencia;
  - **las misiones sin `frame`** se ven como antes, con sus bordes de `dressEdges`.
- **Aceras de 2** (fase 3): junto a una calle de 5 casillas o más de ancho, la segunda fila de la acera también es baldosa, no césped (`GroundMap.lawns`).
- **La Lata en su plaza de 2 × 9** (fase 3): el autobús ocupa la plaza; un vehículo más corto (el todoterreno, la furgoneta, el camión) aparca delante, con el morro donde lo tendría el autobús (`EvacBus.park`).
- **Proporciones reales, lo que no espera al motor nuevo** ([MAPAS.md](MAPAS.md), 19/10):
  - **Árboles a su tamaño** (§4.7): el de calle, el árbol de copa, el pino y el seco ya no miden una casilla y media de ancho. Ahora tienen 5–8 m de copa (`fit` 3,47, 2,73, 2,13 y 3,17). El tronco sigue en su casilla y la copa va por encima de las de alrededor. Para que no tape lo que se mira, **la copa se funde** (`treeFade.ts`): sobre el soldado elegido, el objetivo y la casilla del cursor, y entre ellos y la cámara, como los tejados. Los árboles van en un lote propio (`TreeLayer`), y cada vértice lleva dónde está su árbol (`aTree`); el tronco no se funde;
  - **ventanas de laboratorios, naves y cobertizos:** el marco del kit Bunker es de una puerta y se abría a 0,47 m del suelo. Ahora lleva un antepecho de un metro (1,15 m dibujado: `bunkerSill`, con el ladrillo o la chapa por fuera: `brickSill`, `sheet…Sill`), y el cristal va del antepecho al marco;
  - **las piezas pequeñas de la base,** por encima del soldado: la chabola de la radio y la tienda abierta de la Sala de mapas no bajan de 1,75 casillas (2,6 m), aunque sobresalgan de su parcela hasta que la pieza crezca (§4.9). Las farolas, a 0,9 de su tamaño (4,4 m);
  - **La Lata, a su tamaño:** los modelos del kit ya vienen en casillas, y `evacBus.ts` los encogía otra vez a dos tercios (`SYNTY_SCALE`). El autobús mide ahora sus 7,6 casillas. La huella de 2 × 9 llega con la fase 3;
  - **hidrantes y buzones,** ya en su propia casilla (fase 1): hacia el bordillo en medio de su casilla y al 85 %. En la esquina estorbaban al paso en diagonal;
  - **puertas en fila:** en la base, cada puerta se cuelga del extremo contra lo que no es otra puerta. La de en medio de tres o más es corredera: cerrada en su sitio y, abierta, corrida por fuera tras la de al lado. En los edificios, una fila de tres puertas o más (muelles, portones) lleva persianas enrollables, bajadas si está cerrada y recogidas bajo el dintel si está abierta; ninguna hoja cuelga de un poste.
- **Rendimiento para los mapas de 72** (§7): `buildWalls` y `layRaised` (`buildings.ts`) solo recorren el rectángulo de casillas que cambió, y los props de tejado solo de los edificios de alrededor. La puerta del rótulo se busca en los contornos. Los parapetos de los pisos se guardan de una reconstrucción a otra y solo se rehacen si cambió uno (una explosión). A 48, abrir una puerta o romper una ventana baja de 1,8–2,6 ms a 0,6–1,2 ms. La caja de la sombra, que en los mapas de más de 48 sigue a la vista, cuenta también los tejados del borde del mapa (`renderer.ts`: las esquinas a `SHADOW_TOP`).

- **Centros médicos** (referencia del hospital de POLYGON Apocalypse, 15/10): baldosas claras de unos 65 cm con juntas, pared clara con zócalo gris y franja granate. `medicalFinish.ts` pinta suciedad, grietas, desconchones y ladrillo visto según el desgaste **sólo en la cara interior**; la normal exterior de cada módulo distingue ambas caras y la fachada mantiene el kit normal del edificio; no crea agujeros transitables ficticios ni modifica la cobertura. Los límites `GameState.medical` conservan la identidad del edificio aunque se destruyan los muebles.
  - El consultorio de Brena, de una planta y huella máxima de 11×11 casillas, está ordenado y poco deteriorado (`wear: 0.08`): recepción/espera, consulta y sala de exploración/equipo separadas por paredes y puertas; mantiene las medicinas en suelo interior accesible de la sala clínica. La enfermería abandonada (`MapSource.medical: 'ward'`, `0.45`) y el hospital (`'hospital'`, `0.85`) cambian la distribución con la semilla. Los hospitales grandes sólo aparecen en ciudades, en manzanas mayores. «La doctora» genera un hospital de tres plantas, con salas de 3×3 o 3×4 casillas: recepción, espera, consultas, radiología/ecografía, curas, aseos y almacenes; ingresos de una o dos camas por habitación, con mesillas, monitor, armario y silla de acompañante. Dos alas de habitaciones con puerta propia al pasillo de dos casillas y dos espacios reservados a circulación/escaleras; el espacio sobrante se amuebla como espera sin ocupar unidades ni objetivos. La enfermería pequeña organiza espera/recepción, curas, observación y almacén a lo largo de un pasillo propio, con 4–8 muebles y equipos por sala según su tamaño. Se conserva exactamente la generación de los consultorios aprobados. `MedicalSite.rooms` conserva el uso y la planta de cada sala. Las escaleras médicas usan acabado metálico, conservando peldaños, soportes y recorrido. Las casas ordinarias mantienen su acabado y generación.
  - Cama, camilla, portasueros, estantería y botiquín son los GLB Synty ya versionados, reutilizados mediante alias del manifiesto. El carro de curas, el armario, la silla de ruedas, el mostrador de recepción con ordenador, los asientos de espera, la mesa de exploración, el monitor clínico, el equipo de radiografía/ecografía, las mesillas, sillas de acompañante, lavamanos, autoclave/mesa de instrumental, mesa de revistas, fuente de agua, aseo y cubo clínico son geometría propia de `medicalFurniture.ts`, dentro de la casilla de cobertura, fusionada con el mapa y sujeta al corte por planta y a la niebla. No se han convertido packs ni incorporado originales de Synty.
  - La enfermería de Atalaya comparte el suelo, paredes y franja, conservados, con cama y material clínico desde nivel 1, camilla en nivel 2 y ducha en nivel 3. La franja se aplica sólo a las caras interiores. Se abre hacia la cámara para que se vea el mobiliario.
  - Ambientación clínica (15/10, revisión de abandono): desconchones muy puntuales; las paredes mantienen casi todo el revoco. Cada centro nuevo recibe de 1 a 10 cadáveres según una semilla independiente (incluye cantidades de 5 y 10), sin unidades, cobertura ni obstáculos adicionales. `MedicalSite.dressing` guarda su posición para que no cambie al cargar o reconstruir el mapa. Se evitan umbrales, accesos de escalera y objetivos. Algunos cuerpos permanecen bajo una sábana en camas; otras camas sólo conservan el bastidor, sin colchón. Biombos plegados al lado de camas y exploración, sueros, medicación, vendas, libros, maletas, muletas, sillones y sillas de ruedas azules o negras. Crucifijos y carteles de ATENCIÓN / ZONA SEGURA sujetos al muro, con su corte y destrucción.
  - Los cadáveres reutilizan personajes y clips de muerte Synty ya convertidos, horneados como mallas estáticas mediante `node packages/client/tools/synty/bake-medical-corpses.mjs`. En el manifiesto llevan `baked`: el conversor los salta, para no pisar el horneado con el personaje en T. La cama cubierta, el bastidor, el biombo, el sillón, los pequeños enseres y la variante de silla negra son geometría propia; los modelos exactos del pack Apocalypse de las referencias no están disponibles en este Mac. No se atribuyen a Synty esos sustitutos.
  - Sangre, suciedad y desorden (15/10): charcos oscuros junto a los cuerpos, rastros de pisadas y salpicaduras en paredes cercanas; suciedad de humedad y tránsito en suelo y zócalos. Papeles clínicos, medicación derramada, vendas, residuos y ropa de cama arrugada; algunos asientos y soportes de suero caídos en los bordes. Cada centro y sala varía mediante una semilla separada, conservando las posiciones aprobadas de cuerpos, muebles y objetivos. Todo es decorado propio, estático y fusionado con el mapa. Las manchas planas no proyectan sombras y las salpicaduras desaparecen con su muro; nunca crea obstáculos ni agranda los desconchones.
  - Revisión reproducible en `/referencia.html?medical=clinic&seed=320610070`, `?medical=ward&seed=3`, `?medical=hospital&seed=7&level=1`; `&room=ward&room-index=0` acerca una habitación de ingreso de la planta seleccionada (también `waiting`, `consultation`, `equipment`, `treatment` y `supplies`); `&exterior` muestra la fachada sin corte por unidades; `&objects` oculta las unidades para inspeccionar el arte y `&snapshot=nombre` guarda el lienzo del motor tras asentarse el corte. Los mapas nuevos reciben las distribuciones nuevas; el consultorio ya guardado se reconoce por sus muebles y recibe el acabado sin recolocar nada.
- **Nieve del invierno** (`snow.ts`; desde el 16/10 la pide el motor con `GameState.snow` mientras dura el invierno de la campaña, antes solo el capítulo 1 en el consultorio de Brena; probada en ciudad, afueras y fábrica): la misma nieve convertida de Synty que Atalaya sobre el suelo, los tejados y lo que mira al cielo de los objetos (no bajo techo), ventisqueros de la base en la cobertura alta de fuera, la hierba baja enterrada y una luz de invierno propia (`SNOW_DAY` en `renderer.ts`). Las calles quedan con su marca, más oscuras.
- **Edificios en los que se entra,** de los kits modulares:
  - el tejado se desvanece cuando se ve el interior por una ventana o una puerta;
  - las puertas se abren y se cierran girando sobre la bisagra, un cuarto de vuelta hacia dentro en 0,45 s (`doors.ts`): las hojas no van fusionadas con la pared, sino en una malla instanciada por tipo, con los datos de corte de su pared;
  - varias plantas, con corte por planta y la planta activa (RePág/AvPág, Mayús+rueda);
  - escaleras con pasamanos a los lados del tramo y barandilla alrededor del hueco, abierta solo al rellano, que es por donde se dejan ([FASE7.md](FASE7.md));
  - paredes que rompe una granada: un muñón a la altura de cobertura baja, el dintel encima (la planta de arriba sigue en pie) y escombros a cada lado. Se reconocen por el decorado `breach`, también tras recargar;
  - forjados rotos: el agujero, por el que se ve la planta de abajo, y cascotes planos debajo. Rehacer las plantas con un agujero nuevo cuesta 7–10 ms, una vez por explosión.
- **Casas por salas** (prueba del 18/10, #15; el reparto en `mapgen.ts`, de acuerdo con Gameplay; `homeDressing.ts`; `GameState.homes`):
  - **las salas,** por tamaño y por dónde se entra (`homeUses`): la del portal es el salón; una cocina pegada a él si hay sitio; la más pequeña, el baño; el resto, dormitorios y, en una casa grande, un despacho. Arriba, dormitorios, un baño y quizá un despacho. Una casa de dos salas parte la de atrás en dormitorio y baño, y la de una sola, en salón y dormitorio, si mide 7 o más (`splitOff`: un tabique con puerta, salas de 3 o más de lado, como pide la prueba de mapgen); la cocina va entonces en el salón, y una casa de una sala es un estudio, con su cama;
  - **los muebles por sala** (`furnishHome`): salón, con librería, sofá frente a la tele o la mesa y cocina pequeña; cocina, con nevera, encimeras, fogón y mesa; dormitorio, con cama y armario; baño, con bañera, váter y lavabo; despacho, con mesa, librería y archivador. Muebles nuevos del motor: `fridge`, `stove`, `bathtub`, `washbasin`, `tvStand` y `wc` (el `toilet` es de las clínicas), con modelos de Town y Apocalypse;
  - **el suelo de cada sala** (`HOME_FLOORS` en buildings.ts): tablas de tonos distintos en salón, dormitorios, despacho y recibidor, y baldosa clara en cocina y baño. Una casa sigue siendo casa aunque tenga encimeras (antes, una encimera la hacía tienda);
  - **fase 4, salas de 2 × 3** (decisión 1 de Arturo, 19/10): el baño, el aseo y la despensa se cortan de 2 de fondo (`splitOff`, que deja 3 o más al resto y no corta una tira que tenga puerta propia). La casa de dos plantas (con recibidor) tiene abajo el salón delante y la cocina detrás, un aseo (váter y lavabo, con alfombrilla) cortado del salón o de la cocina y una despensa (estanterías: da comida) cortada de la cocina; los dormitorios y el baño, arriba. Solo se cortan donde la sala mide 6 o más, así que salen en una casa de cada cuatro, más o menos;
  - **el zaguán** (fase 4, §5.4): en una fila de casas pegadas alrededor de un patio, sin hueco, la casa del medio abre su recibidor por delante y por detrás (2 de ancho, sin puertas), con la planta de arriba encima y la escalera a un lado. No es sala de la casa: no se amuebla ni se ensucia, y su suelo es de baldosa de piedra hasta las aberturas (buildings.ts). Hoy solo hay patios en el hospital (las manzanas de la ciudad no dan fondo para dos filas y el patio), así que todavía no sale en ningún mapa;
  - **el desgaste,** por casa (`homeWear`): las del pueblo viejo, peor que las de la cuadrícula. En una destrozada, algún armario cae de cara (cobertura baja; se dibuja tumbado). Con semilla propia (`dressHome`): los muertos donde cayeron, su sangre y su rastro, ropa fuera de las camas, libros y papeles por el suelo, una silla volcada, maletas hechas, suciedad junto a las paredes y sangre en una pared en las destrozadas. Son los detalles de las clínicas, dibujados igual;
  - **el azar del generador,** el mismo: las salas nuevas se amueblan con un azar aparte, así que el resto de cada ciudad sale como antes. Probado con la semilla 31: las casas, en el mismo sitio y del mismo tamaño;
  - capturas en `.dev-snapshots/arte/casas/`.
- **Interiores con cariño** (prueba del 18/10, #36: «que se sienta una ciudad o un pueblo de verdad»; `homeFurnishing.ts`, decorado del cliente que no es cobertura, solo en suelo libre y fuera del paso de puertas y escaleras):
  - **el ajuar de cada sala,** según lo que ya tiene. En el salón, una alfombra bajo el sofá y la mesa, un sillón enfrente o al lado, una lámpara de pie y una planta en las esquinas. En la cocina, sillas alrededor de la mesa, la panera o la cafetera en la encimera y una planta. En el dormitorio, la mesilla con su lamparita junto a la cama, una alfombra delante y una silla o una planta. En el despacho, lamparita y libros en la mesa, una alfombra y una planta. En el baño, alfombrilla junto a la bañera y toallero; en el aseo, alfombrilla delante del váter. En el recibidor, perchero y planta. Todo de Town, a su tamaño real, con un estilo por casa (sillas, alfombras, lámparas); en una casa destrozada, sillas y lámparas por el suelo;
  - **coherencia:** las estanterías de una casa (y de una oficina) son sus librerías, de un solo modelo por casa; las encimeras de una casa son de cocina y las de una tienda, mostradores. Antes se mezclaban al azar casilla a casilla: de ahí salió la biblioteca que le gustó a Arturo;
  - **las tiendas, por oficio** (por su `look`, el mismo que deciden sus estanterías): **librería** (3 de cada 10), con estanterías de libros, libros en el mostrador y un rincón de lectura; **café-bar**, con taburetes en la barra, la cafetera y mesas con sillas en el suelo libre; **ultramarinos**, con estanterías de tienda, la nevera de bebidas contra una pared, la nevera portátil junto al mostrador y cajas de género. Arriba, el almacén con sus cajas. Tres de cada diez naves de estanterías son una **biblioteca**: todas librerías y mesas de lectura;
  - **el granero** (#53: había salido como una sala de baldosas con pacas sueltas): el cliente lo reconoce por la paja, sea del tamaño que sea, con sus paredes de madera pintada y suelo de tierra. En el motor (`furnishBarn` en `mapgen.ts`, sin sacar azar), las pacas van contra las paredes de los lados y del fondo, con huecos y apiladas en alto a trechos (tres pacas cuadradas una sobre otra), y el tractor viejo en medio (`SM_Veh_TractorOld_01` de Farm, sobre un bloque de 2 × 4 de camión), con el morro a las puertas. El cuarto de la esquina es el de los aperos. Además, la horca, el rastrillo y la pala apoyados en la pared, una carretilla y cubos.
- **Cajas de botín** (`lootBox` en `mapView.ts`, `lootMarks.ts`; [FASE8.md](FASE8.md) §B): cada tipo con su forma y su color para que se distinga de lejos. Armas, una caja larga de madera con tapa; medicinas, el contenedor rojo con el botiquín encima; munición, la caja militar verde con su lata; comida, cajas de cartón. Abiertas (`s.searched`), la tapa o lo de encima queda en el suelo, con algo de lo que había. Encima de cada una, su marca: el icono de Synty del tipo en una placa de acero con el anillo de su color (amarillo, verde, ámbar y latón). Sale cuando la casilla se ha visto, se queda en lo explorado y se va al abrirla.
- **Lo que se deja en el suelo** (`dropped.ts`; [FASE8.md](FASE8.md) §C, `GameState.dropped`): una cosa por casilla, apartada del centro para que no la tape quien está encima. Un arma, su modelo tumbado de lado; una medicina, su botiquín; comida, una caja de cartón con una lata encima; lo demás, y varias cosas juntas, una bolsa de lona. Debajo, un aro fino y claro que la separa de la basura de la calle. Se va al recogerla y sigue el corte de los edificios.
- **Escenarios** (#25; `GameState.site`, el plano lo hace Gameplay en `mapgen.ts`):
  - **Área de servicio** (`roadside`, en el bosque): carretera de asfalto con arcén de grava, grietas, polvo y línea central en los tramos rectos (`ground.ts`, `gTarmac`), y los patios asfaltados; pinos macizos de Nature (`PolyPine`: los de agujas y los de Woodland piden texturas que no tenemos); vallas de tablas rotas, la malla caída de Woodland, el cartel «Royal Gas» de cara a la carretera y el leñador sobre su peana en lugar de la estatua.
  - **Polígono** (`industrial`, en la ciudad): contenedores de Woodland en tres colores (texturas propias a 512, metidas en la textura empaquetada del mapa: no añaden llamadas) y espino de Military en las vallas de los patios.
  - **Patios** (el suelo 3 del motor, `YARD`): asfalto sin marcas, en el bosque y en la ciudad; la línea central solo va en las calles y la carretera.
  - **Naves de chapa** (kits `shedGrey`, `shedRed`, `shedBlue` en `buildings.ts`): el aserradero del bosque y tres naves de cada cuatro del polígono. La chapa es una tira del costado del contenedor de Woodland (lejos del logotipo), como piel de un muro de búnker, igual que el ladrillo de las otras naves; ventanas con marco de acero; los hastiales y el pretil, del color de la chapa. En el tejado plano, una tira de claraboyas de la nave industrial; en los tramos largos de pared lisa, persianas cerradas (las de Generic). Un edificio que es sobre todo estanterías (6 o más, el doble que mesas) es una nave, aunque tenga un despacho.
  - **Bloques** (`BLOCKS` en `mapView.ts`): el camión (2×4), el contenedor (2×5), la torre de agua (2×2), el silo de la fábrica (2×2) y la pila de troncos (1×2 o 2×2) se dibujan enteros desde la casilla de su centro, el morro o las puertas hacia su `facing`, mientras no les falte ninguna casilla; lo que deja de ellos una explosión es escombro.
  - **Gasolineras** (también en la ciudad): una isleta bajo cada par de surtidores y una marquesina de Apocalypse sobre todos (a veces quemada). La marquesina se desvanece mientras el seleccionado, el objetivo o la casilla del cursor está debajo.
- **La fábrica** (bioma `facility`, prueba del 15/10, #17; el trazado es de Gameplay en `mapgen.ts`: entrada con garita y oficinas, la nave principal con sus muelles y camiones, el patio de contenedores, silos y aparcamiento). Sus decorados:
  - `pallets`: palés de Apocalypse apilados, tres en bajo y seis en alto, cada uno un poco torcido;
  - `drums`: las pilas de bidones de Apocalypse, sobre palé o bajo lona;
  - `machine`: el generador grande de Construction, más alto, dentro de la nave;
  - `silo`: los depósitos de Construction con barandilla y escalera;
  - `forklift`: no hay carretilla en los packs; el motor pone palés con bidones en su lugar;
  - la nave lleva su tira de claraboyas: su tejado no se pisa (`skylit` en `mapgen.ts`), para que nadie domine desde arriba el patio del objetivo.
- **Dos estilos de ciudad,** mitad y mitad: cuadrícula y pueblo viejo. Hileras de tiendas, gasolinera, motel, casas con jardín, obras y control militar. Los bordes siguen la calle con vehículos abandonados.
- **El borde de la ciudad, cerrado** (prueba del 18/10, #32: «si no se puede ir por un sitio, que se vea por qué»). Justo pasado el borde (`dressEdges` en `dressing.ts`, solo cliente):
  - cada calle que sale del mapa se cierra de lado a lado, con sus aceras: muros en T de Military con neumáticos, un autobús atravesado con sacos en los extremos, o coches calcinados con tablones entre ellos. Más allá, lo que quedó en la calzada (autobús, furgoneta o coche), ya apartado del cierre;
  - los tramos abiertos (aceras, plazas, patios, casi la mitad del borde) se tapan a trozos de 10 casillas como mucho: los montones de un bloque derrumbado con su ruina detrás (`debrisPile`, `ruinPile`, de Military), una valla de chapas (`sheetFence`, Construction), un muro de placas de hormigón con basura al pie (`concreteFence`), muros en T o contenedores puestos en fila. Las esquinas quedan cubiertas;
  - los cierres van por turnos alrededor del mapa: no salen dos iguales seguidos. Es todo decorado, fusionado con el mapa; no cambia ni el motor ni la cobertura;
  - fuera de la ciudad también (la nota de Arturo vale para todo el juego). En las instalaciones, el borde es su perímetro: muro de placas con concertinas delante, muros en T, contenedores y chapas. En el campo, el monte se cierra: dos filas de árboles con matorral al pie (pinos en la parada del bosque), paredes de roca con piedras delante y, donde sale un camino, árboles talados de través, tronco sobre tronco, con el bosque detrás. En el claro de la base no, porque los muertos entran por sus pistas.
- **Luz de día** (`renderer.ts`), medida contra la captura de la ciudad de Apocalypse por bandas de tono: una neblina fina y cálida, el sol bajo a la derecha, la sombra levantada hacia el malva y una saturación media como la suya (0,29).
- **Suelo:** terreno continuo, sin aspecto de cuadrícula, y oclusión de contacto horneada (`groundAO.ts`). En la ciudad:
  - pavimento claro y poco cálido;
  - polvo a manchas, losas y asfalto agrietados en las zonas viejas, musgo en el bordillo;
  - papeles, escombros y basura sueltos, todo plano y cosmético (`ground.ts`, `dressing.ts`);
  - los tejados con trastos (`buildings.ts`); cajas solo en los que no se pisan.
  Lo que pueda leerse como cobertura lo pone el motor, nunca el decorado. El generador (mapgen.ts, acordado con Gameplay) pone árboles en las esquinas de las plazas, escombros en plazas y aparcamientos y más coches abandonados, siempre fuera del despliegue, del objetivo, de la evacuación y de los bordes, y a 2 casillas o más de paredes, puertas y escaleras. La cobertura cercana pasa del 25,7 % al 26,9 % del suelo abierto.
  - **Alcantarillas** (prueba del 18/10, #11: «muchísimas»): una tapa cada 16 casillas por calle, desfasada al azar entre una calle y otra, y un sumidero cada 13 por bordillo. Antes salían al azar en el 6 % de las casillas de calzada y el 8 % de las de bordillo.
- **Plazas** (prueba del 18/10, #12: «mobiliario al azar»):
  - **la fuente,** entera: la de Apocalypse (`SM_Prop_Fountain_01`, `fountainWhole`) sobre sus 3 × 3 casillas, con la estatua por remate, o más pequeña sobre las 2 × 2 de una plazuela. Antes, su borde eran bloques de hormigón (los «quitamiedos haciendo formas raras»). Lo que deja una explosión es escombro;
  - **los bancos** (decorado nuevo `bench` del motor, dos casillas de cobertura baja; se dibujan como bloque): el de City (`SM_Prop_ParkBench_01`) y, uno de cada tres, el roto de Apocalypse, también cuando la explosión se lleva una de las dos casillas. En cada plaza, dos que miran a la fuente; en la plazuela con fuente, uno;
  - **la plaza de salida** (`squadSquare` en `mapgen.ts`), ya como las del medio del mapa. La fuente queda al fondo; dos bancos la miran desde el lado de la escuadra, con una papelera; en las dos esquinas de los bordes, un árbol con su jardinera. Es la cobertura del primer movimiento. Los tres «puestos» de barricadas y cajas que el generador ponía junto a la escuadra solo salen en la ciudad si no hay nada en 3 casillas. La plaza tira del azar lo mismo que la de antes: el resto de la ciudad de cada semilla sale igual;
  - **las cajas altas,** una grande con otra encima, torcida. Antes, una máquina de refrescos o una barrera alta al azar: de ahí las «7 máquinas».
- **Noche** (`night.ts`), contra la captura del motel de Woodland de Synty: neblina verdosa y luna fría, y la luz cálida de lo que aún funciona (farolas, algún faro, bengalas y fuegos). Qué casillas están a la luz lo dice el motor.
  - **Sin deslumbrar** (Arturo 17/10: «la luz deslumbra mucho de noche»):
    - bloom de noche a 0,45 con umbral 0,88, para que solo florezca lo que emite y no el suelo iluminado;
    - farolas a 9 y bengalas y fuegos a 16 (antes 22 y 30); la luz del fuego, 0,55 por encima de las llamas;
    - cabezas de farola y faros más bajos;
    - el tinte de las casillas a la luz (lo que blanqueaba el centro de los charcos sobre la acera clara, medido: sin él, de 0,68 a 0,41) es un naranja más hondo a 0,6 (antes 1,4), y suma menos donde la luz ya da brillo. Los charcos se leen por el color frente a la noche verdosa más que por el brillo;
    - los faros de los refuerzos y las luces de La Mula, a la mitad de noche.
  - **Medido a 1920×1080** (bengala y fuego junto a la tropa, a distancia de juego):
    - en la ciudad, del 5,3 % al 1,4 % de píxeles quemados;
    - en las afueras, del 2,0 % al 0,4 %; lo que queda son sobre todo los anillos y las líneas de la interfaz;
    - en la segunda tanda (17/10), la ciudad pasa al 0,8 %, y el centro del charco de una farola sobre la acera, de 0,70 a 0,57 (la plaza a la luna está a 0,41).
    - Las zonas a la luz se siguen leyendo cálidas desde lejos.
- **Niebla de guerra** (`fogOfWar.ts`, `explored.ts`) sobre los materiales, por planta.
- **Coste:**
  - lo estático, fundido en pocas mallas (`batch.ts`), con sombras en caché;
  - la reconstrucción por trozos baja de 160–270 ms a 4–13 ms;
  - los ocho atlas del mapa en una textura: de 83 a 63 llamadas en la manzana de referencia.

### El Bastión, el mapa del asedio ([FASE12.md](FASE12.md) §10; `buildings.ts`, `doors.ts`, `mapView.ts`, `siegeDressing.ts`)

- **La vieja estación militar sobre Brena** (`maps.ts`, `bastion`, de Gameplay); cuando el estado trae `mission.siege`:
  - **la muralla:** el muro de cuarentena de Apocalypse (el kit `fence`: paneles de hormigón, columnas y alambre de espino);
  - **los portones:** las hojas de chapa `SM_Prop_Wall_Quarantine_GateDoor_01/02`, una por casilla (`gateLeaf`, `gateLeafFar`), sin dintel. Cada hoja gira en su extremo del portón (`hingeFar`: la segunda gira al revés para abrir al mismo lado) y lleva una columna del muro a cada lado;
  - **las barricadas `b`:** sacos terreros (`sandbags`);
  - **las torres:** `SM_Bld_GuardTower_02` de POLYGON Military sin su caseta ni su escalera (recortadas en el conversor: `keep` y `hole`). Queda la plataforma sobre patas con barandilla, a 3 × 3 casillas y con el suelo justo a una planta (`deck`), girada hacia su escalera de mano (`RAISED_TOWER`). Para el motor es un bloque macizo, así que por debajo no se pasa: por los lados de fuera sigue el muro de cuarentena, de modo que la muralla no se corta en la esquina, y por los de dentro la cierra una caseta de chapa, con la puerta cerrada en el lado sin escalera. Los muros llegan a la torre sin columna;
  - **los edificios:** de una planta y tejado plano. La Sala de radio (el edificio en el centro del mapa), de hormigón, con el mástil de celosía `SM_Bld_RadioTower_01`, su remate de antenas y una parabólica en el tejado; el barracón, de bloque; la enfermería, de hormigón; el taller y el almacén, de chapa. Sin letreros.
- **El daño** (`s.breakables` frente a `SIEGE.door` 18 y `SIEGE.barricade` 9; `siegeDressing.ts` `wearStage`):
  - a 2/3 o menos, «tocada»; a 1/3 o menos, «casi rota»;
  - las hojas se entreabren, se ladean y se descuelgan (`doors.ts` `WEAR`), con gravilla al pie por fuera;
  - los sacos, más bajos (78 % y 50 %) y con sacos sueltos;
  - un golpe sacude el portón 0,4 s (`mapView.knock`).
- **Al ceder** (la casilla pasa a suelo y su entrada queda a 0; se sabe qué era por el mapa tal como se hizo, `madeAs`, también al recargar):
  - el portón deja su hoja tirada hacia dentro y gravilla en las columnas;
  - la barricada, un resto bajo y los sacos desparramados.
- **Sin niebla ni oscuridad** (prueba del 18/10, #18): en el asedio, el mapa se ve entero, aunque el motor mande la vista de cada soldado (`mapView.setVisibility`). La vista de verdad sigue llegando a los edificios, para el corte y los tejados.
- **El golpe:** `unitViews.smash(id, tile)` embiste (resuelve al dar, 0,34 s; vuelve en 0,35 s). `fx.smash(at, 'door' | 'barricade', cede)` da chispas, escamas y polvo en la chapa, o tierra y arena en los sacos, más si cede. Lo reproduce UI (game.ts), solo si la casilla se ve.

### La base nueva, el claro del pinar ([FASE12.md](FASE12.md) §11; `basePieces.ts`, `mapView.ts`, `strategy/clearingArt.ts`, `syntyFigures.ts` TurretRig, `evacBus.ts`)

- **Una sola manera de dibujarla:** el MapView del combate, en su asedio y en la pantalla de la base (el `ClearingSet` de UI, que mete `ClearingArtView`). En la pantalla, el estado sale de `createMission({ map: { kind: 'base', base } })` sin unidades; se ve todo, con la luz de día del combate o la de la nieve (`dayMood` en renderer.ts), y su cielo y neblina (`dress`). Las tareas del mapa en la GPU y las sombras van en el `beforeRender` del set (`runMapJobs`).
- **Las piezas** (`GameState.base.pieces`, base.ts `PIECES`): buildings.ts y ground.ts ven suelo llano donde hay una (`terrainOf`), y `drawPiece` la dibuja desde la casilla bajo su centro. Un cambio en una pieza (puesta, quitada, de nivel, de desgaste o de cargas: `pieceKey`) rehace solo los trozos que toca, también en la pantalla de la base (talar, construir, mejorar).
- **La explanada:** pinos de Nature de tres tamaños, fuera de la rejilla y girados, algún pino seco (`basePines`); las pistas (`ground` a 3) en tierra pisada, como los caminos del bosque; un tocón (`bStump`) donde se taló (`felled`), plano, hasta que se construye encima. Nada crece dentro de los edificios ni de sus ruinas.
- **La caravana:** `SK_Veh_Caravan_01` de Apocalypse, a 4,35 × 1,6 casillas.
- **El Bastión crece con el nivel** (prueba del 18/10, #40; `drawBastion` en `basePieces.ts`). Cuatro etapas de dos niveles cada una, cada edificio sobre su huella del solar de 9 × 9 (fase 6: `bastionLot` y `bastionFootprint` del motor; 5 × 2, 7 × 3, 7 × 5 y 7 × 7), de frente al patio (al sur). En el nivel par de cada etapa se le añaden cosas:
  - niveles 1 y 2, **la caravana**; en el 2, escalón, antena en el techo, lona a un lado y generador;
  - 3 y 4, **la casa móvil** (`SM_Bld_Trailer_01` de Apocalypse) sobre su faldón, con sus cristales; en el 4, el porche cubierto, la parabólica y otro escalón;
  - 5 y 6, **el barracón** de contrachapado sobre pilotes (`SM_Bld_Plywood_Shed_01` de Military), con su escalera al frente; en el 6, paneles solares y generador en la columna libre del solar, sacos a los lados de la escalera y un foco;
  - 7 y 8, **la casa** (`SM_Bld_House_03` de Apocalypse), con su porche dentro de la huella de 7 × 7; en el 8, sacos en el porche, un mástil de radio en el tejado y un foco en la esquina libre de delante.
  Cada modelo va sobre el origen de su FBX, para que encajen el cristal y el faldón. El desgaste del asedio sigue al de las piezas: en 1 y 2, cascotes alrededor y el edificio algo torcido; la casa móvil cambia a su versión rota de Apocalypse. Cuando cede: la casa móvil hundida, el barracón destruido de Military, la casa hecha un montón (`SM_Bld_Destroyed_02`) y la caravana hundida. En el resto del solar, el patio, cada cosa en el centro de su casilla y las mismas casillas que `BASTION_YARD` del motor (cobertura baja en el asedio): la lumbre con una silla a cada lado, vueltas hacia ella (delante de la caravana, a lo largo del frente de la casa móvil, en la esquina de delante del barracón), y la leña en la esquina opuesta; la lona de la caravana en 2 × 2. Comprobado desde arriba con la niebla abierta solo en esas casillas (capturas en `.dev-snapshots/arte/fase6/`). La Lata no aparca en el solar; la vitrina de Soldados y el rincón de la Armería se buscan fuera de él.
- **Los edificios, por nivel 1–3** (`BUILDINGS`): los modelos de la base de antes (BASE_MODELS reutilizados como props `b…`, sin GLB propio), encajados en su huella (`Lot.fill`). Desde la fase 6, cada uno dibujado para su huella nueva (MAPAS.md §4.9: sala de radio, cuarentena, camposanto y taller de oficios 4 × 4; sala de mapas 6 × 6; barracón y enfermería 6 × 5; taller 5 × 4; chapistería 7 × 5; entrenamiento 5 × 5; huerto 7 × 6, con cuatro bancales; campo de tiro 10 × 3): lo que se dibuja ocupa su suelo. Una pieza que guarda su tamaño de antes (`BasePiece.size`) lleva el mismo dibujo encogido a su huella (`Lot.scaled`). Por ejemplo:
  - la Sala de radio: refugio de chatarra con lona y antena; luego el mástil; luego la parabólica;
  - el barracón: la tienda pequeña y el fuego; luego la tienda militar;
  - la enfermería: la tienda con botiquín y gotero; luego la de cuarentena; luego la ducha y la camilla;
  - el taller: el cobertizo de Farm con el banco; el huerto, bancales, plantas y espantapájaros;
  - la cuarentena: una jaula de paneles de malla de Military (`cagePanel`; el contenedor de antes no cabe en 3×3);
  - el camposanto, cruces; el campo de tiro, dianas y sacos; la sala de mapas, la tienda abierta con mesas y pizarra; la chapistería, el garaje; el taller de oficios, el cobertizo y los caballetes.
- **Las defensas a mano:**
  - la empalizada, medias estacas de Apocalypse (`SM_Prop_SpikeWall_01`) hacia cada vecina;
  - el muro, los bloques en T de Military, uno por casilla, dos en una esquina;
  - la puerta, de tablas junto a la empalizada y de chapa (la de cuarentena) junto al muro; gira en su bisagra hacia dentro al abrirse (`doorOpen`);
  - los sacos, los del asedio; los troncos, la pila de Farm; el alambre, una bobina de Apocalypse;
  - la torre, la del asedio a 2/3 con la cubierta a una planta y su escalera apoyada delante;
  - el foco, la torre de luz de Construction, con su luz de noche en la cabeza (el doble que una farola: `Glow.power`).
- **Las trampas:** la mina, medio enterrada (gastada, un cráter); el foso, un hoyo con estacas hacia fuera (gastado, rotas); el cepo, abierto (gastado, sus dos mandíbulas cerradas). Las cargas, de `s.traps` en el asedio o de la pieza fuera.
- **La torreta:** una unidad (`template: 'turret'`) con figura propia (`TurretRig`): el cañón antiaéreo de Apocalypse en sus piezas (trípode, giro, cuerpo y tambor, cada una en su origen), que gira hacia donde apunta, retrocede al disparar y se vuelca al caer. Fuera del asedio, el mismo cañón quieto.
- **El daño:** por etapas, como el asedio («tocada» a 2/3, «casi rota» a 1/3): inclinado, con piezas de menos, más bajo y con restos al pie; cedido (hp 0, o todas sus casillas en suelo), su ruina: estacas o muros caídos, la puerta en el suelo, montones quemados y chatarra en un edificio. En el asedio, por `s.breakables` de sus casillas frente a `PIECES[id].hp`; fuera, por su `hp`.
- **La Lata crece** (prueba del 18/10, #45; `buildLata` en `strategy/mula.ts`): antes de ser el autobús, es uno de los vehículos de Apocalypse con su propio blindaje, cada pieza en su sitio alrededor del origen del vehículo (`KitModel.origin`), pintado con la textura de vehículos con barro (`apocalypseVehicle`):
  - 0, el todoterreno (`SK_Veh_Car_Medium_01`), con rejas en las ventanas y púas en el morro;
  - 1, la furgoneta de la tele (`SK_Veh_NewsVan_01`), con su parabólica en el techo, focos y chapas; sus puertas de atrás giran sobre las bisagras;
  - 2, el camión del ejército con lona (`SK_Veh_Army_Truck_01`); se sube por detrás;
  - 3, La Mula.
  Las ruedas giran como las del autobús. En las misiones sale según `GameState.vehicle`; en el claro, según `setVehicle(vehicleOf(c))`, que UI llama. Si el cambio llega mientras se va o vuelve, espera a que termine.
- **La escuadra en el claro** (prueba del 18/10, #44; `setLineup`, `padPosition`, `lineupShot` en `ClearingArtView`): la cochera ya no existe. La selección y el informe pasan junto a La Lata, a lo largo de un costado o detrás de ella, donde la cámara la ve con menos pinos y edificios por medio, con las puertas giradas hacia la fila:
  - hasta 8 en una fila, con la mitad de cada comandante y un hueco entre ellas; de 9 a 12, en dos filas. En el cliente del segundo jugador va en espejo (`setLineup(entries, { mirror: true })`, #51): cada uno ve a los suyos a la izquierda, junto a su nombre;
  - cada uno lleva un aro del color de su comandante, y los huecos vacíos uno gris;
  - en el informe, los heridos salen agachados y los caídos dejan una cruz de madera con una vela;
  - con `depart()`, corren a las puertas uno tras otro, con el arma colgada, suben y La Lata se va (22/10 #12, `board`, `boardCar`):
    - en el todoterreno, cada uno por una puerta, el conductor primero y los demás medio segundo detrás uno de otro. Con «Entering Car» de Mixamo (el de la izquierda, y en espejo el de la derecha) cogen la manilla, abren, se sientan de lado, meten las piernas y cierran. La puerta va al ritmo de la mano del clip y no se cierra mientras la mano está fuera;
    - en la furgoneta (por la hoja izquierda, porque la derecha lleva la plancha de blindaje), en el camión (de dos en dos, por encima del portón) y en el autobús (por el escalón de cada puerta), suben de un paso con un paso de «Ascending Stairs» y entran agachados.
    - Estos clips van con la altura de cadera del propio clip, no con los pies pegados al suelo (`CAMP_FREE`).
- **Los puestos del asedio** (prueba del 18/10, #58; `setPosts` en `ClearingArtView`): mientras cada jugador coloca a sus soldados por el claro, cada uno está de pie en su casilla, a la altura de su suelo (arriba, en un torreón), mirando al borde más cercano, por donde entran los muertos. Las figuras se conservan de un cambio a otro; el anillo y el nombre son de UI.
- **La mochila del caído** (prueba del 18/10, #49; `dropped.ts`): lo que llevaba un soldado caído (`from`) es su mochila militar (`SM_Chr_Attach_Backpack_01` de Military) tumbada, con su arma principal al lado; una por casilla y por caído, hasta que no queda nada suyo.
- **La Lata** en el claro (`EvacBus`): aparcada junto a la caravana en el hueco de pista más cercano sin piezas ni pinos debajo (a un lado del carril si el medio está ocupado), de cara a la salida; si no hay ninguno, está fuera. `depart()` se va por la pista; `arrive()` llega de frente, da la vuelta y se queda lista para salir.
- **Soldados y la Armería** (nota de Arturo del 18/10: «el background del cuartel y la armería están mal»): pasan en el claro, donde la caravana hace de cuartel y de armería. En Soldados, el soldado elegido (`setShowcase`) está de pie junto al barracón si lo hay (si no, junto a la caravana) y gira despacio en reposo; `soldierShot()` lo mira desde el claro, con el pinar detrás. La Armería es un rincón en el extremo de la caravana (`armoryShot()`), con el banco y su panel de herramientas, el armero de Military con tres rifles, munición y suministros, solo donde la casilla está libre. El claro se nieva en invierno: el estado del mapa lleva `snow`, y no solo la luz.
- **La pieza suelta** (`pieceModel`): el fantasma de lo que se va a construir, con los modelos como los dibuja `drawPiece`, centrado en su huella; si sus modelos aún cargan, se completa con el material de `userData.material`.
- **Laboratorio:** `/claro.html` (`?seed`, `?bare`, `?snow`, `?ghost`; `__claro.look`, `fell`, `depart`, `arrive`). Capturas a 2560×1600 en `.dev-snapshots/arte/claro/`.
- **Coste** (asedio de muestra, 1920×1080, Mac M2 Pro): 60 fps en medio y en alto; cpu 1,5–1,9 ms; gpu 15–18 ms; 85–94 llamadas; 0,8–1,1 M triángulos, casi todo los pinos.

### Base y valle (`strategy/bastion.ts`, `baseKit.ts`, `stage.ts`, `geoscape.ts`, `valleyGeography.ts`, `valleyRoads.ts`, `valley.css`, `tools/valle/mapa.ts`)

- **El Bastión:** Atalaya en la sierra, con la luz de la manzana de referencia (de 524 a 132 llamadas). Tiene el Cuartel, el Taller de oficios, obras con Construction y el Huerto por niveles: bancales con plástico, invernadero con estufa, riego por goteo y espantapájaros.
- **Las losas de las parcelas** (prueba del 16/10: «al acercar y alejar aparecen y desaparecen»): el suelo pisado del recinto ya no lleva `polygonOffset`, que lo ponía por delante de las losas desde la distancia de la vista general. Ahora va por alturas: nieve a −0,02, suelo pisado a 0 y la cara de arriba de las losas a +0,02. Con eso basta para el búfer de profundidad a cualquier zoom.
- **La Cuarentena** (`quarantine`; prueba del 15/10, #13): «una nave aparte, con cerrojo por fuera». Un contenedor hecho celda, con las puertas cerradas por una barra con candado por fuera, una luz roja encima, barreras de hormigón alrededor y un poste con el aviso. A nivel 2, la silla del guardia en la puerta, su lámpara y bidones de agua. A nivel 3, la chimenea de la estufa humeando en el techo y una radio sobre una caja.
- **El valle de Kessra, el mapa de Arturo** (15/10: «este va a ser el mapa»; desde la 0.2.8, en lugar del Ilustrado):
  - **una pintura vista desde arriba,** al aire de Fortnite: `assets-src/valle/mapa-valle.png` (1536×1024, no se sube); en el juego, `public/maps/valle/mapa.jpg` (1 MB). Ocupa x de −15 a 15 y z de −10 a 10, a 51,2 píxeles por unidad;
  - **dónde está cada cosa** (`valleyGeography.ts`), sobre lo que se ve en la pintura:
    - las 8 zonas: Atalaya, el recinto con agua del noroeste; Brena, el pueblo de al lado; Carvik, a la izquierda; La Vega, en el cruce del río; El Pantano Gris, a la orilla del lago de arriba; Velsa, arriba a la derecha; Galen, en la rotonda de la ciudad grande; Corran, en la de la ciudad de abajo; Los Llanos de Ceniza, en las grandes obras del sur. El motor tiene las mismas x/z (`REGIONS`);
    - los lugares de Lore (`LANDMARKS`), cada uno junto a algo que se ve: la mina sobre la colina con su cinta, la presa en la boca del lago, la central junto al río, el peaje en la carretera del norte…;
  - **las carreteras de la Marea y de La Lata van por las de la pintura:** `tools/valle/mapa.ts` (`npx tsx packages/client/tools/valle/mapa.ts`) busca el camino más corto sobre sus píxeles de calzada (claros y sin color) entre cada par de zonas del grafo del motor, con algún punto de paso donde el más corto iría por otro lado, y lo guarda en `valleyRoads.ts`. Además convierte la imagen. No se pintan carreteras encima: se ven las de la pintura;
  - **la pintura se ve siempre entera** (prueba del 15/10, #1 y #2): sin desenfoque, nada reflejado alrededor y sin zoom. Alrededor, el fondo liso de la mesa (el acero de los paneles);
  - **en vivo encima,** solo:
    - la Marea, en el sombreador del suelo: el suelo apagado y teñido de violeta, un velo que se mueve, y el borde con su brillo y una línea clara de pocos píxeles a cualquier tamaño. Sube por las carreteras con la infestación, con los umbrales del motor; en el menú, solo la franja del sur;
    - por debajo de la Marea, en el mismo sombreador (Lore, «Atalaya cayó», 18/10): Atalaya, la estación amurallada de la pintura, quemada (tejados y patios negros, tizne, brasas que titilan, el agua cenicienta, humo que se va al este; `ATALAYA_WORKS`), con su nombre (`LANDMARKS.ruins`); y el Bastión, un claro en el pinar de encima, sobre Brena (`BASTION`): un prado seco rodeado de pinos más oscuros, su cruz de pistas y la caravana en medio, bajo la casa del mapa. Sin llamadas de más;
    - los marcadores y los nombres, en HTML (`valley.css`): chincheta por zona (azul con contacto, violeta bajo la Marea, amarilla la elegida, con su anillo), Nido, operaciones con el mismo marcador que la lista, rumor (verde si se escucha), Atalaya, La Lata (`BUS.name`) por las carreteras y los lugares de Lore, que solo salen si el mapa es lo bastante grande;
  - **la geografía es la del motor** (REGIONS, ROADS); La Lata sigue `roadRoute`;
  - **el valle cabe siempre entero entre los paneles:** el encuadre lo mide el propio valle cada pocos fotogramas, con los paneles como estén (entran con animación). Se mide sin la ficha de la derecha y con el panel izquierdo desplegado, así que abrir una ficha no cambia el tamaño del mapa;
  - **la ficha no tapa lo elegido:** con una ficha abierta, si la zona elegida queda a menos de 150 px de ella, el mapa se desliza a la izquierda lo justo (sin dejar ver la mesa entre el mapa y la ficha, ni salirse por la izquierda de la ventana). Se queda así mientras la ficha siga abierta, aunque se elija otra zona, para que no vaya y venga, y vuelve al centro al cerrarla. Mientras tanto, lo de la izquierda puede quedar bajo el panel de operaciones;
  - **ni rueda ni arrastre:** con la pintura entera no hay nada que ampliar ni mover. Solo los laboratorios encuadran más de cerca (`look`);
  - **coste:** un plano y una textura, dos llamadas de dibujo, sin postproceso (`plain`). Telemetría como siempre (`globo` en `red.log`).
- **`stage.ts`:** los sets pueden hacer su propio zoom (`zoom`; hoy ninguno lo usa) y conocer el escenario (`attach`), y `frameNow()` dibuja un fotograma al momento para capturarlo. Una barra mucho más ancha que alta y a pocos píxeles del borde de arriba o de abajo cuenta como ese borde. La de navegación flota 6 px y se tomaba por un panel lateral, lo que encogía los encuadres de la base y del valle.

### El tráiler, la portada y las capturas ([TRAILER.md](TRAILER.md); `lab/trailer/cine.ts`, `sala.ts`, `shots/`)

- **Las tomas de Arte** (`src/lab/trailer/shots/<id>.ts`, con la interfaz de Red: `setup`, `frame(t)` y duración). Las graba la página de Red, `trailer.html`, con su reloj virtual.
  - calle, horda y atalaya: el juego sin reglas (`cine.ts`, `mapScene`) o la base (`BastionSet`, con su despegue). La horda son 45 figuras a la velocidad del juego, con gemidos y pasos.
  - Los diez planos del montaje y la evacuación son batallas de verdad (`sala.ts`, `LocalBattle`): un `Game` con una red falsa. Las órdenes de los planos de Gameplay (`.dev-snapshots/trailer/partidas/tomas/<id>.json`) pasan por `executeCommand` con una sola `Rng(seed)` y van a `game.onEvents`.
    - Sin HUD, aros ni superposiciones, y la cámara es de cine (`cameraPath`, por puntos clave).
    - `lead` adelanta el plano a la acción, `times` cambia los tiempos (nunca el orden) para que quepa en 2 s y `rate` cambia la «Velocidad del combate» a mitad de plano. `missionEnded` no se juega.
    - De cerca, el golpe no tiñe el cuerpo (`UnitViews.flashScale = 0`) y el halo del impacto es pequeño (`Fx.impactGlow = 0.3`): con el bloom, el zombi alcanzado se veía entero de rosa. En el juego los dos siguen a 1.
  - valle: el mapa del juego acercándose mientras sube la Marea.
  - **El horizonte** (`backdrop`): más allá del mapa el juego solo tiene suelo, y la neblina lo dejaba en una franja gris. Calle, horda y portada llevan siluetas de ciudad (bloques bajos cerca y más altos lejos, oscuros, que la neblina aclara con la distancia) y el humo de un incendio.
- **Luces nuevas del `Renderer`** (`setTime`): `dawn` (el amanecer de la calle: sol bajo y naranja, neblina cálida) y `dusk` (la portada: sol bajo del oeste, cielo de azul a naranja). `closeUp` quita la oclusión ambiental: de cerca, lo de detrás de los personajes se veía a través de ellos.
- **La portada** (`shots/portada.ts`, `?toma=portada&ver&ss=2`): la partida de Gameplay recolocada para la foto (no se juega nada). La escuadra en el borde de un tejado, la horda en la calle, La Lata al fondo, al atardecer; el cielo de arriba queda libre para el logo. `__portada.save()` la guarda a 3840×2160 en `.dev-snapshots/trailer/portada/keyart.png`.
- **Las capturas de combate** (`.dev-snapshots/trailer/capturas/combate/`): combate, horda, noche, área de servicio, polígono y evacuación, a 1920×1080, en partidas de Gameplay en mi servidor. Las dos plazas están conectadas, con el HUD y la cámara del juego (su inclinación y giros de 45°) y el modo de disparo abierto.

### Efectos y renders (`game/fx.ts`, `songFx.ts`, `lab/renders/`)

- **Efectos,** con el aspecto de Particle FX: humo, polvo y gas en bocanadas facetadas, sangre en gotas planas, fogonazos en estrellas y fuego en lenguas de llama. La luz (fogonazos, trazadoras, chispas) queda como brillo suave. Aparte, los del Oficio (`songFx.ts`).
- **Renders para la interfaz:** cosas del juego como modelos de Synty, en vista de 3/4 y sobre fondo transparente, cada una con su silueta para teñir con CSS. Salen a `public/ui/synty/renders/`. Hay clases, recluta, molotov, La Mula (tres vistas), botiquín, ración, piezas, camposanto, roca y ruinas.

## Laboratorios

Se abren con el cliente de pruebas (el mío, en el 5197). Todos usan el código del juego y su luz.

| Página | Para qué |
|---|---|
| `/referencia.html` | Una misión generada con la cámara del juego, para comparar con las capturas de Synty y medir un fotograma: CPU, GPU, llamadas, triángulos y objetos. `__ref.bench(300)` dibuja 300 fotogramas sin tope. Con `?biome=city&seed=7&night&fog` y `?map=torre` se elige el mapa |
| `/hordas.html` | Lo que cuestan N zombis por fotograma, por partes (clips, transferencia, pose, escena, render). `__horde.run([10, 20, 40, 60, 80])`, y `__horde.floor(40)` estima lo que ahorraría hornearlos |
| `/modelos.html` | Todos los personajes en una pose (`#idle`, `#aim`, `#run`, `#crouch`, `#search`, `#hit`, `#dead`) y los retratos de la radio |
| `/armas.html` | Cada arma en las manos de cada cuerpo, pose a pose. `__weapons.sheet(...)` guarda una hoja |
| `/efectos.html` | Cada efecto del combate y del Oficio sobre un superviviente y tres zombis (`#molotov`, `__fx.play('bengala')`) |
| `/valle.html` | El valle solo, como en la campaña, con las llamadas, los triángulos y los milisegundos de cada fotograma. Teclas 0 (entero), 1–8 (cada zona de cerca) y M (fondo del menú); `?doom=6` para la Marea, `?contact` para contactarlo todo. `__valle.look(x, z, zoom)` (solo el laboratorio encuadra más de cerca) y `__valle.snap(nombre)` (el mapa, sin los nombres) |
| `/renders.html` | Los renders de la interfaz. `__renders.save()` los manda a `.dev-snapshots/` |
| `/sonido.html` | Todos los sonidos (son de Red y de UX/UI) |

Las capturas de los laboratorios van a `.dev-snapshots/`, que no se sube. Con el panel del navegador oculto, `?timer` hace que los fotogramas avancen igual.

## Pendiente

- **Medir en el PC con la RTX 3090:** la telemetría de una misión (`data/red.log`) dice si hace falta lo siguiente.
- **Lo que pedirán las reglas más adelante:** saltar desde ventanas altas, suelos y paredes destructibles, escaleras que solo se dejan por un lado.
- **Escenarios, después de la 0.2.6** (acordado con Gameplay): la cafetería propia (hoy es una tienda de ciudad con tablas) y el portón grande de la nave industrial (7,5 m: un hueco de 5 casillas en el plano); hasta entonces, las puertas de las naves son de metal de búnker.
- **Cárcel y centro comercial:** esperan a los atlas de «Police Station» y «Shops».
- **La fábrica:** una carretilla elevadora, si aparece en algún pack (el decorado `forklift` sigue en el motor, sin usar).
- **Restos del diorama del valle:** los modelos `map*` del kit de la base (`syntyManifest.ts`) ya solo los usaba el valle 3D. Quitarlos del manifiesto y sus GLB de `public/` cuando se confirme el valle pintado.
- **Sacar el arma:** la pistola y el cuchillo o el machete aparecen en la mano de golpe, al empezar el gesto. Si en la prueba se nota, la mano va antes a la funda o a la cadera (unos 0,15 s).
- **Los cuerpos y el decorado pequeño:** la caída esquiva paredes y cobertura alta, pero no un banco ni un buzón (no son casillas que tapen).
- **Para UX/UI, si lo pide:** la venda en render ([UX-CARENCIAS.md](UX-CARENCIAS.md)).
- **Si las etiquetas de los soldados aún molestan** con la escuadra junta: apartar en vertical las que se solapen. Por ahora, la del seleccionado y la del señalado van por encima de las demás (`renderOrder` en `unitViews.ts`).
