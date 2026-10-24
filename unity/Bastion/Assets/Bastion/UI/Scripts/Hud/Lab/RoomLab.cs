using System.Collections.Generic;
using Bastion.Net;
using Bastion.UI.Start;
using UnityEngine;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The room menu with a made-up room, for captures without a network (HudShots.Room): Arturo hosting and in, Ana
    /// still to come, two addresses to give her. BASTION_SALA: «campana», a saved campaign (as the sample's: 8 outings,
    /// level 3, Marea 5); «pregunta», its «Nueva campaña» asked; «escaramuza», the skirmish's sheet.
    /// </summary>
    [RequireComponent(typeof(RoomMenu))]
    public sealed class RoomLab : MonoBehaviour
    {
        void Start()
        {
            var room = new RoomMessage
            {
                Phase = RoomPhases.Lobby,
                Players = new List<PlayerInfo> { new PlayerInfo { Slot = 0, Name = "Arturo", Color = "#a78bfa", Connected = true } },
            };
            var addresses = new List<HostAddress>
            {
                new HostAddress { Address = "192.168.1.20:41391", Kind = "lan" },
                new HostAddress { Address = "100.101.12.7:41391", Kind = "tailscale" },
            };
            var which = System.Environment.GetEnvironmentVariable("BASTION_SALA") ?? "";
            if (which == "campana" || which == "pregunta") room.SavedCampaign = new SavedCampaign { Day = 20, Missions = 8, Doom = 5, Level = 3 };
            GetComponent<RoomMenu>().Preview(room, addresses, true, which == "pregunta" ? "ask" : which == "escaramuza" ? "skirmish" : "main");
        }
    }
}
