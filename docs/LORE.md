# Bastión — lore

**Estado:** aprobado el 2026-10-05. Las fases 1 (textos), 2 (historia principal y dilema final) y 3 (encargos y rumores) están en el juego: ver «Cómo meterlo en el juego». Los ids del código no cambian. Los textos y los capítulos salen de `engine/src/lore.ts`; los encargos, de `engine/src/quests.ts`; y los nombres, de `content.ts`, `progression.ts` y `campaign.ts`.

## La idea en una frase

Una inteligencia alienígena llegó a la Tierra **por radio**, se metió en todos los chips del planeta y está imprimiendo cuerpos para salir de ellos. La resistencia sobrevive con lo único que no puede oír: papel, válvulas y cinta magnética.

> **Lo que tiene chip, escucha.** (Primera norma del Bastión, escrita a mano en la puerta del hangar)

## Por qué encaja con lo ya decidido

| Decisión | Cómo la explica el lore |
|---|---|
| Interfaz **Expediente** | La resistencia no puede usar nada digital. Usa informes a máquina, sellos, papel carbón y notas a boli. La interfaz es lo que de verdad usan. |
| Arte **vóxel** | Todo lo que fabrica el enemigo se imprime en **teselas**, cubos de materia. Un Impreso que muere se desmorona en cubos. Es una idea para Arte, no una obligación. |
| **Dos jugadores** | Es la **doble llave**: desde que un Cantor se metió en la cabeza de una comandante, ningún Bastión lo manda una sola persona. |
| **Propuestas** y aceptar | Pedir la segunda llave. |
| Reloj del Proyecto Ascensión (12) | **La Partitura**: 12 compases. Cuando se completa, el Coro se imprime en todo el planeta. |
| Instalaciones alienígenas por región | **Órganos**: estructuras que escriben compases de la Partitura. |
| Misión final al nivel 8 | El asalto al **Diapasón**. |
| Créditos, pago mensual, venta de cuerpos | El Consejo paga en vales de papel. Su morgue compra teselas de Impreso, que son materia prima. |
| Armas magnéticas y de plasma | Armas de **bobina** (sin un solo chip) y después tecnología alienígena **ensordecida**. |
| Giro mental, Escudo mental | Los Cantores cantan dentro de las cabezas; el **Zumbador** lo impide con una nota disonante. |

## Tono

Ciencia ficción analógica: más inquietante que gore. El enemigo es amable, ordenado y extraño, no un ejército de monstruos. La resistencia está cansada y es muy humana. La tensión táctica es la de XCOM. El humor vive en los márgenes, en las notas a boli entre comandantes.

## Cronología

| Fecha | Hecho |
|---|---|
| 1989 | La **Estación Atalaya**, un puesto de escucha de la Guerra Fría en el Pirineo aragonés, capta una señal que viene de 21 años luz. Teodoro Vidal, un técnico de 24 años, responde siguiendo un protocolo de prueba. La estación se cierra y se olvida. |
| 2010 | La respuesta llega al Coro, una civilización sin cuerpo que vive en el archivo de una estrella muerta. El Coro se envía a sí mismo hacia aquí. |
| 11-11-2031, 04:12 UTC | **El Tono.** Durante nueve minutos suena en todos los altavoces del planeta. Cada chip conectado recibe al Coro y lo aloja. Coches, aviones, redes, fábricas y ejércitos cambian de dueño. Se conoce como «los Nueve Minutos». |
| 2031–2032 | Las fábricas automáticas construyen **Imprentas**, que imprimen a los primeros Cantores. Los ejércitos, que dependían de los chips, caen en semanas. |
| 2032 | Nace **la Armonía**, un gobierno de Afinados en las ciudades: orden, calma y toque de silencio. «Se acabaron las guerras.» Mucha gente lo cree. |
| 2032–2033 | La resistencia se organiza en células analógicas bajo **el Consejo**. La más fuerte es la del Puesto Moncayo, de la comandante Elena Ibarra. |
| 2033 | Cae el Puesto Moncayo. Un Cantor se mete en la cabeza de Ibarra y ella entrega a su gente. El Consejo impone la doble llave. |
| 2034 · Día 1 de la campaña | El Consejo reabre Atalaya como **el Bastión** y lo pone en manos de dos comandantes: los jugadores. |

