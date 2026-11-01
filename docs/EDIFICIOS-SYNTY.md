# Edificios: inventario de Synty y propuesta (nota #3 de Ana, 1/11)

Ana: «Las fachadas repiten la misma textura y los edificios parecen variaciones de una misma construcción, cambiando principalmente su tamaño». Arturo pidió primero un inventario de los paquetes y una propuesta con capturas, antes de tocar el generador, empezando por la ciudad.

Capturas en `.dev-snapshots/arte/unity/edificios/nota3-01-11/`. El inventario completo, pieza a pieza, en `inventario.json` de esa carpeta.

## 1. Qué hay hoy y por qué se repite

Los edificios del combate se montan con piezas de muro casilla a casilla (`WallLayout`), según el plano del motor. El cliente ya sabe qué es cada edificio: casa, tienda, oficina, motel, nave, clínica, granero (`BuildingPlan`). Pero solo hay siete juegos de piezas:

| Juego | De dónde sale | Dónde se usa |
|---|---|---|
| Casa | Town: un solo muro (`House_ExteriorWall_GroundFloor_01`), una ventana, una puerta, una esquina | Casas, oficinas, tiendas, motel y clínicas de la ciudad |
| Ladrillo | Apocalypse: el ladrillo de la nave (`Warehouse_Brick_01`) | Salones y supermercados |
| Búnker | Apocalypse: muro de hormigón | Instalaciones |
| Naves de chapa (gris, roja, azul) | Woodland: el contenedor | Naves de las afueras |
| Escaparate | Office: cristal y puerta de cristal | La planta baja de las tiendas |
| Bloque | City: los dos edificios de pisos (`Apartment_01` y `02`), cortados | Las plantas altas de oficinas y tiendas altas |
| Valla | Apocalypse: el muro de la cuarentena | Patios |

Por eso todos los edificios son la misma caja: el mismo muro, la misma ventana, tejado plano con pretil. Solo cambia el tamaño y el color del tinte (`BuildingLooks`). (Corrección del 1/11: las casas no; ya llevaban un tejado a dos aguas hecho a mano, con las tejas de Town, siempre el mismo.)

## 2. Lo que hay en el disco

186 paquetes en `/Volumes/4T_White/50_INBOX/Synty`. Quedan fuera los de fantasía, ciencia ficción, oeste, piratas y vikingos, y los SIMPLE y MINI (son de otro estilo, CLAUDE.md). Para una ciudad moderna sirven estos (contados en sus SourceFiles):

| Paquete | En el proyecto | Piezas de edificio | Qué trae para la ciudad |
|---|---|---|---|
| Town | Sí | 146 | Casas por piezas: 3 muros de planta baja y 4 de planta alta, 11 ventanas, garajes, porches, 26 piezas de tejado a dos aguas, chimeneas. 11 casas enteras (`House_Preset_01`–`11`) y 3 tiendas. |
| City | Sí | 85 | 6 tiendas pequeñas con 5 marquesinas, pisos por plantas, oficinas antiguas de piedra (pequeña y grande), oficinas redondas, cuadradas y octogonales, ayuntamiento, estación, escaleras de incendios. |
| Apocalypse | Sí | 452 | Edificios enteros: casas (3), tiendas (pequeña, mediana, grande), café, *diner*, comercio (3 tamaños), mercado, motel, taller, naves de ladrillo y de hormigón, industria (3 tamaños), torres de pisos. Casi todos con versión dañada. |
| Office | Sí | 129 | Muros de oficina por piezas: con ventana, de cristal, de ladrillo, lisos, columnas, toldos de ventana. |
| Construction | Sí | 85 | Casa en obra, estructura de hormigón con ferralla, casetas, grúa. |
| Shops | No | 244 | Edificios de tiendas por piezas (muros, columnas, cornisas, toldos, escaparates, carteles) con texturas propias que se repiten: ladrillo de color y blanco, revestimiento, bloques de hormigón, mármol, cinco azulejos, madera. |
| Shopping Plaza | No | 85 | El mismo sistema de Shops, para un centro comercial. |
| Palm City | No | 372 | Ciudad de costa: villas, muros con molduras y arcos, cuatro ladrillos, grafitis, carteles. |
| Gang Warfare | No | 99 | Barrio duro e industrial: muros de ladrillo y de chapa, muelles de carga, vallas. |
| Police Station | No | 93 | Una comisaría por piezas. |
| Apocalypse Wasteland | No | 349 | Chabolas, gasolinera, garajes, plataformas: más de páramo que de ciudad. |
| Horror Mansion, Horror Asylum | No | 209, 176 | Una mansión y un manicomio: un edificio especial cada uno, no una calle. |

