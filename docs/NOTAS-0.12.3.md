# Notas de la 0.12.3

Texto de Lore para la 0.12.3. Recoge el informe de interfaz de Arturo del 7/11, «Cambios de interfaz · Informe para desarrollo» ([PRUEBA-2026-11-07-ARTURO.md](PRUEBA-2026-11-07-ARTURO.md), «Para la 0.12.3»), que va en dos commits:
- la pantalla de preparación de la misión, según la maqueta v39 (db5679b);
- las tarjetas de todo el juego.

No cambia nada de las reglas ni de las partidas guardadas. Va para Mac y Windows, sin nada de iOS. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.3: la preparación de la misión nueva, y tarjetas que dejan ver el mapa

Versión 0.12.3 (commit [commit]). Los cambios de interfaz que Arturo pidió el 7/11. Solo cambia cómo se ve: las reglas son las de la 0.12.2.

## Novedades de la 0.12.3

### La pantalla de preparación de la misión
- **La barra de arriba** es más alta y más oscura, sin textura ni el ribete naranja. Es la misma en toda la base, y «Menú» va en una placa rasgada.
- **La infestación (o la Marea) sale de la barra y va al Mapa,** arriba de la columna de la izquierda, con su valor y su ayuda.
- **«Tus soldados»** va en una placa rasgada negra, en vez del marco de acero. Los elegidos llevan un marco blanco, en vez del ✓ verde.
- **Los carteles bajo los personajes** son más grandes y están todos a la misma altura, con el nombre más grande y el rango debajo. Ya no hay aros morados en el suelo.
- **El panel de la misión** va en la misma placa rasgada, con el objetivo en amarillo, y «LANZAR MISIÓN», en un rectángulo con muescas.

### Las tarjetas de todo el juego
- **Placas rasgadas negras y algo transparentes, en vez del acero:** en el combate, la base, el inicio, la sala, los menús y las ventanas. Dejan ver más el mapa y la nieve.
- **En el parte,** los carteles de los soldados son como los de la escuadra.

## Partidas guardadas

- **La campaña de la 0.12.2 sigue** en la 0.12.3, y también una misión guardada a medias.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.3-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.3-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.3:** con la 0.12.2 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.2.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
