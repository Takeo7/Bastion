using System.Collections.Generic;
using UnityEngine;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The camping (runs, H9 §5): its caravans (Building.trailer), each Apocalypse's mobile home whole (Caravans.Make) on the
    /// ground storey, in place of the walls and roof a building of kit pieces has (WallLayout and BuildingSurfaces leave them
    /// out), which the cutaway, the fog and the night take as any building of the plan; and its fire lit (CampFire). Its
    /// pines are the base's (Woods, Beyond: BaseWoods.Pinar).
    /// </summary>
    public sealed partial class MapBuilder
    {
        readonly List<GameObject> campArt = new List<GameObject>();

        /// <summary>The camping's caravans and fire, those there were gone (BuildingParts: once the cutaway has its buildings).</summary>
        void Camping()
        {
            foreach (var o in campArt) DestroyNow(o);
            campArt.Clear();
            if (plan == null) return;
            foreach (var b in plan.Buildings)
            {
                if (b.trailer == null) continue;
                var o = Bastion.Client.Map.Caravans.Make(Storeys[0], terrain, b, b.trailer.Value, Missing);
                if (o != null) campArt.Add(o);
            }
            var fire = CampFire.Make(Storeys[0], s);
            if (fire != null) campArt.Add(fire.gameObject);
        }

        /// <summary>The caravans' doors' leaves as their doors are now (Rebuild: a door opened or shut changes the walls).</summary>
        void CampDoors()
        {
            foreach (var o in campArt)
                if (o != null && o.GetComponent<Bastion.Client.Map.Caravans.CaravanView>() is Bastion.Client.Map.Caravans.CaravanView view) view.Doors(terrain);
        }
    }
}
