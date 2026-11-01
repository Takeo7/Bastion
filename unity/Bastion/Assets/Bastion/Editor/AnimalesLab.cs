using System;
using System.Linq;
using Bastion.Client.Map;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// The animals about a map (Fauna; Gameplay 1/11) on MapLab's (BASTION_MAP, city-1 unless said), and a stand-in for
    /// someone (a grey post a man's height) walking at a soldier's pace towards them from BASTION_DESDE tiles off (9), so
    /// they are seen to run or fly off; the camera turned BASTION_GIRO (45).
    ///   Perros: on the first dog. Pajaros: on the first crows. Campo: on the first wild one (a country map, wilds-7).
    /// The camera on it (between it and the stand-in's start, a third of the way) BASTION_DIST tiles off (12).
    ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.AnimalesLab.Perros -shotFrames 80 -shotFps 10 -shotWarm 0.5 …
    /// </summary>
    public static class AnimalesLab
    {
        public static void Perros() => Show("Dog");
        public static void Pajaros() => Show("Crow");
        public static void Campo() => Show("Wild");

        static float Number(string name, float otherwise) =>
            float.TryParse(Environment.GetEnvironmentVariable(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : otherwise;

        static void Show(string kind)
        {
            MapLab.Day();
            string name = Environment.GetEnvironmentVariable("BASTION_MAP") ?? "city-7";
            var state = MapLab.Golden(name);
            // (The map makes its own, as the game's does: MapBuilder.)
            var fauna = Fauna.Current;
            if (fauna == null)
            {
                Debug.LogWarning("[animales] the map made no animals");
                return;
            }
            // (The one nearest the map's middle: one by the edge runs off it at once.)
            var middle = MapSpace.World(state.Width / 2, state.Height / 2);
            // (BASTION_ANIMAL=k: the k-th so, not the first.)
            int skip = (int)Number("BASTION_ANIMAL", 0);
            var first = fauna.Animals.Where(a => a.kind == kind).OrderBy(a => Vector3.Distance(a.root.position, middle)).Skip(skip).FirstOrDefault().root;
            if (first == null)
            {
                Debug.LogWarning($"[animales] no {kind} on {name}");
                return;
            }
            // The stand-in: from so many tiles off (to the south-east, the camera's side) towards the animal, at 1.4 m/s.
            var post = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            post.name = "Persona";
            UnityEngine.Object.DestroyImmediate(post.GetComponent<Collider>());
            post.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            post.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.55f, 0.55f, 0.6f) };
            var goal = new Vector3(first.position.x, 0, first.position.z);
            var start = goal + new Vector3(1, 0, -1).normalized * Number("BASTION_DESDE", 9) * MapSpace.Tile;
            float walked = 0, delay = 1;
            void Place() => post.transform.position = Vector3.MoveTowards(start, goal, Mathf.Max(0, walked - delay) * 1.4f) + Vector3.up * 0.9f;
            Place();
            Fauna.ExtraThreats = () => new[] { post.transform.position };
            Fauna.Stepping = dt =>
            {
                walked += dt;
                Place();
            };
            var camera = GameObject.Find("Shot camera").GetComponent<Camera>();
            // (On the animal, at its height, the stand-in coming in from the bottom of the picture.)
            MapSpace.Aim(camera, Vector3.Lerp(first.position, start, 0.3f), Number("BASTION_DIST", 12), Number("BASTION_GIRO", 45));
            var tile = MapSpace.WorldToTile(first.position);
            Debug.Log(FormattableString.Invariant($"[animales] {kind} {first.name} on {tile.x},{tile.y} at {first.position.y:0.00} m; ") + string.Join(", ", fauna.Animals.Select(a => $"{a.kind} {MapSpace.WorldToTile(a.root.position)}")));
        }
    }
}
