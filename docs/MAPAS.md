# Mapas: proporciones y trazado orgánico

Propuesta del 19/10 para rehacer los mapas de combate, aprobada por Arturo el mismo día («adapta los mapas… todo»). Se hace por fases (§ plan); lo pendiente de decidir está al final. Lo pidió Arturo: «orgánicos y con las proporciones adecuadas: edificios, habitaciones, calles, tamaño de mapa, todo».

Sale de dos mediciones:

- la escala de los modelos del 19/10 (`scratchpad/escala/measured.json` y `props.json`);
- 192 mapas generados con el mismo código que usa el servidor (`setup.ts` `missionLayout` → `mapgen.ts` `generateLayout`): 24 semillas de cada tipo. Los scripts están en el anexo.

## 0. En pocas líneas

- **Los edificios y las salas ya tienen un tamaño casi real.** Una casa mide unos 10 × 12 m vistos y un dormitorio, 5 × 8 m de suelo libre. No hay que achicarlos.
- **Lo que está mal es lo de alrededor y lo de dentro.** Las calles miden 6 m de fachada a fachada. Las manzanas, 13 × 19 m. El mapa entero, 64 m. Los coches, los autobuses, las camas y los sofás salen a la mitad. La escalera sube una planta en una casilla, a 66°.
- **La propuesta:**
  - la casilla no cambia;
  - crecen las calles, las manzanas, el mapa (de 48 a 64–72), los vehículos (coche 1 × 3, autobús 2 × 9), los muebles (cama 1 × 2) y las escaleras (tramo de 3 casillas);
  - el trazado pasa a manzanas cerradas con parcelas, patios traseros, retranqueos y pasajes, no una cuadrícula de bloques de una casa de fondo.
- **El recorrido de la misión no se alarga.** De la salida al objetivo se siguen andando 60–80 casillas. El mapa más grande da flancos, no más camino.
- **Rendimiento:**
  - 64 × 64 es seguro;
  - 72 × 72 pide tres arreglos antes (sombra, reconstrucción por trozos, visión);
  - 96 × 96, no.
- **Seis fases.** Cada una se juega y se prueba sola.

## 1. La escala

- **1 casilla = 1,5 m del mundo** (`SYNTY_SCALE`; en el cliente, 1 unidad = 1 casilla).
- **El soldado** se dibuja de 2,01 m: el modelo mide 1,79 m y `CHARACTER_SCALE` es 1,125. A su lado, todo se ve a 0,895 de lo dibujado.
- **Medida «vista»:** 1 casilla parece **1,34 m**. En este documento, «m» son metros vistos, salvo que diga «dibujado».
- **Una planta:** 2,3 casillas (3,45 m dibujados, 3,09 vistos). Está bien.
- **Las paredes** son casillas enteras que no se pisan, pero el muro se dibuja fino en su línea media (`buildings.ts`, 0,28 casillas de grueso). Por eso:
  - **suelo libre de una sala** = (casillas interiores + 0,72) × 1,34 m. Una sala de 3 casillas por dentro tiene 5,0 m libres;
  - **un edificio por fuera** = (casillas − 0,72) × 1,34 m. Una casa de 8 casillas mide 9,8 m;
  - **una calle de fachada a fachada** = (casillas entre muros + 0,72) × 1,34 m.
- **Lo que no cambia:** la casilla, el movimiento en casillas, los alcances en casillas y la regla de salas de 3 × 3 o más (prueba del 12/10).

## 2. Lo que hay hoy (medido)

Formato: mínimo / mediana / máximo. Todos los mapas generados miden 48 × 48 (`GEN_SIZE`), 64 m de lado.

### 2.1 Calles y manzanas

| | Cuadrícula y casco viejo | Polígono | Hospital |
|---|---|---|---|
| Calles por mapa | 7 / 12 / 15 | 3 / 4 / 6 | 3 |
| Calzada | 2–4 casillas (2: 35 %, 3: 43 %, 4: 21 % de la longitud) = 2,7–5,4 m | 3–4 (4: 65 %) | 3–4 |
| Acera | 1 casilla, y solo en el 38 % de los lados que dan a una calle | 1, en el 80 % | 1, en el 58 % |
| Retranqueo de la fachada | 0 (p75: 1) | 2 | 1 |
| Fachada a fachada | 2 / **4** / 19 casillas = **6,3 m** | 13 | 6 |
| Manzana | 8–19 × 8–20; mediana **10 × 14** (13 × 19 m) | 16 × 23 | 21 × 24 |
| Plaza | 8 × 11 (6–12 × 7–15) = 11 × 15 m | — | 6 × 15 |
| Plazuela del casco viejo | 5 × 5 | — | — |
| Aparcamiento | 8 × 12; plazas de 2 de paso y coche de 1 × 2 | 8 × 13 | 6 × 14 |

Las reglas del corte (`city`, `district`): cuadrícula con avenida de 4, calles de 3 y callejas de 2, y manzanas de 9 a 20; casco viejo con 3–4 / 2–3 / 2–3, manzanas de 8 a 18 y calles quebradas cada 8 casillas (`STRETCH`).

### 2.2 Edificios (por fuera, muros incluidos)

