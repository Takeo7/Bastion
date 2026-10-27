# Prueba de la campaña en Unity (hito 2)

Guía para Arturo, de Gameplay (26/10). Se hace cuando estén las dos cosas:
- Arte ha acabado la vida del Asentamiento y el modo de construir (fases 5 y 6);
- UI ha acabado lo que faltaba del combate respecto a la 0.10.1 y se nota en una misión normal: el clic derecho, la cámara al soldado, el sello y la tarjeta de llegada, la pantalla de carga, la mochila, los avisos de enemigo a la vista y el desgaste de las armas.

El objetivo es jugar una campaña entre dos apps de Unity, de la base a una misión y vuelta, y apuntar lo que vaya distinto de la 0.10.1. La campaña de la app de Electron no se trae: en Unity se empieza una nueva.

Cómo abrir las dos apps (doble clic, la segunda con `BASTION_DATOS`, la cuarentena de macOS, Linux) está en [PRUEBA-UNITY.md](PRUEBA-UNITY.md) §1. Es igual.

## La app

`unity/Bastion/Builds/Mac/Bastion.app`, de las 16:57 del 26/10 (commit 9ff0b7e). Para Ana, `Builds/Bastion-Mac.zip`.

Ya lleva todo lo del hito 2 y además:
- el sonido de la 0.10.1: combate, base, Mapa, radio, clics y pasos (Ajustes → Sonido; M lo silencia en combate);
- los muertos especiales con su cuerpo (el Corredor, el Acorazado, el Chillón más alto, el Hinchado), los 17 cuerpos del zombi común, encorvados al andar como en la 0.10.1;
- el destello rojo al recibir un golpe, y cada uno cae alejándose de quien le dio;
- «Ajuste auto» en Ajustes: si el combate va lento, baja los gráficos y lo avisa.

La primera vez que se abre tarda unos 4 s más en tener todos los sonidos; la pantalla de inicio funciona mientras.

## 1. Empezar

1. En las dos apps, un nombre distinto en «TU NOMBRE». Quien aloja pulsa «ALOJAR LA PARTIDA»; el otro entra por «PARTIDAS EN TU RED» o «UNIRSE POR DIRECCIÓN».
2. En la sala: los dos en «Comandantes» como «En línea».
3. «Nueva campaña» → «Empezar».
4. Sale la transmisión de llegada: «Seguir», clic o Intro.

## 2. La base

Mirar cada cosa en las dos ventanas. Lo que hace uno con el otro delante va a votación: le sale «Propuesta» con «Aceptar» y «Rechazar».

**El marco**
- Arriba: el día, el nivel y la Marea con su ayuda al pasar el ratón.
- Las pestañas: Mapa, Armería, Soldados, Asentamiento (y Escuadra con una misión elegida).
- El diario y la radio.

**Mapa**
- Pulsar una zona abre su hoja con sus misiones («Misiones en la zona»).
- Encargos, Rumores y Red de enclaves.
- Elegir una misión y «Desplegar»: el otro vota.
- «Cancelar misión» también se vota.

**Soldados**
- La hoja de cada uno: armas, los dos huecos de accesorios, el rango.
- Personalizar con el armario.
- El tablero de habilidades: gastar un punto.

**Armería**
- Comprar un accesorio, arreglar un arma, ponerle una mejora.

**Asentamiento**
- El claro en 3D: la caravana, La Lata, la hoguera, los huertos, el pinar.
- La gente: sentada junto al fuego, apoyada, andando, bebiendo o comiendo.
- Construir:
  - elegir una pieza en el panel y ponerla con un clic; R la gira;
  - los muros, los sacos y el alambre se ponen en línea arrastrando;
  - con el otro delante, se vota;
  - lo que no se puede poner dice por qué junto al ratón.
- La nieve y el viento, y las luces de noche si toca de noche.
- «Andar por la base»: WASD y el ratón, Mayús para correr, Esc para salir. Se choca con las piezas y con La Lata.

## 3. La misión

1. Escuadra: elegir los soldados. Cada uno pulsa «Lanzar misión»; sale «A punto» hasta que el otro también lo pulsa.
2. El combate es el del hito 1 (PRUEBA-UNITY.md §3). Además:
   - los soldados llevan lo que se les puso en la base: el arma de la Armería en la mano, la pistola en su funda del muslo, el cuchillo o el machete en el cinto; recargar;
   - la mochila al pie del panel del soldado: usar, curar a otro, dar, coger lo del suelo, cambiar con la mochila llena, registrar la de un caído;
   - H o clic en el nombre: la hoja del soldado; clic derecho sobre un enemigo: su ficha;
   - el suelo con sus piedrecitas, papeles y matas, y las calles con sus señales, postes y bocas de riego;
   - las sombras también con la cámara muy lejos;
   - Esc → Ajustes: la velocidad de los soldados y el tamaño de la interfaz;
   - la evacuación: el autobús llega, se sube y se va.
3. Al acabar, el Parte: «Misión cumplida» o «Misión fracasada», heridos, caídos, experiencia y botín. «Continuar» vuelve a la base.
4. De vuelta en la base:
   - los heridos, en la enfermería;
   - el día avanza;
   - acontecimientos y transmisiones nuevas.

## 4. Guardar y volver

1. Esc → «Volver al menú principal». Sale «La campaña queda guardada» y el otro vuelve también al menú.
2. En la sala, «Continuar campaña» con su resumen («1 salida · Nivel 1 · Infestación 3/12»; cuando ya se conoce la Marea, «La Marea, a N km»).
3. Cerrar las dos apps sin pasar por el menú y abrirlas otra vez: la sala vuelve directamente a donde estabais, a la base o a la misión, como en la 0.10.1. «Continuar campaña» solo sale si antes se volvió al menú principal.
4. Si se sale en medio de una misión, «Continuar campaña» vuelve a ella.

## 5. Si sale el asedio

Si llega el asedio, en Escuadra el botón es «Defender el Bastión», y la misión se juega en la base. Antes, cada soldado se pone en su puesto del claro (sale de pie en él, con el arma). En el combate del asedio:
- la rosa arriba a la derecha marca en rojo los lados de la próxima oleada (si hay una Sala de radio que funciona);
- las puertas y barricadas dañadas llevan su barra de vida, y al pasar el ratón «Barricada: aguanta 3 de 9»;
- las barricadas de sacos se gastan con cada golpe; al ceder, su haz naranja y una flecha en el borde («Brecha · sur · 7 casillas»).

## 6. Qué apuntar

- Lo que se vea mal o vaya distinto de la 0.10.1, con el momento y en qué ventana. Una captura ayuda (Cmd+Mayús+4).
- Lo que se atasque: un botón que no responde o una votación que no llega al otro.
- Lo que suene mal, fuera de sitio, demasiado alto o bajo, o que falte, comparado con la 0.10.1.
- El cursor del juego (flecha, mano sobre lo que se pulsa, mira al apuntar): si en la pantalla del Mac se ve pequeño.
- Al acabar, los `red.log` de las dos apps (las rutas, en PRUEBA-UNITY.md §5). Cada app manda una línea de rendimiento cada 10 s, también en la base.
