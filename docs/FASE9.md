# Fase 9 · Se aprende haciendo

La progresión nueva. Arturo eligió el 15/10 la propuesta A de [PROGRESION-PROPUESTAS.md](PROGRESION-PROPUESTAS.md), con campaña nueva. Sustituye a las clases, las aptitudes, el árbol de ejes, los rangos por experiencia y los niveles de arma.

Las cifras son el punto de partida. Se ajustan jugando.

## 1. Las habilidades

Cuatro, de 0 a 5:

| Habilidad | Qué da por nivel | Extra |
|---|---|---|
| **Tiro** | +5 de puntería con armas de fuego | Nivel 3: +10 de crítico con armas de fuego |
| **Cuerpo a cuerpo** | +8 de puntería con el cuchillo o el machete | Niveles 3 y 5: +1 de daño cuerpo a cuerpo |
| **Medicina** | Niveles 2 y 4: las curas sanan +1 | Nivel 5: los botiquines tienen un uso más |
| **Maña** | +10 a arreglar equipos y a forzar | Nivel 2: una granada más; nivel 4: +1 de daño de los explosivos |

**Suben con el uso**, nunca con puntos. Se cuenta lo que hace cada soldado en la misión y se suma al volver (vivo):
- **Tiro:** +1 por disparo de arma de fuego que acierta y +1 por baja con arma de fuego.
- **Cuerpo a cuerpo:** +1 por golpe que acierta y +1 por baja cuerpo a cuerpo.
- **Medicina:** +2 por cada cura o contagio parado en otro y +1 en uno mismo.
- **Maña:** +1 por caja abierta o habitación registrada, +2 por equipo arreglado o alarma callada, y +1 por explosivo que alcanza a alguien.
- **Sin tope** (Arturo, 16/10): todo lo hecho cuenta. A cambio, los niveles son largos.

**Para subir de nivel** hace falta experiencia acumulada: 10, 25, 50, 85 y 130 (niveles 1 a 5). Medido con un bot en 24 misiones, quien combate gana unos 5 de Tiro por misión (uno de cada cuatro, 8 o más): el nivel 2 llega hacia la quinta misión y el 5, pasadas las veinte; una estrella, el doble de rápido.

## 2. Los talentos

En los niveles 2 y 4 de cada habilidad se elige uno de dos. Son 8 elecciones por soldado, como mucho.

| Habilidad | Nivel 2 | Nivel 4 |
|---|---|---|
| Tiro | A la cabeza / Ráfaga | Nadie pasa / Cara a cara |
| Cuerpo a cuerpo | Uno tras otro / Golpe certero | Mano de machete / Hacerse sitio |
| Medicina | Buen pulso / Espabilar | Manos de enfermero (las medicinas paran el contagio) / Pasar visita |
| Maña | Buen brazo / Sin hacer ruido | Demolición / Apaños |

- Los nombres y las líneas son de Lore (más abajo, «Textos»).
- La elección es provisional hasta «Guardar».
- Si el soldado sube en la misión, la elección le espera en el Cuartel.

**El árbol:** la estructura de la referencia. Un centro (el soldado) y cuatro troncos, uno por habilidad, con cinco anillos (los niveles). Las parejas de talentos están en los anillos 2 y 4. No hay nada escrito fuera de las casillas.

## 3. La veteranía

- Es la suma de las cuatro habilidades (de 0 a 20), con nombre:
  - Novato, de 0 a 2;
  - Fogueado, de 3 a 5;
  - Curtido, de 6 a 9;
  - Veterano, de 10 a 13;
  - Superviviente, 14 o más.
- **Vida:** 5, y +1 cada 4 puntos de veteranía.
- **Voluntad:** 40, y +3 por punto.
- **La tercera acción**, desde Veterano.
- **Reclutas:** salen con un 1 en una o dos habilidades, al azar, para que no sean todos iguales.

## 4. Lo que lleva

- **Mano:** el arma principal, si la tiene (fusil, escopeta, fusil de precisión o ametralladora).
- **Cinto:** el cuchillo o el machete, y la pistola.
- **Cinturón:** dos huecos de equipo, cualquier accesorio: granadas (dos por accesorio), humo, aturdidora, botiquín (dos usos), chaleco o tapones.
  - Nadie lleva granadas si no las tiene en el cinturón.
