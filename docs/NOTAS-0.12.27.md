# Notas de la 0.12.27

Texto de Lore para la 0.12.27, la primera sobre la 0.12.26 tras aparcar la 0.14 (cuadro 182). Lo pidió Producto el 27/11 (`lore/MENSAJE-DE-PRODUCTO-0.12.27-notas.md`). Corto. Sin commit.

**Qué lleva:** unirse (cuadros 183 y 186):
- «Unirse por dirección» siempre a la vista;
- la tarjeta «Unirte a la partida de…» entra sola desde casa, también con el puerto del router abierto;
- el aviso al no llegar a quien aloja dice la causa, y `red.log` la guarda.

**Fuentes:**
- `producto/ENTREGA-unirse-0.12.27-UI.md`, con las fotos de `ui/unity/runs/unirse-0.12.27/`;
- `producto/ENTREGA-tarjeta-sin-clave-red.md`, `PARA-PRODUCTO-partidas-0.12.26-red.md` y `PARA-PRODUCTO-0.12.27-herramientas-red.md`;
- las fotos de la sala en `ui/unity/runs/sala-0.12.27/` (antes y después, con el puerto sin abrir);
- el commit 8c8b1a5 (`NetLink.NotReached`, con `Lore.JoinText`);
- `producto/RUNS.md`, cuadros 182 a 187.

**Lo de Lore:**
- **`Lore.JoinText`,** en 8c8b1a5.
- **Un cambio después, en el árbol y sin commit:** el aviso del Mac decía «bórralas de Aplicaciones». En el cuadro 184 Arturo dijo que sus apps no están en Aplicaciones, así que ahora dice solo «bórralas». `Quit.cs` y su prueba; LoreGolden y NetClient, 0 con error en una copia.

**Sin probar, para Producto:**
- **Nada en la app;** ni entre dos ordenadores de verdad.
- **El aviso con la causa** solo se ha visto puesto a mano en el laboratorio de UI, no llegando por `NetLink`.

**La línea de la clave y la ayuda del router:** UI ya las ha cambiado con las frases de Lore (`RoomText.KeyLine` y `RouterText.About`).

**El nombre en «Red local»:** Red cree que saldrá «Bastion», sin tilde, como en el Finder; no lo ha visto. El aviso del juego dice «activa «Bastión»». Si sale «Bastion», es una palabra en `Quit.cs` y su prueba, para la siguiente (el árbol está congelado). Las notas dicen las dos formas.

Es para Arturo y Ana: cuenta qué cambia al jugar. Va para Mac y Windows, sin nada de iOS. Producto completa al publicar lo que va entre [corchetes]: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.27: unirse desde casa

Versión 0.12.27 (commit [commit]). Sobre la 0.12.26: lo de la 0.13 y la 0.14 queda aparte. Para unirse desde casa, la tarjeta entra sola, la dirección siempre está a mano y, si no se llega, el aviso dice por qué.

## Novedades de la 0.12.27

### Unirse

- **La tarjeta «Unirte a la partida de …» entra sola desde casa,** también con «Abrir el puerto del router» en «Sí». Antes, con el puerto abierto, la partida pedía la clave también desde casa y la tarjeta no la llevaba, así que no se entraba.
- **«Unirse por dirección», siempre a mano.** Cuando la app oye una partida, bajo su tarjeta sale la tecla «UNIRSE POR DIRECCIÓN». Antes, con una partida oída, no estaba, y si la tarjeta fallaba no había dónde escribir la dirección.
- **Si no se llega a quien aloja, el aviso dice por qué y qué mirar:**
  - en esa dirección no hay ninguna partida abierta;
  - no responde nadie: la dirección, el cortafuegos de quien aloja o una VPN;
  - el Mac no deja a Bastión usar la red local: dónde activarlo.

  La causa queda también en el `red.log` de quien se une, en la carpeta de datos.
- **La sala de quien aloja, otra vez en su sitio.** Desde la 0.12.24, cuando no había partida que continuar, salía pegada arriba a la izquierda y con textos montados unos sobre otros.
- **La línea de la clave en la sala y la ayuda de «Abrir el puerto del router»** ya dicen lo de ahora: desde casa, la clave solo se pide a quien entra por dirección.

## Partidas guardadas

- **La campaña de la 0.12.26 sigue en la 0.12.27.**
- **Una expedición de la 0.14 no se lee.** La 0.12.27 no la enseña, y «Nueva partida» o «Alojar partida» la apartan y empiezan otra. Antes de abrir la 0.12.27, copiad a otro sitio la carpeta de datos:
  - Mac: `~/Library/Application Support/Bastion-Unity/Bastion/`;
  - Windows: `%USERPROFILE%\AppData\LocalLow\Bastion-Unity\Bastion\`.

## Lo que sigue sin arreglar

- **Entre los dos ordenadores de casa, unirse aún falla:** quien se une ve «No responde nadie…» a los 4 s, y no se sabe por qué. Esta versión dice la causa: si os pasa, apuntad qué dice el aviso y guardad el `red.log` de los dos.
- **Unirse no se ha probado entre dos ordenadores:** las pruebas se han hecho con las dos apps en el mismo ordenador.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.27-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.27-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.27:** con la 0.12.26 no se puede jugar juntos.
- **En el Mac,** la app es `Bastion.app`: puesta en la misma carpeta que la 0.12.26, la sustituye.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **En el Mac, para unirse:** en Ajustes del Sistema → Privacidad y seguridad → Red local, activad Bastión: puede salir como «Bastion» o como «Bastión». Si salen otras, como «Bastión 0.14.13», borradlas.
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
