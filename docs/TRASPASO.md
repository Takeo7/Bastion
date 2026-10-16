# Traspaso: estado del proyecto y cómo seguir en otra máquina

Escrito el 2026-10-14 con la versión 0.2.1 y puesto al día el 16/10 con la 0.3.1. Recoge lo hecho, las decisiones tomadas y lo que hay que llevar a mano a otra máquina, porque no está en GitHub. Las normas de trabajo están en [CLAUDE.md](../CLAUDE.md); los roles, en [EQUIPO.md](EQUIPO.md).

## 1. Qué no está en GitHub y hay que copiar a mano

| Qué | Dónde está | Hace falta para | Cómo |
|---|---|---|---|
| Packs originales de Synty (unos 4,6 GB) | `assets-src/synty/` | Convertir modelos nuevos | Copiar la carpeta tal cual (disco o red). **Nunca a GitHub**: la licencia lo prohíbe |
| Capturas de referencia de Synty | `assets-src/referencias/aspecto-synty/` | El aspecto objetivo | Va dentro de `assets-src/` |
| La partida de los jugadores | `data/save.json` (y las copias `save.antes-*.json`) | Seguir la campaña donde la dejasteis | Copiar `data/`. Sin él, el servidor empieza sin partida |
| Certificados https para el compañero | `data/tls/` | Que Safari se conecte por https | Opcional: si falta, el servidor los crea de nuevo con openssl. En Windows no hay openssl: solo http, que con Chrome, Edge o la aplicación va bien |
| Configuración de los servidores de prueba | `.claude/launch.json` | Que las sesiones de Claude arranquen sus servidores | Copiarla, o rehacerla con la tabla de puertos de EQUIPO.md |
| La memoria de Claude | `~/.claude/projects/<carpeta del proyecto>/memory/` | Preferencias y decisiones de las sesiones | Opcional: lo importante está en este documento y en CLAUDE.md. Para copiarla, la carpeta destino lleva la ruta del proyecto en la nueva máquina, con `/` cambiadas por `-` |

Lo demás, código, modelos convertidos, sonidos, iconos y documentos, viene con `git clone`.

## 2. Poner la máquina en marcha

1. `git clone` de `github.com/Takeo7/Bastion` (privado: hace falta acceso).
2. **Node.js** 22 o superior. Aquí se usa la 26.
3. Instalar las dependencias:

   ```bash
   npm install
   ```

4. Comprobar que todo compila y que los tests pasan:

   ```bash
   npm run typecheck
   ```

   ```bash
   npx vitest run --root packages/engine
   ```

   Deben pasar 384 tests.
5. **Jugar en el navegador:**

   ```bash
   npm run build
   ```

   ```bash
   npm start
   ```

   El servidor sale en 3000, y en 3001 con https para el compañero.
6. **Aplicación de escritorio:**

   ```bash
   npm run desktop:dist
   ```

   Sale en `packages/desktop/dist/release/`. Las versiones de Windows (`npx electron-builder --config electron-builder.json --win`) y de Linux (`--linux`: AppImage y tar.gz) se construyen también desde el Mac, en `packages/desktop` ([PRUEBA-PC-3090.md](PRUEBA-PC-3090.md), paso 0); cómo va la firma, en [FIRMA.md](FIRMA.md).
7. **Convertir modelos de Synty** (solo si se van a añadir más): hace falta **Blender**, y `assets-src/` copiado. Siempre con ids explícitos (ver `packages/client/tools/synty/README.md`).

## 3. Estado (versión 0.3.1)

- **Juego:** combate por turnos cooperativo para dos, con el mando que se cede. La campaña tiene base, el valle, misiones, soldados que aprenden de lo que hacen ([FASE9.md](FASE9.md)), heridas y muerte permanente.
- **Tema (zombis), las 5 fases hechas** ([LORE.md](LORE.md)):
  - Saqueo, ruido e IA de zombis ([FASE2.md](FASE2.md));
  - contagio y medicinas ([FASE3.md](FASE3.md));
  - comida, Huerto, piezas, armas fabricadas o encontradas ([FASE4.md](FASE4.md));
  - el valle de Kessra con carreteras y la Marea por zonas ([FASE5.md](FASE5.md)).
