# Textos pendientes para Lore

Textos provisionales que han puesto Gameplay, Reglas y Combate mientras la sesión Lore no estaba abierta. Lore los revisa y los cambia en su sitio; el archivo de cada uno va entre paréntesis. Cuando estén revisados, se borra la línea.

## Noche ([ALTURA-Y-NOCHE.md](ALTURA-Y-NOCHE.md))

- Etiqueta: «De noche» (`NIGHT_LABEL`, `setup.ts`).
- Línea de radio la primera noche: «Noche cerrada. Se ve poco: id pegados a las farolas o llevad bengalas.» (`game.ts`).
- Regla en el registro: «De noche: los vivos ven 8 casillas menos (6 como mínimo) y os detectan a la mitad de distancia. A la luz de una farola, una bengala o un fuego os ven como de día.» (`game.ts`).
- «La bengala ilumina la calle» y «El fuego ilumina la zona» (`game.ts`).
- Nota de casilla: «A la luz: os ven de lejos» (`game.ts`).
- Menú de escaramuza: «Hora», «De día · Como siempre», «De noche · Se ve poco: farolas y bengalas. Más muertos y más botín» (`lobby.ts`).
- Falta:
  - la línea «De noche» del briefing;
  - la línea del parte nocturno (`report.night`).

## Contagio y medicinas ([FASE3.md](FASE3.md))

- Causa en los Caídos: «Se convirtió» (`TURNED_CAUSE`, `campaign.ts`).
- Aviso al convertirse:
  - titular «Enfermería» y título «X se ha convertido»;
  - registro: «X no ha superado la fiebre: se ha convertido.» y « Descansa en el Camposanto.» (`campaign.ts`).
- Registro de la base:
  - «X vuelve con un mordisco: tiene fiebre.»
  - «X recibe el tratamiento: le baja la fiebre.»
  - «X recibe medicinas: 3 días menos de recuperación.» y «…y se ha recuperado.»
  - «La Vega y Ciudad Varela mandan medicinas: +2 ✚.» (`campaign.ts`)
- Agenda: «Tu compañero quiere tratar a X (2 ✚)», «X se convierte en N días» y «mañana» (`campaign.ts`).
- Errores:
  - «No está contagiado.»
  - «No hay medicinas suficientes.»
  - «Ya hay otro tratamiento propuesto: confirmadlo o rechazadlo antes.»
  - «No está herido.»
  - «No hay medicinas.» (`campaign.ts`)
- Cuartel: «Mordido · se convierte en N d», «Fiebre», «Proponer tratarle (2 ✚)», «Confirmar el tratamiento», «Rechazar», «Retirar la propuesta», «Las medicinas no llegan para todos: tratarle lo decidís los dos.», «Curar (1 ✚): 3 días menos» (`screen.ts`).
- Calendario: «X se convierte si no le tratáis» (`screen.ts`).
- Combate:
  - «MORDIDO»
  - «Han mordido a X»
  - «Mordieron a X: se levanta»
  - el cartel «¡SE LEVANTA!» con «Mordieron a X: ya no es de los vuestros» (`game.ts`)
- Nombre del que se levanta: «Zombi · Nombre» (`rules.ts`).
- Faltan los consejos de radio: el primer mordisco, el primero que se convierte y «Las medicinas no llegan para todos».

## Fase 4 · Comida, el Taller y las armas encontradas ([FASE4.md](FASE4.md))

- Nombres provisionales: «raciones» y «piezas» (los recursos), «Huerto» (`garden`).
- El Huerto (`FACILITIES.garden`, `campaign.ts`):
  - descripción: «+1 ración al día. Bancales bajo plástico y bidones para el agua de lluvia.»;
  - nivel 2: «+2 raciones al día. Un invernadero de verdad, con estufa.»;
  - nivel 3: «+3 raciones al día. Riego por goteo y semillas de La Vega.»
- Hambre (`campaign.ts`):
  - aviso con titular «Despensa» y título «Hambre en el Bastión»; registro: «No quedan raciones: el Bastión pasa hambre. Hasta que vuelva a haberlas, −10 de voluntad en las misiones y las heridas curan más despacio.»;
  - «Vuelve a haber raciones: se acabó el hambre.»;
  - «La Vega manda comida: +2 raciones.»
- Agenda (`campaign.ts`): «Pasáis hambre: −10 de voluntad y heridas lentas. Buscad raciones o un Huerto», «Quedan raciones para N días», «El Taller puede hacer armas de nivel 2».
- El Taller (`campaign.ts`, `progression.ts`):
  - proyectos (`CRAFTS`): «Armas de nivel 2», «Armas de nivel 3», «Armadura de nivel 2», «Armadura de nivel 3»;
  - propuesta: «hacer en el Taller: armas de nivel 2»;
  - registro: «El Taller se pone con las armas de nivel 2: N días.» y «El Taller ha terminado: armas de nivel 2 para todos (cada uno, hasta lo que permita su rango).»;
  - errores: «Ya está hecho.», «Ya se está haciendo.», «Hace falta el Taller.», «Hace falta el Taller a nivel N.», «Hace falta el nivel N del Bastión.», «Antes hay que hacer las armas de nivel 2.», «No hay piezas suficientes.»;
  - el siguiente nivel en la ficha (`nextTierBlocker`): «Armas de nivel 3: requiere nivel 6 del Bastión y fabricarlas en el Taller»;
  - las descripciones de las cuatro tecnologías de nivel de arma y armadura (`TECHS`, `progression.ts`), que ahora dicen que el Taller puede hacerlas.
- Desmontar Acorazados (`campaign.ts`): «Desmontáis un Acorazado: +1 pieza.» y «Desmontáis N Acorazados: +N piezas.»
- Armas encontradas (`FOUND_WEAPONS`, `content.ts`): «Fusil de caza» («un fusil de caza en buen estado»), «Escopeta recortada» («una escopeta recortada»), «Fusil con mira» («un fusil con mira telescópica»), «Ametralladora ligera» («una ametralladora ligera»).
- Armería (`campaign.ts`):
  - errores: «Esa arma no está en la Armería.», «La lleva X, de tu compañero.», «Solo puedes armar a tus soldados.», «Es para quien lleva fusil de asalto.»;
  - «Con X se pierde su fusil de caza.»
- Línea del botín (`lootLine`, `campaign.ts`): «N raciones», «N piezas» y el nombre del arma encontrada, en la misma frase que `lootSummary` (conviene llevarlo a `lootSummary`, en `lore.ts`).
- Detalle de la propuesta de fabricar (`screen.ts`, provisional): «N días de obra», «N § y N piezas».
- Faltan: los consejos de radio de la primera hambre y la primera arma encontrada.
- Hallazgos en combate (`game.ts`, provisional hasta que `searchLine` y `lootSummary` de `lore.ts` sepan de ellos): «una ración / N raciones», «una pieza / N piezas» y el arma con su `FOUND_WEAPONS[].find`. Sin eso, una ración sale como «Nada que sirva».
- Símbolos de la barra de arriba: «◍» raciones y «⚙» piezas (`screen.ts`).
