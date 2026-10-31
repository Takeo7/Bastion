# Unirse con código

Jugar desde casas distintas con un código, sin instalar nada (sin Tailscale) y sin abrir puertos en casa de los jugadores. Lo hace Red.

**Estado (31/10):** aparcado.
- Arturo lo quiere en una máquina que va a montar él para alojar cosas, no en Hetzner, Fly.io ni Oracle.
- El plan está aprobado por Gameplay tal cual, con:
  - TLS con un certificado nuestro fijado en la app, por el 443;
  - el cambio de la sala: por código, el puesto de quien no está solo se ocupa con su token o con su nombre.
- El prototipo está en [engine-cs/wip/Relay/](../engine-cs/wip/Relay/), fuera de lo que se compila, con 13 pruebas que pasan (§7).
- La red local y Tailscale siguen como están.

Se eligió entre:
- seguir solo con Tailscale, que ya funcionaba en la 0.11.0;
- abrir el puerto con UPnP;
- Steam;
- un servidor intermedio propio.

Ganó el servidor intermedio.

## 1. Cómo funciona

Un relé de bytes:
1. Quien aloja abre una conexión hacia fuera, al servidor, y recibe un código.
2. Quien se une escribe el código. El servidor avisa a quien aloja.
3. Quien aloja abre otra conexión hacia fuera, solo para ese jugador.
4. El servidor empalma las dos y desde ahí solo copia bytes.

Lo que viaja por el empalme es lo de siempre: los mensajes de `Frame` con JSON, los latidos y el `hello` con la versión. La comprobación de versión sigue en el `hello` de la sala. El servidor no lee el juego.

Para la sala, cada empalme es una conexión más, igual que las de `TcpHost`.

Nadie abre puertos en casa: todo sale hacia fuera, y por eso ni macOS ni Windows preguntan nada (ni cortafuegos ni red local).

### El saludo

Antes del empalme, cada lado manda una línea ASCII (200 bytes como mucho) y el servidor contesta otra. Lo que va después de un `OK` es del juego. Todo el saludo está en `RelayWire.cs`.

| Quién | Manda | El servidor contesta |
|---|---|---|
| Quien aloja, la primera vez | `BASTION-RELE 1 HOST` | `OK <código> <cuándo> <firma>` |
| Quien aloja, al volver tras un corte | `BASTION-RELE 1 HOST <código> <cuándo> <firma>` | `OK …` con el mismo código |
| Quien se une | `BASTION-RELE 1 JOIN <código>` | `OK` cuando el empalme está listo, o `ERR <motivo>` |
| El servidor, por el enlace de quien aloja | `PEER <ticket> <ip>` | (quien aloja abre `PIPE`) |
| Quien aloja, por cada jugador | `BASTION-RELE 1 PIPE <ticket>` | `OK` |
| Quien aloja, cada 20 s | `PING` | `PONG` |
| Quien aloja, al cerrar la partida | `BYE` | (el código muere) |

**Motivos de `ERR`.** Son palabras, no textos. La app pone el texto de Lore, así que cambiar un texto no toca el servidor.

| Motivo | Qué quiere decir |
|---|---|
| `version` | El servidor habla otra versión de este saludo: hay que actualizar la app. |
| `unknown` | No hay partida con ese código: nunca existió, se cerró o caducó. |
| `away` | Quien aloja ha perdido el enlace con el servidor; volverá solo. |
| `limit` | Demasiados intentos, códigos o bytes desde esa dirección. |
| `full` | No caben más: en el servidor o en esa partida. |
| `timeout` | Quien aloja no abrió el empalme en 10 s. |
| `bad` | Una línea que no se entiende, o una firma que no es del servidor. |

## 2. El código

- **Formato:** 8 caracteres en dos grupos, como KXRT-4MWP. Usa 30 símbolos, sin 0, O, 1, I, L ni U. Al escribirlo o pegarlo se aceptan minúsculas, espacios y guiones.
- **Cuándo cambia:** hay uno nuevo cada vez que se pulsa «Alojar la partida».
- **Cuánto dura:** vale mientras la partida siga abierta. Muere al cerrarla (`BYE`), y a las 24 h como mucho.
- **Si quien aloja pierde el servidor** (la Wi-Fi, un reinicio del servidor):
  - recupera solo el mismo código;
  - con el código, el servidor le dio una firma (HMAC-SHA256 con una clave suya), que lo reconoce aunque el servidor se haya reiniciado;
  - el servidor no guarda nada en disco salvo esa clave;
  - en los 2 primeros minutos tras un reinicio, un código que el servidor no conoce recibe `away`, no `unknown`: puede ser de quien aún no ha vuelto.
