# Revisión de UX: menús y base

**Estado (2026-10-06):** aplicada. Gameplay hizo 1, 2, 4, 7, 8 y `nextSteps` (9 y 13); Arte, las pantallas (3, 5, 6, 9, 11–20 y P3); Lore, 10, la jerga de 14 y el ritmo de 21 (la radio es un panel, no una ventana). El panel de radio va abajo a la derecha (arriba en el centro en la geoesfera) y los avisos salen de uno en uno, con el resto en «Novedades».

Revisión del 2026-10-06, hecha recorriendo el juego como un jugador nuevo, con una ventana de 1000 × 663 (la del usuario): menú principal → campaña nueva → base → geoesfera → informe → escuadra → misión → informe de vuelta → cuartel, armería, progreso e instalaciones → escanear.

## La regla

Cada pantalla tiene que responder a tres preguntas, en este orden y de un vistazo:

1. **¿Qué tengo que hacer ahora?** Una sola acción principal, la más visible.
2. **¿Qué puedo hacer además?** Acciones secundarias, discretas.
3. **¿Qué pasa en el mundo?** Información de contexto, compacta. El detalle va al pasar el ratón.

Lo que no responde a ninguna de las tres sobra en pantalla.

## Problemas, por prioridad

### P1 · Te bloquean o te llevan a error

| # | Dónde | Qué pasa | Propuesta | Quién |
|---|---|---|---|---|
| 1 | Menú principal | Para el compañero muestra `http://localhost:3310`, que en su ordenador no sirve. | Mostrar las direcciones de red y Tailscale que el servidor ya imprime en la consola. | Gameplay (servidor → cliente) + Arte |
| 2 | Menú principal | Si empiezas la campaña antes de que entre tu compañero, se crea **en solitario**: el compañero que entre después no tiene soldados. Nada lo avisa. | Avisar antes de crearla («Tu compañero no está: la campaña será para uno»). Y, mejor, que un compañero que entra después reciba su mitad de soldados. | Gameplay + Arte |
| 3 | Base · botón «Menú» | Manda a **los dos** jugadores al menú principal, sin confirmar. | Confirmar y decir que es para los dos. | Arte (+ Gameplay si hace falta otra orden) |
| 4 | Escaramuza terminada | No hay forma de volver al menú principal: solo «Otra igual» y «Misión al azar». | Añadir «Volver al menú». | Gameplay (servidor) + Arte |
| 5 | Geoesfera | **ESCANEAR** es el botón más grande y rojo aunque haya operaciones esperando. Escanear deja pasar los días: las operaciones caducan y el Proyecto avanza. | Con operaciones disponibles, ESCANEAR pasa a segundo plano y la acción principal es elegir una. Si se escanea igualmente con una operación a punto de caducar, avisar. | Arte |
| 6 | Geoesfera · informe | Con una operación seleccionada compiten dos botones rojos: DESPLEGAR y ESCANEAR. | DESPLEGAR es la única acción principal; ESCANEAR se oculta mientras hay un informe abierto. | Arte |
| 7 | Cuartel (en solitario) | Empiezas con 8 soldados y el máximo es 6: «Reclutar (8/6) · El cuartel está lleno» desde el primer día. | El máximo debe contar con los soldados de inicio (en solitario, 12). | Gameplay |
| 8 | Escuadra | Empieza **vacía**: hay que meter a los soldados uno a uno. | Rellenarla con los mejores soldados listos de cada jugador; tú solo cambias a quien quieras (como XCOM). | Gameplay |
| 9 | Base al entrar | No dice qué hacer. «Estado» lista el nivel y «3 operaciones», y «Pulsa una sala para entrar». | Un bloque **«Ahora»** con la siguiente acción y un botón: «Elige una operación (3)», «Ascenso pendiente: Lucía Prat», «Sala vacía: construye algo»… | Gameplay (función `nextSteps`) + Arte |
| 10 | Aviso de rumor | «Si lo escuchamos unos días, sabremos…» y solo «Entendido»: no dice cómo escucharlo. | Botón «Escuchar» (orden `investigate`) o «Ver en la geoesfera». | Lore + Arte |

### P2 · Saturan o se leen mal