- **Además:**
  - noche y altura de los edificios ([ALTURA-Y-NOCHE.md](ALTURA-Y-NOCHE.md));
  - edificios de varias plantas que se pisan ([FASE6.md](FASE6.md), [FASE6-PLAN.md](FASE6-PLAN.md)).
- **Arte:**
  - todo Synty POLYGON: personajes con animaciones, mapas en los que se entra, dos estilos de ciudad (cuadrícula y pueblo viejo), la base, el mapa del valle y los efectos;
  - lo vóxel está borrado.
- **Interfaz:** «Mixta» es la única: paneles de acero, iconos, movimiento y el árbol de habilidades nuevo. Cada jugador ve la suya.
- **Aplicación de escritorio** (Electron):
  - servidor dentro, «Ser anfitrión» y «Unirse», partidas en la red local, icono;
  - en Windows, la regla del cortafuegos;
  - releases v0.1.0, v0.2.0, v0.2.1, v0.2.6, v0.2.7, v0.2.8, v0.2.9, v0.3.0 y v0.3.1 en GitHub (el repositorio es público desde el 6/10, y Arturo lo sabe: las releases también lo son); de la 0.2.2 a la 0.2.5 no están publicadas. Las builds de Mac, Windows y (desde la 0.2.7) Linux se construyen todas desde el Mac; la de Mac va con firma ad hoc ([FIRMA.md](FIRMA.md)). La de Linux sale sin probar en un Linux de verdad.
- **Lo nuevo de la 0.2.2** (2026-10-14):
  - un velo de carga al entrar en combate, y la interfaz cabe en ventanas de 1024 px;
  - el estado viaja comprimido por la red (de ~110 KB a ~9 KB por misión) y el panel «Comandantes» solo enseña direcciones útiles;
  - el valle con relieve, campos, pueblos y carreteras; en combate, luz más cálida, suciedad y plazas con árboles y escombros;
  - los textos que quedaban del globo y de XCOM, fuera; nombres que chocaban, cambiados.
- **Lo nuevo de la 0.2.3** (2026-10-14):
  - los zombis primero ([ZOMBIS-PRIMERO.md](ZOMBIS-PRIMERO.md)): las misiones normales son de los muertos; los humanos, solo en las suyas;
  - la fase 7 ([FASE7.md](FASE7.md)): saltar por ventanas altas, escaleras que solo se dejan por el rellano, munición contada (3 cargadores), paredes y suelos que rompen las granadas.
- **Lo nuevo de la 0.2.4** (2026-10-14), lo que pidió Arturo: «el turno de la IA va muy rápido, corren muy rápido, no se ve bien la animación de andar ni correr, y al morir se desploman como un tronco»:
  - pasos sin patinar y por plantilla (`game/gaits.ts`): el soldado trota en el azul (0,50 s/casilla) y esprinta en el amarillo (0,24); el zombi anda arrastrándose (1,02) con el andar de Goblin Locomotion;
  - muertes de verdad (Bow Combat y Sword Combat), según de dónde viene el golpe y con sitio para caer; posturas de reposo (Idles); Tajo con arco y mordisco que agarra; escaleras que se suben andando;
  - el turno enemigo se ve entero ([UX-RITMO.md](UX-RITMO.md)): sin la aceleración por cola, la cámara se asienta en cada enemigo, el resultado se lee, y los muertos de un grupo que solo andan lo hacen juntos (y la IA los mueve antes que a los que muerden);
  - «Velocidad del combate» en Ajustes (Lenta, Normal, Rápida), y la barra espaciadora acelera el turno enemigo.
