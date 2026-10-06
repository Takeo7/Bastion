# A0 — Informe técnico de la fase A

Estado: **pendiente de revisión**. No hay código de implementación hasta que apruebes este informe.

`<MOD>` es un marcador para `{{NOMBRE_MOD}}`, que aún no está definido. Se sustituirá en A1 en nombres de clase, ini y tag de log.

---

## 0. Fuentes y convenciones de cita

Todas las referencias se han comprobado leyendo el código fuente. Formato: `PREFIJO:ruta:línea`.

| Prefijo | Fuente | Versión |
|---|---|---|
| `CHL` | `X2CommunityCore/X2WOTCCommunityHighlander`, `X2WOTCCommunityHighlander/Src/` | v1.31.2, commit `6ec2e9b` (2026-09-05) |
| `ORIG` | Copia pública de `Development/SrcOrig` de WotC (`daakru/xcom2-wotc-modding`, `Firaxis/XCOM/SrcOrig/`) | WotC SDK |
| `RJSS` | `robojumper/robojumperSquadSelect` | HEAD 2023-07-25 |

Notas:
- El repositorio de la Highlander solo contiene las clases que modifica (343 en `XComGame`). Para el resto cito `ORIG`. Cuando una clase existe en ambos, cito `CHL`, porque es la que se ejecuta.
- **Antes de A1 hay que comparar con el `SrcOrig` de tu SDK** (ver §11). Las líneas de `ORIG` vienen de una copia de terceros. Coinciden con WotC en todo lo que he contrastado contra la Highlander, pero no es tu copia.
- Etiquetas: **[V]** verificado en el código fuente. **[RT]** verificado en el código, pero su comportamiento exacto se comprueba en partida en el hito indicado. **[UNVERIFIED]** sin verificar.

---

## 1. Arquitectura propuesta (vista general)

| Componente | Tipo | Función |
|---|---|---|
| `<MOD>_Config` | `Object`, `config(<MOD>)` | Nombres, colores, `bAutoAssignNewSoldiers`, `bStrictMode`, tecla de fin de fase. Si falta algún nombre, `IsEnabled()` = false y todo el mod queda inerte. |
| `X2DownloadableContentInfo_<MOD>` | DLCInfo | Ciclo de vida: arranque, nueva campaña, carga de partida y pre-misión. |
| Valor de unidad `'<MOD>_Owner'` | Datos en `XComGameState_Unit` | Guarda la propiedad del soldado. Ver §3. |
| `XComGameState_<MOD>Campaign` | `XComGameState_BaseObject` (estrategia) | Alternancia de asignación y token del hueco impar. |
| `XComGameState_<MOD>Phase` | `XComGameState_BaseObject`, `bTacticalTransient=true` | Estado de fases del turno táctico. Sobrevive a save/load dentro de la misión. |
| `X2EventListener_<MOD>` | `CHEventListenerTemplate` | Inicio y fin del turno del jugador, y eventos de UI de la Highlander. |
| `XComTacticalController_<MOD>` | **MCO** de `XComTacticalController` | Filtro de Tab / siguiente unidad, redirección de la autoselección y modo estricto. Ver §4. |
| `UIScreenListener`s | UISL | Armería, lista de personal, Squad Select y HUD táctico. |
| `UI<MOD>PhaseBanner` | `UIPanel` | Banner "Fase de <jugador>", botón "Terminar mi fase" e indicador del modo laxo. |

**Solo hay una MCO** (`XComTacticalController`). El Squad Select **no** lleva MCO, para no chocar con RJSS (ver §6.3).

---

## 2. Tabla requisito → hook

