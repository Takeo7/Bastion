# Notas de la 0.12.26

Texto de Lore para la 0.12.26: el sigilo que eligió Arturo el 16/11. Fuentes:
- las reglas: `.dev-snapshots/gameplay/notas-2026-11-15/SIGILO-Y-RECLUTAS.md` (puntos 1 y 2) y `.dev-snapshots/gameplay/0.12.26/LEEME-MOTOR.md`;
- UI: `.dev-snapshots/ui/unity/0.12.26/LEEME.md` y `DUDAS.md`;
- los textos, revisados por Lore: `.dev-snapshots/lore/0.12.26/TEXTOS-MOTOR.md` y `TEXTOS-UI.md`;
- Arte: `.dev-snapshots/arte/unity/arreglos-0.12.26/silencioso/` (el golpe, en unos 2 s);
- Red: `.dev-snapshots/red/0.12.26/red-01226-golpe.patch` (su sonido).

Lo que no va en las notas:
- **Lo que viene después:** los reclutas por encuentros (0.12.27) y el ruido de los grupos de zombis.
- **Lo de dentro:** los bots y su semilla, las partidas grabadas y los laboratorios.

Las reglas cambian en el movimiento mientras la escuadra está oculta, en quién nota a quién y en la acción nueva. Va para Mac y Windows, sin nada de iOS. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.26: el golpe silencioso y más movimiento oculto

Versión 0.12.26 (commit [commit]). El sigilo que eligió Arturo el 16/11: más movimiento mientras la escuadra está oculta y el golpe silencioso. Para que el golpe se pueda dar, ahora se puede llegar por detrás de un zombi sin que te note.

## Novedades de la 0.12.26

### Reglas (lo eligió Arturo)

- **Más movimiento mientras la escuadra está oculta.**
  - **Antes:** 8 casillas por turno, oculta o no.
  - **Ahora:** mientras la escuadra está oculta, cada soldado tiene 11. Al romperse la ocultación, cada uno pierde 3 de las que le queden ese turno: si le quedaban 2, se queda sin ninguna. Desde el turno siguiente, 8.
  - **Correr,** oculto, es gastar las 11 de una vez. Andar 8 ya no es correr: no se oye y no llena Forma física.
  - **En pantalla:** las casillas a las que solo se llega con las 3 de más van en menta, en lugar de azul. Con el puntero encima: «Con el movimiento extra de la ocultación: si se rompe, se pierde».
- **El golpe silencioso,** una acción nueva.
  - **Qué hace:** mata siempre, sin tirada y sin ruido. Su grupo no se entera, y no rompe la ocultación. El Tajo, en cambio, sí la rompe.
  - **Contra quién:** un Zombi o un Corredor que tengas al lado, que aún no esté alerta y que no te tenga delante. No vale contra el Acorazado, el Hinchado, el Chillón ni los Cuervos.
  - **Con qué:** con cualquier arma de cuerpo a cuerpo, también con el cuchillo, menos con la motosierra.
  - **Cuánto cuesta:** una acción, como un tajo, y el turno sigue. Cuenta para la barra de Cuerpo a cuerpo.
  - **Cómo se da:**
    - su tecla, detrás de las que tienen número, solo sale cuando hay un zombi contra el que vale;
    - sobre esos zombis sale «GOLPE SILENCIOSO», en menta;
    - sin otra acción preparada, un clic sobre uno prepara el golpe y otro clic lo da.
  - **Con el puntero sobre un zombi que tienes al lado,** en lugar del % del disparo: «Golpe silencioso · Lo mata sin ruido». Si no se puede, sale el porqué, por ejemplo «Golpe silencioso · Te tiene delante: ponte detrás o a un lado».
  - **Cómo se ve:** por encima del hombro, como cualquier golpe que mata. El soldado le sujeta la cabeza y le clava la hoja en la nuca, con un golpe sordo, sin el silbido del tajo. En unos 2 s el zombi está en el suelo.

### Acercarse por detrás (para que el golpe se pueda dar)

- **Antes:** un zombi notaba a cualquiera que tuviera al lado, mirara adonde mirara. Nadie podía llegar a su lado sin que lo notara.
- **Ahora:** mientras la escuadra está oculta, un zombi solo nota a quien tiene delante. Por detrás o por un lado, no te nota aunque estés pegado a él.
  - Descubierta la escuadra, todo es como antes.
  - Los Cuervos siguen notando a quien tienen al lado.
  - **En pantalla:** el ojo rojo de las casillas desde las que os verían, y lo que ve cada zombi, ya van con esta regla.

### Los zombis que oyen un ruido

- **Sobre cada zombi a la vista que oye un ruido** sale un momento (2 s) el altavoz y «?», en el color del ruido: amarillo el vuestro, morado el grito de un Chillón y rojo los demás. Los que ya están luchando con la escuadra no la llevan: no atienden a los ruidos.

## Partidas guardadas

- **La campaña de la 0.12.25 sigue** en la 0.12.26, y también una misión guardada a medias. Si en ella la escuadra sigue oculta, los soldados que aún no se han movido en ese turno ya tienen las 11 casillas; los demás, desde el turno siguiente.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.26-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.26-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.26:** con la 0.12.25 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.25.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
