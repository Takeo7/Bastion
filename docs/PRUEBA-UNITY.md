# Primera prueba de Unity con dos ventanas

Guía para Arturo. La preparó UI el 2026-10-24. Se hará cuando Arte tenga los edificios enteros (piezas, muros, tejados y el corte para ver dentro).

El objetivo es jugar una misión de ciudad de día entre dos apps de Unity y apuntar lo que se vea mal o vaya distinto de la 0.10.1. Si ya están los gráficos en Ajustes y la telemetría, también se mide el rendimiento en el Mac.

## 0. Antes (UI)

- UI compila la app justo antes de la prueba, con lo último de todos. Sale en `unity/Bastion/Builds/Mac/Bastion.app`.
- UI la abre dos veces sin ventana para comprobar que entra en una misión y que no hay errores en los registros.

## 1. Abrir las dos apps

**Con Ana, cada uno en su ordenador:** cada uno abre `Bastion.app` con doble clic. Si Ana no tiene el repositorio, se le pasa la carpeta `Bastion.app`. Si le llega por AirDrop o descargada, macOS la bloquea la primera vez («no se puede comprobar…»). Hay dos salidas:
- Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente»;
- en el Terminal, `xattr -dr com.apple.quarantine Bastion.app`.

**Las dos en el Mac de Arturo**, una al lado de la otra: la primera, con doble clic en `Bastion.app`. La segunda, desde el Terminal, en ventana y con su propia carpeta de datos (si no, las dos usarían el mismo puesto):

```bash
BASTION_DATOS="$HOME/Library/Application Support/BastionPrueba2" /Users/yume/projects/Bastion/unity/Bastion/Builds/Mac/Bastion.app/Contents/MacOS/Bastion -screen-fullscreen 0 -screen-width 1280 -screen-height 800
```

Si la primera sale a pantalla completa, se puede abrir en ventana igual, desde otro Terminal:

```bash
/Users/yume/projects/Bastion/unity/Bastion/Builds/Mac/Bastion.app/Contents/MacOS/Bastion -screen-fullscreen 0 -screen-width 1280 -screen-height 800
```

**En Linux (sin probar):** como con la de la 0.10.1, no se ha probado en un Linux de verdad; solo se ha comprobado que compila sin errores. Es para x86_64, con Vulkan u OpenGL.
- Se pasa `unity/Bastion/Builds/Bastion-Linux.tar.gz` (194 MB).
- Se descomprime y se abre desde una terminal:

  ```bash
  tar -xzf Bastion-Linux.tar.gz
  ```

  ```bash
  ./Bastion/Bastion.x86_64
  ```

- Si se aloja con el cortafuegos activado (ufw), hay que abrir el TCP 41391 (o el siguiente libre, hasta el 41400) y el UDP 41390.
- Sus datos van en `~/.config/unity3d/Bastion-Unity/Bastion/`. Para una segunda copia en el mismo ordenador, como en el Mac: `BASTION_DATOS=<otra carpeta> ./Bastion/Bastion.x86_64`.

## 2. Entrar en la partida

1. En las dos, escribir un nombre distinto en «TU NOMBRE».
2. Quien aloja pulsa «SER ANFITRIÓN».
3. El otro ve la partida en «PARTIDAS EN TU RED» y la elige. Si no sale, usa «UNIRSE POR DIRECCIÓN» con la dirección que aparece en la sala de quien aloja.
4. En la sala:
   - mirar que los dos salen en «Comandantes» como «Conectado»;
   - Misión: **Limpieza**; Mapa: **Ciudad**; Hora: **De día**;
   - «Empezar escaramuza».
5. Sale la tarjeta de llegada. Se cierra con «Entendido», Intro o Espacio.

## 3. Qué mirar

Cada cosa, en las dos ventanas.

**El mapa y la vista**
- La ciudad: edificios, calles, coches; la niebla sobre lo que no se ve.
- La cámara: Q y E giran, WASD y la rueda mueven y acercan, el botón derecho arrastra. RePág y AvPág (o Mayús y la rueda) cambian de planta.

**Tu turno**
- El sello «TU TURNO», el marco del color de quien tiene el mando y los relojes junto a «Terminar turno».
- Elegir soldado con clic o Tab: su anillo y su panel abajo a la izquierda.
- El alcance al pasar el ratón: una acción y carrera, el camino, los escudos de cobertura y la nota de la casilla («Ruido 3: corre»).
- Mover: el soldado anda casilla a casilla y el otro jugador lo ve.
- Sin el mando: el alcance se ve más tenue, y un clic deja un plan que el otro ve en tu color. Al llegarte el mando, el soldado recorre su plan.
- Señalar un sitio con G, Mayús+clic o clic con la rueda: haz de luz y tu nombre encima, en las dos ventanas.
- Disparar: el panel con la probabilidad y el daño, el anillo del blanco y, si lo mata, el plano al hombro.
- Granada: el área, el centro y las paredes que rompe.
- «Terminar turno» (o Retroceso): pide confirmación con una segunda pulsación.

**El turno enemigo**
- El sello «TURNO ENEMIGO» y la cámara yendo a cada zombi que actúa, sin pelearse contigo si mueves la cámara.
- Mantener la barra espaciadora: va más rápido; al soltarla, vuelve a su velocidad.
- Contactos («CONTACTO», «OS HAN OÍDO»), el anillo del ruido y los mordiscos.

**El menú (Esc)**
- Ajustes: Gráficos (Altos, Medios, Bajos), Límite de FPS, Mostrar FPS, la velocidad del turno enemigo y «Seguir las rutas planeadas».
- Controles, y las preguntas de «Volver al menú principal», «Abandonar la misión» y «Salir de Bastión» (se puede contestar «Seguir» a cada una).

**El final**
- Acabar la misión o abandonarla, con la llamada que le llega al otro, y el resultado.

## 4. Medir el rendimiento

No hay que hacer nada aparte: cada app manda una línea cada 10 s de juego al red.log de quien aloja («[rend Arturo] combate 58 fps · p95 … · cpu … · gpu …»).

- En el Mac, con «Gráficos: Medios» (lo que sale por defecto en Mac), jugar al menos 5 minutos de combate. El objetivo es ≥30 fps.
- Si se quiere ver en pantalla: Ajustes → «Mostrar FPS: Sí».
- En el PC de la 3090, lo mismo con «Altos». El objetivo es ≤4,2 ms por fotograma.

## 5. Qué apuntar

- Lo que se vea mal o no se entienda, con el momento y en qué ventana. Una captura ayuda (Cmd+Mayús+4).
- Lo que vaya distinto de la 0.10.1 o falte.
- Al acabar, los `red.log` de las dos apps:
  - la de doble clic: `~/Library/Application Support/Bastion-Unity/Bastion/red.log` (en Windows, `AppData/LocalLow/Bastion-Unity/Bastion/red.log`);
  - la segunda del Mac: `~/Library/Application Support/BastionPrueba2/red.log`.
