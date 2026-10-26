# Biblioteca de recursos Synty

Web local para consultar los assets Synty por biblioteca y paquete, ver sus usos detectados y seleccionar otros con una nota de para qué servirían. El resultado que puede consultar Claude Code queda en [`docs/assets/SELECCION.md`](../../docs/assets/SELECCION.md) y [`docs/assets/selecciones.json`](../../docs/assets/selecciones.json). No hay integración ni ejecución automática de las notas.

## Arrancar

Desde la raíz del repo, sin instalar nada (Python 3.9 o superior, solo la biblioteca estándar):

```bash
python3 tools/asset-library/server.py --port 5193
```

Y abrir <http://127.0.0.1:5193/>. Para indexar al arrancar, se añade `--reindex`; también hay un botón «Indexar biblioteca» en la web.

Opciones: `--host` (por defecto `127.0.0.1`), `--library-root` (por defecto `/Volumes/4T_White/50_INBOX/Synty`), `--data-dir` (por defecto `data/asset-library`), `--selections-dir` (por defecto `docs/assets`) y `--quiet`. No acepta los puertos de los jugadores (3000, 3001 y 5173).

### Desde la red local (móvil u otro equipo)

Por defecto solo escucha en este Mac. Para abrirla desde la red local de casa, se arranca con la IP privada concreta del Mac en esa red (`ipconfig getifaddr en0` o `en1`; hoy 192.168.1.128 en en0 y 192.168.1.131 en en1):

```bash
python3 tools/asset-library/server.py --host 192.168.1.128 --port 5193
```

Y abrir `http://192.168.1.128:5193/` desde el móvil o el otro equipo, conectados a la misma red. En este modo escucha solo en esa IP, así que en el propio Mac también se usa esa dirección y no `127.0.0.1`.

- Solo se acepta una IPv4 privada concreta (10.0.0.0/8, 172.16.0.0/12 o 192.168.0.0/16). Se rechazan `0.0.0.0`, `::`, las IP públicas, CGNAT, link-local, IPv6 y los nombres. Solo se atienden conexiones de loopback o de esos rangos privados.
- **Solo en una red de confianza.** No hay contraseña ni TLS: cualquier equipo de la red local puede ver la biblioteca y cambiar la selección y las notas. El tráfico va sin cifrar, así que no se usa en wifi públicas ni compartidas.
- **Nunca se expone a Internet**: ni redirección de puertos en el router, ni túneles, ni proxies.
- Si la IP del Mac cambia (DHCP), hay que arrancarla con la nueva. El firewall de macOS puede pedir permiso para Python la primera vez.
- Al ser `http://` sin TLS, el navegador no la trata como contexto seguro. La web no usa portapapeles, `crypto.subtle`, `randomUUID` ni service workers, que lo exigen; si algo fallara solo fuera del Mac, este es el primer motivo a revisar.

## Qué hace y qué no

- **Lee** los originales de Synty del disco externo. Nunca los modifica, copia al repo ni sube a ningún sitio.
- **Marca como seleccionados por defecto** los recursos con usos detectados en el repo. Los usos detectados son una inferencia del indexador (`catalog.py`), no la palabra de Arturo.
- **Guarda la intención de Arturo** (seleccionado sí o no, y una nota de hasta 5000 caracteres) en `docs/assets/selecciones.json`, que va con el repo. Solo guarda metadatos y notas, nunca archivos de Synty.
- **Exporta** `docs/assets/SELECCION.md` cada vez que se guarda (y en `GET /api/export`): solicitudes pendientes, usados que Arturo ha desmarcado, usados con nota y el inventario de usos detectados, cada uno con su id, la ubicación exacta (paquete, archivo y miembro), la nota literal y los usos con su evidencia.
- **No integra nada.** Una solicitud pendiente no se convierte ni se mete en el juego hasta que Arturo lo pida en la conversación. Desmarcar un recurso usado no lo quita del juego; los usos detectados se conservan.
- Si el disco no está conectado, se sigue viendo el índice anterior y la indexación no arranca (no borra nada). Si aún no existe `catalog.py`, la web arranca vacía.

