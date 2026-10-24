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
            ("G", Lore.SettingsText.PingControls),
            ("Q / E", "Girar la cámara"),
            ("Rueda", "Acercar o alejar"),
            ("RePág / AvPág", "Subir o bajar de planta"),
            ("WASD", "Mover la cámara"),
            ("F", "Centrar en el soldado"),
            ("O", "Centrar en el objetivo"),
            ("Esc", "Cancelar o abrir este menú"),
        };

        const string EnemySpeedId = "enemySpeed";
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
                Settings = new List<SettingView>
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
                    new SettingView
                    {
                        Id = PlansId,
                        Label = Lore.SettingsText.Plans,
                        About = Lore.SettingsText.PlansAbout,
                        Choices = new List<string> { "Sí", "No" },
                        On = AutoPlan ? 0 : 1,
                    },
                },
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
            else if (id == PlansId) AutoPlan = value == 0;
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
