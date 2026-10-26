# Notas de la primera versión de Unity (borrador)

Borrador de Lore (26/10) para cuando Arturo saque la primera versión hecha en Unity (hito 3, «Releases de la app de Unity», en [UNITY.md](UNITY.md) §5). Es para Arturo y Ana, que vienen de la 0.10.1: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] se ajusta al final, con lo que haya. Desde «Título» hasta el final es el texto de la release, con el formato de la 0.10.1.

---

**Título:** Bastión [versión]: el juego en Unity

Versión [versión] (commit [commit]). Es la primera versión hecha en Unity. Se juega igual que la 0.10.1: la misma campaña, las mismas misiones y las mismas reglas. Lo que cambia es la app.

## Una app nueva

- **Es otra app,** no una actualización de la 0.10.1. [Comprobar al cerrar: en el Mac las dos se llaman `Bastion.app`. Si es así, al copiar esta a Aplicaciones sustituye a la 0.10.1, y hay que decirlo.]
- **Los dos, con esta versión:** con la 0.10.1 no se puede jugar juntos.

## Partidas guardadas

- **La campaña de la 0.10.1 no pasa a esta versión.** Aquí se empieza una campaña nueva.
- **La de la 0.10.1 sigue en su carpeta:** si se abre la 0.10.1, está como se dejó.

## Lo que cambia al jugar

### Entrar en la partida
- **«Ser anfitrión» se llama ahora «Alojar la partida».**
- **El nombre se escribe en la primera pantalla,** en «Tu nombre».
- **La dirección para «Unirse por dirección»** lleva ahora el puerto 41391 (por ejemplo, 192.168.1.20:41391). Sale en el menú principal de quien aloja.

### La base
- **Pulsar una zona del Mapa** abre su hoja, con todas sus misiones.
- **«Cancelar misión» se vota,** como desplegar y contactar.

### Los textos
- **Nada da por hecho que un soldado es un hombre:**
  - los avisos dicen «No tienes heridas» o «No sufre aturdimiento, desorientación ni pánico»;
  - los rótulos de estado concuerdan con quien los lleva: ATURDIDA, DESORIENTADA, EVACUADA.
- **El otro jugador sale «A punto», «En línea» o «Sin conexión».**
- **El tiempo, siempre en misiones:** «Misiones de baja», «Se sabrá tras la próxima misión», «Fiebre: +1 misión».
- **«Volver al menú principal»,** donde antes ponía «menú de inicio».
- **Quien se rescata es «la persona retenida»,** donde antes ponía «el VIP».

### Ajustes
- **«Gráficos» (Altos, Medios, Bajos)** junta lo que antes eran Efectos, Sombras y Resolución. Cambia al momento.

### Se ve distinto
- **Viento suave** en los árboles y las enredaderas, en la base y en el combate.
- **La pistola va en su funda y el fusil, a la espalda,** cuando no se usan.
- **Las barricadas de sacos se ven gastadas** según el daño que llevan.
- **Las sombras llegan a todo lo que se ve,** también con la cámara alejada del todo, en el combate y en la base.

## Lo que aún no está

[Se ajusta al final: se quita lo que haya llegado.]
- [**El sonido:** Ajustes ya tiene sus filas, pero aún no se oye nada.]
- [**Los muertos especiales con su cuerpo:** el Corredor, el Acorazado, el Chillón y el Hinchado se ven como un zombi corriente. Hacen lo mismo que en la 0.10.1.]

## Descargas

[Los nombres, como salgan de AppBuild.]

| Archivo | Para |
|---|---|
| `Bastion-[versión]-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-[versión]-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |
| `Bastion-[versión]-linux-x64.tar.gz` | Linux de 64 bits: descomprimir y abrir `Bastion/Bastion.x86_64` desde una terminal |

## Avisos

- **Los dos jugadores, con la misma versión.**
- **Solo escritorio:** no hay versión para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos:**
  - macOS: la primera vez, el Mac pregunta si Bastion puede aceptar conexiones entrantes: «Permitir».
  - Linux con el cortafuegos activado (ufw): para alojar, hay que abrir el TCP 41391 (o el siguiente libre, hasta el 41400) y el UDP 41390.
- [**Windows y Linux:** construidas desde el Mac y sin probar en un PC ni en un Linux de verdad.]
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
