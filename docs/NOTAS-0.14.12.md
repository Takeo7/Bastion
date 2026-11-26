# Notas de la 0.14.12

Texto de Lore para la 0.14.12. Lo pidió Producto el 26/11 (`lore/MENSAJE-DE-PRODUCTO-0.14.12.md`). Corto. Sin commit.

**Qué lleva:**
- el repaso del menú de inicio (cuadro 171): el velo, los textos cortos y dos cosas de la escena;
- unirse (cuadro 172): «Unirse por dirección» siempre a la vista, y el aviso que dice por qué no se llega a quien aloja.

**Fuentes:**
- `producto/ENTREGA-menu-repaso-UI.md`, con las fotos de `ui/unity/runs/menu-repaso/`;
- `producto/ENTREGA-menu-escena-arte.md`, con las fotos de `arte/unity/runs/menu-171/`;
- `producto/ENTREGA-unirse-por-direccion-UI.md`;
- `red/MENSAJE-DE-PRODUCTO-unirse-aviso.md` y `producto/PARA-PRODUCTO-unirse-0.14.11-red.md`;
- `producto/RUNS.md`, cuadros 171 y 172.

**Lo de Lore:**
- `Lore/Quit.cs` dice «Salir» y «¿Salir?» (menú de la partida y de la base);
- los avisos de unirse están en `Lore.JoinText`, para que Red los llame desde `NetLink` (`producto/TEXTOS-unirse-lore.md`);
- pruebas LoreGolden y NetMenu sobre una copia de HEAD: 0 con error.

**Sin probar, para Producto:**
- **Los avisos de unirse:** cuando escribí esto, Red aún no los había puesto en `NetLink`. Las notas los dan por puestos.
- **El velo en la app:** la mitad de arriba sale más clara que en el editor (77 contra 67 detrás del menú, según Pruebas). Va en «Lo que sigue sin arreglar». Arte cree que la causa era la niebla de los copos y del farol, que ya no la usan, pero no lo ha visto en una app.
- **Nada en la app;** ni a dos jugadores ni con el dedo.

Es para Arturo y Ana: cuenta qué cambia al jugar. Va para Mac y Windows. Producto completa al publicar lo que va entre [corchetes]: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.14.12: el menú de inicio, repasado

Versión 0.14.12 (commit [commit]). El menú de inicio, con menos palabras y el velo más fuerte. Y para unirse, la dirección siempre a mano y un aviso que dice qué falla.

## Partidas guardadas

- **La partida de la 0.14.11 sigue en la 0.14.12.**

## Novedades de la 0.14.12

### El menú de inicio

- **El velo en diagonal,** detrás de los textos, es más oscuro y gris, como una lona.
- **Los textos, más cortos:**
  - el menú: «Continuar», «Nueva partida», «Multijugador», «Ajustes» y «Salir»;
  - «Multijugador»: «Alojar», «Unirse a …» con el nombre de quien aloja, y «Volver». La sala también se llama «Multijugador».
- **«Capítulo 1 · Medio»** ya no sale junto a «Continuar». Sigue en la sala.
- **La escena:** los copos de nieve son más grandes y de color crema, y al pie de la torre se ve la luz naranja de un farol.

### Unirse

- **«Unirse por dirección» sale siempre en «Multijugador».** Cuando la app oye una partida, «Unirse a …» sale además, encima. Antes, con una partida oída, no estaba, y si esa conexión fallaba no había dónde escribir la dirección.
- **Si no se llega a quien aloja, el aviso dice por qué y qué mirar:**
  - el Mac que se une no deja a Bastión usar la red local: dónde activarlo;
  - en esa dirección no hay ninguna partida abierta;
  - no responde nadie: la dirección, el cortafuegos de quien aloja o una VPN.

### En la partida y en la base

- **El menú dice «Salir»,** y la pregunta, «¿Salir?».

## Lo que sigue sin arreglar

- **El velo del menú de inicio,** en la app, sale más flojo que en el editor: la mitad de arriba se ve más clara.
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
| `Bastion-0.14.12-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.14.12-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.14.12:** con la 0.14.11 no se puede jugar juntos.
- **En el Mac,** la app se llama «Bastión 0.14.12» y no sustituye a la 0.14.11. Borrad de Aplicaciones las versiones viejas: con varias, el Mac puede dejar a la nueva sin red local. Las partidas y los ajustes son los mismos. Si teníais la vieja en el Dock, poned la nueva.
- **En Windows,** la carpeta se llama «Bastion 0.14.12».
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **En el Mac, para unirse:** en Ajustes del Sistema → Privacidad y seguridad → Red local, activad «Bastión 0.14.12».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
