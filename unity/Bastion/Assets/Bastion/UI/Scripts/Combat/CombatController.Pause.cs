using System;
using System.Linq;
using Bastion.Client.Net;
using Bastion.Engine;
using Bastion.UI.Hud;

namespace Bastion.UI.Combat
{
    public sealed partial class CombatController
    {
        /// <summary>The battle's time stopped or let go (the event player's: Time.timeScale), as the pause comes and goes.</summary>
        public Action<bool> Halt;

        /// <summary>Whether the mission waits now: this player's menu open, or the partner's (the room says whose).</summary>
        public bool Paused { get; private set; }

        /// <summary>What was last said to the host: this player's menu open in the mission.</summary>
        bool pauseSent;

        /// <summary>
        /// The pause (Arturo, 28/10; Ana: Esc didn't stop the game): while either player has the game's menu open in the
        /// mission, it waits for both. This player's menu says so to the host (NetLink.Pause: it holds the orders and the
        /// turn's clock), the battle's time stops on both screens (the event player, the figures, the effects), no order
        /// goes but the answer to the partner's call to evacuate, and the partner's open menu is said over the battle.
        /// Every frame, from the menu as it is: Esc, «Menú», «Continuar» and a question all count.
        /// </summary>
        void PauseTick()
        {
            var mine = Hud != null && Hud.MenuOpen && State.Outcome == null;
            if (mine != pauseSent)
            {
                pauseSent = mine;
                NetLink.Instance?.Pause(mine);
            }
            var partner = Players.FirstOrDefault(p => p.Slot != MySlot && p.Paused == true);
            var paused = mine || partner != null;
            if (paused != Paused)
            {
                Paused = paused;
                Halt?.Invoke(paused);
            }
            Hud?.SetPause(!mine && partner != null ? HudText.Menu.PartnerPaused(partner.Name) : null, HudText.Menu.PartnerPausedSub);
        }

        /// <summary>Whether `c` waits for the pause to end (all orders but the answer to the partner's call to evacuate).</summary>
        bool HeldByPause(Command c) => Paused && !(c is EvacAnswerCommand);
    }
}
