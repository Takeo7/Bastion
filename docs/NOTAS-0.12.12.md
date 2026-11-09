# Notas de la 0.12.12

Texto de Lore para la 0.12.12: la segunda tanda de la revisión de armas y animaciones que pidió Arturo el 9/11, hecha por Arte (`.dev-snapshots/arte/unity/revision-animaciones/LEEME.md`, sección 8). Solo cambia cómo se ven las figuras: las reglas, los textos del juego y las partidas guardadas no cambian. La recarga con sonido propio (Red) no va en esta, sino en la 0.12.13. Va para Mac y Windows, sin nada de iOS. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.12: golpes, lanzamientos y curas que se ven bien

Versión 0.12.12 (commit [commit]). La segunda parte de la revisión de armas y animaciones. Solo cambia lo que se ve: acertar y fallar van igual que antes, y las reglas, los textos y las partidas guardadas no cambian.

## Novedades de la 0.12.12

- **El golpe cuerpo a cuerpo empieza sin abrir los brazos.**
  - **Antes:** al empezar el golpe, con cualquier arma, los dos brazos se abrían en cruz un momento y la guardia se deshacía.
  - **Ahora:** el brazo que golpea va al blanco y el otro se queda donde estaba. Solo con el rastrillo y la motosierra se mueven los dos, porque se cogen con las dos manos.
- **El cuchillo llega al blanco.**
  - **Antes:** la estocada se quedaba en el aire, a 42 cm del pecho del blanco.
  - **Ahora:** el soldado avanza un poco más y la punta llega.
- **Cada arma tiene su guardia.**
  - **Antes:** todas usaban la del cuchillo, con la hoja delante de la cara. La katana y la espada tapaban la cara, y el rastrillo se quedaba con los dientes en el aire junto a la cabeza.
  - **Ahora:** la katana y la espada, con la hoja baja. El rastrillo y la motosierra, cruzados por delante y cogidos con las dos manos. Las demás, como antes.
- **La mano se cierra sobre el mango.**
  - **Antes:** al golpear, la mano se quedaba abierta, con el arma debajo de la palma.
  - **Ahora:** el puño se cierra sobre el mango, con el pulgar por encima.
- **Al lanzar, el gesto va hacia el blanco.**
  - **Antes:** el cuerpo no giraba, y el brazo cruzaba por delante, 55° a un lado de donde caía lo lanzado.
  - **Ahora:** el cuerpo gira al lanzar y vuelve al soltar. El brazo va hacia donde cae.
- **La granada, la de humo y la aturdidora se ven** (el modelo lo eligió Arturo).
  - **Antes:** no se veían en la mano y volaban como una bola verde.
  - **Ahora:** cada una tiene su modelo, en la mano y en el aire.
- **Curar con una medicina se ve.**
  - **Antes:** el soldado solo se giraba hacia el herido.
  - **Ahora:** da un paso hasta el herido, se inclina un poco y le pone una jeringa en el pecho. Si se cura a sí mismo, se agacha y se la pone en el muslo.
- **Levantarse con el arma en las manos.**
  - **Antes:** después de esconderse o de estar agachado, las piernas se deslizaban de rodillas a de pie y el arma bajaba.
  - **Ahora:** se ve cómo se levanta, en algo más de un segundo, con el arma arriba.
- **El Cazador de los Cuervos dispara con su carabina.**
  - **Antes:** llevaba la porra en las manos, y el fogonazo salía de ella.
  - **Ahora:** lleva la carabina en las manos y la porra al cinto.
- **Los zombis muerden al caído.**
  - **Antes:** al comerse a un caído, mordían el aire, medio metro antes del cuerpo.
  - **Ahora:** muerden el cuerpo.

## Partidas guardadas

- **La campaña de la 0.12.11 sigue** en la 0.12.12, y también una misión guardada a medias.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.12-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.12-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.12:** con la 0.12.11 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.11.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
