# Notas de la 0.14.10

Texto de Lore para la 0.14.10. Lo pidió Producto el 25/11 (`lore/MENSAJE-DE-PRODUCTO-0.14.10.md`). Corto. Sin commit.

**Qué lleva:**
- la tanda 2 de las mejoras de la base: Taller, Chatarrero y Baúl de La Lata, con el baúl en el Trueque (cuadros 165 y 167);
- un arreglo de red.

**Fuentes:**
- `producto/mejoras-base-2026-11-25/`: `CONTRATO.md`, `PLAN.md` y `TEXTOS.md`;
- `producto/ENTREGA-0.14.10-gameplay.md` y `ENTREGA-0.14.10-UI.md`, con las fotos de `ui/unity/runs/01410/`;
- `producto/PARA-PRODUCTO-0.14.10-red.md`;
- `producto/RUNS.md`, cuadros 165 y 167.

**Lo que no va:** el bot (no usa el taller, el chatarrero ni el baúl), las pruebas de la sala y lo que solo cambia en el iPad y el iPhone.

**Las palabras de las fotos:** bien. Son «Hacer», «Cambiar», «BAÚL», «→ BAÚL», «PUESTO» con el candado, «Eso no se puede cambiar.» y, al volver, «Lo del baúl pasa al almacén.».

**Sin probar, para Producto:**
- **El equilibrio:** la sonda dice que el Baúl sube lo que se trae a casa un 21–36 % con dos soldados y un 5–8 % con cuatro, y el Chatarrero, un 13–15 %. Es un techo.
- **Nada en la app;** ni a dos jugadores ni con el dedo.
- **Partidas guardadas:** el baúl va vacío en las de antes. Lo cubren las pruebas, no un guardado real.

**Hecho por Gameplay:** los dos porqués con baúl, `NoRoomToBuy` y `SuppliesNoRoom(…, trunk)`, ya en `Gears.cs` (suite 1694/0).

**Reglas corregidas por Producto (cuadro 167, según Gameplay):**
- lo que se cambia va a la mochila del que cambia y, si no cabe, al baúl; ya no a la de otro soldado;
- los § del baúl también pagan, después de los de las mochilas.

Las notas lo dicen así. UI ya enseña arriba «Tus soldados llevan 35 §, y el baúl, 30 §.» cuando el baúl tiene § (sin foto mía).

Es para Arturo y Ana: cuenta qué cambia al jugar. Va para Mac y Windows. Producto completa al publicar lo que va entre [corchetes]: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.14.10: el Taller, el Chatarrero y el baúl de La Lata

Versión 0.14.10 (commit [commit]). Tres mejoras más de la base y un arreglo de red.

## Lo que se arregla

- **Al cerrar una partida a dos,** el aviso de cierre podía cortarse, y el compañero se quedaba intentando reconectar.

## Partidas guardadas

- **La partida de la 0.14.9 sigue en la 0.14.10,** con las mejoras nuevas sin comprar.

## Novedades de la 0.14.10

### Taller (100 / 200 §)

- **Una pestaña nueva en la base, «Taller».** Con los § del almacén se hacen:
  - cargadores, 15 §;
  - flechas, 15 § las 5;
  - cócteles molotov, 10 §.
- **En el nivel 2,** también accesorios: mira, silenciador, culata y cargador ampliado, a 45 § cada uno.
- **Cada cosa con «Hacer»** y su precio. Lo hecho va al almacén.

### Chatarrero (120 §)

- **En la misma pestaña,** lo del almacén se cambia por §: «Lo que le ofreces», con lo que paga en verde y «Cambiar».
- **Paga la mitad que el Trueque,** sin contar «Buen trato».
- **No coge lo que no se cambia en el Trueque:** los §, el cuchillo, lo de una misión ni lo que da el Botiquín de salida.

### Baúl de La Lata (120 / 200 §)

- **La Lata lleva un baúl de 4×2 casillas;** en el nivel 2, de 4×4.
- **Se usa entre etapa y etapa,** en las paradas y en el mapa de etapas, junto al equipo del soldado. En combate, no.
- **Al volver a la base,** lo que lleve pasa al almacén, también si cae la escuadra. Lo que no cabe en el almacén se pierde, y se dice.
- **En el Trueque:**
  - también se puede ofrecer lo del baúl: lleva la marca «BAÚL»;
  - lo que se cambia y no cabe en la mochila del soldado va al baúl («→ BAÚL»). Si tampoco cabe allí, no se puede;
  - los § de lo que se ofrece van a su mochila; si no caben, a la de otro soldado tuyo, y si no, al baúl;
  - **los § del baúl también pagan,** después de los de las mochilas. Arriba se ven los dos: «Tus soldados llevan 35 §, y el baúl, 30 §.».

### Las mejoras de la base

- **Con estas, son nueve de las diez nuevas.** Queda la Camilla.

## Lo que sigue sin arreglar

- **Al abrir «Unirse por dirección» con el teclado,** hay que pinchar el campo para escribir.
- **«Alojar partida» con una partida guardada** enseña la hoja «Empezando una partida nueva», aunque abre la guardada.
- **Un soldado o un cuerpo** puede quedar tapado por un edificio sin descubrir.
- **Los marcos de una casa sin descubrir** se ven claros, como si diera la luz de la calle.
- **Una farola** puede quedar en el bordillo, delante de las puertas de La Lata.
- **El aviso del compañero caído** («Pierre Lambert ha caído · Ver (F)») tapa «Turno enemigo».
- **Los guardias sentados de la hoguera,** con la cámara normal, parecen de pie.
- **Quedan saltos de animación:**
  - un soldado se levanta de golpe;
  - algunos muertos suben el brazo o bajan el cuerpo de golpe.

  Si veis alguno, apuntad dónde y cuándo.
- **La dificultad** se ajusta jugando: apuntad lo que notéis en cada nivel.
- **La sincronía vertical con la pantalla dormida,** o apagada al momento con una esquina activa, está sin probar.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.14.10-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.14.10-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.14.10:** con la 0.14.9 no se puede jugar juntos.
- **En el Mac,** la app se llama «Bastión 0.14.10» y no sustituye a la 0.14.9: se quedan las dos. Borrad la vieja cuando queráis; las partidas y los ajustes son los mismos. Si teníais la vieja en el Dock, poned la nueva.
- **En Windows,** la carpeta se llama «Bastion 0.14.10».
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
