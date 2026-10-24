using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Net;
using Bastion.Engine;
using Bastion.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Start
{
    /// <summary>
    /// The room menu, between joining and the mission (lobby.ts's skirmish sheet; docs/UNITY-UI.md): the commanders and
    /// their state, where the partner joins (the host's addresses, with «Copiar»), the mission, the map (the city alone in
    /// the first milestone), day or night, and start (the `start` message). It reads NetLink.LastRoom and never listens to
    /// NetLink.Message: what the host sends waits there for the game, its snapshot first (AppFlow loads the battle when
    /// the room's phase is mission). «Ir al inicio» leaves; a host with the partner in is warned first.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class RoomMenu : MonoBehaviour
    {
        const string MissionKey = "bastion.room.mission";
        const string NightKey = "bastion.room.night";

        VisualElement root;
        NetLink link;
        string key;
        string mission;
        bool night;
        bool warned;
        /// <summary>Made-up room and addresses for a capture (Preview); null, NetLink's.</summary>
        RoomMessage previewRoom;
        IReadOnlyList<HostAddress> previewAddresses;
        bool previewHosting;

        void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            var tree = Resources.Load<VisualTreeAsset>("Start/Room");
            if (tree == null)
            {
                Debug.LogError("Room: Resources/Start/Room.uxml missing");
                return;
            }
            doc.rootVisualElement.Clear();
            tree.CloneTree(doc.rootVisualElement);
            root = doc.rootVisualElement.Q("room");
            link = NetLink.Instance;
            mission = PlayerPrefs.GetString(MissionKey, "elimination");
            if (!RoomText.Missions.Any(m => m.Id == mission)) mission = "elimination";
            night = PlayerPrefs.GetInt(NightKey, 0) == 1;
            warned = false;
            key = null;
            root.Q<Label>("mission-head").text = RoomText.Mission.ToUpperInvariant();
            root.Q<Label>("map-head").text = RoomText.Map.ToUpperInvariant();
            root.Q<Label>("hour-head").text = RoomText.Hour.ToUpperInvariant();
            root.Q<Button>("start").clicked += StartMission;
            root.Q<Button>("leave").clicked += Leave;
            Draw();
        }

        /// <summary>A room to show without a connection (HudShots.Room): its players, the host's addresses, hosting or not.</summary>
        public void Preview(RoomMessage room, IReadOnlyList<HostAddress> addresses, bool hosting)
        {
            previewRoom = room;
            previewAddresses = addresses;
            previewHosting = hosting;
            key = null;
        }

        RoomMessage Room => previewRoom ?? link.LastRoom;
        IReadOnlyList<HostAddress> Addresses => previewAddresses ?? link.Addresses;
        bool Hosting => previewRoom != null ? previewHosting : link.Hosting;
        int Slot => previewRoom != null ? 0 : link.Slot;

        void Update()
        {
            if (root == null) return;
            var room = Room;
            // Drawn again only when who is in, their state, or the choices change.
            var k = room == null ? "" : string.Join("|", room.Players.Select(p => $"{p.Slot}:{p.Name}:{p.Connected}:{p.Away}"));
            k += $"#{Hosting}#{mission}#{night}#{warned}";
            if (k == key) return;
            key = k;
            Draw();
        }

        void Draw()
        {
            var room = Room;
            var players = room?.Players ?? new List<PlayerInfo>();
            root.Q<Label>("seats-head").text = (room != null ? RoomText.Commanders : RoomText.Connecting).ToUpperInvariant();
            var seats = root.Q("seats");
            seats.Clear();
            for (var slot = 0; slot < 2; slot++)
            {
                var p = players.FirstOrDefault(x => x.Slot == slot);
                var seat = Ui.El("room__seat", seats);
                seat.EnableInClassList("room__seat--empty", p == null);
                seat.EnableInClassList("room__seat--offline", p != null && !p.Connected);
                seat.style.borderLeftColor = Ui.Hex(p?.Color, new Color32(0x3a, 0x46, 0x52, 0xff));
                Ui.Text(p != null ? p.Name : RoomText.WaitingPlayer, "room__seat-name", seat);
                if (p != null) Ui.Text(!p.Connected ? RoomText.Offline : p.Away == true ? RoomText.Away : RoomText.Connected, "room__seat-state", seat);
            }
            DrawInvite(players);

            Options(root.Q("missions"), RoomText.Missions.Select(m => (m.Id, m.Name, m.About)), mission, id =>
            {
                mission = id;
                PlayerPrefs.SetString(MissionKey, id);
            });
            Options(root.Q("maps"), RoomText.Maps.Select(m => (m.Id, m.Name, m.About)), "city", _ => { });
            Options(root.Q("hours"), new[] { ("day", RoomText.Day, RoomText.DayAbout), ("night", RoomText.Night, RoomText.NightAbout) }, night ? "night" : "day", id =>
            {
                night = id == "night";
                PlayerPrefs.SetInt(NightKey, night ? 1 : 0);
            });

            var connected = players.Count(p => p.Connected);
            var start = root.Q<Button>("start");
            start.SetEnabled(room != null && connected > 0);
            root.Q<Label>("start-title").text = (connected < 2 ? RoomText.StartAlone : RoomText.StartTwo).ToUpperInvariant();
            root.Q<Label>("start-about").text = connected < 2 ? RoomText.StartAloneAbout : RoomText.StartTwoAbout;
            var leave = root.Q<Button>("leave");
            leave.text = warned ? Lore.QuitText.Accept : Lore.HostClosed.Home;
            Say(warned ? Lore.QuitHostWarning(Partner(players)) : "");
        }

        /// <summary>How the partner joins (lobby.ts invite): the host's addresses, the default route's first, the rest folded.</summary>
        void DrawInvite(List<PlayerInfo> players)
        {
            var invite = root.Q("invite");
            invite.Clear();
            if (!Hosting || players.Count(p => p.Connected) >= 2) return;
            var addresses = Addresses;
            if (addresses == null || addresses.Count == 0) return;
            Ui.Text(RoomText.Invite, "start__hint", invite);
            Address(invite, addresses[0]);
            if (addresses.Count > 1)
            {
                var more = new Foldout { text = RoomText.OtherAddresses(addresses.Count - 1), value = false };
                for (var i = 1; i < addresses.Count; i++) Address(more, addresses[i]);
                invite.Add(more);
            }
        }

        static void Address(VisualElement parent, HostAddress a)
        {
            Ui.Text(a.Kind == "tailscale" ? RoomText.Tailscale : RoomText.Lan, "room__address-kind", parent);
            var row = Ui.El("room__address", parent);
            Ui.Text(a.Address, "room__address-text", row);
            Button copy = null;
            copy = new Button(() =>
            {
                GUIUtility.systemCopyBuffer = a.Address;
                copy.text = RoomText.Copied;
                copy.schedule.Execute(() => copy.text = RoomText.Copy).StartingIn(1500);
            }) { text = RoomText.Copy };
            copy.AddToClassList("room__copy");
            row.Add(copy);
        }

        /// <summary>A row of choices, the chosen one marked; a click picks one and draws again.</summary>
        void Options(VisualElement host, IEnumerable<(string Id, string Name, string About)> items, string chosen, System.Action<string> pick)
        {
            host.Clear();
            foreach (var (id, name, about) in items)
            {
                var b = new Button(() =>
                {
                    pick(id);
                    key = null;
                });
                b.AddToClassList("room__option");
                b.EnableInClassList("room__option--on", id == chosen);
                Ui.Text(name, "room__option-name", b).pickingMode = PickingMode.Ignore;
                Ui.Text(about, "room__option-about", b).pickingMode = PickingMode.Ignore;
                host.Add(b);
            }
        }

        void StartMission()
        {
            if (previewRoom != null) return;
            link.Send(new StartMessage { Mission = mission, Map = "city", Night = night });
        }

        /// <summary>Back to the start screen; a host whose partner is in is told what happens first, and presses again.</summary>
        void Leave()
        {
            var partnerIn = Hosting && Room != null && Room.Players.Any(p => p.Slot != Slot && p.Connected);
            if (partnerIn && !warned)
            {
                warned = true;
                return;
            }
            warned = false;
            if (previewRoom != null) return;
            link.Leave();
        }

        string Partner(List<PlayerInfo> players) => players.FirstOrDefault(p => p.Slot != Slot)?.Name ?? "";

        void Say(string text)
        {
            var status = root.Q<Label>("status");
            status.text = text;
            status.EnableInClassList("start__status--bad", !string.IsNullOrEmpty(text));
        }
    }
}
