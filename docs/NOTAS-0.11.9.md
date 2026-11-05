# Notas de la 0.11.9

Texto de Lore para la 0.11.9, sin cambios de reglas: el mando al seguir en red una misión guardada (Red, `Rules.SyncTurn` con `back` y `Room.ResyncTurn`) y la pasada de rendimiento de Arte, medida en el Mac, con «Bajos» de verdad (Arturo lo quiso en esta versión; la nieve con sombras en «Bajos» se arregló antes de salir y no se cuenta). Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.11.9: «Bajos» de verdad, más fluido y el mando al seguir una misión en red

Versión 0.11.9 (commit [commit]). Un arreglo de red y una pasada de rendimiento, con «Bajos» por fin funcionando. Las reglas no cambian.

## Novedades de la 0.11.9

### Seguir una misión guardada en red
- **El mando se queda con quien aloja hasta que entra el otro jugador.** Al abrir la partida guardada en mitad de una misión, si quien aloja no tenía nada que mover, a los 4 s el mando pasaba al otro jugador aunque aún no hubiera entrado.
- **Con «Continuar campaña» ya esperaba.** Ahora las dos formas hacen lo mismo.

### Va más fluido
- **«Bajos» por fin funciona.** Desde la 0.11.0, en las apps de Mac y Windows, elegir «Bajos» dejaba los gráficos en «Medios». Ahora es un nivel propio: la imagen va al 80 % de la resolución, sin el posproceso más pesado y con sombras más simples.
- **«Bajos» cuesta entre 1,3 y 1,6 ms menos por fotograma que «Medios»** en el Mac. En la ciudad, de 7,32 a 5,81 ms.
- **«Ajuste auto» baja ahora a este «Bajos»** cuando el combate va lento en «Medios». Antes solo quitaba cosas del suelo.
- **Con los gráficos en «Altos», cada fotograma cuesta entre 1 y 1,35 ms menos** en el Mac. La oclusión ambiental va a media resolución y las sombras suaves, en calidad media.
- **El color y la niebla cuestan unos 0,1 ms menos.** Se ven igual.
- **En las hordas, los zombis lejanos se animan una imagen de cada dos.** Pasa con 24 figuras o más a la vista, a los zombis a más de 10 casillas de donde mira la cámara, y solo cuando el juego va a 60 imágenes por segundo o más.
- **Los que muerden o caen, y los vivos, se animan siempre en todas las imágenes.**
- **Con 60 zombis lejos y los gráficos en «Medios»,** cada fotograma pasa de 6,72 a 5,85 ms.

## Partidas guardadas

- **La campaña de la 0.11.8 sigue** en la 0.11.9, y también una misión guardada a medias.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.11.9-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.11.9-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.11.9:** con la 0.11.8 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.11.8.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
