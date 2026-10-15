# Bastión — lore

**Estado (2026-10-14):** las cinco fases del cambio a zombis están en el juego (ver «Orden de trabajo»), con los nombres y el tono aprobados por Arturo el 13/10 ([LORE-PROPUESTA-2026-10-13.md](LORE-PROPUESTA-2026-10-13.md)). Este documento manda sobre los nombres: si un texto del juego no coincide, se corrige el texto.

Decisiones tomadas:
- un lugar inventado;
- suministros, comida y medicinas;
- la magia pasa a ser el Oficio;
- «zombis» y «la Pandemia».

El lore anterior, el del Coro, está en [archive/LORE-coro.md](archive/LORE-coro.md).

**Cómo está planteado:** cambiar de piel, no de motor. Casi todos los sistemas que ya funcionan se quedan y cambian de nombre y de historia:
- capítulos, encargos y rumores;
- radio y Diario (antes, Expediente);
- la base en superficie;
- el árbol de habilidades y sus ramas;
- las decisiones a medias: uno propone y el otro acepta.

Lo nuevo de verdad es la supervivencia: saquear, el ruido, la infección y la escasez.

## La idea en una frase

Hace dos inviernos llegó la Pandemia: quien se contagia muere en un día y se levanta zombi. Atalaya, una vieja estación militar en la sierra, aguanta con cuarenta supervivientes llegados de todas partes. Los dos jugadores la dirigen y mandan sus equipos a los pueblos del valle a por comida, medicinas y gente viva. Cada disparo atrae a más.

**La premisa en el juego** (`PREMISE` en `lore.ts`, la tarjeta antes de la primera misión, aprobada por Arturo el 13/10):

> Hace dos inviernos, la Pandemia vació el valle de Kessra. Los muertos se levantaron y siguen andando.
>
> Atalaya, una vieja estación militar en la sierra de Brena, aguanta con cuarenta supervivientes llegados de todas partes.
>
> Vosotros dos la dirigís. Lo importante lo decidís juntos.
>
> Atalaya vive de lo que sus equipos traen de fuera. El primero sale hoy.

## Tono

- **Adulto y seco** (prueba del 13/10): frases cortas, nada de explicar lo que se ve, nada de chistes ni guiños. Los personajes hablan como adultos cansados. La regla va en la línea pequeña del consejo, no en el diálogo.
- **Morse habla poco** (prueba del 14/10, Arturo y Ana): solo un aviso táctico o algo que el jugador no ve. Nada de presentaciones, nada de repetir el objetivo ni el informe, nada que el juego ya enseñe en pantalla.
- **Sin metáforas sobre el mando compartido** (prueba del 14/10): ni «dos llaves» ni «llave doble». Se dice lo que pasa: uno propone, el otro acepta; lo deciden los dos.
- **Supervivencia seria:** más *The Last of Us* y *This War of Mine* que gore de serie B.
- **El miedo:** la escasez y el ruido, más que el monstruo.
- **Los vivos, en sus misiones** ([ZOMBIS-PRIMERO.md](ZOMBIS-PRIMERO.md)): las misiones normales son de los muertos. Cuervos y Congregación aparecen en las suyas, y ahí son lo más peligroso.
- **Todo es inventado:** el país, el valle, las ciudades. Ningún sitio real, y nombres de lugar sin idioma claro (Kessra, Brena, Corran, Velsa, Galen, Carvik, Telm). Que suenen a un pueblo de hoy, no a fantasía: cortos, sin «-ren», «-dren» ni «-vik» (prueba del 15/10, Ana: «Aldren» y «Volkren» pasaron a Brena y Velsa).
- **Gente de todas partes:** el puerto de Galen y el Instituto traían gente de todo el mundo, y el valle fue camino de huida hacia la sierra. Los nombres de soldados y personajes son internacionales.

## Cronología

| Cuándo | Qué pasó |
|---|---|
| Otoño, año 0 | Primeros casos en el Instituto Galen, un laboratorio de vacunas a las afueras de Galen. Los telediarios hablan de «la Pandemia». |
| Tres semanas después | Los muertos se levantan en las ciudades. Ya nadie dice «infectados»: dicen **zombis**. |
| Invierno, año 0 | Caen Galen y Corran. El ejército se repliega y desaparece. La radio se queda muda. |
| Año 1 | Los vivos se esconden en los pueblos de la sierra. Aparecen los Cuervos, bandas que viven de saquear a otros. |
| Primavera, año 2 · Día 1 de la campaña | Dos grupos de supervivientes llegan a la vez a la Estación Atalaya. Se quedan los dos, y se mandan a medias. |

## El mundo: el valle de Kessra

Es una comarca inventada entre una sierra alta y una llanura, con dos ciudades muertas. El globo de XCOM pasa a ser un **mapa del valle** en Synty (la pestaña «El valle»), con ocho zonas unidas por carreteras en lugar de las ocho regiones ([FASE5.md](FASE5.md)). Para el jugador son siempre **zonas**, nunca «regiones».

