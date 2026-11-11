# Notas de la 0.12.21

Texto de Lore para la 0.12.21: la tercera y última de las versiones con los arreglos de la revisión de lo 3D que pidió Arturo el 10/11 (tandas 8, 9 y 10, y lo que quedaba de la 0.12.19 y la 0.12.20). Fuentes:
- lo elegido: `.dev-snapshots/gameplay/REVISION-3D-ARREGLOS-GAMEPLAY.md`;
- Arte: `.dev-snapshots/arte/unity/arreglos-0.12.21/LEEME.md`, con `ropa/LEEME.md` (la lista de lo que cambia en los soldados ya hechos), `efectos/LEEME.md`, `mov/LEEME.md` y `cierre/LEEME.md`;
- Arturo, el 11/11: el cuchillo se queda sin vaina.

Lo que no va en las notas:
- **La carga de la misión:** no ha cambiado nada; era el shader del suelo, que el Mac compila una sola vez.
- **Lo cerrado sin cambio:** L-10, A-15 contra un Cuervo, F-10, A-27.
- **Lo que no llega del todo a la ficha:** la lluvia de la tormenta tapa un 5,2 %; agachado, el pie aún resbala 6,8 cm.
- **Los laboratorios.**

Los nombres de cabezas y trajes son los del juego (`Appearances.Heads`, `People.OutfitNames`). En Personalizar no se elige el pelo ni la barba: los pone el juego. Las reglas no cambian. Va para Mac y Windows, sin nada de iOS. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.21: ropa, pelo, efectos y animales

Versión 0.12.21 (commit [commit]). La última de las tres versiones con los arreglos de la revisión de lo 3D que pidió Arturo el 10/11. Las reglas no cambian. **Ojo: a algunos soldados que ya tenéis les cambia el pelo o la barba** (abajo, la lista).

## Novedades de la 0.12.21

- **Pelo y barba de los soldados que ya tenéis.** Para que el pelo y la barba no atraviesen gorros, cascos, capuchas y mochilas, cambian en algunos soldados. Pasa en todas partes: combate, Soldados, Personalizar, el campamento y los retratos. Nadie cambia de ropa, cara, piel ni color de pelo.
  - **Sin pelo debajo** de «Casco con visera», «Gorra de camionero», «Casco con gafas y máscara» y «Casco antidisturbios». Antes asomaba atravesando la tela o el casco.
  - **El pelo, metido bajo el gorro** con «Casco táctico», «Gorro con orejeras», «Pañuelo», «Gorra con linterna» y «Gorra lisa». Es el mismo pelo; con las dos gorras, la coleta sigue saliendo por detrás.
    - Las mujeres con «Casco táctico» o «Gorro con orejeras» pierden la coleta: debajo llevan pelo corto.
  - **Las dos barbas largas** (la poblada y la oscura y espesa):
    - con «Máscara», «Casco con gafas y máscara», «Casco antidisturbios», «Casco con visera» o «Capucha con bufanda», ya no hay barba;
    - con «Capucha» o «Gorra de camionero», la poblada pasa a barba corta y la oscura, a bigote con perilla (lo eligió Arturo).
  - **El pelo largo pasa a corto** (lo eligió Arturo) a cara descubierta o con «Máscara», con estos 14 trajes: «Chaleco verde», «Chaqueta marrón», «Cazadora de borrego», «Chaqueta oscura», «Borrego y bombonas», «Ropa de calle», «Sudadera», «Cuero y esterilla», «Camuflaje y fardo», «Vaqueros y mochila», «Equipo de asalto», «Chaleco azul», «Chaleco antibalas» y «Chaleco táctico».
  - **En Personalizar,** al ponerle a un soldado uno de esos trajes o cabezas, el pelo o la barba cambia; al quitárselo, vuelve.
- **Más arreglos de ropa y cara:**
  - **La capucha de los trajes que la llevan** (lo eligió Arturo): con «Capucha», «Capucha con bufanda» o «Gorra de camionero», ya no abulta debajo ni asoma por encima. Va aplastada sobre la espalda.
  - **La boca,** cerrada al apuntar y al recargar. Antes se quedaba abierta y se veía morada por dentro.
  - **La nariz** ya no atraviesa la «Máscara».
  - **Las coderas de policía** ya no se separan del codo.
  - **En el retrato,** con «Gorra con linterna» o «Gorra de camionero» la cabeza va un poco levantada y se ven los ojos (lo eligió Arturo).
  - **Un policía zombi** llevaba a la espalda un bulto rosa chillón (un cerdo de peluche). Ahora es pardo.
- **Los efectos:**
  - **La sangre del mordisco:**
    - **Antes:** bolas grandes que saltaban por encima de la cabeza y un polígono rojo.
    - **Ahora:** gotas pequeñas que caen hacia atrás y un charco oscuro que crece.
  - **El Hinchado al reventar:** trozos más pequeños que no se quedan en el suelo, y charcos que salen uno tras otro.
  - **Las llamas del molotov:**
    - **Antes:** columnas de bolas con tapa blanca, un destello blanco al prender y una columna de humo negro.
    - **Ahora:** tienen forma de llama, el fuego crece sin destello y sin la columna. El humo de la botella al romperse sigue.
  - **El trapo del molotov:**
    - **Antes:** en la mano dejaba un reguero de bolas naranjas por delante de la cara, y en el aire iba apagado.
    - **Ahora:** una llama pequeña en el cuello de la botella, en la mano y en el aire.
  - **La cruz de la curación** se ve siempre de frente. Antes se ponía de canto y parecía un palito.
  - **La Lata levanta polvo** al moverse por la pista de la base.
- **Los animales:**
  - **Al dejar de pastar,** levantan la cabeza y echan a andar. Antes se deslizaban con la cabeza baja, sin mover las patas.
  - **Al huir,** giran menos de golpe. El gato se levanta despacio.
  - **Ya no se atraviesan:** se esperan o se apartan.
- **Lo que quedaba de antes:**
  - **La escuadra de noche,** desde la cámara del juego, ya no va moteada: un borde liso. De cerca, como antes.
  - **Andar y correr con fusil** (soldados y Cuervos): zancadas más largas, la mitad de pasos, y el pie ya no resbala al frenar. Cada casilla tarda lo mismo.
  - **Registrar** se hace de rodillas, con la mano hasta el suelo. Dura lo mismo. Curarse a sí mismo, también de rodillas.
  - **La lluvia de la tormenta** junto a la cámara tapa menos.
  - **Las copas de los pinos del borde** se aclaran sobre la escuadra (lo eligió Arturo). Era lo que la 0.12.20 dejó pendiente.
  - **Dos soldados que se cruzan** ya no se meten uno en otro.
  - **Recargar la pistola:** la mano izquierda pasa del cargador a la corredera sin irse lejos.
  - **Los fusiles de precisión a la espalda** ya no se hunden en el «Chaleco acolchado» ni en la «Camisa abierta».
  - **Las planchas del autobús** ya no sobresalen de los costados.
- **Lo que se queda como está:**
  - **El cuchillo del cinto, sin vaina.** La vaina anunciada para esta versión salía como un bulto flotando junto a la cadera, y Arturo ha decidido dejarlo así.
  - **Los faldones del jersey y el abrigo,** también como están (Arturo).

## Partidas guardadas

- **La campaña de la 0.12.20 sigue** en la 0.12.21, y también una misión guardada a medias.
- **Los soldados que ya tenéis** salen con el pelo y la barba de la lista de arriba.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.21-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.21-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.21:** con la 0.12.20 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.20.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
