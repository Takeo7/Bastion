# Firmar la aplicación de escritorio

Lo mantiene Red. Cómo se firman hoy las apps de Unity, qué dio la prueba con el certificado de desarrollo de Arturo y, al final, cómo se firmaba la app de Electron (la 0.10.1, congelada).

## Hoy: la app de Unity

### Mac

- **Firma *ad hoc*, otra vez desde la versión siguiente a la 0.12.18.** De la 0.12.16 a la 0.12.18 salió firmada con el certificado de desarrollo de Arturo; está explicado más abajo.
- `AppBuild.Mac` firma con `codesign --force --deep --sign -` y lo comprueba con `codesign --verify --deep --strict`. Si no pasa, la compilación falla.
- El ejecutable se llama «Bastion», sin tilde: con la tilde, `codesign` no puede sellarlo. El Finder y la barra de menús enseñan «Bastión» (`CFBundleName`, `CFBundleDisplayName`).
- El `Info.plist` lleva `NSLocalNetworkUsageDescription`: desde macOS 15, buscar partidas y unirse por 192.168.x.x pasan por el permiso de red local.
- **Gatekeeper la rechaza** (`spctl -a -t exec` → `rejected`). Quien la descarga la abre la primera vez desde Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
- **El Mac pregunta otra vez con cada versión:**
  - si la app puede aceptar conexiones entrantes (el cortafuegos);
  - si puede buscar en la red local.

  En los dos, «Permitir» (README).
- `BASTION_FIRMA` sigue funcionando: con el nombre o el hash de una identidad del llavero (`security find-identity -v -p codesigning`), `AppBuild.Mac` firma con ella y con el sello de tiempo de Apple. `unity/tools/publicar.sh` ya no la pone por defecto.

### Windows

Sin firmar. SmartScreen avisa con «Windows protegió tu PC» → «Más información» → «Ejecutar de todas formas».

### iOS

Se firma con el equipo personal de Arturo, la cuenta gratuita ([IOS.md](IOS.md)).

## La prueba con «Apple Development» (0.12.16 a 0.12.18)

Se hizo para ver si el Mac dejaba de preguntar en cada versión.

- Se publicaron tres versiones firmadas con `BASTION_FIRMA="Apple Development: Arturo Wagener (SWRT945VV4)"` y `--timestamp`.
- Con esa firma, lo que macOS guarda de la app no es el hash del código, que cambia en cada versión. Es este requisito, igual en todas:
  ```
  identifier "com.takeo7.bastion.unity" and anchor apple generic and certificate leaf[subject.CN] = "Apple Development: Arturo Wagener (SWRT945VV4)" and certificate 1[field.1.2.840.113635.100.6.2.1] /* exists */
  ```
- Firmar tardaba 9 s y no pedía nada: el llavero deja a `codesign` usar la clave.

**Resultado (Arturo, 10/11):** al abrir la 0.12.17 o la 0.12.18, el Mac volvió a preguntar por el cortafuegos o por la red local; no sabemos cuál de las dos. Gatekeeper la trató igual que a la *ad hoc*: no es Developer ID ni está notarizada. No se ganó nada, así que se vuelve a la firma *ad hoc*. Las tres versiones firmadas siguen publicadas.

### Por qué pudo preguntar con la misma identidad (sin probar)

Lo que se vio el 10/11 en el Mac donde se compilan las apps, sin cambiar nada:

- **Cortafuegos:**
  - sus reglas van por la ruta del ejecutable (`/usr/libexec/ApplicationFirewall/socketfilterfw --listapps`);
  - no hay ninguna para la app firmada en `unity/Bastion/Builds/Mac`; solo una antigua para `…/MacOS/Bastión` (con tilde, de compilaciones viejas) y otra para una copia de prueba de Red;
  - las reglas no se borran al borrar la app, así que si alguien hubiera contestado por una app firmada, su regla seguiría ahí;
  - o el cortafuegos no preguntó por ella, o nadie contestó. Lo más probable es que la pregunta fuera la de la red local.
