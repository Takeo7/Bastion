using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Menu;
using Bastion.Client.Net;
using Bastion.Net;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Start
{
    /// <summary>
    /// The start screen (Arturo's notes of 14/11, menu-inicial; 0.12.24), over Arte's scene. The main menu: BASTIÓN at the
    /// top left and under it Continuar partida (with a game saved), Nueva partida, Partida multijugador, Ajustes and Salir
    /// de Bastión, on a dark band fading into the scene. «Partida multijugador»: two cards, Alojar partida and, on the
    /// right, «Unirse a la partida de Ana» when a game is heard on the network, else «Unirse por dirección», which asks
    /// for the address and its key. The name is asked before a new game (and before anything while there is none); a new
    /// game over a saved one is asked first.
    ///
    /// Red's NetLink does the rest (Continue, NewGame, Host, Join): each says at once what is wrong, else Joined or
    /// Failed come later; meanwhile the keys rest and a line at the foot says what goes on, and what went wrong.
    /// «Continuar» and «Nueva partida» go straight to the base (NetLink.Opening; AppFlow keeps this screen up until
    /// then). It never listens to NetLink.Message: what the host sends meanwhile waits there for the game.
    ///
    /// Behind it, Arte's MenuBackdrop: the road at dusk under the main menu, the crossroads from above under «Partida
    /// multijugador» and what it asks.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class StartScreen : MonoBehaviour
    {
        /// <summary>Why the player is back here (the host closed the game, the seat was opened elsewhere…), said once.</summary>
        public static string Note;

        /// <summary>In: this player's seat. The app's flow loads the game.</summary>
        public event Action<int> Entered;

        /// <summary>
        /// Which of Arte's views is behind (PLAN of 14/11: La Lata on a snowy road at dusk for the main menu, a snowy
        /// crossroads seen from above for «Partida multijugador»): «menu» or «multijugador»; SceneChanged when it changes.
        /// </summary>
        public static string Scene { get; private set; } = "menu";
        public static event Action<string> SceneChanged;

        /// <summary>The backdrop this screen put up last (MenuBackdrop.Show); null, none yet since it came on.</summary>
        string backdrop;

        /// <summary>The main band's dark: solid to these shares of the width at the top and the foot, then clear over Band more.</summary>
        const float SolidTop = 0.3f, SolidFoot = 0.2f, Band = 0.2f;

        enum Page { Menu, Name, Two, Address }

        VisualElement root;
        VisualElement fade, side, menu, cards, form, nameBox, addressBox, keyBox;
        TextField nameField, addressField, keyField;
        Button continueKey, goKey, backKey;
        /// <summary>
        /// The main menu's five entries (cuadro 149, Arturo's picture): words with no plates, the chosen one on an amber brush
        /// stroke, dark; one chosen always, by the pointer over it, the finger on it or the arrows; Intro presses it.
        /// </summary>
        StartLook.Entries menuEntries, twoEntries, formEntries;
        /// <summary>«Partida multijugador»'s column (cuadro 151: as the main menu's): Alojar, Unirse, Volver.</summary>
        VisualElement twoMenu;
        Button joinEntry;
        Label title;
        Label status, joinLabel;
        GameMenu settings;
        readonly List<Button> buttons = new List<Button>();
        Page page;
        /// <summary>The name asked from the main menu or from «Partida multijugador», and what it does once given.</summary>
        Page nameFrom;
        Func<string, string> afterName;
        string afterNameWaiting;
        /// <summary>
        /// The game's level (N9; cuadro 125, Arturo 23/11: the four as cards in the middle, Medio picked to start with), asked
        /// with the name for a new game; a hosted one's, in the room (RoomMenu, HostLevel).
        /// </summary>
        string level = Bastion.Engine.LevelIds.Medium;
        bool askingLevel;
        VisualElement levelsBox;
        LevelPicker levelPicker;
        /// <summary>The level last picked in the room's «Nueva partida» (cuadro 124), marked there the next time.</summary>
        public static string HostLevel = Bastion.Engine.LevelIds.Medium;
        bool busy;
        NetLink link;
        /// <summary>The game the right card joins: the first of this version heard on the network; null, none (by address).</summary>
        LanGame heard;
        IReadOnlyList<LanGame> heardList = new LanGame[0];

        // The labs' (Preview): a game saved or not, one heard on the network. Asked before play, kept serialized for then
        // (entering play reloads the scene: a plain field came back empty).
        [SerializeField, HideInInspector] string previewAsked;
        bool previewing;
        string previewName;
        /// <summary>The frame a form opened: its fields heed no Intro in it nor the next two (Go).</summary>
        int formOpened = -10;

        /// <summary>Labs (TeclasLab): what would be opened, hosted or joined only said in the log, nothing done.</summary>
        public static bool DryRun;

        /// <summary>The name last played with (a preview's own).</summary>
        string KnownName => previewing && previewName != null ? previewName : link.LastName;
        SavedCampaign previewSaved;
        LanGame previewHeard;

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
            buttons.Clear();
            backdrop = null;
            busy = false;
            link = NetLink.Instance;
            var tips = new TooltipLayer(root);
            settings = new GameMenu(root, tips, Bastion.Client.Audio.UiSounds.Play);
            settings.Changed += (id, value) => App.GameSettings.Change(id, value);
            Fill();
            link.Joined += OnJoined;
            link.Failed += OnFailed;
            link.LanGamesChanged += Heard;
            // On iOS nothing is heard on the network (NetLink.LanBlind): the right card is always «Unirse por dirección».
            if (!NetLink.LanBlind) link.StartLooking();
            Heard(link.LanGames);
            Go(Page.Menu);
            ShowSaved();
            // «Continuar partida» as soon as there is a game saved (NetLink.Saved reads the save again when it changes).
            root.schedule.Execute(ShowSaved).Every(1000);
            if (!string.IsNullOrEmpty(Note))
            {
                Say(Note, true);
                Note = null;
            }
            if (!string.IsNullOrEmpty(previewAsked))
            {
                var asked = previewAsked.Split('|');
                previewAsked = null;
                Preview(asked[1] == "1", asked[2], asked[0], asked.Length > 3 ? asked[3] : null);
            }
        }

        void OnDisable()
        {
            if (link == null) return;
            link.Joined -= OnJoined;
            link.Failed -= OnFailed;
            link.LanGamesChanged -= Heard;
            link.StopLooking();
        }

        /// <summary>
        /// For the labs' captures (HudShots.Entry, BASTION_INICIO): `saved`, a game saved (8 outings, level 3); `heardOf`, a
        /// game on the network hosted by that name; then the page: menu, nombre, nueva, multijugador, direccion or ajustes.
        /// </summary>
        public void Preview(bool saved, string heardOf, string open, string name = null)
        {
            // (A name played with before, or none: «nombre=Ana».)
            previewName = name ?? "";
            if (root == null)
            {
                previewAsked = $"{open}|{(saved ? 1 : 0)}|{heardOf}|{name}";
                return;
            }
            Debug.Log($"[inicio] vista previa: {open}, guardada {saved}, en la red {heardOf ?? "nadie"}");
            previewing = true;
            nameField.value = previewName;
            previewSaved = saved ? new SavedCampaign { Chapter = 1, Row = 3, Soldiers = 4, Supplies = 245, NodesPlayed = 7, GameLevel = Bastion.Engine.LevelIds.Medium } : null;
            previewHeard = string.IsNullOrEmpty(heardOf) ? null : new LanGame { Name = heardOf, Address = "192.168.1.20:41391", Version = link.Version };
            Heard(heardList);
            ShowSaved();
            switch (open)
            {
                case "nombre": AskName(Page.Menu, name => null, StartText.Opening, StartText.Start); break;
                case "nueva": NewGame(); break;
                // «nueva-supervivencia»: the form with Supervivencia picked (the proposal's photo).
                case "nueva-supervivencia": NewGame(); PickLevel(Bastion.Engine.LevelIds.Survival); break;
                case "multijugador": Go(Page.Two); break;
                case "direccion":
                    Go(Page.Two);
                    Go(Page.Address);
                    break;
                case "ajustes": OpenSettings(); break;
            }
        }

        SavedCampaign Saved => previewing ? previewSaved : link.Saved;

        void Fill()
        {
            fade = root.Q("fade");
            fade.generateVisualContent += Fade;
            side = root.Q("side");
            menu = root.Q("menu");
            cards = root.Q("cards");
            form = root.Q("form");
            nameBox = root.Q("name-box");
            addressBox = root.Q("address-box");
            keyBox = root.Q("key-box");
            // The title in the middle at the top (cuadro 149), out of the keys' column; Arte's worn picture of it when it is there.
            title = root.Q<Label>("title");
            title.text = StartText.Title;
            var start = root.Q("start");
            start.Insert(start.IndexOf(fade) + 1, title);
            if (Resources.Load<Texture2D>(TitleArt) is Texture2D titleArt)
            {
                title.text = "";
                title.style.backgroundImage = titleArt;
                title.AddToClassList("start__title--art");
                root.RegisterCallback<GeometryChangedEvent>(_ => PlaceTitle(title, titleArt, root.layout.width, root.layout.height));
            }
            // Arte's grain over the scene, under the words, and its darker edges (cuadro 149), when they are there.
            // The dark veil with Arte's grain over the top left, its edge on a slant (cuadro 150, Arturo's picture), under the words.
            start.Insert(start.IndexOf(fade) + 1, StartLook.Veil());
            menuEntries = new StartLook.Entries(root);
            twoEntries = new StartLook.Entries(root);
            formEntries = new StartLook.Entries(root);
            // «Capítulo 1 · Medio» past «Continuar partida»'s stroke, on its line (the entries keep the picture's steps).
            menuEntries.Sized += (entry, left, width) =>
            {
                if (entry != continueKey || savedNote == null) return;
                var words = entry.Q<Label>(className: "start__entry-text").layout;
                savedNote.style.left = left + width + 6;
                savedNote.style.top = Mathf.Round(words.y + words.height / 2 - 9);
            };
            if (Resources.Load<Texture2D>("Start/bordes") is Texture2D edges)
            {
                var e = new VisualElement { pickingMode = PickingMode.Ignore };
                e.AddToClassList("start__edges");
                e.style.backgroundImage = edges;
                start.Insert(start.IndexOf(fade) + 1, e);
            }
            continueKey = Entry("continue", StartText.Continue, Continue);
            // Where it would be, the campaign from before the expeditions, set aside (Red, 16/11).
            oldNote = new Label();
            Ui.AddClasses(oldNote, "start__old");
            continueKey.parent.Insert(continueKey.parent.IndexOf(continueKey) + 1, oldNote);
            // Under «Continuar partida», the saved game's chapter and level (cuadro 125: seen there only).
            // (On its line, past the brush stroke (menuEntries.Sized): the entries keep the picture's steps, CORRECCIONES-2.)
            savedNote = new Label { pickingMode = PickingMode.Ignore };
            Ui.AddClasses(savedNote, "start__saved");
            continueKey.Add(savedNote);
            Entry("new", StartText.New, NewGame);
            Entry("two", StartText.Two, () => Go(Page.Two));
            Entry("settings", StartText.Settings, OpenSettings);
            // Not with the finger: iOS apps aren't closed from inside them (Apple; Application.Quit there looks like a crash. Red, 7/11;
            // Gameplay, 8/11: wherever the game is played with the finger, the lab's too).
            Ui.Show(Entry("quit", StartText.Quit, Quit), !Bastion.Client.GameInput.Touch);
            backKey = root.Q<Button>("back");
            Ui.Show(backKey, false);
            // «Partida multijugador» as the main menu (cuadro 151): a column of entries where its cards were.
            twoMenu = Ui.El("start__menu", side);
            twoMenu.pickingMode = PickingMode.Ignore;
            Button NewEntry() { var b = new Button(); twoMenu.Add(b); return b; }
            buttons.Add(twoEntries.Make(NewEntry(), StartText.Host, Host));
            joinEntry = twoEntries.Make(NewEntry(), StartText.ByAddress, JoinCard);
            buttons.Add(joinEntry);
            buttons.Add(twoEntries.Make(NewEntry(), StartText.Back, () => Go(Page.Menu)));

            var host = root.Q<Button>("host");
            host.clicked += Host;
            buttons.Add(host);
            root.Q<Label>("host-label").text = StartText.Host.ToUpperInvariant();
            var join = root.Q<Button>("join");
            join.clicked += JoinCard;
            buttons.Add(join);
            joinLabel = root.Q<Label>("join-label");
            Icon(root.Q("host-icon"), "Start/icono-alojar");
            Icon(root.Q("join-icon"), "Start/icono-unirse");

            root.Q<Label>("name-label").text = StartText.NameLabel;
            nameField = root.Q<TextField>("name");
            nameField.value = link.LastName ?? "";
            root.Q<Label>("address-label").text = StartText.AddressLabel;
            addressField = root.Q<TextField>("address");
            addressField.value = link.LastAddress ?? "";
            addressField.textEdition.placeholder = StartText.JoinPlaceholder;
            root.Q<Label>("key-label").text = StartText.KeyLabel;
            keyField = root.Q<TextField>("key");
            keyField.textEdition.placeholder = StartText.KeyPlaceholder;
            Levels();
            // (The form's keys as entries too, their strokes round their words; the fields as they were, cuadro 151.)
            goKey = formEntries.Make(root.Q<Button>("go"), StartText.Start, FormGo, false);
            buttons.Add(goKey);
            buttons.Add(formEntries.Make(root.Q<Button>("form-back"), StartText.Back, FormBack, false));
            // Intro in a field, as its key.
            foreach (var field in new[] { nameField, addressField, keyField })
                field.RegisterCallback<KeyDownEvent>(e =>
                {
                    // (Not the Intro that opened the form, come to the field as it took the focus: the review.)
                    if (Time.frameCount <= formOpened + 2) return;
                    if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) FormGo();
                });

            status = root.Q<Label>("status");
            root.Q<Label>("version").text = StartText.Version(link.Version);
        }

        /// <summary>The four levels under the name (N9, cuadro 125): the shared LevelPicker, after the name in the form.</summary>
        void Levels()
        {
            levelPicker = new LevelPicker(level);
            levelPicker.Picked += id => level = id;
            levelsBox = levelPicker.Root;
            buttons.AddRange(levelPicker.Keys);
            nameBox.parent.Insert(nameBox.parent.IndexOf(nameBox) + 1, levelsBox);
        }

        void PickLevel(string id)
        {
            level = id;
            levelPicker?.Set(id);
        }

        Button Key(string name, string text, Action click)
        {
            var b = root.Q<Button>(name);
            b.text = text.ToUpperInvariant();
            b.clicked += click;
            buttons.Add(b);
            return b;
        }

        /// <summary>Arte's worn «BASTIÓN» (cuadro 149); the boot's sheet takes it too (LoadWindow.StartTitle).</summary>
        public const string TitleArt = "Start/titulo";

        /// <summary>
        /// Arte's title placed as Arturo's picture's (CORRECCIONES-2): 29 % of the screen's width, as high as its own shape
        /// makes it, its letters (from TitleAccent of the picture down: the accent stands over them) from 5.9 % of the height.
        /// </summary>
        public static void PlaceTitle(VisualElement title, Texture2D art, float w, float h)
        {
            if (art == null || float.IsNaN(w) || w <= 0 || h <= 0) return;
            var width = Mathf.Round(w * TitleWide);
            var high = Mathf.Round(width * art.height / art.width);
            title.style.left = Mathf.Round((w - width) / 2);
            title.style.right = StyleKeyword.Auto;
            title.style.width = width;
            title.style.top = Mathf.Round(h * TitleLetters - TitleAccent * high);
            title.style.height = high;
        }

        const float TitleWide = 0.29f, TitleLetters = 0.059f, TitleAccent = 0.242f;

        /// <summary>A main menu's entry (cuadro 149; StartLook): the button of the layout by its name.</summary>
        Button Entry(string name, string text, Action click)
        {
            var b = menuEntries.Make(root.Q<Button>(name), text, click);
            buttons.Add(b);
            return b;
        }

        /// <summary>A card's red icon (Arte's: the flag, the two soldiers), when it is there.</summary>
        static void Icon(VisualElement e, string path)
        {
            var icon = Resources.Load<Texture2D>(path);
            if (icon != null) e.style.backgroundImage = icon;
        }

        void Update()
        {
            if (root == null) return;
            // Ajustes over the title and the keys hid part of «BASTIÓN» (and, on the Duo, of «PARTIDA MULTIJUGADOR»; Producto's
            // review, 23/11): they step out of sight while it is open, the band stays.
            var under = settings.Open ? Visibility.Hidden : Visibility.Visible;
            if (side.resolvedStyle.visibility != under) side.style.visibility = under;
            if (title.resolvedStyle.visibility != under) title.style.visibility = under;
            // One chosen always (the first to start with); the arrows and Intro, on a page of entries.
            var entries = page == Page.Menu ? menuEntries : page == Page.Two ? twoEntries : formEntries;
            entries.ChooseFirst();
            // (Not under Ajustes nor its question, «¿Empezar de cero?», nor under a loading sheet: the review, 1 and 2.)
            if (!busy && !settings.Open && !settings.Asking && !App.AppFlow.Veiled && (page == Page.Menu || page == Page.Two)) entries.Keys();
            // Esc: Ajustes or its question first, then a step back (the form, Partida multijugador).
            if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
            if (settings.Back() || busy) return;
            if (page == Page.Address) Go(Page.Two);
            else if (page == Page.Name) Go(nameFrom);
            else if (page == Page.Two) Go(Page.Menu);
        }

        /// <summary>
        /// A page up: the main menu (the band, the title and the keys), «Partida multijugador» (the cards and Volver), or
        /// what is asked (the form, under the title from the menu, in the middle from the cards).
        /// </summary>
        void Go(Page to)
        {
            if (DryRun) Debug.Log($"[inicio] página {to}");
            if ((to == Page.Name || to == Page.Address) && page != to) formOpened = Time.frameCount;
            page = to;
            var two = to == Page.Two || to == Page.Address || (to == Page.Name && nameFrom == Page.Two);
            // The veil, never the dark band (cuadro 151); the title on every page; «Partida multijugador» a column as the menu.
            Ui.Show(fade, false);
            Ui.Show(side, true);
            Ui.Show(title, true);
            Ui.Show(menu, to == Page.Menu);
            Ui.Show(twoMenu, to == Page.Two);
            Ui.Show(cards, false);
            Ui.Show(backKey, false);
            var asking = to == Page.Name || to == Page.Address;
            Ui.Show(form, asking);
            Ui.Show(nameBox, to == Page.Name || (to == Page.Address && NoName));
            Ui.Show(levelsBox, to == Page.Name && askingLevel);
            form.EnableInClassList("start__form--levels", to == Page.Name && askingLevel);
            form.EnableInClassList("start__form--cards", to == Page.Name && askingLevel);
            Ui.Show(addressBox, to == Page.Address);
            Ui.Show(keyBox, to == Page.Address);
            if (asking) (two || (to == Page.Name && askingLevel) ? root.Q("center") : side).Add(form);
            if (to == Page.Address) StartLook.Entries.SetText(goKey, StartText.Join);
            Say("", false);
            ShowScene(two ? "multijugador" : "menu");
            if (to == Page.Name) nameField.Focus();
            else if (to == Page.Address) (NoName ? nameField : addressField).Focus();
        }

        void ShowScene(string scene)
        {
            if (scene != backdrop)
            {
                backdrop = scene;
                // Under a loading window (the app opening, back from a game: cuadro 136), built once the window is drawn,
                // and the window lifted once the backdrop is: the title, not a frozen black, while it builds.
                if (App.AppFlow.Veiled) _ = BackdropUnderWindow(scene);
                else MenuBackdrop.Show(scene == "multijugador" ? MenuBackdrop.Scene.Crossroads : MenuBackdrop.Scene.Road);
            }
            if (scene == Scene) return;
            Scene = scene;
            SceneChanged?.Invoke(scene);
        }

        async Awaitable BackdropUnderWindow(string scene)
        {
            // (Only the window it was built under: «Continuar partida» raises the base's over it meanwhile.)
            var raised = App.AppFlow.Raised;
            try
            {
                await Awaitable.NextFrameAsync(destroyCancellationToken);
                await Awaitable.NextFrameAsync(destroyCancellationToken);
                if (backdrop == scene) MenuBackdrop.Show(scene == "multijugador" ? MenuBackdrop.Scene.Crossroads : MenuBackdrop.Scene.Road);
                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The screen went first.
            }
            App.AppFlow.LiftVeil(raised);
        }

        bool NoName => string.IsNullOrWhiteSpace(KnownName);

        /// <summary>«Continuar partida» with a game saved; the menu's other keys close up when it goes.</summary>
        void ShowSaved()
        {
            if (continueKey != null) Ui.Show(continueKey, Saved != null);
            if (savedNote != null)
            {
                Ui.Show(savedNote, Saved != null);
                if (Saved != null) savedNote.text = StartText.SavedLine(Saved.Chapter, Bastion.Engine.Lore.GameLevelText.NameOf(Saved.GameLevel));
            }
            if (oldNote == null) return;
            var old = previewing ? null : link.OldCampaign;
            Ui.Show(oldNote, Saved == null && old != null);
            if (old != null) oldNote.text = StartText.OldCampaign(old);
        }

        /// <summary>The campaign from before the expeditions, where «Continuar partida» would be; hidden without one.</summary>
        Label oldNote;
        /// <summary>Under «Continuar partida»: «Capítulo 1 · Medio».</summary>
        Label savedNote;

        // ------------------------------------------------------------ the keys

        /// <summary>«Continuar partida»: the saved game hosted and gone on with (NetLink.Continue), to the base or the mission.</summary>
        void Continue() => WithName(Page.Menu, name =>
        {
            // (What the loading sheet says: what was pressed, R22.)
            App.AppFlow.Ask(App.AppFlow.Asked.GoOn);
            return link.Continue(name);
        }, StartText.Opening, StartText.ContinueGo);

        /// <summary>
        /// «Nueva partida»: over a saved game, asked first as the room's «Nueva campaña» asks (it is kept aside, as there);
        /// then the name, always (p1: «pide el nombre antes de empezar»), and a game of one's own that the partner can join.
        /// </summary>
        void NewGame()
        {
            void Ask() => AskName(Page.Menu, name =>
            {
                App.AppFlow.Ask(App.AppFlow.Asked.New);
                return link.NewGame(name, level);
            }, StartText.Opening, StartText.Start, true);
            var saved = Saved;
            if (saved == null)
            {
                Ask();
                return;
            }
            settings.Confirm(new ConfirmView { Title = RoomText.AskAgain, Lines = { RoomText.Replaces(saved) }, Yes = RoomText.StartAgain, No = RoomText.Cancel, Danger = true }, Ask);
        }

        void OpenSettings() => settings.Show(new GameMenuView { Settings = App.GameSettings.Views(), SettingsNote = StartText.SettingsHere }, "settings");

        /// <summary>
        /// The left card: the game hosted under the name last played with (asked first with none). Its level isn't asked here
        /// but in the room, on «Nueva partida» (cuadro 124: going on with a game saved needs none).
        /// </summary>
        void Host() => WithName(Page.Two, name => link.Host(name), StartText.Opening, StartText.HostGo);

        /// <summary>The right card: into the game heard on the network; with none, the address and its key asked.</summary>
        void JoinCard()
        {
            if (heard == null)
            {
                Go(Page.Address);
                return;
            }
            var game = heard;
            WithName(Page.Two, name => link.Join(game.Address, name), StartText.Entering(game.Name), StartText.Join);
        }

        /// <summary>`run` as the name last played with; with none, the name asked first, under it `go` (what was pressed).</summary>
        void WithName(Page from, Func<string, string> run, string waiting, string go)
        {
            if (NoName) AskName(from, run, waiting, go);
            else Attempt(KnownName.Trim(), run, waiting);
        }

        void AskName(Page from, Func<string, string> run, string waiting, string go, bool levels = false)
        {
            askingLevel = levels;
            nameFrom = from;
            afterName = run;
            afterNameWaiting = waiting;
            Go(Page.Name);
            StartLook.Entries.SetText(goKey, go);
        }

        /// <summary>The form's key (or Intro in it): on with the name given, or into the game at the address.</summary>
        void FormGo()
        {
            if (busy || (page != Page.Name && page != Page.Address)) return;
            var name = nameField.value.Trim();
            if ((page == Page.Name || NoName) && name.Length == 0)
            {
                Say(StartText.NameMissing, true);
                nameField.Focus();
                return;
            }
            if (page == Page.Name)
            {
                Attempt(name, afterName, afterNameWaiting);
                return;
            }
            var address = addressField.value;
            var key = keyField.value;
            Attempt(NoName ? name : KnownName.Trim(), who => link.Join(address, who, key), StartText.Looking);
        }

        void FormBack() => Go(page == Page.Name ? nameFrom : Page.Two);

        /// <summary>One try at a time: its words while it runs, the keys resting; what went wrong, and back.</summary>
        void Attempt(string name, Func<string, string> run, string waiting)
        {
            if (busy) return;
            if (DryRun)
            {
                Debug.Log($"[inicio] haría: {waiting} (nombre «{name}»)");
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
            // Continuar or Nueva partida: «Abriendo la partida…» until the base is up (AppFlow keeps this screen meanwhile).
            if (!link.Opening) Say("", false);
            Entered?.Invoke(slot);
        }

        /// <summary>It didn't get in (NetLink's words): said, and the keys back. A taken name asks for another one.</summary>
        void OnFailed(string text)
        {
            Say(text, true);
            SetBusy(false);
        }

        void SetBusy(bool on)
        {
            busy = on;
            foreach (var b in buttons) b.SetEnabled(!on);
        }

        void Say(string text, bool bad)
        {
            status.text = text ?? "";
            status.EnableInClassList("start__status--on", !string.IsNullOrEmpty(text));
            status.EnableInClassList("start__status--bad", bad);
        }

        /// <summary>The games heard on the network: the right card joins the first of this version («Unirse a la partida de Ana»).</summary>
        void Heard(IReadOnlyList<LanGame> list)
        {
            heardList = list ?? new LanGame[0];
            heard = previewing ? previewHeard : heardList.FirstOrDefault(g => g.Version == link.Version);
            joinLabel.text = (heard != null ? StartText.JoinOf(heard.Name) : StartText.ByAddress).ToUpperInvariant();
            if (joinEntry != null) StartLook.Entries.SetText(joinEntry, heard != null ? StartText.JoinOf(heard.Name) : StartText.ByAddress);
        }

        /// <summary>
        /// The main menu's band (p1: «Fundir en diagonal el fondo oscuro de la columna con la escena»): dark from the left
        /// to SolidTop of the width at the top and SolidFoot at the foot, then clear over Band more, on the same slant. The
        /// room's too (RoomMenu, cuadro 124).
        /// </summary>
        internal static void Fade(MeshGenerationContext ctx)
        {
            var r = ctx.visualElement.contentRect;
            float w = r.width, h = r.height;
            if (w <= 0 || h <= 0) return;
            var dark = new Color32(7, 8, 9, 240);
            var clear = new Color32(7, 8, 9, 0);
            float top = w * SolidTop, foot = w * SolidFoot, band = w * Band;
            Vertex At(float x, float y, Color32 c) => new Vertex { position = new Vector3(x, y, Vertex.nearZ), tint = c };
            var mesh = ctx.Allocate(6, 12);
            mesh.SetAllVertices(new[] { At(0, 0, dark), At(top, 0, dark), At(top + band, 0, clear), At(0, h, dark), At(foot, h, dark), At(foot + band, h, clear) });
            mesh.SetAllIndices(new ushort[] { 0, 1, 4, 0, 4, 3, 1, 2, 5, 1, 5, 4 });
        }

        /// <summary>
        /// For the labs' captures (HudShots.Entry): the shot's camera kept where the backdrop's own stands, framing as it
        /// frames (as Arte's MenuLab.InPlay): Shots holds the camera it found before play, when the backdrop had none.
        /// </summary>
        public sealed class BackdropShot : MonoBehaviour
        {
            void LateUpdate()
            {
                var eye = MenuBackdrop.Current != null ? MenuBackdrop.Current.Eye : null;
                var own = GetComponent<Camera>();
                if (eye == null || own == null) return;
                own.enabled = true;
                transform.SetPositionAndRotation(eye.transform.position, eye.transform.rotation);
                own.nearClipPlane = eye.nearClipPlane;
                own.farClipPlane = eye.farClipPlane;
                own.clearFlags = CameraClearFlags.SolidColor;
                own.backgroundColor = eye.backgroundColor;
                var wide = Camera.VerticalToHorizontalFieldOfView(MenuBackdrop.Current.Fov, 16f / 9);
                own.fieldOfView = own.aspect >= 16f / 9 ? MenuBackdrop.Current.Fov : Camera.HorizontalToVerticalFieldOfView(wide, own.aspect);
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
