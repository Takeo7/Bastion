using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bastion.Client
{
    /// <summary>
    /// What entering a stage takes, said by the game itself (Arturo's M2, 23/11: «se congela la imagen» on choosing a stage;
    /// Producto: one line a stage, so the app tells). From a map's build on: the figures' catalog (if it was loaded now),
    /// the state (if whoever makes it says so: State), the map (MapView.Build), the figures (UnitViews' first Sync on it)
    /// and the first frame drawn with it, beside the second (what the first pays for alone: shaders, textures going up).
    /// One line «[carga] etapa …» to the log and to Said (Red's red.log), once that second frame is out. In play only.
    /// </summary>
    public static class StageLoad
    {
        /// <summary>The line, as it is said: for whoever keeps it (Red: red.log).</summary>
        public static event Action<string> Said;

        /// <summary>The first frame with the stage's map and figures on it is out: a loading screen can go (UI's, cuadro 136).</summary>
        public static event Action FirstDrawn;

        /// <summary>
        /// Whether that frame is out already, for whoever asks after it (the event is gone by then: UI's LiftVeil subscribed a
        /// frame late and waited its whole patience, the 0.14.2's review). False from a map's build till its figures are drawn.
        /// </summary>
        public static bool Drawn { get; private set; }

        /// <summary>What making the state took (ms), if its maker says so before the map is built; forgotten once said.</summary>
        public static double State = -1;

        static string what;
        static double catalog, map, figures = -1, first = -1, second = -1, began;
        static bool waiting, hooked;
        static int frames;
        /// <summary>How many frames a map's figures are waited for before its line is said without them.</summary>
        const int Patience = 1800;

        /// <summary>Whether a map has been built and its figures are still to come (UnitViews times its Sync).</summary>
        public static bool Waiting => waiting && figures < 0;

        /// <summary>The figures' catalog loaded now, in so many ms (FigureCatalog.Load, the once it does).</summary>
        public static void Catalog(double ms)
        {
            Hook();
            catalog += ms;
        }

        static bool scenes;

        /// <summary>
        /// A scene loaded: what the catalog took in the one before is not this stage's (the base loads it for its own
        /// figures, and its second and a half was said in the first stage's line).
        /// </summary>
        static void Hook()
        {
            if (scenes) return;
            scenes = true;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
            {
                catalog = 0;
                // (Nor is the stage before's frame this scene's: none drawn yet.)
                Drawn = false;
            };
        }

        /// <summary>What the catalog has taken so far in this stage (ms): UnitViews takes off its Sync what of it fell inside.</summary>
        public static double CatalogSoFar => catalog;

        /// <summary>A map built in so many ms (MapView.Build): its stage's line begins.</summary>
        public static void Map(string name, double ms)
        {
            if (!Application.isPlaying) return;
            what = name;
            map = ms;
            figures = first = second = -1;
            frames = 0;
            waiting = true;
            Drawn = false;
            Hook();
            if (hooked) return;
            hooked = true;
            RenderPipelineManager.beginContextRendering += Begin;
            RenderPipelineManager.endContextRendering += End;
        }

        /// <summary>The figures put on the map in so many ms (UnitViews.Sync, the first after the map).</summary>
        public static void Figures(double ms)
        {
            if (!waiting || figures >= 0) return;
            figures = ms;
            // (The first frame drawn is the first with them on it.)
            first = second = -1;
        }

        static void Begin(ScriptableRenderContext context, System.Collections.Generic.List<Camera> cameras) => began = Now();

        static void End(ScriptableRenderContext context, System.Collections.Generic.List<Camera> cameras)
        {
            if (!waiting) return;
            double ms = Now() - began;
            frames++;
            // (Its figures may come some frames on, behind a loading screen: waited for, not for ever.)
            if (figures < 0 && frames < Patience) { if (first < 0) first = ms; return; }
            if (first < 0)
            {
                first = ms;
                Drawn = true;
                try { FirstDrawn?.Invoke(); } catch (Exception e) { UnityEngine.Debug.LogException(e); }
                return;
            }
            second = ms;
            waiting = false;
            string Ms(double v) => v < 0 ? "-" : v.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
            string line = $"[carga] etapa {what}: catálogo {Ms(catalog)} · estado {Ms(State)} · mapa {Ms(map)} · figuras {Ms(figures)} · primer dibujo {Ms(first)} (el segundo {Ms(second)}) ms";
            catalog = 0;
            State = -1;
            UnityEngine.Debug.Log(line);
            try { Said?.Invoke(line); } catch (Exception e) { UnityEngine.Debug.LogException(e); }
        }

        static double Now() => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

        /// <summary>A stopwatch's reading (ms), for those who time a stage: Now() later, less this.</summary>
        public static double Clock() => Now();
    }
}