- **Lo nuevo de la 0.2.5:** reacciones a los golpes según de dónde vienen (Bow Combat y Sword Combat, de cintura para arriba) y puertas que giran sobre la bisagra al abrirse y cerrarse (`game/doors.ts`).
- **Lo nuevo de la 0.3.1** (2026-10-16), la PR de Ana ([Takeo7/Bastion#1](https://github.com/Takeo7/Bastion/pull/1)), integrada sobre la 0.3.0:
  - centros médicos rehechos (consultorio, enfermerías y hospitales de ciudad de tres plantas), con mobiliario por sala y abandono: cadáveres, sangre, suciedad, material tirado; todo decorado, sin cambiar cobertura ni paso;
  - escaleras interiores con peldaños de madera, separadas de las paredes y con el acceso asegurado;
  - el capítulo 1, «El consultorio de Brena», con nieve y la presentación aprobada (versión 05 del PDF); la escuadra alineada en la Cochera;
  - sus objetos van del 51 al 71 (del 45 al 50 son los de la fábrica de la 0.3.0).
- **Lo nuevo de la 0.3.0** (2026-10-16), «Se aprende haciendo» ([FASE9.md](FASE9.md)), con campaña nueva:
  - sin clases, aptitudes ni rangos: cuatro habilidades (Tiro, Cuerpo a cuerpo, Medicina, Maña) que suben con lo que cada soldado hace en las misiones, sin tope y con niveles largos; talentos de dos en dos en los niveles 2 y 4; la veteranía, de Novato a Superviviente, da vida, voluntad y la tercera acción;
  - el cinturón de dos huecos (sin granadas en él, no hay granadas); las armas sin niveles, con estado (gastada, rota) que se arregla con piezas, y mejoras (mira, culata, cargador ampliado, silenciador) que se encuentran o se hacen en el Taller;
  - una campaña de antes se guarda aparte (`save.antes-0.3.json`) y el vestíbulo lo dice;
  - la escaramuza: armas al azar, y con «Veteranos», habilidades de 2 a 4.
- **Lo nuevo de la 0.2.9** (2026-10-15), las notas de la prueba de ese día ([PRUEBA-2026-10-15.md](PRUEBA-2026-10-15.md)):
  - turnos de uno en uno: cada soldado gasta sus acciones (o termina) y pasa el mando al compañero, que mueve uno de los suyos; se puede ceder (`GameState.activeUnit`, evento `unitActive`). Evacuar se pide y se acepta desde la barra, sin ventana;
  - la horda llega por los bordes cada turno, el doble mientras suena la alarma de un sabotaje; los muertos, con 2 de vida más;
  - el mordido pierde 1 de vida por turno y no sube a La Lata sin la Cuarentena, la instalación nueva (`quarantine`);
  - el sabotaje calla un coche patrulla, una ambulancia o la alarma de una nave (`alarm`), con su modelo, sus luces y su sonido; el coche ocupa dos casillas (`sirenCar`);
  - la fábrica, rehecha (el bioma `facility`, `factory` en mapgen.ts): la entrada con las oficinas, la nave principal con muelles y patio de maniobra, el patio de contenedores, silos y aparcamiento, palés y bidones, y ninguna explanada sin cobertura;
  - el rescate nombra a quien está retenido y el mapa se llama como la operación; las medicinas, solo en tiendas y laboratorios;
  - Aldren pasa a llamarse Brena y Volkren, Velsa (Lore);
  - interfaz: repaso con los packs de Synty (marcos de acero «a», placas de listos, iconos del valle de una sola familia), el busto del soldado de verdad en el árbol, el Diario solo con «Entendido» y sin radio en misión;
  - el valle entero en la ventana, sin desenfoque ni zoom; los zombis ya no van y vuelven al andar; la granada mata a la vez.
- **Lo nuevo de la 0.2.8** (2026-10-15):
  - el valle es el mapa de Arturo (una imagen suya, `assets-src/valle/mapa-valle.png`, convertida a `public/maps/valle/mapa.jpg`): las zonas, Atalaya y los sitios de Lore están sobre ella, y la Marea y La Lata van por sus carreteras (`strategy/valleyRoads.ts`, generado con `packages/client/tools/valle/mapa.ts`). Las posiciones de REGIONS son las mismas que en el mapa;
  - en Linux, también un .deb (Ubuntu y Debian), que prepara el aislamiento de Chromium y AppArmor ([FIRMA.md](FIRMA.md)).
- **Lo nuevo de la 0.2.7** (2026-10-15):
  - el valle, rehecho de cero: un mapa ilustrado visto desde arriba (Arte), horneado en teselas (`public/maps/valle`) con la Marea, los marcadores y los rótulos encima; elegido por Arturo entre tres direcciones;
  - todos los zombis eran el mismo cuerpo (el obrero del chaleco): ahora salen de los diez comunes y los de corredores y acorazados;
  - los informes de las operaciones ya no nombran un sitio distinto del de la operación (Lore);
  - versión para Linux (AppImage y tar.gz);
  - las herramientas del tráiler, la portada y las capturas de la ficha ([TRAILER.md](TRAILER.md)): los vídeos e imágenes no van al repositorio.
- **Lo nuevo de la 0.2.6** (2026-10-14), las notas de la prueba de ese día ([PRUEBA-2026-10-14.md](PRUEBA-2026-10-14.md)):
  - evacuar es conjunto: desde La Lata (el autobús, aparcado junto a la zona) uno lo pide y el otro acepta; quien se queda fuera se pierde. Una misión se gana al irse, no al matar a todos (salvo Limpieza);
  - la fase 8 ([FASE8.md](FASE8.md)):
    - armas desde cero: todos salen con cuchillo y pistola, y las armas se encuentran, se fabrican o llegan con el nivel del Bastión; cualquiera lleva cualquiera;
    - registrar por habitaciones, una vez para toda la escuadra, y cajas especiales marcadas (armas, medicinas, munición, comida);
    - la mochila: soltar, coger, dar, usar medicinas y equipar armas en la misión;
  - el triple de zombis, y la mitad de las cajas, barricadas, papeleras y jardineras sueltas;
  - dos escenarios nuevos con los packs de Woodland y de la nave industrial (`GameState.site`): el área de servicio en la carretera del bosque y el polígono industrial. Salen solos en la campaña y se eligen en la escaramuza;
  - por las ventanas se ve a través de dos como mucho;
  - una propuesta o una petición de evacuar sin respuesta se acepta sola a los 90 s;
  - el árbol de habilidades nuevo, más orgánico;
  - sin días de viaje a las misiones; más acciones al subir mucho de rango; reclutas algo distintos;
  - hordas horneadas: 45 o 90 zombis cuestan lo mismo que 15.
- **Rendimiento:** la telemetría de cada navegador va a `data/red.log` (o `%APPDATA%\Bastion\red.log` en la aplicación).

## 4. Decisiones importantes (y por qué)

- **Arte:**
  - **Solo Synty POLYGON** (Arturo tiene la suscripción), Kenney CC0 o lo nuestro. Nada de vóxel ni de otros packs.
  - El aspecto objetivo son las capturas promocionales de Synty.
- **Rendimiento:** el PC de referencia es Windows con una RTX 3090; objetivo ≥240 fps en «alto» (≤4,2 ms por fotograma). El Mac es el mínimo («medio» ≥30). El aspecto no se recorta por el Mac: para eso están los ajustes.
- **Plataforma:** Three.js y web, más la aplicación de escritorio en Electron (Tauri no: en Mac usa WebKit). WebGPU, más adelante.
- **Tema y tono** ([LORE-PROPUESTA-2026-10-13.md](LORE-PROPUESTA-2026-10-13.md)):
  - supervivencia zombi clásica;
  - premisa: Atalaya, una vieja estación militar en la sierra de Brena, con supervivientes de todas partes;
  - tono adulto y seco, sin explicar de más, con nombres internacionales.
- **Nombres:**
  - pestañas «Bastión», «El valle», «Diario», «Armería», «Cuartel», «Instalaciones», «Cochera»;
  - el tiempo pasa con «Esperar noticias»; la amenaza se llama «Infestación» (y «la Marea» desde el nivel 4).
- **Los zombis primero** (14/10, [ZOMBIS-PRIMERO.md](ZOMBIS-PRIMERO.md)): las misiones normales son de los muertos; los humanos solo en Rescate y en los capítulos y encargos que son suyos.
- **Reglas** (de las pruebas de juego del 12 y del 13/10):
  - disparar gasta una acción;
  - la clase llega en Capitán;
  - no hay refuerzos humanos: los zombis solo entran por los bordes;
  - abrir puertas es gratis;
  - la campaña empieza con una misión;
  - (14/10) evacuar es de los dos y se gana al irse; la clase no trae arma; registrar es por habitaciones.
- **Progresión** (0.3.0, [FASE9.md](FASE9.md)): sin clases; se aprende haciendo. Lo guardado en el árbol queda fijo (no hay reentrenar). El Taller de oficios hace que cada misión enseñe un 20 % más por nivel.
- **Equilibrio:**
  - la Marea cae por zonas a 4/6/9/10/11 de infestación;
  - empieza con 16 raciones;
  - disparar desde más alto da +20 de puntería; la IA de los vivos valora la altura con +6 por planta al elegir sitio;
  - subir o bajar una planta por la escalera cuesta como 2 casillas; la IA cuenta 3 casillas de rodeo por planta.
- **Interfaz:** Mixta, votada por Arturo. La cuadrícula del suelo, opción A (solo contornos).
- **Cómo se juega:** Arturo y Ana juegan con la aplicación de escritorio; la web queda para probar (por eso Safari no se prueba).
- **Sesiones de Claude:** Gameplay (coordina), Arte, Lore, UI y Networking, todas con Remote Control para seguirlas desde claude.ai/code ([EQUIPO.md](EQUIPO.md)).

## 5. Pendiente

- **De Arturo y Ana:**
  - probar la 0.3.0 en el PC con la RTX 3090, con la guía [PRUEBA-PC-3090.md](PRUEBA-PC-3090.md), y mandar el `red.log` del anfitrión;
  - escuchar los sonidos nuevos (`sonido.html`).
- **Probar en partida** la 0.3.0: si se aprende a buen ritmo y si las armas se gastan lo justo. Lo más fácil de ajustar, en `skills.ts`: `SKILL_XP`, `DEED_XP` y `BASE_AIM`; en `content.ts`: `wearOf`, `WORN_BELOW` y `REPAIR_PER_PART`. Y lo de antes: `podPlan` en `setup.ts`, `PISTOL_CLIP` y los precios del Taller (`CRAFTS`).
- **El tráiler:** aparcado por Arturo (15/10). El último borrador, la portada y las 10 capturas de la ficha están en `.dev-snapshots/trailer/` (no se suben); las herramientas siguen en `packages/client/tools/trailer/` ([TRAILER.md](TRAILER.md)) por si se retoma.
- **Escenarios:** la cárcel y el centro comercial esperan los packs «POLYGON – Police Station» y «POLYGON – Shops», que tienen los atlas de sus piezas. Para después: la cafetería con aspecto propio y el portón grande de las naves (un hueco de 5 casillas).
- **Para más adelante:**
  - firmar la aplicación (cuenta de desarrollador de Apple; los pasos, en [FIRMA.md](FIRMA.md)) y Steam;
- **Limpieza del código muerto** del árbol viejo (habilidades del Grillo y del Oficio, sus perks, los vuelos del dron): Gameplay, UI y Arte, en una pasada aparte ([FASE9.md](FASE9.md) §9).
- **Textos pendientes de revisar por Lore:** [TEXTOS-PENDIENTES.md](TEXTOS-PENDIENTES.md).

## 6. Mapa de los documentos

| Documento | Qué tiene |
|---|---|
| [CLAUDE.md](../CLAUDE.md) | Normas para cualquier sesión de Claude |
| [EQUIPO.md](EQUIPO.md) | Roles, archivos de cada uno, puertos de prueba, normas y lo que está en marcha |
| [DESIGN.md](DESIGN.md) | Arquitectura, mecánicas, hoja de ruta y limitaciones conocidas |
| [LORE.md](LORE.md) | El mundo, los zombis, las zonas y los recursos |
| [ARTE.md](ARTE.md) | Dirección de arte y la tubería de Synty |
| `FASE2.md`…[FASE8.md](FASE8.md), [ALTURA-Y-NOCHE.md](ALTURA-Y-NOCHE.md), [ZOMBIS-PRIMERO.md](ZOMBIS-PRIMERO.md) | Especificación de cada fase y del reparto de enemigos |
| [TRAILER.md](TRAILER.md) | El tráiler, la portada y las capturas de la ficha: guion, quién hace qué y cómo se rehace |
| [PRUEBA-2026-10-12.md](PRUEBA-2026-10-12.md), [PRUEBA-2026-10-13.md](PRUEBA-2026-10-13.md), [PRUEBA-2026-10-14.md](PRUEBA-2026-10-14.md) | Notas de las pruebas de juego y qué se hizo con cada una |
| [ux/FUENTES.md](ux/FUENTES.md), [UX-CARENCIAS.md](UX-CARENCIAS.md) | Licencias de tipografías y sonidos; huecos de los packs de interfaz |
| [PRUEBA-PC-3090.md](PRUEBA-PC-3090.md), [FIRMA.md](FIRMA.md) | La guía de la prueba en el PC de referencia; cómo se firma la aplicación y qué cambiar con la cuenta de Apple |