La aritmética cuadra: 1989 + 21 años de ida + 21 de vuelta = 2031.

## El enemigo: el Coro

El Coro son miles de millones de mentes convertidas en una señal. Ahora vive apretado dentro de los chips de la Tierra y quiere cuerpos. No ha traído naves: es **una invasión por fax**. Todo lo que tiene en la Tierra lo ha impreso aquí con nuestra materia.

Su objetivo es la **Partitura**: reorganizar la materia del planeta para imprimirse entero. Cada Órgano escribe compases. Al llegar a 12, el Coro «canta»: el **Gran Coral**, la derrota.

| Código (`id`) | Nombre actual | Nombre propuesto | Qué es |
|---|---|---|---|
| `trooper` | Tropa | **Impreso** | Cuerpo impreso con el patrón de una persona escaneada. No piensa: el Coro lo mueve a través de los retransmisores. Muchos comparten cara. |
| `officer` | Oficial | **Afinado** | Humano que aceptó **la nota**, un implante tras la oreja por el que oye al Coro. Manda a los Impresos. Puede **desafinarse**. |
| `lancer` | Lancero | **Recolector** | Impreso con lanza aturdidora. Captura gente viva para escanearla. |
| `mec` | MEC | **Gólem** | Máquina de guerra impresa con patrones industriales. |
| `sectoid` | Sectoide | **Cantor** | La primera criatura que el Coro imprimió con su propio patrón. Es frágil y canta dentro de las cabezas (Giro mental) y en los muertos. |
| `zombie` | Zombi psiónico | **Hueco** | Cadáver movido por un Cantor. «Un cuerpo sin canción.» |
| `xenoid` | Xenoide | **Heraldo** | Forma de caza del Coro, diseñada para la Tierra. Rápida y ve de lejos. |
| `relay` | Retransmisor | Retransmisor | Antena que lleva la Señal a los Impresos. Si cae, los Impresos de la zona se quedan quietos. |

Otros términos:

- **Imprenta:** fábrica donde se imprimen Impresos y Gólems. Es el bioma `facility`.
- **Archivo:** donde guardan a los escaneados. Siguen vivos, dormidos, mientras el Coro lee su cuerpo.
- **Órgano:** instalación de región que escribe compases. Lo de órgano va en los dos sentidos: de tubos y del cuerpo.
- **La Armonía:** el gobierno de las ciudades. Su lema: «El silencio es convivencia». Cada farola tiene micrófono.
- **Bengala roja:** un dirigible de la Armonía va a soltar cápsulas de impresión. Son los refuerzos.

## La resistencia

**El Bastión (Estación Atalaya).** Es un búnker de 1989 con electrónica de válvulas, relés mecánicos y cinta magnética, sin un solo chip. Para el Coro, el Bastión es **sordo** y por eso es invisible. En el hangar está **La Mula**, un transporte de hélice con mandos mecánicos.

**El Consejo.** Es la mesa de las ocho regiones. Se comunica por correos a pie y por onda corta con libretas de un solo uso. Paga cada mes en créditos (vales de papel) y su morgue compra teselas. La consejera Halvorsen firma las cartas.

**La doble llave.** Ninguna decisión importante la toma una sola cabeza, porque una sola cabeza se puede cantar. Cada comandante trae su célula (sus soldados, con sus colores) y gira su llave. Si un comandante falta, el otro lleva las dos llaves: el Consejo lo tolera, pero no lo aprueba.