- **Mochila:** tres huecos (cuatro con la Chapistería).
- **Qué puede hacer** depende de lo que lleva y de sus talentos:
  - Disparar, Pistola, Tajo, Vigilancia, Recargar y Agazaparse, todos;
  - Granada, con granadas;
  - Botiquín, con botiquín;
  - las acciones de los talentos.

## 5. Las armas

- **Estado,** de 0 a 100: cada disparo gasta 1 (2 el fusil de precisión, por ráfaga la ametralladora).
  - Por debajo de 30: «Gastada», −10 de puntería.
  - A 0: «Rota», no dispara.
  - Se arregla en la Armería con piezas, 1 por cada 25 puntos.
- **Mejoras:** dos huecos por arma. Se encuentran en cajas y habitaciones y se montan en la Armería:
  - mira: +10 de puntería;
  - culata: +10 en la vigilancia;
  - cargador ampliado: +2 balas;
  - silenciador: el disparo hace la mitad de ruido.
- **Ya no hay niveles de arma** (el Taller deja de hacer armas de nivel 2 y 3).
  - El Taller sigue fabricando armas, munición y armadura (para todos).
  - Las armas buenas encontradas (fusil de cerrojo, escopeta recortada…) traen una mejora puesta.

## 6. La escaramuza

Sin campaña, cada soldado sale con un arma al azar (fusil, escopeta, fusil de precisión o ametralladora) y su equipo. Con «Veteranos», también con habilidades de 2 a 4 y sus talentos.

## 7. Partidas guardadas

Campaña nueva (Arturo, 15/10). Una partida de antes no se convierte:
- se guarda aparte (`save.antes-0.3.json`);
- el servidor empieza en el vestíbulo.

## Textos (Lore, 15/10)

Propuesta de nombres y de una línea para cada cosa. El tono es el de siempre: seco, concreto, sin heroísmo ni palabras de videojuego. La línea dice la regla; donde ayuda, va delante una frase corta del mundo. Los efectos marcados con † son nuevos o cambian: son una propuesta, y si el motor hace otra cosa, cambio la línea.

### Las habilidades

| Nombre | Corto | Línea |
|---|---|---|
| **Tiro** | TIR | Puntería con armas de fuego. Sube acertando y abatiendo con ellas. |
| **Cuerpo a cuerpo** | CaC | El cuchillo y el machete. Sube acertando y abatiendo de cerca. |
| **Medicina** | MED | Curar a otros y a uno mismo. Sube curando y parando contagios; más si es a otro. |
| **Maña** | MAÑ | Arreglar, forzar y los explosivos. Sube registrando, abriendo cajas, arreglando, apagando alarmas y con las granadas que alcanzan a alguien. |

La experiencia, en la ficha: «Tiro 2 · 11/15», y debajo, al volver de una misión: «+3 de Tiro: dos aciertos y una baja».

### Los talentos