| Tipo | Por mapa | Huella (mediana; rango) | Vista | Plantas | Puerta exterior |
|---|---|---|---|---|---|
| Casa | 4,4 | 8 × 10 (7–11 × 7–13) | 9,8 × 12,5 m | 1–2 (media 1,3) | 1 casilla |
| Tienda de calle comercial | 3,9 | 7 × 9 (6–9 × 7–12) | 8,4 × 11 m | 2–3 | 1 |
| Tienda en hilera | 3,2 | 8 × 10 | 9,8 × 12,5 m | 2–3 | 1 |
| Oficina | 0,8 | 11 × 12 (10–12 × 10–14) | 14 × 15 m | 2–3 | doble (2) |
| Nave (ciudad / polígono) | 0,1 / 1,9 | 12 × 15 / 13 × 14 | 15 × 19 m | 1 | hueco de 3 |
| Motel (ala) | 0,1 | 7 × 13 | 8 × 16 m | 1 | 1 por cuarto |
| Consultorio / enfermería | 1 | 11 × 11 / 11 × 12 | 14 × 14 m | 1 | 1 |
| Hospital | 1 | 12 × 21 | 15 × 27 m | 3 | doble |
| Casa de campo, cabaña | 0,8 | 7 × 9 | 8 × 11 m | 1 | 1 |
| Granero | 0,2 | 12 × 14 | 15 × 18 m | 1 | hueco de 3 |
| Aserradero, restaurante, tienda de gasolinera | 1 | 9 × 13, 7 × 9, 7 × 7 | | 1 | 3, 1, 1 |
| Nave de la fábrica, oficinas | 1 | 12 × 16, 7 × 9 | 15 × 21 m | 1, 1–2 | muelles de 2, 3 y 5 |
| Bloque de pisos | — | No se entra: es un bloque macizo con escaleras de mano (`flats`) | | 2 | — |

Las ventanas van cada 2 o 3 casillas; una casa tiene una mediana de 4. Hay 11 puertas interiores por mapa de ciudad, todas de 1 casilla (2 de hueco en las plantas diáfanas).

### 2.3 Salas (casillas interiores, mediana y rango)

| Sala | Casillas | Suelo libre |
|---|---|---|
| Salón | 4 × 5 (3–6 × 4–7) | 6,3 × 7,7 m |
| Cocina | 4 × 4 (3–4 × 4–6) | 6,3 × 6,3 m |
| Dormitorio | 3 × 5 (3–6 × 3–7) | 5,0 × 7,7 m |
| Baño | 3 × 4 abajo, 3 × 5 arriba (hasta 5 × 7) | 5,0 × 6,3 m |
| Despacho | 4 × 4 | 6,3 × 6,3 m |
| Recibidor | 3 × 3 | 5,0 m |
| Oficina (con pasillo / diáfana) | 3 × 4 / 4 × 8 | |
| Tienda y almacén | 4 × 6 (3–6 × 4–11) | |
| Consulta, sala de equipo | 4 × 5 | |
| Hospital: todas | 3 × 3 (algunas 3 × 4) | 5,0 × 5,0 m |
| Enfermería | 3 × 4 a 3 × 5 | |
| Nave diáfana | 10 × 10–14 | |
| Pasillo | 2 (oficinas, hospital), 1 (enfermería) | 3,6 m / 2,3 m |

### 2.4 Escaleras

- **Tramo de 1 casilla.** Sube una planta a 66°. El rellano está en la casilla de delante, en la planta de arriba. El hueco es de 1 casilla.
- **Por mapa de ciudad:** 22 (12–31) escaleras y 2 de mano.
- **Forjados:** unas 2050 casillas por mapa en las plantas de arriba.

### 2.5 Vehículos y objetos grandes

| Objeto | Huella | Dibujado | Real | Visto hoy |
|---|---|---|---|---|
| Coche | 1 × 2 | 2,92 m (1,95 casillas) | 4,5 m | 2,6 m |
| Patrulla, ambulancia | 2 (morro + objetivo) | 2,92 m | 5,3 m, 6,5 m | 2,6 m |
| Autobús (La Lata) | 2 × 5, para todas las etapas | ~7,6 casillas: sobresale | 11,4 m | |
| Camión | 2 × 4 | 5,85 m | 9,5 m | 5,2 m |
| Contenedor | 2 × 5 | 7,27 m | 6,1 m | 6,5 m (bien) |
| Tractor | 2 × 4 de camión | 3,9 m | 3,3 m | bien |
| Cama, sofá, bañera, mesa, escritorio | 1 × 1 | estirados a 1,5 m | 1,8–2,7 m | 1,34 m |
| Cama de hospital, camilla | 1 × 1 | 1,7 m, 1,4 m | 2,5 m, 2,3 m | |
| Árbol de calle, árbol | 1 | 3,0 m y 4,4 m de alto | 9,4 m y 6,2 m | |
| Estatua, depósito de agua, silo | | 1,8 m, 7,3 m, 3,4–5,8 m | 8,2 m, 12,7 m, 10,2 m | |

Por mapa: en la ciudad, 21 coches (7–29) y 1 autobús; en el polígono, 14 contenedores; en la fábrica, 3 camiones, 6 contenedores y 2 silos.

### 2.6 Reparto del suelo y distancias

| Tipo | Edificado | Calle o camino | Patio asfaltado | Abierto | Objetos de cobertura | Suelo abierto junto a cobertura o muro |
|---|---|---|---|---|---|---|
| Ciudad | 46 % | 17 % | — | 33 % | 3,7 % | 47 % |
| Polígono | 20 % | 14 % | 27 % | 28 % | 10,6 % | 33 % |
| Afueras | 8 % | 8 % | — | 59 % | 24 % (árboles, 17 %) | 33 % |
| Área de servicio | 16 % | 10 % | 10 % | 45 % | 20 % | 31 % |
| Fábrica | 11 % | 20 % | 31 % | 26 % | 10 % | 36 % |

| Distancia, andando | Ciudad | Polígono | Hospital | Afueras | Área de servicio | Fábrica |
|---|---|---|---|---|---|---|
| Escuadra → objetivo | 65 (47–76) casillas = 87 m = 8 movimientos azules | 62 | 68 | 63 | 47 | 52 |
| Escuadra → evacuación (La Lata) | 51 (42–57) | 53 | 53 | 53 | 52 | 47 |
| Grupo más cercano | 21 (16–33) en línea recta | 21 | 21 | 21 | 19 | 20 |

Hay 8 grupos por mapa en la dificultad 2.

### 2.7 La base y los mapas a mano