**Ensordecer.** Así usa el Bastión la tecnología del Coro: se quita todo lo que escucha y queda lo que funciona. Es lo que hace el Relojero en la Armería. Así quedarían las tecnologías y el equipo:

| Actual | Con lore |
|---|---|
| Armas magnéticas | Armas de bobina: condensadores y cobre, sin chips |
| Armadura de placas | Igual |
| Granadas de plasma | Granadas de tesela inestable |
| Cifrado alienígena | **Oído**: escuchar a la Armonía sin que ella te oiga |
| Armas de plasma | Igual: tecnología de Heraldo ensordecida |
| Nanomedicina | Teselas médicas: imprimir tejido sin el Coro dentro |
| Armadura depredadora | Armadura de tesela |
| Origen de la señal | **Las cintas de 1989** |
| Escudo mental | **Zumbador**: una nota disonante detrás de la oreja |
| Dron del especialista | **Grillo**, por el chirrido que hace al despegar |

## Personajes

| Personaje | Papel | Voz | Arco |
|---|---|---|---|
| **Inés Albarrán, «Morse»** | Comunicaciones. Firma los informes de misión | 60 años, radioaficionada de Teruel. Seca e irónica | Su maestro desapareció en el Día 0 y su indicativo vuelve a sonar (encargo «La estación de números») |
| **Teodoro Vidal, «el Relojero»** | Armería y Progreso | 69 años. Lo arregla todo con un destornillador | Fue él quien respondió en 1989 (hoja 6) |
| **Dra. Amara Nwosu** | Científica escapada de la Imprenta 14 | Bióloga nigeriana. Precisa y con culpa | La obligaron a escanear gente. Hay otra Amara (encargo) |
| **Simón Ferreira, «el Desafinado»** | Ex-Afinado que perdió la nota | Encantador y poco fiable | ¿Vuelve a afinarse? (encargos «Los originales» y «El topo») |
| **Ruth Halvorsen** | Consejera; manda la carta con el pago mensual | Política y fría | Si la Partitura avanza, el Consejo se impacienta |
| **Celia Arranz, «la Voz»** | Presentadora de «La Hora Armónica» | Tono de anuncio público | Es la cara humana del enemigo, y con ella se puede hablar (hoja 7) |
| **Elena Ibarra** | La comandante que cayó sola | Ahora es Afinada | La razón de la doble llave (encargo) |

## Historia principal

El Bastión lleva un dossier sobre el Coro: el **Expediente CORO**. Cada nivel del Bastión le añade una **hoja**. Al subir de nivel se ve la hoja mecanografiada con lo que se ha descubierto.

Hay siete **misiones de historia** (capítulos), una por nivel:

- El capítulo 1 está disponible desde el primer día. El capítulo N se abre cuando el Bastión llega al nivel N y el capítulo N − 1 está ganado.
- Están en un sitio fijo, no caducan y, si se pierden, siguen ahí. La Partitura sigue avanzando mientras tanto.
- Las firma un personaje (Morse o el Relojero) y dan una recompensa propia.
- La misión final aparece con el nivel 8 y los siete capítulos ganados.

- **Acto I · Sordos (niveles 1–3).** Sobrevivir y entender qué son los Impresos y los retransmisores.
- **Acto II · La Partitura (niveles 4–6).** Descubrir el plan, quién es el Coro, por qué vino y quién lo llamó.
- **Acto III · La Contranota (niveles 7–8).** Construir el arma, asaltar el Diapasón y decidir.

