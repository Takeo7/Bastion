# Bastión: normas para las sesiones de Claude

Táctica por turnos cooperativa para dos jugadores (XCOM 2 con zombis). Desde el 23/10 el juego se está pasando a Unity con todo en C# ([docs/UNITY.md](docs/UNITY.md)):
- `engine-cs/`: las reglas en C#, deterministas, idénticas al bit con las de TypeScript; también la red (`package/Runtime/Net`);
- `unity/Bastion/`: el proyecto de Unity 6.3 (URP), con el cliente;
- `packages/`: la versión de TypeScript (engine, server, Three.js, Electron), congelada en la 0.10.1, solo para arreglos graves y como referencia del port.

Antes de nada, lee [docs/UNITY.md](docs/UNITY.md) (estado del port, lo que falta y cómo se trabaja), [docs/TRASPASO.md](docs/TRASPASO.md) (decisiones del juego) y [docs/EQUIPO.md](docs/EQUIPO.md) (roles, archivos de cada uno y normas).

## Normas fijas

- **Idioma:** se responde al usuario en español. En el juego, todo lo visible va en español; identificadores y comentarios del código, en inglés.
- **Formato:** nada de Prettier ni formateadores. El código está formateado a mano, con el estilo del archivo que se toca.
- **Commits y publicación:** solo con el «sí» del usuario. Lo mismo para releases y cualquier publicación.
- **Repositorio:** `Takeo7/Bastion` es siempre privado, porque lleva modelos de Synty convertidos y la licencia no deja publicarlos. Nadie cambia su visibilidad.
- **Puertos de los jugadores:** 3000, 3001 y 5173 nunca se usan ni se paran. Para probar, cada uno usa su puerto (tabla en EQUIPO.md).
- **Arte:**
  - solo Synty POLYGON y Sidekick (con sus animaciones e interfaces), animaciones de Mixamo (Arturo, 21/10: las de armas, que Synty no tiene), Kenney (CC0) o lo hecho por nosotros; nada de Synty SIMPLE ni MINI, que son de otro estilo; tipografías OFL, como mucho dos familias;
  - los originales de Synty viven en `assets-src/`, que no se sube; todos los paquetes están en el disco externo, en `/Volumes/4T_White/50_INBOX/Synty`;
  - la conversión (`packages/client/tools/synty/convert.ts`, con Blender) se lanza siempre con ids explícitos, nunca entera.
- **Opciones rechazadas:** no se vuelven a proponer (vóxel, Expediente, Holotáctico, Viñeta, CRT…). Se construye sobre lo elegido.
- **Coordinación:** la sesión Gameplay reparte el trabajo por roles y revisa; nadie reescribe un archivo de otro rol. Los cambios en archivos ajenos son exactos y pequeños.
- **Ritmo del combate:** cada acción se ve entera antes de pasar a la siguiente. La cámara nunca pelea con el jugador.
- **Rendimiento:** ≤4,2 ms por fotograma en «alto» en el PC de referencia (Windows, RTX 3090); en el Mac, «medio» a ≥30 fps. La telemetría va a `data/red.log`.

## Comprobar antes de entregar

```bash
dotnet test engine-cs/tests
```

```bash
unity/tools/unity.sh
```

Si se toca `packages/` (solo arreglos graves de la 0.10.1):

```bash
npm run typecheck
```

```bash
npx vitest run --root packages/engine
```