Casi todos traen su textura en 12 versiones de color (`_01_A` a `_04_C`). En Town cambian el color del revestimiento, del tejado, del ladrillo y de las baldosas (`2-town-12-atlas.jpg`). Las de Town, City, Apocalypse y Office ya están en el proyecto.

Portadas de los paquetes: `4-paquetes-portadas.jpg`.

## 3. Probado y descartado: cambiar solo la textura

Probé a dar a cada edificio una de las 12 versiones de su textura (`MapLab` con `BASTION_ATLAS=1`): `2-probado-atlas-antes-ahora.jpg`. No basta, y queda peor:
- el revestimiento pierde las vetas y sale en colores planos y fuertes (naranja, mostaza);
- el color ya variaba antes con el tinte;
- la forma sigue siendo la misma caja.

## 4. Propuesta

Lo que falta es forma, no color: tejados, porches, escaparates, cornisas y materiales distintos según qué es el edificio.

**La maqueta.** Una calle hecha con los edificios enteros que ya están en el proyecto (laboratorio `EdificiosLab.Calle`, a su escala y con nuestra luz), en cuatro tramos:

| Tramo | Edificios |
|---|---|
| Afueras | Casas de Town y de Apocalypse |
| Calle comercial | Tiendas de City y de Town, café, *diner* |
| Centro | Oficinas antiguas de City, comercio de Apocalypse |
| Polígono | Taller, naves, industria, motel, mercado |

Capturas: `3-maqueta-cuatro-tramos-48.jpg` (cada tramo entero) y `3-maqueta-cuatro-tramos-24.jpg` (a la distancia del juego). No son nuestros edificios: es a lo que se puede llegar.

**Cómo se llevaría al juego.** El generador del motor no cambia. Cada tipo de edificio que el cliente ya conoce tendría su propio juego de piezas, sacadas de esos edificios enteros como ya se hizo con los bloques de City. Por fases:

1. **Casas** (Town, ya en el proyecto):
   - tejado a dos o cuatro aguas en lugar del plano con pretil;
   - los tres muros de planta baja y los cuatro de planta alta;
   - varias ventanas;
   - porche y garaje donde quepan.
   Es lo que más se ve.
2. **Tiendas.** La planta baja con los escaparates y marquesinas de las tiendas de City y Town, toldos y carteles; arriba, los pisos.
3. **Oficinas y bloques del centro.** Las fachadas de piedra con cornisa de las oficinas antiguas de City y los muros de Office.
4. **Afueras e industria.** Taller, naves de ladrillo y de hormigón, mercado (Apocalypse).

Si después hace falta más variedad de materiales, se importan Shops (ladrillos, revocos, azulejos y madera que se repiten), Gang Warfare (ladrillo y chapa) y Palm City.

**El reparto por la ciudad** (centro alto, calles de tiendas, casas en los bordes, industria a un lado) es del generador del motor, que hoy elige el uso de cada manzana al azar según su tamaño (`Mapgen/Lots.cs`). Si Arturo lo quiere, es trabajo de Gameplay. Con las fases de arriba, cada edificio ya se vería según lo que es, esté donde esté.

## 5. Para decidir

- Si la dirección vale (forma por tipo de edificio, con piezas de Synty), y por qué fase empezar. Propongo empezar por las casas.
- Si se ordena la ciudad por zonas en el generador.
- Si se importan Shops, Gang Warfare o Palm City, o de momento solo lo que ya está en el proyecto.

## 6. Lo que decidió Arturo (1/11) y lo hecho

- **Empezar por las casas** (fase 1), con tejado inclinado a dos o cuatro aguas, porche, garaje y varias ventanas, de Town. Las casas no tienen azotea: el motor ya no deja pisar su tejado ni subir a él (`Upstairs.Pitched`), y el cliente pone el tejado inclinado solo donde nadie lo pisa. Tiendas, oficinas y bloques siguen con su azotea. **Hecho el 1/11** (detalle en [UNITY-ARTE.md](UNITY-ARTE.md), «Las casas, fase 1»; capturas en `.dev-snapshots/arte/unity/edificios/casas-01-11/`).
- **Importar Shops, Gang Warfare y Palm City.** **Hecho el 1/11** (`importar-synty.sh`, fuera de git). Aún no se usan.
- **Zonas:** cada ciudad con su carácter al azar (más de viviendas, más comercial, mixta…) y los usos de las manzanas al azar según ese carácter. Es del generador del motor (Gameplay).