- **Reconexión de quien se une:**
  - vuelve con el mismo código y su token, y recupera su puesto mientras dure el margen de la sala (30 s, o 5 min si estaba fuera);
  - si después de entrar el servidor dice `unknown`, la partida se ha cerrado: la app lo trata como «quien aloja ha cerrado».

## 3. Las piezas

**Red:**
- **`package/Runtime/Net`:**
  - `RelayWire.cs`: el saludo, los motivos y el formato del código. El servidor compila el mismo archivo, con `BASTION_RELAY`, como suyo.
  - `RelayDial.cs`: `RelayAddress` (servidor, puerto y claves del certificado), `RelayDial.JoinAsync` para quien se une y `RelayRefusedException`.
  - `RelayHost.cs`: el enlace de quien aloja. Pide el código, abre un empalme por jugador, late, vuelve solo y recupera su código, y pide otro si caduca. `RelayState` dice cómo está: `Connecting`, `Ready`, `Unreachable` u `Outdated`.
  - `Connection.cs`: `TcpConnection` sobre un `Stream` (para el TLS) y `ByCode`.
  - `RoomHost.cs`: `Relay(link)`. Los empalmes entran como las conexiones del puerto, y `Stop` cierra el enlace.
  - `Room.cs`: con los dos puestos hechos, una conexión por código no ocupa el puesto de quien no está, salvo con su token o su nombre.
- **ClientSession:** sin cambios, porque ya abre la conexión con un delegado.
- **NetLink (Assets/Bastion/Client/Net):**
  - Quien aloja abre también el enlace con el servidor: `Code`, `CodeState` y `CodeChanged`.
  - `JoinCode(code, name)`, y `Join` también reconoce un código, así AppFlow:429 no cambia.
  - `BASTION_RELE=host:port` para las apps de prueba, y `no` para ninguno.
  - Los textos de cada fallo.
  - Las apps de prueba (`LocalOnly`) no salen a internet si no llevan `BASTION_RELE`.

**UI:**
- en el menú de quien aloja (RoomMenu), el código en grande, con «Copiar», y su estado (`CodeState`): conectando, listo, sin servidor o app antigua;
- en la pantalla de inicio (StartScreen), el campo para el código. La propuesta es que vaya aparte de la dirección y primero, porque será lo normal; la dirección queda para la red local y Tailscale;
- la etiqueta del código en la lista de direcciones (RoomText, junto a «Por internet (Tailscale)»).

**Lore**, los textos de estas situaciones (los del prototipo son provisionales):
- qué es el código y qué se hace con él;
- conectando con el servidor;
- el servidor no responde (la red local y Tailscale siguen);
- no hay partida con ese código, o ya se cerró;
- código mal escrito;
- demasiados intentos: hay que esperar;
- servidor lleno;
- hace falta una versión más nueva de la app;
- quien aloja ha perdido la conexión con el servidor y se reintenta;
- y el aviso de NetLink «No responde nadie… misma red o Tailscale», que puede nombrar el código.

## 4. Seguridad

El servidor está abierto a internet.

**Límites** (`RelayOptions`, con estos valores por defecto):

| Por dirección IP | Global | Por código |
|---|---|---|
| 3 códigos vivos (si los tiene todos, el más viejo sin enlace se suelta) | 200 códigos | 4 empalmes a la vez |
| 10 conexiones abiertas | 1000 conexiones | 2 MB/s (los bytes de más esperan) |
| 30 conexiones nuevas por minuto | | 1 GB al día (después se corta; una tarde de campaña son decenas de MB) |
| 10 códigos fallidos en 10 min: 30 min sin poder unirse | | |

Una conexión tiene 5 s para decir a qué viene, con el TLS incluido. Si no, se cierra. Así nadie deja sitios ocupados con conexiones mudas, y nadie puede usar el servidor de túnel gratis.

**El código como única llave.** Basta, por cuatro razones:
- con 8 caracteres hay unos 650.000 millones de códigos;
- con 200 vivos, acertar uno desde 10.000 IPs a la vez llevaría unos 7 meses (con 6 caracteres serían unas 6 h: por eso 8);
- un código visto en una captura deja de servir al cerrarse la partida;
- quien aloja ve quién entra.

Y el cambio de la sala impide que alguien con el código ocupe el puesto del compañero mientras está fuera.

**TLS: certificado nuestro fijado en la app** (decidido, opción b):
- el servidor tiene su certificado propio (RSA 2048, 20 años), hecho una vez con `--crear-certificado`;
- la app lleva el SHA-256 de su clave pública (`RelayWire.KeyOf`) y solo acepta esa;
- no hace falta dominio ni renovaciones, y va por el 443, que casi ninguna red bloquea;
- si se pierde la clave del certificado, hace falta una app nueva. La app puede llevar dos claves para pasar de una a otra.

