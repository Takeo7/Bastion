# Notas de la 0.12.2

Texto de Lore para la 0.12.2, con lo que Arturo encontró el 7/11 jugando solo con la 0.12.1 en el Mac ([PRUEBA-2026-11-07-ARTURO.md](PRUEBA-2026-11-07-ARTURO.md)) y lo que decidió en el cuadro:
- nadie se mueve en diagonal;
- al seguir una misión sin el compañero, no se le espera;
- el arreglo del clic en la casilla de detrás de un soldado.

Va para Mac y Windows, sin nada de iOS. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.2: nadie se mueve en diagonal, y jugar solo ya no se bloquea

Versión 0.12.2 (commit [commit]). Lo que Arturo encontró el 7/11 jugando solo con la 0.12.1: un cambio en las reglas y dos arreglos.

## Novedades de la 0.12.2

### Nadie se mueve en diagonal
- **Ni los soldados, ni los zombis, ni los vivos.** Para llegar a la casilla de la esquina hacen falta dos pasos.
- **Cada punto de movimiento es una casilla:** 8 puntos, 8 casillas. En la 0.12.1, una casilla en diagonal costaba 1,5.
- **Golpear y morder sí llegan a la casilla de la esquina:** un zombi en la esquina de un soldado le muerde sin moverse. Las armas cuerpo a cuerpo que llegan a dos casillas, en línea o en diagonal, tampoco cambian.

### Seguir una misión sin el otro jugador
- **Ya no se le espera:** al seguir una misión guardada sin el otro jugador, sus soldados son tuyos desde el principio. En la 0.12.1 se le esperaba 5 minutos, sin avisar, y sus soldados no se podían mover.
- **El turno espera a sus soldados** hasta que los muevas o pulses «Terminar turno». En la 0.12.1, pasados esos 5 minutos, el turno acababa con ellos sin mover.
- **Si entra después,** cada uno vuelve a mandar en lo suyo.
- **Si alguien se desconecta a media misión, se le espera como antes:** 30 segundos, o 5 minutos si tenía el juego minimizado o el portátil dormido. Después, sus soldados pasan a quien está, y el turno también los espera.

### El clic detrás de un soldado
- **Con la cámara lejos, un clic en la casilla de detrás de un soldado lo volvía a elegir,** en vez de mover allí. Ahora solo lo elige un clic sobre su figura, a cualquier distancia.

## Partidas guardadas

- **La campaña de la 0.12.1 sigue** en la 0.12.2, y también una misión guardada a medias.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.2-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.2-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.2:** con la 0.12.1 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.1.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
