# Personajes Sidekick

Decidido por Arturo el 21/10: soldados, zombis y los vivos pasan de los personajes POLYGON a los de Synty Sidekick. Saldrá como 0.9.0. Antes se probó un prototipo de Arte con 2 soldados y 2 zombis (sin coste de fps en el Mac).

## Qué hay

Todos los paquetes están en el disco externo, en `/Volumes/4T_White/50_INBOX/Synty` (CLAUDE.md). Cada `.unitypackage` es un tar.gz con una carpeta por recurso: `pathname` y `asset`.

| Paquete | Personajes hechos | Piezas de ropa | Para |
|---|---|---|---|
| Apocalypse Survivors | 5 | 77 | los soldados |
| Apocalypse Outlaws | 5 | 330 | los vivos (Saqueador, Cabecilla, Cazador) y más soldados |
| Modern Civilians | 6 | 165 | zombis civiles, el VIP, más soldados |
| Modern Police | 9 | 80 | zombis policía, más soldados |
| Starter | 4 | 58 | piezas de relleno |
| Apocalypse Zombies | 5 | 44 (y 126 piezas de cuerpo de zombi) | los zombis |

- Arturo, el 21/10: «no solo esos». Vale cualquier pieza de cualquier paquete Sidekick que encaje en un personaje, y lo mismo para zombis y entorno con los POLYGON del disco. La condición: que pegue con un apocalipsis zombi de hoy, en invierno y en un valle; nada de fantasía ni de ciencia ficción que se note.
- Todas las piezas comparten un esqueleto de 88 huesos. El conversor de Arte (trabajo `sidekick`) las junta en uno solo, con un material y la paleta de 32×32 de cada personaje, y las adapta a nuestros 44 huesos, así que nuestras animaciones y poses sirven tal cual.
- Cuerpo: los valores BodyType, BodySize y Muscle de cada preajuste.
- Un personaje tiene 12.700–14.400 triángulos y pesa unos 200 KB (los de antes, 2.000–4.000 y 60–90 KB).

## Qué se hace

1. **Soldados (Arte):** un armario que viste a cada soldado con piezas según su ficha (`Appearance`).
   - `sex` da el cuerpo de hombre o de mujer; `build` (delgado, normal, fuerte), el tamaño y el músculo.
   - `head` (casco, capucha, boina, gorra, máscara, nada) elige la pieza de la cabeza.
   - Los colores (`primary`, `secondary`, `helmet`, `torso`, `arms`, `legs`) van a las casillas de la paleta. El color del jugador va en la ropa y en el brazalete.
   - `character` pasa a ser el conjunto de ropa. Los ids viejos (los POLYGON, como 'hunter') se traducen en el cliente a un conjunto parecido, así que las partidas guardadas no cambian.
   - Sin `character`, se elige por el id del soldado, como ahora.
2. **Zombis (Arte):**
   - Cuerpos de zombi con ropa de zombi, de civil o de policía, distintos según el id de cada uno.
   - El soldado que se levanta lleva su ropa con piel de zombi.
   - Los jefes (Corredor, Acorazado, Chillón, Hinchado) siguen como están: Sidekick no tiene nada parecido.
3. **Los vivos (Arte):** Saqueador, Cabecilla y Cazador, con los Forajidos.
4. **Brazos y armas (Arte):** volver a ajustar que no se metan en estos cuerpos, sobre todo agachado (el arma en el muslo), con la misma medida que la nota #1 del 20/10.
5. **Personalizar (UI):** elegir el conjunto de ropa entre los de su sexo, además de lo que ya hay (cabeza, cuerpo, colores). Los retratos de las tarjetas, con los personajes nuevos.
6. **Nombres (Lore):** el nombre de cada conjunto de ropa en Personalizar, corto y en español.
7. **Gameplay:** revisar, probar jugando y sacar la versión. No hace falta tocar el motor: `Appearance` ya tiene todo, y `character` admite cualquier id de hasta 24 letras y cifras.
8. **Al terminar:** quitar los personajes POLYGON que ya no se usen (menos los jefes) y el interruptor del prototipo. Medir en el Mac y, Arturo, en el PC con la 3090 (red.log).

## Estado

- 21/10: prototipo hecho (Arte) y cambio completo decidido.
- 21/10: el armario de los soldados funciona en la Escuadra (Arte). Lleva 18 conjuntos de ropa, 8 caras, 17 peinados, 8 barbas, 5 piezas para la cabeza, 6 tonos de piel y 7 colores de pelo. Las partidas guardadas conservan el aspecto. Pendiente de la revisión:
  - que no se repita el conjunto dentro de la escuadra;
  - menos policía y menos camisa con corbata;
  - ropa de invierno para los soldados (nada de pantalón corto ni brazos al aire);
  - la soldado de la máscara lleva el fusil colgando.
  En Personalizar (UI) se quitan «Visor y luces del traje» y se corrige la nota de «Detalles».
- 21/10: Personalizar con «Ropa» (los conjuntos de Arte por sexo, con los nombres de Lore y vista previa al pasar el ratón). Los retratos salen solos del armario (UI).
- 21/10: el aspecto de serie tiene su propia cabeza y complexión según el id del soldado (`defaultAppearance(…, seed)`). La campaña pasa a la v24, que se lo da a los soldados sin personalizar de las partidas de antes (Gameplay).
- 21/10, Arturo: «quitar el tinte del color del jugador y dejar solo el circulo de debajo», con un modo en Ajustes para teñir como antes. Por defecto, cada conjunto con sus colores, sin brazalete; el jugador se distingue por el círculo. La opción, apagada de serie (UI en Ajustes, Arte en el armario).
- 21/10: cambio completo hecho (Arte).
  - Soldados: 15 conjuntos sin repetir en la escuadra, con ropa de invierno, y 88 de 90 cuerpos sujetando el fusil con las dos manos.
  - Zombis: 17 de Sidekick, y el resucitado con su ropa.
  - Los vivos: Forajidos.
  - Tinte: lo controla `squadTint`.
  - Soldados mira a la cámara.
  - Quitados 34 personajes POLYGON.
  - En el Mac, los mismos fps que con POLYGON.
- 21/10, Arturo: «no tiene que quedar muy "Borderlands", quizás los cuervos si pero los buenos no». Los soldados, sobrios: nada de pinchos, crestas, parches de colores ni hombreras de Mad Max. Los Cuervos pueden quedarse con ese aire.