Las opciones descartadas fueron:
- **a) sin TLS:** el código, los nombres y el token del puesto irían a la vista;
- **c) Let's Encrypt:** pide un dominio y renovar cada 90 días;
- **d) cifrado de extremo a extremo:** 2–3 días más, y no hace falta.

En las pruebas locales, sin TLS.

**El registro del servidor:**
- nunca lleva lo que se juega;
- tampoco direcciones IP enteras, que son datos personales: salen como `ip-` y 8 cifras hexadecimales, un HMAC con la clave del servidor;
- las IP solo están en memoria, para los límites.

A quien aloja sí le llega la IP de quien entra (`PEER`), que va a su `red.log`, como en la red local.

## 5. La máquina de Arturo

Lo que tiene que tener:

### 5.1 Siempre encendida
- El servidor tiene que estar en marcha cuando se juegue. Si está apagado, el código no funciona; la red local y Tailscale siguen.
- Mejor con cable que por Wi-Fi.
- Gasta poco:
  - menos de 100 MB de memoria y casi nada de CPU;
  - unos pocos kB/s por partida y decenas de MB por tarde de campaña;
  - cualquier subida de una conexión de casa sobra.

### 5.2 El 443 abierto a internet
Una de dos:
- **Detrás del router de casa (lo normal):**
  - en el router, redirigir el TCP 443 de fuera a la máquina, al puerto en que escuche el servidor (por ejemplo, el 41401);
  - si hay dos routers (el de la operadora y otro propio), hay que hacerlo en los dos, o poner el de la operadora en modo puente.
- **Con IP pública en la propia máquina:** el servidor escucha en el 443 directamente. En Linux necesita permiso para un puerto bajo: `AmbientCapabilities=CAP_NET_BIND_SERVICE` en systemd, o Docker con `-p 443:41401`.

En la máquina, el cortafuegos abierto solo a ese puerto. SSH no se abre a internet (§5.6).

**Si el 443 ya lo usa otra cosa de la máquina** (una web):
- se pueden compartir por el nombre (SNI): un proxy que no descifre (Caddy con layer4, nginx `stream` con `ssl_preread` o HAProxy) manda al relé lo que pide su nombre, y lo demás a la web;
- para eso la app tiene que conectarse por un nombre, no por una IP (con una IP no hay SNI);
- la otra salida es usar otro puerto de fuera para el relé. Funciona en casi todas las casas, pero algunas redes de trabajo o de hotel solo dejan salir el 80 y el 443.

### 5.3 CG-NAT
Algunas operadoras ponen varias casas detrás de una misma IP pública (CG-NAT). Con CG-NAT, redirigir puertos en el router no sirve: no llega nada de fuera.

**Cómo se ve:** en la página del router, la IP de internet (WAN) no coincide con la que muestra una página de «cuál es mi IP», o empieza por:
- 100.64 a 100.127;
- 10.;
- 192.168.;
- o 172.16 a 172.31.

Ojo: Tailscale también usa direcciones 100.x, pero en el Mac, no en la WAN del router. Hay que mirarlo en el router.

**Salidas:**
1. **Pedir a la operadora que lo quite o una IP pública.** Algunas lo hacen gratis si se pide; otras cobran por una IP fija. Es lo más limpio.
2. **IPv6:** solo sirve si las dos casas tienen IPv6, y no se puede contar con ello.
3. **Delante, un servidor pequeño en internet** que pase el 443 a la máquina de casa por un túnel (WireGuard). Vuelve a haber un servidor de pago, aunque el más barato.
4. **Tailscale Funnel en la máquina de Arturo:** publica un puerto suyo en internet sin abrir nada, y los jugadores no instalan nada.
   - Habría que comprobar que pasa TCP con TLS.
   - Funnel pone su propio certificado (de `*.ts.net`, que se renueva solo), así que la app tendría que comprobar el nombre en vez de la clave fijada.

### 5.4 Un nombre fijo si la IP cambia
- La app lleva el nombre y el puerto del servidor (`NetLink.TheRelay`, hoy vacío).
- Si la IP de casa cambia (lo normal sin IP fija), hace falta un nombre que la siga:
  - un dominio de Arturo con un registro que se actualice solo (DNS dinámico; muchos routers lo traen);
  - o un nombre gratuito de DNS dinámico.
