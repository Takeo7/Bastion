# Notas de la 0.12.13

Texto de Lore para la 0.12.13: la tercera tanda de la revisión de armas que pidió Arturo el 9/11, el fusil al hombro (Arte: `.dev-snapshots/arte/unity/revision-animaciones/LEEME.md`, sección 9), y el sonido propio de la recarga del fusil (Red: [UNITY-SONIDO.md](UNITY-SONIDO.md), «La recarga del fusil»). La espera de 1,3 s de UI (`.dev-snapshots/ui/unity/recarga-01213/LEEME.md`) solo vale sin figuras, así que no se nota al jugar y no va en las notas. Las reglas, los textos del juego y las partidas guardadas no cambian. Va para Mac y Windows, sin nada de iOS. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.13: el fusil, al hombro

Versión 0.12.13 (commit [commit]). La tercera parte de la revisión de armas y animaciones, y un sonido nuevo para la recarga del fusil. Acertar y fallar van igual que antes, y las reglas, los textos y las partidas guardadas no cambian.

## Novedades de la 0.12.13

- **El fusil, al hombro** (lo eligió Arturo). Vale para todas las armas largas, de pie, agachado y en vigilancia. La pistola no cambia.
  - **Antes:** el arma quedaba a la altura del pecho, con la culata a 23–39 cm del hombro, y la cabeza iba por encima de ella, no apoyada.
  - **Ahora:** la culata va en el hueco del hombro, la mejilla sobre la culata y el ojo a la altura de las miras. Al disparar, la culata se queda en el hombro y el golpe se ve en la boca del cañón.
  - **Dónde se nota:** sobre todo en el plano al hombro del disparo que mata. Desde la cámara del juego se nota menos: el arma se ve más arriba, junto a la cara.
- **Apuntar hacia abajo.**
  - **Antes:** con el blanco mucho más abajo, por ejemplo una planta más abajo y cerca, el soldado se doblaba por la cintura con las piernas rectas.
  - **Ahora:** de pie, dobla también las rodillas y baja la cadera, y el cuerpo se inclina menos.
- **La recarga del fusil tiene su propio sonido.**
  - **Antes:** sonaba la recarga de siempre, que va con los gestos de la pistola y no con los del fusil.
  - **Ahora:** suena al ritmo de la recarga de 1,3 s: el cargador sale, entra y la mano vuelve al arma, cuando se ve. Vale para todas las armas largas.
  - **La pistola y el cambio de arma** suenan como antes.

## Partidas guardadas

- **La campaña de la 0.12.12 sigue** en la 0.12.13, y también una misión guardada a medias.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.13-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.13-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.13:** con la 0.12.12 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.12.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
