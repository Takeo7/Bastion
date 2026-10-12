# Equipo y reparto

Quién hace qué en Bastión, qué archivos toca cada uno y en qué está ahora. Lo mantiene **Gameplay**, que coordina. Antes de tocar un archivo de otro rol, se le pregunta por mensaje.

## Roles

| Rol | Quién | Hace | Sus archivos |
|---|---|---|---|
| **Gameplay** (coordina) | Sesión Gameplay | Reglas, campaña, servidor, la lógica de las pantallas y el reparto del trabajo | `engine/src/` (reglas: `rules.ts`, `campaign.ts`, `abilities.ts`, `aim.ts`, `ai.ts`, `setup.ts`, `mapgen.ts`…), `server/src/room.ts`, `client/src/strategy/screen.ts`, `client/src/main.ts` |
| **Lore** | Sesión Lore | El mundo y todos los textos: nombres, capítulos, encargos, radio, glosario, fichas | `engine/src/lore.ts`, `quests.ts`, `transmissions.ts`, `docs/LORE.md` (el de zombis; el del Coro está en `docs/archive/LORE-coro.md`); los textos (no la lógica) de `content.ts`, `campaign.ts`, `progression.ts`, `classes.ts`, `setup.ts`, `mapgen.ts` |
| **Arte** | Agente de Arte | El aspecto: modelos Synty, conversión, animaciones, escenas 3D, iluminación, efectos, estilo de la interfaz | `client/tools/synty/`, `client/public/models/`, `client/src/game/` (vista: `models.ts`, `figures.ts`, `unitViews.ts`, `mapView.ts`, `props.ts`, `fx.ts`, `post.ts`), `client/src/strategy/bastion.ts`, `geoscape.ts`, `stage.ts`, el CSS |
| **Combate (interfaz)** | Agente de combate | El HUD y el control en combate: avisos, fichas, teclas, cámara | `client/src/game/game.ts`, `client/src/ui/hud.ts`, `client/src/styles.css` |
| **Red** | Sesión Networking | Conexión, https, informes del navegador del compañero, el servidor de juego de los jugadores | `client/src/net.ts`, `report.ts`, `ui/lobby.ts`, `audio/`, `server/src/index.ts`, `tls.ts` |

Las sesiones **Arte**, **Clases** y **UX/UI** de antes ya no existen. Sus archivos son ahora de Arte (aspecto) y de Gameplay (clases y pantallas).

## Normas

- Nada de Prettier ni formateadores: el código está formateado a mano.
- Ediciones exactas y pequeñas en archivos compartidos, nunca reescribir un archivo entero de otro.
- Nadie hace commit sin avisar a Gameplay, que agrupa y sube.
- Cada uno prueba con su propio servidor:

  | Quién | Servidor | Cliente |
  |---|---|---|
  | Gameplay | `bastion-test` (3310) | `bastion-gp-client` (5193) |
  | Combate | `bastion-ux-server` (3330) | `bastion-ux-client` (5197) |
  | Arte | `bastion-clases-server` (3340) | `bastion-clases-client` (5199) |
  | Red | 3390 (https en 3391), arrancado a mano | — |

  Nunca se usan ni se paran el 3000/3001 (la partida de los jugadores) ni el 5173.
- Los packs de Synty originales viven en `assets-src/synty/`, que no se sube: la licencia no deja redistribuirlos. Al repositorio solo van los modelos convertidos que use el juego.

## En marcha (2026-10-12)

| Quién | Qué |
|---|---|
| Gameplay | Coordinar. Notas de la segunda partida (base y campaña: hechas). Fase 2 de zombis cuando acabe la 1. |
| Lore | Fase 1 de zombis terminada (todos los textos). Espera a la fase 2 para los consejos de ruido y las fichas de enemigo. |
| Arte | Prueba de Synty terminada: conversión (`tools/synty`), supervivientes, zombis, saqueadores y todos los muebles y coberturas en combate. Falta: animaciones (las tiene que bajar el usuario), paredes y suelo, la base y el globo. |
| Combate | Terminado (notas de la segunda partida). Libre para la fase 2 de zombis. |
| Agente de mapas (Arte) | Terminado: niebla de guerra (`explored.ts`, `fogOfWar.ts`), zoom y encuadre de Instalaciones, z-fighting. `mapView.ts` y `props.ts` pasan a Arte. |
| Red | El cuelgue del Safari de Ana con el sonido. |

## Plan de zombis (fases)

1. Reskin: textos y nombres (Lore). Gameplay aplica los de las pantallas.
2. Saqueo, ruido e IA de zombis (Gameplay; Arte, los muebles registrables y los zombis).
3. Infección y medicinas (Gameplay; Lore, los textos).
4. Comida y armas que se encuentran o se fabrican (Gameplay).
5. El mapa del valle de Valdesombra en lugar del globo (Arte; Gameplay, la lógica).
