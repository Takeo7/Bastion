# Textos pendientes para Lore

Textos provisionales que han puesto Gameplay, Reglas y Combate mientras la sesión Lore no estaba abierta. Lore los revisa y los cambia en su sitio; el archivo de cada uno va entre paréntesis. Cuando estén revisados, se borra la línea.

Revisado por Lore el 13/10: noche, contagio y medicinas, y fase 4. Lo que estaba bien se queda como estaba. Lo que Lore ha cambiado en sus archivos, en `game.ts`, en `screen.ts` y en `content.ts` ya está hecho. Abajo solo queda lo que tiene que aplicar otro.

## Para Combate (`game.ts`)

- Los hallazgos los dicen ya `lore.ts`:
  - `findText` → `findLabel(loot)`;
  - `findLine` → `searchLine(who, decor, loot, seed)`;
  - `haulText` → `lootSummary(…, t.food, t.parts, t.weapons)`.
  - Las funciones `rations` y `partsOf` de `game.ts` sobran: `lore.ts` exporta `rations` y `partsCount`.
- Consejos de radio nuevos en `RADIO_TIPS` (`transmissions.ts`), con su momento:
  - `night`: la primera misión de noche. Puede sustituir a `NIGHT_RADIO`, que suena en cada misión de noche.
  - `bitten`: el primer `infected` de un soldado propio.
  - `risen`: el primer soldado propio que se levanta.
  - `foundWeapon`: la primera arma encontrada al registrar.
- Las escenas de la base (`firstBite`, `shortMedicines`, `firstTurned`, `hunger`, `foundWeapon`) salen solas por su condición: no hay que hacer nada.

## Para UX/UI

- Los iconos de raciones y piezas en la barra de arriba (`screen.ts`, hoy «◍» y «⚙»). En el texto, Lore usa palabras: «3 raciones», «1 pieza», «+2 raciones». Las medicinas siguen con «✚» y los suministros con «§».

## Fase 5 ([FASE5.md](FASE5.md))

Revisado por Lore el 13/10. Hasta el nivel 4 del Bastión el jugador no conoce a la Marea, y Los Llanos pueden caer antes, así que los textos dependen del nivel: antes son «los muertos». Además, ningún verbo concuerda con el nombre de la zona, que puede ser plural («Los Llanos de Ceniza»). Las funciones están en `lore.ts` y `campaign.ts` ya las llama.

Hecho: la escena de radio `firstFlood` («Los Llanos no contestan»), que sale sola cuando cae la primera zona.