| # | Dónde | Qué pasa | Propuesta |
|---|---|---|---|
| 11 | Barra superior | «₡200», un «1» suelto con una barra vacía y 12 casillas rojas, todo sin rótulo. Un jugador nuevo no sabe qué son. | Icono más rótulo corto: «₡ 200», «Nivel 1» con su barra, «Actividad enemiga 3/12» (el término de Lore). El detalle, al pasar el ratón. |
| 12 | Base | Cada hueco tiene su etiqueta («Roca», «Sala vacía»): diez etiquetas iguales. | En la vista general, etiquetas solo en las salas de mando y en las construidas; «Roca» y «Sala vacía» al pasar el ratón. |
| 13 | Barra de navegación | Ninguna pestaña avisa de que hay algo pendiente. | Marcas con número: Geoesfera (operaciones nuevas), Cuartel (ascensos), Instalaciones (obra terminada), Progreso (nivel nuevo). Mismo origen que el bloque «Ahora». |
| 14 | Geoesfera | Las secciones vacías ocupan sitio («Órganos: Ningún Órgano conocido»), y la jerga no se explica. | Ocultar las secciones vacías; explicar la jerga al pasar el ratón («Órgano: instalación del Coro»). |
| 15 | Geoesfera | Los dos paneles tapan el globo a 1000 px. | Con un informe abierto, plegar la lista de la izquierda. |
| 16 | Escuadra | Repite el informe entero, ya leído en la geoesfera, en el centro. | Una línea: nombre · tipo · objetivo. El texto completo, al pasar el ratón. |
| 17 | Vuelta de la misión | Dos pantallas seguidas que dicen casi lo mismo: el resultado en combate y luego el informe en el hangar. | El resultado en combate, breve («Misión fracasada» + «Volver a la base»); los números, solo en el informe del hangar. |
| 18 | Cuartel · ficha | Muy larga: Accesorios queda por debajo del pliegue. Requisitos en frases («Armas de bobina: requiere nivel 3 del Bastión y rango Cabo»), «Misiones/Bajas» siempre a la vista, «Cada rango da +5 de puntería». | Arma y armadura con su icono de nivel y el requisito en una etiqueta corta («Nv 3 · Cabo»). Misiones y bajas al pasar el ratón por el nombre. Quitar «Cada rango da…». Accesorios justo debajo del arma. |
| 19 | Armería | Se abre en «Armas y armadura», que es solo informativa (las armas no se compran). | Abrir en «Accesorios», donde se puede hacer algo. |
| 20 | Instalaciones | Sin nada seleccionado solo hay instrucciones, y hay que encontrar las salas en 3D. | Lista de las salas con su estado y su acción (Excavar ₡25 · Construir…), sincronizada con el clic en 3D. |
| 21 | Avisos encadenados | Al escanear pueden salir varios seguidos con «Siguiente (n)», más los acontecimientos y ahora las transmisiones de Lore. | Una sola ventana por vez de vuelta a la base o por escaneo. El resto, en una bandeja de «Novedades» que se abre cuando quieras. Las transmisiones, cortas y que se salten con un clic. |

### P3 · Pulido

- Menú principal: la hoja ocupa toda la altura con renglones vacíos y tapa media Tierra. Podría ser más baja.
- Selección de escuadra: «Libre · Elige a la izquierda» en cada hueco sobra si la escuadra viene rellena.
- Informe de misión: el sello «CLASIFICADO» sale cortado en el borde del panel.
- Geoesfera: «6 d» sin unidad clara; mejor «6 días» o un icono de reloj.

## Qué debería verse en cada pantalla

| Pantalla | Acción principal | A la vista | Al pasar el ratón | Quitar |
|---|---|---|---|---|
| Menú principal | Continuar / Nueva campaña | Opciones, compañero conectado y su dirección | Explicación de cada modo | — |
| Base (vista general) | Lo que diga «Ahora» | Salas de mando, obras, bloque «Ahora» | Nombre de cada hueco, efectos activos | «Pulsa una sala para entrar», etiquetas repetidas |
| Geoesfera | Elegir operación (o Escanear si no hay) | Operaciones con días que quedan, contactos | Jerga, detalle de regiones | Secciones vacías |
| Informe de operación | Desplegar | Nombre, tipo, dificultad, recompensa, texto | — | ESCANEAR |
| Escuadra | Lanzar | Soldados formados con su ficha mínima | Informe completo | Texto del informe repetido |
| Informe de vuelta | Continuar | Resultado, quién cae o asciende, XP, créditos | Detalle por soldado | — |
| Cuartel | Ascender (si hay) / Personalizar | Soldado en el pedestal, salud, puntería, arma, armadura y accesorios | Misiones, bajas, requisitos completos | Frases de ayuda |
| Armería | Comprar | Accesorios con precio y existencias | Descripción | — |
| Progreso | — (consulta) | Nivel, barra, lo que trae el siguiente nivel | Niveles lejanos | — |
| Instalaciones | Excavar / Construir | Salas con su estado y su acción | Efecto de cada instalación | Instrucciones largas |

## Reparto

- **Gameplay** (lógica y servidor): 1 (direcciones de red), 2 (compañero tardío y aviso), 4 (volver al menú desde escaramuza), 7 (límite de soldados), 8 (escuadra rellena), 9 y 13 (`nextSteps(c, slot)`: lo pendiente para cada jugador, que usan el bloque «Ahora» y las marcas de la barra).
- **Arte** (pantallas): 3, 5, 6, 11, 12, 14 a 20, la parte visual de 9 y 13, y P3.
- **Lore** (textos y transmisiones): 10, la jerga de 14 y el ritmo de 21.
