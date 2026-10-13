# Synty → juego

Convierte los modelos de Synty POLYGON que usa el juego en GLB ligeros, en `packages/client/public/models/synty/<pack>/`.

- Los packs originales viven en `assets-src/synty/`, que no se sube: la licencia no deja redistribuirlos. Solo se suben los convertidos.
- La lista de lo que se usa, y para qué, es una sola: `src/game/syntyManifest.ts`. El conversor convierte exactamente eso y el juego lo lee para saber qué modelo lleva cada unidad, arma, sombrero y objeto del mapa.

## Uso

```sh
npx tsx packages/client/tools/synty/convert.ts               # lo que falta o ha cambiado en el manifiesto
npx tsx packages/client/tools/synty/convert.ts --force       # todo otra vez
npx tsx packages/client/tools/synty/convert.ts hunter sofa   # solo esos ids
```

Sin argumentos convierte lo nuevo y lo que cambió en el manifiesto (lo apunta en `public/models/synty/sources.json`) y borra lo que ya no aparece. Si cambias `convert.py`, usa `--force`.

Hace falta Blender (por defecto `/Applications/Blender.app/Contents/MacOS/Blender`; otro con `BLENDER=/ruta`) y red la primera vez, para que `npx` traiga `@gltf-transform/cli`. Todo junto tarda menos de un minuto.

## Qué hace

1. **Desempaqueta** los packs que solo vienen como `.unitypackage` (City Zombies) en `assets-src/synty/POLYGON_City_Zombies_Extracted/`, una sola vez.
2. **Blender** (`convert.py`, sin ventana):
   - Gira a Y arriba y escala de metros a casillas (`SYNTY_SCALE` = 1/1,5: una casilla son 1,5 m, así que un superviviente de 1,8 m mide 1,2).
   - Exporta sin imágenes. Cada material se llama como el atlas de su pack (`apocalypse`, `town`…), y el juego le pone la textura compartida (`synty.ts`).
   - **Personajes:** los huesos se renombran a los nombres de los packs de animación de Synty (`Hips`, `Spine_01`, `Shoulder_L`…). Dedos, dedos de los pies, IK y cara se funden en su padre: quedan 22 huesos.
     - Pelo, barbas y sombreros cuelgan rígidos de su hueso.
     - Un segundo mapa UV, `part`, dice qué pieza de la armadura es cada vértice (u) y si su texel es tela que se puede teñir (v). La piel, el pelo, las botas y los cinturones no se tiñen.
   - **Armas:** el cañón mira a +Z y la empuñadura queda en el origen; la boca del cañón va en `extras.muzzle`.
   - **Sombreros** (`HEADGEAR`): centrados en la articulación de la cabeza, para colgarlos en el juego.
   - **Objetos:** las mallas se unen por material. El ajuste a la casilla (`fit`, `yaw`, `shift`) se hace en el juego, así que cambiarlo no pide reconvertir.
   - **Piezas de edificio** (`PIECES`, de los kits modulares): como los objetos, pero recortadas antes a la caja `keep` (en metros del FBX). Los kits van en módulos de 2,5 m y una casilla son 1,5 m: el juego los dibuja 1,2 veces más grandes (`KIT_SCALE`) y cada casilla de pared es la mitad central de un módulo, con su puerta o su ventana justo en ella. Con `parts` las mallas quedan separadas (la hoja de una puerta, que se abre).
   - **Atlas:** reducidos al tamaño del manifiesto y guardados en WebP.
   - **Animaciones** (`ANIMATIONS`, de «ANIMATION – Base Locomotion»): cada clip, sin raíz en movimiento, con el esqueleto del pack (solo los huesos que usa el juego) y todos los fotogramas. No se hornean transformaciones: el juego mide cada pose contra la pose en T del pack y la pasa a los huesos de cada personaje (`syntyAnims.ts`), así que el mismo clip sirve para los dos rigs de Synty.
3. **gltf-transform** comprime cada GLB con meshopt sin tocar esqueletos, sombreros ni el mapa `part`.

## Añadir un modelo

1. Busca el FBX en el pack, por ejemplo `POLYGON_Town_SourceFiles_v7/FBX/SM_Prop_Couch_03.fbx`.
2. Añádelo al manifiesto en la tabla que toque:
   - `CHARACTERS`, con sus `attach` (pelo, barba, sombrero) desde `SM_Chr_Attach_*`; para que se pueda elegir en el cuartel, también en `SURVIVOR_CHOICES`;
   - `ANIMATIONS`, con la ruta dentro del pack (los mismos nombres existen para el rig Sidekick), y en `MOTIONS` si es una de las que se mezclan;
   - `WEAPONS`;
   - `HEADGEAR`;
   - `PROPS`;
   - `PIECES` (paredes, puertas y ventanas de edificio; las usa `buildings.ts`);
   - `BASE_MODELS` (la base en superficie, `strategy/bastion.ts`): se convierten como piezas enteras a `base-<id>.glb` y la base les da tamaño, giro y sitio. `fbx` es una ruta dentro del pack, porque Apocalypse guarda en `FBX/Collision` gemelos de colisión con el mismo nombre que un nombre suelto puede encontrar primero.
3. Úsalo donde toque: `SQUAD_BODIES` / `UNIT_BODIES` (quién es quién), `UNIT_WEAPONS`, `HEAD_GEAR` o `PROP_SETS` (qué objeto dibuja cada pista de decorado).
4. Ejecuta el conversor con su id y prueba el juego.

Un objeto mal girado o de tamaño raro se arregla en su entrada de `PROPS`: `yaw` (cuartos de vuelta para que mire al norte, −Z), `fit` (casillas que ocupa su lado mayor) y `shift`.

Lo que se deje de usar se quita del manifiesto y se borra su `.glb`.
