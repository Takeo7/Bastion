using System;
using System.Collections.Generic;
using Bastion.Client.Net;
using Bastion.Net;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Start
{
    /// <summary>
    /// The start screen (the Electron app's launcher.html; docs/UNITY-UI.md): the name, be the host, the games on the
    /// network, join by address, quit. Red's NetLink does the connecting: Host and Join say at once what is wrong, else
    /// Joined or Failed come later. While a try is under way the buttons rest (attempt in launcher.html). Joined, it
    /// raises Entered and whoever runs the app's scenes takes the player in. It never listens to NetLink.Message: what the
    /// host sends meanwhile waits there for the game.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class StartScreen : MonoBehaviour
    {
        /// <summary>How long «Buscando partidas en tu red…» shows before «Ninguna por ahora» (launcher.html: 4 s).</summary>
        const long SearchMs = 4000;

        /// <summary>Why the player is back here (the host closed the game, the seat was opened elsewhere…), said once.</summary>
        public static string Note;

        /// <summary>In: this player's seat. The app's flow loads the game.</summary>
        public event Action<int> Entered;

        VisualElement root;
        TextField nameField;
        TextField addressField;
        Label status;
        VisualElement games;
        Label lanHint;
        readonly List<Button> buttons = new List<Button>();
        bool busy;
        bool searching = true;
        NetLink link;

        void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            var tree = Resources.Load<VisualTreeAsset>("Start/Start");
            if (tree == null)
            {
                Debug.LogError("Start: Resources/Start/Start.uxml missing");
                return;
            }
            doc.rootVisualElement.Clear();
            tree.CloneTree(doc.rootVisualElement);
            root = doc.rootVisualElement.Q("start");
            // mixta.css .menu-column: a big plate in the steel frame with rivets, the main key slanted (as the 0.10.1).
            PlateFrame.Dress(root.Q(className: "start__column"));
            root.Query<Button>(className: "btn-primary").ForEach(Slant.Dress);
            link = NetLink.Instance;
            Fill();
            link.Joined += OnJoined;
            link.Failed += OnFailed;
            link.LanGamesChanged += ShowGames;
            link.StartLooking();
            ShowGames(link.LanGames);
            root.schedule.Execute(() =>
            {
                searching = false;
                ShowGames(link.LanGames);
            }).StartingIn(SearchMs);
            if (!string.IsNullOrEmpty(Note))
            {
                Say(Note, true);
                Note = null;
            }
        }

        void OnDisable()
        {
            if (link == null) return;
            link.Joined -= OnJoined;
            link.Failed -= OnFailed;
            link.LanGamesChanged -= ShowGames;
            link.StopLooking();
        }

        void Fill()
        {
            root.Q<Label>("title").text = StartText.Title;
            root.Q<Label>("kicker").text = StartText.Kicker;
            root.Q<Label>("name-label").text = StartText.NameLabel;
            nameField = root.Q<TextField>("name");
            nameField.value = link.LastName ?? "";
            root.Q<Label>("host-title").text = StartText.Host;
            root.Q<Label>("host-about").text = StartText.HostAbout;
            root.Q<Label>("lan-label").text = StartText.LanLabel;
            lanHint = root.Q<Label>("lan-hint");
            games = root.Q("games");
            root.Q<Label>("join-label").text = StartText.JoinLabel;
            root.Q<Label>("join-hint").text = StartText.JoinHint;
            addressField = root.Q<TextField>("address");
            addressField.value = link.LastAddress ?? "";
            addressField.textEdition.placeholder = StartText.JoinPlaceholder;
            root.Q<Label>("quit-title").text = StartText.Quit;
            root.Q<Label>("version").text = StartText.Version(link.Version);
            status = root.Q<Label>("status");

            var host = root.Q<Button>("host");
            host.clicked += () => Attempt(name => link.Host(name), StartText.Opening);
            var join = root.Q<Button>("join");
            join.text = StartText.Join;
            join.clicked += JoinTyped;
            // Intro in the address joins, as the web form's submit.
            addressField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) JoinTyped();
            });
            buttons.Add(host);
            buttons.Add(join);
            root.Q<Button>("quit").clicked += Quit;
        }

        void JoinTyped()
        {
            var address = addressField.value;
            Attempt(name => link.Join(address, name), StartText.Looking);
        }

        /// <summary>One try at a time: its words while it runs, the buttons resting; what went wrong, and back.</summary>
        void Attempt(Func<string, string> run, string waiting)
        {
            if (busy) return;
            var name = nameField.value.Trim();
            if (name.Length == 0)
            {
                Say(StartText.NameMissing, true);
                nameField.Focus();
                return;
            }
            SetBusy(true);
            Say(waiting, false);
            var problem = run(name);
            if (problem == null) return;
            Say(problem, true);
            SetBusy(false);
        }

        void OnJoined(int slot)
        {
            Say("", false);
            Entered?.Invoke(slot);
        }

        /// <summary>It didn't get in (NetLink's words): said, and the buttons back. A taken name asks for another one.</summary>
        void OnFailed(string text)
        {
            Say(text, true);
            SetBusy(false);
        }

        void SetBusy(bool on)
        {
            busy = on;
            foreach (var b in buttons) b.SetEnabled(!on);
            games.Query<Button>().ForEach(b => b.SetEnabled(!on && !b.ClassListContains("start__game--other")));
        }

        void Say(string text, bool bad)
        {
            status.text = text ?? "";
            status.EnableInClassList("start__status--bad", bad);
        }

        /// <summary>What the hosts on the network announce: one button each; another version shown, not joinable.</summary>
        void ShowGames(IReadOnlyList<LanGame> list)
        {
            games.Clear();
            lanHint.text = list.Count > 0 ? "" : searching ? StartText.LanSearching : StartText.LanNone;
            Ui.Show(lanHint, list.Count == 0);
            foreach (var g in list)
            {
                var other = g.Version != link.Version;
                var address = g.Address;
                var who = g.Name;
                var b = new Button(() => Attempt(name => link.Join(address, name), StartText.Entering(who)));
                b.AddToClassList("start__game");
                b.EnableInClassList("start__game--other", other);
                Ui.Text(StartText.GameOf(g.Name), "start__game-name", b).pickingMode = PickingMode.Ignore;
                var about = $"{StartText.Players(g.Players.Count)} · {StartText.Phase(g.Phase)} · {g.Address}";
                if (other) about += " · " + StartText.OtherVersion(g.Version, link.Version);
                Ui.Text(about, "start__game-about", b).pickingMode = PickingMode.Ignore;
                b.SetEnabled(!busy && !other);
                games.Add(b);
            }
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