| Habilidad | Nivel | Provisional | Propuesto | Línea |
|---|---|---|---|---|
| Tiro | 2 | Disparo apuntado | **A la cabeza** | Un disparo con −25 de puntería que hace la mitad más de daño. Enfriamiento 3. |
| Tiro | 2 | Ráfaga | **Ráfaga** | Dos disparos seguidos, cada uno con −15 de puntería. Gasta dos balas. Enfriamiento 3. |
| Tiro | 4 | Zona de muerte | **Nadie pasa** | Vigilancia que dispara a cada enemigo que se mueva a la vista, una vez a cada uno. Enfriamiento 4. |
| Tiro | 4 | Cara a cara | **Cara a cara** | Un disparo de pistola a cada enemigo a la vista. Enfriamiento 4. |
| Cuerpo a cuerpo | 2 | Implacable | **Uno tras otro** | Abatir con el Tajo devuelve una acción, una vez por turno. |
| Cuerpo a cuerpo | 2 | Golpe certero | **Golpe certero** | +20 de crítico con el Tajo. |
| Cuerpo a cuerpo | 4 | Maestro del machete | **Mano de machete** | Con el machete, el Tajo hace +2 de daño y gana +15 de puntería. |
| Cuerpo a cuerpo | 4 | Torbellino | **Hacerse sitio** † | Un Tajo a cada enemigo que tenga al lado. Enfriamiento 3. |
| Medicina | 2 | Médico de campo | **Buen pulso** | El botiquín y las medicinas curan +1. |
| Medicina | 2 | Espabilar | **Espabilar** † | Con una acción, quita a un compañero de al lado el aturdimiento, la desorientación o el pánico. Sin botiquín. |
| Medicina | 4 | Manos de enfermero | **Manos de enfermero** | Las medicinas también paran el contagio de un mordido. |
| Medicina | 4 | Restauración | **Pasar visita** | Cura 2 a cada compañero a 4 casillas o menos y les quita los estados alterados. Una vez por misión. |
| Maña | 2 | Granadero | **Buen brazo** † | Las granadas llegan 3 casillas más lejos, y lleva una más en cada accesorio de granadas. |
| Maña | 2 | Cerrajero | **Sin hacer ruido** † | Registrar, abrir cajas y forzar no hacen ruido. |
| Maña | 4 | Demolición | **Demolición** | Los explosivos hacen +1 de daño y su radio crece una casilla. |
| Maña | 4 | Técnico | **Apaños** † | Una vez por misión, con una acción, arregla 40 de estado del arma propia o de un compañero de al lado. |

Por qué:
- **A la cabeza:** es lo que se dice de los zombis; «Disparo apuntado» no dice nada que no diga el botón.
- **Nadie pasa** y **Uno tras otro:** dicen lo que pasa en el tablero. «Zona de muerte» e «Implacable» eran nombres de videojuego.
- **Mano de machete:** «Maestro» sonaba a título. Ahora nadie es maestro de nada: es alguien que ha usado mucho el machete.
- **Hacerse sitio** y **Pasar visita:** «Torbellino» y «Restauración» eran de fantasía. Pasar visita es la ronda del médico por las camas.
- **Buen brazo:** «Granadero» era el nombre de una clase que desaparece, y confundiría.
- **Sin hacer ruido:** «Cerrajero» solo hablaba de forzar. El ruido es lo que importa en este juego.
- **Apaños:** con el estado de las armas (§5), lo útil es arreglar en la misión. «Técnico» no decía qué arreglaba.
- **Espabilar:** el nombre se queda, pero ya no es del Grillo, que desaparece con las clases.

### La veteranía

| Suma | Nombre | Línea |
|---|---|---|
| 0–2 | **Novato** | Todavía no ha visto lo peor. |
| 3–5 | **Fogueado** | Ya ha salido y ha vuelto. |
| 6–9 | **Curtido** | Sabe cuándo no disparar. |
| 10–13 | **Veterano** | Tercera acción por turno. Los demás le siguen sin que lo pida. |
| 14+ | **Superviviente** | Ha vuelto de todo. |

Por qué: «Leyenda» era épico, y aquí nadie es una leyenda. En este mundo, lo más que se puede decir de alguien es que sobrevive, así que **Superviviente** va arriba, y su sitio de antes (3–5) lo ocupa **Fogueado**. La vida y la voluntad que da cada punto van en la ayuda de la ficha, no en el nombre.

### Las armas

**Mejoras** (dos huecos por arma):

| Nombre | Línea |
|---|---|
| **Mira** | +10 de puntería. |
| **Culata** | +10 de puntería en vigilancia: el arma apoyada no tiembla. |
| **Cargador ampliado** | Dos balas más en cada cargador. |
| **Silenciador** | El disparo hace la mitad de ruido. Casero: un tubo, arandelas y cinta. |

«Cargador grande» pasa a **Cargador ampliado**, que es como se dice.

**Estado** (de 0 a 100, en la ficha del arma: «Estado 64»):

| Estado | Nombre | Línea |
|---|---|---|
| 30–100 | (sin nombre: solo el número) | — |
| 1–29 | **Gastada** | −10 de puntería. Pide piezas. |
| 0 | **Rota** | No dispara. Se arregla en la Armería. |

