# Prueba en el PC con la RTX 3090

Guía para Arturo y Ana. La preparó Red el 2026-10-14. El objetivo es comprobar el instalador, que Ana se une sin problemas y cuánto tarda cada fotograma en «alto» frente al objetivo de 4,2 ms.

## 0. Preparar el PC (Arturo)

1. Traer lo último y las dependencias:

   ```bash
   git pull
   ```

   ```bash
   npm install
   ```

2. Construir el instalador. Sale en `packages/desktop/dist/release/`.

   ```bash
   npm run desktop:dist
   ```

   También se puede construir desde el Mac, sin el PC, y llevar el `.exe` en un pendrive o por la red. Primero la aplicación:

   ```bash
   node packages/desktop/scripts/build.mjs
   ```

   Y luego el instalador de Windows:

   ```bash
   cd packages/desktop && npx electron-builder --config electron-builder.json --win
   ```

## 1. Instalar (Arturo)

- El instalador no está firmado, así que Windows avisa con «Windows protegió tu PC». Hay que pulsar «Más información» y luego «Ejecutar de todas formas».
- Elegir «Para todos los usuarios». Pide permiso de administrador, pero así el instalador añade la regla del cortafuegos y Windows no pregunta nada. Con «Solo para mí», Windows pregunta la primera vez: hay que pulsar «Permitir» en redes privadas.
- La red de casa tiene que estar como «Privada» en Configuración → Red e Internet → la red → Tipo de perfil. Si está como «Pública», la regla no vale y Ana no podrá entrar.

## 2. Ser anfitrión (Arturo)

- Bastión → «Ser anfitrión» → nombre → Entrar.
- Abajo a la derecha, en «Comandantes», sale la dirección para Ana, por ejemplo `192.168.1.20:3000`, con un botón «Copiar».

## 3. Unirse (Ana, con la aplicación)

- En la pantalla de inicio, la partida de Arturo debería salir en «Partidas en tu red» en unos segundos. Si no sale, «Unirse por dirección» con la dirección del paso 2.
- En un Mac, la aplicación no está firmada con una cuenta de Apple. Desde macOS 15, «clic derecho → Abrir» ya no basta. La primera vez:
  1. intentar abrirla; macOS dice que no puede comprobarla;
  2. ir a Ajustes del Sistema → Privacidad y seguridad, bajar hasta el aviso de Bastion y pulsar «Abrir igualmente»;
  3. confirmar.

  Otra forma, en Terminal, una sola vez:

  ```bash
  xattr -dr com.apple.quarantine /Applications/Bastion.app
  ```

- Si macOS pregunta por la red local, «Permitir». Con cada versión nueva puede volver a preguntarlo: es por la firma provisional, y se responde igual.
- **Si el Mac tiene el cortafuegos encendido, la primera vez hay que permitir las conexiones entrantes.** macOS pregunta si Bastion puede aceptarlas: «Permitir». Si se deniega, la partida de Arturo no sale en «Partidas en tu red», aunque «Unirse por dirección» sigue funcionando. En un Mac anfitrión, el compañero no podría entrar. Si ya se denegó, se cambia en Ajustes del Sistema → Red → Cortafuegos → Opciones.
- En un PC con Windows, el aviso «Windows protegió tu PC» del instalador se pasa igual que en el paso 1: «Más información» → «Ejecutar de todas formas».
- Si no conecta, apuntar el mensaje exacto que sale.

## 4. Ajustes para medir (los dos, en Ajustes → gráficos)

- Efectos «Altos», Sombras «Sí», Resolución «100 %», Límite de FPS «Sin límite», Mostrar FPS «Sí».
- **Ajuste auto «No».** Si está en «Sí», baja la calidad solo y ya no se mide «alto».
- Pantalla completa: menú Partida → Pantalla completa.
- **Los portátiles, enchufados y sin modo de bajo consumo.** Con la batería baja o en ese modo, el sistema frena el ordenador y la medida no vale (lo que le pasó a Ana el 16/10, PRUEBA-2026-10-16 #17). La telemetría no lo puede ver en el ordenador del compañero.

## 5. Jugar una misión

- Una escaramuza o la de la campaña, entera, con al menos unos 10 minutos de combate. Mejor si hay muchos zombis a la vista.
- La telemetría se guarda sola cada 10 s; no hay que tocar nada.
- Apuntar a mano si algo da tirones o va lento, y qué estaba pasando en ese momento.

## 6. Al terminar, mandar el `red.log` de Arturo

- En el Explorador de Windows, escribir en la barra `%APPDATA%\Bastion` y coger el archivo `red.log`. Tiene los datos de los dos: lo que mide el ordenador de Ana también llega al anfitrión.
- Si Ana no pudo unirse, también el suyo: `%APPDATA%\Bastion\red.log` en Windows, o `~/Library/Application Support/Bastion/red.log` en Mac.

## Qué mira Red en ese `red.log`

- En combate y por cada jugador, el tiempo por fotograma (procesador y gráfica) frente a los 4,2 ms.
- El p95 y los tirones.
- Las llamadas de dibujo.
- Que las conexiones no salgan «(sin comprimir)».
- Las desconexiones o errores, si los hay.
