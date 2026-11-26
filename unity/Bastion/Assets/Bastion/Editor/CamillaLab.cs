// La Camilla (0.14.11): the first soldier to fall lies badly hurt till it is carried to La Lata (Unit.Downed, and Lame).
// The pictures for Arturo's choice of how it lies. A lab alone.
using System;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Client.Units;
using Bastion.Engine;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    public static partial class CombatLab
    {
        /// <summary>
        /// Two soldiers on the plain floor, each with a rifle and a pistol (Arms): the first made Downed and Lame, as the engine
        /// leaves the one La Camilla takes (today's view: sat with the wounded's splint); the second falls as one killed does
        /// (UnitViews.Die) and lies there, for «lying, without the splint». From their side, near; BASTION_VIEW=game, from
        /// the game's camera at BASTION_DIST tiles (24).
        ///   unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Camilla -shotPlay -shotCamera "Shot camera"
        ///     -shotWarm 5 -shotFrames 1 -shotOut &lt;file&gt;
        /// </summary>
        public static void Camilla()
        {
            var first = MapLab.Golden("city-7");
            var ids = first.Units.Where(u => u.Alive && u.Team == "xcom").Take(2).Select(u => u.Id).ToList();
            Plain(u => ids.Contains(u.Id), 9, 14);
            var sat = Put(state.Units.First(u => u.Id == ids[0]), 10, 10, -Math.PI / 2);
            var lying = Put(state.Units.First(u => u.Id == ids[1]), 13, 10, -Math.PI / 2);
            foreach (var u in new[] { sat, lying })
            {
                u.Template = "rookie";
                u.Arms = new Arms { Primary = "assaultRifle", Pistol = true };
            }
            var camera = GameObject.Find("Shot camera")?.GetComponent<Camera>() ?? new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.farClipPlane = 400;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.72f, 0.78f, 0.86f);
            var middle = (MapSpace.World(10, 10) + MapSpace.World(13, 10)) / 2;
            if (Environment.GetEnvironmentVariable("BASTION_VIEW") == "game")
                MapSpace.Aim(camera, middle, float.TryParse(Environment.GetEnvironmentVariable("BASTION_DIST"), out float d) ? d : 24);
            else
            {
                camera.fieldOfView = 46;
                // (BASTION_LADO=otro: from their other side.)
                float side = Environment.GetEnvironmentVariable("BASTION_LADO") == "otro" ? 1 : -1;
                camera.transform.position = middle + new Vector3(0.4f, 1.2f, 4.3f * side);
                camera.transform.LookAt(middle + Vector3.up * 0.35f);
            }
            sequence = async () =>
            {
                try
                {
                    units.Sync(state);
                    Reveal();
                    await Awaitable.WaitForSecondsAsync(4f);
                    // (As UI's EventPlayer plays «downed»: the fall, then the event settled.)
                    Debug.Log($"[combat] {Time.time:0.00} s: goes down");
                    await units.GoDown(sat.Id);
                    sat.Downed = true;
                    sat.Lame = true;
                    sat.Hp = 0;
                    units.Sync(state);
                    Debug.Log($"[combat] {Time.time:0.00} s: sat");
                    await Awaitable.WaitForSecondsAsync(2.5f);
                    // (BASTION_CAMILLA=hombros: the other takes it up, as the engine has one carried: not alive on the board, CarriedBy.)
                    if (Environment.GetEnvironmentVariable("BASTION_CAMILLA") == "hombros")
                    {
                        sat.CarriedBy = lying.Id;
                        sat.Pos = new Pos { X = lying.Pos.X, Y = lying.Pos.Y, Z = lying.Pos.Z };
                        units.Sync(state);
                        Debug.Log($"[combat] {Time.time:0.00} s: carried");
                    }
                    await Awaitable.WaitForSecondsAsync(4f);
                    Debug.Log($"[combat] {Time.time:0.00} s: done");
                }
                catch (Exception e)
                {
                    Debug.LogError("[combat] " + e);
                }
            };
            EditorApplication.playModeStateChanged += Started;
        }
    }
}
