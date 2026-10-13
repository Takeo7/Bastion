# Bastión — lore

**Estado:** diseño del 2026-10-12. El usuario ha elegido zombis clásicos y un mundo nuevo, sin el Coro.

Decisiones tomadas:
- un lugar inventado;
- suministros, comida y medicinas;
- la magia pasa a ser el Oficio;
- «zombis» y «la Pandemia».

Gameplay ha revisado el coste en reglas (ver «Orden de trabajo»). La fase 1 (nueva piel) empezó el 2026-10-12. El lore anterior, el del Coro, está en [archive/LORE-coro.md](archive/LORE-coro.md).

**Cómo está planteado:** cambiar de piel, no de motor. Casi todos los sistemas que ya funcionan se quedan y cambian de nombre y de historia:
- capítulos, encargos y rumores;
- radio y Expediente;
- la base en superficie;
- el árbol de habilidades y sus ramas;
- la doble llave.

Lo nuevo de verdad es la supervivencia: saquear, el ruido, la infección y la escasez.

## La idea en una frase

Hace dos inviernos llegó la Pandemia: quien se contagia muere en un día y se levanta zombi. Dos grupos de supervivientes se han juntado en una vieja estación de montaña, Atalaya, y bajan a los pueblos del valle a por comida, medicinas y gente viva. Cada disparo atrae a más.

## Tono

- **Supervivencia seria, con calor humano:** más *The Last of Us* y *This War of Mine* que gore de serie B.
- **El miedo:** la escasez y el ruido, más que el monstruo.
- **El enemigo más peligroso** suele ser otra persona.
- **El humor:** vive en las notas a boli entre los dos líderes, como hasta ahora.
- **Todo es inventado:** el país, el valle, las ciudades. Ningún sitio real.

## Cronología

| Cuándo | Qué pasó |
|---|---|
| Otoño, año 0 | Primeros casos en el Instituto Galena, un laboratorio de vacunas a las afueras de Puerto Galena. Los telediarios hablan de «la Pandemia». |
| Tres semanas después | Los muertos se levantan en las ciudades. Ya nadie dice «infectados»: dicen **zombis**. |
| Invierno, año 0 | Caen Puerto Galena y Ciudad Varela. El ejército se repliega y desaparece. La radio se queda muda. |
| Año 1 | Los vivos se esconden en los pueblos de la sierra. Aparecen los Cuervos, bandas que viven de saquear a otros. |
| Primavera, año 2 · Día 1 de la campaña | Dos grupos de supervivientes llegan a la vez a la Estación Atalaya. Se quedan los dos, y se mandan a medias. |

## El mundo: el valle de Valdesombra

Es una comarca inventada entre una sierra alta y una llanura, con dos ciudades muertas. La geoesfera pasa a ser un **mapa del valle** en vóxel, con ocho zonas en lugar de las ocho regiones.

| Zona | Id | Qué hay | Aliado (enclave) |
|---|---|---|---|
| Altomonte | `weu` | Los pueblos de la sierra, junto a Atalaya (la de partida) | El Refugio de Altomonte |
| Ciudad Varela | `afr` | La primera ciudad caída, llena de zombis | Los del Hospital |
| El Pantano Gris | `eas` | Un embalse y una central eléctrica muerta | La Red Callada |
| Valdelobos | `eeu` | Valles cerrados, aldeas que aún resisten | Los Pastores |
| Puerto Galena | `me` | La ciudad muerta y el Instituto Galena | Los del Instituto |
| Los Llanos de Ceniza | `na` | La llanura por donde sube la Marea | Los Camioneros |
| Carboneda | `sa` | Minas y carreteras cortadas | El Sindicato de la Mina |
| La Vega | `oce` | Granjas, bodegas, el río y comida | Las Bodegas |

Los ids de región del código (`weu`, `afr`…) se quedan; solo cambian los nombres.

Contactar una zona es encontrar a sus supervivientes por radio. En vez de un pago mensual, cada enclave aliado manda suministros mientras siga vivo.

## Los zombis (y los vivos que dan miedo)

Los ids del código se mantienen; cambian el nombre, el aspecto y, donde haga falta, el comportamiento.

