# Notas de la 0.14.8

Texto de Lore para la 0.14.8. Lo pidió Producto el 25/11 (`lore/MENSAJE-DE-PRODUCTO-0.14.8.md`). Corto. Sin commit.

**Qué lleva:**
- menos texto (U2);
- la dificultad y las Mejoras con cifras (U3);
- elegir lo que va en un hueco pinchándolo (U4);
- cómo se cogen la pistola y la katana;
- la versión en el nombre de la aplicación.

Son los cuadros 159 a 163.

**Fuentes:**
- `producto/analisis-2026-11-25/PLAN.md` y `producto/RUNS.md`, cuadros 159 a 163;
- `producto/ENTREGA-0.14.8-UI.md`, con sus fotos en `ui/unity/runs/0148/`;
- `producto/ENTREGA-agarres-arte.md`;
- `producto/PARA-PRODUCTO-nombre-con-version-red.md`.

**Lo que no va:** lo de dentro (laboratorios, `principal`) y lo que solo cambia en el iPad y el iPhone.

**Sin probar, para Producto:**
- **los agarres,** solo en el laboratorio; la recarga de la pistola con la mano izquierda nueva;
- **el nombre en el Dock y en la barra de menús,** sin verlo en pantalla (lo dice `CFBundleName`); el zip de Windows, sin abrirlo en Windows;
- **nada más en la app.**

**Las palabras de las fotos:** en `producto/MENSAJE-DE-LORE-0148-textos.md`. Hay dos que no son ciertas o que chocan.

Es para Arturo y Ana: cuenta qué cambia al jugar. Va para Mac y Windows. Producto completa al publicar lo que va entre [corchetes]: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.14.8: menos texto y el equipo pinchando el hueco

Versión 0.14.8 (commit [commit]). Menos frases en pantalla, la dificultad y las Mejoras con cifras, el equipo eligiéndolo en su hueco, y la versión en el nombre de la aplicación.

## Lo que se arregla

- **La pistola** se coge con la culata en el puño. Antes, al apuntar y disparar, la culata quedaba por detrás de la mano.
- **La katana y la espada larga:**
  - en la mano, el puño va pegado a la guarda;
  - a la espalda, con el mango arriba, sobre el hombro. Antes, el mango iba junto a la cadera.

## Partidas guardadas

- **La partida de la 0.14.7 sigue en la 0.14.8.**

## Novedades de la 0.14.8

### Menos texto

- **Fuera las frases que repetían el título o el botón:** en el Trueque, el Refugio, el mapa de etapas y la tarjeta del final de una etapa.
- **En el Trueque:**
  - lo que te falta para algo sale como su precio en rojo;
  - lo que lleva puesto, con la marca «PUESTO»;
  - adónde va lo que cambias, como «→ Anja» junto a «Lo que te ofrece»;
  - las explicaciones, al pasar el puntero.
- **La tarjeta del final de una etapa** ya no lista el botín: lo enseña el parte, justo después.
- **Cómo se mueven las cosas** en el Almacén y en la mochila del combate: «ⓘ Cómo mover», con la explicación al pasar el puntero.

### La dificultad, con cifras

- **Cada nivel con tres cifras,** frente a Difícil (100 %):
  - Muertos: 60, 80, 100 y 125 %;
  - Horda: 50, 75, 100 y 130 %;
  - Suministros: 150, 125, 100 y 75 %.
- **En verde lo que ayuda,** en rojo lo que no.
- **Sobre las tarjetas,** un candado: «se elige una vez».

### Las Mejoras, con cifras

- **Cada mejora dice qué da el siguiente nivel:** «2 → 4 plazas», «+2 filas en el almacén», «+1 etapa a la vista»…
- **Sus rayitas de nivel,** con «0 de 3».
- **El precio, en el botón:** «MEJORAR 40 §».

### El equipo, pinchando el hueco

- **En el Almacén y en el equipo del mapa de etapas,** pinchar un hueco del soldado (vacío o lleno) abre una lista con lo que puede ir ahí:
  - en la base, del Almacén y de su mochila;
  - en una expedición, de su mochila.
- **Cada cosa dice lo que cambia** frente a lo puesto: daño y puntería de ese soldado en las armas, salud y blindaje en los chalecos.
- **Un clic lo pone. «Quitar»** lo deja en el Almacén, o en su mochila en una expedición.
- **Si el hueco tiene algo,** también sus órdenes: «Quitar la mira», «Quitar la culata»…
- **La lista se abre a la izquierda,** sin tapar la mochila. Esc o un clic fuera la cierran.
- **Arrastrar sigue valiendo.**
- **La hoja de Soldados,** igual: sus huecos se pinchan, con la misma lista.

## Lo que sigue sin arreglar

- **Al abrir «Unirse por dirección» con el teclado,** hay que pinchar el campo para escribir.
- **«Alojar partida» con una partida guardada** enseña la hoja «Empezando una partida nueva», aunque abre la guardada.
- **Un soldado o un cuerpo** puede quedar tapado por un edificio sin descubrir.
- **Los marcos de una casa sin descubrir** se ven claros, como si diera la luz de la calle.
- **Una farola** puede quedar en el bordillo, delante de las puertas de La Lata.
- **El aviso del compañero caído** («Pierre Lambert ha caído · Ver (F)») tapa «Turno enemigo».
- **Los guardias sentados de la hoguera,** con la cámara normal, parecen de pie.
- **Quedan saltos de animación:**
  - un soldado se levanta de golpe;
  - algunos muertos suben el brazo o bajan el cuerpo de golpe.

  Si veis alguno, apuntad dónde y cuándo.
- **La dificultad** se ajusta jugando: apuntad lo que notéis en cada nivel.
- **La sincronía vertical con la pantalla dormida,** o apagada al momento con una esquina activa, está sin probar.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.14.8-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.14.8-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.14.8:** con la 0.14.7 no se puede jugar juntos.
- **La versión va en el nombre:**
  - en el Mac, la app se llama «Bastión 0.14.8», también en el Dock y en la barra de menús;
  - en Windows, la carpeta se llama «Bastion 0.14.8».
- **En el Mac, al copiarla a Aplicaciones ya no sustituye a la 0.14.7:** se quedan las dos. Borrad la vieja cuando queráis.
  - Las partidas y los ajustes son los mismos para las dos.
  - Si teníais la vieja en el Dock, poned la nueva.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