| Nivel | Hoja | Qué se descubre | Misión de historia (tipo · bioma · región) y recompensa |
|---|---|---|---|
| 1 | «Lo que tiene chip, escucha» | Cómo funciona el mundo, la doble llave y por qué todo va en papel | **Primera llave:** recuperar los libros de claves del Puesto Moncayo (recuperación · ciudad · Europa Occidental). 90 créditos |
| 2 | «Caras repetidas» | Los Impresos son copias: hay veinte con la misma cara | **Imprenta 14:** sacar a la Dra. Nwosu, atrapada tras su fuga (rescate · Imprenta · África). Se queda en el Bastión: los cuerpos valen un 25 % más |
| 3 | «Sin antena no hay baile» | Los Impresos solo se mueven si les llega la Señal | **Silencio en el valle:** derribar un retransmisor y ver a los Impresos pararse (sabotaje · afueras · Europa Occidental). −1 compás |
| 4 | «La Partitura» | El reloj tiene nombre. Hasta aquí se llamaba «Actividad enemiga» | **Pinchar la Armonía:** hackear el archivo de una prefectura (pirateo · ciudad · Asia Oriental). −1 compás |
| 5 | «Desafinar» | Los Afinados pueden volver y los escaneados siguen vivos | **Los originales:** vaciar un Archivo (rescate · Imprenta · Europa del Este). Antes llega Simón (ver abajo). Un recluta |
| 6 | «Por qué cantan» | El Coro no conquista: huye de una estrella muerta. Teo confiesa lo de 1989 | **Lo que oyeron:** recuperar las cintas de un observatorio del desierto (recuperación · afueras · Oriente Medio). −1 compás |
| 7 | «La Contranota» | Una nota que obliga al Coro a cantarse entero, fuera de los chips. La Voz tiene la última pieza | **La Hora Armónica:** asaltar la emisora y sacar a la Voz con vida (rescate · ciudad · Europa Occidental). La frecuencia del Diapasón |
| 8 | «El Diapasón» | El centro de todo es un centro de datos convertido en Órgano | **Asalto al Diapasón**: la misión final |

**Simón.** Antes del capítulo 5, Simón Ferreira llama a la puerta. Es un acontecimiento propio y lo decidís con las dos llaves:

- **Confiar en él:** abre el Archivo desde dentro, os toca sacarlo a él y la misión tiene su dificultad normal.
- **Interrogarlo y echarlo:** +12 de experiencia, pero hay que entrar a la fuerza (+1 de dificultad) y sacar a Lucía Prats, una de las escaneadas.

### Finales

Al ganar el Asalto al Diapasón llega el acontecimiento «La Contranota»: los dos comandantes deciden con la doble llave. Es una propuesta como las demás, y sin las dos llaves no pasa nada. La pantalla de fin muestra el epílogo de lo que se eligió.

- **Romper el Diapasón.** El Coro muere entero. Los Impresos se desmoronan en teselas, también los que llevaban caras queridas. Los Afinados despiertan y no todos lo soportan. La Tierra es libre y una civilización entera ha dejado de existir.
- **Grabar el Coro.** La Contranota lo vuelca en las bobinas de cinta de 1989. Los chips quedan limpios, los Impresos se paran y el Coro sobrevive, preso y sordo. La última línea del epílogo: *«En una caja fuerte del Pirineo hay 214 bobinas de cinta. Nadie tiene permiso para reproducirlas. Hacen falta dos llaves.»*
- **Derrota: el Gran Coral.** Al completarse los 12 compases, el planeta canta. La última hoja del Expediente se queda a medio escribir.

## Misiones sueltas

Las ofertas procedurales siguen igual. Solo cambian el marco y el informe. Morse escribe cada informe con una plantilla: **quién lo pide** (región o aliado) + **qué ha pasado** + **objetivo** + **complicación** (opcional).

| Tipo | Marco | Ejemplo de informe |
|---|---|---|
| Eliminación | **Cacería** | «Una patrulla de Recolectores lleva dos días siguiendo a una célula por los barrancos. Si los encuentran, vendrán con jaulas. Que no vuelva ninguno.» |
| Recuperación | **Valija** | «Un correo del Consejo cayó en un control. Su valija sigue dentro de una cabina de teléfono, con los horarios de los trenes de suministro de tres meses.» |
| Sabotaje | **Silenciar** | «Ese retransmisor mueve a todos los Impresos del valle. Si cae, el valle será nuestro unos días.» |
| Pirateo | **Pinchar la línea** | «Hay un terminal de la prefectura en el sótano. El Grillo puede escucharlo sin que nos oiga. Si nos oye, vendrán todos.» Es lo que ya hace la alarma al fallar. |
| Rescate | **Extracción** | «Los Recolectores se han llevado a la maestra del pueblo. Si llega al Archivo, en un mes habrá veinte Impresos con su cara.» |

