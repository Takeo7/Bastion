# Notas de la 0.12.4

Texto de Lore para la 0.12.4, con las notas de Arturo del 7/11 tras la 0.12.2 ([PRUEBA-2026-11-07-ARTURO.md](PRUEBA-2026-11-07-ARTURO.md), «Para la 0.12.4»):
- el turno de cada soldado y el mando;
- la mochila, que junta lo que es igual;
- el escudo de cobertura en la ficha;
- los animales.

Va para Mac y Windows, sin nada de iOS. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.4: cada soldado acaba su turno cuando no le queda nada, y la mochila junta lo que es igual

Versión 0.12.4 (commit [commit]). Las notas de Arturo del 7/11, tras la 0.12.2.

## Novedades de la 0.12.4

### El turno de cada soldado
- **Un soldado sigue en juego mientras le queden acciones o movimiento.** Con el compañero, el mando pasa al otro cuando ya no le queda nada. En la 0.12.3, quien se quedaba sin acciones dejaba de estar en juego aunque le quedara movimiento, y el mando pasaba al otro.
- **«Terminar el turno de Yuki»:** cuando ya no le quedan acciones pero sí movimiento, la barra tiene ese botón. No gasta ninguna acción. Pierde el movimiento que le quede y, con el compañero, el mando pasa al otro.
- **Al elegir a otro soldado con uno aún en juego, el juego pregunta antes:** «Yuki aún puede moverse: le quedan 3 puntos de movimiento. Si terminas su turno, pierde lo que le queda y el mando pasa a Ana.» Si le quedan acciones, también lo dice, para no perderlas sin querer. Solo pasa con el compañero: jugando solo, se cambia de soldado sin perder nada.
- **Terminar el turno sin tener el mando:** quedas «A punto», y el turno acaba cuando termine el otro: «A punto. Ana sigue con el mando: el turno acaba cuando termine.»
- **La tira de arriba dice «MANDO»** junto al nombre de quien lo tiene.

### La mochila junta lo que es igual
- **Lo que es igual va en el mismo hueco, sin límite:** piezas, suministros, medicinas, comida, y los accesorios y las mejoras iguales. El hueco lleva su número: «×3».
- **Cabe aunque la mochila esté llena,** si ya lleva lo mismo.
- **Las armas no se juntan:** cada una va en su hueco, con su estado y sus mejoras.
- **Se cuenta lo que se lleva en la mano:** dos hallazgos de «dos granadas» son «4 granadas».
- **Las mejoras sueltas ya tienen nombre:** una mira encontrada salía como «Está vacía», «Nada» o «recoge nada». Ahora es «una mira», en el registro, en la mochila y en el parte.
- **El parte junta lo repetido:** «2 botiquines», no «un botiquín y un botiquín».

### La ficha del soldado
- **El escudo de cobertura pasa de la barra de vida a la ficha,** junto al nombre:
  - azul, cobertura completa;
  - amarillo, media;
  - rojo, le falta cobertura ante algún enemigo a la vista.

  Con el puntero encima sale su ayuda.

### Arreglos
- **Encontrar un cóctel molotov** en una tienda o un maletero daba un error en el registro. Ahora sale «dos cócteles molotov».
- **Los animales ya no dan tirones** al andar de casilla en casilla.

## Partidas guardadas

- **La campaña de la 0.12.3 sigue** en la 0.12.4, y también una misión guardada a medias. Lo que ya ocupaba dos huecos en una mochila se queda así; lo que se recoja a partir de ahora, se junta.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.4-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.4-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.4:** con la 0.12.3 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.3.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