| Id actual | Nombre | Qué es |
|---|---|---|
| `zombie` | **Zombi** | El muerto corriente: lento y torpe. Va hacia lo que ve y lo que oye. En grupo es letal. |
| `xenoid` | **Corredor** | Un zombi reciente, todavía ágil. Rápido y ve de lejos. |
| `mec` | **Acorazado** | Fue policía o soldado. El chaleco y el casco le dan blindaje. |
| `sectoid` | **Chillón** | Un zombi que grita. El grito llama a todos los zombis cercanos y mete el miedo en el cuerpo (pánico o desorientación). Primero el Chillón. |
| *(nuevo)* | **Hinchado** | Revienta al morir o al llegar a ti y deja una nube que contagia. Mejor de lejos. |
| `trooper` | **Saqueador** | Un Cuervo con escopeta o fusil de caza. Se cubre y dispara como una persona. |
| `officer` | **Cabecilla** | Manda un grupo de Cuervos. Mejor arma, más aguante. |
| `lancer` | **Cazador** | Un Cuervo con red y porra: te inmoviliza para llevarte vivo. |
| `relay` | **Sirena** | El altavoz con el que los Cuervos atraen hordas contra sus enemigos. Objetivo de sabotaje. |
| `vip` | **Superviviente** | Alguien a quien rescatar. |

**La Congregación** (enemigo de los actos II y III). Es una secta que cree que los zombis están salvados. Los guían con campanas y con su líder por radio, la **Voz**, y son ellos los que empujan la Marea hacia los vivos.

## El Bastión

- **La Estación Atalaya:** un puesto de defensa civil en lo alto de la sierra de Altomonte, con refugio subterráneo, muros de hormigón y una presa al lado. La nieve, la altura y una sola carretera la mantienen viva.
- **La base en superficie** encaja tal cual: el refugio en el centro y alrededor barracones, huerto, barricadas y pasarelas. Se despejan rocas, bosque y ruinas, y las ruinas dan suministros.
- **La Mula:** un autobús de línea blindado con chapa. Lleva a la escuadra y vuelve cargado.
- **La doble llave se queda, con otra historia.** Dos grupos que llegaron a la vez y no se fían del todo: lo importante se decide a medias. El año pasado, un líder solo vendió su refugio a los Cuervos a cambio de su vida.

## Personajes

Se mantienen las voces de la radio que ya existen y cambian sus vidas.

| Personaje | Papel nuevo |
|---|---|
| **Inés Albarrán, «Morse»** | Radioaficionada de toda la vida. Mantiene la radio de Atalaya y es la primera en oír a otros vivos. Firma los informes. |
| **Teodoro Vidal, «el Relojero»** | Mecánico jubilado. Blindó La Mula, arregla armas y enseña oficios. Construyó el Grillo. Secreto: trabajó de vigilante en el Instituto Galena la noche en que todo empezó. |
| **Dra. Amara Nwosu** | Viróloga del Instituto Galena. Huyó con sus cuadernos y cree que hay una vacuna. |
| **Simón Ferreira** | Desertor de los Cuervos. Sabe dónde guardan lo que roban y a quién. |
| **Ruth Halvorsen** | Portavoz del Refugio de Altomonte, el enclave más grande. Socia exigente. |
| **Celia Arranz, «la Voz»** | Presentadora de radio antes de la Pandemia. Ahora guía a la Congregación y a la Marea con su emisora. |

## La historia

Mantiene la estructura actual: siete capítulos, uno por nivel del Bastión, que no caducan, y una misión final con un dilema de dos llaves.

| Nivel | Capítulo | Tipo · zona | Qué pasa |
|---|---|---|---|
| 1 | **Primer invierno** | Recuperación · Altomonte | Las medicinas del consultorio del pueblo, antes de que caduquen. |
| 2 | **La doctora** | Rescate · Ciudad Varela | Sacar a la Dra. Nwosu de un hospital lleno de zombis. |
| 3 | **La sirena** | Sabotaje · Los Llanos de Ceniza | Callar la sirena con la que los Cuervos atraen hordas. |
| 4 | **La torre del Pico Gris** | Pirateo (arreglar la radio) · El Pantano Gris | Volver a poner en marcha el repetidor: oír a los demás enclaves. |
| 5 | **Los inmunes** | Rescate · Valdelobos | Una niña a la que la Pandemia no mata. Simón sabe dónde la tienen los Cuervos. |
| 6 | **Los cuadernos de Galena** | Recuperación · Puerto Galena | Lo que pasó de verdad en el laboratorio. Teo estaba allí. |
| 7 | **La Voz** | Rescate · Los Llanos de Ceniza | Asaltar la emisora de la Congregación y sacar a la Voz con vida: solo ella sabe parar la Marea. |
| 8 | **La Marea** | Final | La horda llega a la sierra. Atalaya resiste, y la vacuna está lista. |

