# Notas de la 0.14.9

Texto de Lore para la 0.14.9. Lo pidió Producto el 25/11 (`lore/MENSAJE-DE-PRODUCTO-0.14.9.md`). Corto. Sin commit.

**Qué lleva:** seis mejoras nuevas de la base (la tanda 1) y la pantalla de Mejoras en tres columnas (cuadros 164 a 166).

**Fuentes:**
- `producto/mejoras-base-2026-11-25/`: `ELEGIDAS.md`, `CONTRATO.md`, `PLAN.md` y `TEXTOS.md`;
- `producto/ENTREGA-0.14.9-gameplay.md` y `ENTREGA-0.14.9-UI.md`;
- `producto/RUNS.md`, cuadros 164 a 166.

**Lo que no va:** el bot (no compra las seis nuevas), los laboratorios y lo que solo cambia en el iPad y el iPhone.

**Sin probar, para Producto:**
- **El equilibrio,** cuánto se tarda en comprarlas, es una estimación con la sonda del Trueque de la 0.14.3.
- **Nada en la app;** ni a dos jugadores ni con el dedo.
- **El Huerto con el almacén lleno,** y su línea en el parte al superar el capítulo: solo se ha visto con la escuadra caída.
- **El candado del botiquín después de un combate de la expedición.** Gameplay arregló que se perdía, pero no se ha visto en pantalla.
- **Partidas guardadas:** según Gameplay valen, porque las mejoras son claves nuevas y lo nuevo va vacío. Lo cubren las pruebas, no un guardado real.

Es para Arturo y Ana: cuenta qué cambia al jugar. Va para Mac y Windows. Producto completa al publicar lo que va entre [corchetes]: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.14.9: seis mejoras nuevas para la base

Versión 0.14.9 (commit [commit]). Las primeras seis de las diez mejoras nuevas que eligió Arturo, y la pantalla de Mejoras en tres columnas.

## Partidas guardadas

- **La partida de la 0.14.8 sigue en la 0.14.9,** con las mejoras nuevas sin comprar.

## Novedades de la 0.14.9

### La pantalla de Mejoras, en tres columnas

- **Soldados, Expedición y Base,** cada mejora en la suya. Caben todas sin desplazar.
- **Cada fila,** como en la 0.14.8: lo que da el siguiente nivel, sus rayitas y el precio en el botón. La explicación entera, al pasar el puntero.

### Las seis mejoras nuevas

| Mejora | Columna | Precio | Qué da |
|---|---|---|---|
| **Entrenamiento** | Soldados | 60 / 120 / 200 § | Quien se une al Bastión, un superviviente o un recluta, llega con 1, 2 o 3 puntos de habilidad por gastar. |
| **Botiquín de salida** | Soldados | 100 / 150 § | Cada soldado sale de la base con un botiquín en el cinturón; en el nivel 2, también con un cóctel molotov. Solo si no lleva ya uno y hay sitio. |
| **Ruta de vuelta** | Expedición | 100 § | Desde un Trueque también se puede volver a la base, no solo desde un Refugio. |
| **Motor silencioso** | Expedición | 80 / 160 § | La horda no llega hasta el turno 3; en el nivel 2, hasta el 5. Hasta entonces no entran muertos, ni los de cada turno ni los que trae el ruido. |
| **Radio** | Expedición | 120 § | En el mapa se ven los avisos de todas las etapas del capítulo, también de las que aún son «?», y una etapa más del camino. |
| **Huerto** | Base | 150 / 250 § | Al volver de cada expedición, aunque haya ido mal, el almacén recibe 25 §; en el nivel 2, 50 §. |

- **Lo que da el Botiquín de salida no se puede cambiar.**
  - Lleva un candado en la esquina de su casilla.
  - En el Trueque sale sin precio: «Eso no se puede cambiar.».
- **Con el Motor silencioso,** mientras no llega la horda, el medidor de ruido dice desde qué turno puede llegar.
- **El Huerto dice lo que deja:** «El huerto deja 25 § en el almacén.».
  - Al volver de un Refugio o de un Trueque, en una nota al llegar a la base.
  - Si la expedición acaba en un combate, en el parte.
  - Si el almacén está lleno, no deja nada, y lo dice.
- **Vienen cuatro más** en las versiones siguientes: Taller, Chatarrero, Baúl de La Lata y Camilla.

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
| `Bastion-0.14.9-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.14.9-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.14.9:** con la 0.14.8 no se puede jugar juntos.
- **En el Mac,** la app se llama «Bastión 0.14.9» y no sustituye a la 0.14.8: se quedan las dos. Borrad la vieja cuando queráis; las partidas y los ajustes son los mismos. Si teníais la vieja en el Dock, poned la nueva.
- **En Windows,** la carpeta se llama «Bastion 0.14.9».
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
