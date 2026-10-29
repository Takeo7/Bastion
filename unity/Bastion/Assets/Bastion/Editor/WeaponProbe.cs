using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// How Unity's Apocalypse guns come (where their origin is, which way the barrel runs): each one's box in its own
    /// frame, for the grip (UnitViews). unity/tools/unity.sh -executeMethod Bastion.EditorTools.WeaponProbe.Run -quit
    /// </summary>
    public static class WeaponProbe
    {
        public static void Run()
        {
            // (BASTION_ARMAS=name,…: others of Apocalypse's, its models.)
            var names = System.Environment.GetEnvironmentVariable("BASTION_ARMAS")?.Split(',')
                ?? new[] { "SM_Wep_AssaultRifle_01", "SM_Wep_Shotgun_01", "SM_Wep_SniperRifle_01", "SM_Wep_MachineGun_01", "SM_Wep_SubMGun_01", "SM_Wep_HuntingRifle_01", "SM_Wep_Pistol_01", "SM_Wep_Baton_01" };
            foreach (string name in names)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Synty/PolygonApocalypse/Prefabs/Weapons/Guns/{name}.prefab")
                    ?? AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Synty/PolygonApocalypse/Models/{name}.fbx");
                if (prefab == null)
                {
                    Debug.Log($"[weapons] {name}: not found");
                    continue;
                }
                var o = (GameObject)Object.Instantiate(prefab);
                var renderers = o.GetComponentsInChildren<Renderer>();
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                string parts = string.Join(", ", o.GetComponentsInChildren<Transform>().Select(t => $"{t.name} {t.localPosition} {t.localEulerAngles}"));
                Debug.Log($"[weapons] {name}: size {b.size:F3}, min {b.min:F3}, max {b.max:F3}; {parts}");
                Object.DestroyImmediate(o);
            }
        }
    }
}