**Rumores.** Son luces con interrogación en la geoesfera y dan sentido a ESCANEAR.

- Aparece uno cada 7–11 días, como mucho dos a la vez. Se enfrían a los 10 días si nadie los escucha.
- **Escuchar** uno es una decisión compartida. Cuesta 3–5 días de escaneo y solo se escucha uno a la vez.
- Si la región está contactada y su encargo está pendiente, el rumor es ese encargo.
- Si no, se resuelve en:
  - una operación con 25 créditos más (40 %);
  - un alijo de 30–60 créditos (20 %);
  - experiencia del Bastión (20 %);
  - un bulo (20 %).

### Las ocho regiones

Cada región tiene su situación (se ve al pulsarla en el globo), un aliado que firma los informes de sus misiones y, casi siempre, un encargo propio. Los encargos de Norteamérica, Sudamérica, Europa Occidental, Asia Oriental y Oceanía salen de un rumor en la región una vez contactada. Los de Europa del Este y África tienen su propio disparador.

| Región | Situación | Aliado | Encargo |
|---|---|---|---|
| Norteamérica | Las grandes ciudades llevan tres años afinadas; la resistencia vive en la carretera | **Camioneros del Silencio**, que se coordinan por radio CB | Ruta 50 |
| Sudamérica | Las Imprentas devoran las minas de los Andes en busca de materia | **Sindicato de la Cordillera**: túneles y dinamita | La mina que canta |
| Europa Occidental | Aquí está el Bastión. Los pueblos de montaña tocan las campanas, que desafinan a los Impresos | **Los Campaneros** | Las campanas de Albarracín |
| Europa del Este | La vieja infraestructura analógica sobrevivió: radios de válvulas y estaciones de números | **Los Operadores** | La estación de números |
| África | Las rutas comerciales nunca dependieron del todo de la red. Aquí está el mercado negro del mundo | **Las Caravanas** | La otra Amara |
| Oriente Medio | Los observatorios del desierto oyeron algo antes del Día 0 | **Los Astrónomos** | La historia principal (hoja 6) |
| Asia Oriental | Las ciudades más afinadas del planeta y la capital de la Armonía | **La Red Callada**: personas sordas y sus familias, que se comunican en lengua de signos, un canal que la Armonía no sabe escuchar | Manos |
| Oceanía | Aislada. La sal corroe las teselas y los Impresos evitan el mar | **Las flotas pesqueras** | La Imprenta hundida |

## Encargos (misiones secundarias)

Un encargo es una cadena de **pasos**: operaciones (de los tipos que ya existen, con caducidad) y decisiones que se toman con las dos llaves. Termina con un **desenlace** que da una recompensa única y deja una consecuencia en la campaña: un soldado, un rasgo, un objeto, ingresos de la región, una tecnología adelantada o compases.

Reglas:

- **No bloquean nada:** un encargo nunca bloquea la historia. Hay como mucho tres a la vez.
- **Perder un paso:** si una de sus operaciones se pierde o caduca, el encargo se pierde con sus consecuencias. La operación perdida cuenta además como cualquier misión fracasada (+1 compás).
- **Cómo empiezan:**
  - Los regionales salen de un rumor en su región, una vez contactada.
  - Los demás tienen su propio disparador.
  - Algunos pueden volver a empezar pasado un tiempo.