| Zona | Id | Qué hay | Aliado (enclave) |
|---|---|---|---|
| Brena | `weu` | Los pueblos de la sierra, junto a Atalaya (la de partida) | El Refugio de Brena |
| Corran | `afr` | La primera ciudad caída, llena de zombis | Los del Hospital |
| El Pantano Gris | `eas` | Un embalse y una central eléctrica muerta | La Red Callada |
| Velsa | `eeu` | Valles cerrados, aldeas que aún resisten | Los Pastores |
| Galen | `me` | La ciudad muerta y el Instituto Galen | Los del Instituto |
| Los Llanos de Ceniza | `na` | La llanura por donde sube la Marea | Los Camioneros |
| Carvik | `sa` | Minas y carreteras cortadas | El Sindicato de la Mina |
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

- **La Estación Atalaya:** una vieja estación militar en lo alto de la sierra de Brena, con refugio subterráneo, muros de hormigón y una presa al lado. La nieve, la altura y una sola carretera la mantienen viva. Quienes llegaron conservaron su orden militar: equipos, rangos y armería.
- **La base en superficie** encaja tal cual: el refugio en el centro y alrededor barracones, huerto, barricadas y pasarelas. Se despejan rocas, bosque y ruinas, y las ruinas dan suministros.
- **La Lata:** un autobús de línea con la chapa que Teo le soldó por encima; de ahí el nombre que le puso la gente de Atalaya (antes, «La Mula»: Ana la quitó en la prueba del 14/10 y Arturo eligió este). Lleva a la escuadra, la espera en la zona de evacuación y vuelve cargado. Su taller en la base es la **Chapistería**. En el código, el nombre sale de `BUS` (`lore.ts`).
- **Las decisiones a medias se quedan, con otra historia.** Dos grupos que llegaron a la vez y no se fían del todo: lo importante lo deciden los dos. El año pasado, un líder solo vendió su refugio a los Cuervos a cambio de su vida.

## Personajes

Se mantienen las voces de la radio que ya existen y cambian sus vidas.

| Personaje | Papel nuevo |
|---|---|
| **Nadia Albescu, «Morse»** | Radioaficionada de toda la vida. Mantiene la radio de Atalaya y es la primera en oír a otros vivos. Firma los informes. |
| **Teodor Vidić, «Teo»** | Mecánico jubilado. Blindó La Lata, arregla armas y enseña oficios. Construyó el Grillo. Secreto: trabajó de vigilante en el Instituto Galen la noche en que todo empezó. |
| **Dra. Amara Nwosu** | Viróloga del Instituto Galen. Huyó con sus cuadernos y cree que hay una vacuna. |
| **Simão Ferreira** | Desertor de los Cuervos. Sabe dónde guardan lo que roban y a quién. |
| **Ruth Halvorsen** | Portavoz del Refugio de Brena, el enclave más grande. Socia exigente. |
| **Celia Marsh, «la Voz»** | Presentadora de radio antes de la Pandemia. Ahora guía a la Congregación y a la Marea con su emisora. |

## La historia

Mantiene la estructura actual: siete capítulos, uno por nivel del Bastión, que no caducan, y una misión final con un dilema que deciden los dos.

| Nivel | Capítulo | Tipo · zona | Qué pasa |
|---|---|---|---|
| 1 | **Primer invierno** | Recuperación · Brena | Las medicinas del consultorio del pueblo, antes de que caduquen. |
| 2 | **La doctora** | Rescate · Corran | Sacar a la Dra. Nwosu de un hospital lleno de zombis. |
| 3 | **La sirena** | Sabotaje · Los Llanos de Ceniza | Callar la sirena con la que los Cuervos atraen hordas. |
| 4 | **La torre del Pico Gris** | Pirateo (arreglar la radio) · El Pantano Gris | Volver a poner en marcha el repetidor: oír a los demás enclaves. |
| 5 | **Los inmunes** | Rescate · Velsa | Una niña a la que la Pandemia no mata. Simão sabe dónde la tienen los Cuervos. |
| 6 | **Los cuadernos de Galen** | Recuperación · Galen | Lo que pasó de verdad en el laboratorio. Teo estaba allí. |
| 7 | **La Voz** | Rescate · Los Llanos de Ceniza | Asaltar la emisora de la Congregación y sacar a la Voz con vida: solo ella sabe parar la Marea. |
| 8 | **La Marea** | Final | La horda llega a la sierra. Atalaya resiste, y la vacuna está lista. |

**El reloj: la Marea.**
- Es una horda inmensa que sube desde la llanura con el deshielo, y sustituye a la Partitura.
- En vez de compases cuenta kilómetros: «La Marea, a 9 km». Cuando llega a 0, Atalaya cae.
- Hasta el nivel 4 del Bastión (cuando se arregla la radio del Pico Gris) nadie sabe que existe: el reloj se llama **Infestación** («Infestación 3/12») y las zonas caídas están «en manos de los muertos». Desde el nivel 4 es la Marea.
- Entra por Los Llanos de Ceniza y sube por las carreteras: cubre las zonas a 4, 6, 9, 10 y 11, según lo lejos que estén de Los Llanos ([FASE5.md](FASE5.md)). Brena no cae: a 12, la campaña se pierde. Los textos no conjugan verbos con el nombre de la zona, que puede ser plural.
- Los **Nidos**, sitios donde los zombis se juntan (antes, los Órganos), la alimentan. Quemarlos la hace retroceder.