**El reloj: la Marea.**
- Es una horda inmensa que sube desde la llanura con el deshielo, y sustituye a la Partitura.
- En vez de compases cuenta kilómetros: «La Marea, a 9 km». Cuando llega a 0, Atalaya cae.
- Los **Nidos**, sitios donde los zombis se juntan (antes, los Órganos), la alimentan. Quemarlos la hace retroceder.

**El final** (dos llaves). Con la Marea parada y la vacuna en la mano:
- **Darla a todos:** emitirla por radio para todos los enclaves, también los Cuervos. Nadie se queda fuera, aunque algunos la usarán contra otros.
- **Guardarla para la Red:** solo para quienes os ayudaron. El valle sobrevive, y fuera de él…

## Misiones y supervivencia

Las reglas son de Gameplay. Esto es lo que pide el mundo.

- **Saquear** (tipo de misión nuevo, el corazón del juego):
  - Hay que entrar en casas y registrar muebles con 1 acción. Los mapas ya marcan armarios, cómodas, escritorios, estanterías, mostradores y camas.
  - Lo encontrado va en la mochila de cada soldado, que tiene un límite.
  - Se evacúa con el botín. Lo que se deja atrás se pierde.
- **Ruido:**
  - Cada disparo tiene un radio de ruido, y los zombis y los grupos inactivos van hacia el último ruido.
  - El machete y la ballesta no suenan.
  - Romper una puerta o una ventana suena.
  - El Chillón llama a todos los de alrededor.
- **Contagio:**
  - El mordisco de un zombi contagia.
  - Un contagiado vuelve a la base con días contados: o medicinas, o se convierte y se pierde.
  - Es el drama del juego: una decisión de dos llaves cuando las medicinas no llegan para todos.
- **Escasez:**
  - Munición contada por misión.
  - Las armas mejores se encuentran o se fabrican. Gameplay propone un camino mixto: fabricar en el Taller desbloquea el nivel del arma.
- **Los tipos que ya existen**, con piel nueva (así se llaman ya en el juego):
  - Eliminación es «Limpieza».
  - Recuperación, «Botín».
  - Sabotaje se queda: callar una sirena, o quemar un Nido.
  - Pirateo, «Arreglar la radio».
  - Rescate se queda.

**Recursos:**
- **Suministros:** la moneda de construir, con el signo «§» (el de los suministros de XCOM 2). Ocupa el lugar de los créditos, y las ruinas y el saqueo la dan.
- **Medicinas** (✚): curan heridas y contagios. El tratamiento de un mordido se aprueba con las dos llaves.
- **Comida**, contada en **raciones**: el Bastión come cada día; sin raciones hay hambre. El **Huerto** las produce.
- **Piezas:** muelles, motores, tornillería. Con ellas el Taller hace armas y armaduras mejores. Salen de almacenes, maleteros y oficinas, y del equipo de los Acorazados.

**Armas encontradas** (raras, al registrar): fusil de cerrojo, escopeta recortada, fusil con mira y ametralladora ligera. Van a la Armería, y a quien la lleve, su arma le sube un nivel.

**De noche:** se ve poco y os ven poco, salvo bajo farolas, bengalas o fuego. Hay más muertos en la calle, y el botín vale la mitad más.

## Clases y el oficio de superviviente

El árbol de habilidades (antes «la Rosa»; Arturo lo renombró el 2026-10-13) y sus ramas se quedan. La magia de Velmar desaparece, pero su rama no se tira: pasa a ser el **Oficio**, una cuarta rama de cada eje que abre un edificio, el **Taller de oficios**, donde Teo enseña. Las mecánicas que ya están hechas se reaprovechan con otro nombre:

| Antes (Canto) | Ahora (Oficio) | Lo que hace |
|---|---|---|
| Asalto · Juramento | **Machete** | Hoja afilada, escudo antidisturbios, saltar por una ventana (el antiguo teletransporte, más corto), vendaje rápido y «Aguantad» (nadie entra en pánico) |
| Granadero · Evocación | **Fuego** | Bengala, petardos (atraen a los zombis lejos de vosotros: era Silencio), cóctel molotov (daño en área, sin que el fuego se extienda), gasolina extra y el bidón |
| Francotirador · Ojo de Velmar | **Ballesta** | Rastreo, flecha perforante (atraviesa la cobertura), encaramarse, prismáticos y lluvia de flechas |
| Especialista · Voz | **Enfermería de campo** | Calma, sedante (quita el pánico), arenga, manos de enfermero y desfibrilador (levantar a alguien caído en el turno) |

Además:
- el Temple 10 «Sordo al Coro» pasa a ser «Nervios de acero»: el grito del Chillón no le afecta;
- la aptitud que se llamaba Oficio pasa a ser **Maña** (MAÑ), para no confundirla con la rama: arreglar equipos, granadas y curas (Arturo, 2026-10-13).

**El Grillo** se queda: es el dron del Especialista, uno de juguete con una batería de coche, que Teo montó en el taller y que hace cri-cri al volar. Cura, cubre, da calambres y arregla equipos a distancia. Sus acciones dejan de llamarse «protocolos» (Arturo, 2026-10-13):

| Antes | Ahora |
|---|---|
| Protocolo médico | Botiquín volante |
| Protocolo de ayuda | Cobertura del Grillo |
| Protocolo de combate | Calambrazo |
| Protocolo de reanimación | Espabilar |
| Descarga de condensador | Descarga de batería |
| Hackear | Arreglar |

## Lo que se queda y lo que cambia

| Parte | Qué pasa |
|---|---|
| Árbol de habilidades, ramas, aptitudes, rangos | Se quedan. Se renombran el Oficio y lo psiónico. |
| Base en superficie, edificios, cercanías | Se quedan, con otra piel: huerto, barricadas, depósito de agua, Camposanto… |
| Capítulos, encargos, rumores | Se quedan como sistema y se reescriben todos los textos. |
| Radio, Expediente, avisos de Morse | Se quedan. Se reescriben las escenas, las hojas y las fichas. |
| Doble llave, cooperativo | Se queda, con la nueva historia. |
| Geoesfera | Pasa a ser el mapa de Valdesombra (trabajo de Arte). |
| Créditos, pago mensual, venta de cuerpos | Se cambian por suministros, medicinas (y luego comida), saqueo y enclaves aliados. |
| Combate | Se añaden registro, mochila, ruido, contagio, el Hinchado y la IA de los zombis (van al ruido, en masa). |
| Partidas guardadas | Lo más limpio es empezar campañas nuevas: es otro mundo. |

## Orden de trabajo

Es la propuesta de Gameplay, con el coste de cada paso.

| Fase | Contenido | Quién | Coste |
|---|---|---|---|
| 1 · Nueva piel | Este documento como nuevo LORE.md. Todos los textos: nombres de enemigos, zonas, capítulos, encargos, acontecimientos, escenas de radio, Expediente, fichas, glosario y edificios. La Marea en kilómetros, los Nidos, La Mula como autobús. Los tipos de misión con nombre nuevo, la rama de Oficio y «Nervios de acero» | Lore (textos) · Gameplay y Clases (renombres) | Bajo |
| 2 · Saquear y ruido | Misión de saqueo (registrar muebles, mochila, evacuar el botín), ruido e IA de los zombis, el grito del Chillón y el Hinchado | Gameplay · Arte (interiores, zombis) | Medio |
| 3 · Contagio y medicinas | El contagio con días contados, las medicinas como recurso y la decisión de dos llaves | Gameplay · Lore (textos) | Medio |
| 4 · Comida y armas encontradas | Consumo diario, munición contada y fabricar en el Taller para desbloquear el nivel del arma | Gameplay (con su simulación de equilibrio) | Alto |
| 5 · El mapa del valle | Valdesombra en lugar del globo | Arte | Alto |

**Arte:** el usuario está comprando un pack de Synty. Si es POLYGON Apocalypse, trae zombis, supervivientes, casas y vehículos, y encaja de lleno con este tema. Pero cambiaría la dirección de arte (ahora todo es vóxel), así que lo decide el usuario.
