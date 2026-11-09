# Notas de la 0.12.11

Texto de Lore para la 0.12.11: la revisión de armas y puntería que pidió Arturo el 9/11 («revisa como cogen las armas… si apuntan bien»), hecha por Arte (`.dev-snapshots/arte/unity/revision-animaciones/LEEME.md`, secciones 1, 1b, 1c y 2). Solo cambia cómo se ven las armas y las figuras: las reglas, los textos del juego y las partidas guardadas no cambian. Va para Mac y Windows, sin nada de iOS. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.11: las armas, bien agarradas y apuntando al blanco

Versión 0.12.11 (commit [commit]). Arturo pidió revisar cómo cogen las armas y si apuntan bien. Solo cambia lo que se ve: acertar y fallar van igual que antes, y las reglas, los textos y las partidas guardadas no cambian.

## Novedades de la 0.12.11

Todo vale para los soldados y para los Cuervos.

- **La mano izquierda agarra el guardamanos.**
  - **Antes:** se quedaba suelta, a 10–26 cm de él. Con el fusil automático del Cabecilla, sujetaba el aire delante de la boca del cañón.
  - **Ahora:** agarra el guardamanos de pie, agachado, apuntando, disparando, en vigilancia y escondido.
- **El arma apunta al blanco.**
  - **Antes:** el cañón apuntaba por encima del blanco y a un lado; a 4 casillas, su línea pasaba a 1,3 m. Y no subía ni bajaba hacia otra planta: con el blanco una planta más abajo, se desviaba 47°.
  - **Ahora:** apunta al blanco, también si está más alto o más bajo (en otra planta o en un tejado). El fusil gira alrededor de la culata, sin bajarse del hombro. Si el blanco está mucho más abajo y cerca, el cuerpo también se inclina hacia delante.
- **La cabeza sigue al blanco.**
  - **Antes:** la cara miraba a un lado y por debajo del blanco (con fusil, 19° a un lado y 24° por debajo), y no subía ni bajaba con él.
  - **Ahora:** con pistola, la cara mira al blanco. Con fusil, la mejilla se queda en la culata, como al apuntar de verdad, y la cabeza gira con el arma, también hacia arriba y hacia abajo.
- **La recarga se ve entera** (lo eligió Arturo).
  - **Antes:** un tirón de 0,6 s, con el arma por encima del hombro.
  - **Ahora:** dura 1,3 s, de pie y agachado. El fusil se inclina junto a la cara y se ve sacar y meter el cargador con la mano izquierda. La escopeta no tiene cargador: solo se ve el gesto.
  - **Al cambiar a un arma de la mochila,** el gesto es el mismo, en 0,7 s y sin el tirón.
- **La pistola.**
  - **Antes:** disparaba sin moverse, y para recargar el soldado esperaba quieto.
  - **Ahora:** al disparar, sube con el retroceso y vuelve al blanco. Al recargar, el soldado la sube junto al hombro, mete el cargador con la otra mano y monta la corredera, en 1,3 s.

## Partidas guardadas

- **La campaña de la 0.12.10 sigue** en la 0.12.11, y también una misión guardada a medias.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.11-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.11-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.11:** con la 0.12.10 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.10.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