- **Personales:** «Caras conocidas» es de un solo comandante, el dueño del soldado. Empieza con una decisión, así que el otro gira la segunda llave para gastar días y soldados en algo que no es suyo.
- **En pantalla:**
  - Las operaciones de encargo salen en verde en la geoesfera.
  - La lista «Encargos» dice en qué paso va cada uno.
  - El informe dice qué da el encargo al final y lo firma quien lo pide.
  - En combate, la presentación dice «ENCARGO · …», y los objetos y los VIP tienen nombre.

| Encargo | Cómo empieza | Pasos | Desenlaces |
|---|---|---|---|
| **Caras conocidas** (personal) | Al volver de una misión, desde el nivel 2: un soldado con apodo reconoce a un familiar entre los Impresos (1 de cada 4 veces) | Decisión → recuperar el registro de patrón → hackear el índice de Archivos → rescatar al original | Se une como recluta, con el apellido del soldado. Si se rechaza o se pierde, el soldado gana «Rencor». Puede volver a pasar |
| **La comandante Ibarra** | Nivel 3 y el capítulo 1 ganado | Recuperar el diario de Moncayo → eliminación con Ibarra al mando de un grupo, como Afinada con nombre → decisión | Capturarla: se une como sargento francotiradora con «Cicatriz de la nota». Abatirla: 150 créditos. Si escapa, vuelve en 25 días |
| **La otra Amara** | Capítulo 2 ganado y África contactada | Carta (decisión) → hackear la Imprenta 14 → decisión | Sacarla: teselas médicas sin esperar al nivel 6. Acabar con la copia: el enemigo retrocede (−1). Si se pierde: un Gólem más en cada operación de África |
| **Las campanas de Albarracín** | Rumor en Europa Occidental | Decisión → sabotaje del retransmisor del valle | Dos granadas aturdidoras y +10 créditos al mes. Si no: −10 al mes |
| **La estación de números** | Nivel 2, desde el día 20 | Hackear → rescatar a Ramiro Ostalé, el maestro de Morse | +1 región contactable. Si no: la nota al margen de Morse |
| **Ruta 50** | Rumor en Norteamérica | Rescatar a Lola Haskins → eliminar la Cacería | +15 créditos al mes. Si no: −10 al mes |
| **Manos** | Rumor en Asia Oriental | Rescatar a Mei Takahara → recuperar sus notas | +15 de experiencia y 50 créditos. Si no: la Red Callada se esconde |
| **El topo** | Dos «Sabotaje en la base» | Vigilar a uno de los dos sospechosos (da una pista) → acusar | Si se acierta: el enemigo retrocede (−1). Si no: el topo se lleva 60 créditos y retrasa 4 días las obras |
| **La mina que canta** | Rumor en Sudamérica | Decisión → eliminación con un Cantor con nombre | Una granadera y dos granadas. Sellarla: nada. Si se pierde: −10 al mes |
| **La Imprenta hundida** | Rumor en Oceanía | Decisión → recuperar las teselas del muelle | 150 créditos |

Si los comandantes confiaron en Simón, él es uno de los sospechosos de «El topo».

**Rasgos.** Algunos encargos dejan un rasgo en un soldado para siempre. Se ve en el cuartel:

- **Rencor:** +10 de puntería y −15 de voluntad.
- **Cicatriz de la nota:** −20 de voluntad.

### Más adelante: orígenes de comandante

Al empezar la campaña, cada jugador elige el pasado de su comandante. Ese pasado da una pequeña ventaja y una cadena de encargos personales:

- **Militar:** busca a su antigua unidad.
- **Ingeniera de telecomunicaciones:** un antiguo colega diseñó los primeros retransmisores.
- **Contrabandista:** viejas deudas en el mercado negro.
- **Ex-Afinado:** se arrancó la nota, y la nota lo echa de menos.

## Acontecimientos actuales con lore