- **Con el certificado fijado encaja sin más.** La app comprueba la clave del certificado, no el nombre ni la IP:
  - la IP puede cambiar todas las veces que quiera;
  - el certificado no se renueva nunca.
- Lo que no puede cambiar es el **nombre**, porque va dentro de la app: un nombre nuevo pide una app nueva. Hay que elegir uno que vaya a durar.
- Con IP fija, también vale la IP sin nombre, salvo para compartir el 443 por SNI (§5.2).

### 5.5 .NET 10 o Docker
El servidor es un programa de consola de .NET 10 (`engine-cs/wip/Relay/server`). No depende del motor ni de Unity. Tres maneras de llevarlo:
- **Con .NET 10 instalado en la máquina:**
  - se publica aquí con `dotnet publish engine-cs/wip/Relay/server -c Release -o publicado` y se copia la carpeta;
  - se arranca con `dotnet Bastion.Relay.dll …`.
- **Sin instalar .NET:** se publica autocontenido para su sistema y procesador (`-r linux-x64` o `-r linux-arm64 --self-contained`). Esto descarga aquí, una vez, el runtime de ese sistema desde NuGet.
- **Con Docker:** la imagen `mcr.microsoft.com/dotnet/runtime:10.0` con la carpeta publicada, y la clave y el certificado en un volumen.

Sirve Linux (lo recomendado), pero también macOS o Windows.

Arranque (bocetos, sin probar):

