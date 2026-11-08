# Notas de la 0.12.10

Texto de Lore para la 0.12.10: un arreglo de UI, la barra de acciones que se recolocaba sin parar en combate (al menos desde la 0.12.2, y más con dos jugadores desde la 0.12.8). No cambia nada de reglas, de textos del juego ni de partidas guardadas. Va para Mac y Windows, sin nada de iOS. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.10: la barra de acciones deja de recolocarse

Versión 0.12.10 (commit [commit]). Un arreglo de la interfaz del combate. Las reglas, los textos y las partidas guardadas no cambian.

## Novedades de la 0.12.10

- **La barra de acciones deja de recolocarse.**
  - **Antes:** cuando la placa de la escuadra llegaba hasta la barra, la empujaba a la derecha. Pasaba con la ventana pequeña, y más con dos jugadores, porque desde la 0.12.8 la placa lleva también las tarjetas del compañero. Según dónde caía, la barra medía un píxel más o menos y se volvía a colocar, decenas de veces en cada fotograma. No se veía, y en el Mac de las pruebas no cambiaba los fps.
  - **Ahora:** la barra se coloca una vez y se queda, en el mismo sitio de siempre.

## Partidas guardadas

- **La campaña de la 0.12.9 sigue** en la 0.12.10, y también una misión guardada a medias.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.10-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.10-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.10:** con la 0.12.9 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.9.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