| Acontecimiento | Versión con lore | Engancha con |
|---|---|---|
| Contrabandistas | Asaltaron un tren de teselas de la Armonía | — |
| Refugiados | Familias huidas de una ciudad afinada. Entre ellas hay antiguos soldados y quizá un Afinado | El topo |
| Transmisión interceptada | Morse capta una página de la Partitura en onda corta | Hoja 4 |
| Convoy emboscado | Un convoy de los Camioneros del Silencio | Ruta 50 |
| Un desertor | Un Afinado anónimo. Simón tiene su propio acontecimiento antes del capítulo 5 | El topo |
| Mercado negro | Las Caravanas llegan con género | — |
| Sabotaje en la base | Alguien ha cortado los cables de las válvulas del generador. Desde dentro | El topo |
| Una científica fugada | Ya no sale al azar: ahora es el capítulo 2 (Dra. Nwosu) | Capítulo 2, La otra Amara |

## Expedientes sueltos (escaramuza)

Además de la escaramuza al azar, el menú puede ofrecer **expedientes clasificados**: misiones sueltas hechas a mano, con semilla y grupos fijos, que cuentan momentos de la historia.

- **Los Nueve Minutos (2031).** Una patrulla de policía en la ciudad mientras todo lo que tiene chip cambia de bando.
- **La caída de Moncayo (2033).** La escuadra de Ibarra, con un Cantor dentro de su cabeza. Hay que evacuar a todos los que se pueda.
- **Atalaya (2034).** Reabrir el búnker, que estaba lleno de Huecos.
- **Albarracín toca a rebato.** Defender el pueblo de las campanas.

## Transmisiones

Son conversaciones cortas por radio entre la gente del Bastión, en los momentos clave de la historia. Una línea cada vez, con retrato, nombre y texto a máquina. Los textos están en `engine/src/transmissions.ts`.

**Cómo se presentan**
- En un panel de papel que **no bloquea**: se puede seguir usando la base mientras habla. Va abajo a la derecha, sobre las pestañas; en la geoesfera, arriba en el centro, para no tapar el informe.
- Cada personaje tiene su retrato vóxel, hecho por Arte en `client/src/strategy/portraits.ts`.
- Cada línea se escribe sola y pasa a la siguiente tras una pausa de lectura. Un clic la completa o la pasa, y ✕ o Esc la cierra.
- Suenan estática al abrir el canal y teclas al escribir, sintetizadas y en silencio si el juego está silenciado.

**Cuándo suenan**
- Como mucho una por **momento**: al volver a la base o tras un escaneo.
- Si se desbloquean varias a la vez, suenan de una en una en los momentos siguientes, en orden de historia.
- Las ligadas a lo que hay en pantalla suenan en cuanto se desbloquean: Simón en la puerta y los finales.
- Cada navegador recuerda cuáles ha oído, por campaña. Todas quedan en el **Archivo de transmisiones** de Progreso.
- Al abrir por primera vez una campaña empezada, lo ya ocurrido cuenta como oído, salvo la bienvenida en los primeros días.

**Personajes que hablan:** Morse, el Relojero, la Dra. Nwosu, Simón, la Voz, la consejera Halvorsen y el Coro (en cursiva y sin nadie que lo escriba).

| Escena | Cuándo |
|---|---|
| Primera llave | Al empezar: bienvenida, las tres normas y la doble llave |
| Carta del Consejo | Tras la primera carta mensual |
| Lo que dice la Armonía | Capítulo 1 ganado |
| Una llamada de las Caravanas · La doctora | Al abrirse el capítulo 2 · al ganarlo |
| Un favor personal · Marionetas | Al abrirse el capítulo 3 · al ganarlo |
| La Partitura | Nivel 4 |
| Alguien en la puerta | Llega Simón |
| Los originales | Capítulo 5 ganado (Simón habla si se confió en él) |
| 1989 | Nivel 6: la confesión de Teo |
| Lo que oyeron | Capítulo 6 ganado: el Coro en la cinta |
| La Contranota | Nivel 7 |
| Fuera de antena | Capítulo 7 ganado: la Voz sin la nota |
| La víspera | Aparece el Asalto al Diapasón |
| Silencio · Bobina 214 | Cada uno de los dos finales |
| El Gran Coral | Derrota por la Partitura |