## Cobertura y límites

- El índice verificado contiene 415.677 entradas y 1.786 con usos detectados. Una entrada puede ser una copia Unity o SourceFiles del mismo recurso, una textura, un material o un prefab: no son 415.677 modelos únicos.
- Hay 181 packs comerciales locales. La interfaz cuenta además `_Biblioteca` y la ficha del pack ausente `SIDEKICK - Modern Thugs`: 183 entradas de biblioteca, 182 disponibles. No confundir este contador con los 182 packs comerciales de la cuenta.
- Los usos se deducen de referencias GUID en catálogos Unity, de los JSON del cliente Unity y del manifiesto/fuentes de conversión de Three.js. El detalle identifica el cliente y el archivo de evidencia. Una referencia declarada no demuestra que el recurso se haya mostrado en una partida. Importar un archivo, por sí solo, no lo marca como usado.
- Las referencias no resueltas se muestran como incidencias y no inventan usos. El índice es una instantánea; se debe reindexar tras cambiar los catálogos del juego o el disco.
- Se prefieren paquetes Unity, SourceFiles y extras; se omiten copias de Godot y Unreal salvo el plugin exclusivo de Unreal. El informe completo de cobertura queda en `data/asset-library/last-index.json`.
- SIMPLE y POLYGON MINI se pueden consultar, pero no están permitidos en Bastión. Su presencia en la biblioteca no cambia las normas de arte.
- El visor resuelve FBX/GLB, materiales y texturas mediante sus referencias reales: `.meta`, prefab y GUID de material/textura. Lee miembros concretos de los paquetes Unity y ZIP a una caché local acotada, sin modificar los originales ni convertir packs enteros. Admite texturas PNG y TGA, mallas activas del prefab y piezas compuestas; avisa si faltan piezas o transformaciones.
- Las animaciones FBX se reproducen con controles de pausa, posición, velocidad y bucle. Cuando el clip no tiene malla, se busca el cuerpo del avatar de origen y sus materiales. No se aplica retarget implícito entre esqueletos distintos. Un clip o formato que no pueda resolverse se identifica como no disponible, no como reproducción completa.
- **No hay fidelidad universal acreditada.** El detalle distingue «Apariencia fiel», «Apariencia parcial» y «Sin apariencia resuelta». Los shaders propios de Synty se aproximan con materiales estándar y los mapas extra no reproducidos se señalan como parciales. Una textura cargada no implica que el resultado coincida con Unity. Los casos comprobados incluyen el coche de Apocalypse (modelo, copia SourceFiles y prefab), el zombi Bellboy y un clip de Base Locomotion; no certifican todo el catálogo.
- Por defecto solo funciona en este Mac por loopback: el enlace `127.0.0.1` no se abre desde el móvil ni desde otro equipo. Para eso se arranca con `--host` y la IP privada del Mac (ver «Desde la red local»).

Para aplicar una selección en una sesión de Claude Code, se puede pedir: «Consulta `docs/assets/SELECCION.md` y `docs/assets/selecciones.json`, revisa los encargos pendientes y aplica [los que indique Arturo] usando sus localizadores y notas». El export incluye las notas literales y separa solicitudes pendientes, usos existentes y recursos desmarcados. Los archivos se pueden versionar con el resto del proyecto cuando Arturo lo autorice; la herramienta no hace commits ni los envía a otra máquina.

## Archivos

| Archivo | Qué es | ¿Va al repo? |
| --- | --- | --- |
| `server.py` | Servidor HTTP y API | Sí |
| `store.py` | Persistencia de la selección y exportación a Markdown | Sí |
| `catalog.py` | Indexador de paquetes y usos (otro rol) | Sí |
| `web/` | Interfaz | Sí |
| `tests/test_server.py` | Pruebas de integración | Sí |
| `docs/assets/selecciones.json` | Selección y notas de Arturo | Sí |
| `docs/assets/SELECCION.md` | Exportación para Claude Code (generada) | Sí |
| `data/asset-library/` | Índice, miniaturas, copias de seguridad | No (ignorado) |

