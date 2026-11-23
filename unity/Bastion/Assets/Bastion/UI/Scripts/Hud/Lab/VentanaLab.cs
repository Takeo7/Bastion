using Bastion.UI.App;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// A loading window on its own for the captures (HudShots.Ventana, cuadro 136): BASTION_VENTANA picks which, with the
    /// words the game would give it (a stage of the sample map, its squad of two players; the way back from a stage won).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class VentanaLab : MonoBehaviour
    {
        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            foreach (var sheet in new[] { "Hud/Mixta" })
                if (Resources.Load<StyleSheet>(sheet) is StyleSheet s) root.styleSheets.Add(s);
            var which = System.Environment.GetEnvironmentVariable("BASTION_VENTANA") ?? "etapa";
            LoadView v;
            // R22 (Red, 23/11): the sheets out of the start screen through the game's own choice (AppFlow.SheetFor), with the
            // room's words as Red saw them; «r22» checks every case into the log and shows the one named after it.
            if (which.StartsWith("r22"))
            {
                v = R22(which);
                var shown = new LoadWindow(v);
                root.Add(shown.Root);
                return;
            }
            switch (which)
            {
                case "titulo": v = new LoadView { Title = Bastion.UI.Start.StartText.Title }; break;
                // (All on paper but the title since the cuadro 139.)
                case "continuar": v = new LoadView { Paper = true, Kicker = LoadText.GoingOn, Title = Bastion.UI.Start.StartText.SavedLine(1, "Medio") }; break;
                case "nueva": v = new LoadView { Paper = true, Kicker = LoadText.New, Title = Bastion.UI.Start.StartText.SavedLine(1, "Supervivencia") }; break;
                case "a-medias": v = new LoadView { Paper = true, Kicker = LoadText.GoingOn, Title = Bastion.UI.Start.StartText.SavedLine(1, "Difícil"), Place = "Etapa a medias: Saqueo · Campo" }; break;
                case "escaramuza": v = new LoadView { Paper = true, Kicker = Bastion.UI.Start.RoomText.Skirmish, Title = "Eliminación", Place = "Ciudad" }; break;
                case "sigue": v = new LoadView { Paper = true, Kicker = LoadText.GoOn, Title = "Etapa superada", Place = "Saqueo · Pueblo", Line = "Traéis 145 §, un botiquín y 2 cargadores." }; break;
                case "vuelta":
                case "vuelta-papel":
                    v = new LoadView { Paper = true, Kicker = LoadText.Back, Title = "Escuadra perdida", Place = "Saqueo · Pueblo", Line = "Nadie ha vuelto a La Lata." };
                    break;
                default:
                    v = new LoadView
                    {
                        Paper = true, Kicker = LoadText.Going, Title = "Rescate", Symbol = "place:camping", Place = "Camping", Danger = "Peligro 2",
                        Line = "Alguien sigue vivo en el camping. Llegad hasta él y volved todos a La Lata.", WhoHead = LoadText.Who(4),
                    };
                    v.Notes.Add(("de noche", false));
                    v.Notes.Add(("Sin plaza libre", true));
                    v.Who.Add(("Anja", "#a78bfa"));
                    v.Who.Add(("Rafael", "#a78bfa"));
                    v.Who.Add(("Yuki", "#f59e0b"));
                    v.Who.Add(("Mateo", "#f59e0b"));
                    break;
            }
            var window = new LoadWindow(v);
            root.Add(window.Root);
            if (which == "etapa") window.Progress(0.5f);
        }
    
        /// <summary>
        /// The six sheets out of the start screen (R22), each through AppFlow.SheetFor as the game makes it, with what the
        /// room says then (the new game already made, chapter 1; a stage half played with InMission, or without it as before
        /// Red's fix): each checked against the words it must have, a line in the log; the one named (r22-<case>) shown.
        /// </summary>
        static LoadView R22(string which)
        {
            Bastion.Net.SavedCampaign Saved(int nodes, int? row, string inMission) => new Bastion.Net.SavedCampaign { Chapter = 1, GameLevel = "medium", NodesPlayed = nodes, Row = row, InMission = inMission, Soldiers = 4, Supplies = 120 };
            var skirmish = new LoadView { Paper = true, Kicker = Bastion.UI.Start.RoomText.Skirmish, Title = "Limpieza" };
            var cases = new (string id, LoadView v, string kicker, string place)[]
            {
                // «Nueva partida»: the room has made it already (chapter 1, nothing played): «Empezando», not «Continuando».
                ("nueva", AppFlow.SheetFor(false, AppFlow.Asked.New, null, Saved(0, null, null), "medium"), LoadText.New, null),
                ("continuar", AppFlow.SheetFor(false, AppFlow.Asked.GoOn, null, Saved(5, 3, null), "medium"), LoadText.GoingOn, null),
                // «Continuar partida» into a stage half played, with Red's InMission and without it (as in the 0.14.2).
                ("a-medias", AppFlow.SheetFor(true, AppFlow.Asked.GoOn, null, Saved(5, 3, "Saqueo · Campo"), "medium"), LoadText.GoingOn, "Etapa a medias: Saqueo · Campo"),
                ("a-medias-sin-sala", AppFlow.SheetFor(true, AppFlow.Asked.GoOn, null, Saved(5, 3, null), "medium"), LoadText.GoingOn, null),
                ("escaramuza", AppFlow.SheetFor(true, AppFlow.Asked.Skirmish, skirmish, Saved(5, 3, null), "medium"), Bastion.UI.Start.RoomText.Skirmish, null),
                // Whoever joins presses nothing: the room's words.
                ("unido-nueva", AppFlow.SheetFor(false, AppFlow.Asked.None, null, Saved(0, null, null), "medium"), LoadText.New, null),
                ("unido-continuar", AppFlow.SheetFor(false, AppFlow.Asked.None, null, Saved(5, 3, null), "medium"), LoadText.GoingOn, null),
                ("unido-a-medias", AppFlow.SheetFor(true, AppFlow.Asked.None, null, Saved(5, 3, "Saqueo · Campo"), "medium"), LoadText.GoingOn, "Etapa a medias: Saqueo · Campo"),
                ("unido-escaramuza", AppFlow.SheetFor(true, AppFlow.Asked.None, null, Saved(5, 3, null), "medium"), Bastion.UI.Start.RoomText.Skirmish, null),
            };
            LoadView named = null;
            var failed = 0;
            foreach (var (id, v, kicker, place) in cases)
            {
                var ok = v.Kicker == kicker && v.Place == place && v.Paper;
                if (!ok) failed++;
                Debug.Log($"[r22] {id}: «{v.Kicker} · {v.Title}{(v.Place != null ? " · " + v.Place : "")}» {(ok ? "bien" : $"MAL (se esperaba «{kicker}»{(place != null ? " · " + place : "")})")}");
                if (which == "r22-" + id) named = v;
            }
            Debug.Log($"[r22] {cases.Length - failed} de {cases.Length} bien");
            return named ?? cases[0].v;
        }
    }
}
