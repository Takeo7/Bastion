# Bastión en una web

**Abandonada por Arturo el 18/10:** no se desarrolla más ni se publica el paquete web; solo la aplicación de escritorio ([FASE11.md](FASE11.md)). La guía se queda como estaba, para la 0.5.0 y la 0.5.1.

Para jugar desde el navegador, del móvil o del ordenador, en una web propia, por ejemplo `bastion.yumewagener.com` (Fase 11, [FASE11.md](FASE11.md) §6).

- El servidor de la web hace de anfitrión: la partida vive allí.
- Los dos entran con el navegador o con la app de escritorio («Unirse por dirección»). Uno puede jugar con la app y el otro con el móvil, en la misma partida.
- Sin la contraseña no se entra, y no se descarga ningún modelo.

## 1. Hacer el paquete

En el ordenador con el repositorio:

```bash
npm run web:dist
```

Deja `packages/server/dist/Bastion-web-<versión>.tar.gz`. Dentro va la carpeta `bastion/`:

- `server.mjs`: el servidor de la partida, en un solo archivo;
- `client/`: el juego compilado;
- `LEEME.md`: esta guía.

## 2. Copiarlo al servidor

El servidor necesita Node 22 o más nuevo (`node --version`). No hace falta `npm install`: no tiene dependencias.

Dos carpetas, una al lado de la otra (aquí en `/srv`):

- `/srv/bastion/`: el programa. Cada versión nueva la sustituye entera.
- `/srv/bastion-partida/`: la partida (`save.json`) y el registro (`red.log`). No se toca al actualizar.

```bash
scp packages/server/dist/Bastion-web-0.4.5.tar.gz servidor:/srv/
```

Y en el servidor:

```bash
cd /srv && tar -xzf Bastion-web-0.4.5.tar.gz
```

## 3. Arrancarlo

Para probar, a mano:

```bash
BASTION_PASSWORD='una-contraseña-larga' PORT=8390 BASTION_DATA=/srv/bastion-partida node /srv/bastion/server.mjs
```

| Variable | Qué es | Si no se pone |
| --- | --- | --- |
| `BASTION_PASSWORD` | La contraseña de la partida. | No arranca. |
| `PORT` | El puerto donde escucha, que solo usa el proxy. | 3000 |
| `BASTION_DATA` | La carpeta de la partida. | `bastion-partida`, junto a la carpeta del programa |
| `BASTION_HOST` | La dirección donde escucha. | `127.0.0.1`: solo el proxy de la misma máquina llega. Con el proxy en otra máquina o en un contenedor, `0.0.0.0`. |

Para que arranque solo y se reinicie si se cae, con systemd. Primero, un usuario sin privilegios que pueda escribir en la carpeta de la partida:

```bash
sudo useradd --system bastion && sudo mkdir -p /srv/bastion-partida && sudo chown bastion /srv/bastion-partida
```

Luego la contraseña, en un archivo que solo lee root:

```bash
sudo install -m 600 /dev/null /etc/bastion.env
```

En `/etc/bastion.env`:

```ini
BASTION_PASSWORD=una-contraseña-larga
PORT=8390
BASTION_DATA=/srv/bastion-partida
```

En `/etc/systemd/system/bastion.service`:

```ini
[Unit]
Description=Bastión
After=network.target

[Service]
EnvironmentFile=/etc/bastion.env
ExecStart=/usr/bin/node /srv/bastion/server.mjs
Restart=on-failure
User=bastion

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload && sudo systemctl enable --now bastion
```

Para ver qué dice:

```bash
journalctl -u bastion -f
```

## 4. El proxy con https

El proxy pone el https (y con él, wss para la partida) y le pasa todo al puerto de Bastión.

Con **Caddy**, que saca el certificado solo y pasa el WebSocket y las cabeceras sin más:

```
bastion.yumewagener.com {
	encode zstd gzip
	reverse_proxy 127.0.0.1:8390
}
```

Con **nginx** (el certificado, por ejemplo con certbot), dentro de `http { … }`:

```nginx
map $http_upgrade $connection_upgrade {
    default upgrade;
    ''      close;
}

server {
    listen 443 ssl;
    server_name bastion.yumewagener.com;
    # ssl_certificate y ssl_certificate_key, los de certbot

    gzip on;
    gzip_types text/css text/javascript application/json model/gltf-binary;

    location / {
        proxy_pass http://127.0.0.1:8390;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $connection_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 1h;
    }
}
```

