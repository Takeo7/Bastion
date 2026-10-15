# Fase 8 · Saquear, la mochila y las armas desde cero

Tres notas de la prueba del 14/10 ([PRUEBA-2026-10-14.md](PRUEBA-2026-10-14.md), #6, #7 y #9) que van juntas: lo que se registra llena la mochila, y de ahí salen las armas. Es la propuesta de Gameplay, con las respuestas de Arturo del 14/10.

## A. Las armas, desde cero (solo en la campaña)

- **Cada soldado lleva tres cosas:**
  - un **arma principal**, que puede faltar: fusil, escopeta, fusil de precisión o ametralladora;
  - la **pistola**;
  - un arma **cuerpo a cuerpo**: el cuchillo, y más adelante el machete.
- **La clase ya no trae arma.** Las habilidades y talentos de un arma piden llevarla puesta, no ser de esa clase. Por ejemplo, «Necesita el machete», «Necesita un fusil de precisión» o «Necesita una pistola»; el disparo lejano del Francotirador va con el fusil de precisión. Sin arma principal, se dispara con la pistola.
- **Al empezar la campaña, todos llevan cuchillo y pistola, y nada más.** La pistola sale solo con el cargador que lleva puesto, sin ninguno de repuesto. «La primera partida la sufras.»
- **Las armas principales se consiguen de tres formas:**
  - **encontrándolas** en las misiones: cajas de armas, o registrando;
  - **fabricándolas** en el Taller, con piezas;
  - **desbloqueándolas** con el nivel del Bastión.

  Van a la Armería y se reparten a cada soldado, como hoy las armas encontradas.
- **Los cargadores de repuesto** dejan de ser 3 fijos. Salen de la Armería: el Taller fabrica munición, y cada nivel da más cargadores a cada soldado al salir de misión. En la misión se siguen encontrando.
- **La pistola cuenta balas,** como el resto.
- **El nivel del arma** (las mejoras del Taller: nivel 2 y 3) sigue como hoy, para todas las armas del soldado.
- **La escaramuza no cambia:** cada clase con su arma, como ahora.

## B. Registrar por habitaciones, como en Zombicide

- **Registrar es una acción** (1) que se hace **dentro de una habitación** de una casa, una tienda, una oficina o un almacén. Cada habitación se registra **una vez para toda la escuadra**: la primera vez que alguien lo hace.
- **Lo que sale depende del edificio,** como hoy con los muebles: en casa, comida y medicinas; en la oficina, piezas; en la tienda, comida; en el almacén, de todo.
- **Los muebles dejan de registrarse uno a uno.** **Los coches ya no se registran.**
- **Las cajas especiales:** 3 como mucho, según el tamaño del mapa, dentro de edificios o en patios. Cada una es de un tipo:
  - **armas:** un arma principal;
  - **medicinas:** 1 o 2;
  - **munición:** 2 cargadores;
  - **comida:** 3 a 5 raciones.

  Llevan su marca (un icono por tipo), **que solo se ve cuando la caja está a la vista**; una vez vista, la marca se queda en el mapa explorado. Se abren con una acción, junto a ellas, y dan siempre lo de su tipo.

## C. La mochila

- **Huecos:** 3, y uno más con la Chapistería de La Lata.
- **Soltar:** cualquier cosa de la mochila se deja en el suelo, sin gastar acción. Queda en la casilla y la recoge quien pase por encima.
- **Dar:** se pasa una cosa a un compañero de al lado, con una acción, si le cabe.
- **Mochila llena:** al encontrar algo con la mochila llena, se elige qué te quedas. Lo que no, cae al suelo.
- **Equipar un arma encontrada:** en la misión, con una acción. La principal que llevaba pasa a la mochila, o al suelo si no cabe.
- **Usar medicinas:** en la misión, con una acción, en uno mismo o en un compañero de al lado. **Solo cura vida** (3).
- **Que además paren el contagio** de un mordido se desbloquea de dos formas: con un nivel de la Enfermería, para todos; o con un talento de la rama de apoyo, para el soldado que lo tenga.
- **Comida, piezas, suministros y accesorios:** solo se llevan, como hoy.
- **Lo que se trae a casa:** lo que vuelve en el autobús, como ahora.

## Cómo queda en el motor (Gameplay, 14/10)

**Las armas**
- La Armería guarda armas sueltas (`c.foundWeapons`, `content.ts FOUND_WEAPONS`), cada una para un soldado de cualquier clase: una en la mano y una al cinto como mucho.
  - Normales: fusil de asalto, escopeta, fusil de precisión, ametralladora (dispara como el cañón) y machete (al cinto, en lugar del cuchillo).
  - Buenas, las de antes (`better`): un nivel de arma más.
- Sin arma en la mano, se dispara con la pistola. Todos tienen el Tajo (cuchillo o machete), y la acción Pistola solo quien lleva un arma en la mano.
- La pistola cuenta balas: 6 por cargador (`PISTOL_CLIP`). Los cargadores de repuesto valen para todas; recargar llena el arma principal y, si ya está llena, la pistola.
- Lo que trae el nivel del Bastión (`LEVEL_ARMS`): al 2, un fusil de asalto y una escopeta; al 3, un fusil de precisión y un machete; al 4, una ametralladora.
- El Taller (`CRAFTS`, tipo `arms`), una cada vez y tantas como haga falta: fusil y escopeta desde el principio, el machete desde el nivel 2 del Bastión, el fusil de precisión desde el 3 y la ametralladora desde el 4.
- Munición: tres encargos del Taller (`ammo1`–`ammo3`), cada uno un cargador de repuesto más para cada soldado que sale. Al empezar, ninguno.
- Talentos de arma (`kit`): se aprenden siempre y duermen hasta llevar el arma (`carriesKit`). El Grillo sigue siendo del Especialista y el lanzagranadas del Granadero.
- La visión de escuadra es del Francotirador con fusil de precisión en la mano (`hasSquadsight`).
- Partidas guardadas (versión 16): cada soldado conserva el arma de su clase, ahora suya en la Armería, y el Asalto su machete; los tres cargadores de antes son los tres encargos de munición, ya hechos.
- En la misión, cada soldado lleva `Unit.arms` (qué lleva y de qué arma de la Armería es) y `Unit.pistolAmmo` (la pistola junto a un arma principal).

**La mochila** (orden `item`)
- `drop` y `take`: soltar y coger, gratis, incluso sin acciones. Lo suelto queda en `GameState.dropped`, en la casilla.
- `give`: dar a un compañero de al lado, con una acción, si le cabe.
- `use`: una medicina, en uno mismo o en un compañero de al lado, con una acción. Cura 3 (`MEDICINE_HEAL`) y para el contagio con la Enfermería a nivel 2 (`mission.medicineCures`) o con el talento Manos de enfermero.
- `equip`: un arma de la mochila a la mano, con una acción. La que llevaba pasa a la mochila, o al suelo si no cabe.
  - Al volver, el arma que lleva en la mano es suya.
  - La suya, si vuelve en una mochila, va a la Armería; si se quedó en el suelo, se pierde.
- Eventos: `lootDropped`, `lootTaken`, `lootGiven`, `medicineUsed` (con `healed` y `cured`) y `weaponEquipped`.
- Huecos: 3, y 4 con la Chapistería (`Unit.packSlots`).

## Quién hace qué

- **Gameplay:** las reglas en el motor (armas por soldado, cajas, habitaciones, mochila), con tests.
- **Arte:** las cajas especiales y sus marcas; los objetos sueltos en el suelo; las armas nuevas en la mano.
- **UI:** la mochila en la ficha (soltar, dar, equipar, usar); elegir al encontrar con la mochila llena; la Armería con las armas por soldado.
- **Lore:** los nombres de las armas y las cajas, y los textos.
