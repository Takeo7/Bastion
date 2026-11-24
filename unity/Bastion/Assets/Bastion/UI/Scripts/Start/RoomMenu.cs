using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Menu;
using Bastion.Client.Net;
using Bastion.Engine;
using Bastion.Net;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Start
{
    /// <summary>
    /// The room, between joining and the mission or the base (lobby.ts; docs/UNITY-UI.md), remade as a screen of the game
    /// (cuadro 124, Arturo 23/11) after the start screen it comes from: over Arte's crossroads, the main menu's band with the
    /// title and the room's keys: «Continuar partida» (the saved one's chapter and level, where it is, or the mission left
    /// half played), «Nueva partida» (its level, on the four cards, and what it replaces or that the partner isn't in),
    /// «Escaramuza» (its sheet: the mission, the map, day or night, start: the `start` message) and «Ir al inicio» (a host
    /// with the partner in is warned first). The two players big in the middle, each in its colour; under them how the
    /// partner joins, the addresses (the host's, the router's open, the room's key) folded behind «¿No le sale tu partida?».
    /// It reads NetLink.LastRoom and never listens to NetLink.Message: what the host sends waits there for the game, its
    /// snapshot first (AppFlow loads the battle when the room's phase is mission).
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
        /// <summary>The addresses unfolded («¿No le sale tu partida?»).</summary>
        bool addresses;
        /// <summary>main, new (a new game's level and question) or skirmish.</summary>
        string page = "main";
        LevelPicker levels;
        /// <summary>The band's entries and the sheet's keys (StartLook, cuadro 151).</summary>
        StartLook.Entries roomEntries, sheetEntries;
        /// <summary>Made-up room and addresses for a capture (Preview); null, NetLink's.</summary>
        RoomMessage previewRoom;
        IReadOnlyList<HostAddress> previewAddresses;
        bool previewHosting;
        RouterState? previewRouter;
        string previewInternet;
        int previewSlot;

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
            // As the start screen's (cuadro 151): the veil, not the dark band; the keys as entries.
            var fade = root.Q("fade");
            Ui.Show(fade, false);
            root.Insert(root.IndexOf(fade) + 1, StartLook.Veil());
            roomEntries = new StartLook.Entries(root);
            sheetEntries = new StartLook.Entries(root);
            // The crossroads of «Partida multijugador», where the room is hosted or joined from; again when the room comes back
            // after a mission or the base (the start scene loaded anew).
            if (MenuBackdrop.Current == null || MenuBackdrop.Current.Shown != MenuBackdrop.Scene.Crossroads) MenuBackdrop.Show(MenuBackdrop.Scene.Crossroads);
            link = NetLink.Instance;
            mission = PlayerPrefs.GetString(MissionKey, "elimination");
            if (!RoomText.Missions.Any(m => m.Id == mission)) mission = "elimination";
            night = PlayerPrefs.GetInt(NightKey, 0) == 1;
            warned = false;
            addresses = false;
            page = "main";
            key = null;
            root.Q<Label>("title").text = RoomText.Title.ToUpperInvariant();
            root.Q<Label>("mission-head").text = RoomText.Mission.ToUpperInvariant();
            root.Q<Label>("map-head").text = RoomText.Map.ToUpperInvariant();
            root.Q<Label>("hour-head").text = RoomText.Hour.ToUpperInvariant();
            sheetEntries.Make(root.Q<Button>("start"), "", Begin, false);
            sheetEntries.Make(root.Q<Button>("back"), RoomText.Back, () => Go("main"), false);
            levels = new LevelPicker(StartScreen.HostLevel);
            levels.Picked += id => StartScreen.HostLevel = id;
            root.Q("levels").Add(levels.Root);
            Draw();
        }

        /// <summary>
        /// A room to show without a connection (HudShots.Room): its players, the host's addresses, hosting or not, the page
        /// (main, new, skirmish; «direcciones», main with the addresses unfolded); the router's state and, open, its address on
        /// the internet; this app's seat.
        /// </summary>
        public void Preview(RoomMessage room, IReadOnlyList<HostAddress> addresses, bool hosting, string page = "main", RouterState? router = null, string internet = null, int slot = 0)
        {
            previewRoom = room;
            previewAddresses = addresses;
            previewHosting = hosting;
            previewRouter = router;
            previewInternet = internet;
            previewSlot = slot;
            this.addresses = page == "direcciones";
            this.page = page == "direcciones" ? "main" : page;
            key = null;
        }

        RoomMessage Room => previewRoom ?? link.LastRoom;
        IReadOnlyList<HostAddress> Addresses => previewAddresses ?? link.Addresses;
        bool Hosting => previewRoom != null ? previewHosting : link.Hosting;
        /// <summary>Opening the game's port on the router, hosting (Red's RouterPort; Arturo, 01/11): null, not asked (a test app).</summary>
        RouterState? Router => previewRoom != null ? previewRouter : link.Router;
        /// <summary>
        /// Where the partner joins from the internet, with the room's key (Arturo, 01/11: the protocol is public): «83.45.12.7:41391
        /// · KXRT5», shown and copied whole, to paste as it is in «Unirse por dirección».
        /// </summary>
        string Internet => previewRoom != null ? previewInternet : link.Invite ?? link.Internet;
        int Slot => previewRoom != null ? previewSlot : link.Slot;
        string RoomKey => previewRoom != null ? (previewRouter == RouterState.Open ? "KXRT5" : null) : link.Key;

        void Update()
        {
            if (root == null) return;
            var room = Room;
            // Drawn again only when who is in, their state, or the choices change.
            var k = room == null ? "" : string.Join("|", room.Players.Select(p => $"{p.Slot}:{p.Name}:{p.Connected}:{p.Away}:{p.Color}"));
            var saved = room?.SavedCampaign;
            k += $"#{Hosting}#{mission}#{night}#{warned}#{addresses}#{page}#{(saved != null ? RoomText.Saved(saved) + saved.GameLevel : "")}#{room?.SetAside}#{room?.Restored}#{Router}#{Internet}#{RoomKey}";
            // One chosen always; the arrows and Intro on the band's column.
            if (page == "main")
            {
                roomEntries.ChooseFirst();
                // (Not under a loading sheet: the review, 2.)
                if (!App.AppFlow.Veiled) roomEntries.Keys();
            }
            else sheetEntries.ChooseFirst();
            if (k == key) return;
            key = k;
            Draw();
        }

        void Draw()
        {
            var room = Room;
            var players = room?.Players ?? new List<PlayerInfo>();
            var main = page == "main";
            Ui.Show(root.Q("menu"), main);
            Ui.Show(root.Q("players"), main);
            Ui.Show(root.Q("center"), !main);
            DrawSeats(room, players);
            DrawInvite(players);
            DrawMenu(room, players);
            DrawSheet(room, players);
            Say(warned ? Lore.QuitHostWarning(Partner(players)) : "");
        }

        /// <summary>
        /// The two players (cuadro 124: big), as «Partida multijugador»'s cards: the host's flag and the partner's two soldiers
        /// in each one's colour, the name in the band with whether they are in; this app's marked «TÚ»; the seat still empty,
        /// faint, waiting.
        /// </summary>
        void DrawSeats(RoomMessage room, List<PlayerInfo> players)
        {
            var seats = root.Q("seats");
            seats.Clear();
            for (var slot = 0; slot < 2; slot++)
            {
                var p = players.FirstOrDefault(x => x.Slot == slot);
                Color grey = new Color32(0xc4, 0xc6, 0xc2, 0xff);
                var colour = p != null ? Ui.Hex(p.Color, grey) : grey;
                var seat = Ui.El("room__seat", seats);
                seat.EnableInClassList("room__seat--empty", p == null);
                seat.EnableInClassList("room__seat--offline", p != null && !p.Connected);
                if (p != null) SetBorder(seat, colour);
                var face = Ui.El("room__seat-face", seat);
                var icon = Ui.El("room__seat-icon", face);
                var art = Resources.Load<Texture2D>(slot == 0 ? "Start/icono-alojar" : "Start/icono-unirse");
                if (art != null) icon.style.backgroundImage = art;
                icon.style.unityBackgroundImageTintColor = p != null ? colour : new Color(0.77f, 0.78f, 0.76f, 0.7f);
                var band = Ui.El("room__seat-band", seat);
                if (p != null) band.style.borderTopColor = colour;
                Ui.Text((p != null ? p.Name : room != null ? RoomText.WaitingPlayer : RoomText.Connecting).ToUpperInvariant(), "room__seat-name font-title", band);
                if (p != null) Ui.Text(!p.Connected ? RoomText.Offline : p.Away == true ? RoomText.Away : RoomText.Connected, "room__seat-state", band);
                if (p != null && p.Slot == Slot) Ui.Text(RoomText.You.ToUpperInvariant(), "room__you", seat);
            }
        }

        static void SetBorder(VisualElement e, Color c)
        {
            e.style.borderTopColor = c;
            e.style.borderRightColor = c;
            e.style.borderBottomColor = c;
            e.style.borderLeftColor = c;
        }

        /// <summary>
        /// The room's keys on the band (lobby.ts renderMain): «Continuar partida» with the saved game's chapter and level and
        /// where it is (the level in view, cuadro 124); «Nueva partida»; «Escaramuza» (not with a mission half played: why,
        /// under it); the notes of a campaign kept aside or a save that wouldn't start; «Ir al inicio».
        /// </summary>
        void DrawMenu(RoomMessage room, List<PlayerInfo> players)
        {
            var menu = root.Q("menu");
            menu.Clear();
            roomEntries.Clear();
            var saved = room?.SavedCampaign;
            var ok = room != null && players.Any(p => p.Connected);
            if (saved != null)
            {
                Key(menu, RoomText.Continue, ok, () =>
                {
                    // (What the loading sheet says: what was pressed, R22.)
                    App.AppFlow.Ask(App.AppFlow.Asked.GoOn);
                    Send(new ContinueCampaignMessage());
                });
                if (saved.Chapter > 0) Ui.Text(StartText.SavedLine(saved.Chapter, Lore.GameLevelText.NameOf(saved.GameLevel)), "room__note room__note--level", menu);
                Ui.Text(RoomText.SavedWhere(saved), "room__note", menu);
            }
            Key(menu, RoomText.NewCampaign, ok, () => Go("new"));
            var blocked = saved?.InMission != null;
            Key(menu, RoomText.Skirmish, ok && !blocked, () => Go("skirmish"));
            if (blocked) Ui.Text(RoomText.SkirmishBlocked, "room__note", menu);
            if (!string.IsNullOrEmpty(room?.SetAside)) Ui.Text(RoomText.SetAside(room.SetAside), "start__old", menu);
            // The save wouldn't start: which copy the game goes on from (the host's words, Red 0.12.17).
            if (!string.IsNullOrEmpty(room?.Restored)) Ui.Text(room.Restored, "start__old", menu);
            Key(menu, warned ? Lore.QuitText.Accept : Lore.HostClosed.Home, true, Leave);
        }

        void Key(VisualElement menu, string text, bool enabled, System.Action click)
        {
            var b = new Button();
            menu.Add(b);
            roomEntries.Make(b, text, click);
            b.SetEnabled(enabled);
        }

        /// <summary>
        /// The plate in the middle. A new game (cuadro 124: its level in view): the four levels (LevelPicker, the one last picked
        /// marked), the saved game it replaces, that the partner isn't in yet; «Empezar» or «Empezar de cero». The skirmish's
        /// sheet (lobby.ts renderSkirmish): the mission, the map, day or night; «Empezar en solitario» or «Empezar escaramuza».
        /// </summary>
        void DrawSheet(RoomMessage room, List<PlayerInfo> players)
        {
            var saved = room?.SavedCampaign;
            var alone = players.Count(p => p.Connected) < 2;
            var connected = players.Count(p => p.Connected);
            var isNew = page == "new";
            var skirmish = page == "skirmish";
            root.Q("sheet").EnableInClassList("start__form--levels", isNew);
            root.Q("sheet").EnableInClassList("room__sheet--levels", isNew);
            Ui.Show(root.Q("levels"), isNew);
            Ui.Show(root.Q("skirmish"), skirmish);
            if (isNew) levels.Set(StartScreen.HostLevel);
            var ask = root.Q("ask");
            ask.Clear();
            if (isNew && saved != null) Ui.Text(RoomText.Replaces(saved), "room__ask-text room__ask-text--warn", ask);
            if (isNew && alone) Ui.Text(RoomText.AloneText, "room__ask-text", ask);
            if (skirmish) Ui.Text(connected < 2 ? RoomText.StartAloneAbout : RoomText.StartTwoAbout, "room__ask-text", ask);
            var start = root.Q<Button>("start");
            StartLook.Entries.SetText(start, isNew ? (saved != null ? RoomText.StartAgain : RoomText.StartNew) : connected < 2 ? RoomText.StartAlone : RoomText.StartTwo);
            start.SetEnabled(room != null && connected > 0);
            if (!skirmish) return;
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
        }

        void Go(string to)
        {
            page = to;
            warned = false;
            key = null;
        }

        void Send(ClientMessage msg)
        {
            if (previewRoom == null) link.Send(msg);
        }

        /// <summary>The plate's go key: the new game at the level picked, or the skirmish.</summary>
        void Begin()
        {
            if (page == "new")
            {
                Go("main");
                App.AppFlow.Ask(App.AppFlow.Asked.New);
                Send(new NewCampaignMessage { Level = levels.Level });
                return;
            }
            if (previewRoom != null) return;
            // The skirmish's sheet with its words from the start: its kind and whether by night; its map with its state.
            var words = new App.LoadView { Paper = true, Kicker = RoomText.Skirmish, Title = RoomText.Missions.FirstOrDefault(x => x.Id == mission).Name ?? mission };
            if (night) words.Notes.Add((Lore.NightText.Label.ToLowerInvariant(), false));
            App.AppFlow.Ask(App.AppFlow.Asked.Skirmish, words);
            link.Send(new StartMessage { Mission = mission, Map = "city", Night = night });
        }

        /// <summary>
        /// How the partner joins, hosting while they aren't in (lobby.ts invite; cuadro 124: the address hidden): the line of
        /// where they will see this game, and «¿No le sale tu partida?», which unfolds the addresses: the host's, the default
        /// route's first; the router's (Arturo, 01/11: over the internet without Tailscale), its address once the port is open,
        /// a line while it opens or why it won't; the room's key; the rest folded. On iOS nothing is announced (NetLink.LanBlind):
        /// the addresses at once, the only way in.
        /// </summary>
        void DrawInvite(List<PlayerInfo> players)
        {
            var invite = root.Q("invite");
            invite.Clear();
            if (!Hosting || players.Count(p => p.Connected) >= 2) return;
            var list = Addresses ?? new List<HostAddress>();
            var router = Router;
            var internet = router == RouterState.Open ? Internet : null;
            if (list.Count == 0 && internet == null) return;
            var blind = NetLink.LanBlind;
            var plate = Ui.El("room__invite-plate", invite);
            if (blind) Ui.Text(RoomText.InviteBlind, "room__invite-line", plate);
            else
            {
                Ui.Text(RoomText.InviteSeen(players.FirstOrDefault(p => p.Slot == Slot)?.Name), "room__invite-line", plate);
                var more = new Button(() =>
                {
                    addresses = !addresses;
                    key = null;
                }) { text = (addresses ? "▾ " : "▸ ") + RoomText.NotSeen };
                more.AddToClassList("room__more");
                // (Out of UI Toolkit's own navigation, as the entries: the review.)
                more.focusable = false;
                plate.Add(more);
                if (!addresses) return;
            }
            var box = Ui.El("room__addresses", plate);
            if (!blind) Ui.Text(RoomText.InviteAddress, "room__invite-line", box);
            if (list.Count > 0) Address(box, list[0]);
            if (internet != null) Address(box, new HostAddress { Address = internet, Kind = "internet" });
            else if (router != null && router != RouterState.Open) Ui.Text(RoomText.RouterLine(router.Value), router == RouterState.Trying ? "room__router" : "room__router room__router--off", box);
            // The room's key on its own line, with the internet's address or without it: asked of whoever comes from the
            // internet (an IPv6 address too, the port closed or not), and from the house while the router's port is open, where
            // what is typed is the house's address and the key (Red, 0.12.17 and 0.12.18; Gameplay's review, 10/11, #9; Lore).
            if (RoomKey is string k) Ui.Text(RoomText.KeyLine(k), "room__router", box);
            if (list.Count > 1)
            {
                var others = new Foldout { text = RoomText.OtherAddresses(list.Count - 1), value = false };
                for (var i = 1; i < list.Count; i++) Address(others, list[i]);
                box.Add(others);
            }
        }

        static void Address(VisualElement parent, HostAddress a)
        {
            Ui.Text(a.Kind == "tailscale" ? RoomText.Tailscale : a.Kind == "internet" ? RoomText.Internet : RoomText.Lan, "room__address-kind", parent);
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
            copy.focusable = false;
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

        /// <summary>Back to the start screen; a host whose partner is in is told what happens first, and presses again.</summary>
        void Leave()
        {
            var partnerIn = Hosting && Room != null && Room.Players.Any(p => p.Slot != Slot && p.Connected);
            if (partnerIn && !warned)
            {
                warned = true;
                key = null;
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
            status.EnableInClassList("start__status--on", !string.IsNullOrEmpty(text));
            status.EnableInClassList("start__status--bad", !string.IsNullOrEmpty(text));
        }
    }
}
