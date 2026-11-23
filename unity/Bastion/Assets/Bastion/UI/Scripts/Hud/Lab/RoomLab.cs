using System.Collections.Generic;
using Bastion.Net;
using Bastion.UI.Start;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The room menu with a made-up room, for captures without a network (HudShots.Room): Arturo hosting and in, Ana
    /// still to come, two addresses to give her. BASTION_SALA: «campana», a game saved (chapter 1, row 3, Medio); «pregunta»,
    /// its «Nueva partida» (the level); «escaramuza», the skirmish's sheet; «direcciones», the addresses unfolded («¿No le sale
    /// tu partida?»). BASTION_SALA_DOS=1: Ana in too, in her colour; BASTION_SALA_ANA=1, seen from Ana's app (her seat, not
    /// hosting); BASTION_SALA_NIVEL: the saved game's level (LevelIds). BASTION_SALA_APARTE=1:
    /// the campaign of before set aside, its line. BASTION_SALA_ROUTER:
    /// the router's state (trying, open, norouter, shared, refused), open with 83.45.12.7:41391 and its key; BASTION_SALA_ABIERTO=1,
    /// the other addresses unfolded, to see them all.
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
            var level = System.Environment.GetEnvironmentVariable("BASTION_SALA_NIVEL");
            if (which == "campana" || which == "pregunta" || System.Environment.GetEnvironmentVariable("BASTION_SALA_GUARDADA") == "1")
                room.SavedCampaign = new SavedCampaign { Chapter = 1, Row = 3, Soldiers = 4, Supplies = 245, NodesPlayed = 7, GameLevel = string.IsNullOrEmpty(level) ? Bastion.Engine.LevelIds.Medium : level };
            var two = System.Environment.GetEnvironmentVariable("BASTION_SALA_DOS") == "1";
            var ana = System.Environment.GetEnvironmentVariable("BASTION_SALA_ANA") == "1";
            if (two || ana) room.Players.Add(new PlayerInfo { Slot = 1, Name = "Ana", Color = "#f59e0b", Connected = true });
            // BASTION_SALA_APARTE=1: the campaign of before the runs set aside (Red's room, 16/11), its line under the choices.
            if (System.Environment.GetEnvironmentVariable("BASTION_SALA_APARTE") == "1") room.SetAside = "save.antes-de-las-runs.json";
            var router = System.Environment.GetEnvironmentVariable("BASTION_SALA_ROUTER") switch
            {
                "trying" => RouterState.Trying,
                "open" => RouterState.Open,
                "norouter" => RouterState.NoRouter,
                "shared" => RouterState.Shared,
                "refused" => RouterState.Refused,
                _ => (RouterState?)null,
            };
            var page = which == "pregunta" ? "new" : which == "escaramuza" ? "skirmish" : which == "direcciones" ? "direcciones" : "main";
            GetComponent<RoomMenu>().Preview(room, addresses, !ana, page, router, router == RouterState.Open ? "83.45.12.7:41391 · KXRT5" : null, ana ? 1 : 0);
        }

        void Update()
        {
            if (System.Environment.GetEnvironmentVariable("BASTION_SALA_ABIERTO") != "1") return;
            var more = GetComponent<UIDocument>().rootVisualElement.Q<Foldout>();
            if (more != null && !more.value) more.value = true;
        }
    }
}
