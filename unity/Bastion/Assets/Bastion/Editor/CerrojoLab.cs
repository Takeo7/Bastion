using System;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// T14 (Arturo, 18/11: the bolt-action rifle fires as an assault rifle): the hunting rifle measured in its prefab's frame
    /// (as the figures hold it: WeaponArt's palm and fore are in it), to place its bolt handle for the gesture after a shot.
    /// Each part's box, and the points standing out furthest to its right and its left, slice by slice along it (z, 2 cm).
    ///   unity/tools/unity.sh -executeMethod Bastion.EditorTools.CerrojoLab.Medir -quit
    /// </summary>
    public static class CerrojoLab
    {
        public static void Medir()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonApocalypse/Prefabs/Weapons/Guns/SM_Wep_HuntingRifle_01.prefab");
            var gun = UnityEngine.Object.Instantiate(prefab);
            var points = gun.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh != null).SelectMany(f => f.sharedMesh.vertices.Select(v => gun.transform.InverseTransformPoint(f.transform.TransformPoint(v)))).ToList();
            var min = points.Aggregate(Vector3.one * 1e9f, Vector3.Min);
            var max = points.Aggregate(-Vector3.one * 1e9f, Vector3.Max);
            Debug.Log(FormattableString.Invariant($"[cerrojo] box x {min.x:0.000}..{max.x:0.000}, y {min.y:0.000}..{max.y:0.000}, z {min.z:0.000}..{max.z:0.000}"));
            foreach (var g in points.GroupBy(p => Mathf.FloorToInt(p.z * 50)).OrderBy(g => g.Key))
            {
                var right = g.OrderByDescending(p => p.x).First();
                var left = g.OrderBy(p => p.x).First();
                var top = g.Max(p => p.y);
                var bottom = g.Min(p => p.y);
                Debug.Log(FormattableString.Invariant($"[cerrojo] z {g.Key / 50f:0.00}: x {left.x:0.000}..{right.x:0.000} (right at y {right.y:0.000}), y {bottom:0.000}..{top:0.000}"));
            }
            UnityEngine.Object.DestroyImmediate(gun);
        }
    }
}
