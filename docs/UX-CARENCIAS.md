# Lo que falta en los packs de interfaz (actualizado el 2026-10-13)

Lista de UX/UI. Recoge lo que las tres propuestas (Apocalipsis, Moderna y Mixta) no encuentran en los packs de Synty, con los nombres del juego (`classes.ts`, `content.ts`, `docs/LORE.md`).

- Packs que ya se usan:
  - INTERFACE Apocalypse HUD;
  - INTERFACE Modern Menus;
  - INTERFACE Military Combat HUD, desde hoy (`assets-src/synty/MilitaryHUD`).
- Regla de Arturo: todo sale de Synty o de Kenney (CC0), o lo hacemos nosotros. Nada de otros packs gratuitos: game-icons.net está descartado.

**Conclusión: Arturo no tiene que descargar nada más.** Lo que sigue faltando lo dibuja UX/UI en el estilo que gane, o lo saca Arte en render desde los modelos 3D de Synty (apartado 3).

## 1. Cubierto con Military Combat HUD (ya puesto)

Los iconos salen de `tools/ui/synty-sprites.py` (los `mh-*` de `public/ui/synty/icons`) y se asignan en `ui/themes/synty-icons.css`.

- **Las 50 habilidades tienen ya silueta de pack** en Apocalipsis y Mixta, tanto en el combate como en el árbol. Las que faltaban:
  - Granadero: Supresión (ametralladora), Disparo en cadena (balas), Ruptura (escudo roto).
  - Francotirador: Manos rápidas (»»»), Cara a cara (revólver), Zona letal (mira).
  - El Grillo: Botiquín volante (dron con cruz), Cobertura del Grillo (dron), Restauración (corazón), Descarga de batería (dron con bomba).
  - Granada de humo y Granada aturdidora: las suyas.
  - Del Oficio:
    - Escudo antidisturbios (escudo) y ¡Aguantad! (escudo con flecha);
    - Saltar por la ventana (corredor) y Encaramarse (galones hacia arriba);
    - Petardos (dinamita) y Rastreo (prismáticos);
    - Sedante (jeringuilla), Arenga (bandera) y Desfibrilador (pulso).
  - El Tajo lleva ahora un **machete**, en lugar del hacha.
  - Enemigos: Mordisco, Grito, Despertar a un muerto, Reventón, Red y porra y Granada del chaleco.
- **Aptitudes**, junto a cada una en el árbol:
  - Pulso: diana;
  - Aguante: corazón;
  - Reflejos: pie alado;
  - Temple: cerebro;
  - Maña: llave inglesa.

- **Parcelas del recinto** (`plotIcons.ts`, `data-plot`): todos los edificios y terrenos tienen ya icono en Apocalipsis y Mixta.
  - Barracón: tienda. Entrenamiento: pesa. Campo de tiro: diana. Simulación: chip.
  - Escucha: auriculares. Comunicaciones: walkie. Taller de oficios: alicates. Obras: llave inglesa. Bosque: planta.
  - De Arte, en render desde modelos de Synty: Camposanto (cruz), Roca, Ruinas y Taller de La Mula (el autobús de perfil).
- **Renders de Arte en las habilidades:** Cóctel molotov (la botella) y Evacuar (La Mula).

## 2. En el pack, pero todavía sin poner

Hace falta que el código marque el elemento (lo hace su dueño), y después basta con CSS.

- **Rangos:** los 8 galones salen de `Badges` (chevrones y escudos en bronce, plata y oro). Hace falta que `.rank` lleve el número de rango.
- **Estados:** las etiquetas `em.*` de la ficha de escuadra.
  - Agazapado: escudo.
  - Aturdido: rayo.
  - Vigilancia: mira.
  - Controlado: cerebro.
  - Cautivo: candado.
  - Evacuado: helicóptero.
  - Datos: notas.

  Pánico y Desorientado no tienen uno claro (apartado 3).
- **Tapones:** las orejeras.
- **El cañón:** ametralladora o RPG.

## 3. No está en ningún pack: lo hacemos nosotros

| Qué | Cómo |
|---|---|
| Emblemas de las 4 clases y de Recluta | Dibujo de UX/UI. Hasta entonces, el símbolo y las 3 letras de ahora |
| Emblemas de las 16 ramas | Dibujo. Hoy el árbol usa el icono de la habilidad más típica de cada rama |
| Talentos pasivos | Sin icono propio. Llevan el de lo que mejoran (puntería, salud, defensa…), sacado de los de arriba |
| Vigilancia (un ojo) | Dibujo. Hoy, una diana |
| Cobertura baja (medio escudo) | Se saca del escudo del pack, cortado por la mitad |
| Pánico, Desorientado | Dibujo |
| Ruido | El altavoz de Modern Menus, o unas ondas dibujadas |
| Venda | Hoy, la gota de sangre. Se puede pedir a Arte en render |
| Retratos de los enemigos y de la radio | Arte-Personajes (`portraits.ts`) |

## 4. Fuentes y sonidos

No hace falta comprar nada. Lo que se usa, con su licencia, está en `docs/ux/FUENTES.md`:

- **Fuentes:** libres (OFL): Barlow, Barlow Condensed, Oswald y Black Ops One. Oxanium (Synty) está disponible.
- **Sonidos:** los de Kenney, «Interface Sounds» (CC0).
