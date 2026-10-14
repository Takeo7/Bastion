# Ritmo del combate: medida y propuesta

Encargo de Arturo, por Gameplay, del 14/10: cuánto tardan las acciones y el turno de la IA, dónde se va el tiempo y qué hacer.

- §1–3: la medida de la versión 0.2.3.
- §4: lo aprobado y hecho. Arturo aclaró que el turno de la IA iba **demasiado rápido**, así que el objetivo pasó de acortar a que se vea.
- §5: la medida del ritmo nuevo.

## Cómo se ha medido

- **Versión 0.2.3** tal cual (5e36b9e), compilada y servida por el servidor de pruebas de UX/UI (3360). Mac, ventana de 1440 × 900.
- **Dos escaramuzas creadas por el motor a dificultad 3,** con 6 veteranos:
  - **Saqueo** en Polígono Norte: 13 muertos al empezar, más la horda;
  - **Limpieza de noche** en Barrio Viejo: 19 muertos.
- **Seis turnos enemigos de cada una,** con la escuadra disparando, avanzando y poniéndose en vigilancia.
- **Cada evento cronometrado al animarse** en `game.ts`, con los avisos «A la vista» y las veces que la cámara cambia de objetivo. Los tiempos van de la orden (o de «Fin del turno») a que se puede volver a mandar.
- Las animaciones van por tiempo, no por fotogramas, así que los números valen igual en el PC con la 3090. Los pasos son los de hoy (0,16 s por casilla). Arte propone otros más lentos para los muertos (ver §4).

## 1. Turno enemigo

| Misión · turno | A la vista (antes → después) | Muertos que se mueven (vistos) | Total | Dónde se va |
|---|---|---|---|---|
| Saqueo · 1 | 0 → 0 | 13 (0) | **2,0 s** | carteles 1,64 |
| Saqueo · 2 | 0 → 0 | 13 (0) | **2,0 s** | carteles 1,64 |
| Saqueo · 3 | 0 → 0 | 13 (0) | **2,0 s** | carteles 1,63 |
| Saqueo · 4 | 3 → 3 | 13 (6 mov.) | **5,2 s** | carteles 1,65 · 6 movimientos 2,19 · 2 mordiscos 0,94 |
| Saqueo · 6 | 1 → 1 | 9 (0) | **1,9 s** | carteles 1,63 |
| Saqueo · 8 | 2 → 4 | 11 (6 mov.) | **11,5 s** | carteles 1,64 · 6 movimientos 3,70 · 2 avisos «a la vista» 1,48 · contacto 1,27 · mordisco 0,75 · daño 0,40 · una baja 1,27 · contagio 0,52 |
| Saqueo · 10 | 4 → 4 | 6 (4 mov.) | **9,7 s** | carteles 1,65 · 4 movimientos 2,75 · 3 mordiscos 1,97 · contacto 1,28 · aviso 0,91 · contagio 0,51 · daño 0,25 |
| Saqueo · 12 | 4 → 0 | 9 (5 mov.) | **8,9 s** | carteles 1,41 · 5 movimientos 1,74 · 3 mordiscos 1,71 · 3 daños 0,94 · dos bajas 2,10 · horda en camino 0,62 |
| Limpieza · 1 | 0 → 0 | 19 (0) | **1,8 s** | carteles 1,43 |
| Limpieza · 2 | 2 → 6 | 17 (6 mov.) | **8,5 s** | carteles 1,43 · se levanta un muerto 1,14 · 6 movimientos 2,01 · 3 avisos 1,52 · 2 mordiscos 0,96 · daños 0,53 · contagio 0,51 |
| Limpieza · 3 | 6 → 7 | 13 (5 mov.) | **7,8 s** | carteles 1,43 · grito del Chillón 0,89 · 5 movimientos 1,58 · 5 mordiscos 2,48 · daños 0,44 · aviso 0,57 |
| Limpieza · 4 | 3 → 4 | 13 (8 mov.) | **10,2 s** | carteles 1,41 · se levantan 2 muertos 2,96 (**uno sin verse: 1,75**) · 8 movimientos 2,82 · mordiscos 1,22 · baja 0,81 · aviso 0,38 · daño 0,26 |
| Limpieza · 5 | 3 → 0 | 10 (1 mov.) | **4,4 s** | carteles 1,65 · 2 mordiscos 0,97 · baja 0,80 · movimiento 0,41 |
| Limpieza · 6 | 1 → 1 | 8 (0) | **3,3 s** | carteles 1,64 · grito 1,37 |