| Req. | Mecanismo | Hook verificado |
|---|---|---|
| 1. Config y modo inerte | Clase de config + DLCInfo | `X2DownloadableContentInfo.OnPostTemplatesCreated` `CHL:XComGame/Classes/X2DownloadableContentInfo.uc:105` **[V]** |
| 2. Propiedad persistente | Valor de unidad con `eCleanup_Never` | `XComGameState_Unit.SetUnitFloatValue` / `GetUnitValue` / `ClearUnitValue` `CHL:…/XComGameState_Unit.uc:1381-1383`. Enum `EUnitValueCleanup` `ORIG:…/X2TacticalGameRulesetDataStructures.uc:509-514` **[V]**. Detalle en §3. |
| 2. Incluye SPARK y héroes | `IsSoldier()` | `CHL:…/XComGameState_Unit.uc:8924`. SPARK: `bIsSoldier = true` en `ORIG:DLC_3/Classes/X2Character_DLC_Day90Characters.uc:69`. Héroes: `CreateSoldierTemplate('ReaperSoldier'/'SkirmisherSoldier'/'TemplarSoldier')` en `ORIG:…/X2Character_DefaultCharacters.uc:4142/4167/4196` **[V]** |
| 3. Cambiar dueño en la pantalla del soldado | Evento CHL + `UIListItemString` | `'OnArmoryMainMenuUpdate'` (EventData = `UIList`, Source = `UIArmory_MainMenu`) `CHL:…/UIArmory_MainMenu.uc:238`. Botón autocontenido con `ButtonBG.OnClickedDelegate` (Issue #47) `CHL:…/UIArmory_MainMenu.uc:40,450`. Unidad: `UIArmory.UnitReference` `ORIG:…/UIArmory.uc:26` **[V]** |
| 3. Etiqueta en la lista de soldados | Evento CHL + `UIText` hijo | `'UIPersonnel_OnSortFinished'`, que se dispara al final de `RefreshData()` `CHL:…/UIPersonnel.uc:312-320`. Lista `m_kList` `CHL:…/UIPersonnel.uc:50`. Unidad `UIPersonnel_ListItem.UnitRef` `ORIG:…/UIPersonnel_ListItem.uc:6` **[V]**. Posición de la etiqueta **[RT A2]** |
| 3/5. Etiqueta en el Squad Select | UISL + refresco por timer (vanilla) o evento RJSS | `UISquadSelect.m_kSlotList` `CHL:…/UISquadSelect.uc:20`, `UISquadSelect_ListItem.SlotIndex` `CHL:…/UISquadSelect_ListItem.uc:12`. Con RJSS: `'rjSquadSelect_ExtraInfo'` `RJSS:…/robojumper_UISquadSelect_ListItem.uc:441` **[V]**. Correspondencia `SlotIndex` → `XComHQ.Squad[i]` **[RT A3]** |
| 4. Soldados nuevos | Barrido perezoso (§7). Ningún evento cubre todos los casos. | `XComGameState_HeadquartersXCom.AddToCrew` **no emite evento** `CHL:…/XComGameState_HeadquartersXCom.uc:693`. Lo llaman recompensas, órdenes, el reclutamiento y las misiones xpack (búsqueda en `ORIG`) **[V]** |
| 4. Instalación a mitad de campaña | Objeto de campaña ausente ⇒ marcar existentes como "Sin asignar" | `InstallNewCampaign` `CHL:…/X2DownloadableContentInfo.uc:63`, `OnLoadedSavedGame` `:45` **[V]** |
| 5. Reparto y aviso | UISL + diálogo, sin MCO | `UISquadSelect.LaunchButton` `CHL:…/UISquadSelect.uc:21`, `OnLaunchMission` `:1456`, `CreateOrUpdateLaunchButton` (reinicializa el delegate) `:250-295` **[V]** |
| 5. Hueco impar alterno | Token en el objeto de campaña, actualizado en `OnPreMission` | `OnPreMission(StartGameState, MissionState)` `CHL:…/X2DownloadableContentInfo.uc:72`, invocado en `ORIG:…/XGStrategy.uc:541` antes de añadir el start state **[V]** |
| 6. Fases y alternancia del primer jugador | Listener de `'PlayerTurnBegun'` / `'PlayerTurnEnded'` | `CHL:…/X2TacticalGameRuleset.uc:4880` y `:4955`. No se disparan al cargar partida (`:4846`) ni en turnos de interrupción (`:4859`). Contador `PlayerTurnCount += 1` en `ORIG:…/XComGameStateContext_TacticalGameRule.uc:187` **[V]** |
| 6. Banner | UISL sobre `UITacticalHUD` + `UIPanel` | `UIScreenListener.OnInit` `ORIG:…/UIScreenListener.uc:19` **[V]**. Posición **[RT A4]** |
| 7. Tab solo del jugador activo | **MCO** | `XComTacticalController.Visualizer_CycleToNextAvailableUnit` `CHL:…/XComTacticalController.uc:532` y `Visualizer_SelectUnit` `:189` **[V]**. §4 |
| 7. Botón y atajo "Terminar mi fase" | `UIButton` + `SubscribeToOnInputForScreen` (CHL Issue #501) | `CHL:…/UIScreenStack.uc:868`. La UI recibe el input antes que el juego: `ORIG:…/XComInputBase.uc:508,523` **[V]** |
| 7. Fin de turno tras la fase 2 | Fin de turno nativo, automático | `XComTacticalController.PerformEndTurn(ePlayerEndTurnType_PlayerInput)` `CHL:…/XComTacticalController.uc:1374`. En standalone llama a `InternalPerformEndTurn` sin diálogo `:1382-1384` **[V]**. Decisión en §5.3 |
| 8. Modo estricto / laxo | La misma MCO (`Visualizer_SelectUnit`) | Igual que el req. 7 **[V]** |
| 9. Pool sin dueño | Regla de dueño efectivo (§9) | — |
| 9. Control mental | Fuente del efecto `'MindControl'` | `GetUnitAffectedByEffectState` `ORIG:…/XComGameState_Unit.uc:7673`, `IsMindControlled` `:7690`, `EffectName="MindControl"` `ORIG:…/X2Effect_MindControl.uc:215`, `EffectAppliedData.SourceStateObjectRef` `ORIG:…/X2TacticalGameRulesetDataStructures.uc:339`. Precedente de uso: `ORIG:…/X2StatusEffects.uc:1018` **[V]** |
| 10. Saltar fase sin operativos | Comprobación al abrir la fase | `IsAlive` `ORIG:…/XComGameState_Unit.uc:7662`, `IsIncapacitated` `:7689`, `bRemovedFromPlay` `:121`, `bCaptured` `:190`. `UnitHasActionsAvailable` `CHL:…/X2TacticalGameRuleset.uc:4526` **[V]** |
| 11. Sin cambios de reglas | Por diseño (§10) | — |

APIs de soporte **[V]**:
- `X2EventListenerTemplate.RegisterInTactical/RegisterInStrategy/AddEvent`: `ORIG:…/X2EventListenerTemplate.uc:21,22,25`.
- `CHEventListenerTemplate.AddCHEvent`: `CHL:…/CHEventListenerTemplate.uc:75`.
- `XComGameStateContext_ChangeContainer.CreateChangeState`: `ORIG:…:48`.
- `XComGameState.CreateNewStateObject/ModifyStateObject`: `ORIG:…/XComGameState.uc:151,144`.
- `X2GameRuleset.SubmitGameState`: `CHL:…/X2GameRuleset.uc:657`.
- Macros `` `XCOMHISTORY ``, `` `XEVENTMGR ``, `` `TACTICALRULES ``, `` `GAMERULES ``, `` `HQPRES ``: `ORIG:Core/Globals.uci:186-227`.

---

## 3. Persistencia de la propiedad

**Decisión: valor de unidad `'<MOD>_Owner'` (float) con `eCleanup_Never`.**

| Valor | Significado |
|---|---|
| ausente | Soldado aún no procesado por el mod (= "nuevo") |
| `0` | Sin asignar |
| `1` | Jugador 1 |
| `2` | Jugador 2 |

Por qué funciona:
1. **Sobrevive a save/load.** Los valores de unidad forman parte del `XComGameState_Unit` serializado.
2. **No se borra entre capas.** Solo se limpian `eCleanup_BeginTurn` (`ORIG:…/XComGameState_Unit.uc:6439`), `eCleanup_BeginTactical` (`CHL:…/XComGameState_Unit.uc:2388`, en `OnBeginTacticalPlay` `:2353`) y `eCleanup_BeginTacticalChain` (`:2412`). `eCleanup_Never` no se limpia nunca. Precedente: la propia Highlander guarda `'CH_StartMissionWill'` con `eCleanup_Never` (`CHL:…/XComGameState_Unit.uc:2410`).
3. **Viaja con la unidad.** El paso de estrategia a táctico usa el mismo historial y los mismos ObjectID (`ORIG:…/XGStrategy.uc:311-555`). Al volver, `CreateStrategyGameStartFromTactical` copia la versión más reciente de cada objeto que existía antes de la misión (`ORIG:…/XComGameStateContext_StrategyGameRule.uc:237-285`).
4. **Seguro a mitad de campaña.** Un valor ausente se trata explícitamente (§7). Si se quita el mod, el valor queda huérfano y es inofensivo.
5. **Un soldado capturado** conserva su dueño: el valor vive en la unidad, no en la tripulación.

Descartados:
- **Componente** (`AddComponentObject`): más complejo y no aporta nada sobre el valor de unidad.
- **Tabla en un objeto singleton** (ObjectID → dueño): sincronización manual y riesgo de huérfanos.

Objeto `XComGameState_<MOD>Campaign` (singleton de estrategia):
- `NextAutoOwner` (1/2): alternancia de asignación.
- `OddSlotOwner` (1/2): quién recibe el hueco extra si la escuadra es impar.
- Persiste en el save. Si se modifica dentro del start state táctico, vuelve a estrategia por el mecanismo del punto 3.

Objeto `XComGameState_<MOD>Phase` (táctico):
- `bTacticalTransient=true` (`ORIG:…/XComGameState_BaseObject.uc:28`).
- Además, los objetos creados en táctico no vuelven a estrategia (comentario en `ORIG:…/XComGameStateContext_StrategyGameRule.uc:271-273`).

---

## 4. Filtrado del ciclo de unidades (requisitos 7 y 8)

### 4.1 Por qué una MCO
`Visualizer_SelectUnit` es el único punto por el que pasa toda selección de unidad. La Highlander no tiene ningún evento ni override en este flujo: he buscado `SelectUnit`/`Cycle`/`UnitSelect` en todo el repo. Llamadas encontradas en `ORIG`:

| Origen | Línea | Qué es |
|---|---|---|
| Tab / Shift+Tab | `XComTacticalInput.uc:2123,2170` → `SelectNext/PreviousUnit` `XComTacticalController.uc:592,600` | Ciclo manual |
| Fin de visualización | `XComTacticalController.uc:666` | La unidad se quedó sin acciones → `SelectNextUnit` |
| Ruleset | `X2TacticalGameRuleset.ActionsAvailable` `CHL:4987` → `XGPlayer.OnUnitActionPhase_ActionsAvailable` `ORIG:XGPlayer.uc:929-937` | Autoselección del siguiente miembro del grupo con acciones |
| Clic en unidad | `XComTacticalInput.uc:2669,2850` | Selección manual |
| Acción visual | `X2Action_SelectNextActiveUnit.uc:17` (usado por `X2Effect_GrantActionPoints`) | Selecciona la unidad que recibe PA |
| Pánico | `XGUnit.uc:2945` | `SelectNextUnit`, o `CheckForEndTurn` si falla |

`CheckForEndTurn` está declarada **sin cuerpo** en `ORIG:…/XGPlayerNativeBase.uc:52` y nadie la sobrescribe. Por tanto, que nuestro ciclo filtrado devuelva `false` **no puede forzar un fin de turno**. El fin automático de turno lo decide solo `ActionsAvailable` sobre todos los miembros del grupo, y no lo tocamos.

Precedentes de MCO sobre esta clase:
- Workshop 2129065433 (`WOTC_UnitSelectionOrder`)
- Workshop 1267323416 (MisclickConfirm)
- `chrishayesmu/XCOM2-Direct-Control`

### 4.2 Diseño de la MCO (mínima y delegando en `super`)
```
class XComTacticalController_<MOD> extends XComTacticalController;
// MCO: XComEngine.ini +ModClassOverrides=(BaseGameClass="XComTacticalController", ModClass="<MOD>.XComTacticalController_<MOD>")

var private bool bOwnerFilterActive;   // true only while our filtered cycle runs

simulated function bool Visualizer_CycleToNextAvailableUnit(int Direction)
  -> if !PhasesActive(): return super(...)
  -> bOwnerFilterActive = true; result = super.Visualizer_CycleToNextAvailableUnit(Direction); bOwnerFilterActive = false

simulated function bool Visualizer_SelectUnit(XComGameState_Unit SelectedUnit)
  -> if !PhasesActive(): return super(...)
  -> MaybeAutoAdvancePhase()                   // active player out of actions -> next phase
  -> if bOwnerFilterActive && !IsSelectableInPhase(unit): return false   // vanilla loop skips it
  -> if bStrictMode && OwnedByInactivePlayer(unit): return false
  -> if IsAutoSelectFromRuleset(unit): redirect to the active player's next unit
  -> result = super(...); update lax-mode indicator; return result
```
- **No se copia código vanilla.** El bucle de `super.Visualizer_CycleToNextAvailableUnit` llama de forma virtual a nuestro `Visualizer_SelectUnit`, que rechaza las unidades fuera de fase. El bucle las salta solo.
- Con `IsMyModEnabled() == false`, ambas funciones hacen solo `return super...`. El comportamiento queda idéntico al vanilla.
- **Redirección de la autoselección** (heurística **[RT A4]**). La unidad pedida es del jugador inactivo, la unidad actualmente controlada ya no tiene acciones y el jugador activo sí tiene unidades con acciones ⇒ se selecciona la siguiente unidad del jugador activo. En el siguiente tick, `ActionsAvailable` ve que la unidad activa tiene acciones y no vuelve a pedir otra (`CHL:…/X2TacticalGameRuleset.uc:5031-5039`). El estado es estable.
- **Qué recorre Tab:** unidades del jugador activo + unidades del pool (§9) con acciones. El orden es el vanilla (`GetUnits`).

### 4.3 `PhasesActive()`
Solo es verdadero si se cumple todo lo siguiente:
- El mod está habilitado.
- Existe `XComGameState_<MOD>Phase` con una fase abierta.
- El jugador que actúa es el jugador XCOM (`GetCachedUnitActionPlayerRef()` `CHL:…/X2TacticalGameRuleset.uc:5860`, `TeamFlag == eTeam_XCom` `ORIG:…/XComGameState_Player.uc:15`).
- **No hay interrupción en curso** (`BattleData.InterruptingGroupRef.ObjectID <= 0` `ORIG:…/XComGameState_BattleData.uc:155`).

Durante interrupciones (Battlelord, Interrupt del Skirmisher) se aplica el comportamiento vanilla.

---

## 5. Fases tácticas (requisitos 6, 7 y 10)

### 5.1 Máquina de estados
1. **`'PlayerTurnBegun'`** con `XComGameState_Player.TeamFlag == eTeam_XCom`:
   - `FirstOwner = (PlayerTurnCount impar) ? J1 : J2`.
   - Abre la fase 1 para `FirstOwner`.
   - Si `FirstOwner` no tiene unidades **operativas** (req. 10), salta directamente a la fase 2. Si tampoco las tiene el otro jugador, se queda en "fase común" (solo pool) y el banner lo indica.
2. **Fin de fase 1.** Ocurre por botón o atajo, o automáticamente cuando el jugador activo no tiene unidades con acciones (`UnitHasActionsAvailable`). Se abre la fase 2 para el otro jugador y se selecciona su primera unidad con acciones.
3. **Fin de fase 2.** Ocurre por botón o atajo, o automáticamente igual que la 1. Se llama al **fin de turno nativo** (§5.3).
4. **`'PlayerTurnEnded'`** (XCOM): cierra las fases. El banner muestra "Turno enemigo".

**"Operativa"** = la unidad cumple todo esto:
- Su dueño efectivo es P.
- La controla el jugador XCOM.
- `IsAlive()`, `!IsIncapacitated()`, `!bRemovedFromPlay` (cubre la evacuación) y `!bCaptured`.

### 5.2 Persistencia y save/load
- Cada cambio de fase es un `CreateChangeState` sobre `XComGameState_<MOD>Phase`, enviado con `` `TACTICALRULES.SubmitGameState``.
- Al cargar a mitad de turno, `'PlayerTurnBegun'` **no** se dispara (`CHL:…/X2TacticalGameRuleset.uc:4846`). Se recupera la fase guardada y el UISL de `UITacticalHUD` redibuja el banner.
- Solo se añaden frames de historial que cambian un objeto propio. No afecta a RNG, IA ni habilidades.

### 5.3 Decisión: tras la fase 2, fin de turno **nativo automático**
Al cerrarse la fase 2 (por botón o por agotamiento), el mod llama a `PerformEndTurn(ePlayerEndTurnType_PlayerInput)`. Es exactamente lo que hace la tecla de fin de turno vanilla.

Justificación:
1. **Semántica 1:1.** Que J1 pulse "Terminar mi fase" equivale a decir "he terminado este turno". Cuando ambos han terminado, el turno XCOM está terminado. No queda nada que decidir.
2. **Evita una "fase 3" ambigua.** Con fin manual, el juego autoseleccionaría las unidades de J1 que aún tuvieran acciones (`ActionsAvailable`). Eso reabre de hecho la fase de J1 sin que nadie lo haya pedido.
3. **No cambia reglas.** Las acciones sobrantes se pierden igual que con el fin de turno vanilla. El botón nativo "Fin de turno" sigue disponible en cualquier momento.
4. En la fase 2, el botón se llama **"Terminar fase y turno"** para que nadie se lleve una sorpresa.
5. **No se llama nunca** durante interrupciones ni fuera del turno XCOM (§4.3).

### 5.4 Atajo de teclado
- Se registra con `SubscribeToOnInputForScreen(UITacticalHUD, ...)` (CHL #501).
- Configurable en el ini. **Por defecto: `B`** (`FXS_KEY_B`). No tiene manejador en `XComTacticalInput`, estado `ActiveUnit_Moving` (`ORIG:…/XComTacticalInput.uc:1742-2700`), y ninguna habilidad vanilla la usa como `DefaultKeyBinding`.
- **Descartadas:**
  - `Fin`/`End` y `Retroceso`: **ya terminan el turno** en vanilla (`ORIG:…/XComTacticalInput.uc:2462-2472`).
  - `J`, `U`: waypoints.
  - `F1-F3`.
- Con ciertos mods (§12) la tecla puede quedar ocupada **[RT A4]**.

---

## 6. Inyección de UI

### 6.1 Armería: pantalla del soldado
- Listener de `'OnArmoryMainMenuUpdate'` (strategy). Añade un `UIListItemString` con el texto "Dueño: <nombre en su color>".
- Clic → J1 → J2 → Sin asignar → J1. Se aplica con un change state y se repuebla la lista.
- Tooltip: "Cambia el jugador que controla a este soldado".
- Patrón de botón autocontenido de la Highlander (Issue #47): el clic va por `ButtonBG.OnClickedDelegate`, no por índice (`CHL:…/UIArmory_MainMenu.uc:468-473`).

### 6.2 Lista de soldados (Personal)
- Listener de `'UIPersonnel_OnSortFinished'`. Se dispara tras cada `RefreshData()`, y todos los refrescos pasan por ahí (`CHL:…/UIPersonnel.uc:721,834,953`).
- Para cada item de `m_kList`: un `UIText` hijo con el nombre del dueño coloreado, reutilizado si ya existe.
- Cubre también `UIPersonnel_SquadSelect` (el selector de soldados del Squad Select), porque hereda de `UIPersonnel`.

### 6.3 Squad Select — sin MCO
**RJSS hace MCO de `UISquadSelect`** (`RJSS:robojumperSquadSelect/Config/XComEngine.ini:12`). Una segunda MCO sobre la misma clase entraría en conflicto. Por eso todo va por UISL. Filtramos con `UISquadSelect(Screen) != none`, que cubre la subclase de RJSS.
- **Etiquetas por hueco:**
  - Vanilla: `UIText` hijo en cada `UISquadSelect_ListItem`.
  - Con RJSS: el hook `'rjSquadSelect_ExtraInfo'`, más limpio.
- **Panel de reparto** fijo en la cabecera: "Reparto: J1 3/3 · J2 2/3 · hueco extra: J1". Verde si cumple, ámbar si no.
- **Aviso al lanzar:** se sustituye `LaunchButton.OnClickedDelegate` por nuestro handler.
  - Reparto correcto: llama a `OnLaunchMission(LaunchButton)` original.
  - Reparto incorrecto: diálogo "El reparto no es equitativo: … ¿Lanzar igualmente?" con [Lanzar igualmente] / [Volver].
  - `CreateOrUpdateLaunchButton` reinicializa el botón y su delegate en cada refresco. Por eso un panel invisible propio reaplica el handler con un timer de 0,25 s. También refresca las etiquetas, porque vanilla no emite evento al terminar `UpdateData` **[RT A3]**.
- El refresco se engancha con `OnInit`/`OnReceiveFocus`, y con RJSS además con `'rjSquadSelect_UpdateData'` (`RJSS:…/robojumper_UISquadSelect.uc:599`).

### 6.4 HUD táctico
- UISL sobre `UITacticalHUD`.
- `UI<MOD>PhaseBanner` anclado arriba en el centro, con:
  - "Fase de <jugador>" en su color.
  - Botón "Terminar mi fase" / "Terminar fase y turno".
  - En modo laxo, si la unidad seleccionada es del otro jugador: "⚠ Moviendo unidad de <otro>".
- La posición exacta se ajusta en A4 para no tapar objetivos, el temporizador ni el HUD de los Chosen **[RT A4]**.

---

## 7. Asignación de soldados nuevos (requisito 4)

`AddToCrew` no emite evento, y los soldados llegan por al menos 6 rutas (reclutas, recompensas, rescates, héroes, Proving Ground, covert actions). Por eso usamos un **barrido idempotente** `AssignNewSoldiers()`:

1. Si no existe `XComGameState_<MOD>Campaign` ⇒ **instalación a mitad de campaña**:
   - Se crea el objeto.
   - Todos los soldados existentes reciben `0` (Sin asignar): tripulación actual, capturados por los Chosen (`XComGameState_AdventChosen.CapturedSoldiers` `ORIG:…:98`) y unidades con `bCaptured`.
2. Cualquier soldado de `XComHQ.Crew` (`ORIG:…/XComGameState_HeadquartersXCom.uc:40`) con `IsSoldier()` y **sin** valor de dueño es nuevo. Se recorren en orden de ObjectID ascendente:
   - Si `bAutoAssignNewSoldiers` está activo, recibe `NextAutoOwner` y se alterna.
   - Si no, recibe `0`.
3. Un solo change state por barrido. Si no hay cambios, no se envía nada.

**Cuándo se ejecuta:** antes de cualquier pantalla que muestre el dueño (Armería, Personal, Squad Select), y en `OnLoadedSavedGameToStrategy` (`CHL:…/X2DownloadableContentInfo.uc:53`) / `OnExitPostMissionSequence` (`:97`). Así ningún jugador llega a ver a un soldado sin procesar.

**Campaña nueva:** `InstallNewCampaign` crea el objeto en el start state. Los soldados iniciales cuentan como nuevos y se alternan.

**Excluidos:** los reclutas aún no contratados (`XComGameState_HeadquartersResistance.Recruits`). No están en `Crew` y se asignan al contratarse.

---

## 8. Reparto en el Squad Select (requisito 5)

- `N` = número de soldados desplegados (`XComHQ.Squad` no vacíos).
  - Objetivo: `ceil(N/2)` para el dueño de `OddSlotOwner` y `floor(N/2)` para el otro.
  - Si `N` es par, `N/2` cada uno.
- Los soldados "Sin asignar" no cuentan para nadie y generan su propia línea en el aviso.
- **Alternancia del hueco impar:** en `OnPreMission`, **solo si `N` es impar**, se invierte `OddSlotOwner`.
  - Decisión: alternar solo cuando el hueco extra se usa de verdad.
  - Si se alternara en cada misión, una secuencia impar–par–impar le daría los dos huecos extra al mismo jugador.
- Misiones multi-escuadra (`XComHQ.AllSquads`) y escuadras forzadas: se tratan en A5.

---

## 9. Unidades sin dueño y control mental (requisito 9)

`EffectiveOwner(Unit)`:
1. Si `Unit.IsMindControlled()`, se busca `GetUnitAffectedByEffectState('MindControl')` → `ApplyEffectParameters.SourceStateObjectRef` → dueño efectivo de la unidad fuente. Si la fuente no tiene dueño, la unidad pasa al pool.
   - Cubre Dominación y Control mental del Psi Op, y el hackeo de robots y torretas.
   - Todos usan `X2Effect_MindControl` con el `EffectName` por defecto (`ORIG:…/X2StatusEffects.uc:906-935`, `X2Ability_HackRewards.uc:96+`).
2. Si no, se usa el valor `'<MOD>_Owner'`: 1 o 2 es ese jugador; 0 o ausente es el pool.
3. Pool: VIPs, unidades de resistencia controlables, unidades temporales y soldados "Sin asignar". **Siempre seleccionables**, en cualquier fase y también en modo estricto. Entran en el ciclo de Tab.

Se calcula al vuelo, sin guardarlo. El control mental termina cuando se elimina el efecto (`X2Effect_MindControl.OnEffectRemoved` `ORIG:…:83`) y el dueño vuelve solo.

---

## 10. Garantía de "sin cambios de reglas" (requisito 11)

El mod **solo**:
- escribe un valor de unidad con nombre propio;
- crea dos objetos de estado propios;
- añade UI;
- en táctico, decide **qué unidad se selecciona** y puede pulsar el fin de turno nativo.

No toca plantillas de habilidad, personaje u objeto, IA, RNG, recompensas, dificultad ni economía. No escucha eventos de combate para modificarlos: solo lee `'PlayerTurnBegun'` y `'PlayerTurnEnded'`.

---

## 11. Lo que no es implementable o tiene límites

| # | Límite | Consecuencia |
|---|---|---|
| L1 | Con input compartido por Parsec, el mod **no puede saber quién actúa**. | Las fases son un protocolo de UI. El modo estricto solo limita **qué** se puede seleccionar, no **quién** lo hace. |
| L2 | El modo estricto bloquea la selección, pero no puede impedir que el otro jugador mueva el ratón en la fase ajena. | Aceptado (L1). |
| L3 | Con mando, la ruta del botón X (`CHL:…/UISquadSelect.uc:1799-1803`) llama a `OnLaunchMission` sin pasar por el botón, y se salta el diálogo. | No afecta a teclado y ratón. El panel de reparto fijo sigue visible. |
| L4 | Si otro mod del stack hace MCO de `XComTacticalController`, **solo una gana**. | Incompatibilidad dura. Hay que revisar el `{{MOD_STACK}}`. Casos conocidos: Unit Selection Order, MisclickConfirm, Direct Control. |
| L5 | Interrupciones de WotC (Battlelord, Interrupt). | Las fases se suspenden y se aplica el comportamiento vanilla (§4.3). |
| L6 | El juego termina el turno solo cuando ninguna unidad XCOM tiene acciones. | Es nativo y no se toca. Coincide con nuestro fin automático. |
| L7 | El SDK de WotC (ModBuddy / X2ModBuildCommon) solo funciona en Windows. **Este Mac no puede compilar ni probar el mod.** | Tú compilas en Windows y me pasas errores y `Launch.log` (pregunta Q3). |

---

## 12. Riesgos abiertos y UNVERIFIED

| ID | Riesgo | Hito |
|---|---|---|
| R1 | Las líneas de `ORIG` vienen de una copia de terceros. | Hay que contrastarlas con tu `Development/SrcOrig` antes de A1. |
| R2 | Heurística de redirección de la autoselección (§4.2): un clic manual en una unidad del otro jugador justo cuando la unidad activa se queda sin acciones se redirige. Es una ventana muy corta. | RT A4 |
| R3 | Refresco por timer en el Squad Select para reaplicar el delegate de lanzamiento. | RT A3 |
| R4 | `SlotIndex` → `XComHQ.Squad[SlotIndex]` (vanilla usa `SlotListOrder` para el orden visual). | RT A3 |
| R5 | Fuente del efecto de control en el hackeo (`HackRewardControlRobot`): se asume que la fuente es el hacker. Si no, la unidad cae al pool, que es un fallback seguro. | RT A4 |
| R6 | La tecla por defecto `B` puede estar ocupada por un mod del stack. | RT A4 |
| R7 | Compatibilidad con mods que rediseñan los list items (p. ej. Extended Personnel Info): la posición de las etiquetas. | Depende del MOD_STACK |
| R8 | Si el MOD_STACK incluye LWotC: escuadras de infiltración, otro Squad Select y otro flujo de misión. | Hay que reevaluar A3 entero |

Ningún identificador del SDK o de la Highlander citado en este informe es **[UNVERIFIED]**. Los puntos inciertos son de comportamiento en partida (**[RT]**) y están en la tabla.

---

## 13. Nota adelantada para B0

La premisa "Tactical Co-op de Team Dragonpunk, sin código disponible" **no es correcta**: el código está publicado en `github.com/quixotic-cloud/Tactical-Co-Op`.
- Licencia GPLv3, último commit 2017-03-30, 18 clases propias.
- Incluye `X2TacticalCoOpGameRuleset`, `XComCoOpTacticalController`, `XComCoOpTacticalGame`, `XComCoOpReplayMgr` y `XComCo_Op_ConnectionSetup`.

Es para XCOM 2 sin WotC. Será la referencia principal del spike B0, aunque no condiciona nada de la fase A.

---

## 14. Ideas v2 (fuera de alcance)

- Color del dueño en los unit flags tácticos.
- Estadísticas por jugador en el resumen de misión.
- Configuración en MCM en lugar del ini.
- Filtro "por dueño" en la lista de Personal.
- Autorrelleno del Squad Select respetando el reparto.
