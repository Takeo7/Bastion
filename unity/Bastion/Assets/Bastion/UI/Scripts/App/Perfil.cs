using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace Bastion.UI.App
{
    /// <summary>
    /// Where a frame's main-thread time goes, over some frames (`BASTION_PERFILAR=&lt;frames&gt;`, AppFlow adds it): every
    /// profiler marker of the scripts, the UI, rendering and waiting that Unity offers here, averaged per frame, and the
    /// costliest in the log («[perfil] …»). In the editor and development builds; a release build offers few markers.
    /// </summary>
    public sealed class Perfil : MonoBehaviour
    {
        /// <summary>Frames measured, after 30 of settling.</summary>
        public int Frames = 300;

        readonly List<(string name, ProfilerRecorder rec)> recorders = new List<(string, ProfilerRecorder)>();
        readonly Dictionary<string, double> sums = new Dictionary<string, double>();
        int skip = 30;
        int frames;

        void Start()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            var wanted = new[] { ProfilerCategory.Scripts.Name, ProfilerCategory.Gui.Name, ProfilerCategory.Render.Name, ProfilerCategory.Internal.Name, ProfilerCategory.Network.Name, ProfilerCategory.Loading.Name };
            foreach (var h in handles)
            {
                var d = ProfilerRecorderHandle.GetDescription(h);
                if (d.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds || !wanted.Contains(d.Category.Name)) continue;
                recorders.Add((d.Name, ProfilerRecorder.StartNew(d.Category, d.Name, 1)));
            }
            Debug.Log($"[perfil] {recorders.Count} markers for {Frames} frames");
        }

        void Update()
        {
            if (skip-- > 0) return;
            frames++;
            foreach (var (name, rec) in recorders)
            {
                if (!rec.Valid || rec.LastValue <= 0) continue;
                sums[name] = (sums.TryGetValue(name, out var v) ? v : 0) + rec.LastValue;
            }
            if (frames < Frames) return;
            var top = sums.OrderByDescending(p => p.Value).Take(30).Select(p => $"{p.Key} {p.Value / frames / 1e6:0.000}");
            Debug.Log($"[perfil] {frames} frames, ms per frame: " + string.Join(" · ", top));
            foreach (var (_, rec) in recorders) rec.Dispose();
            recorders.Clear();
            Destroy(this);
        }

        void OnDestroy()
        {
            foreach (var (_, rec) in recorders) rec.Dispose();
        }
    }
}