```ini
# /etc/systemd/system/bastion-rele.service
[Unit]
Description=Bastión: servidor intermedio (Unirse con código)
After=network-online.target
Wants=network-online.target

[Service]
User=bastion-rele
ExecStart=/usr/bin/dotnet /opt/bastion-rele/Bastion.Relay.dll --puerto 41401 --clave /var/lib/bastion-rele/clave --certificado /var/lib/bastion-rele/rele.pfx
Restart=always
RestartSec=2
NoNewPrivileges=yes
ProtectSystem=strict
ProtectHome=yes
ReadWritePaths=/var/lib/bastion-rele
PrivateTmp=yes
MemoryMax=256M

[Install]
WantedBy=multi-user.target
```

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:10.0
COPY publicado/ /app/
USER app
ENTRYPOINT ["dotnet", "/app/Bastion.Relay.dll", "--puerto", "41401", "--clave", "/datos/clave", "--certificado", "/datos/rele.pfx"]
```

Con Docker: `docker run -d --restart=always -p 443:41401 -v /srv/bastion-rele:/datos bastion-rele`.

Hay dos archivos del servidor que no van nunca a git y de los que hay que guardar copia fuera de la máquina (el disco externo o el gestor de contraseñas de Arturo):
- **`rele.pfx`** (el certificado con su clave privada). Si se pierde, hace falta una app nueva.
- **`clave`** (la que firma los códigos). Si se pierde, solo se pierden los códigos vivos en ese momento.

### 5.6 SSH
Lo que necesito para instalarlo y actualizarlo, siempre con el sí de Arturo:
- **Una cuenta en la máquina** solo para esto (por ejemplo, `bastion`), con mi clave pública SSH en `~/.ssh/authorized_keys`. La clave la genero en este Mac y se la paso; la privada no sale de aquí.
- **Permiso para instalar el servicio:** `sudo` para systemd (o para Docker), o que Arturo ejecute una vez el paso de instalación.
- **SSH solo desde la red de casa.** El Mac de Arturo está en la misma red, así que el 22 no se abre a internet.
- **Saber qué es la máquina:** el sistema (Linux y su distribución, macOS o Windows), el procesador (x64 o arm64), su IP en la red de casa y el puerto SSH.

## 6. Pruebas

1. **Unitarias** (hechas, §7): `dotnet test engine-cs/wip/Relay/tests`, con el servidor en 127.0.0.1.
2. **En local, con dos apps de prueba:**
   - el servidor en 127.0.0.1:41401 (el de Red en la tabla de [EQUIPO.md](EQUIPO.md)), con `--retraso` para imitar internet;
   - dos apps con su `BASTION_DATOS`, en modo de prueba (`LocalOnly`), y `BASTION_RELE=127.0.0.1:41401`;
   - quien aloja escribe el código en `red.log` (`[rele] …: código XXXX-XXXX`), y quien se une entra con `BASTION_UNIRSE=<código>`;
   - la campaña de `BASTION_CAMPANA` con la semilla 4, entera, por el servidor;
   - a mitad: reiniciar el servidor y cortar a quien se une (los dos vuelven a su puesto); al cerrar quien aloja, el otro lo oye;
   - hace falta una compilación de prueba con el código nuevo, que se pide a Gameplay. Es de prueba: solo va a 127.0.0.1 y no saca cuadros de macOS.
3. **En el servidor de verdad:**
   - dos apps de prueba en este Mac contra él;
   - luego el PC de Windows, que tienen que abrir Arturo o Ana;
   - y solo entonces una app para Ana, con el aviso de Gameplay.
   - **Ninguna app nueva para Arturo hasta que funcione.**

**Aún sin probar:** el TLS del cliente dentro de Unity. El `SslStream` de Mono (UnityTls) con la clave fijada solo se ha probado en .NET 10. Es lo primero que hay que mirar en la compilación de prueba, y luego en Windows.

## 7. El prototipo: engine-cs/wip/Relay

No lo compila ningún csproj del motor: `Bastion.Engine.csproj` deja fuera `wip/Relay/**`. Tampoco lo ve Unity. Tiene sus propios csproj:

| Carpeta | Qué |
|---|---|
| `net/` | `RelayNet.csproj`: la red de `package/Runtime/Net` con `RelayWire.cs`, `RelayDial.cs` y `RelayHost.cs`, y las copias de `Connection.cs`, `RoomHost.cs` y `Room.cs` (las de ef195ce con los cambios del relé) en lugar de las de `package` |
| `server/` | `Bastion.Relay.csproj` (.NET 10): `RelayServer.cs` (el servidor y sus límites, `RelayOptions`) y `Program.cs` (`--puerto`, `--direccion`, `--clave`, `--certificado`, `--retraso`, `--crear-certificado`) |
| `tests/` | `RelayTests.csproj`: 13 pruebas (abajo) |
| `unity/` | `NetLink.patch`: los cambios de NetLink (§3). Se compiló con todos los scripts de `Assets/Bastion` fuera de Unity, pero no se ha probado en Unity |

```bash
dotnet test engine-cs/wip/Relay/tests
```

```bash
dotnet run --project engine-cs/wip/Relay/server -- --direccion 127.0.0.1 --puerto 41401 --clave /tmp/clave-rele
```

Las pruebas, el 31/10, pasaron en 4 s, y repetidas 5 veces sin fallos:
- los códigos tal como se escriben o se pegan;
- un jugador llega a quien aloja por el servidor, con un mensaje grande que va comprimido;
- la sala recibe por código, mantiene el puesto tras un corte (mismo token) y avisa al cerrar (el código desaparece);
- por código, el puesto de quien no está solo es suyo: un desconocido recibe «La partida ya tiene dos jugadores.» y el jugador vuelve por su nombre desde otro ordenador;
- el enlace de quien aloja vuelve con su código (mientras, quien se une oye `away`);
- un servidor reiniciado reconoce a quien aloja por su firma;
- un código caducado se cambia por uno nuevo;
- los códigos fallidos bloquean la dirección;
- los límites de códigos por dirección y de empalmes por partida;
- una conexión muda, o con una línea sin fin, se cierra;
- una app de otra versión del saludo recibe `version`;
- por TLS solo pasa la clave del servidor.

**Cómo se retoma:**
1. `net/RelayWire.cs`, `RelayDial.cs` y `RelayHost.cs` van a `package/Runtime/Net/`, como cualquier archivo de wip: `dotnet build`, `dotnet test` y Unity compilando.
2. Los cambios de `net/Connection.cs`, `RoomHost.cs` y `Room.cs` se pasan a los de `package` (`diff package/Runtime/Net/Room.cs wip/Relay/net/Room.cs` desde `engine-cs`). Las copias son de ef195ce: si `package` ha cambiado desde entonces, se pasan a mano.
3. `server/` va a `engine-cs/relay/`, con `RelayWire.cs` enlazado desde `package/Runtime/Net`, y `tests/RelayTests.cs` va a `engine-cs/tests/`, que referencia el proyecto del servidor.
4. NetLink: `patch -p0 < engine-cs/wip/Relay/unity/NetLink.patch` desde la raíz del repositorio, y `TheRelay = new RelayAddress("<nombre>", 443, "<clave del certificado>")`.
5. Las piezas de UI y los textos de Lore (§3).
6. Quitar `wip/Relay/**` del `Exclude` de `Bastion.Engine.csproj` cuando la carpeta quede vacía.

**Lo que falta:**
- las pruebas con dos apps (§6);
- el TLS en Unity;
- UI y Lore;
- montar el servidor en la máquina de Arturo (§5);
- y el token del puesto, que se guarda por código. Dentro de una partida vale: tras un corte, el código es el mismo. Pero en la partida siguiente el código es otro, así que quien se une vuelve a su puesto por su nombre, no por el token. Funciona; un identificador fijo de quien aloja lo arreglaría.
