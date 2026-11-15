# Notas de la 0.12.23

Texto de Lore para la 0.12.23: lo del informe «Prueba de partida» de Arturo del 14/11 (`.dev-snapshots/gameplay/notas-2026-11-14/partida/` y su `PLAN.md`). Fuentes:
- UI: `.dev-snapshots/ui/unity/0.12.23/LEEME.md` y `DUDAS.md`;
- Arte: `.dev-snapshots/arte/unity/arreglos-0.12.23/LEEME.md`;
- Gameplay: el ruido dentro de una casa (`PLAN.md`, «Hecho y decidido después»).

Lo que no va en las notas:
- **Lo de dentro:** las capas del área, `NoiseIndoorsTests`, la semilla de los bots y los laboratorios.
- **Lo que no se ha hecho:** los gatos encima de los coches, los animales dentro de los cobertizos.
- **El menú de la 0.12.24.**

Las reglas cambian en lo que hacen los zombis con el ruido y las ventanas, no en los números. Va para Mac y Windows, sin nada de iOS. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.23: ventanas, ruido y cámara

Versión 0.12.23 (commit [commit]). Lo que pidió Arturo tras su prueba de partida del 14/11. Cambia lo que hacen los zombis al oír un disparo dentro de una casa; los números de las reglas, no.

## Novedades de la 0.12.23

- **Entrar en una casa sin descubrir, también por la ventana.**
  - **Antes:** en una casa sin descubrir no se pintaba ninguna casilla de dentro, así que no había dónde pulsar para entrar.
  - **Ahora:** el área de andar marca la casilla de justo dentro de cada puerta y ventana a la que llega el soldado. Al pasar por una ventana, se rompe.
  - **El resto de la casa** sigue sin verse hasta entrar.
- **La ventana rota se distingue de la entera.**
  - **Antes:** se veían iguales.
  - **Ahora:** quedan trozos en el marco, el hueco se ve oscuro y hay cristales en el suelo. Cambia en el momento en que se rompe.
- **Un disparo dentro de una casa atrae a los zombis.**
  - **Antes:** los zombis de fuera que lo oían podían olvidarlo en el acto, y no entraban por una ventana.
  - **Ahora:** van a buscar de dónde vino, y entran por la puerta o por las ventanas. **Ojo:** un disparo dentro de una casa hace que acudan de verdad.
- **La cámara cuando atacan los zombis.**
  - **Antes:** se alejaba para encuadrar a todos los que andaban, y el ataque se perdía. Con «TU TURNO», a veces no volvía.
  - **Ahora:** se acerca a los que van a atacar y a sus blancos. Con «TU TURNO» vuelve al soldado que juega, salvo si estás moviendo la cámara en ese momento.
- **Cuando el juego pasa solo al siguiente soldado,** sale su nombre encima durante 2 s, con un sonido. Antes cambiaba sin avisar.
- **Q y E al apuntar** giran la vista 45° alrededor del tirador, hasta 90° a cada lado, sin salir de la vista por encima del hombro. Antes volvían a la cámara de siempre.
- **El área de andar:**
  - **los huecos de dentro** (alrededor de coches o bolardos) solo se ven con el cursor encima de uno: entonces salen todos;
  - **el recorrido y el círculo de destino** llevan el mismo estilo que el contorno: una línea fina con su brillo.
- **Los animales:**
  - **son menos,** la mitad o menos: en una ciudad, unos 7 en vez de unos 16;
  - **comen, descansan y huyen** de los soldados y de los zombis que se ven;
  - **los zombis no los ven** (lo eligió Arturo).
- **Las ventanas enteras de algunas casas,** que se veían opacas, llevan ahora el mismo cristal que las demás.

## Partidas guardadas

- **La campaña de la 0.12.22 sigue** en la 0.12.23, y también una misión guardada a medias. Sus zombis ya reaccionan al ruido como se cuenta arriba.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.23-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.23-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.23:** con la 0.12.22 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.22.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
