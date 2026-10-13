# Propuesta de Lore tras la prueba del 13/10

Respuesta a los puntos 23–27 de [PRUEBA-2026-10-13.md](PRUEBA-2026-10-13.md).

**Aprobado por Arturo el 13/10:** la premisa de la estación militar, los nombres internacionales de soldados y personajes, todos los cambios de nombre de la interfaz y también los lugares, con nombres inventados y neutros (§2, «Lugares»).

## 1. La premisa: quiénes somos

Una tarjeta al abrir la campaña, antes de la primera misión. Cinco líneas y ninguna más:

> Hace dos inviernos, la Pandemia vació el valle de Valdesombra. Los muertos se levantaron y siguen andando.
>
> Atalaya, una vieja estación militar en la sierra de Altomonte, aguanta con cuarenta supervivientes llegados de todas partes.
>
> Vosotros dos la dirigís. Lo que importa se decide con las dos llaves.
>
> Atalaya vive de lo que sus equipos traen de fuera. El primero sale hoy.

Qué responde:

- **¿Quién soy?** Uno de los dos que dirigen un refugio. No sois militares de carrera: os eligieron para mandar.
- **¿Por qué hay rangos y soldados?** Atalaya era un puesto militar, y quienes llegaron adoptaron su orden: equipos, rangos y armería. Así no hay que cambiar nada de la progresión.
- **¿Por qué nombres de todas partes?** Puerto Galena era un puerto, y el Instituto Galena un centro de investigación internacional. Además, el valle fue camino de huida hacia la sierra.
- **¿Por qué luchamos?** Para comer, para curar y para que Atalaya siga en pie. La Marea y la vacuna llegan después, con la historia.

## 2. Nombres internacionales

### Soldados

Un pool nuevo de 36 nombres, mitad mujeres y mitad hombres, de muchos países. Cada nombre lleva su sexo, para que el modelo de Arte coincida:

```ts
export const SOLDIER_NAMES: { name: string; sex: 'f' | 'm' }[] = [
  { name: 'Amara Okafor', sex: 'f' }, { name: 'Kofi Mensah', sex: 'm' }, …
];
```

- **Para Gameplay y Arte:** el soldado guarda `sex` al nacer, y Arte · Personajes elige el modelo por ese campo, no por el nombre. Es tarea de Gameplay (el estado, con migración: a los soldados que ya existen se les asigna por la lista vieja) y de Arte.
- **Mujeres:** Amara Okafor, Yuki Tanaka, Leila Mansour, Sofia Marin, Priya Nair, Anja Kowalski, Elena Petrova, Nadia Benali, Maya Cohen, Chiara Rossi, Freya Lund, Ana Ríos, Zainab Bello, Hana Novak, Lucía Ortega, Ingrid Berg, Mariana Costa, Aisha Diallo.
- **Hombres:** Kofi Mensah, Jonas Becker, Arjun Mehta, Dmitri Volkov, Mateo Rojas, Hiro Sato, Omar Saleh, Liam O'Brien, Pierre Dubois, Ivan Horvat, Rafael Costa, Erik Nilsson, Samuel Adeyemi, Wei Zhang, Luca Bianchi, Marcus Reid, Tariq Aziz, Pablo Rey.

Quedan algunos nombres españoles (Ana Ríos, Lucía Ortega, Pablo Rey): el valle está en un país de habla hispana, pero la gente viene de todas partes.

### Personajes de la radio y de la historia

| Hoy | Propuesta | Por qué |
|---|---|---|
| Inés Albarrán, «Morse» | **Nadia Albescu, «Morse»** | Rumana. Se queda el apodo. |
| Teodoro Vidal, «Teo», «el Relojero» | **Teodor Vidić, «Teo»** | Croata. Se queda Teo y se quita «el Relojero». |
| Dra. Amara Nwosu | Se queda | Ya es internacional. |
| Simón Ferreira | **Simão Ferreira** | Portugués. |
| Ruth Halvorsen | Se queda | |
| Celia Arranz, «la Voz» | **Celia Marsh, «la Voz»** | Locutora; hacía el programa de la tarde. |
| Lucía Prats, la niña inmune | **Noor Haddad** | |
| Elena Ibarra, la que vendió su refugio | **Vera Lindqvist** | |
| Lola Haskins y Mei Takahara | Se quedan | |
| Ramiro Ostalé, la voz de la estación de números | **Otto Brandt** | |
| Tomás Rubio, el ayudante de Nwosu | **Tomasz Rybak** | |
| Sospechosos del topo | **Pavel Lanz, el electricista**; **Rosa Alves, la cocinera**; **Bruno Sauer, el de la radio** | |
| VIP de rescate | Dra. Irina Sokolova, Ing. Kenji Mori, alcaldesa Mara Velasco, Dr. Femi Adebayo, agente Claire Dubois | |
| Familiares («Caras conocidas») | Mariam, Tomás, Ewa, Andrés, Lucía, Kenji | Con su parentesco. |

