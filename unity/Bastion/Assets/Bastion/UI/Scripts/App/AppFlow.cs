using Bastion.Client.Net;
using Bastion.Engine;
using Bastion.Net;
using Bastion.UI.Hud;
using Bastion.UI.Start;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Bastion.UI.App
{
    /// <summary>
    /// What the app shows, from Red's NetLink (docs/UNITY-UI.md; UI's, Gameplay 23/10). Out of a room, the start screen;
    /// in one, its menu while it is in the lobby, and the battle's scene while it is in a mission. It reads the room
    /// (NetLink.LastRoom) and never listens to NetLink.Message, which waits for the battle with its snapshot.
    ///
    /// Over any scene, its own card and line: the host closed the game («Ana ha cerrado la partida»: back in, to the
    /// start screen, or out, as net.ts hostClosed did), nothing changing scene meanwhile; the seat opened in another
    /// window (to the start screen, saying so); and the network's state at the top.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class AppFlow : MonoBehaviour
    {
        [SerializeField] string startScene = "Inicio";
        [SerializeField] string battleScene = "Partida";

        static AppFlow instance;
        NetLink link;
        VisualElement root;
        VisualElement card;
        Label cardNote;
        Label line;
        bool loading;
        bool rejoining;
        NetStatus status = NetStatus.Open;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            link = NetLink.Instance;
            link.HostClosed += OnHostClosed;
            link.Replaced += OnReplaced;
            link.Status += OnStatus;
            link.Rejoining += OnRejoining;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            // Over everything the scenes put up.
            doc.sortingOrder = 100;
            root = doc.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            foreach (var sheet in new[] { "Hud/Mixta", "Hud/Cards" })
            {
                var s = Resources.Load<StyleSheet>(sheet);
                if (s != null) root.styleSheets.Add(s);
            }
            root.AddToClassList("font-text");
            line = Ui.Text("", "app__line", root);
            line.pickingMode = PickingMode.Ignore;
            line.style.position = Position.Absolute;
            line.style.top = 6;
            line.style.alignSelf = Align.Center;
            line.style.backgroundColor = new Color(0.07f, 0.08f, 0.08f, 0.94f);
            line.style.paddingLeft = 10;
            line.style.paddingRight = 10;
            Ui.Show(line, false);
        }

        void OnDestroy()
        {
            if (instance != this || link == null) return;
            link.HostClosed -= OnHostClosed;
            link.Replaced -= OnReplaced;
            link.Status -= OnStatus;
            link.Rejoining -= OnRejoining;
            instance = null;
        }

        void Update()
        {
            if (loading || card != null) return;
            var phase = link.LastRoom?.Phase;
            var inBattle = SceneManager.GetActiveScene().name == battleScene;
            if (phase == RoomPhases.Mission)
            {
                if (!inBattle) Load(battleScene);
                return;
            }
            if (inBattle)
            {
                // The mission over and back to the menu, or out of the room: the start scene.
                Load(startScene);
                return;
            }
            Screens(phase == null);
        }

        /// <summary>
        /// A scene in: the battle's gets the HUD (Arte's scene stays his alone, Gameplay 23/10): a document with the HUD's
        /// panel (AppFlow's own), HudRoot, and the mission over the network (CombatSession: the controller, the camera's
        /// rig and the marks).
        /// </summary>
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            loading = false;
            if (scene.name != battleScene) return;
            var hud = new GameObject("HUD");
            SceneManager.MoveGameObjectToScene(hud, scene);
            hud.AddComponent<UIDocument>().panelSettings = GetComponent<UIDocument>().panelSettings;
            var root = hud.AddComponent<HudRoot>();
            hud.AddComponent<Bastion.UI.Combat.CombatSession>().Hud = root;
        }

        void Load(string scene)
        {
            loading = true;
            SceneManager.LoadScene(scene);
        }

        /// <summary>In the start scene: the start screen out of a room, its menu in one (one at a time on its document).</summary>
        static void Screens(bool outside)
        {
            var start = FindFirstObjectByType<StartScreen>(FindObjectsInactive.Include);
            var room = FindFirstObjectByType<RoomMenu>(FindObjectsInactive.Include);
            if (start == null || room == null) return;
            if (start.enabled == outside && room.enabled == !outside) return;
            // The one going off first: the one coming on clears the document and draws itself.
            if (outside) room.enabled = false;
            else start.enabled = false;
            if (outside) start.enabled = true;
            else room.enabled = true;
        }

        // ------------------------------------------------------- host closed

        /// <summary>The host closed the game: the card, for the guest (a host's own app is closing with it).</summary>
        void OnHostClosed()
        {
            if (link.Hosting || card != null) return;
            var host = link.PartnerName;
            card = Ui.El("card panel");
            card.style.position = Position.Absolute;
            card.style.alignSelf = Align.Center;
            card.style.top = Length.Percent(32);
            Ui.Text(Lore.HostClosedLine(host), "card__title font-title", card);
            cardNote = Ui.Text(Lore.HostClosed.Back, "card__text", card);
            var actions = Ui.El("card__actions", card);
            var retry = new Button(Retry) { text = Lore.HostClosed.Retry };
            retry.AddToClassList("btn-primary");
            actions.Add(retry);
            actions.Add(new Button(Home) { text = Lore.HostClosed.Home });
            actions.Add(new Button(Quit) { text = Lore.QuitText.Button });
            root.Add(card);
        }

        /// <summary>Back in, the same seat (NetLink keeps it by host): «Aún no está abierta» while it isn't.</summary>
        void Retry()
        {
            link.Joined -= OnBackIn;
            link.Failed -= OnNotYet;
            link.Joined += OnBackIn;
            link.Failed += OnNotYet;
            var problem = link.Join(link.LastAddress ?? "", link.LastName ?? "");
            if (problem != null) OnNotYet(problem);
        }

        void OnBackIn(int slot)
        {
            link.Joined -= OnBackIn;
            link.Failed -= OnNotYet;
            CloseCard();
        }

        void OnNotYet(string _)
        {
            link.Joined -= OnBackIn;
            link.Failed -= OnNotYet;
            if (cardNote != null) cardNote.text = Lore.HostClosed.NotYet;
        }

        void Home()
        {
            link.Leave();
            CloseCard();
        }

        void CloseCard()
        {
            card?.RemoveFromHierarchy();
            card = null;
            cardNote = null;
        }

        /// <summary>This seat opened in another window: back to the start screen, saying so.</summary>
        void OnReplaced()
        {
            StartScreen.Note = RoomText.Replaced;
            CloseCard();
            link.Leave();
        }

        /// <summary>Closes the game (the game's menu, the host's card); in the editor, stops playing.</summary>
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------- the line

        void OnStatus(NetStatus s)
        {
            status = s;
            DrawLine();
        }

        void OnRejoining(bool on)
        {
            rejoining = on;
            DrawLine();
        }

        /// <summary>At the top while not connected (hud.ts setConnection), or coming back after a sleep (RejoinLine).</summary>
        void DrawLine()
        {
            if (line == null) return;
            var text = rejoining ? Lore.RejoinLine : status == NetStatus.Connecting ? HudText.Net.Connecting : status == NetStatus.Closed && link.LastRoom != null ? HudText.Net.Lost : "";
            line.text = text;
            Ui.Show(line, text.Length > 0 && card == null);
        }
    }
}