- **La base:**
  - 40 × 40, con una franja de 2 al borde;
  - el claro llega a 9 casillas del centro (7–11), es decir, 12 m;
  - pinos en el 52 % del suelo, y pistas de 3 que la cruzan;
  - el solar del Bastión es de 6 × 6, con las etapas 4 × 2, 6 × 3, 5 × 4 y 6 × 5;
  - los edificios ocupan de 3 × 3 a 4 × 4 (de 4 a 5,4 m), pero sus modelos se dibujan a su tamaño real (tiendas y cobertizos de 6 a 9 m) y sobresalen. La caravana ocupa 4 × 2 y se dibuja de 3,9 casillas.
- **Mapas a mano:** «plaza» 30 × 30, «torre» 24 × 20 con 3 plantas y «bastion» 36 × 36 (asedio).

### 2.8 Lo que está bien y lo que no

- **Bien:**
  - las huellas de casas, tiendas, oficinas y consultorios;
  - el tamaño de las salas: un poco grandes, como pide el juego;
  - la altura de planta;
  - las puertas (algo grandes, aceptadas);
  - los contenedores;
  - la fuente.
- **Algo grande:** los baños (5 × 6 m libres; uno real mide 2 × 2,5 m).
- **Pequeño:**
  - calles y aceras;
  - manzanas (una casa de fondo);
  - plazas, aparcamientos y el mapa entero;
  - vehículos, muebles y árboles;
  - el tramo de escalera;
  - el claro de la base.
- **Rígido:**
  - fachadas casi siempre a ras de calle;
  - aceras de 1 casilla o ninguna;
  - manzanas rectangulares de un solo programa;
  - pocos patios y traseras.

## 3. Cómo se genera hoy

### 3.1 Orden de las pasadas (`generateLayout`)

1. **El lienzo canónico, 48 × 48.** La escuadra va arriba a la izquierda, el objetivo lejos y la evacuación arriba a la derecha. Al final se refleja o se traspone al azar.
2. **El rincón de la escuadra:** se reservan 5 × 5. `pickStyle` da una escala por mapa: casa de 7–9, oficina de 11–14, nave de 13–16 y ventanas cada 2–3.
3. **El bioma:**
   - `city`: corte binario `district`, `townLot` con sus aceras, plazas de esquina, `splitLot` y `cityLot` por programa;
   - `facility` → `factory`: cuatro zonas entre dos carriles de patio;
   - `wilds`: camino, granja, campos y bosque por ruido;
   - `roadside`: carretera, gasolinera, restaurante, aserradero y motel.