En la Armería, el botón: «Arreglar: 1 pieza cada 25». Al volver de una misión, si alguna se ha gastado: «La escopeta de Ana vuelve gastada.»

## 8. Quién hace qué

- **Gameplay:** el motor y sus tests (habilidades, lo que se cuenta en misión, talentos, veteranía, cinturón, estado y mejoras de las armas, escaramuza y el paso a campaña nueva).
- **UI:**
  - la ficha del Cuartel (habilidades, experiencia hasta el siguiente nivel, veteranía, talentos pendientes);
  - el árbol nuevo con «Guardar»;
  - el cinturón;
  - la Armería con estado, arreglar y mejoras;
  - lo que se ha aprendido en el parte de la misión.
- **Lore:** los nombres y textos de las habilidades, los talentos, la veteranía, las mejoras y los estados del arma.
- **Arte:**
  - las mejoras en el arma, si se ve, como la mira o el silenciador;
  - que la ropa y el aspecto no dependan de la clase.

Va en la **0.3.0**. Antes sale la 0.2.9 con las notas de la prueba del 15/10.

## 9. Estado (16/10)

**El motor está hecho** (Gameplay, con 417 pruebas):
- `skills.ts` lleva las habilidades, la experiencia, los talentos, la veteranía, los números del soldado (`fighterStats`), los reclutas y los veteranos de escaramuza.
- Lo que hace cada soldado en la misión lo cuentan los eventos (`Unit.deeds`).
- El soldado de campaña lleva `skills` y `picks`; al volver aprende, y el parte dice qué (`learned`, `deeds`, `levels`).
- El cinturón, el estado de las armas, las mejoras y la Armería (arreglar y montar) también están.
- La campaña de antes se aparta al arrancar (Red) y el vestíbulo lo dice.

**Decisiones tomadas por el camino:**
- **Talentos:** lo guardado queda fijo y no hay «Reentrenar».
- **Cinturón:** son los dos huecos de antes (`loadout.utility` y `loadout.ammo`); cualquier accesorio va en cualquiera, y se puede repetir. La munición especial deja de existir: la sustituyen las mejoras.
- **El Taller:**
  - «Armas de taller» (nivel 3) deja hacer culatas y silenciadores, y «Armas militares» (nivel 6), miras y cargadores ampliados. Se hacen una y otra vez.
  - Las armas de nivel 2 y 3 ya no se hacen. La armadura sigue, y es para todos.
- **Armas encontradas:** llegan con un estado de 40 a 90. Las buenas traen su mejora: el fusil de cerrojo, culata; la recortada y la ametralladora ligera, cargador ampliado; el fusil con mira, mira.
- **Base de puntería:** 60. Tiro suma con armas de fuego y Cuerpo a cuerpo con el cuchillo y el machete.
- **Heridas:** 2 días más 2 por punto de vida perdido.
- **El Camposanto a nivel 3:** un punto más de lo que más se usó en la misión.
- **El centro de instrucción:** los reclutas llegan con uno o dos niveles más.

**Hecho también:**
- UI: la ficha del Cuartel, el árbol con «Guardar», el cinturón, la Armería (estado, arreglar, mejoras) y el parte;
- Lore: los textos, en femenino donde toca («Novata»…), y la frase del arma gastada;
- Arte: la mira y el silenciador sobre el arma, el arma gastada o rota, y la ropa sin clase;
- fuera del motor: las clases, las aptitudes, el árbol viejo, los rangos (`classes.ts`, `RANKS`), los niveles de arma y la migración de las campañas de antes;
- el Taller de oficios hace que cada misión enseñe un 20 % más por nivel, y el Camposanto a nivel 3, otro 20 % tras una misión con bajas.

**El código muerto del árbol viejo, quitado el 19/10** ([REVISION-2026-10-19.md](REVISION-2026-10-19.md)): las habilidades del Grillo y del Oficio (con la Bengala y el Cóctel molotov), sus ventajas (`PerkId` se queda con las 28 del tablero) y los vuelos del dron, en el motor y en el cliente. La plantilla `specialist` se queda, porque la usan los tests.
