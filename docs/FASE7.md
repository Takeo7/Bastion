# Fase 7 · Lo que quedó de las plantas, y la munición

Lo que [FASE6-PLAN.md](FASE6-PLAN.md) dejó para más adelante (decisiones 4 y 5 y las escaleras de un solo lado), y la munición contada que [FASE4.md](FASE4.md) aplazó. Lo pidió Arturo el 14/10. Las reglas las fija Gameplay; cada número queda aquí para que Arturo lo cambie si no le gusta en una partida.

## 1. Saltar por una ventana de arriba (hecho en el motor)

- Desde una ventana de una planta de arriba se puede saltar a lo que haya debajo, como desde el borde de una azotea: hasta 2 plantas y sin forjados ni tejados en medio.
- **Coste:** subirse a la ventana, como hoy (`COST_ORTHO + COST_VAULT`); bajar, `COST_ORTHO + COST_DROP` por planta. Desde la primera planta a la calle son 4 casillas en total.
- Rompe el cristal, como cualquier ventana que se salta. No hace daño.
- Lo hacen también los zombis y los vivos. Las patrullas que aún no han visto a la escuadra no saltan por ventanas, como hoy.
- «Saltar por la ventana», del Oficio, sigue siendo de la planta baja.
- **Cliente:**
  - Arte (personajes): el salto desde una ventana alta, que es saltar la ventana y caer;
  - UI: que la previsión del camino lo enseñe bien.

## 2. Escaleras que solo se dejan por un lado (hecho en el motor)

- Una escalera de edificio (`ladder` con `decor` `'stairs'`) se sube y se baja solo por su rellano: la casilla de arriba a la que mira.
- No se baja desde los lados de la escalera ni se salta al hueco.
- Abajo, el pie de la escalera es una casilla como otra cualquiera.
- Las escaleras de mano siguen como hoy: se dejan por cualquier lado.
- La escalera se vuelve un paso obligado. Los mapas generados siguen sin bolsas sin acceso (el test lo comprueba).
- **Cliente:** Arte pone barandilla en los lados del tramo y del hueco. Hoy no la hay, porque las reglas dejaban salir por cualquier lado.

## 3. Munición contada (hecho en el motor)

- Cada soldado sale con **3 cargadores de repuesto** para su arma principal. Recargar gasta uno. Sin cargadores no se recarga: quedan la pistola, el cuerpo a cuerpo y los explosivos.
- **Registrar** puede dar un cargador.
- Al volver a la base se reponen: no hay reserva que comprar. Si en las partidas hace falta, ya vendrá.
- Solo la escuadra. Los vivos enemigos recargan sin límite por ahora: su IA no sabe quedarse sin balas.
- Las misiones guardadas de antes, sin el dato, empiezan con los 3.
- **Cliente:**
  - UI: los cargadores en la ficha del soldado, y «Recargar» desactivado con su motivo;
  - Lore: los textos del hallazgo y del motivo.

## 4. Paredes que se rompen (hecho en el motor)

- La granada, el lanzagranadas y la «Granada del chaleco» de los enemigos rompen las paredes que toca la explosión, en su planta: la pared pasa a escombros (cobertura baja).
- Una pared «tocada» es la que está dentro del radio y junto a una casilla de la explosión.
- No se derrumba nada encima: la planta de arriba sigue en pie.
- El molotov, el humo, la descarga del dron y el reventón del Hinchado no rompen paredes.
- **Cliente:** Arte dibuja el hueco con escombros cuando llega `tileChanged` de una pared.

## 5. Suelos que se rompen (hecho en el motor)

- Una de esas granadas que estalla en una planta de arriba o en una azotea abre un agujero en el forjado de la casilla donde cae.
- Quien esté encima cae al forjado de debajo o al suelo, y recibe 1 de daño por planta (`FALL_DAMAGE`), que el blindaje no para. Si ahí ya hay alguien, cae en el sitio libre más cercano, a 2 casillas como mucho. Si no hay ninguno, el suelo aguanta.
- El objetivo que esté tirado ahí también cae.
- Por el agujero se ve y se puede bajar saltando, como por un borde.
- **Eventos nuevos:** `floorBroken` (el forjado de esa casilla desaparece) y `fell` (quién cae, de dónde y adónde); el daño llega después, como `damaged`.
- **Cliente:**
  - Arte rehace esa planta con el agujero y anima la caída;
  - UI la cuenta en el registro y enseña en la previsión de la granada qué paredes y qué suelo se rompen;
  - Lore pone los textos.

## Orden

1. Ventanas y escaleras: hecho en el motor, con tests.
2. Munición contada: hecho en el motor, con tests.
3. Paredes: hecho en el motor, con tests.
4. Suelos: hecho en el motor, con tests.
5. El cliente: hecho por Arte (barandillas, saltos y caídas, boquetes con muñón y escombros, agujeros en los forjados; la pared rota lleva `decor` `'breach'` para que se vea igual tras recargar), UI (cargadores en la ficha, previsión de paredes y suelos, registro) y Lore (textos y el consejo `noMags`).

360 tests (346 antes de la fase). Va en la 0.2.3.