**Lo que dicen los números:**

- **Sin nada a la vista, 1,8–2,0 s,** y casi todo son los dos carteles: «TURNO ENEMIGO» y «TU TURNO» (1,4–1,65 s por ronda).
- **Los movimientos que no se ven no cuestan nada.** Pero otros eventos fuera de la vista sí cuestan:
  - un muerto que se levanta, 1,75 s;
  - un grito, 1,45 s;
  - una explosión, 0,55 s.
- **Con 3–7 a la vista, 5–11,5 s.** Precio de cada cosa:

  | Evento | Tiempo |
  |---|---|
  | Movimiento visto | 0,34 s de media (3,4 casillas) |
  | Mordisco | 0,45–0,75 s, más 0,25–0,40 de daño |
  | Baja | 0,8–1,3 s |
  | Aviso «A la vista» | 0,55 s (hasta 0,9) |
  | Contacto de un grupo | 1,27 s |
  | Muerto que se levanta | 1,1–1,9 s |

- **La cámara salta de zombi en zombi y sigue cada paso:** 181 cambios de objetivo en 43 movimientos vistos, unos 4 por movimiento.
- **La aceleración de hoy depende de la cola entera** (`setTimeScale`: ×1,6 con más de 12 eventos en cola y ×2,5 con más de 24). El turno llega de golpe, y los movimientos que no se ven alargan la cola, así que los primeros eventos visibles van deprisa y los últimos no. El mismo movimiento de 3 casillas ha durado entre 0,17 y 0,85 s. El ritmo no es predecible.

## 2. Acciones propias

Desde la orden hasta poder volver a mandar.

| Acción | Total | Desglose |
|---|---|---|
| Mover 3 casillas | **0,63 s** | paso 0,47 · pausa 0,12 |
| Mover 8 casillas (una acción) | **1,3–1,5 s** | paso 1,25–1,38 |
| Mover 10 casillas | **1,81 s** | paso 1,65 |
| Carrera (14 casillas) | **2,7 s** | paso 2,57 |
| Mover y destapar un grupo | **4,7–5,2 s** | paso · contacto 1,27 · uno o dos avisos «A la vista» de 0,5 |
| Disparo que da y no mata | **2,05 s** | disparo 0,84 · daño 0,40 · pausa 0,75 |
| Disparo que mata | **3,2–3,4 s** | disparo 0,82 · daño 0,41 · baja 1,13–1,29 · pausa 0,6 |
| Disparo fallado | **≈1,5 s** (estimado) | disparo 0,66 (con «falla» 0,35) · pausa 0,6 |
| Recargar | **0,84 s** | recarga 0,61 |
| Granada de mano que mata | **4,57 s** | explosión 1,79 · dos daños 0,82 · baja 1,27 |
| Lanzagranadas (rompe la ocultación) | **3,35 s** | explosión 1,74 · daño 0,41 · cartel 0,80 |
| Tajo (con acercamiento de 7–8 casillas) | **3,3–4,35 s** | acercarse 1,2–1,4 · golpe 0,73–0,76 · daño 0,41 · baja 1,29 si mata |
| Registrar | **≈1,6 s** | registro 0,89 · pausa 0,7 |
| Abrir una puerta | **≈0,6 s** (por código: no había ninguna al alcance) | sonido y mapa; no hay animación del soldado |
| Ponerse en vigilancia | **0,69 s** | 0,46 |
| **Disparo de vigilancia que salta** | **≈3,5 s** (2 casos) | **anómalo:** un disparo normal son 0,5–0,8 s, y las esperas fijas de `playAttack` no pasan de 1 s. Hay que mirarlo con Arte (el giro, el alzar el arma o la animación del que se movía). |

## 3. Tiempo muerto

