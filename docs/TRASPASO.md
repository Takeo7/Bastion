# Traspaso: estado del proyecto y cómo seguir en otra máquina

Escrito el 2026-10-14 con la versión 0.2.1 y puesto al día el 21/10 con la 0.9.3, la de la segunda tanda de notas de Ana del 21/10. Recoge lo hecho, las decisiones tomadas y lo que hay que llevar a mano a otra máquina, porque no está en GitHub. Las normas de trabajo están en [CLAUDE.md](../CLAUDE.md); los roles, en [EQUIPO.md](EQUIPO.md).

## 1. Qué no está en GitHub y hay que copiar a mano

| Qué | Dónde está | Hace falta para | Cómo |
|---|---|---|---|
| Packs originales de Synty (unos 4,6 GB) | `assets-src/synty/` | Convertir modelos nuevos | Copiar la carpeta tal cual (disco o red). **Nunca a GitHub**: la licencia lo prohíbe |
| Capturas de referencia de Synty | `assets-src/referencias/aspecto-synty/` | El aspecto objetivo | Va dentro de `assets-src/` |
| La partida de los jugadores | `data/save.json` (y las copias `save.antes-*.json`) | Seguir la campaña donde la dejasteis | Copiar `data/`. Sin él, el servidor empieza sin partida |
| Certificados https para el compañero | `data/tls/` | Que Safari se conecte por https | Opcional: si falta, el servidor los crea de nuevo con openssl. En Windows no hay openssl: solo http, que con Chrome, Edge o la aplicación va bien |
| Configuración de los servidores de prueba | `.claude/launch.json` | Que las sesiones de Claude arranquen sus servidores | Copiarla, o rehacerla con la tabla de puertos de EQUIPO.md |
| La memoria de Claude | `~/.claude/projects/<carpeta del proyecto>/memory/` | Preferencias y decisiones de las sesiones | Opcional: lo importante está en este documento y en CLAUDE.md. Para copiarla, la carpeta destino lleva la ruta del proyecto en la nueva máquina, con `/` cambiadas por `-` |

Lo demás, código, modelos convertidos, sonidos, iconos y documentos, viene con `git clone`.

## 2. Poner la máquina en marcha

1. `git clone` de `github.com/Takeo7/Bastion` (público desde el 6/10; ver §5).
2. **Node.js** 22 o superior. Aquí se usa la 26.
3. Instalar las dependencias:

   ```bash
   npm install
   ```

4. Comprobar que todo compila y que los tests pasan:

   ```bash
   npm run typecheck
   ```

   ```bash
   npx vitest run --root packages/engine
   ```

   Deben pasar 542 tests.
5. **Probar en el navegador** (se juega con la aplicación; esto queda para probar):

   ```bash
   npm run build
   ```

   ```bash
   npm start
   ```

   El servidor sale en 3000, y en 3001 con https para el compañero.
6. **Aplicación de escritorio:**

   ```bash
   npm run desktop:dist
   ```

   Sale en `packages/desktop/dist/release/`. Las versiones de Windows (`npx electron-builder --config electron-builder.json --win`) y de Linux (`--linux`: AppImage y tar.gz) se construyen también desde el Mac, en `packages/desktop` ([PRUEBA-PC-3090.md](PRUEBA-PC-3090.md), paso 0); cómo va la firma, en [FIRMA.md](FIRMA.md).
7. **Convertir modelos de Synty** (solo si se van a añadir más): hace falta **Blender**, y `assets-src/` copiado. Siempre con ids explícitos (ver `packages/client/tools/synty/README.md`).

## 3. Estado (versión 0.9.3)

- **Juego:** combate por turnos cooperativo para dos, con el mando que se cede. La campaña tiene la base en un claro del pinar que se construye y se defiende en asedios ([FASE12.md](FASE12.md) §11), el valle, misiones, soldados que aprenden de lo que hacen ([FASE10.md](FASE10.md)), heridas y muerte permanente.
- **Tema (zombis), las 5 fases hechas** ([LORE.md](LORE.md)):
  - Saqueo, ruido e IA de zombis ([FASE2.md](FASE2.md));
  - contagio y medicinas ([FASE3.md](FASE3.md));
  - comida, Huerto, piezas, armas fabricadas o encontradas ([FASE4.md](FASE4.md));
  - el valle de Kessra con carreteras y la Marea por zonas ([FASE5.md](FASE5.md)).
- **Además:**
  - noche y altura de los edificios ([ALTURA-Y-NOCHE.md](ALTURA-Y-NOCHE.md));
  - edificios de varias plantas que se pisan ([FASE6.md](FASE6.md), [FASE6-PLAN.md](FASE6-PLAN.md)).
- **Arte:**
  - todo Synty POLYGON: personajes con animaciones, mapas en los que se entra, dos estilos de ciudad (cuadrícula y pueblo viejo), la base, el mapa del valle y los efectos;
  - lo vóxel está borrado.
- **Interfaz:** «Mixta» es la única: paneles de acero, iconos, movimiento y el árbol de habilidades nuevo. Cada jugador ve la suya.
- **Aplicación de escritorio** (Electron):
  - servidor dentro, «Ser anfitrión» y «Unirse», partidas en la red local, icono;
  - en Windows, la regla del cortafuegos;
  - releases v0.1.0, v0.2.0, v0.2.1, v0.2.6, v0.2.7, v0.2.8, v0.2.9, v0.3.0, v0.3.1, v0.4.0, v0.4.1, v0.4.2, v0.4.3, v0.4.4, v0.4.5, v0.5.0, v0.5.1, v0.6.0, v0.7.0, v0.7.1, v0.7.2, v0.8.0, v0.8.1, v0.8.2, v0.8.3, v0.8.4, v0.9.0, v0.9.1, v0.9.2 y v0.9.3 en GitHub (la 0.5.0 y la 0.5.1, también con el paquete web; desde la 0.6.0, solo la aplicación de escritorio) (el repositorio es público desde el 6/10, y Arturo lo sabe: las releases también lo son); de la 0.2.2 a la 0.2.5 no están publicadas. Las builds de Mac, Windows y (desde la 0.2.7) Linux se construyen todas desde el Mac; la de Mac va con firma ad hoc ([FIRMA.md](FIRMA.md)). La de Linux sale sin probar en un Linux de verdad.
