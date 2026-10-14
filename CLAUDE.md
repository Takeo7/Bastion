# Bastión: normas para las sesiones de Claude

Táctica por turnos cooperativa para dos jugadores (XCOM 2 con zombis). Monorepo TypeScript:
- `packages/engine`: reglas, deterministas;
- `packages/server`: anfitrión Node;
- `packages/client`: Three.js;
- `packages/desktop`: Electron.

Antes de nada, lee [docs/TRASPASO.md](docs/TRASPASO.md) (estado y decisiones) y [docs/EQUIPO.md](docs/EQUIPO.md) (roles, archivos de cada uno y normas).

## Normas fijas

- **Idioma:** se responde al usuario en español. En el juego, todo lo visible va en español; identificadores y comentarios del código, en inglés.
- **Formato:** nada de Prettier ni formateadores. El código está formateado a mano, con el estilo del archivo que se toca.
- **Commits y publicación:** solo con el «sí» del usuario. Lo mismo para releases y cualquier publicación.
- **Repositorio:** `Takeo7/Bastion` es siempre privado, porque lleva modelos de Synty convertidos y la licencia no deja publicarlos. Nadie cambia su visibilidad.
- **Puertos de los jugadores:** 3000, 3001 y 5173 nunca se usan ni se paran. Para probar, cada uno usa su puerto (tabla en EQUIPO.md).
- **Arte:**
  - solo Synty POLYGON, Kenney (CC0) o lo hecho por nosotros; tipografías OFL, como mucho dos familias;
  - los originales de Synty viven en `assets-src/`, que no se sube;
  - la conversión (`packages/client/tools/synty/convert.ts`, con Blender) se lanza siempre con ids explícitos, nunca entera.
- **Opciones rechazadas:** no se vuelven a proponer (vóxel, Expediente, Holotáctico, Viñeta, CRT…). Se construye sobre lo elegido.
- **Coordinación:** la sesión Gameplay reparte el trabajo por roles y revisa; nadie reescribe un archivo de otro rol. Los cambios en archivos ajenos son exactos y pequeños.
- **Ritmo del combate:** cada acción se ve entera antes de pasar a la siguiente. La cámara nunca pelea con el jugador.
- **Rendimiento:** ≤4,2 ms por fotograma en «alto» en el PC de referencia (Windows, RTX 3090); en el Mac, «medio» a ≥30 fps. La telemetría va a `data/red.log`.

## Comprobar antes de entregar

```bash
npm run typecheck
```

```bash
npx vitest run --root packages/engine
```