1. **Los dos carteles de cada ronda,** 1,4–1,65 s aunque no se haya visto nada.
2. **La espera tras cada baja,** 1,1–1,3 s. En un turno con varias bajas se suman.
3. **Destapar un grupo encadena avisos:** el contacto (1,27 s) y uno o dos «A la vista» (0,5–0,9 s cada uno), más de 2 s seguidos.
4. **Eventos fuera de la vista que esperan igual:** levantarse, gritar y explotar (0,55–1,75 s).
5. **La cámara va y vuelve:** sigue cada paso de cada zombi, uno detrás de otro, por todo el mapa.
6. **El disparo de vigilancia,** unos 3,5 s.

Cada acción propia acaba con una pausa (0,6–0,75 s tras disparar o dañar; 0,12 s tras mover). Esa sí sirve: deja ver el resultado antes de devolver el mando.

## 4. Lo aprobado (hecho en `game.ts`, `tween.ts`, `camera.ts`, `settings.ts`, `settingsPanel.ts`, `hud.ts`)

Arturo: «el turno de la IA va muy rápido» y «todos corren demasiado». Cada acción enemiga tiene que verse entera, como en XCOM 2. Los pasos los pone Arte (`gaits.ts`); las muertes, sus clips.

| Momento | 0.2.3 | Ahora («Normal») |
|---|---|---|
| Aceleración por largo de cola | ×1,6 con más de 12 eventos, ×2,5 con más de 24 | **fuera** |
| Antes de que actúe un enemigo a la vista | nada: sale a la vez que la cámara | pausa tras el anterior (`BETWEEN` 0,5 s, o lo que falte de leer el resultado); la cámara va a él si está a más de 5 casillas y se asienta; quieto `SETTLE` 0,35 s |
| Muertos de un mismo grupo que andan en el mismo tramo | uno detrás de otro, con la cámara saltando | **juntos** (dos o más del mismo grupo): un encuadre para todos, quietos 0,35 s, y andan a la vez. Los ataques, después, uno a uno |
| Resultado de un ataque enemigo | 0,25–0,4 s y lo siguiente | `READ_HIT` 0,9 s (fallo: `READ_MISS` 0,7) antes de cualquier otra cosa |
| Fuera de la vista | 0, salvo levantarse o explotar (0,55–1,75 s) | 0 siempre |
| Turno enemigo sin nada a la vista | dos carteles | solo «TU TURNO» |
| La cámara y el jugador | el turno enemigo la retomaba en cada evento | si el jugador la mueve, el turno la deja 2,5 s; nunca vuelve atrás si el siguiente está cerca |
| Acciones propias | número 0,4 + 0,75 s; fallo 0,6; al mover 0,12 | número 1,0 s; fallo 0,8; al mover 0,25 |
| Bajas, contacto y avisos «A la vista» | — | igual (los marca la animación) |

**Velocidad del combate** (Ajustes):
- **Opciones:** Lenta (todo ×1,4), Normal y Rápida (×0,6).
- **Barra espaciadora:** mantenida durante el turno enemigo, va en «Rápida». Al soltarla, vuelve lo elegido.
- **En pantalla:** el botón de terminar turno dice «TURNO ENEMIGO · Espacio: más rápido».
- **Cómo funciona:**
  - `tween.ts` suma el tiempo de cada fotograma al ritmo del momento, así que pulsar o soltar actúa a mitad de una animación;
  - los cuerpos lo siguen en `unitViews.ts` (Arte), y los efectos y la cámara, en `game.ts`;
  - fuera del combate todo va a ritmo normal.

## 5. El ritmo nuevo, medido

Las mismas dos misiones (Saqueo de dificultad 3 y Limpieza de noche de dificultad 3, creadas con la misma semilla), en «Normal», con los pasos de Arte, y la misma pauta: disparar a lo que se ve, recargar, vigilar y avanzar.

**Turno enemigo** (por orden de ronda; entre paréntesis, a la vista antes y después):

| Misión | 0.2.3 | Ahora |
|---|---|---|
| Saqueo, sin nada a la vista | 1,9–2,0 s | **1,3 s** |
| Saqueo, 2 a la vista | — | 6,2 s · 11,9 s |
| Saqueo, 3 (3→3) | 5,2 s | **14,5 s** |
| Saqueo, 6 a la vista (6→0) | — | 18,2 s |
| Saqueo, 4 a la vista | 8,9–11,5 s | — |
| Limpieza, sin nada a la vista | 1,8 s | **1,3 s** |
| Limpieza (2→6) | 8,5 s | **26,5 s** |
| Limpieza (6→7) | 7,8 s | **29,8 s** |
| Limpieza (3→4) | 10,2 s | **30,6 s** |
| Limpieza (3→0) | 4,4 s | 10,4 s |
| Limpieza (1→1) | 3,3 s | 6,3 s |