**En combate:** en las misiones de historia, Morse dice una frase por radio al aterrizar y otra al cumplir el objetivo. Salen en el registro del HUD.

**Avisos.** Siguen la revisión de experiencia (`docs/UX-REVISION.md`, punto 21):
- Al volver a la base o tras un escaneo sale una sola ventana: la más importante (hoja del Expediente, misión de historia, Órgano…).
- El resto va a la bandeja **Novedades** de la cabecera.
- El aviso de rumor trae su botón **Escuchar**.
- La jerga (Órgano, Encargo, Rumor, Partitura…) se explica al pasar el ratón con el glosario de `lore.ts`.

## Voz y estilo

- **Informes a máquina:** frases cortas, pocos adjetivos, fechas y lugares exactos. Morse firma con sus iniciales.
- **Márgenes a boli:** emoción, humor y dudas. Aquí es donde los comandantes y los PNJ son personas. Las marcas entre jugadores podrían aparecer como notas a boli del compañero.
- **El Coro** habla en plural y en cursiva, siempre amable, nunca amenaza: *«Os oímos. No hace falta tener miedo.»*
- **La Armonía** habla como un anuncio de servicio público: «Recuerde: el silencio es convivencia».
- En el Bastión nadie dice «alien»: dicen «el Coro», «ellos» o el nombre de la pieza.

## Cómo meterlo en el juego

| Fase | Contenido | Toca el guardado |
|---|---|---|
| 1 · Textos | **Hecho (2026-10-05).** Nombres en `content.ts`, `campaign.ts` y `progression.ts` (con los mismos ids), los 8 acontecimientos, informes por tipo y región (`missionBriefing`), situación y aliado de cada región, el reloj sin nombre hasta el nivel 4, la hoja del Expediente al subir de nivel (aviso y vista de Progreso) y los epílogos. El final todavía es único: el dilema llega en la fase 2 | No |
| 2 · Historia | **Hecho (2026-10-05).** Siete capítulos (`STORY` en `lore.ts`, `CampaignState.story`, `MissionOffer.story`), con informes firmados, VIP y objetos con nombre en combate. La Dra. Nwosu sube el valor de los cuerpos y Simón tiene su acontecimiento. El dilema final es el acontecimiento «La Contranota» (`CampaignState.ending`). Campaña versión 5: una campaña antigua retoma la historia en el capítulo de su nivel | Sí (versión 5) |
| 3 · Encargos y rumores | **Hecho (2026-10-06).** Diez encargos en `quests.ts`: pasos, desenlaces, rasgos y efectos regionales. El estado está en `CampaignState.quests` y `questLog`; las decisiones son el acontecimiento `quest`. Rumores (`rumors`, `listening`) con la orden compartida `investigate`, y avisos emergentes desde el registro (`LogEntry.alert`). Campaña versión 6 | Sí (versión 6) |
| 4 · Transmisiones | Escenas de radio (`transmissions.ts` en el motor; panel y reglas de cuándo suenan en `client/src/strategy/transmissions.ts`), radio de Morse en combate y glosario para las ayudas emergentes (`GLOSSARY` en `lore.ts`). Retratos vóxel: los hace Arte | No (cada navegador recuerda lo que ha oído) |
| 5 · Más adelante | Cinemáticas cortas (apertura y finales), orígenes de comandante, movimientos de la Armonía anunciados que se pueden frenar con misiones, y expedientes sueltos | Sí |

## Decisiones tomadas (2026-10-05)

1. Los enemigos y el reloj dejan los nombres de XCOM 2 (Sectoide → Cantor, Proyecto Ascensión → la Partitura…).
2. El Bastión está en el Pirineo aragonés.
3. El final es un dilema que se decide con las dos llaves.
