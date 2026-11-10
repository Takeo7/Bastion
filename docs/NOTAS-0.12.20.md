# Notas de la 0.12.20

Texto de Lore para la 0.12.20: la segunda de las tres versiones con los arreglos de la revisión de lo 3D que pidió Arturo el 10/11 (tandas 4, 5, 6 y 7). Fuentes:
- lo elegido y el plan: `.dev-snapshots/gameplay/REVISION-3D-ARREGLOS-GAMEPLAY.md`;
- Arte: `.dev-snapshots/arte/unity/arreglos-0.12.20/LEEME.md`, con `figuras/LEEME.md` (tandas 4 y 6) y `lugares/LEEME.md` (tandas 5 y 7);
- Gameplay: las farolas del campo (E-09, el motor).

Lo que no va en las notas:
- **Lo que se probó y se deshizo** (P-2, A-12) y lo mirado sin cambio (E-13, E-15, E-17).
- **Lo que no se ha cerrado** (sigue en las fichas): Z-1, la valla nevada, A-06, A-18, A-15 contra un Cuervo, A-25 en combate y F-18 parte 2.
- **Las copas sobre la escuadra (E-05):** el cambio está, pero no se ve. Las notas lo dan como pendiente, junto con la vaina (F-07), y no como hecho.
- **Los laboratorios.**

Las reglas solo cambian en las farolas de los mapas de campo nuevos. Va para Mac y Windows, sin nada de iOS. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.20: los zombis, los mapas y la base

Versión 0.12.20 (commit [commit]). La segunda de tres versiones con los arreglos de la revisión de lo 3D que pidió Arturo el 10/11. Las reglas solo cambian en las farolas de los mapas de campo nuevos.

## Novedades de la 0.12.20

- **Los zombis:**
  - **Comer** (lo eligió Arturo).
    - **Antes:** el zombi se tumbaba boca abajo sobre el caído y parecía otro cuerpo.
    - **Ahora:** come de rodillas, inclinado sobre él. Al empezar el turno de los zombis, los que comían se levantan antes: 1,9 s (antes, 2,6), sin deslizarse.
  - **El mordisco:** el zombi ya no se mete dentro del soldado. Si el soldado muere, ya no se pone de pie un instante antes de caer.
  - **El grito del Chillón,** con un gesto nuevo: echa la cabeza atrás y sube los brazos. Antes casi no se movía.
  - **Un caído que se levanta como zombi** ya no mete la cabeza bajo el suelo ni flota, y sube poco a poco.
  - **Al cambiar de un movimiento a otro,** como ponerse a comer o levantarse, el cuerpo ya no salta de golpe.
- **Las armas de un caído.**
  - **Antes:** la pistola quedaba de pie sobre el cuerpo, y el fusil atravesaba el suelo.
  - **Ahora:** el arma de la mano cae al suelo, a su lado. Lo que llevaba colgado ya no se ve.
- **Las armas en la mano:**
  - **La pistola del muslo** ya no se hunde en la pierna. En los 7 trajes que traen su propia funda con pistola (los cinco de policía, «Camuflaje y cartuchos» y «Verde oliva»), ya no se dibuja otra encima (lo eligió Arturo). Al sacarla, sale en la mano.
  - **El cuchillo del cinto** ya no se hunde en la cadera.
  - **Recargar la pistola:** el cargador viejo sale, el nuevo se coge del cinto con la izquierda y la mano vuelve a la corredera. Antes, la izquierda no hacía nada.
  - **Con la pistola,** la mano se cierra en la empuñadura. Corriendo, el brazo se mueve; antes colgaba recto y quieto.
  - **Esconderse con la pistola:** el soldado se gira a la cobertura antes de agacharse.
  - **El fusil de caza y la escopeta:** la culata ya no se mete en el antebrazo.
  - **La ametralladora a la espalda** ya no asoma por encima de la cabeza.
  - **En guardia:**
    - la katana y la espada, con la punta hacia delante (antes, en el suelo);
    - la motosierra, hacia delante y abajo, con las dos manos;
    - el rastrillo, con las manos más juntas.
  - **Corriendo con una hoja de una mano,** la hoja va inclinada hacia fuera, no de pie delante de la cara.
  - **La palanca contra los tablones:** el soldado da un paso hacia ellos y golpea con las dos manos. Los tablones caen cuando la barra está abajo, no cuando está por encima de la cabeza.
- **Los mapas:**
  - **Con nieve,** la calle se distingue del campo: la calzada se ve pisada y más gris (lo eligió Arturo).
  - **En la fábrica,** el pinar del borde crece en hierba y tierra, no sobre losas de hormigón.
  - **Un coche volado por una granada** deja una carcasa baja y quemada (lo eligió Arturo). No da cobertura: la casilla sigue siendo suelo, como antes.
  - **El agujero de una granada en un piso** tiene el canto roto y tablas colgando (lo eligió Arturo). Antes era un cuadro limpio.
  - **En los mapas de campo nuevos,** ya no hay farolas en mitad de los campos arados. Como las farolas alumbran de noche, cambia dónde hay luz en esos mapas.
  - **Los coches de un choque** ya no se meten uno dentro de otro.
  - **El rótulo de una tienda** ya no se ve al revés por detrás.
  - **El río del borde del mapa** llega hasta el borde, sin recuadros claros.
  - **La nieve** ya no blanquea las ruedas ni la leña.
- **La base y La Lata:**
  - **Subir a La Lata.**
    - **Antes:** todos corrían a la puerta, giraban de golpe y desaparecían contra la chapa.
    - **Ahora:** hacen fila ante cada puerta, cada uno espera su turno y entra por la puerta. Con el autobús tarda 3,4 s (antes, 1,8).
  - **La evacuación:** la niebla ya no corta el autobús en trozos, y los coches aparcados junto a su salida se apartan.
  - **La Lata y la Armería, nevadas en invierno.**
  - **El armero de la Armería:** el fusil con mira ya no asoma por fuera, y las armas apoyan la culata en el suelo del armero en vez de hundirse.
  - **Los faros del 4×4, la furgoneta y el camión,** en la rejilla, no por delante del parachoques.
  - **En el Informe,** la fila de la escuadra ya no pone a nadie en la hoguera del Bastión.
  - **En el campamento:**
    - el que anda rodea a los que están charlando, en vez de atravesarlos;
    - la taza se guarda cuando baja la mano y se inclina hacia la boca al beber;
    - la ametralladora de quien está apoyado va más pegada a la espalda.
- **Para la 0.12.21:** la vaina del cuchillo y las copas que se vuelven transparentes sobre la escuadra, que eligió Arturo, aún no han salido.

## Partidas guardadas

- **La campaña de la 0.12.19 sigue** en la 0.12.20, y también una misión guardada a medias.
- **El cambio de las farolas del campo** solo vale para los mapas nuevos.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.20-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.20-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.20:** con la 0.12.19 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.19.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
