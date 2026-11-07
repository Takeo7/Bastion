# Notas de la 0.12.6

Texto de Lore para la 0.12.6, con las notas de Arturo del 7/11 tras la 0.12.4 y lo que decidió en el cuadro ([PRUEBA-2026-11-07-ARTURO.md](PRUEBA-2026-11-07-ARTURO.md), «Para la 0.12.6»):
- lo que acaba el turno, «Mechero» y «Manos de enfermero»;
- el mordisco que solo hace daño, y sin Cuarentena;
- sin asedios por ahora;
- las placas más opacas, el contorno de lo que se abre o se registra, y La Lata que atropella al irse.

Va para Mac y Windows, sin nada de iOS. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.6: el mordisco solo hace daño, lanzar ya no acaba el turno, y sin asedios por ahora

Versión 0.12.6 (commit [commit]). Las notas de Arturo del 7/11, tras la 0.12.4.

## Novedades de la 0.12.6

### Casi nada acaba el turno
- **Solo cinco acciones acaban el turno:** Vigilancia, Vigilancia cuerpo a cuerpo (Hoja en alto), Nadie pasa (Zona letal), Cubrirse y Esconderse.
- **Las demás gastan su acción y dejan el resto:**
  - las granadas, el humo, la aturdidora, el cóctel molotov, el lanzagranadas y la granada del chaleco;
  - Arreglar, Supresión, Cara a cara, Pasar visita y Hacerse sitio.

  Por ejemplo: lanzar una granada, disparar y moverse.
- **Los zombis y los vivos,** como antes.

### En el tablero
- **«Sin pausa» se llama ahora «Mechero»:** un cóctel molotov más, si lleva cócteles molotov. Lo de antes, que lanzar una granada no acabara el turno, ya vale para todos.
- **«Manos de enfermero»:** usar una medicina no gasta acción. Antes, las medicinas paraban el contagio de un mordido, y ya no hay contagio.

### El mordisco solo hace daño
- **Un mordisco o el reventón de un Hinchado solo quitan salud:** sin contagio, sin fiebre y sin perder salud cada turno.
- **Con un mordisco se sube a La Lata** como cualquiera.
- **Cualquier soldado que muera en la misión se levanta como zombi,** lo mate quien lo mate.
- **Sin Cuarentena ni tratamiento:** la Cuarentena ya no se construye. La Enfermería sigue curando las heridas antes; lo que hacía a nivel 2 con el contagio ya no hace falta.

### Sin asedios, por ahora
- **En la campaña no hay asedios por ahora:** ni se anuncian ni llegan. Volverán más adelante.
- **La Sala de radio** ya no avisa de ellos: trae misiones y zonas que contactar.

### Lo que se ve
- **Las placas rasgadas son más opacas,** al 90 % (eran al 75 %): lo de dentro se lee mejor sobre el mapa.
- **Las cajas, las puertas, las ventanas tapiadas y los muebles** se marcan con su contorno y el icono de su acción (abrir, registrar, quitar las tablas…), sin resaltar la casilla, como los cuerpos y los coches.
- **La Lata atropella al irse:** los zombis que tenga delante salen volando. Es solo lo que se ve.

## Partidas guardadas

- **La campaña de la 0.12.5 sigue** en la 0.12.6, y también una misión guardada a medias.
- **Al cargarla:**
  - una Cuarentena construida se desmonta y devuelve lo que costó (+50 §);
  - los soldados con fiebre se curan;
  - un asedio anunciado desaparece.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.6-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.6-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.6:** con la 0.12.5 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.5.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
