# Notas de la 0.13.8

Texto de Lore para la 0.13.8: el arco y la ballesta, decididos por Arturo el 20 y el 21/11 (RUNS.md, cuadros 102, 103, 105, 106 y 108). Sin commit todavía. Fuentes:
- el contrato de Gameplay, `.dev-snapshots/gameplay/g23/ARCO-Y-BALLESTA.md`;
- los textos de Lore, `.dev-snapshots/lore/ARCO-BALLESTA-TEXTOS.md`;
- el encargo de Producto.

Lo que no va: lo de dentro (pruebas, bots, bancos, laboratorios), Evacuación y las misiones nuevas.

Es para Arturo y Ana: cuenta qué cambia al jugar. Va para Mac y Windows. Lo que va entre [corchetes] lo completa Producto al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.13.8: el arco y la ballesta

Versión 0.13.8 (commit [commit]). Dos armas nuevas y calladas, el arco recurvo y la ballesta, con sus flechas. El Cazador de los Cuervos lleva ahora ballesta.

## Partidas guardadas

- **La partida de la 0.13.7 sigue en la 0.13.8.** No se empieza de cero.

## Novedades de la 0.13.8

### El arco y la ballesta

- **Ballesta:** daño 5, mejor de lejos. Tras cada tiro se recarga, con una acción y una flecha. Se oye poco.
- **Arco recurvo:** daño 3. No se recarga: cada tiro gasta una flecha. Casi no se oye.
- **Las flechas** son la munición de las dos y van en la mochila, 10 por casilla.
  - **La que acierta** se queda en el cuerpo y se recupera al registrarlo. **La que falla** se pierde.
  - **Sin flechas,** el arco no dispara y la ballesta no recarga: «No quedan flechas.»
- **El tiro callado:** un tiro de arco o de ballesta que mata, si no os ve ningún otro enemigo, no rompe la ocultación ni despierta al grupo del muerto. Es como el golpe silencioso, pero de lejos.
  - **Al apuntar,** el panel dice si el tiro será callado o si alguien os ve.
- **Dónde salen:**
  - las dos armas, raras en las cajas y en el Trueque;
  - las flechas, a veces en las cajas de munición y en el Trueque;
  - del cuerpo del Cazador, su ballesta y 5 flechas.

### El Cazador, con ballesta

- **Los Cazadores de los Cuervos** llevan ballesta en vez de red y porra: disparan de lejos, con mucho daño.
- **En el Rescate con Cuervos** sale un Cazador menos, para que la etapa no sea más dura que antes.

### En pantalla

- **La ficha del soldado** lleva el icono del arma que lleva.
- **Al registrar junto a un guardia** que aún no está alerta, con la escuadra oculta, se avisa antes: «Os oye un Saqueador: mirará hacia aquí. Haz clic otra vez para abrir.»
- **El glosario** tiene «Tiro callado» y «Flechas».

## Lo que sigue sin arreglar

- **Los marcos de una casa sin descubrir** se ven claros, como si diera la luz de la calle.
- **Una farola** puede quedar en el bordillo, delante de las puertas de La Lata.
- **El aviso del compañero caído** («Pierre Lambert ha caído · Ver (F)») tapa «Turno enemigo».
- **La dificultad con 2 plazas** aún no se ha ajustado. Apuntad lo que notéis al jugar.
- **Los guardias sentados de la hoguera,** con la cámara normal, parecen de pie.
- **La recarga de la ballesta** se lee poco, y la flecha no se ve volar.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.13.8-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.13.8-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.13.8:** con la 0.13.7 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.13.7.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
