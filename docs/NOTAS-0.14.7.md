# Notas de la 0.14.7

Texto de Lore para la 0.14.7. Lo pidió Producto el 25/11 (`lore/MENSAJE-DE-PRODUCTO-0.14.7.md`). Corto. Sin commit.

**Qué lleva:**
- los enemigos vivos (P1 a P4);
- el porcentaje de acierto con la barra y las fichas (P5, U1; cuadros 157 y 158);
- lo que se ve por encima cubre a medias (P6);
- la rejilla sobre la nieve.

**Fuentes:**
- `producto/analisis-2026-11-25/PLAN.md` y `PETICION.md`;
- `producto/RUNS.md`, cuadros 157 y 158;
- `producto/ENTREGA-0.14.7-gameplay.md` y `ENTREGA-0.14.7-UI.md`;
- las fotos de `ui/unity/runs/u1-apuntar/cuadro158/`.

**Lo que no va:** los bancos de bots, y lo que solo cambia en el iPad y el iPhone.

**Sin probar, para Producto:**
- **La dificultad de las etapas con vivos cambia,** y solo está medida con el bot (la sonda de Gameplay):
  - duran más, porque los muertos los matan menos (en el Rescate con Cuervos, 17 → 7);
  - os pierden de vista con más facilidad.
- **Nada en la app;** ni a dos jugadores ni con el dedo.
- **Las fichas del porcentaje con «Interfaz» al 130 %:** pueden partirse en tres filas.
- **Un guardado real de la 0.14.6.** El motor guarda el campo viejo de los vivos (`Pod.WokenBlind`) por las partidas guardadas.

Es para Arturo y Ana: cuenta qué cambia al jugar. Va para Mac y Windows. Producto completa al publicar lo que va entre [corchetes]: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.14.7: los vivos y el porcentaje de acierto

Versión 0.14.7 (commit [commit]). Lo que pidió Arturo tras analizar a los enemigos vivos y el porcentaje de acierto.

## Lo que se arregla

- **La rejilla del suelo** se ve también sobre la nieve.

## Partidas guardadas

- **La partida de la 0.14.6 sigue en la 0.14.7.**

## Novedades de la 0.14.7

### Los enemigos vivos

- **Ya no se arriman a los muertos.** Sin tiro, van hacia la escuadra o hacia donde la vieron. Solo se ponen al lado de un muerto si no tienen otro sitio.
- **Notan a los muertos a 3 casillas o menos,** aunque no los tengan delante, también dormidos.
- **Con un muerto al lado,** primero se apartan y luego disparan.
- **Cada uno sabe de la escuadra solo lo que ve él.** Antes sabían dónde estaba toda la escuadra.
  - Si os pierde de vista, va a por el último soldado que vio, durante 3 turnos.
  - Después, va adonde oyó algo, y luego se queda vigilando.
  - No se lo cuenta a los suyos. Despertar al grupo, como antes.
- **El que despierta sin haber visto a nadie** va adonde oyó algo, o se queda vigilando.
- **En su turno miran a su alrededor,** no solo hacia delante. Para que os vean en vuestro turno, sigue contando hacia dónde miran.

### El porcentaje de acierto, al apuntar

- **Una barra de 0 a 100:**
  - en gris, la puntería del soldado;
  - en verde, lo que suma;
  - en rojo, lo que resta;
  - una raya, donde queda.
- **Debajo, cada sumando en una ficha** con su icono y su cifra. Al pasar el puntero, su nombre.
- **La cobertura dice qué la da:** «−40 estantería». Esa casilla sale marcada en rojo en el mapa.
- **El alcance dice a cuántas casillas está el blanco:** «+19 2 c.».
  - Se cuenta desde la casilla de la que sale el tiro.
  - Al asomarse, un anillo claro marca esa casilla, con la línea hasta el blanco.
- **Sobre cada enemigo,** junto a su %, el icono de lo que más le resta.
- **Con el crítico a 0 %:** «Sin crítico: está a cubierto».
- **Con la defensa en negativo** (el Hinchado), suma: «Defensa baja del blanco +15».

### Lo que se ve por encima cubre a medias

- **Cubren a medias:**
  - las cajas, los palés y los bidones;
  - las máquinas y el depósito de gas;
  - la nevera de bebidas, el estante de taller y el dispensador de agua.
- **Dan cobertura baja (−20),** antes alta (−40), porque quedan por debajo de los ojos. Lo que se ve, como antes.
- **La cobertura pegada al blanco** cuenta igual, esté el tirador a una casilla o a diez.

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
| `Bastion-0.14.7-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.14.7-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.14.7:** con la 0.14.6 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.14.6.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
