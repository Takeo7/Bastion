using System;
using Bastion.Client.Net;
using Bastion.Engine;
using Bastion.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The base over the network (main.ts's handling of the host's messages on the base; UI's), as CombatSession is the
    /// mission's: AppFlow mounts it with the base's screens while the room's phase is `base`. What the host says (the
    /// room, the campaign, the partner's tab, the proposal's deadline, an order turned down) goes to the controller, and
    /// the controller's messages to the host. It listens to NetLink's Message only while it is up, so what comes after it
    /// goes waits for the battle's scene, its snapshot first.
    /// </summary>
    [RequireComponent(typeof(StrategyRoot))]
    public sealed class BaseSession : MonoBehaviour
    {
        /// <summary>The campaign as the squad took off on the last mission (CombatSession), for the windows of what it changed.</summary>
        public static CampaignState TookOff;

        NetLink net;
        BaseController control;

        public BaseController Control => control;

        void Start()
        {
            net = NetLink.Instance;
            var root = GetComponent<StrategyRoot>();
            control = new BaseController(root, Math.Max(0, net.Slot), msg => net.Send(msg))
            {
                Quit = App.AppFlow.Quit,
                Hosting = () => net.Hosting,
            };
            control.Before = TookOff;
            TookOff = null;
            if (net.LastRoom != null) control.Players(net.LastRoom.Players);
            net.Message += OnMessage;
        }

        void OnDestroy()
        {
            if (net != null) net.Message -= OnMessage;
        }

        void OnMessage(ServerMessage m)
        {
            switch (m)
            {
                case RoomMessage room:
                    control.Players(room.Players);
                    // Off the base (to a mission, or the menu): the rest is for the next scene (the mission's snapshot), in
                    // order. Only the room as it is now: an old one held for this screen says nothing.
                    if (room.Phase != RoomPhases.Base && room == net.LastRoom) net.Message -= OnMessage;
                    break;
                case CampaignMessage campaign:
                    control.Campaign(campaign.State);
                    break;
                case PartnerBaseViewMessage partner:
                    control.PartnerView(partner.Slot, partner.View);
                    break;
                case DeadlineMessage deadline:
                    control.Deadline(deadline.Kind, deadline.Ms);
                    break;
                case RejectedMessage rejected:
                    control.Note(rejected.Message);
                    break;
                case ErrorMessage error:
                    control.Note(error.Message);
                    break;
            }
        }

        /// <summary>Esc for the base (the radio, the board, the menu); Intro and Espacio move the radio on.</summary>
        void Update()
        {
            var keys = Keyboard.current;
            var root = GetComponent<StrategyRoot>();
            if (keys == null || control == null || !root.Ready) return;
            if (keys.escapeKey.wasPressedThisFrame) control.Escape();
            else if (keys.enterKey.wasPressedThisFrame) root.RadioKey("enter");
            else if (keys.spaceKey.wasPressedThisFrame) root.RadioKey("space");
        }
    }
}
