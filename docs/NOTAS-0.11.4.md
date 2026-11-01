# Notas de la 0.11.4

Texto de Lore para la 0.11.4, con las fases 2 a 4 de los edificios de la nota #3 de Ana del 1/11 ([EDIFICIOS-SYNTY.md](EDIFICIOS-SYNTY.md) §6; [UNITY-ARTE.md](UNITY-ARTE.md), «Las tiendas», «Las oficinas», «Las naves» y «El peine de los bloques del borde») y el arreglo de UI del combate que se colgaba en el Mac (`GameSettings.Pacing`). Las reglas no cambian. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.11.4: tiendas, oficinas y naves nuevas

Versión 0.11.4 (commit [commit]). Los edificios que pidió Ana el 1/11, de las tiendas a las naves, y un arreglo en el Mac.

## Novedades de la 0.11.4

### Las tiendas
- **Ya no son casas de madera con un cartel:** son de ladrillo rojo, de ladrillo blanco, de ladrillo de colores o de revoco pintado.
- **Ninguna es igual que la de al lado:** así se ve dónde acaba cada una.
- **Las ventanas son de guillotina,** con un arco arriba.
- **Muchas llevan toldos** rojos, azules o granates sobre los escaparates y la puerta.

### Las oficinas y los bloques del centro
- **Son de piedra en sillares,** pintada del color de la oficina, **o de hormigón gris.**
- **Tienen ventanas altas,** una cornisa arriba y un zócalo abajo.
- **Dos pegadas nunca son iguales.**

### Las naves
- **Las naves del polígono y de la fábrica, y el aserradero de la carretera,** ya no son cajas de ladrillo ni de rayas blancas y negras.
- **Ahora son de ladrillo rojo o pardo, de bloque gris o de chapa ondulada** marrón, roja, verde o azul. Cada una es distinta de la de al lado.
- **Llevan ventanales de fábrica o ventanas corridas,** persianas de carga y puertas con su hoja.
- **El supermercado, el granero de madera de los pueblos y la base del asedio** no cambian.
- **En el polígono, cada fotograma cuesta algo menos** que antes.

### Arreglos
- **Los bloques del borde del mapa** pegados al marco sacaban media pared en cada casilla de la fachada, como un peine. Ahora la fachada sale lisa y con sus ventanas.
- **En el Mac, el combate se quedaba colgado** si el Mac se bloqueaba y la pantalla se dormía con «Límite de FPS» en «Sin límite», que es como viene. Al despertar, no volvía. Pasaba también en la 0.11.2 y la 0.11.3.
  - Ahora, en el Mac, «Sin límite» llega como mucho a los fotogramas que da la pantalla, por ejemplo 60 o 120.
  - En Windows no cambia nada.

## Partidas guardadas

- **La campaña de la 0.11.3 sigue** en la 0.11.4.
- **Las reglas no cambian.** Las tiendas, las oficinas y las naves nuevas salen en todos los mapas, también en una misión guardada a medias.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.11.4-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.11.4-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.11.4:** con la 0.11.3 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.11.3.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
