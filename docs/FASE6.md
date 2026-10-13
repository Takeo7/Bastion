# Fase 6 · Edificios de varias plantas

Pedido en la prueba del 13/10 ([PRUEBA-2026-10-13.md](PRUEBA-2026-10-13.md), punto 4): «Se tiene que poder subir a los edificios y varias plantas». Hoy el motor tiene una sola superficie por casilla (`elev`). Se sube a las azoteas de las casas de una planta y a los bloques macizos; las plantas altas de tiendas y oficinas tapan la vista, pero no se pisan.

## Qué queremos

- Entrar en un edificio de 2 o 3 plantas y **subir por una escalera interior** a cada planta, y a la azotea.
- En cada planta, habitaciones, puertas, ventanas, muebles registrables y cobertura, como en la planta baja.
- **Disparar por las ventanas de arriba**, con la ventaja de altura de hoy, y que te disparen.
- **Ver entre plantas:** el techo de cada planta tapa; por las ventanas se ve hacia fuera y hacia dentro, como en la planta baja.
- Los zombis también suben, y los humanos se apostan arriba.

## Cómo (propuesta de Gameplay; Reglas la revisa antes de empezar)

- **Varios niveles por casilla.** Hoy una casilla es `(x, y)` con una superficie. Pasa a `(x, y, nivel)`, con hasta 4 niveles: planta baja, primera, segunda y azotea. Lo más sencillo puede ser una capa por planta dentro de los edificios:
  - `GameState.floors?`: por cada planta de 1 en adelante, las casillas de suelo, paredes, puertas, ventanas y decoración de esa planta, solo dentro de los edificios;
  - la planta baja sigue siendo el mapa de hoy.
- **Posiciones:** `Vec2` gana `z?: number`, el nivel. Las partidas y los eventos sin `z` siguen siendo la planta baja.
- **Escaleras:** una casilla de escalera une un nivel con el siguiente. Moverse por ella cuesta como la escalera de mano de hoy.
- **Línea de visión en 3D,** con el modelo de alturas de `los.ts`: el suelo de cada planta tapa, y las ventanas son huecos en su planta.
- **Cobertura, alcance, ruido, detección y la IA,** por nivel.
- **Generador:**
  - las tiendas, oficinas y casas de 2 o 3 plantas que ya dibujan las plantas (`storeys`) pasan a tenerlas de verdad, con escalera, habitaciones y ventanas;
  - las azoteas de los edificios de varias plantas se pisan.

## Cliente

- **Arte · Mapas:**
  - las plantas se dibujan con suelo, paredes, ventanas y muebles;
  - el corte por plantas, como en XCOM: se ve la planta de la unidad elegida y las de abajo, y las de arriba se ocultan;
  - el cursor elige la planta: con la rueda, con teclas, o según dónde apunte.
- **Combate:** casillas, previsión y movimiento con nivel; un indicador de planta.
- **Arte · Personajes:** subir escaleras, que ya tiene animación.

## Orden

1. **Gameplay · Reglas:** primero, un plan del modelo de datos y de qué funciones cambian (rejilla, recorridos, visión, cobertura, IA, generador, eventos, red), con su coste y riesgos. Gameplay lo aprueba antes de tocar código.
2. **Después, por partes, con tests:**
   - los datos y el movimiento;
   - la visión y la cobertura;
   - el generador;
   - la IA.
3. **Arte · Mapas y Combate,** en cuanto haya plantas en el estado.