### Lugares (decidido: inventados y neutros)

| Hoy | Nuevo |
|---|---|
| el valle de Valdesombra | **el valle de Kessra** |
| la sierra de Altomonte (`weu`, casa) | **la sierra de Aldren** |
| Ciudad Varela (`afr`) | **Corran** |
| Valdelobos (`eeu`) | **Volkren** |
| Puerto Galena (`me`) y el Instituto Galena | **Galen** y el **Instituto Galen** |
| Carboneda (`sa`) | **Carvik** |
| San Telmo (el pueblo de las campanas) | **Telm** |
| El Pantano Gris, Los Llanos de Ceniza, La Vega | Se quedan |
| Atalaya | Se queda |

Los grupos (los Cuervos, la Congregación, los Camioneros, el Sindicato de la Mina) también se quedan. Para el pool de soldados, «Leila Haddad» pasa a «Leila Mansour», para no repetir apellido con Noor Haddad.

## 3. Nombres de la interfaz

| Hoy | Propuesta | Notas |
|---|---|---|
| Geoesfera | **El valle** | La pestaña y el mapa. |
| Expediente (pantalla e informes de historia) | **Diario** | El diario de Atalaya, que escribe Morse: las hojas de historia, la progresión y los informes. El titular «EXPEDIENTE · HOJA N» pasa a «DIARIO · HOJA N». |
| Bastión | Se queda | Es el resumen. |
| Armería, Cuartel, Instalaciones | Se quedan | |
| Hangar | **Cochera** | La Mula es un autobús. |
| ESCANEAR / ESCANEANDO… | **ESPERAR NOTICIAS / A LA ESCUCHA…** | «Los días solo pasan al esperar noticias y en las misiones.» «X quiere esperar noticias.» |
| «días de escaneo» (rumores) | **«días a la escucha»** | |
| Actividad zombi N/12 | **Infestación N/12** | Antes de saber de la Marea. Desde el nivel 4 sigue «La Marea, a N km». La alternativa de Ana, «avance de la Pandemia», no encaja porque la Pandemia ya pasó. |
| Sala de simulación | **Sala de mapas** | Lo de «simulación» suena a ciencia ficción. |
| «Operación Ceniza Rota» (nombres de misión) | **Nombre de lugar**: «La gasolinera de Valdelobos», «El colegio de Carboneda» | Los nombres de operación al azar son de XCOM. Uno de lugar dice adónde se va y por qué. |
| Suministros §, Medicinas ✚, raciones, piezas | Se quedan | |
| Rangos (Novato … Coronel) | Se quedan | La premisa los justifica: Atalaya fue un puesto militar. |

## 4. La apertura

Con la campaña empezando por una misión (punto 5 de la prueba):

1. La tarjeta de la premisa (§1).
2. El informe de la primera misión, que se queda como hoy: las medicinas del consultorio, antes de que caduquen.
3. La misión.
4. Al volver, **una** transmisión corta: Morse y Teo se presentan en una línea cada uno y dicen lo que falta (medicinas, comida). Nada de normas.
5. El resto (el Consejo, las medicinas y la comida) entra cuando importa: la primera fiebre, la primera hambre, el primer convoy.

## 5. El tono (después de aprobar esto)

- **Reglas para todos los textos:**
  - frases cortas y declarativas;
  - nada de explicar lo que se ve;
  - nada de chistes ni de guiños;
  - los personajes hablan como adultos cansados;
  - la regla, en la línea pequeña del consejo, nunca en el diálogo.
- **Orden de reescritura:**
  1. Menú y apertura.
  2. Primera misión, con su informe y su parte.
  3. Base: avisos, agenda y ayudas.
  4. Radio y consejos.
  5. Encargos y acontecimientos.
  6. Expediente (Diario) y fichas.