- **Lo nuevo de la 0.9.3** (2026-10-21), la segunda tanda de Ana del 21/10 ([PRUEBA-2026-10-21.md](PRUEBA-2026-10-21.md) #29–#47):
  - el tiempo (Arturo): cada misión es un día y cada soldado come una ración al día; heridas, fiebre, obras, convoy y reloj de la Marea, ajustados a misiones (campaña v25, que convierte las partidas de antes). Cada Huerto da 5 raciones por nivel al día; la Chapistería quita un día a cada herida por nivel;
  - reclutar y comprar en la Armería se proponen al otro, como construir; fuera los bonus de cercanía («Junto a»); la Sala de radio, la Cuarentena y el Camposanto, desde la Caravana con toldo;
  - Asentamiento: Talar, Edificios y Defensas en paneles con cuadrícula de imágenes; el panel «Bastión» con camas, raciones, Huertos y convoy en lugar de «Efectos»; las ayudas, frase a frase y con las cifras grandes; «Día N» en el Mapa y en las raciones; fuera los textos que tratan al jugador como novato;
  - en Soldados, solo los tuyos; los contactos, solo en el Mapa; el título largo de una misión cabe;
  - la vista de los zombis (Arturo): fuera los conos rojos fijos; al pasar el ratón, lo que ve un zombi; el ojo en la zona de movimiento;
  - los pueblos con nombres reales (Ossau, Bielsa, Tarbes, Sappada, Koper, Rudnik, Tende) y sin red amiga: tratos desconfiados por radio, sin correo ni mensajeros (Arturo);
  - la caravana y la mesa de la Armería con texturas normales, y los muros macizos al cortarlos;
  - si el anfitrión cierra, la compañera ve una pantalla clara con «Volver a entrar» y «Salir de Bastión»; Esc quita la pantalla completa y el inicio tiene «Salir de Bastión».
- **Lo nuevo de la 0.9.2** (2026-10-21), las notas de Arturo y Ana del 21/10 ([PRUEBA-2026-10-21.md](PRUEBA-2026-10-21.md)):
  - animaciones de armas de Mixamo (Arturo las permitió y las bajó, [MIXAMO.md](MIXAMO.md)): fusil en reposo, apuntando, disparando y recargando; agachado; pistola siempre a dos manos; lanzar granada y piedra con el fusil a la cadera; cuchillo y machete. Las otras siguen siendo de Synty;
  - la vista en tres zonas (Arturo): cerca, con enemigos; hasta 40 casillas en la línea de visión, el entorno con bruma leve y sin enemigos (`teamView`); fuera, oscuro. Los zombis que te ven llevan un ojo rojo y dicen a por quién van;
  - el primer disparo ya no congela el juego (los efectos usan dos luces fijas y todo se dibuja una vez tras el velo);
  - ventanas atrancadas de verdad, todas las de una habitación, con muebles apilados; ningún mueble tapa una ventana por casualidad. Supermercados con género (dan 6–10 raciones) o vaciados;
  - fuera del mapa, el pueblo o el bosque siguen en lo oscuro; la base, sin corte cuadrado;
  - la subida a La Lata con su plano; la cámara sigue al soldado sin saltos; sin el saltito al lanzar la misión, sin el disparo mirando atrás y sin árboles flotando;
  - Escuadra en dos paneles, «Lanzar misión» / «A punto», el informe maquetado de nuevo, solo ventanas para el asedio y la subida de nivel, «Nueva misión», la tumba del caído en Soldados, la zona de movimiento sin recuadros alrededor de coches y personas, y el crítico sin sonido de campana.
- **Lo nuevo de la 0.9.1** (2026-10-21), el cielo:
  - una cúpula con degradado que sigue la luz del mapa (día, atardecer, noche y nieve), con montañas nevadas en el horizonte (POLYGON Nature), siluetas de edificios en los pueblos y polígonos (Generic_Skyline) y anillos de nubes;
  - se ve en la cámara al hombro, en los primeros planos y en la base; en la vista táctica, con su picado de 52°, no entra;
  - lo oscuro de fuera del mapa se funde con la neblina lejos del borde;
  - la «Chaqueta oscura» de los soldados ya no lleva pinchos (era de los Forajidos).
- **Lo nuevo de la 0.9.0** (2026-10-21), los personajes de Synty Sidekick ([SIDEKICK.md](SIDEKICK.md)):
  - soldados, zombis y Cuervos pasan de POLYGON a Sidekick; los jefes zombi (Corredor, Acorazado, Chillón, Hinchado) siguen igual;
  - cada soldado se viste con piezas: 15 conjuntos de invierno y sobrios (supervivientes, civiles y policía), sin repetirse en la escuadra, y además cara, peinado, barba, piel, color de pelo, lo de la cabeza y la complexión. En Personalizar, «Ropa»;
  - el aspecto de serie tiene su propia cabeza y complexión por soldado (campaña v24: los soldados sin personalizar de las partidas de antes lo reciben);
  - la ropa va con sus colores y cada jugador se distingue por el círculo; «Ropa con el color del jugador», en Ajustes, la tiñe como antes;
  - 17 zombis de Sidekick (con ropa de zombi, de civil y de policía), el resucitado con su ropa y los Cuervos con los Forajidos;
  - la norma de arte de CLAUDE.md pasa a «Synty POLYGON y Sidekick»; los paquetes están en el disco externo de Arturo;
  - en el Mac, los mismos fps que con POLYGON; falta la medida de Arturo en la 3090.
- **Lo nuevo de la 0.8.4** (2026-10-21), las notas de Arturo y Ana del 20/10, jugadas con la 0.8.3 ([PRUEBA-2026-10-20.md](PRUEBA-2026-10-20.md)):
  - la base sin la vista de Campaña: se abre en Soldados (también tras el informe); abajo, Mapa · Armería · Soldados · Asentamiento (antes Construir); el Bastión se mejora al pulsar la caravana en Asentamiento; los avisos que llegan juntos salen como ventanas, de uno en uno, el más importante primero;
  - los niveles del Bastión con nombre (Caravana, Caravana con toldo, Casa móvil, Casa móvil con porche, Albergue, Albergue ampliado, Casa y Casa fortificada), sin «nivel N»; lo que aún no se tiene no aparece y lo nuevo sale con «Nuevo»; cada mejora se ve en el claro;
  - varios Huertos, cada uno con lo suyo (5, 10 o 15 raciones por misión), dicho antes de ponerlo;
  - en combate: la cámara por encima del hombro en el disparo o el golpe que mata; «FALLO» en lugar de la tirada, y en el registro la probabilidad que había; los zombis que salen de la niebla entran andando y fundiéndose; el primer disparo ya no se queda pillado; la rueda solo acerca y aleja (las plantas, con Mayús); fuera la barra «HORDA»; la vigilancia sin balas dice «Sin balas: recarga»; la ayuda de la noche, con las cifras;
  - los mapas: un 25 % más de muertos en los grandes; lo de fuera de la zona jugable, oscuro; supermercados de verdad en 6 de cada 10 pueblos; las tiendas con escaparate y el cartel sobre la puerta; coches de colores y menos montones de basura;
  - los brazos ya no se meten en el cuerpo ni dan saltos; se sube a La Lata por sus puertas; la Armería es un rincón con banco, armero y caja; la Escuadra y el informe, centrados como en el dibujo de Ana;
  - 120 fps en Ajustes y el contador de fps sin contar doble; la música en «No» calla del todo; las opciones de «Refugiados» con sentido.
- **Lo nuevo de la 0.8.3** (2026-10-20), el tiempo real tras la primera prueba de Arturo ([TIEMPO-REAL.md](TIEMPO-REAL.md)):
  - Tab (o clic en la tarjeta) para cambiar de soldado; 1, 2, 3… las habilidades, como por turnos, y al armar una que pide blanco el juego va a cámara muy lenta, con el blanco ya elegido como por turnos (Tab, el siguiente; la misma tecla, Intro o Espacio, usarla);
  - todas las habilidades de la barra en tiempo real, cada una con su tiempo, yendo antes adonde se puede usar; la vigilancia como postura; lo que dura un turno dura una ronda de 6 s de cada uno;
  - la barra de habilidades en una sola fila, también por turnos.
- **Lo nuevo de la 0.8.2** (2026-10-20):
  - el prototipo del modo en tiempo real ([TIEMPO-REAL.md](TIEMPO-REAL.md), paso 1): en Escaramuza, «Ritmo: Tiempo real (prueba)». Todo a la vez: clic para ir y para atacar, los zombis persiguen y muerden, barra espaciadora para la pausa. Para juzgar el ritmo; por turnos no cambia nada;
  - la escuadra ya no sale apiñada: en la 0.8.0 y la 0.8.1 el primer soldado podía empezar encerrado entre sus compañeros, sin casilla adonde ir.
- **Lo nuevo de la 0.8.1** (2026-10-20), las notas de Arturo del 19/10, jugadas con la 0.7.1 ([PRUEBA-2026-10-19.md](PRUEBA-2026-10-19.md)):
  - la Escuadra: la misión abajo a la izquierda y los botones a la derecha, sin tapar a los soldados; la fila recta y centrada, también con un jugador solo y en la pantalla del segundo; tarjetas bajas con una × para quitar; «Cancelar misión» pasa a la ficha de la misión en El Valle;
  - arriba, sin los jugadores conectados, sin «Aún sin salir» y sin «−10/misión» (está en la ayuda); la comida con su icono;
  - un solo «Menú», también con Esc: Continuar, Ajustes, Volver al menú de inicio, (en combate) Abandonar la misión y Salir de Bastión. En mitad de una misión de campaña, volver al menú la deja en pausa y guardada, y «Continuar campaña» vuelve a ella; mientras tanto no hay escaramuza;
  - en combate: la ventana del principio con el texto entero y los turnos en amarillo; señalar con Mayús+clic; «Cámara al soldado» con interruptor y otra vez «Ir a …» (F); las acciones sin objetivo, de un clic; los datos de cada acción en una tabla igual para todas; sin «Zombis: llegan este turno» ni «Puede matarlo»;
  - cursores del juego (Apocalypse HUD de Synty), que cambian sobre lo que se pulsa y sobre un enemigo;
  - el arte: La Lata de la portada más despacio; los soldados suben a La Lata por su puerta, sin atravesarla; una alcantarilla por calle, lejos de los cruces; las puertas se distinguen de las ventanas viendo la primera planta; el «ACTION NEWS» de la furgoneta ya no sale al revés.
- **Lo nuevo de la 0.8.0** (2026-10-20), los mapas nuevos ([MAPAS.md](MAPAS.md), fases 0 a 6, decididas por Arturo el 19/10):
  - nadie atraviesa a nadie: los compañeros se bloquean y los muebles grandes ocupan su casilla; coches, camiones, camas y mesas a su tamaño; las animaciones no atraviesan cosas;
  - escaleras de tramo: 3 casillas (2 en casas pequeñas), solo se suben desde el pie y nadie se para a mitad;
  - los pueblos son de 64 (el del hospital, 72 × 64) y salen de un plano: cuadrícula con avenida, o casco viejo con plaza y calle mayor. Casas por parcelas, patios, bloques de pisos, baños y despensas;
  - el borde no es un cuadrado: lo cierran manzanas cerradas, solares, ruinas y obras, y a veces un río, las vías, una autovía o una barrera. Se entra por 2–4 calles, y la escuadra empieza en una;
  - el autobús espera junto a la acera en una calle de salida, mirando hacia ella, con la evacuación a lo largo de sus puertas. Los turnos de la misión dependen de lo que hay que andar, y hay más grupos de muertos en los mapas grandes;
  - el campo es de 72 (el área de servicio, 64 × 72), cerrado por bosque, rocas, agua o un barranco. El camino entra y sale con una boca abierta en el bosque. El polígono y la fábrica, de 72, con la nave a su tamaño;
  - la base es de 56: piezas a su tamaño nuevo y un solar del Bastión de 9 × 9. Una base guardada va al centro con lo construido a su tamaño de antes; lo que cae en el solar nuevo se desmonta y se devuelve entero (campaña 23);
  - las hordas entran siempre: si una oleada no cabe junto al borde, entra un poco más adentro. En el campo, las de 6 o más no llegaban nunca;
  - una granada ya no se lleva toda una fila de troncos en la base, y el Hinchado sube los tramos de 3;
  - rendimiento para los mapas de 72: la sombra encuadrada en lo que se ve, puertas y muros que rehacen solo lo tocado.
- **Lo nuevo de la 0.7.2** (2026-10-19), la revisión del código ([REVISION-2026-10-19.md](REVISION-2026-10-19.md)):
  - el tablero de habilidades ya compra siempre (era el fallo #38 y #48 del 18/10);
  - la partida no se pierde: copia anterior (`save.anterior.json`), una ilegible se aparta y se sigue desde la copia, y un fallo al escribir no tumba el servidor; «Campaña nueva» guarda la de antes;
  - las propuestas que ya no se pueden pagar se quitan a la vista de los dos; «Entre los dos» cura de verdad; el daño del asedio cuenta todo el edificio del Bastión; nadie empieza un asedio encima de una torreta o una trampa;
  - el servidor aguanta direcciones mal escritas; «Unirse» vuelve al inicio si no carga; el `red.log` no crece sin fin;
  - menos fugas y tirones: contextos WebGL, eventos que retenían el combate, shaders compilados detrás del velo;
  - fuera el código muerto: la base de parcelas y su vista 3D, el dron, el Oficio, las ventajas que ya no da nadie y la experiencia del Bastión; «Cochera» pasa a «Escuadra».
- **Lo nuevo de la 0.7.1** (2026-10-18), el resto de la prueba del 18/10 ([PRUEBA-2026-10-18.md](PRUEBA-2026-10-18.md), #24–#58):
  - el nivel del Bastión ya no sale de la experiencia: se compra con suministros y misiones hechas (`levelUp`, `LEVEL_COST`, `LEVEL_MISSIONS`), y en el claro el Bastión pasa de la caravana a un edificio, cada dos niveles, sobre su solar de 6×6. La Sala de mapas da más suministros por misión;
  - las misiones no caducan: hasta 3 a la vez (4 con la Sala de radio), y con el asedio solo se ve el asedio;
  - La Lata empieza siendo el todoterreno de Teo (4 plazas) y se mejora con misiones de vehículo: furgoneta 6, camión 7 y autobús 8, que está en Atalaya. Las partidas empezadas pasan a la furgoneta. El Centro de entrenamiento da reclutas con más nivel;
  - cada uno lleva a sus soldados: solo el dueño sube sus habilidades y los mete en la escuadra, aunque el otro esté desconectado. La escuadra se elige en el claro, junto a La Lata, y la fila sale en espejo para que cada uno vea a los suyos junto a su nombre;
  - todo lo de la base va a voto con el otro conectado. Talar cuesta 3 § por árbol. Las vallas, los muros, los sacos, los troncos y el alambre se ponen en fila arrastrando;
  - antes de un asedio, cada uno coloca a sus defensores donde quiera del claro (`post`);
  - los enemigos vivos ven en cono, y vivos y muertos se atacan entre sí (`hostileTo`). El soldado que cae deja su mochila con lo que llevaba;
  - en combate, los objetivos siempre a la vista en una tarjeta a la derecha; las rutas planeadas se siguen en orden y se quitan con el botón derecho; un solo círculo en el soldado elegido; el cono rojo tenue; líneas continuas; sin modales de doble botón;
  - arriba, el nombre de cada recurso al pasar el ratón y lo que gastan las raciones; fuera el «(tú)»; no entran dos jugadores con el mismo nombre;
  - interiores con su ajuar, tiendas por oficio y bibliotecas; el granero de verdad; bordes del mapa cerrados con sentido; textos de ayuda directos, sin adornos.
- **Lo nuevo de la 0.7.0** (2026-10-18), la base nueva en la campaña ([FASE12.md](FASE12.md) §11) y la prueba del 18/10 ([PRUEBA-2026-10-18.md](PRUEBA-2026-10-18.md)):
  - la campaña empieza con una caravana en un claro del pinar, distinto cada vez; se tala, se construye (edificios, empalizadas, muros, puertas, torres, focos, alambre, minas, pinchos, cepos y torretas), se repara, se repone y se desmonta. Lo que pasa de 30 § se vota;
  - las instalaciones son los edificios del claro: un edificio con la mitad de su aguante o menos no funciona;
  - el tiempo se cuenta en misiones (5 días cada una), sin «Esperar noticias». Solo se espera cuando nadie puede salir ni hay con qué reclutar;
  - cada pocas misiones, un asedio en la base: los muertos llegan por cualquier lado y van a por los soldados; defienden todos los sanos, y las torretas y las trampas actúan solas. Con la Sala de radio se sabe por dónde y cuántos. Uno de cada tres, desde el segundo, de noche: el foco alumbra 4 casillas. Si caen todos, la base cae y se empieza en otro claro; si no queda nadie, se pierde la campaña;
  - la campaña es nueva: la de antes se guarda aparte (`save.antes-0.7.json`);
  - la prueba del 18/10: plantas con la rueda, soldados ya jugados que se pueden mirar, rutas planeadas que se siguen solas, objetos encontrados al cinturón, La Lata que se va al acabar el tiempo, el aviso de 5 turnos, señalar, velocidad del turno enemigo, la hoja del soldado en combate, sonidos nuevos, casas por salas, plazas y alcantarillas con sentido, y las líneas y casillas nuevas («Degradado» con contorno de «Tiza», en «Hueso y óxido»);
  - quitado lo de la web y el móvil: solo la aplicación de escritorio.
- **Lo nuevo de la 0.6.0** (2026-10-18), el hito 1 de la Fase 12, el asedio ([FASE12.md](FASE12.md) §10): «El Bastión» en el menú de escaramuza, la base como mapa de combate (muralla, portones, barricadas, torres de vigía y la Sala de radio), cinco oleadas por un lado anunciado, muertos que tiran puertas y barricadas («Golpear»), brechas a la vista y el corazón de la base que no hay que perder. También el tablero de habilidades encuadrado en táctil, lo último de la Fase 11.
- **Lo nuevo de la 0.5.1** (2026-10-17): jugando solo, los soldados del compañero que no está obedecen al que juega, en combate y en la base (`commandsFor`); la interfaz escala con la pantalla, con el iPhone Air en horizontal (912 × 420) como referencia.
- **Lo nuevo de la 0.5.0** (2026-10-17), la Fase 11, móvil y navegador ([FASE11.md](FASE11.md)): el modo táctil (se detecta solo), el combate y la base con el dedo en horizontal, el ajuste gráfico «Móvil», y el juego servido en una web propia con contraseña (quitado el 18/10), con reconexión al volver de otra app y entrada desde la app con «Unirse por dirección».
- **Lo nuevo de la 0.4.5** (2026-10-17), notas #44–#52: «Salir de Bastión» (y el compañero sabe cuándo cierra el anfitrión, mensaje `closing`); disparos más ruidosos; en el turno enemigo, primero solo los que muerden y el resto a la vez; la cámara al turno (con interruptor); en gris sin el mando; la luz de noche explicada y sin quemar; cambiar de objetivo con un clic en el cuerpo; el coste en acciones siempre igual y con el arma; la columna del objetivo y una guía en el suelo.
- **Lo nuevo de la 0.4.4** (2026-10-17), nota #43: la puntería cuerpo a cuerpo junto a la de disparo en la ficha del Cuartel y en la de combate.
- **Lo nuevo de la 0.4.3** (2026-10-17), notas #40–#42: la ficha de cada arma (daño, crítico, puntería, cargador, acciones, ruido, blindaje y puntería por distancia; `weaponProfile` y `baseWeaponProfile` en aim.ts) en la Armería, el Taller y el Cuartel, con la comparación con lo que lleva cada soldado; el arma se elige también en la ficha del soldado («Mano» y «Cinto»); la noche sin deslumbrar (menos resplandor y la mitad de luz en farolas, bengalas y fuegos).
- **Lo nuevo de la 0.4.2** (2026-10-17): los zombis ven en cono ([PRUEBA-2026-10-16.md](PRUEBA-2026-10-16.md) #39). Cada muerto mira hacia un lado (`Unit.facing`) y ve solo en 120° hacia ahí, o a quien tiene al lado; se gira al andar, al morder y al oír ruido. El modelo mira hacia ahí, y el cono se ve al pasar el cursor por un zombi (con la escuadra oculta, su zona de detección).
- **Lo nuevo de la 0.4.1** (2026-10-17), la segunda tanda de notas del 16/10 ([PRUEBA-2026-10-16.md](PRUEBA-2026-10-16.md), #10–#38):
  - una barra que se llena en la misión sube su estadística allí mismo, con su aviso; las barras en segmentos, sin cifras, y lo que da cada una al final; el rol, un punto del color de la familia con más casillas; la veteranía, un rango cada 5 puntos gastados (+10 de voluntad, y la tercera acción en Veterano);
  - el tablero: comprar ya funciona con el trackpad; un círculo, sin cuadrícula ni normas en la ficha; iconos de los packs de interfaz de Synty; sonidos;
  - los zombis: el básico con 3 de vida, el 60 % del movimiento de un soldado y dos acciones (moverse y morder son acciones aparte), y cada uno solo muerde a quien ve; el Tajo, solo a quien está al lado, y gasta una acción;
  - el chaleco encontrado se pone en la misión; abandonar se pregunta al compañero; las esperas, de 90 a 30 s;
  - los mapas sin callejones sin salida, el autobús del tamaño que se ve (2 × 5), nieve hasta el deshielo del capítulo 3, los terrenos del Bastión sin parpadeo; «Cubrirse»; la pestaña Bastión dentro de «Campaña» (antes, el Diario).
- **Lo nuevo de la 0.4.0** (2026-10-16):
  - las notas de la prueba del 16/10 ([PRUEBA-2026-10-16.md](PRUEBA-2026-10-16.md)): por una ventana se ve como mucho a través de una; el turno enemigo, primero los que atacan y luego todos los demás a la vez y más deprisa; el zombi levantado lleva el aspecto del soldado; «Ver» lo que hace el compañero; el dinero dibujado como la caja del contador y el contador que no se quedaba en el número viejo;
  - la Fase 10 ([FASE10.md](FASE10.md)), con campaña nueva (la de la 0.3 se guarda aparte en `save.antes-0.4.json`): seis barras de experiencia (Disparo, Cuerpo a cuerpo, Lanzar, Curar, Investigar, Forma física) sin niveles; cada barra llena da un punto y un 5–10 % de su estadística; el tablero hexagonal de 169 celdas donde se gastan los puntos; la piedra y la vigilancia cuerpo a cuerpo; Lanzar, Vigilancia e Investigar con menú.
- **Lo nuevo de la 0.3.1** (2026-10-16), la PR de Ana ([Takeo7/Bastion#1](https://github.com/Takeo7/Bastion/pull/1)), integrada sobre la 0.3.0:
  - centros médicos rehechos (consultorio, enfermerías y hospitales de ciudad de tres plantas), con mobiliario por sala y abandono: cadáveres, sangre, suciedad, material tirado; todo decorado, sin cambiar cobertura ni paso;
  - escaleras interiores con peldaños de madera, separadas de las paredes y con el acceso asegurado;
  - el capítulo 1, «El consultorio de Brena», con nieve y la presentación aprobada (versión 05 del PDF); la escuadra alineada en la Cochera;
  - sus objetos van del 51 al 71 (del 45 al 50 son los de la fábrica de la 0.3.0).
- **Lo nuevo de la 0.3.0** (2026-10-16), «Se aprende haciendo» ([FASE9.md](FASE9.md)), con campaña nueva:
  - sin clases, aptitudes ni rangos: cuatro habilidades (Tiro, Cuerpo a cuerpo, Medicina, Maña) que suben con lo que cada soldado hace en las misiones, sin tope y con niveles largos; talentos de dos en dos en los niveles 2 y 4; la veteranía, de Novato a Superviviente, da vida, voluntad y la tercera acción;
  - el cinturón de dos huecos (sin granadas en él, no hay granadas); las armas sin niveles, con estado (gastada, rota) que se arregla con piezas, y mejoras (mira, culata, cargador ampliado, silenciador) que se encuentran o se hacen en el Taller;
  - una campaña de antes se guarda aparte (`save.antes-0.3.json`) y el vestíbulo lo dice;
  - la escaramuza: armas al azar, y con «Veteranos», habilidades de 2 a 4.
- **Lo nuevo de la 0.2.9** (2026-10-15), las notas de la prueba de ese día ([PRUEBA-2026-10-15.md](PRUEBA-2026-10-15.md)):
  - turnos de uno en uno: cada soldado gasta sus acciones (o termina) y pasa el mando al compañero, que mueve uno de los suyos; se puede ceder (`GameState.activeUnit`, evento `unitActive`). Evacuar se pide y se acepta desde la barra, sin ventana;
  - la horda llega por los bordes cada turno, el doble mientras suena la alarma de un sabotaje; los muertos, con 2 de vida más;
  - el mordido pierde 1 de vida por turno y no sube a La Lata sin la Cuarentena, la instalación nueva (`quarantine`);
  - el sabotaje calla un coche patrulla, una ambulancia o la alarma de una nave (`alarm`), con su modelo, sus luces y su sonido; el coche ocupa dos casillas (`sirenCar`);
  - la fábrica, rehecha (el bioma `facility`, `factory` en mapgen.ts): la entrada con las oficinas, la nave principal con muelles y patio de maniobra, el patio de contenedores, silos y aparcamiento, palés y bidones, y ninguna explanada sin cobertura;
  - el rescate nombra a quien está retenido y el mapa se llama como la operación; las medicinas, solo en tiendas y laboratorios;
  - Aldren pasa a llamarse Brena y Volkren, Velsa (Lore);
  - interfaz: repaso con los packs de Synty (marcos de acero «a», placas de listos, iconos del valle de una sola familia), el busto del soldado de verdad en el árbol, el Diario solo con «Entendido» y sin radio en misión;
  - el valle entero en la ventana, sin desenfoque ni zoom; los zombis ya no van y vuelven al andar; la granada mata a la vez.
- **Lo nuevo de la 0.2.8** (2026-10-15):
  - el valle es el mapa de Arturo (una imagen suya, `assets-src/valle/mapa-valle.png`, convertida a `public/maps/valle/mapa.jpg`): las zonas, Atalaya y los sitios de Lore están sobre ella, y la Marea y La Lata van por sus carreteras (`strategy/valleyRoads.ts`, generado con `packages/client/tools/valle/mapa.ts`). Las posiciones de REGIONS son las mismas que en el mapa;
  - en Linux, también un .deb (Ubuntu y Debian), que prepara el aislamiento de Chromium y AppArmor ([FIRMA.md](FIRMA.md)).
- **Lo nuevo de la 0.2.7** (2026-10-15):
  - el valle, rehecho de cero: un mapa ilustrado visto desde arriba (Arte), horneado en teselas (`public/maps/valle`) con la Marea, los marcadores y los rótulos encima; elegido por Arturo entre tres direcciones;
  - todos los zombis eran el mismo cuerpo (el obrero del chaleco): ahora salen de los diez comunes y los de corredores y acorazados;
  - los informes de las operaciones ya no nombran un sitio distinto del de la operación (Lore);
  - versión para Linux (AppImage y tar.gz);
  - las herramientas del tráiler, la portada y las capturas de la ficha ([TRAILER.md](TRAILER.md)): los vídeos e imágenes no van al repositorio.
- **Lo nuevo de la 0.2.6** (2026-10-14), las notas de la prueba de ese día ([PRUEBA-2026-10-14.md](PRUEBA-2026-10-14.md)):
  - evacuar es conjunto: desde La Lata (el autobús, aparcado junto a la zona) uno lo pide y el otro acepta; quien se queda fuera se pierde. Una misión se gana al irse, no al matar a todos (salvo Limpieza);
  - la fase 8 ([FASE8.md](FASE8.md)):
    - armas desde cero: todos salen con cuchillo y pistola, y las armas se encuentran, se fabrican o llegan con el nivel del Bastión; cualquiera lleva cualquiera;
    - registrar por habitaciones, una vez para toda la escuadra, y cajas especiales marcadas (armas, medicinas, munición, comida);
    - la mochila: soltar, coger, dar, usar medicinas y equipar armas en la misión;
  - el triple de zombis, y la mitad de las cajas, barricadas, papeleras y jardineras sueltas;
  - dos escenarios nuevos con los packs de Woodland y de la nave industrial (`GameState.site`): el área de servicio en la carretera del bosque y el polígono industrial. Salen solos en la campaña y se eligen en la escaramuza;
  - por las ventanas se ve a través de dos como mucho;
  - una propuesta o una petición de evacuar sin respuesta se acepta sola a los 90 s;
  - el árbol de habilidades nuevo, más orgánico;
  - sin días de viaje a las misiones; más acciones al subir mucho de rango; reclutas algo distintos;
  - hordas horneadas: 45 o 90 zombis cuestan lo mismo que 15.
- **Lo nuevo de la 0.2.5:** reacciones a los golpes según de dónde vienen (Bow Combat y Sword Combat, de cintura para arriba) y puertas que giran sobre la bisagra al abrirse y cerrarse (`game/doors.ts`).
- **Lo nuevo de la 0.2.4** (2026-10-14), lo que pidió Arturo: «el turno de la IA va muy rápido, corren muy rápido, no se ve bien la animación de andar ni correr, y al morir se desploman como un tronco»:
  - pasos sin patinar y por plantilla (`game/gaits.ts`): el soldado trota en el azul (0,50 s/casilla) y esprinta en el amarillo (0,24); el zombi anda arrastrándose (1,02) con el andar de Goblin Locomotion;
  - muertes de verdad (Bow Combat y Sword Combat), según de dónde viene el golpe y con sitio para caer; posturas de reposo (Idles); Tajo con arco y mordisco que agarra; escaleras que se suben andando;
  - el turno enemigo se ve entero ([UX-RITMO.md](UX-RITMO.md)): sin la aceleración por cola, la cámara se asienta en cada enemigo, el resultado se lee, y los muertos de un grupo que solo andan lo hacen juntos (y la IA los mueve antes que a los que muerden);
  - «Velocidad del combate» en Ajustes (Lenta, Normal, Rápida), y la barra espaciadora acelera el turno enemigo.
- **Lo nuevo de la 0.2.3** (2026-10-14):
  - los zombis primero ([ZOMBIS-PRIMERO.md](ZOMBIS-PRIMERO.md)): las misiones normales son de los muertos; los humanos, solo en las suyas;
  - la fase 7 ([FASE7.md](FASE7.md)): saltar por ventanas altas, escaleras que solo se dejan por el rellano, munición contada (3 cargadores), paredes y suelos que rompen las granadas.
- **Lo nuevo de la 0.2.2** (2026-10-14):
  - un velo de carga al entrar en combate, y la interfaz cabe en ventanas de 1024 px;
  - el estado viaja comprimido por la red (de ~110 KB a ~9 KB por misión) y el panel «Comandantes» solo enseña direcciones útiles;
  - el valle con relieve, campos, pueblos y carreteras; en combate, luz más cálida, suciedad y plazas con árboles y escombros;
  - los textos que quedaban del globo y de XCOM, fuera; nombres que chocaban, cambiados.
- **Rendimiento:** la telemetría de cada navegador va a `data/red.log` (o `%APPDATA%\Bastion\red.log` en la aplicación).

## 4. Decisiones importantes (y por qué)

- **Arte:**
  - **Solo Synty POLYGON** (Arturo tiene la suscripción), Kenney CC0 o lo nuestro. Nada de vóxel ni de otros packs.
  - El aspecto objetivo son las capturas promocionales de Synty.
- **Rendimiento:** el PC de referencia es Windows con una RTX 3090; objetivo ≥240 fps en «alto» (≤4,2 ms por fotograma). El Mac es el mínimo («medio» ≥30). El aspecto no se recorta por el Mac: para eso están los ajustes.
- **Plataforma:** Three.js en la aplicación de escritorio de Electron (Tauri no: en Mac usa WebKit). La versión web y móvil se abandonó el 18/10 (Arturo): solo se desarrolla y se publica la de escritorio. WebGPU, más adelante.
- **Tema y tono** ([LORE-PROPUESTA-2026-10-13.md](LORE-PROPUESTA-2026-10-13.md)):
  - supervivencia zombi clásica;
  - premisa: Atalaya, una vieja estación militar en la sierra de Brena, con supervivientes de todas partes;
  - tono adulto y seco, sin explicar de más, con nombres internacionales.
- **Nombres:**
  - pestañas «Campaña», «El valle», «Armería», «Soldados» (antes «Cuartel»), «Construir» y «Escuadra» (en el claro, junto a La Lata);
  - el tiempo pasa con las misiones (desde la 0.7.0); la amenaza se llama «Infestación» (y «la Marea» desde el nivel 4).
- **Los zombis primero** (14/10, [ZOMBIS-PRIMERO.md](ZOMBIS-PRIMERO.md)): las misiones normales son de los muertos; los humanos solo en Rescate y en los capítulos y encargos que son suyos.
- **Reglas** (de las pruebas de juego del 12 y del 13/10):
  - disparar gasta una acción;
  - la clase llega en Capitán;
  - no hay refuerzos humanos: los zombis solo entran por los bordes;
  - abrir puertas es gratis;
  - la campaña empieza con una misión;
  - (14/10) evacuar es de los dos y se gana al irse; la clase no trae arma; registrar es por habitaciones.
- **Progresión** (Fase 10, [FASE10.md](FASE10.md)): sin clases; se aprende haciendo, en seis barras sin niveles. Los puntos se gastan en el tablero hexagonal; lo comprado queda fijo (no hay reentrenar). El Taller de oficios hace que cada misión llene las barras un 20 % más por nivel.
- **Equilibrio:**
  - la Marea cae por zonas a 4/6/9/10/11 de infestación;
  - empieza con 16 raciones;
  - disparar desde más alto da +20 de puntería; la IA de los vivos valora la altura con +6 por planta al elegir sitio;
  - una escalera es un tramo de 3 casillas (2 en casas, tiendas y hospitales pequeños; [MAPAS.md](MAPAS.md)): solo se entra por el pie y nadie se para en él. El tramo de 3 cuesta como 4 casillas y el de 2 como 3; la IA cuenta 4 de rodeo por planta (3 en los mapas de antes, con escaleras de una casilla);
  - los compañeros se bloquean y los muebles grandes ocupan su casilla (Arturo, 19/10): nadie atraviesa a nadie ni a nada.
- **Interfaz:** Mixta, votada por Arturo. La cuadrícula del suelo, opción A (solo contornos). Las líneas y casillas, «Degradado» con el contorno de «Tiza», en «Hueso y óxido» (18/10).
- **Cómo se juega:** Arturo y Ana juegan con la aplicación de escritorio; la web queda para probar (por eso Safari no se prueba).
- **Sesiones de Claude:** Gameplay (coordina), Arte, Lore, UI y Networking, todas con Remote Control para seguirlas desde claude.ai/code ([EQUIPO.md](EQUIPO.md)).

## 5. Pendiente

- **Medir la 0.9.2 en el PC con la 3090** (Arturo): las líneas «combate» de red.log, en alto, frente a las de antes, y si el primer disparo ya no congela.

- **De Arturo y Ana:**
  - probar la versión actual en el PC con la RTX 3090, con la guía [PRUEBA-PC-3090.md](PRUEBA-PC-3090.md) (escrita para la 0.4.0), y mandar el `red.log` del anfitrión;
  - decidir lo de la revisión del 19/10 ([REVISION-2026-10-19.md](REVISION-2026-10-19.md), «Para decidir»): el repositorio público frente a la norma de que sea privado, si la torre, el alambre y las trampas se rompen, y quitar F8 si «Hueso y óxido» se queda;
  - escuchar los sonidos nuevos (`sonido.html`).
- **Probar en partida** la Fase 10: si las barras se llenan a buen ritmo, si el tablero da elecciones de verdad y si las armas se gastan lo justo. Lo más fácil de ajustar, en `skills.ts`: `BAR_FIRST`, `BAR_STEP`, `BAR_GROWTH`, `DEED_XP`, `BASE_AIM`; en `board.ts`, las cifras de cada celda; en `content.ts`: `STONE_DAMAGE`, `wearOf`, `WORN_BELOW` y `REPAIR_PER_PART`. Y lo de antes: `podPlan` en `setup.ts`, `PISTOL_CLIP` y los precios del Taller (`CRAFTS`).
- **Los mapas nuevos** ([MAPAS.md](MAPAS.md)): jugarlos y ver si el recorrido y los turnos van bien. En la base de 56 los muertos tardan algo más en llegar: revisar `SIEGE.hold` (11) y las torretas. Medir en el PC de la 3090 un mapa de 72 en «alto».
- **Fase 12, el asedio** ([FASE12.md](FASE12.md)): hecha. El hito 1 salió en la 0.6.0 y la base nueva en la campaña, en la 0.7.0 (§11). Falta ajustar las cifras jugando: el tamaño de las oleadas (`SIEGE_FIRST`, `SIEGE_GROWTH` en campaign.ts), el precio y la munición de la torreta y de las trampas (`PIECES` en base.ts).
- **Para revisar más adelante (Arturo, 18/10): «La Lata», la base móvil.** No hay base fija: el Bastión es el autobús con remolques, y la campaña es un viaje por el valle antes de que llegue la Marea. Se elige ruta y se carga el autobús, que tiene poco sitio (gente, combustible, comida, munición), y las instalaciones pasan a ser remolques. Fue la opción 2 al repensar la base; la elegida fue la 1, el asedio ([FASE12.md](FASE12.md)).
- **El modo en tiempo real (Arturo, 20/10)** ([TIEMPO-REAL.md](TIEMPO-REAL.md)): los mismos controles de clic en casillas, todo a la vez, con pausa compartida. Hecho el paso 1, el prototipo: escaramuza en solitario, «Ritmo: Tiempo real (prueba)». Falta que Arturo juzgue el ritmo; si convence, los pasos 2 a 5 (todas las acciones con su tiempo, a dos, el resto de la misión y la campaña).
- **La versión móvil y web** ([FASE11.md](FASE11.md)): abandonada por Arturo el 18/10, y su código quitado para aligerar el juego: modo táctil, ajuste «Móvil», paquete web, contraseña y manifest. Solo la aplicación de escritorio. Se quedan la reconexión, la espera a quien se ausenta, recuperar el puesto por el nombre y jugar con los soldados del compañero ausente.
- **El tráiler:** aparcado por Arturo (15/10). El último borrador, la portada y las 10 capturas de la ficha están en `.dev-snapshots/trailer/` (no se suben); las herramientas siguen en `packages/client/tools/trailer/` ([TRAILER.md](TRAILER.md)) por si se retoma.
- **Escenarios:** la cárcel y el centro comercial esperan los packs «POLYGON – Police Station» y «POLYGON – Shops», que tienen los atlas de sus piezas. Para después: la cafetería con aspecto propio y el portón grande de las naves (un hueco de 5 casillas).
- **Para más adelante:**
  - firmar la aplicación (cuenta de desarrollador de Apple; los pasos, en [FIRMA.md](FIRMA.md)) y Steam;
- **Textos pendientes de revisar por Lore:** [TEXTOS-PENDIENTES.md](TEXTOS-PENDIENTES.md).

## 6. Mapa de los documentos

| Documento | Qué tiene |
|---|---|
| [CLAUDE.md](../CLAUDE.md) | Normas para cualquier sesión de Claude |
| [EQUIPO.md](EQUIPO.md) | Roles, archivos de cada uno, puertos de prueba, normas y lo que está en marcha |
| [DESIGN.md](DESIGN.md) | Arquitectura, mecánicas, hoja de ruta y limitaciones conocidas |
| [LORE.md](LORE.md) | El mundo, los zombis, las zonas y los recursos |
| [ARTE.md](ARTE.md) | Dirección de arte y la tubería de Synty |
| `FASE2.md`…[FASE10.md](FASE10.md), [ALTURA-Y-NOCHE.md](ALTURA-Y-NOCHE.md), [ZOMBIS-PRIMERO.md](ZOMBIS-PRIMERO.md) | Especificación de cada fase y del reparto de enemigos |
| [TRAILER.md](TRAILER.md) | El tráiler, la portada y las capturas de la ficha: guion, quién hace qué y cómo se rehace |
| [PRUEBA-2026-10-12.md](PRUEBA-2026-10-12.md), [PRUEBA-2026-10-13.md](PRUEBA-2026-10-13.md), [PRUEBA-2026-10-14.md](PRUEBA-2026-10-14.md), [PRUEBA-2026-10-16.md](PRUEBA-2026-10-16.md), [PRUEBA-2026-10-18.md](PRUEBA-2026-10-18.md), [PRUEBA-2026-10-19.md](PRUEBA-2026-10-19.md) | Notas de las pruebas de juego y qué se hizo con cada una |
| [ux/FUENTES.md](ux/FUENTES.md), [UX-CARENCIAS.md](UX-CARENCIAS.md) | Licencias de tipografías y sonidos; huecos de los packs de interfaz |
| [MAPAS.md](MAPAS.md) | Los mapas con proporciones reales: la medición, las medidas objetivo, el plan por fases y lo decidido |
| [TIEMPO-REAL.md](TIEMPO-REAL.md) | El modo en tiempo real con clic en casillas: lo decidido, cómo funciona, qué se aprovecha, pasos, riesgos y el paso 1 en el código (§6b) |
| [REVISION-2026-10-19.md](REVISION-2026-10-19.md) | La revisión del código del 19/10: los fallos arreglados, lo quitado y lo que queda por decidir |
| [MAPAS.md](MAPAS.md) | Los mapas: medidas, el plano de los pueblos, el contorno, la base de 56 y el plan por fases (hecho en la 0.8.0) |
| [PRUEBA-PC-3090.md](PRUEBA-PC-3090.md), [FIRMA.md](FIRMA.md) | La guía de la prueba en el PC de referencia; cómo se firma la aplicación y qué cambiar con la cuenta de Apple |
| [FASE11.md](FASE11.md) | Abandonada y quitada el 18/10. Lo que fue la Fase 11, móvil y navegador (la guía de la web, WEB.md, está en el historial de Git) |
