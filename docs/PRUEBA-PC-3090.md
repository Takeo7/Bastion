# Prueba en el PC con la RTX 3090

Guía para Arturo. La preparó Red el 2026-10-24 para la app de Unity (la de Electron, 0.10.1, queda como estaba). El objetivo es medir cuánto tarda cada fotograma en «gráficos altos» frente al de CLAUDE.md: **≤ 4,2 ms**. La medida se hace sola: solo hay que copiar la app, abrir un archivo y mandar otro.

## 1. Copiar la app

- Del Mac, el archivo `unity/Bastion/Builds/Bastion-Windows.zip` (unos 190 MB): por la red, un pendrive o como se prefiera.
- En el PC, descomprimirlo donde se quiera (clic derecho → «Extraer todo…»). Sale una carpeta `Windows` con `Bastion.exe`, `medir.bat` y lo demás.

## 2. Medir

- Doble clic en `medir.bat`.
- Si Windows avisa con «Windows protegió tu PC» (la app no está firmada): «Más información» → «Ejecutar de todas formas». Puede salir dos veces, una por `medir.bat` y otra por `Bastion.exe`.
- Abre el juego ocho veces seguidas, cada una unos 90 segundos, y lo cierra solo. **Son unos 15 minutos.** En cada una juega solo, sin tocar nada:
  - una Limpieza en la ciudad de día;
  - otra de noche;
  - el asedio de El Bastión;
  - la base de una campaña nueva: el Asentamiento, los Soldados y el Mapa.

  Primero lo hace todo con gráficos altos y después con medios.
- **Mientras mide, no usar el PC.** La telemetría solo cuenta los fotogramas con el juego delante; si otra ventana se pone encima, esa parte no se mide.
- Si alguna vez se queda parado, se cierra solo a los dos minutos y medio y sigue con el siguiente caso.
- No toca la partida de verdad ni los ajustes: lo que guarda va a una carpeta `medidas` junto a `medir.bat`.
- Al acabar, la ventana negra dice «Hecho» y espera una tecla.

## 3. Mandar el resultado

- El archivo `medidas\red.log`, dentro de la carpeta `Windows`. Es texto: se puede pegar o mandar tal cual.
- Si algo salió raro (una pantalla negra, un error, que no se abriera), decirlo con lo que se vio.

## 4. Qué sale en `red.log`

Una línea cada 10 segundos de juego, por ejemplo:

```
[rend Medida] combate 240 fps (tope 240 Hz) · p95 5.1 ms · cpu 2.1/3.4 ms (+1.0 de espera) · gpu 3.8/4.6 ms · 520 llamadas · 700 k tri · NVIDIA GeForce RTX 3090 · gráficos altos (Alto) · 2560×1440
```

- `p95`: el fotograma que tarda más que el 95 % de los demás (los peores).
- `cpu a/b`: el trabajo del procesador por fotograma, de media y en el p95; `(+c de espera)`: lo que espera a la pantalla, que no es trabajo.
- `gpu a/b`: lo mismo para la tarjeta gráfica.
- Lo que cuenta para el objetivo es la mayor de las dos medias, cpu o gpu: tiene que ser 4,2 ms o menos en «altos».
- Las líneas `[prueba] medir: …` dicen qué caso empieza y, en la base, qué pantalla.
- Fuera del combate (la base), el juego va como mucho a 60 fps: ahí no hace falta más.

## 5. Si se quiere jugar con Ana

La app de Unity no se instala: se abre `Bastion.exe` con doble clic.

- **Quien aloja** pulsa «Alojar la partida» («Ser anfitrión» en las compilaciones de antes del 26/10). Windows pregunta la primera vez por el cortafuegos: «Permitir» en redes privadas. La partida va por el TCP 41391 (o el siguiente libre, hasta el 41400) y se anuncia en la red por el UDP 41390.
- **La red de casa tiene que estar como «Privada»** (Configuración → Red e Internet → la red → Tipo de perfil). Si está como «Pública», Ana no podrá entrar.
- **Quien se une** ve la partida en «Partidas en tu red» o entra con «Unirse por dirección», con la dirección que sale en la sala de quien aloja.
- Lo que mide cada uno llega al `red.log` de quien aloja: `%USERPROFILE%\AppData\LocalLow\Bastion-Unity\Bastion\red.log` (en el Explorador, pegar la ruta en la barra de direcciones).
- Para medir así, poner antes Ajustes → Gráficos «Altos» y jugar al menos 5 minutos de combate.
