using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Net;
using Bastion.Engine;
using Bastion.Net;
using Bastion.UI.Hud;

namespace Bastion.UI.Combat
{
    public sealed partial class CombatController
    {
        /// <summary>Controles (hud.ts CONTROLS): each key and what it does, as the keys are in this version.</summary>
        static readonly (string keys, string what)[] Controls =
        {
            ("Clic", "Seleccionar y mover"),
            ("Tab", "Siguiente soldado u objetivo"),
            ("1–9", "Usar una habilidad; si necesita objetivo, elegirlo y pulsar otra vez"),
            ("Espacio · Intro", "Usar la habilidad elegida; sin ninguna, disparar"),
            ("C", "Ceder el mando"),
            ("Retroceso", "Terminar turno"),
            // Signalling (playtest 19/10 #14): Shift and a click, G, or the wheel's click.
            ("Mayús + clic · G · clic con la rueda", Lore.SettingsText.PingControls),
            ("H · clic en su nombre", "Hoja del soldado: tablero, estadísticas y lo que lleva"),
            ("Q / E", "Girar la cámara"),
            ("Rueda", "Acercar o alejar"),
            ("RePág / AvPág · Mayús + rueda", "Subir o bajar de planta"),
            ("WASD", "Mover la cámara"),
            ("F", "Centrar en el soldado"),
            ("O", "Centrar en el objetivo"),
            ("Clic derecho", "Ficha del enemigo"),
            ("M", "Sonido"),
            ("Esc", "Cancelar o abrir este menú"),
        };

        const string EnemySpeedId = "enemySpeed";
        const string WalkSpeedId = "walkSpeed";
        const string PlansId = "autoPlan";

        /// <summary>«Menú» or Esc: the game's menu with what it can do now, or closed.</summary>
        void ToggleMenu()
        {
            if (Hud == null || State == null) return;
            Hud.ToggleMenu(MenuView());
        }

        GameMenuView MenuView()
        {
            var partner = PartnerHere();
            var over = State.Outcome != null;
            return new GameMenuView
            {
                // The main menu (#7): not once it is over, the report has its own way out; nor abandoning then.
                Leave = !over,
                Abandon = !over,
                AbandonLabel = Lore.AbandonText.Button,
                QuitLabel = Lore.QuitText.Button,
                AbandonTip = HudText.Menu.AbandonTip(partner?.Name, Players.Count > 1),
                // One's own call waiting for the partner: off, saying so.
                AbandonWaiting = State.AbandonCall != null && State.AbandonCall.By == MySlot ? Lore.EvacWaiting(partner?.Name ?? "el otro jugador") : null,
                // The graphics' rows (GameSettings), the battle's own among them, as the 0.10.1's.
                Settings = App.GameSettings.Views(new List<SettingView>
                {
                    // The enemy turn's speed, a slider (playtest 18/10 #6); the squad's own actions play at their own pace.
                    new SettingView
                    {
                        Id = EnemySpeedId,
                        Label = Lore.SettingsText.EnemySpeed,
                        About = Lore.SettingsText.EnemySpeedAbout + " En el turno enemigo, mantén la barra espaciadora para verlo más rápido.",
                        Slider = true,
                        Min = 0.5f,
                        Max = 2.5f,
                        Step = 0.1f,
                        Value = EventPlayer.EnemySpeed,
                        Low = "Lenta",
                        High = "Rápida",
                    },
                    // How fast the soldiers walk and run on screen, to try (Ana, 22/10 #32): the rules don't change.
                    new SettingView
                    {
                        Id = WalkSpeedId,
                        Label = "Velocidad de los soldados",
                        About = "Lo rápido que se mueven en pantalla. No cambia las reglas.",
                        Slider = true,
                        Min = 0.5f,
                        Max = 2f,
                        Step = 0.05f,
                        Value = EventPlayer.WalkSpeed,
                        Low = "Lenta",
                        High = "Rápida",
                    },
                    new SettingView
                    {
                        Id = PlansId,
                        Label = Lore.SettingsText.Plans,
                        About = Lore.SettingsText.PlansAbout,
                        Choices = new List<string> { "Sí", "No" },
                        On = AutoPlan ? 0 : 1,
                    },
                }),
                Controls = Controls.Select(c => new ControlView { Keys = c.keys, What = c.what }).ToList(),
                NightLabel = Lore.NightText.Label,
                NightRules = Lore.NightText.Rules.ToList(),
            };
        }

        /// <summary>A setting of Ajustes changed, kept on this computer and applied at once.</summary>
        public void SettingChanged(string id, float value)
        {
            if (id == EnemySpeedId)
            {
                EventPlayer.EnemySpeed = value;
                // The enemy's turn playing now takes it at once.
                Hurry?.Invoke(false);
            }
            else if (id == WalkSpeedId) EventPlayer.WalkSpeed = value;
            else if (id == PlansId) AutoPlan = value == 0;
            else App.GameSettings.Change(id, value);
        }

        /// <summary>An entry of the menu: each asks first when it costs something (hud.ts confirmLeave, confirmAbandon, confirmQuit).</summary>
        public void MenuChose(string what)
        {
            if (Hud == null || State == null) return;
            Hud.CloseMenu();
            var partner = PartnerHere();
            if (what == GameMenu.Leave)
            {
                // Asked with the other one here, who goes too; otherwise at once.
                if (partner == null)
                {
                    Send(new BackToMenuMessage());
                    return;
                }
                Hud.Confirm(new ConfirmView
                {
                    Title = HudText.Menu.LeaveTitle,
                    Lines = { State.CampaignMission != null ? HudText.Menu.LeaveCampaign : HudText.Menu.LeaveSkirmish, HudText.Menu.LeaveToo(partner.Name) },
                    Yes = HudText.Menu.LeaveYes,
                    No = HudText.Menu.LeaveNo,
                }, () => Send(new BackToMenuMessage()));
            }
            else if (what == GameMenu.Abandon)
            {
                // What it costs, before it is asked; with the partner there, it waits for a yes on their bar.
                var ask = new ConfirmView
                {
                    Title = $"¿{Lore.AbandonText.Button}?",
                    Lines = { Lore.AbandonText.Stakes },
                    Yes = Lore.AbandonText.Accept,
                    No = Lore.AbandonText.Refuse,
                    Danger = true,
                };
                if (partner != null) ask.Lines.Add(HudText.Menu.AbandonAsks(partner.Name));
                Hud.Confirm(ask, () => Send(new AbandonMessage()));
            }
            else if (what == GameMenu.Quit)
            {
                // It is saved; hosting it, the partner is left without it; a guest's soldiers stand still meanwhile (ui/quit.ts).
                var hosting = NetLink.Instance.Hosting;
                Hud.Confirm(new ConfirmView
                {
                    Title = Lore.QuitText.Title,
                    Lines = { Lore.QuitText.SavedMission },
                    Warning = partner == null ? null : hosting ? Lore.QuitHostWarning(partner.Name) : Lore.QuitGuestWarning(partner.Name),
                    Yes = Lore.QuitText.Accept,
                    No = Lore.QuitText.Refuse,
                    Danger = true,
                }, App.AppFlow.Quit);
            }
        }
    }
}