## Persistencia

- Cada guardado lleva la revisión que tenía la web. Si otro guardado (otra pestaña o una edición a mano del JSON) llegó antes, el servidor responde 409 con el estado actual y no pisa nada; la web enseña las dos versiones.
- Las escrituras son atómicas (archivo temporal y renombrado) y van en serie. Antes de sobrescribir, se copia la versión anterior en `data/asset-library/backups/`.
- Editar `selecciones.json` a mano está permitido: el servidor lo recarga. Si el JSON queda roto, se niega a guardar y lo avisa en vez de sobrescribirlo.
- `SELECCION.md` se regenera. Si alguien lo editó a mano, se guarda una copia en las copias de seguridad antes de regenerarlo.
- Las notas que parecen contraseñas o claves se rechazan.

## API

| Método y ruta | Respuesta |
| --- | --- |
| `GET /api/status` | Estado del índice, del disco, de la indexación y de la selección |
| `GET /api/packs` | `{items}` |
| `GET /api/assets?q=&family=&pack=&kind=&state=used\|selected\|pending&limit=60&offset=0` | `{items, total, offset, limit, revision}`; cada recurso con `selected`, `intent`, `status` (`used`, `requested` o `available`) y `revision` |
| `GET /api/assets/<id>` | Recurso decorado |
| `GET /api/assets/<id>/media`, `/thumbnail`; `GET /api/packs/<id>/thumbnail` | Vista previa o miniatura |
| `GET /api/assets/<id>/appearance` | Descriptor de modelos, materiales, texturas, piezas, animación y límites de fidelidad |
| `GET /api/assets/<id>/file` | Medio indexado permitido, con límite de tamaño y tipo MIME; no acepta rutas arbitrarias |
| `GET /api/selections` | `{revision, items}` con la ubicación y los usos detectados |
| `PUT /api/selections/<id>` | Cuerpo `{selected, intent, revision}` → `{revision, asset}`; 409 si la revisión no es la actual |
| `POST /api/reindex` | 202 si arranca, 409 si ya hay una en curso, 503 sin disco |
| `GET /api/export` | `SELECCION.md` como descarga (`?inline=1` para texto) |

## Seguridad

Loopback por defecto; con `--host`, una única IPv4 privada RFC 1918 concreta, nunca un comodín ni una IP pública, y solo conexiones de clientes loopback o RFC 1918. Se rechazan las cabeceras `Host` que no sean 127.0.0.1, localhost, [::1] o la IP enlazada con `--host`, siempre con el puerto real (protección contra DNS rebinding), y las escrituras con `Origin`, `Referer` o `Sec-Fetch-Site` de otro sitio. Sin autenticación ni TLS: el modo de red local da por buena cualquier máquina de esa red. Sin CORS. Solo se sirven rutas de una lista cerrada: la web, la API y `/vendor/three/` (únicamente `node_modules/three/build` y `examples/jsm`, archivos `.js` y `.wasm`, sin `..` ni enlaces que salgan). CSP estricta sin red externa, `nosniff` y cuerpo de hasta 64 KB.

## Pruebas

```bash
python3 -B -m unittest discover -s tools/asset-library/tests -v
node --test tools/asset-library/tests/test_appearance.mjs
node --check tools/asset-library/web/app.js
node --check tools/asset-library/web/appearance.js
node --check tools/asset-library/web/viewer.js
```

Usan un catálogo falso y una carpeta temporal, en un puerto aleatorio de 127.0.0.1. Las pruebas de red local enlazan además un puerto aleatorio en la IP privada del equipo (192.168.1.128 o 192.168.1.131) y se saltan si no hay ninguna. No tocan `docs/assets` ni el disco de Synty.
