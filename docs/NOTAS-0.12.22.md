# Notas de la 0.12.22

Texto de Lore para la 0.12.22, una versión con un solo cambio, pedido por Arturo el 12/11: «El area de andar deberia entrar tambien en casas». En el cuadro eligió que fuera solo en las ya descubiertas. Fuente: UI (`.dev-snapshots/ui/unity/0.12.22/LEEME.md`, con capturas). Lo de dentro (la máscara de lo descubierto, la capa del borde con profundidad, el toque `girar=` del laboratorio) no va en las notas. Las reglas no cambian. Va para Mac y Windows, sin nada de iOS. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.22: el área de andar entra en las casas

Versión 0.12.22 (commit [commit]). Una versión pequeña, con un solo cambio que pidió Arturo. Las reglas no cambian.

## Novedades de la 0.12.22

- **El área de andar entra en los edificios ya descubiertos** (lo eligió Arturo).
  - **Antes:** las casillas a las que llega el soldado solo se pintaban dentro de un edificio si el soldado ya estaba dentro. Desde la calle, el área se quedaba en la puerta, aunque llegara más allá.
  - **Ahora:** desde la calle se pinta también dentro de los edificios que la escuadra ya ha descubierto, en el suelo de dentro. Las paredes que están en pie tapan su borde, y se ve donde el corte las abre.
  - **En un edificio que nadie ha explorado,** sigue sin verse dentro: hay que asomarse a la puerta, como antes.
  - **Entrar andando en un solo movimiento** ya se podía. Ahora se ve hasta dónde llega.

## Partidas guardadas

- **La campaña de la 0.12.21 sigue** en la 0.12.22, y también una misión guardada a medias.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.22-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.22-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.22:** con la 0.12.21 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.21.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
