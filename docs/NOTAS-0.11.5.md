# Notas de la 0.11.5

Texto de Lore para la 0.11.5, con las notas de Arturo del 1/11 sobre la 0.11.3 y la 0.11.4 ([PRUEBA-2026-11-01-ARTURO.md](PRUEBA-2026-11-01-ARTURO.md), de la #1 a la #11) y lo que dejó la prueba de la 0.11.4 con la app ([UNITY.md](UNITY.md), la línea de las releases). Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.11.5: sin pistola al empezar, cócteles molotov y zombis cada turno en el asedio

Versión 0.11.5 (commit [commit]). Las notas de Arturo del 1/11, con la 0.11.3 y la 0.11.4.

## Novedades de la 0.11.5

### Se empieza con un cuchillo
Todo esto es para las campañas nuevas. Las de antes siguen como estaban (ver «Partidas guardadas»).
- **Una campaña nueva empieza sin pistolas, sin granadas y sin botiquín:** cada soldado lleva su cuchillo. Sin arma de fuego no se puede disparar, vigilar ni recargar.
- **La pistola es un arma de la Armería,** como las demás. Se encuentra registrando o se hace en el Taller.
- **Con un arma en la mano, la pistola va al costado.** Sin otra arma, es la suya. Si el soldado cae, se pierde.
- **En la primera misión, la insulina de Metsovo,** hay un coche de policía cerca de La Lata con dos pistolas: una delante y otra en el maletero. La ficha de la misión lo dice.
- **Subir de nivel el Bastión ya no trae armas a la Armería:** solo se encuentran o se hacen en el Taller.
- **La ficha del soldado tiene un hueco nuevo, «Costado»,** para la pistola, junto a «Mano» y «Cinto». La Armería dice quién lleva cada pistola.
- **En combate, sin arma de fuego,** la ficha no enseña munición ni puntería con armas, y el arma es el cuchillo o el machete.

### El cóctel molotov
- **Va antes que las granadas:** se compra desde el nivel 1 del Bastión, dos por 10 suministros (menos con el Taller), y se encuentra en casas, tiendas y maleteros.
- **Se lanza como una granada,** desde «Lanzar»: arde un cuadrado de 3×3 casillas durante 2 turnos.
- **Quita 1 de salud** a quien esté dentro al caer, a quien entre y a quien acabe el turno dentro, también a los tuyos. Como el fuego del mapa: el blindaje no protege.
- **Solo se lanza a la calle o a una planta baja.** Sobre un piso alto, la zona sale en gris.
- **Al apuntar,** las 9 casillas salen en rojo anaranjado, con una llama en cada una. Al caer, un fogonazo naranja con chispas, y el suelo arde. De noche alumbra como el fuego del mapa.
- **Se oye,** y si la escuadra iba oculta, la descubre.

### Las granadas
- **Se compran desde el nivel 3 del Bastión** (antes, desde el 1).
- **En las misiones salen pocas, y solo en almacenes.**

### El asedio
- **Entra un grupo de zombis al acabar cada turno enemigo, del 1 al 8,** sin turnos vacíos. Antes eran cinco oleadas (turnos 1, 2, 3, 5 y 6) y nada después.
- **Trae los mismos especiales y algo más de zombis en total.**
- **Acaba cuando ha entrado el último grupo y han caído todos.**
- **El primer asedio,** justo después de la primera misión, son 8 zombis comunes, uno por turno y sin especiales.

### El tratamiento de un mordisco
- **Al proponerlo, le llega al compañero:** sale en la placa de las propuestas, «Ana propone tratar el mordisco de Hiro Sato», con el soldado, las medicinas que cuesta y lo que hace. Antes solo salía en la ficha del soldado, y el compañero no lo veía.
- **Lleva el reloj de la sala,** como las demás propuestas. Aceptar y Rechazar votan, y quien lo propuso puede retirarlo.

### En la base
- **Al cambiar de soldado en la Escuadra,** el nombre de debajo de cada figura cambia con ella. Antes se quedaba el del primero.
- **Al pasar el ratón por un soldado de la lista,** sale al lado su ficha en corto: rango, salud, puntería, movimiento, arma, cinturón, habilidades y heridas o mordisco. También en la lista del compañero y en la de los defensores del asedio.

### En combate
- **La línea de la zona de movimiento va seguida:** rodea por fuera los coches, las cajas y las personas del borde, sin cortes.
- **Comer es más corto:** desde que cae el soldado hasta la siguiente acción pasan unos 2,4 s (antes, unos 4,2).
- **La cámara al hombro ya no se mete dentro de La Lata** ni mira a través de ella.
- **En el plano al hombro ya no tapan media pantalla** las marcas de lo que soltó un soldado al caer. Vuelven con la cámara.
- **Los zombis andan todos a la vez al empezar el turno enemigo,** menos unos pocos que van por orden: los de un grupo recién descubierto, los que llegan ese turno, el que anda después de atacar y los vivos que disparan. Ahora el registro de la app (Player.log) apunta en cada turno enemigo quién anda aparte y por qué.

### Los animales
- **Los perros, el lobo y el zorro ya se mueven:** antes se quedaban quietos. Andan, corren, huelen, mueven la cola, se sacuden, bostezan, escarban, aúllan, y a veces se sientan o se tumban. Al asustarse, se levantan y corren.
- **Los perros, solo en pueblos y ciudades.**
- **En el bosque y en el campo,** el lobo y el zorro, y ahora también ciervos, jabalíes y conejos.
- **En la base, uno o dos, y solo en el pinar.** Antes había un perro, dos en el claro y seis cuervos en los tejados.

### Arreglos
- **En la niebla, las paredes de chapa de las naves** salían con rayas claras que seguían sus ondas.
- **En red.log, la última línea del menú** dice también el límite de fotogramas del combate.

## Partidas guardadas

- **La campaña de la 0.11.4 sigue** en la 0.11.5, con sus reglas de armas: cada soldado con su pistola, y armas en la Armería al subir de nivel, como antes.
- **Lo demás también cambia en ella:** el cóctel molotov, las granadas desde el nivel 3, el asedio, el tratamiento y lo de la base y el combate.
- **Un asedio guardado a medias** sigue con sus oleadas de antes.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.11.5-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.11.5-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Las reglas nuevas de las armas, solo en una campaña nueva:** las campañas empezadas antes siguen con su pistola y con las armas al subir de nivel. Para jugar empezando con un cuchillo, hay que empezar una campaña.
- **Los dos jugadores, con la 0.11.5:** con la 0.11.4 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.11.4.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
