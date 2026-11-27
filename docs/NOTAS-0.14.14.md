# Notas de la 0.14.14

Texto de Lore para la 0.14.14, la primera tras volver a la línea de las expediciones («volvemos a la 0.14.xx como main», Arturo, 27/11). Lo pidió Producto el 27/11 (`lore/MENSAJE-DE-PRODUCTO-0.14.14.md`). Corto. Sin commit.

**Qué lleva:** lo de unirse de la 0.12.27 que la 0.14.13 no tenía:
- desde casa, «Unirse a …» entra, también con el puerto del router abierto;
- la línea de la clave y la ayuda del router, al día;
- el aviso del Mac sin «de Aplicaciones».

El resto de lo de unirse de la 0.12.27 ya estaba en la 0.14: el aviso con la causa (0.14.12 y 0.14.13) y «Unirse por dirección» siempre a la vista. El arreglo de la sala es solo de la 0.12.

**Fuentes:**
- `producto/ENTREGA-tarjeta-sin-clave-red.md` y `PARA-PRODUCTO-partidas-0.12.26-red.md`;
- `producto/TABLERO.md` y `RUNS.md` (27/11, noche);
- el árbol: `StartScreen.cs` (`heardEntry`, `joinEntry`), `NetLink.cs` y `LanBeacon.cs` de Red, sin commit;
- las notas de la 0.12.27 (rama `aparcado/0.12`) y de la 0.14.13.

**Lo de Lore, en el árbol y sin commit:**
- **`Lore.JoinText` en `Quit.cs`:**
  - el aviso del Mac dice «bórralas», sin «de Aplicaciones» (cuadro 184);
  - «No hay camino…» y «No responde nadie…» citan «Por internet, con la clave», como se llama esa dirección en la sala (`RoomText.Internet`). Antes decían «Por internet».
- **Sus pruebas** en `LoreGoldenTests.cs`. LoreGolden, NetClient, NetLan y NetRoom en una copia: 81 correctas, 0 con error.
- **Para UI,** ya pasadas:
  - `RoomText.KeyLine`: «Clave de la partida: {key}. Se pide al entrar por internet, y también desde tu red, por dirección, si el puerto del router está abierto.»;
  - la tercera frase de `RouterText.About`: «Con el puerto abierto, desde tu red se entra sin clave con «Unirse a…»; quien entra por primera vez por dirección sí la necesita.»

**Sin probar, para Producto:**
- **Nada en la app,** ni entre dos ordenadores de verdad. La llamada nueva de `NetLink` solo está compilada (Red).
- **La clave va en el anuncio:** cualquier aparato de la red de casa puede leerla mientras dura la sala (Red). No va en las notas.
- **«Partidas guardadas»:** el párrafo de la expedición apartada lleva las instrucciones a mano. Si Red hace que la 0.14.14 la recupere sola, se cambia.

Es para Arturo y Ana: cuenta qué cambia al jugar. Va para Mac y Windows; lo del iPhone y el iPad, para la app que Arturo instala desde Xcode. Producto completa al publicar lo que va entre [corchetes]: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.14.14: unirse desde casa

Versión 0.14.14 (commit [commit]). Vuelven las expediciones: sigue a la 0.14.13. Desde casa, «Unirse a …» ya entra, también con el puerto del router abierto.

## Partidas guardadas

