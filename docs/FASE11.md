# Fase 11 · Móvil y navegador

Lo pidió Arturo el 17/10: jugar desde el navegador del móvil, con controles táctiles, en una web suya (bastion.yumewagener.com). Es para jugar ellos dos y en horizontal. El servidor lo pone Arturo; nosotros damos el juego listo para servir y las instrucciones.

La versión de escritorio (la app) sigue igual. Lo táctil se activa solo cuando hace falta.

## 1. Detectar el móvil

- Se mira la forma de manejo, no el modelo: `(pointer: coarse)` y `(hover: none)`, más el tamaño de la pantalla.
- En Ajustes, «Controles táctiles: Auto / Sí / No», por si la detección falla (un portátil con pantalla táctil, una tablet con ratón).
- El modo táctil cambia los controles y la interfaz; los gráficos van aparte (§5).

## 2. En horizontal y a pantalla completa

- En vertical, un aviso a pantalla completa: «Gira el móvil».
- Un botón para la pantalla completa; donde se pueda, se bloquea la orientación en horizontal.
- Se puede añadir a la pantalla de inicio (manifest de aplicación web: horizontal y a pantalla completa, con el icono de La Lata, el autobús).
- Medidas de referencia: un móvil en horizontal, unos 844 × 390 px CSS (iPhone 13/14) y 915 × 412 (Android grande).

## 3. Controles táctiles en combate

Nada puede depender de pasar el cursor por encima.

- **Mover:** un toque en una casilla enseña el camino, el coste y el ruido; un segundo toque en la misma casilla, mueve. Un toque en otro sitio cambia la vista previa.
- **Atacar:** un toque en un enemigo lo apunta y enseña la probabilidad y el daño; un segundo toque, o el botón de la acción, dispara.
- **Información:** mantener pulsado una unidad, una casilla o un botón enseña su ficha (lo que hoy sale al pasar el cursor), también el cono de visión de un zombi.
- **Cámara:**
  - un dedo arrastra;
  - pellizcar acerca y aleja;
  - dos dedos giran.
  
  La cámara nunca pelea con el jugador.
- **Lo que hoy es una tecla, con botón en pantalla:**
  - cambiar de soldado;
  - acabar el turno;
  - el menú (Esc);
  - «Ver» y ceder el mando.
- **Botones:** de 44 px como mínimo, y la barra de acciones a mano del pulgar.

## 4. La base en el móvil

- Las pantallas de la base (Campaña, El valle, Armería, Cuartel, Instalaciones, Escuadra) se adaptan al horizontal del móvil:
  - paneles que se pliegan;
  - pestañas en una barra de iconos;
  - textos legibles sin acercar.
- El tablero hexagonal ya funciona con el dedo (tocar consulta, mantener compra, pellizcar acerca).
- Los desplegables y las fichas que hoy salen al pasar el cursor, con un toque.

## 5. Rendimiento en el móvil

- Un ajuste gráfico «Móvil», que se elige solo en modo táctil:
  - resolución limitada;
  - sombras bajas o sin ellas;
  - sin resplandor;
  - menos adornos.
- Objetivo: 30 fps estables en un móvil de gama media reciente, sin que se caliente en una misión entera.
- La telemetría de siempre (`[rend …]` en el `red.log` del servidor) dice cómo va el móvil de cada uno.

## 6. Servir el juego en una web

- Un paquete listo para el servidor de Arturo:
  - el cliente compilado y el servidor de partidas (Node, WebSocket);
  - un solo comando para arrancarlo;
  - el puerto, la carpeta de la partida y la contraseña, por variables de entorno.
- **Contraseña de la partida:** sin ella no se entra. Es para que nadie que encuentre la dirección se meta en la campaña.
- Detrás de un proxy con https (wss).
- **El móvil cambia de app y vuelve:** se reconecta solo, sin perder el turno.
- **Sonido:** en el móvil el navegador lo bloquea hasta el primer toque; se desbloquea con él.
- Una guía corta para Arturo: qué copiar, cómo arrancarlo y qué poner en el proxy.
- **Licencia de Synty:** con la contraseña, solo lo ven ellos dos. Si algún día fuera público, los modelos se descargarían tal cual desde el navegador, y habría que revisar la licencia antes.

## 7. Quién hace qué

- **UI:**
  - la detección y el ajuste «Controles táctiles»;
  - los controles táctiles en combate (§3);
  - la base en el móvil (§4);
  - el aviso de girar y la pantalla completa.
- **Arte:** el ajuste «Móvil» (§5), medido en un móvil de verdad o, mientras tanto, en la emulación del navegador.
- **Red:**
  - el paquete para el servidor, la contraseña, el proxy con https y la guía (§6);
  - la reconexión al volver a la app;
  - el sonido en el móvil;
  - el manifest y el icono.
- **Lore:** los textos de los avisos y de la ayuda táctil.
- **Gameplay:** coordina, prueba en un móvil emulado y saca la versión.

## 8. Estado (17/10)

Hecho todo lo de arriba, sin probar aún en un móvil de verdad ni en el dominio:

- **Modo táctil** (UI): se detecta solo y tiene su ajuste; el aviso de girar y la pantalla completa; el combate con el dedo (toque y segundo toque, mantener para la ficha, cámara con uno y dos dedos, botones de 44 px); la base en horizontal, con paneles que se pliegan y pestañas de iconos.
- **«Móvil»** (Arte): resolución justa, 30 fps, sin resplandor ni AO, FXAA, menos luces, adornos y partículas. Se elige solo en táctil. Un tercio del trabajo de la gráfica.
- **La web** (Red):
  - `npm run web:dist` hace el paquete; la contraseña es obligatoria, y sin ella el servidor no da ni los modelos ni la partida;
  - la reconexión al volver de otra app guarda el turno 5 min, y las cuentas atrás esperan a quien está fuera;
  - el sonido arranca con el primer toque;
  - el manifest;
  - «Unirse por dirección» desde la app entra en la web;
  - el puesto se recupera por el nombre.
  
  La guía está en [WEB.md](WEB.md).
