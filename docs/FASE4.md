# Fase 4 · Comida y armas que se encuentran o se fabrican

Cuarta fase del cambio a zombis ([LORE.md](LORE.md) «Escasez» y «Recursos», [EQUIPO.md](EQUIPO.md)). Las reglas las fija Gameplay y las hace Gameplay · Reglas; Arte, UX/UI, Combate y Lore construyen encima. Los números son una primera vuelta: se ajustan con la simulación.

## 1. Comida

- **Recurso:** `c.food`, raciones. Se empieza con 16. El símbolo y el nombre los pone Lore; provisional, «raciones».
- **Se come cada día.** Al pasar cada día, el Bastión gasta 1 ración por cada 4 soldados vivos, redondeando hacia arriba.
- **Hambre.** Si no hay raciones para el día, el Bastión pasa hambre (`c.hungry: true`) hasta que vuelva a haberlas. Con hambre:
  - los soldados salen de misión con −10 de voluntad;
  - las heridas curan a la mitad de ritmo (un día de curación cada dos);
  - un aviso en la base y en la agenda.
- **De dónde sale:**
  - **Botín:** los muebles dan también comida. Despensas de casa, tiendas y almacenes son más probables; maleteros, poco.
  - **Recompensas:** algunas operaciones pagan raciones.
  - **La Vega** (`oce`), si está contactada y sin Nido: 2 raciones cada 4 días. Además de sus medicinas.
  - **Instalación nueva, el «Huerto»** (`garden`): +1 ración al día por nivel, hasta 3. Coste y días como un barracón.
- **Informe:** `report.loot` cuenta también las raciones.

## 2. Armas y armaduras que se fabrican

Hoy el nivel 2 y 3 de armas y armaduras llega solo, con el nivel del Bastión (`magWeapons`, `plasmaWeapons`, `plateArmor`, `predatorArmor`). A partir de ahora, el nivel del Bastión lo **permite** y el Taller lo **fabrica**.

- **Piezas:** un recurso nuevo, `c.parts`.
  - Salen del botín: almacenes, maleteros y oficinas.
  - Algunas recompensas dan piezas.
  - Recuperar un Acorazado abatido da 1 pieza.
- **Fabricar** (comando `craft { project }`), en el Taller. Es una obra más: suministros, piezas y días, con la cuadrilla de obras.

  | Proyecto | Requiere | Coste |
  |---|---|---|
  | Armas de nivel 2 | Taller y Bastión nivel 3 | 60 §, 4 piezas, 3 días |
  | Armas de nivel 3 | Taller a nivel 2 y Bastión nivel 6 | 120 §, 8 piezas, 5 días |
  | Armadura de nivel 2 | Taller y Bastión nivel 4 | 60 §, 4 piezas, 3 días |
  | Armadura de nivel 3 | Taller a nivel 2 y Bastión nivel 7 | 120 §, 8 piezas, 5 días |

- **El rango sigue limitando:** como hoy, un soldado solo usa el nivel que su rango permite.
- **Partidas antiguas:** lo que ya tenían por nivel del Bastión lo conservan, como si estuviera fabricado.

## 3. Armas que se encuentran

- **Hallazgo raro:** al registrar, sobre todo maleteros, almacenes y armarios de casa, a veces sale un arma buena. Por ejemplo, «Un fusil de caza en buen estado» o «Una escopeta recortada».
- **Mochila:** ocupa un hueco de la mochila. Si el soldado la saca de la misión, va a la Armería.
- **Para un soldado:** en la Armería se asigna a un soldado. Le da +1 al nivel de su arma, por encima del de todos, pero sin pasar de 3 y sin saltarse el límite del rango. Si el soldado cae, se pierde.
- **Visible:** en la ficha del soldado («Fusil de caza · nivel 3») y en combate, como hoy el arma.

## 4. Fuera de esta fase

- **La munición contada por misión** queda para más adelante: cambia mucho el combate y antes hay que probar la comida.

## Tests

- **Comida:** el gasto diario, el hambre con sus efectos y su final, el Huerto, La Vega y el botín de comida.
- **Fabricar:** requisitos, coste, días, el resultado y la migración de partidas antiguas.
- **Hallazgos:** la probabilidad, la mochila, la asignación, el tope y la pérdida al caer.
- **Campaña:** `CAMPAIGN_VERSION` 13, con migración.

## Interfaz

- **Gameplay** (`screen.ts`):
  - raciones y piezas en la barra de arriba;
  - el Huerto en la construcción;
  - los proyectos de fabricación en el Taller;
  - las armas encontradas en la Armería;
  - el hambre en la agenda.
- **Arte · Base:** el modelo del Huerto, con invernadero, bancales y bidones de agua, por niveles.
- **UX/UI:** los iconos de raciones, piezas y Huerto.
- **Combate:** la ración encontrada flotando al registrar, como hoy los suministros, y el arma encontrada en la mochila.
- **Lore:**
  - los nombres de las raciones, las piezas y las armas encontradas;
  - el Huerto;
  - los avisos del hambre;
  - la radio: la primera hambre y la primera arma encontrada.

## Orden

1. Gameplay · Reglas: el motor y los tests.
2. Gameplay: la base (`screen.ts`).
3. Arte · Base, UX/UI, Combate y Lore, en paralelo.
