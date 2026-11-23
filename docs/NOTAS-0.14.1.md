# Notas de la 0.14.1

Texto de Lore para la 0.14.1. Lo pidió Producto el 23/11. Sin commit.

**Qué lleva:** retoques, sin reglas nuevas:
- la sala de multijugador nueva (cuadros 124 y 130);
- la sección B de la revisión de la interfaz de Producto;
- lo menor de la pasada R19 de Red;
- seis descripciones de acciones (Gameplay) y la bienvenida de la base (Lore).

**Fuentes:**
- el cierre de UI, `.dev-snapshots/ui/unity/runs/CIERRE-0.14.1.md`, con `sala/LEEME.md`, `r19/LEEME.md` y `seccion-b/LEEME.md`;
- `producto/RUNS.md`, cuadros 124 y 130;
- `red/runs/PARA-PRODUCTO-r19.md`;
- `lore/MENSAJE-DE-GAMEPLAY-acciones.md`.

**Lo que no va:**
- lo de dentro: pruebas, bots y laboratorios;
- el mapa de etapas nuevo;
- lo que solo cambia en el iPad y el iPhone.

Es para Arturo y Ana: cuenta qué cambia al jugar. Va para Mac y Windows. Producto completa al publicar lo que va entre [corchetes]: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.14.1: la sala nueva y retoques de la interfaz

Versión 0.14.1 (commit [commit]). Retoques de la interfaz, sin reglas nuevas: la sala de multijugador nueva y lo que salió de revisar la interfaz entera.

## Partidas guardadas

- **La partida de la 0.14.0 sigue en la 0.14.1,** con su dificultad.

## La sala de multijugador

- **Es una pantalla del juego:** el cruce nevado detrás y las opciones a la izquierda, como en el menú principal.
- **Los dos jugadores, en grande,** cada uno en su color, con su nombre y si está en línea. «TÚ» marca el de esta app.
- **La dirección ya no está a la vista.**
  - Debajo de los jugadores dice qué verá el otro en su app: «Unirse a la partida de Arturo».
  - Si no le sale, «¿No le sale tu partida?» abre las direcciones, con «Copiar», y la clave.
- **La dificultad, a la vista:** bajo «Continuar partida», «Capítulo 1 · Medio».
- **La dificultad se elige en la sala,** al pulsar «Nueva partida», con las cuatro tarjetas. «Alojar partida» pide solo el nombre.
- **En el inicio,** «Unirte a la partida de Ana» pasa a «Unirse a la partida de Ana», como «Unirse por dirección».

## En la base

- **La bienvenida, más corta:** dos párrafos.
- **La hoja del soldado:**
  - las tres barras de colores llevan su nombre y su cifra: «SALUD 5», «ACCIONES 2», «MOVIMIENTO 8»;
  - bajo cada barra de experiencia, lo que sube: «Puntería cuerpo a cuerpo +10 %».
- **Mejoras:** el nivel de cada una en palabras: «Sin mejorar», «Nivel 1 de 3», «Nivel 3 de 3: al máximo».
- **A dos, si uno se queda sin soldados,** la base enseña todas las plazas con los soldados del otro. Antes salían dos «LIBRE», aunque partían los cuatro.
- **Al perder la partida,** sale primero el parte de la escuadra perdida, y con «Continuar», la ventana «Partida perdida». Antes salían las dos a la vez.

## En la expedición

- **Trueque:**
  - bajo lo que no se puede cambiar, en ámbar, el porqué: «Faltan 25 §.», «No cabe en la mochila de Anja.»;
  - bajo lo que llevan los soldados: en una expedición solo cuenta eso; lo del almacén se queda en la base.
- **Suceso:** sin elegir, ya no sale «Seguir» apagado. En su sitio: «Antes hay que elegir una opción.»

## En combate

- **La ficha de una acción** dice primero, en negrita, lo que hace. El detalle va debajo.
- **Seis descripciones, corregidas:**
  - Esfuerzo extra, Granada, Cóctel molotov, Piedra y Golpe silencioso, para que su primera frase se entienda sola;
  - Evacuar, que en una expedición decía que la misión se pierde. No es así: sin el objetivo, la etapa se supera sin la recompensa; ante el jefe, la expedición acaba.
- **La ficha del soldado** enseña siempre la puntería, con su palabra: «PUNT. 55» y «CaC 58».
- **El panel de apuntar:** «1 de 13 · Tab: siguiente», bajo el nombre del blanco. Antes decía «1/13 · Tab».
- **«CAÍDO»** sale en el busto en cuanto la salud llega a 0, a la vez que se vacía la barra. Antes salía unos segundos después, al acabar de verse la muerte.

## Ajustes

- **Una línea bajo el nombre** en Ajuste auto, Interfaz, Aspecto de película, Abrir el puerto del router y Sincronía vertical. La ayuda larga sigue saliendo al pasar el puntero.
- **Con la sincronía vertical y un límite de FPS,** una línea explica que el límite se redondea hacia arriba según la pantalla: con 60 en una de 144 Hz, va a 72.
- **Abiertos desde el inicio,** los Ajustes ya no tapan «BASTIÓN».

## Lo que sigue sin arreglar

- **El fuego dentro de un primer piso,** visto desde la calle, parece arder por fuera, en el alféizar de la ventana.
- **Un soldado o un cuerpo** puede quedar tapado por un edificio sin descubrir.
- **Los marcos de una casa sin descubrir** se ven claros, como si diera la luz de la calle.
- **Una farola** puede quedar en el bordillo, delante de las puertas de La Lata.
- **El aviso del compañero caído** («Pierre Lambert ha caído · Ver (F)») tapa «Turno enemigo».
- **Los guardias sentados de la hoguera,** con la cámara normal, parecen de pie.
- **La dificultad** se ajusta jugando: apuntad lo que notéis en cada nivel.
- **La sincronía vertical con la pantalla dormida,** o apagada al momento con una esquina activa, está sin probar.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.14.1-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.14.1-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.14.1:** con la 0.14.0 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.14.0.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
