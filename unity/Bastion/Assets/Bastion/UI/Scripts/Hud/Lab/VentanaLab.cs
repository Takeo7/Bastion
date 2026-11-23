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
    }
}
