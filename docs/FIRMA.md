# Firmar la aplicación de escritorio

Lo mantiene Red. Cómo está firmada hoy la aplicación (`packages/desktop`) y qué cambiar cuando haya cuenta de desarrollador de Apple, o un certificado para Windows.

## Hoy (0.2.5)

- **Mac:** firma *ad hoc* (`"identity": "-"` en `electron-builder.json`).
  - `codesign --verify --deep --strict` pasa, y la aplicación ya no sale «dañada» en Apple Silicon.
  - Gatekeeper la rechaza (`spctl -a -t exec` → `rejected`). Quien la reciba descargada o por AirDrop la abre una vez desde Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente» ([PRUEBA-PC-3090.md](PRUEBA-PC-3090.md), paso 3).
  - Cada build tiene una firma distinta. macOS la toma por otra aplicación y puede volver a pedir el permiso de red local y, con el cortafuegos de macOS, el de conexiones entrantes.
  - El `Info.plist` lleva `NSLocalNetworkUsageDescription` (desde macOS 15, buscar partidas y unirse por 192.168.x.x pasan por ese permiso).
- **Windows:** sin firmar. SmartScreen avisa con «Windows protegió tu PC» → «Más información» → «Ejecutar de todas formas».
  - El `.exe` lleva sus datos (empresa, nombre, versión) e icono. electron-builder 26 los pone también desde el Mac, sin Wine.

## Cuando llegue la cuenta de Apple

Hacen falta las cuatro cosas a la vez, en `mac` de `electron-builder.json`:

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

`spctl` tiene que decir `accepted` y `source=Notarized Developer ID`. A partir de ahí, macOS recuerda los permisos de una versión a la siguiente.

## Windows, si algún día se firma

- Un certificado de firma de código: uno OV en un token, o Azure Trusted Signing (`win.azureSignOptions` en electron-builder).
- SmartScreen deja de avisar cuando la firma gana reputación, no al momento.
- La regla del cortafuegos del instalador (`build/installer.nsh`) va por la ruta del programa, no por la firma: no cambia.