**Dónde se va ahora un turno de ~30 s:**

| Parte | Tiempo |
|---|---|
| Carteles | 2,0 s |
| Presentar a cada enemigo (pausa, cámara y quieto) | 0,9–1,3 s cada uno |
| Grupos de muertos andando juntos | 3–6 s por grupo |
| Muertos que andan solos | 2–3,4 s cada uno |
| Mordiscos | ~0,8 s, más 0,9 s de lectura |
| Muerto que se levanta | ~3 s |
| Avisos «A la vista» | 0,9 s |

Los grupos que no se ven no tardan nada (15–25 ms).

**Acciones propias:**

| Acción | 0.2.3 | Ahora |
|---|---|---|
| Mover 3 casillas | 0,63 s | 1,7 s |
| Mover 8 casillas (una acción) | 1,3–1,5 s | ~4 s |
| Carrera de 10 casillas | 1,8 s | 2,7 s |
| Disparo que da | 2,05 s | 2,5 s |
| Disparo que mata | 3,2–3,4 s | 3,3–3,5 s |
| Recargar | 0,84 s | 0,84 s |
| Ponerse en vigilancia | 0,69 s | 0,69 s |

**Vídeos** (`.dev-snapshots/ritmo/`, solo la vista 3D, sin el HUD):
- `ritmo-saqueo.mp4`: 30 s del Saqueo;
- `ritmo-limpieza-noche.mp4`: 39 s de un turno enemigo de la Limpieza, con 4–6 a la vista.

**Con el orden nuevo de la IA de los muertos** (Gameplay, `onlyWalks` en `ai.ts`: los que solo andan actúan primero), la misma Limpieza de noche:

| Turno | Antes del orden nuevo | Con él | Grupos a la vista |
|---|---|---|---|
| Sin nada a la vista | 1,3 s | 1,3 s | — |
| 2→6 | 26,5 s | 26,2 s | 6, 3, 3 |
| 6→7 | 29,8 s | 28,6 s | 2, 3 |
| 3→4 | 30,6 s | 31,1 s | 6, 2, 3 |
| 3→0 | 10,4 s | 10,4 s | — |
| 2→2 | 6,3 s | 10,5 s | 3 |

- **Los grupos crecen** (hasta 6 muertos a la vez), pero **el total casi no cambia**: lo que anda en grupo ya iba deprisa.
- **En un turno de ~31 s:**

  | Parte | Tiempo |
  |---|---|
  | Carteles | 2 s |
  | Grupos andando juntos (4) | 8,9 s |
  | Muertos que andan y luego muerden (andan solos) | 7,5 s, ~1,9 s cada uno |
  | Dos cuerpos que se levantan | 6,0 s, ~3 s cada uno |
  | Presentaciones | 2,7 s |
  | Mordiscos, daño y una baja | 3,7 s |
  | Aviso «A la vista» | 0,9 s |

  Para acortarlo sin perder lectura, los candidatos son el levantarse (3 s) y que el que anda y muerde no espere la presentación entera si ya estaba en el encuadre del grupo anterior.
- **El disparo de vigilancia** ya no tarda 3,5 s: cinco disparos de reacción medidos, entre 1,08 y 1,31 s. No se ha reproducido con la versión nueva.

**Sin repetir la presentación dentro del encuadre del grupo** (aprobado por Gameplay; `inGroupFrame` en `game.ts`): el que anda y luego muerde, o cualquiera que ya estaba en el encuadre del grupo anterior con la cámara aún allí, no vuelve a presentarse. Solo espera lo que falte de leer el último resultado.

- **Presentaciones,** en la misma Limpieza: de 3,1 a 1,4 s en el turno 2→6, y de 7,4 a 6,1 s en el 6→7.
- **El total sube igualmente** (31,7 y 32,5 s): entre medias, el zombi pasó de 0,60 a 1,02 s por casilla (`gaits.ts`, números finales), y el andar de esos turnos fue de 14 a 21 s.
- **Si se ve largo,** el mando está en los pasos, no en las pausas.
