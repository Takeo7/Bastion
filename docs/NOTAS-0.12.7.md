# Notas de la 0.12.7

Texto de Lore para la 0.12.7: rendimiento en el combate, con los tirones de la prueba de 90 s en una ciudad. Sale antes de la 0.12.8, que lleva las notas de Ana del 7/11 ([PRUEBA-2026-11-07-ANA.md](PRUEBA-2026-11-07-ANA.md)). No cambia nada de reglas, de textos del juego ni de partidas guardadas. Va para Mac y Windows, sin nada de iOS. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.7: el combate, con menos tirones

Versión 0.12.7 (commit [commit]). Solo rendimiento: el combate se para menos. Las reglas, los textos y las partidas guardadas no cambian.

## Novedades de la 0.12.7

- **Menos tirones en el combate.** En 90 segundos de combate en una ciudad había 86 parones de una vigésima de segundo (50 ms) o más. Ahora hay entre 2 y 9, en tres pruebas.
- **El turno de los zombis va más seguido.** La pantalla se pone al día una vez por cada cosa que pasa, no una vez por cada zombi o soldado.
- **La niebla y el mapa se ponen al día con menos parón.**
  - Al abrir una puerta o volar una cobertura, solo se vuelve a montar lo que ha cambiado.
  - La niebla usa lo que los soldados ya ven, sin calcularlo otra vez.
- **La vista de cada soldado va aparte.** Cuando uno se mueve, solo se calcula de nuevo la suya, no la de toda la escuadra.

## Partidas guardadas

- **La campaña de la 0.12.6 sigue** en la 0.12.7, y también una misión guardada a medias.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.7-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.7-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.7:** con la 0.12.6 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.6.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
