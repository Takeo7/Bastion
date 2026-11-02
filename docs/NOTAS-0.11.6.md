# Notas de la 0.11.6

Texto de Lore para la 0.11.6, con las notas de Ana del 1/11, «Informe para desarrollo 2», decididas por Arturo ([PRUEBA-2026-11-01-ANA.md](PRUEBA-2026-11-01-ANA.md), de la #1 a la #11), y lo que dejó la prueba de la 0.11.5 con la app. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.11.6: esconderse y cada zombi por su cuenta

Versión 0.11.6 (commit [commit]). Las notas de Ana del 1/11, con la 0.11.5.

## Novedades de la 0.11.6

### Esconderse
- **Una acción nueva, junto a «Cubrirse»:** cuesta 1 acción y acaba el turno.
- **Hasta tu próximo turno,** los enemigos tienen que estar a la mitad de la distancia de siempre para ver al soldado. Vale para los zombis y para los vivos, y también mientras la escuadra va oculta.
- **No da defensa ni esquiva.** Para eso sigue «Cubrirse», que no cambia: +30 de defensa y +50 de esquiva hasta tu próximo turno.
- **Al esconderse,** el soldado se gira hacia su cobertura y baja a una rodilla. Sale «ESCONDIDO» o «ESCONDIDA» sobre él, y la etiqueta se queda en su rótulo y en su tarjeta hasta tu turno.
- **Al pasar el puntero por un zombi,** la zona que ve ya cuenta con los escondidos.

### Agachados
- **Mientras la escuadra va oculta, o un soldado está escondido, van agachados:** quietos, al andar y al apuntar, al mismo paso que de pie. Para correr con dos acciones se levantan, y al llegar se vuelven a agachar. Como casi todas las misiones empiezan ocultas (todas menos el asedio), el primer tramo de cada una va agachado.
- **Al descubriros, se levantan,** menos los escondidos, que se levantan en tu turno.

### Cada zombi por su cuenta
- **Cuando un zombi ve a un soldado, o lo golpean, solo se despiertan él y los de su grupo que también ven a ese soldado.** Los demás siguen como estaban, y se les puede seguir sorprendiendo. Antes, uno veía y despertaba al grupo entero.
- **Una explosión o un ruido despiertan al grupo entero,** como antes. Los vivos van siempre en grupo.
- **El contacto es solo de los que despiertan:** el «!», la cámara y el aviso. Si despierta uno solo, lo dice: «Priya lo ha avistado». Si no veis a ninguno de ellos, fueron ellos quienes os vieron: «Os ha visto: ha descubierto a Priya».
- **El registro dice cuántos de su grupo, de los que veis, no se han enterado:** «· otros 2 de su grupo no se han enterado».

### El turno de los zombis
- **Primero muerden, de uno en uno, los que ya estaban al lado de un soldado.** Luego andan todos a la vez. Al final muerden los que han llegado. Antes, primero andaban y luego mordían.
- **Solo cambia el orden en pantalla:** lo que pasa es lo mismo.

### Los zombis
- **Piel gris, pálida o enfermiza,** nunca morada, azul ni verde, también en los Hinchados. La ropa más llamativa, más apagada. Los policías siguen de azul.
- **Sin repetirse:** en un mapa no se repite ningún cuerpo hasta que han salido todos, y los que entran juntos son distintos. Igual con los especiales. Los dos jugadores ven los mismos.
- **El soldado que se levanta como zombi es el mismo:** su cuerpo, su cara, su pelo, su ropa y su equipo, con la piel gris y sin el arma. Antes salía un zombi cualquiera.

### En la base
- **En Escuadra, la tarjeta de debajo de cada figura lleva solo el nombre y el rango,** sin el retrato: la cara ya se ve en la figura. En el parte de la misión sigue el retrato.
- **La vista de Soldados es recta, de frente y a la altura de la cintura.** Antes miraba desde arriba.

### Los animales
- **En los pueblos y las ciudades, un perro** (antes tres) **y uno o dos gatos callejeros** junto a las paredes. Los gatos huyen antes que el perro.

### Arreglos
- **La cámara del contacto va solo a los zombis que se ven,** y si no se ve ninguno, no se mueve. Antes iba al centro del grupo, aunque estuviera dentro de un edificio alto. Todavía se queda sobre una azotea cuando los zombis que se ven están detrás de un bloque alto, como en Metsovo: se arregla en la 0.11.7.
- **Los aros de debajo de los zombis van solo en rojo,** sin el borde gris.
- **El coche de policía de la primera misión:** sus botones dicen «Abrir el coche de policía» y «Abrir el maletero», y Tab dice cuál es el siguiente.
- **Si un cóctel molotov despierta a un grupo,** el aviso dice «Ha oído el cóctel molotov», no «la explosión».

### Se queda como estaba
- **La brújula del asedio.**

## Partidas guardadas

- **La campaña de la 0.11.5 sigue** en la 0.11.6, con todo lo de esta versión.
- **En una misión guardada a medias,** los zombis pueden salir con otro cuerpo al cargarla, por el reparto nuevo.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.11.6-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.11.6-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.11.6:** con la 0.11.5 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.11.5.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
