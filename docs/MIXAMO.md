# Animaciones de Mixamo

Lo que hay que bajar de [mixamo.com](https://www.mixamo.com) para las armas (nota 7 del 21/10: Arturo permite Mixamo para lo que Synty no tiene). Hace falta una cuenta de Adobe, así que las baja Arturo. Arte las convierte después.

## Cómo bajarlas

1. En **Characters**, elige **X Bot**. Todas las animaciones se bajan con ese mismo personaje.
2. En **Animations**, busca cada nombre de la lista. Si hay varias con el mismo nombre, la columna «Cuál» dice cuál bajar. Las descripciones son las que pone Mixamo debajo de cada una.
3. No toques los deslizadores (Overdrive, Character Arm-Space, Trim). Solo dos casillas:
   - **In Place**: si aparece, márcala;
   - **Mirror**: sin marcar.
4. Pulsa **Download** con estos ajustes:
   - Format: **FBX Binary (.fbx)**;
   - Skin: **Without Skin**;
   - Frames per Second: **30**;
   - Keyframe Reduction: **none**.
5. Guarda el archivo en `assets-src/mixamo/`, dentro de la carpeta del juego. Esa carpeta no se sube.
   - El nombre, el que da Mixamo (`Firing Rifle.fbx`).
   - Si ya hay uno con ese nombre, añade « 2», « 3»… en el orden en que salen en la búsqueda (`Rifle Idle 2.fbx`).
6. Si algún nombre no aparece, busca la palabra principal (por ejemplo «rifle turn») y baja lo más parecido. Apunta qué fue.

Las marcadas con ★ son las primeras. Con ellas ya se arreglan las notas 1, 6, 7 y 9. El resto sirve para elegir la mejor.

## La lista

### Referencia

| | Nombre en Mixamo | Cuál | Para qué |
|---|---|---|---|
| ★ | T-Pose | | La postura de partida: con ella se pasan las demás a nuestros personajes |

### Fusil, de pie

| | Nombre en Mixamo | Cuál | Para qué |
|---|---|---|---|
| ★ | Rifle Idle | todas (5) | Esperar con el fusil: la Escuadra, el turno |
| ★ | Rifle Aiming Idle | | Apuntar |
| | Idle Aiming | | Apuntar (otra) |
| ★ | Firing Rifle | las dos «Firing A Rifle While Standing» | Disparar |
| | Shoot Rifle | | Disparar (otra) |
| ★ | Reloading | todas (2) | Recargar |
| | Reload | todas (3) | Recargar (alguna puede ser de pistola) |
| ★ | Rifle Turn | todas (8) | Girarse en el sitio sin que los brazos hagan cosas raras |

### Fusil, agachado

| | Nombre en Mixamo | Cuál | Para qué |
|---|---|---|---|
| ★ | Rifle Kneel Idle | | Esperar agachado |
| ★ | Idle Crouching Aiming | | Apuntar agachado |
| | Crouch Rapid Fire | | Disparar agachado |
| | Rifle Stand To Kneel | | Agacharse |
| | Rifle Kneel To Stand | | Levantarse |

### Pistola

| | Nombre en Mixamo | Cuál | Para qué |
|---|---|---|---|
| ★ | Pistol Idle | todas (3) | Esperar con la pistola, a dos manos |
| ★ | Pistol Aim | | Apuntar |
| | Aim Pistol | | Apuntar (otra) |
| ★ | Shooting Pistol | | Disparar |
| | Shooting | | Disparar (otra) |
| | Pistol Kneeling Idle | | Agachado |
| | Pistol Stand To Kneel | | Agacharse |
| | Pistol Kneel To Stand | | Levantarse |
| | Drawing Gun | | Sacar la pistola |

### Lanzar

| | Nombre en Mixamo | Cuál | Para qué |
|---|---|---|---|
| ★ | Toss Grenade | | Granada |
| ★ | Throw Object | | Piedra |
| | Throw Grenade | todas (2) | Granada (otra) |
| | Grenade Throw | todas (3) | Granada (otra) |

### Cuerpo a cuerpo

El paquete Sword Combat de Synty, que ya tenemos, trae una estocada y un tajo a una mano que sirven para el cuchillo y el machete. Estas tres son por si quedan mejor.

| | Nombre en Mixamo | Cuál | Para qué |
|---|---|---|---|
| | Stabbing | todas (3) | Cuchillo |
| | Standing Melee Attack Horizontal | | Machete, de lado |
| | Standing Melee Attack Downward | | Machete, de arriba abajo |

### Morir con el arma en la mano

| | Nombre en Mixamo | Cuál | Para qué |
|---|---|---|---|
| | Rifle Death | | Caer con el fusil |
| | Death From Front Headshot | | Tiro en la cabeza, de frente |
| | Death From Back Headshot | | Tiro en la cabeza, por detrás |
| | Death Crouching Headshot Front | | Agachado |

En total son 56 archivos; los ★, 26. Cada uno tarda unos segundos en bajar.

## Segunda tanda (22/10)

Para la nota 1 del 22/10, la vida del Asentamiento: los soldados sentados y junto al fuego.

- Lo que ya se ha hecho usa clips que ya tenemos. Para estar de pie (brazos cruzados, manos en la cintura, pensativo) y para hablar en parejas (señalar, asentir, encogerse de hombros) sirven los de Synty, «ANIMATION – Idles» y «Emotes and Taunts».
- Para la nota 3 (pistola y fusil en reposo) tampoco hace falta nada: la pistola baja junto al muslo sale del final de «Pistol Aim», que ya está bajada.
- Synty no tiene nada para sentarse ni para calentarse las manos, así que eso es lo que falta.

Se bajan igual que la primera tanda: X Bot, sin piel, 30 fps, In Place si aparece, sin Mirror. Van a la misma carpeta, `assets-src/mixamo/`.

| | Nombre en Mixamo | Cuál | Para qué |
|---|---|---|---|
| ★ | Sitting Idle | todas | Sentado en una silla o en un banco del claro, quieto |
| ★ | Sitting Talking | | Sentado, hablando con otro |
| ★ | Stand To Sit | | Sentarse (de pie a sentado) |
| ★ | Sit To Stand | | Levantarse |
| ★ | Warming Hands | | Calentarse las manos junto al fuego, de pie |
| | Sitting | todas | Sentado en el suelo |
| | Talking | todas | Hablar de pie, otra forma de las de Synty |
| | Arguing | | Discutir, de pie |
| | Crouch Idle | | En cuclillas junto al fuego |

- Si «Warming Hands» no aparece, busca «warm» o «fire» y baja lo más parecido; con «Sitting» y «Crouch», lo mismo. Apunta qué fue.
- Son unos 12 archivos; los ★, 5 o 6.

## Después

Arte las convierte con `packages/client/tools/synty/convert.ts`, con ids explícitos, como las de Synty. El juego las pasa del esqueleto de Mixamo a los de Synty y Sidekick, medidas desde la T-Pose. Los archivos de Mixamo no se suben; los convertidos, sí, como los de Synty.