Lo que importa en cualquier proxy:

- que pase el WebSocket de `/ws`;
- `X-Forwarded-Proto: https`, para que la cookie de la contraseña vaya solo por https;
- `X-Forwarded-For`, para que `red.log` diga quién entra.

## 5. Entrar

- **Navegador:** `https://bastion.yumewagener.com`. La contraseña se pide una vez; el navegador la recuerda un año. Luego, el nombre de siempre.
- **App de escritorio:** «Unirse por dirección», con `bastion.yumewagener.com`. También pide la contraseña la primera vez.
- **Móvil, como una app:**
  - en Safari, Compartir → «Añadir a pantalla de inicio»;
  - en Chrome, el menú → «Añadir a pantalla de inicio» o «Instalar aplicación».

  Se abre en horizontal y a pantalla completa. En el iPhone, la app de la pantalla de inicio no comparte cookies con Safari: pide la contraseña una vez más.
- **Sonido en el móvil:** el navegador lo tiene callado hasta el primer toque en la pantalla. En el iPhone, con el interruptor de silencio puesto, tampoco suena.
- **Cambiar de app en el móvil:** al volver, se reconecta solo. El turno y el mando lo esperan hasta 5 minutos (con el navegador cerrado de golpe, 30 segundos, como siempre).
- **Cada uno, su nombre de siempre:** desde un navegador o una app nuevos, el puesto (y los soldados) se encuentran por el nombre.

## 6. Llevar la campaña de la app a la web, y volver

La campaña es un archivo, `save.json`:

- en la app, en el Mac: `~/Library/Application Support/Bastion/save.json`;
- en la app, en Windows: `%APPDATA%\Bastion\save.json`;
- en la web: en la carpeta de la partida, `/srv/bastion-partida/save.json`.

La app y la web tienen que ser de la misma versión. Los dos programas tienen que estar parados mientras se copia, porque escriben el archivo en cada cambio.

**De la app a la web:**

1. Cerrar la app de Arturo (Salir de Bastión).
2. Parar la web: `sudo systemctl stop bastion`.
3. Por si acaso, guardar la de la web: `cp /srv/bastion-partida/save.json /srv/bastion-partida/save.antes.json`.
4. Copiar la de la app, desde el Mac:

   ```bash
   scp ~/Library/Application\ Support/Bastion/save.json servidor:/srv/bastion-partida/save.json
   ```

5. Arrancar la web: `sudo systemctl start bastion`. En el registro sale «Partida restaurada (… campaña día N)».

**De la web a la app:** al revés.

1. Parar la web.
2. Cerrar la app.
3. Por si acaso, una copia del `save.json` de la app.
4. Traer el de la web:

   ```bash
   scp servidor:/srv/bastion-partida/save.json ~/Library/Application\ Support/Bastion/save.json
   ```

5. Abrir la app, «Ser anfitrión».

Al entrar con el mismo nombre, cada uno recupera su puesto.

## 7. Actualizar

1. `npm run web:dist` y copiar el `.tar.gz` nuevo, como en el paso 2.
2. En el servidor:

   ```bash
   sudo systemctl stop bastion && cd /srv && rm -rf bastion && tar -xzf Bastion-web-0.4.6.tar.gz && sudo systemctl start bastion
   ```

La partida sigue en `bastion-partida`. Quien esté jugando ve «reconectando…» un momento y sigue.

## 8. Si algo va mal

- `red.log`, en la carpeta de la partida, cuenta:
  - quién conecta y desde dónde;
  - las contraseñas equivocadas;
  - los errores de los navegadores;
  - cómo va cada uno (las líneas `[rend …]`, también las del móvil).
- «Demasiados intentos»: hay como mucho 10 contraseñas equivocadas por minuto, entre todos. Se espera un minuto.
- Para cambiar la contraseña, se cambia `BASTION_PASSWORD` y se reinicia. Todos los navegadores la vuelven a pedir.

## 9. La licencia de Synty

Los modelos son de Synty, y su licencia no deja publicarlos. Con la contraseña, solo los ven los dos jugadores.

Sin ella, solo se descarga lo que hace falta para pedirla: la página, el código del juego, las tipografías y el icono. Nunca los modelos, los mapas, los sonidos ni los dibujos de Synty.

Si algún día la web fuera abierta, los modelos se podrían descargar desde el navegador, así que habría que revisar la licencia antes.
