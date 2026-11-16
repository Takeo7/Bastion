using System.Collections.Generic;

namespace Bastion.UI.Gear
{
    /// <summary>The grids' handlers behind one fixed object (the screens hold it; the game sets its target).</summary>
    public sealed class GearRelay : IGearHandlers
    {
        public IGearHandlers Target;

        static readonly GearActionView[] None = new GearActionView[0];

        public string GearRefusal(GearMove m) => Target != null ? Target.GearRefusal(m) : "";
        public void GearMoved(GearMove m) => Target?.GearMoved(m);
        public void GearDropped(string gear) => Target?.GearDropped(gear);
        public IReadOnlyList<GearActionView> GearActions(string gear) => Target != null ? Target.GearActions(gear) : None;
        public void GearAction(string gear, string action) => Target?.GearAction(gear, action);
    }
}
