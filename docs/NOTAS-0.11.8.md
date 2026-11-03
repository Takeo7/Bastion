# Notas de la 0.11.8

Texto de Lore para la 0.11.8, con los dos arreglos de la prueba de la 0.11.7 con la app (`.dev-snapshots/ui/unity/app-0117/`): la resolución en Ajustes (UI, `GameSettings.ShownSizes`) y seguir en red una misión guardada (Red, `Room.AwaitAbsent` y `Rules.SyncTurn`). La pantalla completa de la 0.11.7 no sale: no se pudo probar con el Mac bloqueado. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.11.8: la resolución en Ajustes y seguir una misión en red

Versión 0.11.8 (commit [commit]). Dos arreglos de la prueba de la 0.11.7.

## Novedades de la 0.11.8

### La resolución en Ajustes
- **«Resolución» marca la que tienes.** Si el tamaño de la ventana no estaba en la lista, marcaba 1920 × 1080 aunque fuera otro.
- **Ahora ese tamaño sale como una opción más,** marcado y con la ayuda «La de ahora». Al elegir otro, desaparece de la lista.

### Seguir una misión guardada en red
- **Al seguir una misión guardada a medias,** con «Continuar campaña» o con la partida guardada, la sala espera 5 minutos a que el otro jugador abra su juego y entre. Mientras, sus soldados siguen siendo suyos.
- **Antes, si quien aloja no tenía soldados en el campo, la escuadra acababa el turno nada más entrar.** Los zombis jugaban sin el otro jugador, y quien alojaba se quedaba con el mando y tenía que cederlo a mano.
- **Si no entra en esos 5 minutos,** la partida sigue como cuando alguien se desconecta.
- **El mando pasa solo:** si quien lo tiene no tiene nada que mover y vuelve el otro jugador con soldados, le pasa a él.

## Partidas guardadas

- **La campaña de la 0.11.7 sigue** en la 0.11.8, y también una misión guardada a medias.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.11.8-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.11.8-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.11.8:** con la 0.11.7 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.11.7.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