- **La partida de la 0.14.13 sigue en la 0.14.14.**
- **Si con la 0.12.26 o la 0.12.27 pulsasteis «Nueva partida» o «Alojar partida»,** esas versiones apartaron la expedición sin avisar. La 0.14.14 la recupera sola al abrirse: vuelve a salir «Continuar partida». No se ha probado con vuestras carpetas: por si acaso, antes de abrirla, copiad a otro sitio la carpeta de datos (en el Mac, `~/Library/Application Support/Bastion-Unity/Bastion/`; en Windows, `%USERPROFILE%\AppData\LocalLow\Bastion-Unity\Bastion\`). Lo que la 0.12 dejara en su sitio no se borra: queda al lado, como `save.antes-de-las-runs.json` o `save.vestibulo.json`.
- **Un caso que no recupera sola:** si después de alojar con la 0.12 jugasteis una escaramuza. Entonces, a mano y con la app cerrada:
  1. copiad a otro sitio la carpeta de datos entera:
     - Mac: `~/Library/Application Support/Bastion-Unity/Bastion/`;
     - Windows: `%USERPROFILE%\AppData\LocalLow\Bastion-Unity\Bastion\`;
  2. en esa carpeta, sacad el `save.json` que haya;
  3. renombrad `save.ilegible.json` a `save.json`; si no está, `save.antes-de-campana-nueva.json`.

## Novedades de la 0.14.14

### Unirse

- **Desde casa, «Unirse a …» ya entra,** también con «Abrir el puerto del router» en «Sí». Hasta ahora, con el puerto abierto, la partida pedía la clave también desde casa y «Unirse a …» no la llevaba, así que no se entraba.
- **La línea de la clave en la sala y la ayuda de «Abrir el puerto del router»** ya dicen lo de ahora: desde casa, la clave solo se pide a quien entra por dirección.

## Lo que sigue sin arreglar

- **Entre vuestros dos ordenadores, la conexión aún no llega,** y no se sabe por qué. Si os pasa, apuntad qué dice el aviso y guardad el `red.log` de los dos, en la carpeta de datos.
- **Unirse no se ha probado entre dos ordenadores:** las pruebas se han hecho con las dos apps en el mismo ordenador.
- **El velo del menú de inicio,** en la app, sale más flojo que en el editor: la mitad de arriba se ve más clara.
- **El icono de la esquiva** en la hoja (las flechas «»»») no se entiende sin su ayuda.
- **Algunas ayudas de la hoja** solo dicen el nombre (la salud, la voluntad, el daño…) y no qué es.
- **Al abrir «Unirse por dirección» con el teclado,** hay que pinchar el campo para escribir.
- **«Alojar» con una partida guardada** enseña la hoja «Empezando una partida nueva», aunque abre la guardada.
- **Un soldado o un cuerpo** puede quedar tapado por un edificio sin descubrir.
- **Los marcos de una casa sin descubrir** se ven claros, como si diera la luz de la calle.
- **Una farola** puede quedar en el bordillo, delante de las puertas de La Lata.
- **El aviso del compañero caído** («Pierre Lambert ha caído · Ver (F)») tapa «Turno enemigo».
- **Los guardias sentados de la hoguera,** con la cámara normal, parecen de pie.
- **Quedan saltos de animación:**
  - un soldado se levanta de golpe;
  - algunos muertos suben el brazo o bajan el cuerpo de golpe;
  - el malherido de la Camilla, si muere, se pone de pie de golpe antes de caer.

  Si veis alguno, apuntad dónde y cuándo.
- **La dificultad** se ajusta jugando: apuntad lo que notéis en cada nivel.
- **La sincronía vertical con la pantalla dormida,** o apagada al momento con una esquina activa, está sin probar.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.14.14-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.14.14-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.14.14:** con la 0.14.13 o con la 0.12.27 no se puede jugar juntos.
- **En el Mac,** la app se llama «Bastión 0.14.14» y no sustituye a la 0.14.13. Borrad las versiones viejas, también `Bastion.app` (la 0.12.26 o la 0.12.27) si la tenéis: con varias, el Mac puede dejar a la nueva sin red local. Las partidas y los ajustes son los mismos. Si teníais la vieja en el Dock, poned la nueva.
- **En Windows,** la carpeta se llama «Bastion 0.14.14».
- **Descargas solo para Mac y Windows:** no hay versión para Linux ni para el navegador. En el iPhone y el iPad, la app se instala desde Xcode.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **En el Mac, para unirse:** en Ajustes del Sistema → Privacidad y seguridad → Red local, activad «Bastión 0.14.14». Si salen otras, borradlas.
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
