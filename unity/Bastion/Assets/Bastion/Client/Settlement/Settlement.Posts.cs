using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client
{
    /// <summary>
    /// The siege's posts' soldiers (clearingArt.ts setPosts, playtest 18/10 #58: each player sets their soldiers about the
    /// clearing): a figure standing on each post's tile, on its floor (a watchtower's deck), at rest with its gun in its
    /// hands, looking out to the nearest edge, where the dead come in; kept from one change to the next by who it is.
    /// The rings and the names over them are Settlement.Build's (SetPosts).
    /// </summary>
    public sealed partial class Settlement
    {
        Transform postsRoot;
        /// <summary>Each post's soldier by its id, and what it was made as (its looks, its band's colour, its gun).</summary>
        readonly Dictionary<string, (Stander stander, string made)> postStanders = new Dictionary<string, (Stander, string)>();

        /// <summary>The posts' figures as `list` has them (the shown campaign's soldiers: Show(campaign)); those no longer posted go.</summary>
        void PostFigures(IReadOnlyList<SiegePost> list)
        {
            if (postsRoot == null)
            {
                postsRoot = new GameObject("Los puestos").transform;
                postsRoot.SetParent(transform, false);
            }
            int width = Base?.Width ?? 40, height = Base?.Height ?? 40;
            var kept = new HashSet<string>();
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                var s = lifeCampaign?.Soldiers?.FirstOrDefault(q => q.Id == p.id);
                if (s == null) continue;
                string band = "#" + ColorUtility.ToHtmlStringRGB(p.colour);
                string made = Newtonsoft.Json.JsonConvert.SerializeObject(new object[] { s.Appearance, band, Campaign.SoldierArms(lifeCampaign, s)?.Primary });
                if (!postStanders.TryGetValue(p.id, out var had) || had.made != made)
                {
                    if (had.stander != null) DropStander(had.stander);
                    var made1 = Standing(s, lifeCampaign, 0, Vector3.zero, 0, false, band, postsRoot);
                    if (made1 == null)
                    {
                        postStanders.Remove(p.id);
                        continue;
                    }
                    // (Not all breathing as one.)
                    made1.anim?.Step(i * 0.9f);
                    had = (made1, made);
                    postStanders[p.id] = had;
                }
                kept.Add(p.id);
                float x = p.x + 0.5f, z = p.y + 0.5f;
                // Out to the nearest edge: north, east, south or west.
                var gaps = new[] { z, width - x, height - z, x };
                int @out = Array.IndexOf(gaps, gaps.Min());
                had.stander.yaw = new[] { Mathf.PI, Mathf.PI / 2, 0, -Mathf.PI / 2 }[@out] + (i % 3 - 1) * 0.15f;
                had.stander.root.position = BuildRoot.TransformPoint(World(x, PostFloor(p.x, p.y), z));
                StepStander(had.stander, 0);
            }
            foreach (var id in postStanders.Keys.Where(k => !kept.Contains(k)).ToList())
            {
                DropStander(postStanders[id].stander);
                postStanders.Remove(id);
            }
        }

        /// <summary>How high tile (x, y)'s floor stands (tiles: a watchtower's deck), from the clearing as its siege has it.</summary>
        float PostFloor(int x, int y)
        {
            var s = ground;
            int i = s == null ? -1 : y * s.Width + x;
            return i >= 0 && i < s.Elev.Count ? s.Elev[i] * MapSpace.Storey / MapSpace.Tile : 0;
        }

        void StepPosts(float dt)
        {
            foreach (var p in postStanders.Values) StepStander(p.stander, dt);
        }

        static void DropStander(Stander s)
        {
            s.anim?.Dispose();
            if (s.root != null) DestroyNow(s.root.gameObject);
        }
    }
}