**El final** (lo deciden los dos). Con la Marea parada y la vacuna en la mano:
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
  - En la misión, el mordido pierde 1 de salud cada turno. Sin una **Cuarentena** en el Bastión (una nave aparte, con cerrojo por fuera) no sube a La Lata y se queda fuera (prueba del 15/10).
  - Si vuelve, vuelve con días contados: o medicinas, o se convierte y se pierde. Cada mejora de la Cuarentena le da un día más.
  - Es el drama del juego: una decisión de los dos cuando las medicinas no llegan para todos.
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
- **Medicinas** (✚): curan heridas y contagios. El tratamiento de un mordido lo aprueban los dos.
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
| Radio, Expediente, avisos de Morse | Se quedan. El Expediente pasa a ser el **Diario** de Atalaya, que escribe Morse. Se reescriben las escenas, las hojas y las fichas. |
| Decisiones a medias, cooperativo | Se quedan, con la nueva historia. |
| Geoesfera | Pasa a ser el mapa del valle de Kessra, en Synty (hecho). |
| Créditos, pago mensual, venta de cuerpos | Se cambian por suministros, medicinas (y luego comida), saqueo y enclaves aliados. |
| Combate | Se añaden registro, mochila, ruido, contagio, el Hinchado y la IA de los zombis (van al ruido, en masa). |
| Partidas guardadas | Lo más limpio es empezar campañas nuevas: es otro mundo. |

## Nombres en el juego

Lo que ve el jugador, para que todos los textos digan lo mismo (aprobado por Arturo el 13/10). La columna de la izquierda es lo que **no** debe aparecer.

| No | Sí |
|---|---|
| Geoesfera, globo | **El valle** (pestaña y mapa) |
| Región | **Zona** |
| Expediente | **Diario** («DIARIO · HOJA N») |
| Hangar | **Cochera** |
| La Mula (el autobús), Taller de La Mula | **La Lata**, **Chapistería** |
| Escanear, escaneando, días de escaneo | **Esperar noticias**, «A LA ESCUCHA…», días a la escucha |
| Actividad zombi | **Infestación** (hasta el nivel 4); **la Marea, a N km** (desde el 4) |
| Órgano | **Nido** |
| Sala de simulación | **Sala de mapas** |
| Terminal, hackear, pirateo | **Arreglar** (el equipo, la radio) |
| Protocolos del dron | Botiquín volante, Cobertura del Grillo, Calambrazo, Espabilar |
| La Rosa | **Árbol de habilidades** |
| Aldren, Volkren | **Brena**, **Velsa** |
| Dos llaves, llave doble | Uno propone y el otro acepta; «lo decidís juntos» |
| Los datos (el objetivo) | El nombre del objeto: «las medicinas»; sin nombre, «la carga» |
| Oficio (la aptitud) | **Maña** (MAÑ); el Oficio es la rama |
| Créditos | **Suministros** (§) |
| Refuerzos (en combate) | **La horda**: los muertos que llegan por los bordes |
| «Operación Ceniza Rota» | Nombre de lugar: «La gasolinera de Velsa» |

Con los nombres de zona: «del Pantano Gris», «al Pantano Gris», «de La Vega», «en Los Llanos de Ceniza» (`ofZone` y `toZone` en `lore.ts`). Nunca «de El…».

## Orden de trabajo

Es la propuesta de Gameplay, con el coste de cada paso. **Las cinco fases están hechas** (2026-10-13); después vinieron la noche y la altura, y la fase 6, los edificios de varias plantas.

| Fase | Contenido | Quién | Coste |
|---|---|---|---|
| 1 · Nueva piel | Este documento como nuevo LORE.md. Todos los textos: nombres de enemigos, zonas, capítulos, encargos, acontecimientos, escenas de radio, Expediente, fichas, glosario y edificios. La Marea en kilómetros, los Nidos, La Mula como autobús. Los tipos de misión con nombre nuevo, la rama de Oficio y «Nervios de acero» | Lore (textos) · Gameplay y Clases (renombres) | Bajo |
| 2 · Saquear y ruido | Misión de saqueo (registrar muebles, mochila, evacuar el botín), ruido e IA de los zombis, el grito del Chillón y el Hinchado | Gameplay · Arte (interiores, zombis) | Medio |
| 3 · Contagio y medicinas | El contagio con días contados, las medicinas como recurso y la decisión de los dos | Gameplay · Lore (textos) | Medio |
| 4 · Comida y armas encontradas | Consumo diario, munición contada y fabricar en el Taller para desbloquear el nivel del arma | Gameplay (con su simulación de equilibrio) | Alto |
| 5 · El mapa del valle | Kessra en lugar del globo | Arte | Alto |

**Arte:** todo es Synty POLYGON (lo vóxel está borrado); ver [ARTE.md](ARTE.md).
