# Fase 3 · Contagio y medicinas

Tercera fase del cambio a zombis ([LORE.md](LORE.md) «Contagio», [EQUIPO.md](EQUIPO.md)). Las reglas las fija Gameplay y las hace Gameplay · Reglas; Combate, UX/UI y Lore construyen encima.

## Reglas (Gameplay · `engine/`)

### En combate

- **El mordisco contagia.** Cada golpe cuerpo a cuerpo de un zombi que hace daño contagia con una probabilidad:

  | Quién | Probabilidad |
  |---|---|
  | Zombi | 25 % |
  | Corredor | 20 % |
  | Acorazado | 35 % |
  | Reventón del Hinchado | 50 % a cada uno que alcanza |

  El Chaleco antibalas resta 10 puntos. Los números van en `content.ts` (`INFECTION`).
- **Evento:** `infected { unit, by }`. La unidad queda con `infected: true` hasta el final de la misión. En combate no tiene efecto; solo se marca.
- **Morir contagiado.** Un soldado contagiado que muere se levanta zombi al empezar el turno de los zombis, como los cadáveres que levanta el Chillón (reutiliza `reanimated`, sin Chillón: `by` vacío).
- **Sin cura en combate.** El Botiquín cura heridas, no el contagio.

### En la base

- **Días contados.** Un soldado que vuelve contagiado lleva `infection: { since: day }` y se convierte a los **3 días**. Una Enfermería añade 2 días más.
- **Tratar** (comando `treat { soldier }`): gasta **2 ✚** (1 ✚ con la Enfermería a nivel 2 o más) y quita el contagio.
  - **Dos llaves:** tratar necesita a los dos jugadores, como lanzar una misión.
    - El primero que pulsa «Tratar» lo propone.
    - El otro lo confirma o lo rechaza; el comando lleva `ready: boolean`, como `launch` y `advance`.
    - Si un jugador juega solo, basta él.
- **Se convierte.** Si pasan los días sin tratarlo, el soldado se pierde: pasa a los Caídos con la causa «Se convirtió».
  - Un aviso en la base lo cuenta.
- **Un contagiado no sale de misión:** tiene fiebre. No puede ir en la escuadra hasta que lo traten.
- **Curar heridas** (comando `medicate { soldier }`): 1 ✚ quita 3 días de recuperación a un herido. Las medicinas dan para dos cosas y hay que elegir.
- **Medicinas:**
  - se empieza con 2 ✚;
  - salen del botín (ya), de algunas recompensas de misión y de los enclaves de La Vega (`oce`) y Ciudad Varela (`afr`), 1 ✚ cada pocos días mientras sigan vivos.
- **Campaña:** `CAMPAIGN_VERSION` 12, con migración. Las partidas antiguas empiezan sin contagiados y con las medicinas que ya tengan.
- **Informe:** `report.infected[]` con los contagiados de la misión.

### Tests

- la probabilidad por tipo y con chaleco;
- el contagiado que muere se levanta;
- los días y la Enfermería;
- tratar con dos llaves y con un jugador;
- no tratar sin medicinas;
- convertirse a los 3 días;
- el contagiado no entra en la escuadra;
- curar heridas;
- las medicinas de los enclaves;
- la migración.

## Interfaz

- **Combate** (`game.ts`, `hud.ts`):
  - «MORDIDO» flotando y un icono de contagio en la tarjeta del soldado y en su bandera;
  - cuando un caído contagiado se levanta, lo mismo que con el Chillón.
- **Base** (`screen.ts`, Gameplay; el estilo, de UX/UI):
  - en el Cuartel, el contagiado con su cuenta atrás («Se convierte en 2 días») y el botón «Tratar (2 ✚)»;
  - la propuesta del compañero se ve como en el lanzamiento;
  - «Curar (1 ✚)» en los heridos;
  - en la agenda, cada contagiado con su plazo;
  - el informe de misión dice quién vuelve mordido.
- **Lore:**
  - los textos de los avisos, de la radio (la primera vez que muerden a alguien y la primera vez que alguien se convierte) y de la causa en los Caídos;
  - el consejo de radio «Las medicinas no llegan para todos».

## Orden

1. Gameplay · Reglas, después de la altura y la noche ([ALTURA-Y-NOCHE.md](ALTURA-Y-NOCHE.md)).
2. Gameplay: la base (`screen.ts`).
3. Combate y Lore, en paralelo.
