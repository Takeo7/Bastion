# Traspaso: estado del proyecto y cómo seguir en otra máquina

Escrito el 2026-10-14 con la versión 0.2.1 y puesto al día el mismo día con la 0.2.5. Recoge lo hecho, las decisiones tomadas y lo que hay que llevar a mano a otra máquina, porque no está en GitHub. Las normas de trabajo están en [CLAUDE.md](../CLAUDE.md); los roles, en [EQUIPO.md](EQUIPO.md).

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

   Deben pasar 361 tests.
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

   Sale en `packages/desktop/dist/release/`. En Windows también se construye el instalador.
7. **Convertir modelos de Synty** (solo si se van a añadir más): hace falta **Blender**, y `assets-src/` copiado. Siempre con ids explícitos (ver `packages/client/tools/synty/README.md`).

## 3. Estado (versión 0.2.5)

- **Juego:** combate por turnos cooperativo para dos, con el mando que se cede. La campaña tiene base, el valle, misiones, clases con árbol de habilidades, heridas y muerte permanente.
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
  - releases v0.1.0, v0.2.0 y v0.2.1 en GitHub (privadas); de la 0.2.2 a la 0.2.5 no están publicadas: el instalador se construye en el PC.
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
- **Rendimiento:** la telemetría de cada navegador va a `data/red.log` (o `%APPDATA%\Bastion\red.log` en la aplicación).

## 4. Decisiones importantes (y por qué)

- **Arte:**
  - **Solo Synty POLYGON** (Arturo tiene la suscripción), Kenney CC0 o lo nuestro. Nada de vóxel ni de otros packs.
  - El aspecto objetivo son las capturas promocionales de Synty.
- **Rendimiento:** el PC de referencia es Windows con una RTX 3090; objetivo ≥240 fps en «alto» (≤4,2 ms por fotograma). El Mac es el mínimo («medio» ≥30). El aspecto no se recorta por el Mac: para eso están los ajustes.
- **Plataforma:** Three.js y web, más la aplicación de escritorio en Electron (Tauri no: en Mac usa WebKit). WebGPU, más adelante.
- **Tema y tono** ([LORE-PROPUESTA-2026-10-13.md](LORE-PROPUESTA-2026-10-13.md)):
  - supervivencia zombi clásica;
  - premisa: Atalaya, una vieja estación militar en la sierra de Aldren, con supervivientes de todas partes;
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
  - la campaña empieza con una misión.
- **Clases:** el «Árbol de habilidades» es común a todos. Tiene 5 aptitudes (la quinta es «Maña») y la rama del Oficio, que se abre con el Taller de oficios.
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
  - probar la 0.2.5 en el PC con la RTX 3090, con la guía [PRUEBA-PC-3090.md](PRUEBA-PC-3090.md), y mandar el `red.log` del anfitrión;
  - escuchar los sonidos nuevos (`sonido.html`).
- **Probar en partida** lo nuevo de la 0.2.3 y la 0.2.4: si las misiones de zombis se quedan cortas o se pasan, si los 3 cargadores aprietan lo justo, y si el ritmo y los pasos (zombi a 1,02 s/casilla; turnos de ~30 s con 6–7 a la vista) están bien. Lo más fácil de ajustar: `GAITS` en `game/gaits.ts`.
- **Para más adelante:**
  - firmar la aplicación (cuenta de desarrollador de Apple) y Steam;
  - horneado de animaciones para hordas grandes, si la telemetría lo pide.
- **Textos pendientes de revisar por Lore:** [TEXTOS-PENDIENTES.md](TEXTOS-PENDIENTES.md).

## 6. Mapa de los documentos

| Documento | Qué tiene |
|---|---|
| [CLAUDE.md](../CLAUDE.md) | Normas para cualquier sesión de Claude |
| [EQUIPO.md](EQUIPO.md) | Roles, archivos de cada uno, puertos de prueba, normas y lo que está en marcha |
| [DESIGN.md](DESIGN.md) | Arquitectura, mecánicas, hoja de ruta y limitaciones conocidas |
| [LORE.md](LORE.md) | El mundo, los zombis, las zonas y los recursos |
| [ARTE.md](ARTE.md) | Dirección de arte y la tubería de Synty |
| `FASE2.md`…[FASE7.md](FASE7.md), [ALTURA-Y-NOCHE.md](ALTURA-Y-NOCHE.md), [ZOMBIS-PRIMERO.md](ZOMBIS-PRIMERO.md) | Especificación de cada fase y del reparto de enemigos |
| [PRUEBA-2026-10-12.md](PRUEBA-2026-10-12.md), [PRUEBA-2026-10-13.md](PRUEBA-2026-10-13.md) | Notas de las pruebas de juego y qué se hizo con cada una |
| [ux/FUENTES.md](ux/FUENTES.md), [UX-CARENCIAS.md](UX-CARENCIAS.md) | Licencias de tipografías y sonidos; huecos de los packs de interfaz |
