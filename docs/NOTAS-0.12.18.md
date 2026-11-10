# Notas de la 0.12.18

Texto de Lore para la 0.12.18: la fila 15 de la revisión del 10/11 (el contagio fuera del código) y la línea de la clave siempre a la vista. Fuentes:
- Gameplay: el motor y las partidas viejas comprobadas (su mensaje del 10/11; `.dev-snapshots/gameplay/CONTAGIO-FILA-15.md`);
- Lore: `contagio-lore.patch` (`.dev-snapshots/lore/0.12.18/LEEME.md`);
- UI: `contagio.patch` y `clave.patch` (`.dev-snapshots/ui/unity/0.12.18/LEEME.md`).

Es una versión de limpieza. No hay reglas, arte ni sonido nuevos, y las notas lo dicen en corto, para que Arturo y Ana sepan que no hay nada nuevo que probar. Lo de dentro (`TreatCommand`, `Infected`, `Quarantine`, los textos de Lore quitados) no va en las notas. Va para Mac y Windows, sin nada de iOS. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.18: limpieza del contagio

Versión 0.12.18 (commit [commit]). Una versión de limpieza: no hay nada nuevo que probar. Las reglas, el arte y el sonido no cambian.

## Novedades de la 0.12.18

- **El contagio, quitado también por dentro.** El mordisco no contagia desde la 0.12.6, pero el juego aún guardaba lo que quedaba de él: la fiebre, el tratamiento y la Cuarentena. Ahora se ha quitado. Al jugar no cambia nada.
- **La clave de la partida, siempre a la vista.**
  - **Antes:** con el puerto del router abierto, la clave solo salía detrás de la dirección «Por internet».
  - **Ahora:** quien aloja la ve siempre en su propia línea: «Clave de la partida: …».
  - **Desde casa, con el puerto abierto:** en «Unirse por dirección» se escribe la dirección de «En tu misma red» con esa clave detrás.

## Partidas guardadas

- **La campaña de la 0.12.17 sigue** en la 0.12.18, y también una misión guardada a medias.
- **Las partidas más viejas también se abren.** Lo hemos probado con partidas de la 0.12.5 y de la 0.12.16, en la base y a media misión.
  - Un soldado que tenía fiebre en una partida de la 0.12.5 o anterior es un soldado más.
  - Una propuesta pendiente de tratar un mordisco se cierra al votarla, sin gastar nada.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.18-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.18-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.18:** con la 0.12.17 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.17.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firmada con el certificado de desarrollo de Arturo, sin notarizar. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** si con la 0.12.17 el Mac ya no preguntó, la firma nueva funciona, y con esta tampoco debería preguntar. Si pregunta, «Permitir» en los dos, y decídnoslo.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
