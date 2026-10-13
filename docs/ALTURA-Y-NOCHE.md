# Altura de los edificios y misiones de noche

Decididas por Arturo el 13/10 («sí y sí»). Las reglas las fija Gameplay y las hace Gameplay · Reglas; Arte, Combate y Lore construyen encima.

## 1. Los edificios altos tapan la vista

Hoy el motor trata cada edificio como de una planta: un soldado en una azotea ve y dispara por encima de una tienda que se dibuja con 3 plantas. A partir de ahora, lo que se dibuja y lo que tapa es lo mismo.

- **Plantas por edificio en el motor.**
  - `mapgen.ts` decide las plantas de cada edificio (1 a 3), de forma determinista por la semilla, con el mismo criterio que hoy usa el cliente: tiendas 2–3, oficinas 2–3, algunas casas 2, naves 1.
  - El estado lleva una altura por casilla: `GameState.storeys?: number[]`, la altura en plantas del edificio al que pertenece la casilla, 0 fuera de los edificios. Las partidas guardadas sin ella se comportan como hoy.
- **Línea de visión** (`los.ts`, `grid.ts`):
  - Una pared o puerta de un edificio de N plantas tapa hasta la altura N.
  - Las ventanas y puertas abiertas solo son hueco en la planta baja; por encima tapa la pared. Hoy el modelo solo tiene «hasta dónde tapa» por casilla, así que hace falta, por ejemplo, un hueco con techo: el hueco va de 0 a 1 y la pared sigue de 1 a N.
  - Una línea que cruza el interior de un edificio alto por encima de la planta baja está tapada: el techo de cada planta cuenta.
- **Azoteas.** Solo se sube a las azoteas de los edificios de 1 planta, como hoy. Los de 2 o más no tienen escalera a la azotea y su azotea no se pisa. Esto vale para los edificios en los que se entra: los bloques macizos sin interior (los pisos con terraza, las plataformas) conservan su escalera y su azotea, porque son terreno alto a propósito.
- **Cobertura.** No cambia: la cobertura se mide a la altura del que está al lado.
- **Cliente.** Arte · Mapas dibuja las plantas que dice el estado, no las suyas.
- **Tests.**
  - Desde la azotea de una casa no se ve por encima de una tienda de 3 plantas.
  - A través de una ventana de la planta baja se ve, desde el suelo, lo que hay dentro.
  - Desde una azotea no se ve el interior de un edificio alto por su ventana.
  - Los mapas antiguos, sin `storeys`, dan los mismos resultados que hoy.

## 2. Misiones de noche

- **Hora de la misión.**
  - Cada misión lleva `mission.night: boolean`.
  - En la campaña, alrededor de 1 de cada 3 misiones son de noche (determinista por la semilla de la misión); el briefing lo dice («De noche») y el icono de la operación lo muestra.
  - En la escaramuza, una opción «De noche» en el menú.
- **Qué cambia de noche:**
  - **Vista:** los humanos (soldados, Saqueadores, Cabecillas, Cazadores) ven 8 casillas menos, con un mínimo de 6.
  - **Zombis:** su vista ya es corta y no cambia: de noche cazan de oído.
  - **Luz:** quien está en una casilla iluminada se ve desde la distancia normal. Iluminan:
    - las farolas y focos del mapa: `mapgen.ts` las coloca como casillas con luz, en un radio de 2 a su alrededor;
    - la **Bengala**: además de su daño, deja el objetivo y un radio de 3 a su alrededor iluminados durante 2 turnos;
    - el fuego (molotov, el bidón) mientras arde.
  - **Ocultación:** el escuadrón empieza oculto, como de día. De noche el radio en que lo detectan baja a la mitad, salvo en casilla iluminada.
  - **Más zombis:** un grupo más al empezar. En el Saqueo, la horda llega un turno antes.
  - **Recompensa:** +50 % de suministros en el botín y en la recompensa, para que valga la pena.
- **Estado:** `GameState.lights: { tile, radius, turns? }[]` (las fijas no caducan). Eventos para encender y apagar luces, para que el cliente las dibuje.
- **Cliente:**
  - Arte · Mapas: `renderer.setNight()` y `mapView.setNight()` cuando `mission.night`, con las farolas del estado como luces.
  - Combate:
    - un aviso «De noche» en la franja de arriba;
    - el alcance de vista de noche en la previsión;
    - la luz de la Bengala sobre el mapa mientras dura.
- **Lore:**
  - las líneas del briefing y del parte;
  - un consejo de radio la primera noche («De noche se ve poco: id con bengalas o pegados a las farolas»).
- **Tests:**
  - la vista de noche con y sin luz;
  - la Bengala ilumina y caduca;
  - la detección a la mitad;
  - el grupo extra;
  - el botín +50 %;
  - el día igual que hoy.

## Orden

1. Gameplay · Reglas: la altura (§1) y la noche (§2) en el motor, con sus tests.
2. Arte · Mapas: las plantas y la noche desde el estado.
3. Combate y Lore, en paralelo.
4. Gameplay: las opciones de escaramuza y campaña, la revisión y el commit.
