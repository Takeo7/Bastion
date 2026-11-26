# Notas de la 0.14.11

Texto de Lore para la 0.14.11. Lo pidió Producto el 26/11 (`lore/MENSAJE-DE-PRODUCTO-0.14.11.md`). Corto. Sin commit.

**Qué lleva:** la Camilla, la última de las diez mejoras nuevas de la base (cuadros 165, 169 y 170).

**Fuentes:**
- `producto/ENTREGA-0.14.11-gameplay.md`, `ENTREGA-0.14.11-UI.md` y `ENTREGA-0.14.11-arte.md`;
- las fotos de `ui/unity/runs/01411/` y `arte/unity/runs/camilla/`;
- `producto/RUNS.md`, cuadros 165, 169 y 170.

**Lo que no va:** el bot (no carga al malherido), los laboratorios y lo que solo cambia en el iPad y el iPhone.

**Las palabras de las fotos:** bien. Son las de `Lore.StretcherText`, con «malherido» o «malherida» según el soldado: «MALHERIDO», «NO ANDA», «Erik Nilsson cae malherido: hay que llevarlo a hombros a La Lata.» y «Llevad a Erik a La Lata».

**Sin probar, para Producto:**
- **Cuánto ayuda:** en la sonda de Gameplay se salva el 2 % de los malheridos con el bot de hoy, y el 6 % con uno que lo carga. Allí cae la escuadra entera en 3 de cada 4 expediciones, así que no mide a un jugador.
- **El malherido con mochila, con otros trajes y con otras armas largas.** Arte solo lo vio con un soldado con fusil de asalto.
- **Llevarlo hasta La Lata** solo lo prueba el motor. En pantalla no se ha visto cargarlo, dejarlo otra vez, pasarlo ni subirlo.
- **Nada en la app;** ni a dos jugadores ni con el dedo.
- **Partidas guardadas:** los campos nuevos van vacíos. Lo cubren las pruebas, no un guardado real.

Es para Arturo y Ana: cuenta qué cambia al jugar. Va para Mac y Windows. Producto completa al publicar lo que va entre [corchetes]: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.14.11: la Camilla

Versión 0.14.11 (commit [commit]). La última de las diez mejoras nuevas de la base.

## Partidas guardadas

- **La partida de la 0.14.10 sigue en la 0.14.11,** con la Camilla sin comprar.

## Novedades de la 0.14.11

### Camilla (250 §)

- **Una vez por expedición, el primer soldado que cae no muere:** queda malherido en el suelo, sentado con la tablilla.
  - Encima lleva «MALHERIDO» (o «MALHERIDA») y, en su busto, «NO ANDA».
  - No anda ni actúa.
- **Hay que llevarlo a hombros a La Lata,** como al herido de «El herido», con «Cargar», «Dejar» y «Pasar».
  - Lo carga cualquiera de la escuadra, también los soldados del otro jugador.
  - El objetivo dice «Llevad a … a La Lata».
- **Si llevaba la llave, el antiviral o las cargas,** caen al suelo junto a él. El resto de su mochila se queda con él.
- **Al subir a La Lata,** sale del combate con 1 de salud. En combate no se le cura. Si la etapa se gana con él a hombros, vuelve igual.
- **Muere si:**
  - le llega cualquier daño antes de subir, también el del fuego;
  - cae el último soldado en pie: se pierde la etapa;
  - La Lata sale sin él, o sin quien lo lleva a hombros.
- **La Lata no sale sola** mientras él siga vivo fuera. Para irse sin él hay que llamarla, y el aviso dice que se queda fuera.
- **Se gasta con el primero que cae,** se salve o no. El registro de la expedición lo dice: «La Camilla ya se ha usado en esta expedición.».
- **Vale en todas las dificultades,** y solo para los soldados de la escuadra: no para quien se une en ese mismo combate.
- **En «Separados»,** una vez en La Lata, cuenta como alguien de su grupo a bordo.

### Las mejoras de la base

- **Con la Camilla, ya están las diez nuevas:** dieciséis mejoras en total.

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
| `Bastion-0.14.11-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.14.11-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.14.11:** con la 0.14.10 no se puede jugar juntos.
- **En el Mac,** la app se llama «Bastión 0.14.11» y no sustituye a la 0.14.10: se quedan las dos. Borrad la vieja cuando queráis; las partidas y los ajustes son los mismos. Si teníais la vieja en el Dock, poned la nueva.
- **En Windows,** la carpeta se llama «Bastion 0.14.11».
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
