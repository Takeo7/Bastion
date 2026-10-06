# Bastión (nombre provisional)

Táctica por turnos **cooperativa para dos jugadores**, inspirada en XCOM 2. Cada jugador usa su propio PC y controla sus propios soldados. En el turno de la escuadra juega uno cada vez: quien tiene **el mando** da las órdenes y se lo **cede** al compañero cuando quiere, aunque le queden acciones.

Estado: **prototipo jugable (hitos M1 a M4)**. Combate con alturas, puertas y ventanas en mapas generados al azar; 4 clases con árbol de habilidades; 7 tipos de enemigo, psiónicos incluidos; 5 tipos de misión con patrullas y refuerzos. La campaña cooperativa tiene base en 3D, mapa global, acontecimientos y equipo por soldado. Los modelos están generados por código: soldados, enemigos, el VIP y el repetidor son figuras articuladas, con brazos, piernas y animaciones. Ver [docs/DESIGN.md](docs/DESIGN.md).

## Requisitos

- [Node.js](https://nodejs.org) 22 o superior, solo en el PC del host.
- Un navegador moderno (Chrome, Edge, Firefox o Safari) en ambos PCs.

## Jugar

En el PC del **host**, la primera vez:

```bash
npm install
npm run build
```

Para arrancar una sesión:

```bash
npm start
```

El servidor muestra varias direcciones:

```
Bastión — servidor listo en el puerto 3000
  Tú:              http://localhost:3000
  Tu compañero:    http://192.168.1.42:3000
  Tu compañero:    http://100.101.140.9:3000
```

- **El host** abre `http://localhost:3000`.
- **El compañero** abre una de las direcciones "Tu compañero":
  - En la misma red: la dirección `192.168.x.x`.
  - Por internet: lo más sencillo es [Tailscale](https://tailscale.com). Ambos lo instaláis, entráis con vuestras cuentas en la misma red de Tailscale y el compañero usa la dirección `100.x.x.x`. No hace falta abrir puertos en el router.
- Cada uno escribe su nombre y elegís en el menú principal (con la Tierra de fondo):
  - **Continuar** retoma la campaña guardada; **Nueva campaña** empieza otra: la base compartida, con soldados que suben de rango y mueren de verdad (ver más abajo).
  - **Escaramuza**: una misión suelta, sin consecuencias. Eliges el tipo de misión, el mapa (ciudad, afueras o Imprenta generados al azar, o el Distrito Comercial hecho a mano) y si la escuadra es veterana (sargentos a capitanes con habilidades al azar) o novata.
- El menú muestra las direcciones que tu compañero puede abrir (tu red y Tailscale). Si empiezas una campaña sin él, te lo avisa: cuando entre, tomará el mando de 4 soldados (tus novatos sin estrenar, o reclutas nuevos).
- Si solo hay un jugador, controla a todos los soldados. Al acabar una escaramuza podéis jugar otra o **volver al menú**.

### Misiones

| Tipo | Objetivo |
|---|---|
| **Eliminación** | Abatir a todos los hostiles |
| **Recuperación** | Recoger los datos (basta con pasar por encima) y sacarlos por la zona de evacuación verde antes de que se agoten los 12 turnos. Si cae el portador, los datos quedan en el suelo |
| **Sabotaje** | Destruir el retransmisor (tiene salud y blindaje) en 10 turnos |
| **Pirateo** | Hackear el terminal en 10 turnos: desde una casilla contigua, o con el Grillo, el dron del especialista, a 10 casillas. Si falla, salta la alarma y llegan refuerzos |
| **Rescate** | Llegar hasta el VIP (se une a la escuadra de quien lo alcance) y sacarlo vivo por la evacuación en 14 turnos. Si muere, la misión fracasa |

Con el objetivo cumplido, la misión se gana al abatir a los hostiles o al evacuar al resto de la escuadra. El temporizador se detiene al cumplir el objetivo.

**Patrullas y refuerzos.** Los grupos enemigos patrullan el mapa mientras no os detectan; si una patrulla os ve, se pone a cubierto. Las misiones con objetivo traen refuerzos: una **bengala roja** marca dónde caerán al final del siguiente turno enemigo.

### Terreno

Los mapas generados se montan con parcelas (oficinas, bloques de pisos, plazas, aparcamientos, contenedores, bosques, cabañas, colinas, laboratorios, plataformas…) separadas por calles.

- **Alturas:** azoteas, colinas y plataformas. Solo se sube por **escaleras**; se puede saltar hacia abajo desde cualquier borde. Disparar desde más altura da **+20 de puntería**. Desde arriba se ve por encima de los muros; el borde de una azotea no protege a quien está encima, pero la pared de un bloque sí protege a quien está al pie.
- **Puertas:** tapan la vista hasta que alguien las cruza (se quedan abiertas).
- **Ventanas:** se ve y se dispara a través de ellas, y quien está al lado tiene cobertura alta. Saltarlas cuesta una casilla extra y rompe el cristal.

### Escuadra

En escaramuza cada jugador lleva tres soldados: uno manda asalto, francotirador y especialista; el otro, granadero, asalto y especialista.

| Clase | Arma | Habilidades propias |
|---|---|---|
| **Asalto** | Escopeta (mucho daño de cerca) | **Tajo**: corre hasta un enemigo a su alcance y le ataca con la espada, ignorando la cobertura |
| **Granadero** | Cañón | **Lanzagranadas**: más alcance (14) y más radio; lleva 2 granadas |
| **Francotirador** | Fusil de precisión (necesita las 2 acciones) | **Visión de escuadra**: dispara a lo que vean sus compañeros; **Pistola** para moverse y disparar |
| **Especialista** | Fusil | **Protocolo médico** (cura 4, 2 usos) y **Protocolo de ayuda** (+20 de defensa) con su dron, el Grillo |

Enemigos ([la historia](docs/LORE.md)): **Impreso** (con granada), **Afinado**, **Heraldo**, **Recolector** (cuerpo a cuerpo que aturde: el soldado empieza su siguiente turno con una sola acción), **Gólem** (blindaje 1 y micromisiles), **Cantor** y **Hueco**. Los explosivos rompen el blindaje. Entre todos los enemigos solo pueden usar un explosivo y un ataque psiónico por turno.

**Psiónicos.** El Cantor usa el **Canto** contra la voluntad del soldado (que sube con el rango). Si acierta, el soldado queda **desorientado** (−20 de puntería y sin explosivos), entra en **pánico** (pierde su siguiente turno) o cae bajo **control mental** durante 2 turnos: luchará contra vosotros hasta que pase el efecto o matéis al Cantor. El Cantor también levanta **cadáveres como Huecos** lentos que atacan cuerpo a cuerpo. El Zumbador protege de todo esto.

### Habilidades por rango

Cada clase tiene un árbol de 5 niveles con 2 opciones por nivel; se elige una al subir a Soldado, Cabo, Sargento, Teniente y Capitán. Las marcadas con ◆ son activas y tienen enfriamiento o usos limitados (aparecen en la barra; el número grande es el enfriamiento que queda).

| Clase | Algunas habilidades |
|---|---|
| **Asalto** | Quemarropa, Paso sombrío (sin fuego de reacción), ◆ Correr y disparar, Implacable (matar con el tajo devuelve una acción), ◆ Fuego rápido, Maestro de la espada, Cazador |
| **Granadero** | Destrozador, Gran calibre, ◆ Supresión, Holo-objetivo (+15 para los aliados), Salva (granadas sin terminar el turno), ◆ Disparo en cadena, ◆ Ruptura |
| **Francotirador** | Vigía lejano (vigilancia con visión de escuadra), Desenfunde, ◆ Ojo letal, ◆ Manos rápidas, Muerte desde arriba, ◆ Cara a cara, ◆ Zona letal |
| **Especialista** | Fuego de cobertura, ◆ Protocolo de combate, Evaluación de amenazas, ◆ Protocolo de reanimación, ◆ Restauración, ◆ Descarga de condensador |

## Campaña

Base, créditos y nivel del Bastión son **compartidos**; los soldados son **de cada jugador**: cada uno empieza con cuatro, los asciende, les elige habilidades y los equipa. Los dos usáis la base a la vez, cada uno en su pantalla; veis dónde está el compañero (arriba a la derecha, con un punto de su color en la barra de abajo y una etiqueta sobre su sala).

La campaña va **por fases, como XCOM 2**:

1. **El Bastión.** La base en corte, a pantalla completa: hangar con la nave en superficie, las cuatro salas de mando y debajo la cuadrícula de 3×3 que se excava. Pulsa una sala para entrar (la cámara se acerca) o usa la barra de abajo.
2. **Geoesfera** (desde Mando). El globo a pantalla completa con regiones, misiones y Órganos. **ESCANEAR** corre los días hasta que pasa algo, y entonces salta un **aviso**: misión nueva, obra terminada, hoja nueva del Expediente (al subir de nivel el Bastión), Órgano nuevo, carta del Consejo a fin de mes o avance de la Partitura. Pulsa una misión para ver su **informe** y **Desplegar**.
3. **Selección de escuadra** en el hangar: los soldados formados en 3D delante de la nave, cada uno con su ficha.
4. **Despegue.** Con los dos listos, la escuadra embarca, se abre el techo del hangar y la nave sale. La misión empieza con su presentación (operación, zona y objetivo).
5. **Informe.** Al volver, la escuadra formada en el hangar: heridos, caídos (un memorial en su sitio), ascensos, recompensas y cuerpos recuperados.

Las salas:

- **Mando:** abre la geoesfera.
- **Instalaciones:** la cuadrícula del Bastión. Pulsa una sala para **excavarla** (las de abajo cuestan más) o **construir** en ella. Las obras se hacen de una en una.
- **Cuartel:** el soldado elegido en un pedestal, con sus datos, su equipo su arma y armadura, sus accesorios y reclutamiento (30 créditos; máximo 6 soldados por jugador, 12 si juegas solo). Los ascensos abren la **cuadrícula de habilidades**: una columna por rama y una fila por rango. **Personalizar** cambia el nombre, el apodo (sale en su etiqueta en combate), los colores de armadura, detalles y visor, la cabeza (casco, capucha, boina, gorra, máscara o a cara descubierta) y la complexión, viéndolo en el pedestal mientras eliges. El anillo y la etiqueta siguen con el color de su jugador.
- **Progreso:** el nivel del Bastión, la experiencia que falta y lo que desbloquea cada nivel.
- **Armería:** los niveles de arma y armadura, y la tienda de **accesorios**.

**Progreso: nivel del Bastión.** No hay investigación: el Bastión gana **experiencia en combate** (+2 por cada misión, +3 y su dificultad si se gana, +1 por cada baja; un 25 % más con la sala de simulación) y sube de nivel. Cada nivel añade una hoja al **Expediente CORO**, con lo que el Bastión ha descubierto del enemigo, y desbloquea tecnología, accesorios e instalaciones:

| Nivel | Experiencia | Desbloquea |
|---|---|---|
| 1 | 0 | Granada extra, granada de humo, botiquín · Taller, enfermería |
| 2 | 12 | Granada aturdidora, munición trazadora · Centro de entrenamiento |
| 3 | 30 | **Armas de bobina** · Sala de simulación |
| 4 | 55 | **Armadura de placas** · Nanochaleco, munición perforante |
| 5 | 85 | Granadas de tesela, Oído (+20 a hackear, +1 región) · Centro de comunicaciones |
| 6 | 120 | **Armas de plasma**, teselas médicas · Zumbador |
| 7 | 160 | **Armadura de tesela** |
| 8 | 205 | Las cintas de 1989: la **misión final** |

**Armas y armadura.** No se compran. Cada soldado lleva el mejor nivel que permitan a la vez el Bastión y su rango: el nivel 2 (magnético, placas) desde **Cabo**, el nivel 3 (plasma, depredadora) desde **Teniente**. Así, un ascenso se nota en el arma y en la armadura, que también cambian de aspecto. Armas magnéticas: +2 de daño; plasma: +4. Placas: +2 de salud; depredadora: +4 de salud y +1 de blindaje.

**Decisiones conjuntas.** Construir, excavar, contactar regiones, elegir misión y responder a los acontecimientos son *propuestas*: aparecen en una franja para el compañero, que las acepta o las rechaza. Si juegas solo (o tu compañero no está conectado) se aplican directamente. Reclutar, ascender, comprar accesorios y equipar a tus propios soldados no necesita permiso.

**Instalaciones.**

| Instalación | Efecto | Nivel |
|---|---|---|
| Taller | Accesorios un 25 % más baratos | 1 |
| Enfermería | Las heridas se curan el doble de rápido | 1 |
| Centro de entrenamiento | Cada jugador lleva un soldado más (escuadra de hasta 8) | 2 |
| Sala de simulación | Las misiones dan un 25 % más de experiencia al Bastión | 3 |
| Centro de comunicaciones | +2 regiones contactables | 5 |

Todo se paga con **créditos**, la única moneda: construir y excavar, comprar accesorios, reclutar y contactar regiones. Llegan con las recompensas de misión, con el pago mensual del Consejo y con la venta de los **cuerpos enemigos** que se recuperan al ganar una misión (la morgue del Consejo paga por sus teselas).

**El mundo.** Empezáis con Europa Occidental. Contactar otras regiones cuesta créditos, y cada región contactada aumenta el **pago del Consejo a fin de mes**. El Coro levanta **Órganos** en distintas regiones: cada uno hace avanzar la Partitura cada 14 días. Para asaltarlo (una misión de sabotaje que no caduca y que hace retroceder la Partitura) necesitáis contacto en esa región. Cada región tiene su situación y un aliado que firma los informes de sus misiones.

**Historia.** Hay siete **misiones de historia**, marcadas en azul en la geoesfera.
- La primera está desde el primer día. Cada una de las siguientes se abre cuando el Bastión llega a su nivel y la anterior está ganada.
- No caducan y dan recompensas propias: la Dra. Nwosu, que hace que los cuerpos valgan más, compases menos o reclutas.
- Antes de la quinta llega Simón, un Afinado desertor, y decidís si os fiáis de él.
- La misión final pide el nivel 8 y las siete misiones de historia ganadas. Al ganarla, los dos decidís qué hacer con el Coro, y cada decisión tiene su final ([la historia](docs/LORE.md)).

**Rumores y encargos.** Al escanear aparecen **rumores** en el globo (una luz con «?»).
- Si decidís **escucharlo**, en unos días de escaneo sabréis qué era: una operación mejor pagada, un alijo, información o un bulo. A veces es el **encargo** de esa región.
- Los **encargos** son misiones secundarias en cadena, en verde: varias operaciones y decisiones con un desenlace que se queda en la campaña (un recluta, ingresos, una tecnología antes de tiempo, un rasgo en un soldado…).
- Algunos llegan solos: Ibarra, la otra Amara, la estación de números, el topo, o un soldado que reconoce a un familiar entre los Impresos.
- Si una operación de encargo se pierde o caduca, el encargo se pierde con sus consecuencias.

**Acontecimientos.** De vez en cuando pasa algo (contrabandistas, refugiados, un desertor, un sabotaje…) con dos respuestas posibles. Ni el tiempo avanza ni la nave despega hasta que decidís.

**Accesorios.** Cada soldado lleva un objeto de **utilidad** (granada extra, de humo, aturdidora, botiquín, nanochaleco, Zumbador) y un tipo de **munición** (perforante, trazadora). Se compran en la Armería cuando el nivel del Bastión los desbloquea. Si el soldado cae, se pierden.

**Tiempo.** **Escanear** corre los días hasta el siguiente acontecimiento (obra terminada, misión nueva, avance enemigo, fin de mes…), pero solo cuando los dos estáis listos. El botón avisa si tu compañero ya lo ha pulsado.

**Misiones.** Cada misión ocurre en una región y en un mapa generado para ella. Elegida una misión, la escuadra viene **ya montada** con los mejores soldados listos de cada uno (una de cada clase primero) y cada jugador cambia a quien quiera, **hasta 3 de sus soldados** (4 con el centro de entrenamiento; los heridos no pueden ir) y se despega cuando los dos estáis listos. Al volver veis el informe: bajas, experiencia de cada soldado y del Bastión, heridas, ascensos, recompensa y cuerpos vendidos.

- Experiencia: +1 por ir, +1 si se gana, +1 por cada baja. Rangos: Novato → Soldado (2) → Cabo (5) → Sargento (9) → Teniente (14) → Capitán (20) → Comandante (27) → Coronel (35). Cada rango da +5 de puntería y más voluntad.
- Heridas: 2 días + 2 por punto de salud perdido. Los caídos no vuelven.
- Abandonar una misión cuenta como fracaso, pero los supervivientes regresan.

**La Partitura.** El reloj del enemigo (arriba; se llama «Actividad enemiga» hasta que el Bastión llega al nivel 4): sube 1 cada 12 días, con cada Órgano, con cada misión fracasada y con cada misión que dejáis caducar. Si llega a 12, se pierde la campaña. Destruir Órganos, sabotajes y algunos pirateos lo hacen bajar. Para ganar hay que llevar el Bastión al **nivel 8**, ganar las siete misiones de historia, superar el **Asalto al Diapasón** y decidir juntos el final.

La partida se guarda sola en `data/save.json`. Si cierras el servidor o se cae la conexión, al volver a entrar retomáis la misión donde estaba. Al actualizar el juego a una versión con otro formato de guardado, la campaña se conserva, pero una misión a medias se pierde (volvéis a la base).

Para usar otro puerto: `npm start -- --port 8080`.

## Controles

| Acción | Control |
|---|---|
| Seleccionar soldado | Clic sobre él, en la lista de la izquierda o **Tab** / **Mayús+Tab** |
| Mover | Clic en una casilla: azul = 1 acción, amarillo = carrera de 2 acciones, rojo = te descubrirían estando ocultos |
| Disparar | **Espacio** apunta al mejor objetivo y **Espacio** otra vez dispara. **Tab** cambia de objetivo. También puedes hacer clic sobre el enemigo y pulsar **Espacio** |
| Habilidades | **1-9** y **0** según la barra de cada soldado (los números aparecen en cada botón). Las de casilla (granadas, humo, descarga) se lanzan con clic; las de objetivo se confirman con **Espacio**. Los botones con borde discontinuo son acciones gratuitas |
| Marcar para el compañero | **G** o **clic central**: posición o enemigo |
| Evacuar | Habilidad **Evacuar** estando en la zona verde |
| Cancelar | **Esc** o clic derecho |
| Menú | **Esc** (si no estás apuntando) o el botón **Menú**: controles, ajustes y abandonar la misión (termina para los dos) |
| Ceder el mando | **C** o el botón junto a Terminar turno: tu compañero pasa a dar las órdenes |
| Terminar turno | **Retroceso**. Si tu compañero no ha terminado, el mando pasa a él; cuando los dos habéis terminado, juega el enemigo |
| Cámara | **WASD** o arrastrar con clic derecho · **Q/E** girar · rueda para zoom · **F** centrar en el soldado |
| Sonido | **M** activa o desactiva los efectos |
| Ajustes gráficos | En **Ajustes** (menú de Esc o menú principal): límite de FPS (sin límite, 60, 30), efectos (altos, medios, bajos), sombras, resolución (100, 75 o 50 %) y contador de FPS. Se guardan en tu navegador y se aplican al momento |

**El mando.** En el turno de la escuadra solo da órdenes quien tiene el mando (arriba se ve quién). Lo cede cuando quiere, aunque a sus soldados les queden acciones, y pasa solo cuando ya no puede hacer nada más o termina su turno. Un turno lo abre cada jugador por turnos. Si tu compañero se desconecta, el mando es tuyo.

La cámara sigue tus acciones y el turno alien, nunca las de tu compañero. En cuanto mueves la cámara o eliges otro soldado, deja de seguir la acción en curso.

## Desarrollo

```bash
npm run dev        # servidor (puerto 3000) + cliente con recarga en caliente (http://localhost:5173)
npm test           # tests del motor de reglas
npm run typecheck  # comprobación de tipos de los tres paquetes
```

Para probar los dos jugadores en un mismo navegador, abre dos pestañas con `?perfil=a` y `?perfil=b`, por ejemplo `http://localhost:5173/?perfil=b`.

Herramientas de depuración: en desarrollo, o con `?debug` en la URL, la consola tiene `window.__bastion` con `step()`, `hover(x, y)`, `click(x, y)` y `snap(nombre)`. Para usar `snap` contra un servidor de producción, arráncalo con `--snapshots <carpeta>`; con `--save <archivo>` usa otro archivo de guardado y no toca tu partida.

## Estructura

```
packages/
  engine/   Reglas puras en TypeScript: mapa, visión, cobertura, puntería, movimiento,
            eventos, IA, generador de mapas (mapgen.ts) y campaña. Lo comparten
            el servidor y el cliente.
  server/   Node + WebSocket. Sala de 2 jugadores, autoridad del estado, guardado.
  client/   Vite + Three.js. game/ es el combate (escena 3D, animaciones, HUD);
            strategy/ es la capa estratégica (el Bastión, la geoesfera y sus
            pantallas); ui/ el menú principal.
docs/
  DESIGN.md                     Decisiones de diseño y hoja de ruta.
  archive/mod-xcom2/            Informe del enfoque anterior (mod de XCOM 2), descartado.
```
