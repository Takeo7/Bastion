# Notas de la 0.12.14

Texto de Lore para la 0.12.14: los zombis nuevos que eligió Arturo de la lista de la biblioteca (Arte: `.dev-snapshots/arte/unity/revision-animaciones/LEEME.md`, sección 10). Son los zombis de City Zombies en la multitud, los pacientes de Horror Asylum en clínicas, enfermerías y hospitales (con `Outfits.Patient` del motor) y seis zombis de Sidekick hechos con las piezas de Apocalypse Zombies que no se usaban. La ropa nueva de los soldados va en la 0.12.15. Las reglas, los textos del juego y el botín no cambian. Va para Mac y Windows, sin nada de iOS. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.14: zombis nuevos

Versión 0.12.14 (commit [commit]). Más cuerpos para los zombis, elegidos por Arturo. Las reglas, los textos y el botín no cambian.

## Novedades de la 0.12.14

- **Gente de la ciudad en la multitud.**
  - **Antes:** todos los zombis comunes salían del mismo grupo de cuerpos.
  - **Ahora:** uno de cada tres lleva uno de 19 cuerpos nuevos: un preso, una novia, un motero, un jugador de fútbol americano, alguien con traje de riesgo biológico, ancianos, un tendero… Cada zombi lleva siempre el mismo, y los dos jugadores lo ven igual. Con muchos zombis juntos, no se repiten seguidos.
- **Seis zombis más con la ropa rota:** camisas y pantalones rotos, una falda, un hacha clavada en la cabeza y otra en el hombro, cuchillos en la espalda, una mochila rota, un bozal y una máscara de gas. Un zombi común sale ahora de 23 cuerpos (antes, 17) o, uno de cada tres, de los 19 de la ciudad.
- **Una policía zombi más,** con los otros policías zombis: como ellos, da botín de policía.
- **Pacientes en las clínicas, las enfermerías y los hospitales.**
  - **Antes:** los zombis que empiezan dentro llevaban ropa de calle.
  - **Ahora:** llevan bata de hospital o la ropa del sitio. Son 8 cuerpos: pacientes, un celador, una enfermera y una sanitaria. Un zombi con uniforme de policía o de militar sigue con su uniforme.
  - **Solo en misiones nuevas:** en una misión ya empezada, no hay pacientes.
- **Arreglado:** un policía zombi llevaba la barba de un rojo chillón desde octubre.
- **Con muchos zombis va algo más ligero:** los cuerpos nuevos de la ciudad tienen menos polígonos.

## Partidas guardadas

- **La campaña de la 0.12.13 sigue** en la 0.12.14.
- **Una misión guardada a medias también sigue,** pero al actualizar, algunos zombis pueden cambiar de cuerpo, porque ahora hay más cuerpos y se reparten de otra forma. Las reglas no cambian.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.14-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.14-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.14:** con la 0.12.13 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.13.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
