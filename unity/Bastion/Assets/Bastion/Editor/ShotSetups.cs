using Bastion.Client;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>Setups for Shots (-shotSetup) that check the tool itself: what a shot and a strip come out as.</summary>
    public static class ShotSetups
    {
        /// <summary>A floor, three shapes and the camera on them: colour, light and shadow in one shot.</summary>
        public static void Primitives()
        {
            var lit = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            GameObject Shape(PrimitiveType type, Vector3 at, Color colour, Vector3 size)
            {
                var o = GameObject.CreatePrimitive(type);
                o.transform.position = at;
                o.transform.localScale = size;
                var m = new Material(lit) { color = colour };
                o.GetComponent<Renderer>().sharedMaterial = m;
                return o;
            }
            Shape(PrimitiveType.Plane, Vector3.zero, new Color(0.55f, 0.5f, 0.45f), Vector3.one);
            Shape(PrimitiveType.Cube, new Vector3(-2, 0.5f, 0), new Color(0.8f, 0.2f, 0.15f), Vector3.one).AddComponent<Spinning>();
            Shape(PrimitiveType.Sphere, new Vector3(0, 0.75f, 1), new Color(0.2f, 0.5f, 0.85f), Vector3.one * 1.5f);
            Shape(PrimitiveType.Capsule, new Vector3(2, 1, 0), new Color(0.9f, 0.8f, 0.3f), Vector3.one);
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 5, -8);
            camera.transform.LookAt(new Vector3(0, 0.5f, 0));
        }

        /// <summary>Turns about its upright a quarter turn a second, by hand: what a strip should show moving.</summary>
        sealed class Spinning : MonoBehaviour, IShotStep
        {
            public void ShotStep(float dt) => transform.Rotate(0, 90 * dt, 0);
        }
    }
}