- **Red local:** Apple lo explica en [TN3179](https://developer.apple.com/documentation/technotes/tn3179-understanding-local-network-privacy):
  - sigue a cada programa por su firma, y también por el UUID del ejecutable principal;
  - si ese UUID lo comparte con otros programas, «puede comportarse de forma rara»;
  - con varias versiones de la misma app instaladas, Ajustes del Sistema puede hacer cosas inesperadas (FB15568200).

  En este Mac se dan los dos casos:
  - el UUID de `Contents/MacOS/Bastion` es el mismo de la 0.12.14 a la 0.12.18 (`3AD45B3B-…` en x86_64, `2D481815-…` en arm64). Unity lo pone por proyecto, no por compilación: el ejecutable del que parte tiene otro;
  - hay a la vez al menos dos copias de la misma app: la de Arturo (`Bastion/unity/Bastion/Builds/Mac`) y la de la copia limpia donde Gameplay compila y prueba cada versión (`Bastion-0122/unity/Bastion/Builds/Mac`). A eso se suman las apps de prueba de cada rol. Todas tienen el mismo identificador y el mismo UUID; hasta la 0.12.15 iban *ad hoc*, y después firmadas.
- **No se puede probar limpio en este Mac:** con macOS 26 no hay forma de quitar una app de la lista de red local. Los botones + y − llegan con macOS 27.2. Hasta entonces, Apple propone probar en otra cuenta de usuario o en una máquina virtual.

### Lo que lo arreglaría

Un certificado «Developer ID Application», de la cuenta de pago de Apple, y notarizar la app:

- Gatekeeper la aceptaría;
- el cortafuegos la dejaría pasar sin preguntar, porque en este Mac está activado «permitir automáticamente el software firmado descargado»;
- la red local debería preguntar una sola vez por app, si hay una sola copia instalada. Esto no está probado.

## La app de Electron (histórico, 0.10.1)

### Cómo se firmaba (desde la 0.2.5)

- **Mac:** firma *ad hoc* (`"identity": "-"` en `electron-builder.json`).
  - `codesign --verify --deep --strict` pasa, y la aplicación ya no sale «dañada» en Apple Silicon.
  - Gatekeeper la rechaza (`spctl -a -t exec` → `rejected`). Quien la reciba descargada o por AirDrop la abre una vez desde Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente» ([PRUEBA-PC-3090.md](PRUEBA-PC-3090.md), paso 3).
  - Cada build tiene una firma distinta. macOS la toma por otra aplicación y puede volver a pedir el permiso de red local y, con el cortafuegos de macOS, el de conexiones entrantes.
  - El `Info.plist` lleva `NSLocalNetworkUsageDescription`.
- **Windows:** sin firmar. SmartScreen avisa con «Windows protegió tu PC» → «Más información» → «Ejecutar de todas formas».
  - El `.exe` lleva sus datos (empresa, nombre, versión) e icono. electron-builder 26 los pone también desde el Mac, sin Wine.

### Si hubiera llegado la cuenta de Apple

Hacían falta las cuatro cosas a la vez, en `mac` de `electron-builder.json`:

1. **`identity`:**
   - el certificado «Developer ID Application» de la cuenta, instalado en el llavero del Mac que construye;
   - o quitar la clave y dar el certificado por entorno: `CSC_LINK` (el `.p12`) y `CSC_KEY_PASSWORD`;
   - con `"-"` puesto, firma *ad hoc* aunque haya certificado, así que hay que cambiarla sí o sí.
2. **`"hardenedRuntime": true`.** Sin ella, Apple no notariza.
3. **`entitlements` y `entitlementsInherit`:** un `build/entitlements.mac.plist` con `com.apple.security.cs.allow-jit` y `com.apple.security.cs.allow-unsigned-executable-memory` a `true`. Es lo que necesita el JIT de V8; sin ello, Electron no arranca con la ejecución reforzada. No hay módulos nativos, así que no hace falta `disable-library-validation`.
4. **`"notarize": true`,** con las credenciales en el entorno, nunca en el repositorio:
   - `APPLE_API_KEY`, `APPLE_API_KEY_ID` y `APPLE_API_ISSUER` (una clave de App Store Connect);
   - o `APPLE_ID`, `APPLE_APP_SPECIFIC_PASSWORD` y `APPLE_TEAM_ID`.

Para comprobarlo:

```bash
codesign --verify --deep --strict packages/desktop/dist/release/mac-arm64/Bastion.app
```

```bash
spctl -a -vv -t exec packages/desktop/dist/release/mac-arm64/Bastion.app
```

`spctl` tiene que decir `accepted` y `source=Notarized Developer ID`.

### Linux (desde la 0.2.7)

Se construye desde el Mac: `npx electron-builder --config electron-builder.json --linux`, en `packages/desktop`. Salen tres formatos x64 (el `.deb` desde la 0.2.8):

- **`.deb`, el recomendado para Ubuntu y Debian.** Se instala en `/opt/Bastion`. Su script de instalación prepara el aislamiento de Chromium (el ayudante `chrome-sandbox` con setuid si el sistema no tiene espacios de nombres) y, en Ubuntu 24 o posterior, instala un perfil de AppArmor. Sin eso, la aplicación no arranca allí. El responsable es «Arturo Wagener», sin correo. La web es `https://bastion.invalid/`, una dirección que no existe a propósito (`.invalid` está reservado): el `.deb` exige una y no hay web pública.
- **AppImage**, para las demás distribuciones. No puede llevar el ayudante con setuid. En Ubuntu 24 o posterior, con doble clic puede no arrancar; desde una terminal, `./Bastion-<versión>-linux-x86_64.AppImage --no-sandbox`. El acceso directo que crea electron-builder ya lleva ese `--no-sandbox`. En las distribuciones sin `libfuse2` (Ubuntu 22.04 o posterior) hace falta instalarlo, o abrirlo con `--appimage-extract-and-run`.
- **`tar.gz`**, la carpeta tal cual.

En la red: la aplicación abre la partida por TCP en el 3000 (o en el siguiente libre) y se anuncia por UDP en el 41390. No usa el 3001: el https es solo del servidor web (`npm start`). Ubuntu trae el cortafuegos (`ufw`) apagado; si alguien lo tiene encendido, tiene que abrir esos dos puertos.

Nada de esto se ha probado en un Linux de verdad: solo se ha revisado el contenido de los paquetes.

### Windows, si se hubiera firmado

- Un certificado de firma de código: uno OV en un token, o Azure Trusted Signing (`win.azureSignOptions` en electron-builder).
- SmartScreen deja de avisar cuando la firma gana reputación, no al momento.
- La regla del cortafuegos del instalador (`build/installer.nsh`) va por la ruta del programa, no por la firma: no cambia.
