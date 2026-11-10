# Notas de la 0.12.17

Texto de Lore para la 0.12.17: la segunda tanda de la revisión del proyecto que pidió Arturo el 10/11, con la seguridad de la red y la memoria. Fuentes:
- Red: las copias de la partida guardada, la sala, el puerto del router y la clave (`.dev-snapshots/red/0.12.17/red-01217.patch`, con `.dev-snapshots/gameplay/0.12.17/clave-por-nombre.py`);
- Arte: Personalizar y los grafos del campamento (`.dev-snapshots/arte/unity/0.12.17/LEEME.md`);
- UI: los zombis tras andar en grupo, cautivos y objetivos y el trabajo por fotograma (`.dev-snapshots/ui/unity/0.12.17/LEEME.md`);
- los textos de red, revisados por Lore (`.dev-snapshots/lore/0.12.17/TEXTOS.md`).

Lo que no va en las notas:
- **El movimiento en gris con menos de un paso** (la barra, las casillas y los planes): solo pasa tras el alambre de espino, y los asedios están apagados.
- **Los motivos de cierre de la sala:** no los ve el jugador, solo red.log.
- **Lo que solo es del laboratorio:** `MemoriaPersonalizar`, `BASTION_FALLO_SUCESOS`.

Las reglas y las partidas guardadas no cambian. Va para Mac y Windows, sin nada de iOS. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.17: copias de la partida y una red más segura

Versión 0.12.17 (commit [commit]). La segunda parte de la revisión que pidió Arturo el 10/11: la seguridad de la red y la memoria. Las reglas y las partidas guardadas no cambian.

## Novedades de la 0.12.17

- **Con el puerto del router abierto, la clave también se pide en casa.**
  - **Antes:** la clave solo se pedía a quien entraba desde una dirección de internet. Pero un router que cambia de dónde viene la conexión, un router detrás de otro o un túnel pueden hacer que alguien de internet parezca de casa.
  - **Ahora:** con el puerto abierto, quien entra desde otro aparato también necesita la clave, aunque esté en vuestra red. Con el puerto cerrado, en casa no cambia nada.
  - **Una vez por aparato:** después, la app recuerda su puesto en esa partida y no la vuelve a pedir.
  - **Dónde está la clave:** en el menú principal de quien aloja, detrás de la dirección «Por internet». Desde casa, escribe en «Unirse por dirección» la dirección de «En tu misma red» con la clave detrás; por ejemplo, `192.168.1.20:41391 KXRT5`.
  - **Las direcciones IPv6 de internet** también piden la clave, con el puerto abierto o cerrado.
- **Un interruptor nuevo en Ajustes: «Abrir el puerto del router».**
  - Viene en «Sí», como hasta ahora.
  - Con «No», el juego no toca el router y se entra desde vuestra red o con Tailscale. Si lo cambias mientras alojas una partida, el puerto se abre o se cierra al momento.
- **El puerto del router, sin sorpresas.**
  - **Si algo falla con el puerto ya abierto,** por ejemplo si el router no dice su dirección de internet, el juego lo cierra. Antes se quedaba abierto aunque la pantalla dijera que no se había podido abrir.
  - **Si abriste el puerto a mano en el router,** el juego lo usa como está y no lo toca. Antes lo cerraba al salir y se llevaba tu regla.
- **Copias de la partida guardada.**
  - **Antes:** si la partida guardada no arrancaba, fallaba cada intento de alojar la partida.
  - **Ahora:** además de la copia anterior (el guardado de antes), que ya existía, el juego guarda una copia al empezar cada día de campaña, junto a la partida guardada, y deja las 5 más nuevas.
  - **Si la partida no arranca,** se sigue desde la copia anterior. Si tampoco arranca, desde la copia más nueva de un día. Se pierde lo jugado después de esa copia.
  - **Lo dice el menú principal de quien aloja,** y cada jugador lo ve al entrar: «La partida guardada no se ha podido abrir. Se sigue desde la copia del día 12…».
  - **La que falló queda aparte,** en `save.no-arranca.json`. Mandádnosla para ver qué le pasaba.
- **La sala ya no deja conexiones colgadas.** Con el puerto abierto, cualquiera de internet puede llamar al puerto del juego.
  - **Antes:** nada echaba a quien llamaba sin decir quién era.
  - **Ahora:**
    - una conexión que no dice quién es en 10 segundos se cierra;
    - como mucho puede haber 8 conexiones a la vez sin puesto;
    - solo los jugadores con puesto escriben en red.log.
  - **Con nuestra app no se nota nada.**
- **Personalizar gasta mucha menos memoria.**
  - **Antes:** cada cambio de cabeza o de cuerpo dejaba en memoria 24 figuras más, unos 100 MB, hasta salir de la base. Probando cabezas se podía pasar de 2 GB.
  - **Ahora:** las figuras que no se han visto se sueltan en el siguiente cambio. Medido en el editor, con 6 cambios: de 1494 a 998 MB.
- **El campamento:**
  - **Antes:** al dejar la pestaña Asentamiento, las animaciones de su gente se quedaban en memoria hasta cerrar la app, hasta 6 más cada vez.
  - **Ahora:** se sueltan.
- **Rescates:**
  - **Antes:** mientras esperaba, se podía elegir a la persona que hay que rescatar. Salía su barra, y cualquier orden acababa en «No puedes dar órdenes a esa unidad.».
  - **Ahora:** no se puede elegir hasta que un soldado llega hasta ella.
- **Zombis que andan en grupo:**
  - **Antes:** alguno podía quedarse mirando hacia otro lado en pantalla, aunque en la partida se había girado hacia el ruido de una puerta.
  - **Ahora:** mira hacia donde dice la partida.
- **El combate hace algo menos de trabajo en cada fotograma.**

## Partidas guardadas

- **La campaña de la 0.12.16 sigue** en la 0.12.17, y también una misión guardada a medias.
- **Las copias de cada día empiezan con la 0.12.17:** la primera se guarda al empezar el siguiente día de campaña. Hasta entonces, solo está la copia anterior.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.17-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.17-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.17:** con la 0.12.16 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.16.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firmada con el certificado de desarrollo de Arturo, sin notarizar. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** la 0.12.17 es la segunda versión con la firma nueva, y con ella se ve si funciona.
  - **Si el Mac no pregunta** por las conexiones entrantes ni por la red local, funciona.
  - **Si pregunta,** «Permitir» en los dos, y decídnoslo: se volverá a la firma de antes.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