4. `thinClutter` quita la mitad del estorbo suelto.
5. Unos puestos de cobertura junto a la escuadra.
6. **La evacuación:** `carveEvac`, 3 × 3 en el rincón de 14 × 14. Después `parkBus` (2 × 5) y su salida, con su propia semilla.
7. `placeLootBoxes`: 1 por cada 750 casillas, 3 como mucho.
8. `openDeadEnds`, para que no haya callejones (prueba del 16/10, #20).
9. `connect`, para que todo se alcance: escaleras de mano, puertas nuevas o caminos abiertos.
10. **Salidas y objetivo:** las 8 casillas más cercanas al rincón, y el objetivo según la misión.
11. **Grupos:** a 16 o más de la escuadra (`POD_DISTANCE`), separados 11, 6 o 3.
12. **Plantas y farolas:** `buildingStoreys` y `streetLights` (cada 8), con su propia semilla.
13. **`upperFloors`,** con su propia semilla: plantas, salas, ventanas, escaleras de 1 casilla, azoteas, escaleras de mano y el sitio de los vivos de arriba (`perch`).
14. **El giro final,** y después `furnishMap`, con su propia semilla. Se deshace si corta un camino.
15. `dressMedical` y `dressHome`.

### 3.2 Un edificio (`building`)

1. Muros y suelo.
2. La distribución:
   - `medicalPlan` en los centros médicos;
   - `corridorPlan` en las oficinas: pasillo de 2 y salas de 3 a 6;
   - `shopPlan`: tienda y almacén;
   - `hallPlan`: nave con su despacho;
   - en los demás, `splitRooms`: lado máximo de 6, u 8 si es diáfano, y nunca menos de 3.
3. Las puertas: la de la calle primero, 3 casillas en las naves, y la trasera el 45 % de las veces.
4. Los usos de la casa (`homeUses`, `splitOff`).
5. Las ventanas.
6. Los muebles, de una casilla cada uno (`furnishRoom`, `furnishHome`).

### 3.3 Las restricciones de hoy

- Salas de 3 × 3 o más; los muebles ocupan como mucho el 40 % del suelo (`MAX_SHARE`).
- Ningún callejón sin salida. Todo lo pisable se alcanza (`connect`, y los tests de alcance).
- Puertas, escaleras y rellanos siempre despejados. Nada tapa una ventana por la que se salta.
- **La cobertura:**
  - ningún claro de más de 6 casillas sin algo detrás (`coverGaps`, en la fábrica);
  - la cobertura nueva, a 2 o más de paredes y puertas (`roomForCover`).
- **Los bordes:** 2 casillas libres para la entrada de la horda (`EDGE_CLEAR`). El cliente cierra el borde con decorado (`dressEdges`).
- **Cada pasada nueva, con su propia semilla,** para no mover lo demás.
- **Los muebles solo donde no cortan:** `keepsWays`, y `joinedAsBefore` deshace todo si se rompe el alcance.

## 4. Medidas objetivo

La casilla sigue siendo de 1,34 m vistos. Entre paréntesis, el tamaño visto.

### 4.1 Calles y manzanas

| Elemento | Hoy | Objetivo |
|---|---|---|
| Avenida (una por mapa; la del autobús) | calzada 4, acera 1 | calzada 7 (9,4 m) + aceras de 2 y 2; de fachada a fachada, 11–13 (16–18 m) |
| Calle de cuadrícula | 3 | calzada 5 (6,7 m) + aceras de 2 y 1–2; con antejardín, 8–12 de fachada a fachada (12–17 m) |
| Calle del casco viejo | 2–3 | calzada 3–4 + acera de 0–1; de fachada a fachada, 4–6 (6–9 m) |
| Callejón | 1–2 entre casas | 2 (3,6 m), siempre con salida |
| Pasaje bajo un edificio (zaguán) | no hay | 2 de ancho, al patio de manzana |
| Camino rural / carretera | 2 / 3 | 3 (4 m) / calzada 5 + arcenes de 1 |
| Manzana de cuadrícula | 10 × 14 | 22–28 × 24–32 (30–43 m), cerrada, con patio |
| Manzana del casco viejo | 8–18 | 12–24, irregular (con muescas o en L) |
| Manzana de afueras | — | 26–32 de fondo: dos filas de parcelas espalda con espalda |
| Manzana de polígono | 16 × 23 | 24–36 |
| Chaflán (esquina cortada) | no hay | 2–3 casillas en escalera en las esquinas de la cuadrícula |
| Plaza | 8 × 11 | 14–22 de lado (19–30 m) |
| Plazuela | 5 × 5 | 8–10 |
| Parque | no hay | 18–28, árboles cada 4–5 casillas y setos |
| Aparcamiento | coche 1 × 2, plaza de 2 | plaza 2 × 4, carril de 5; dos filas = 13 de fondo |

**Comprobación de juego:** un movimiento azul (8 casillas) cruza de bordillo a bordillo una calle de cuadrícula (calzada 5 + acera 2 = 7). El amarillo (16) cruza de fachada a fachada una avenida.

### 4.2 Edificios (huella por fuera, muros incluidos)

| Tipo | Hoy | Objetivo |
|---|---|---|
| Casa adosada (casco viejo) | 7–9 de fachada | fachada 5–7 (de medianera a medianera) × fondo 8–11; 2–3 plantas |
| Casa aislada | 8 × 10 | 8–10 × 9–11 (sin cambio), con antejardín de 2–4, patio trasero y a veces un garaje de 3 × 5 o un cobertizo de 2 × 3 |
| Tienda con pisos encima | 7 × 9 | fachada 6–9 × fondo 9–12; tienda abajo y vivienda arriba |
| Bloque de pisos (hoy un bloque macizo) | — | 12–18 × 9–11, 3 plantas, núcleo de escalera, 2 viviendas por rellano. Se entra |
| Oficina | 11 × 12 | 11–14 × 12–18, pasillo de 2 |
| Consultorio | 11 × 11 | sin cambio (está aprobado) |
| Enfermería | 11 × 12 | 11–13 × 13–17 |
| Hospital | 12 × 21 | 16–20 × 24–30, dos alas, pasillo de 2, 3 plantas |
| Nave (ciudad / polígono) | 12 × 15 | 14–18 × 18–24 |
| Granero | 12 × 14 | 10–12 × 14–18 |
| Nave de la fábrica | 12 × 16 | 16–20 × 24–30 |
| Motel (ala) | 7 × 13 | sin cambio (cuartos de 3 × 5) |
| Gasolinera (tienda; patio) | 7 × 7; 11 × 15 | 7 × 9; patio de 16 × 20 con marquesina |

**Variedad por parcela:**

- el fondo varía en ±2;
- el retranqueo, 0–1 en la ciudad y 2–4 en las afueras;
- en 1 de cada 3 casas, una L o un anexo;
- las esquinas, una planta más.

### 4.3 Salas (casillas interiores)

| Sala | Objetivo | Nota |
|---|---|---|
| Salón | 3–5 × 4–6 | con sofá 1 × 2 y mesa |
| Cocina | 3 × 4 | |
| Dormitorio sencillo / doble | 3 × 3 / 3 × 4 o más | la cama doble (2 × 2) solo cabe en 3 × 4 |
| Baño, aseo, despensa | 3 × 3 hoy; **2 × 3 si Arturo lo acepta** | ver §10 |
| Despacho | 3 × 3 – 3 × 4 | |
| Recibidor con escalera | 2 × 5 – 3 × 6 | es la caja de escalera de la casa |
| Pasillo de casa | 1–2 | |
| Pasillo de oficina u hospital | 2 | |
| Oficina | 3 × 4 – 4 × 6; diáfana, 5 × 8 o más | |
| Habitación de hospital | 1 cama: 3 × 4; 2 camas: 4 × 5 | camas de 1 × 2 con pasillo |
| Escalera | caja de 2 × 5 (tramo de 3) | §4.4 |

### 4.4 Puertas, ventanas y escaleras

- **Puertas:**
  - de 1 casilla en casas e interiores;
  - dobles (2) en tiendas grandes, oficinas, hospitales y portales de pisos;
  - garaje, 2; muelle, 3; portón de nave, 5 (pendiente de Arte).
- **Ventanas:** cada 2–3 casillas, nunca en una esquina ni en una medianera.
- **Tramo de escalera:** 3 casillas de recorrido (38°, como uno de verdad); 2 donde no cabe (49°). De ancho, 1 (1,34 m).
  - **Caja:** 2 × (tramo + 2): el tramo, el pasillo de al lado, el pie y el rellano.
  - **Varias plantas:** cada planta lleva su tramo en la otra columna de la misma caja, en sentido contrario (escalera de ida y vuelta). La caja se apila sin plantas a medias.
  - **El hueco:** las 3 casillas del tramo en la planta de arriba.
- **Escaleras de mano:** 1 casilla, como hoy.

### 4.5 Vehículos (huella; largo dibujado)

| Vehículo | Hoy | Objetivo | Visto |
|---|---|---|---|
| Coche, patrulla | 1 × 2; 2,9 m | **1 × 3**; 4,6 m (apenas sobresale) | 4,1 m |
| Ambulancia, furgoneta | 1 × 2 | **2 × 4**; 6,0 m | 5,4 m |
| Todoterreno de La Lata | 2 × 5 (el del autobús) | 2 × 4 | |
| Furgoneta de La Lata | 2 × 5 | 2 × 4 | |
| Camión de La Lata y camión abandonado | 2 × 5 / 2 × 4 | **2 × 6** a 2 × 7; 9 m | 8 m |
| Autobús de La Lata | 2 × 5 | **2 × 9**; 12,9 m | 11,5 m |
| Tractor | 2 × 4 | 2 × 3 | |
| Contenedor de 20 pies | 2 × 5; 7,3 m | 2 × 5; 6,8 m | 6,1 m |
| Caravana (base) | 4 × 2 | 2 × 5 | 6,5 m |
| Casa móvil (base) | 6 × 3 | 3 × 7 | |

**La evacuación** pasa de 3 × 3 a una franja de 3 × 7 a lo largo del costado del vehículo, donde se abren sus puertas.

### 4.6 Muebles

| Mueble | Hoy | Objetivo |
|---|---|---|
| Cama sencilla, cama de hospital, camilla, mesa de exploración | 1 | 1 × 2 |
| Cama doble | 1 | 2 × 2 |
| Sofá, bañera, mesa de comedor, mueble de la tele, mostrador | 1 | 1 × 2 |
| Escritorio de oficina | 1 | 1 × 1 (en filas de 2) |
| Sillón, mesilla, nevera, fogón, váter, lavabo, armario, librería | 1 | 1 × 1 |
| Encimeras, estanterías | filas de 1 | filas de 1 × n contra la pared |
| Asientos de espera, recepción | 1 | 1 × 2 – 1 × 3 |
| Máquina de la fábrica | 1 | 2 × 2 – 2 × 3 |

- **El límite del 40 % de suelo sigue.**
- **Una sala de 3 × 3:** cama sencilla y armario, o sofá y mesilla.

### 4.7 Árboles y mobiliario urbano

- **Árboles:**
  - el tronco ocupa 1 casilla;
  - la copa se dibuja a su tamaño (4–6 m de ancho y 6–9 m de alto) y se desvanece con el corte, como los tejados;
  - en la acera, cada 6–8 casillas; en los parques, cada 4–5.
- **Farolas:** cada 10–12 casillas, al tresbolillo; la luz llega a 3.
- **Bancos** de 1 × 2, como hoy. Marquesina de autobús de 1 × 3. Quiosco de 2 × 2.

### 4.8 Tamaño del mapa

| Tipo | Hoy | Objetivo | Grupos (dif. 2) |
|---|---|---|---|
| Casco viejo | 48 × 48 | **64 × 64** | 11 |
| Cuadrícula | 48 × 48 | 64 × 64; 72 × 72 tras los arreglos de §7 | 11 / 12 |
| Polígono | 48 × 48 | 72 × 72 | 12 |
| Hospital | 48 × 48 | 72 × 64 | 11 |
| Fábrica | 48 × 48 | 72 × 72 | 12 |
| Afueras | 48 × 48 | 72 × 72 | 12 |
| Área de servicio | 48 × 48 | 64 × 72 | 11 |
| Base | 40 × 40 | **56 × 56** | (asedio) |
| Mapas a mano | 30, 24 × 20, 36 | sin cambio | |

- **Mapas rectangulares:** se permiten (W ≠ H), para variar.
- **Proporción del suelo, en ciudad:**
  - edificado, 35–45 %;
  - calles y aceras, 25–30 %;
  - patios, plazas y solares, 20–30 %;
  - objetos de cobertura, 4–6 %;
  - suelo abierto junto a cobertura, 45–50 % (como hoy).

### 4.9 La base

| | Hoy | Objetivo |
|---|---|---|
| Mapa | 40 × 40 | 56 × 56, franja de 2 al borde |
| Claro | radio 7–11 | radio 12–16 |
| Solar del Bastión | 6 × 6 | 9 × 9; etapas 2 × 5 (caravana), 3 × 7 (casa móvil), 5 × 7 (barracón), 7 × 7 (casa) |
| Sala de radio, Cuarentena, Camposanto, Sala de mapas, Taller de oficios | 3 × 3 | 4 × 4 – 4 × 5 |
| Barracón, Enfermería, Taller, Centro de entrenamiento, Chapistería | 4 × 3 | 5 × 6 – 5 × 7 (del tamaño de su modelo) |
| Huerto | 4 × 4 | 6 × 6 |
| Campo de tiro | 6 × 2 | 3 × 10 |
| Torre | 2 × 3 | 3 × 3 + escalera de mano |

## 5. Cómo hacerlos orgánicos

1. **Trazado por clases de calle,** no por corte binario.
   - **Cuadrícula:** una red de ejes con separación de 26–34 y ±2 al azar. Una avenida. Algunos cruces en T (sin callejones). Calles que se desplazan 1–3 casillas en un cruce. Chaflanes.
   - **Casco viejo:** crece desde una plaza:
     - 3 o 4 calles que salen de ella y se quiebran cada 8–14 casillas (`bentStreet` con tramos más largos);
     - una ronda;
     - callejones de 2 que las cosen.
   - **Afueras:** una carretera y calles en bucle (nada de fondos de saco).
2. **Parcelas a lo largo del borde de cada manzana.**
   - **Fachada por barrio:** casco viejo 5–8, cuadrícula 7–10, afueras 12–16.
   - **Esquinas:** la parcela de esquina tiene dos fachadas.
   - **Fondo:** la mitad de la manzana, o hasta el patio.
3. **Un edificio por parcela,** según el barrio: casa, tienda con pisos, bloque, oficina o nave.
   - **Huella:** la fachada menos los huecos laterales (0 adosadas, 1–2 aisladas); el fondo, 7–12; el retranqueo y la altura, por parcela.
   - **Formas:** L y anexos.
   - **Huecos:** algunas parcelas quedan de solar, ruina o jardín.
4. **El patio de manzana:**
   - **lo que hay:** traseras con vallas y muretes por los lindes, con su puerta; cobertizos de 2 × 3, tendederos, árboles, cubos, huertos (cobertura baja) y algún coche en la entrada;
   - **las salidas:** al menos dos (un zaguán, un hueco entre casas o un portón), para que el patio sea un flanco y no un fondo de saco.
5. **Espacio público:**
   - una o dos plazas en cruces, de 14–22;
   - un parque o un aparcamiento por mapa;
   - la avenida con el autobús, y la evacuación en un tramo recto de 12 o más.
6. **Irregularidad sin diagonales:**
   - manzanas con muescas (restar rectángulos);
   - manzanas unidas en L;
   - calles en escalera (escalones de 2–3 cada 8–12);
   - fondos y retranqueos distintos en cada parcela;
   - alturas que cambian de una casa a otra.
7. **El recorrido manda, no el rincón.** La escuadra entra por un borde. El objetivo se elige a 60–80 casillas andando (con un `costField` como el de la medición) y la evacuación, a 40–60 de las dos cosas. El mapa grande da caminos alternativos, no más camino.
8. **La cobertura:**
   - en las calles, coches de 1 × 3 en línea con huecos de 1–3, árboles de acera, contenedores y barricadas;
   - en patios y plazas, la regla de `coverGaps` (ningún claro de más de 6) en todo el mapa, salvo el centro de la plaza.
9. **Las reglas de hoy siguen:** sin callejones (`openDeadEnds`), todo alcanzable (`connect`), bordes cerrados con sentido (`dressEdges`) y cada pasada con su semilla.

## 6. Lo que se mueve con la escala

Las reglas cuentan en casillas y la casilla no cambia. Casi todo se queda; lo que depende del tamaño del mapa, se ajusta.

| Ajuste | Dónde | Hoy | Propuesta |
|---|---|---|---|
| Movimiento del soldado | `SOLDIER_MOBILITY` | 8 por acción (10,7 m) | 8. Si salen más de 10 movimientos azules hasta el objetivo, 9 |
| Zombi | `ZOMBIE_PACE` | 0,6 (4,8) | 0,6 |
| Visión de soldados, de zombis y de noche | `TEMPLATES` y `NIGHT` (`sight`, `sightCut`) | 18, 14; −8 de noche (mínimo 6) | Igual. Probar 20 para los soldados en los mapas de 72 |
| Puntería por distancia | `RANGE_TABLES` | por casilla, hasta 25 | Igual |
| Radios de ruido | `NOISE` y las armas | fusil 18, escopeta 16, francotirador 22, granada 26 | Igual. En un mapa mayor, un disparo despierta menos parte del mapa: más sigilo |
| Horda | `HORDE` | `perTurn` 10, `wave` 50, `trickle` 0–3 | `trickle` × S/48 (más borde) |
| Distancia de la horda a la escuadra | `EDGE_GAP` | 12 / 8 / 0 | 16 / 10 / 0 |
| Grupos | `podPlan` | 6–10 | × S/48: unos 11 a 64 y 12 a 72 en la dificultad 2. Al lado del mapa, no a su área |
| Distancia de los grupos | `POD_DISTANCE`, separación | 16, 11 | S/3 (21 a 64, 24 a 72); 14 |
| Turnos de la misión | `missionTurns(kind, size)` | por lado del mapa: 15 / 12 / 17 a 48 | Por el recorrido real que da el generador (salida → objetivo → evacuación), calibrado para que los mapas de 48 de hoy den las mismas cifras |
| Rincones de salida y evacuación | `DEPLOY_AREA`, `EVAC_AREA` | 12, 14 | 14, 18 |
| Cajas especiales | `TILES_PER_BOX`, `MAX_BOXES` | 750, 3 | 1200, 4 |
| Farolas | `LIGHT_SPACING`; luz de farola | 8; 2 | 11; 3 |
| Rodeo por planta de la IA | `STOREY_WALK` | 3 | 4 (el tramo es más largo) |
| Coste de subir una planta | `COST_CLIMB` | +2 sobre 1 casilla | Sin recargo: cada casilla del tramo cuesta 2 y el paso al rellano, 2. Un tramo de 3 cuesta como 4 casillas, una más que hoy |
| Asedio | `SIEGE`, `siegeWaves` | 40 × 40 | 56 × 56: llegan un turno más tarde. Revisar `SIEGE.hold` (11) y las torretas jugando |

## 7. Rendimiento

### 7.1 Medido en el motor (este Mac)

Con las proporciones de hoy estiradas, ciudad y 8 semillas por tamaño:

| Lado | Generar | Visión de la escuadra | Visión de los enemigos (grupos) | Estado JSON | Nodos (casillas × niveles) |
|---|---|---|---|---|---|
| 48 | 16 ms | 1,2 ms | 4,7 ms (8) | 95 KB | 9216 |
| 64 | 25 ms | 1,0 ms | 6,5 ms (11) | 163 KB | 16384 |
| 72 | 30 ms | 1,1 ms | 7,9 ms (12) | 206 KB | 20736 |
| 96 | 59 ms | 1,1 ms | 11,9 ms (16) | 375 KB | 36864 |

- **La visión de la escuadra no crece:** se recorre solo el radio de visión.
- **La de los enemigos crece por dos lados:**
  - con el número de grupos;
  - con un `Uint8Array(área × niveles)` nuevo por cada punto de vista (`los.ts` `teamVisibility`, `tried`). Esto último sobra.
- **El alcance de un movimiento** (`computeReach`) no depende del mapa.
- **El estado** se comprime a unos 7 KB hoy, y a unos 15 KB a 72.

### 7.2 El cliente

- **Lo estático** va fundido en trozos de 8 × 8 (`batch.ts`), una llamada por material. Más trozos no añaden llamadas.
- **La cámara** ve como mucho unas 2500 casillas² (52°, distancia hasta 42). Un mapa de 48 entra entero. Desde 64, lo de fuera se descarta por trozos.
- **Crece con el área en cada fotograma:**
  - la pasada de sombra: una cámara ortográfica cubre todo el mapa;
  - el descarte y orden de trozos;
  - el fundido de la niebla: una textura W × H subida entera mientras se funde;
  - el bucle del corte de tejados por edificio.
- **Crece con el área en cada suceso:**
  - `refreshView` (visión, explorado, niebla);
  - `rebuild`: rehace solo los trozos tocados, pero `dressStreets` y `scatterCover` recorren el mapa entero. Son 4–13 ms hoy; serán 9–30 ms a 72, un tirón en cada puerta o explosión.
- **El límite duro:** la cámara de sombra tiene cerca y lejos fijos (`renderer.ts`, lejos 90). Recorta desde unos 72–80 de lado por la noche y unos 80 de día.
- **La densidad de sombra:** 57 téxeles por casilla a 48 y 29 a 96.
- **No hay datos del PC con la RTX 3090** (no hay `red.log`).

### 7.3 Arreglos antes de pasar de 64

1. **Sombra:**
   - `fitMap` con la distancia del sol y el plano lejano según el tamaño;
   - mejor aún, encuadrar la cámara de sombra en lo que se ve y no en el mapa: la densidad queda fija con cualquier tamaño.
2. **`rebuild`:** `dressStreets` y `scatterCover` por trozo sucio.
3. **`teamVisibility`:** un búfer de la ventana del radio, o uno reutilizado con sello de generación, en lugar de uno del mapa entero por punto de vista.
4. **La niebla:** subir solo el rectángulo que cambia (`updateRanges`).
5. **El explorado:** guardarlo en `sessionStorage` con retardo.

### 7.4 Límites

- **64 × 64:** seguro hoy.
- **72 × 72:** con los arreglos 1–3, y midiendo en el PC de referencia (≤4,2 ms en «alto») y en el Mac («medio» a ≥30 fps) antes de publicar.
- **96 × 96:** no. Duplica de nuevo la sombra, los tirones y la IA.
- **Edificios:** no más de unos 30 por mapa (hoy 12–14). Hay que vigilar los triángulos de los interiores.

## 8. Plan por fases

**Estado (19/10):** hechas las fases 0, 1 y 2 y los tres arreglos de rendimiento de §7 (sombra, `rebuild` por trozos y `teamVisibility`), en la sesión «Experimental/brainstorming». Las fases 3 a 6 las reparte Gameplay con Arte (EQUIPO.md). Lo que queda de rendimiento a 72: `buildWalls` (`buildings.ts`) recorre todas las casillas en cada cambio (unos 2,3 ms de los 4,2 de una puerta) y `layRaised` rehace todos los parapetos.

Cada fase sale sola, con `npm run typecheck` y `npx vitest run --root packages/engine`, y se juega antes de la siguiente.

### Fase 0 · La medición como test (½ sesión)

- **Qué:** los scripts del anexo pasan a `packages/engine/test/proportions.test.ts`, con rangos por tipo de mapa:
  - calzada y sección de calle;
  - salas de 3 × 3 o más;
  - huellas de vehículos;
  - largo de los tramos;
  - reparto del suelo;
  - distancia del recorrido.
- **Para qué:** sirve de red para todo lo demás. Al principio da las cifras de hoy.

### Fase 1 · Objetos de varias casillas: muebles y vehículos pequeños

- **Motor:**
  - en `decor.ts`, un `DecorKind` `'part'` nuevo (código al final, sin renumerar), que mira hacia la casilla ancla, y una tabla `FOOTPRINT` por tipo;
  - en `mapgen.ts` (`furnishRoom`, `furnishHome`, `furnishCareRoom`, `parkCars`, `parking`, `TRUCK`, el tractor de `furnishBarn`, la alarma de `generateLayout`): camas, sofás, bañeras, mesas y camas de hospital de 1 × 2; cama doble de 2 × 2; coche y patrulla de 1 × 3; ambulancia de 2 × 4; tractor de 2 × 3;
  - en `furnishing.ts`, `keepsWays` por grupo.
- **Cliente** (Arte): `mapView.ts` (`BLOCKS` generalizado: un modelo por grupo), `homeFurnishing.ts`, `medicalFurniture.ts`, `dressing.ts`.
- **Tests:**
  - cada grupo, entero y coherente;
  - ningún grupo corta un camino (los de alcance);
  - determinismo por semilla;
  - las salas de 3 × 3 siguen teniendo paso.
- **Riesgos:**
  - salas más llenas: la cama doble solo en 3 × 4;
  - calles de 2 o 3 en los mapas de 48: el coche de 1 × 3 solo en calzadas de 3 o más, como hoy.
  - **Partidas:** una misión empezada no tiene `'part'` y se dibuja como hoy.

### Fase 2 · Escaleras de tramo (2–3 casillas)

- **Motor:**
  - `path.ts`, `stepBetween` y `stepsFrom`: se entra al tramo solo por el pie y se sale solo por el rellano; ningún paso de lado ni en diagonal;
  - `grid.ts`, `canStandOn`: nadie se para a mitad del tramo, como en una ventana;
  - `los.ts`: el hueco de n casillas;
  - `mapgen.ts`: `placeStairs` en una caja de escalera planificada antes de partir las salas, en todas las plantas y en sentidos alternos; `upperFloors`;
  - `stairAccess.ts`, y en `ai.ts`, `STOREY_WALK`.
- **Cliente:** `buildings.ts` (el tramo, la barandilla y el hueco de n casillas), `unitViews.ts` (subir a lo largo del tramo) y el corte por plantas.
- **Tests:**
  - subir y bajar: coste y acciones;
  - no se entra de lado;
  - nadie acaba el movimiento en el tramo;
  - réplica (`rules.test.ts`);
  - presupuesto de visión (`FASE6-PLAN.md`);
  - en los mapas sin tramos largos, todo igual.
- **Riesgos:**
  - casas pequeñas sin sitio para la caja: se queda con un tramo de 2, o de una planta;
  - la IA en las escaleras.
  - **Partidas:** un tramo sin largo es de 1, el de hoy. «La torre» sigue igual.

### Fase 3 · Trazado, calles y tamaño del mapa

Antes, los arreglos 1–3 de §7.3 (Arte y Gameplay).

- **Motor:**
  - en `mapgen.ts`: `GEN_SIZE` por tipo (64 la ciudad), W ≠ H, `district` sustituido por la red de §5.1, `townLot` con aceras de 2, chaflanes, plazas y parques de §4.1, la avenida y `parkBus` de 2 × 9 con la evacuación de 3 × 7, y los camiones de 2 × 6;
  - el recorrido elegido por distancia (§5.7);
  - en `setup.ts`: `missionTurns` por recorrido, grupos y `POD_DISTANCE` por lado;
  - en `rules.ts`: `EDGE_GAP` y `trickle`;
  - las etapas de La Lata con su huella (`evacBus.ts`).
- **Cliente:** `dressing.ts` (bordes de mapas más grandes, aceras de 2), `evacBus.ts` y `ground.ts` (aceras, chaflanes).
- **Tests:**
  - alcance;
  - sin callejones;
  - determinismo;
  - proporciones de calles (Fase 0);
  - presupuestos: generar ≤40 ms, visión de los enemigos ≤6 ms (tras el arreglo 3), estado ≤250 KB;
  - una partida de bot (`scavenge-sim`) por dificultad con victorias y bajas comparables a hoy.
- **Riesgos:**
  - misiones más largas;
  - menos presión de los grupos;
  - más rendimiento: medir en el PC de referencia.

### Fase 4 · Edificios y salas

- **Motor** (`mapgen.ts`):
  - parcelas (§5.2–5.4);
  - casas adosadas;
  - L y anexos;
  - patio de manzana con vallas y pasajes;
  - bloque de pisos en el que se entra;
  - oficinas, naves y hospital del tamaño de §4.2;
  - la caja de escalera como sala (recibidor);
  - el baño de 2 × 3, si se acepta.
- **Cliente:** `buildings.ts` (fachadas adosadas sin ventanas en la medianera, zaguán) y `homeFurnishing.ts`.
- **Tests:**
  - salas de 3 × 3 o más (salvo la excepción);
  - toda sala con puerta;
  - todo patio con dos salidas;
  - ventanas solo hacia fuera;
  - `MedicalSite` y `HomeSite` coherentes.
- **Riesgos:** más edificios por mapa (como mucho unos 30), que pesan en triángulos y en registrar.

### Fase 5 · Las afueras, la fábrica, el área de servicio y el polígono

- **Qué:**
  - campos de 14–20 × 20–30;
  - camino de 3;
  - carretera de 5 con arcén;
  - patio de gasolinera de 16 × 20;
  - nave de la fábrica de 16–20 × 24–30, con muelles para camiones de 2 × 6;
  - silos de 3 × 3;
  - mapas de 72.
- **Archivos:** `mapgen.ts` (`wilds`, `roadside`, `facility`, `factory`, `field`).
- **Tests:** como en la fase 3.
- **Riesgo:** el bosque más grande, en árboles dibujados. Van en instancias; medirlo.

### Fase 6 · La base

- **Motor:**
  - en `base.ts`: `BASE_MAP` 56, el claro, `PIECES` con las huellas de §4.9, `BASTION_LOT` 9 y `BASTION_STAGES`;
  - en `campaign.ts`: `CAMPAIGN_VERSION`;
  - el asedio (`SIEGE`, `siegeWaves`).
- **Cliente:** `basePieces.ts`, `strategy/clearingArt.ts` y `mapView.ts`.
- **Migración:** la base guardada se pone en el centro del mapa nuevo (+8, +8). Cada pieza vieja guarda su tamaño (`BasePiece.size`, opcional) hasta que se mejora o se reconstruye.
- **Tests:**
  - la migración no solapa piezas;
  - el asedio llega por las pistas;
  - colocar y talar en el claro nuevo.
- **Riesgo:** la campaña en curso de Arturo y Ana. Probar con su `save.json` copiado.

## 9. Compatibilidad

- **Una misión en curso guarda su mapa entero** (casillas, plantas y decorado), así que no la toca ningún cambio del generador. Lo nuevo de las reglas tiene un valor por defecto que es lo de hoy:
  - sin `'part'`, objetos de 1 casilla;
  - un tramo sin largo es de 1;
  - un autobús de 2 × 5.
- **Una misión ofrecida y no empezada** se genera con el código nuevo: su semilla da otro mapa. No pasa nada.
- **Los mapas a mano** se quedan con su escala. Se pueden rehacer después.
- **El determinismo:** cada pasada nueva, con su semilla. Los tests comparan la misma semilla dos veces, no con el mapa de antes.

## 10. Decidido (Arturo, 19/10)

1. **Baños, aseos y despensas de 2 × 3:** sí. Es la única excepción a «salas de 3 × 3 o más» (prueba del 12/10).
2. **El tamaño del mapa:** 64 en la ciudad y el casco viejo; 72 en afueras, fábrica y polígono, tras los arreglos de rendimiento.
3. **El largo de la misión:** se mantiene el recorrido de 60–80 casillas. El mapa más grande da flancos, no más camino.
4. **Las escaleras:** tramo de 3 (38°), de 2 en las casas pequeñas. Solo se entra por el pie y nadie se para a mitad. Donde no cabe uno de 3 (tiendas pequeñas, hospitales cortos: 12 de 746 tramos), también de 2 (Arturo, 19/10).
5. **La base:** 56 × 56, con la base guardada en el centro y lo construido con su tamaño.

## Anexo · Cómo repetir la medición

En `scratchpad/mapas/` (no está en el repositorio):

- `make-probe.mjs`: copia `mapgen.ts` como `mapgen-probe.ts`, con envolturas que apuntan lo que decide (calles, manzanas, parcelas, programas y edificios). Comprueba que da lo mismo que el motor.
- `measure.mts`: las tablas de §2 (`results.txt`):

  ```bash
  npx tsx <dir>/measure.mts packages/engine/src
  ```

- `scale-bench.mts`: la tabla de §7.1.
