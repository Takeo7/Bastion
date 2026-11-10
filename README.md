# Bastión

Táctica por turnos cooperativa para dos jugadores, cada uno en su ordenador, con zombis. Tras la Pandemia, los dos lleváis juntos un campamento en el pinar, el Bastión, y una escuadra de supervivientes. Cada uno manda a sus soldados en las misiones, y entre misión y misión decidís juntos qué se construye y adónde se va. También se juegan escaramuzas sueltas, y se puede jugar solo.

Es una app de Unity para Mac y Windows. Las reglas se explican en la ayuda del juego.

## Descargar y jugar

La última versión está en las [releases de GitHub](https://github.com/Takeo7/Bastion/releases). El repositorio es privado: hace falta una cuenta con acceso.

| Archivo | Para |
|---|---|
| `Bastion-<versión>-mac.zip` | Mac con Apple Silicon o con Intel: se descomprime y se copia a Aplicaciones |
| `Bastion-<versión>-win-x64.zip` | Windows: se descomprime y se abre `Bastion.exe` |

- **Mac, la primera vez que se abre cada versión:** Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente». La app no está notarizada ([docs/FIRMA.md](docs/FIRMA.md)). Con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y buscar en la red local: «Permitir» en los dos.
- **Windows:** sin firmar: «Más información» → «Ejecutar de todas formas». Se construye desde el Mac y aún no se ha probado en un PC.
- **Los dos, con la misma versión.**

Para jugar juntos, uno pulsa «Alojar la partida» y el otro entra desde su app:
- **En la misma red:** la partida sale en «Partidas en tu red». Si no sale, «Unirse por dirección», con la dirección que se ve en el menú principal de quien aloja.
- **Por internet:** con «Abrir el puerto del router» en Ajustes (viene en «Sí»), quien aloja ve su dirección «Por internet» y la clave de la partida. El otro las escribe juntas en «Unirse por dirección». Si el router no deja abrir el puerto, Tailscale en los dos ordenadores, con la dirección «Con Tailscale».

La partida guardada, `red.log` y los ajustes de red están en la carpeta de datos de la app:
- Mac: `~/Library/Application Support/Bastion-Unity/Bastion/`
- Windows: `%USERPROFILE%\AppData\LocalLow\Bastion-Unity\Bastion\`

Si algo falla, `red.log` dice qué ha pasado.

## Compilar

Hace falta el SDK de .NET 10 y Unity 6.3 (`6000.3.27f1`).

```bash
dotnet test engine-cs/tests
```

Las pruebas de las reglas.

```bash
unity/tools/unity.sh
```

Abre el proyecto de Unity sin ventana, lo compila y dice si hay errores.

Una versión se publica con `unity/tools/publicar.sh` en tres pasos (probar, publicar y llevarla al árbol principal); los tres están en [docs/UNITY.md](docs/UNITY.md), «Cómo se publica».

Lo que no está en git, y hay que poner en cada máquina antes de compilar:
- **Los modelos de Synty:** `unity/tools/importar-synty.sh`, desde los paquetes originales. Sin ellos el proyecto no se ve.
- **Las animaciones de Mixamo:** `unity/tools/unity.sh -executeMethod Bastion.EditorTools.MixamoImport.Run`, desde `assets-src/mixamo` ([docs/UNITY-ARTE.md](docs/UNITY-ARTE.md)).
- **El sonido:** `unity/tools/sonido.sh`, que lo genera a partir del de la 0.10.1. Necesita Node, con `npm install` en la raíz, y ffmpeg ([docs/UNITY-SONIDO.md](docs/UNITY-SONIDO.md)).

## Dónde está cada cosa

| Carpeta | Qué |
|---|---|
| `engine-cs/` | Las reglas, en C#: el motor, la campaña, la red y los textos (`package/Runtime/Lore`), con sus pruebas ([engine-cs/README.md](engine-cs/README.md)) |
| `unity/Bastion/` | El proyecto de Unity 6.3 (URP): el cliente |
| `unity/tools/` | Compilar, capturar, publicar, importar y generar el sonido |
| `packages/` | La versión de TypeScript (Three.js y Electron), congelada en la 0.10.1. Solo para arreglos graves y como referencia |
| `docs/` | El estado del paso a Unity ([UNITY.md](docs/UNITY.md)), las decisiones del juego ([TRASPASO.md](docs/TRASPASO.md)), los roles y las normas ([EQUIPO.md](docs/EQUIPO.md)) y las notas de cada versión (`NOTAS-<versión>.md`) |

## Arte, sonido y licencias

- **Modelos:** Synty POLYGON y Sidekick, y los animales de Synty SIMPLE - Forest Animals y SIMPLE - Cats. La licencia de Synty no deja publicarlos, así que el repositorio es siempre privado: `packages/` lleva modelos de Synty convertidos, y `unity/Bastion/Assets/Synty` se queda fuera de git.
- **Animaciones:** Synty y Mixamo. Los archivos de Mixamo no se comparten: `unity/Bastion/Assets/Mixamo` se queda fuera de git.
- **Sonido:** hecho en código, con grabaciones CC0 de Freesound y OpenGameArt, y los sonidos de interfaz de Kenney (CC0). El origen de cada uno está en [docs/SONIDO.md](docs/SONIDO.md).
- **Tipografías:** Oswald y Barlow (OFL; las licencias, junto a las fuentes, en `unity/Bastion/Assets/Bastion/UI/Fonts/`), y una hecha por nosotros para el «§».
