using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The HUD with made-up views, to look at and capture each piece without a battle (as src/lab on the web): on the
    /// HudRoot's GameObject, it fills every piece and adds a row of buttons, top centre, to go through their states.
    /// Six soldiers (two of Ana's), three enemies with flags in front of the camera, a log, the clocks running.
    /// </summary>
    [RequireComponent(typeof(HudRoot))]
    public sealed class HudLab : MonoBehaviour, IHudHandlers
    {
        /// <summary>States to go to as it starts (Step's names, in order): what a capture's setup asks for.</summary>
        public string[] Steps = new string[0];

        HudRoot hud;
        int turnState;
        int shotState;
        int bannerState;
        bool call;
        int outcomeState;
        bool siege;
        bool close;
        int logCount;
        string selected = "s1";
        float clockStart;

        static readonly string Arturo = "#a78bfa";
        static readonly string Ana = "#f59e0b";

        void Start()
        {
            hud = GetComponent<HudRoot>();
            if (!hud.Ready) return;
            hud.Bind(this);
            hud.HeadOf = HeadOf;
            hud.ClockOf = ClockOf;
            clockStart = Time.time;
            Controls();
            Fill();
            hud.Intro(new IntroView
            {
                Kicker = "OPERACIÓN · BRENA",
                Title = "El colegio de Brena",
                Objective = "Abatid a todo lo que haya",
                Turns = "Sin límite de turnos",
                // With Lore's marks, as the campaign's briefings come (the key words, the objective).
                Briefing = "Aviso del Refugio de Bielsa. Allí queda un **equipo de radio** entero. ==Traedlo a La Lata.==",
            });
            hud.Log("La escuadra llega a la zona.", "#8fd05a");
            foreach (var step in Steps) Step(step);
        }

        /// <summary>The lab's own buttons, top centre, over the HUD.</summary>
        void Controls()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            var row = Ui.El("lab-row", root);
            row.style.position = Position.Absolute;
            row.style.top = 44;
            row.style.left = 260;
            row.style.right = 260;
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.justifyContent = Justify.Center;
            void Add(string label, System.Action act)
            {
                var b = new Button(() =>
                {
                    act();
                    Fill();
                }) { text = label };
                b.style.marginRight = 4;
                b.style.marginBottom = 4;
                row.Add(b);
            }
            Add("Turno", () => turnState = (turnState + 1) % 4);
            Add("Disparo", () => shotState = (shotState + 1) % 4);
            Add("Aviso", () =>
            {
                bannerState++;
                ShowBanner();
            });
            Add("Llamada", () => call = !call);
            Add("Resultado", () => outcomeState = (outcomeState + 1) % 3);
            Add("Registro", () => hud.Log($"Línea {++logCount}: Mateo dispara al zombi y le quita 4.", logCount % 2 == 0 ? "#ff6b5a" : "#54d1ff"));
            Add("Flotante", () => hud.Float(HeadOf("a1") ?? Vector3.zero, "CRÍTICO −5", "crit"));
            Add("Asedio", () => siege = !siege);
            Add("Al hombro", () => close = !close);
        }

        /// <summary>
        /// A state by name, as its button does (for captures in batchmode): turno, disparo, aviso, llamada, resultado,
        /// registro, flotante, asedio, hombro; and «llegada» (the arrival card, closed if open), «cerrar» (everything off).
        /// </summary>
        public void Step(string what)
        {
            switch (what)
            {
                case "turno": turnState = (turnState + 1) % 4; break;
                case "disparo": shotState = (shotState + 1) % 4; break;
                case "aviso":
                    bannerState++;
                    ShowBanner();
                    break;
                case "llamada": call = !call; break;
                case "resultado": outcomeState = (outcomeState + 1) % 3; break;
                case "registro": hud.Log($"Línea {++logCount}: Mateo dispara al zombi y le quita 4.", logCount % 2 == 0 ? "#ff6b5a" : "#54d1ff"); break;
                case "flotante": hud.Float(HeadOf("a1") ?? Vector3.zero, "CRÍTICO −5", "crit"); break;
                // A search's find: the supplies' «§» (the crate, BastionSuministros) in the log and floating.
                case "botin":
                    hud.Log("Ana registra el despacho: una radio de manivela (+12 §) y 1 pieza.", "#ffb84d");
                    hud.Float(HeadOf("s1") ?? Vector3.zero, "+12 §", "info");
                    break;
                case "asedio": siege = !siege; break;
                case "hombro": close = !close; break;
                case "llegada": hud.CloseIntro(); break;
                case "cerrar":
                    shotState = 0;
                    call = false;
                    outcomeState = 0;
                    siege = false;
                    close = false;
                    hud.ReleaseBanner(true);
                    break;
            }
            Fill();
        }

        void Fill()
        {
            var owner = new[] { "mine", "partner", "enemy", "over" }[turnState];
            hud.SetTurn(new TurnView
            {
                Turn = 3,
                Owner = owner,
                OwnerName = "Ana",
                Color = owner == "partner" ? Ana : Arturo,
                Concealed = turnState == 0,
                NightLabel = "De noche",
                NightRules = "De noche se ve menos: hasta 6 casillas, salvo con luz.",
                InPlay = owner == "partner" ? "Chiara Rossi" : "",
            });
            hud.SetPlayers(new PlayersView
            {
                Players = new List<PlayerView>
                {
                    new PlayerView { Slot = 0, Name = "Arturo", Color = Arturo, Me = true, Connected = true, Command = owner == "mine", Status = owner == "partner" ? "Listo" : "" },
                    new PlayerView { Slot = 1, Name = "Ana", Color = Ana, Connected = true, Command = owner == "partner", Status = owner == "mine" ? "Con acciones" : "" },
                },
            });
            hud.SetFloor(new FloorView { Level = 0, Max = 2 });
            hud.SetSquad(Squad());
            hud.SetSelected(Selected());
            hud.SetAbilities(Bar(owner));
            hud.SetTarget(Shot());
            hud.SetEndTurn(new EndTurnView { Mode = owner == "mine" ? "act" : owner == "partner" ? "waiting" : owner == "enemy" ? "enemy" : "disabled", Detail = owner == "partner" ? "Ana" : null });
            hud.SetCommand(new CommandView { Shared = owner == "mine" || owner == "partner", Mine = owner == "mine", Holder = owner == "mine" ? "Arturo" : "Ana", CanPass = true });
            hud.SetGoTo(owner == "mine" ? "Mateo Rojas" : null);
            hud.SetMission(new MissionView
            {
                Label = siege ? "Asedio" : "Limpieza",
                Area = "Brena",
                Objective = siege ? "Acabad con todos" : "Abatid a todo lo que haya",
                ObjectiveTip = siege ? "Acabad con todas las oleadas. Si caen todos, el Bastión cae." : null,
                WaveText = siege ? "OLEADA · ESTE TURNO" : null,
                WaveTip = siege ? "La próxima oleada llega al final de este turno." : null,
                WaveUrgent = siege,
            });
            hud.SetCompass(new CompassView { Shown = siege, Hot = siege ? new List<int> { 0, 2 } : new List<int>() });
            hud.SetFlags(Flags());
            hud.SetCloseShot(close);
            hud.SetCall(call
                ? EvacCall()
                : null);
            hud.SetOutcome(outcomeState == 0 ? null : new OutcomeView { Victory = outcomeState == 1, Line = outcomeState == 1 ? "Volvéis con el equipo de radio." : "No queda nadie en pie." });
        }

        /// <summary>The partner's call to leave, in Lore's words as the game makes it (CombatController.CallView): one left outside.</summary>
        static CallView EvacCall()
        {
            var ask = Bastion.Engine.Lore.EvacAsk("Ana", new Bastion.Engine.Lore.EvacSituation(false, false, new[] { "Freya Lund" }));
            return new CallView
            {
                Kind = "evac",
                Kicker = "Evacuación",
                Title = ask.Title,
                Text = ask.Text,
                ClockNote = $"Sin respuesta, {Bastion.Engine.Lore.Bus.Name} sale",
                Ms = 45000,
                Accept = Bastion.Engine.Lore.EvacBar.Accept,
                Refuse = Bastion.Engine.Lore.EvacBar.Wait,
            };
        }

        void ShowBanner()
        {
            switch (bannerState % 4)
            {
                case 1:
                    hud.Banner("TU TURNO", "xcom", null, true);
                    break;
                case 2:
                    hud.Banner("¡CONTACTO!", "alien", "Mateo ha visto a 3 zombis");
                    break;
                case 3:
                    hud.Banner("OCULTACIÓN ROTA", "warn", "Os ha visto un zombi que no veíais.");
                    break;
            }
        }

        SquadView Squad()
        {
            var v = new SquadView();
            void Card(string id, string name, string accent, bool mine, int hp, int max, int ap, float[] skills, params TagView[] tags)
            {
                v.Cards.Add(new SquadCardView
                {
                    Id = id,
                    Name = name,
                    Accent = accent,
                    Mine = mine,
                    Selected = id == selected,
                    Hp = hp,
                    MaxHp = max,
                    Ap = ap,
                    ApMax = 2,
                    Skills = skills,
                    Title = mine ? "Seleccionar" : "Soldado del otro jugador",
                    Tags = new List<TagView>(tags),
                    Dead = hp == 0,
                });
            }
            Card("s1", "Mateo Rojas", Arturo, true, 6, 7, 2, new float[] { 20, 0, 0, 0, 0, 10 });
            Card("s2", "«Chispa»", Arturo, true, 5, 5, 1, new float[] { 10, 10, 0, 0, 0, 0 }, new TagView { Text = "VIGILANCIA", Kind = "ow" });
            Card("s3", "Freya Lund", Arturo, true, 3, 6, 0, new float[] { 0, 0, 10, 10, 10, 0 }, new TagView { Text = Bastion.Engine.Lore.BiteTag, Kind = "bite", Tip = Bastion.Engine.Lore.BiteNote(true) });
            Card("s4", "Zainab Bello", Arturo, true, 0, 6, 0, new float[] { 0, 0, 0, 0, 0, 0 });
            Card("s5", "Chiara Rossi", Ana, false, 6, 6, 2, new float[] { 0, 10, 0, 0, 0, 10 });
            // Ana's turn, one soldier at a time: hers in play.
            if (turnState == 1) v.Cards[v.Cards.Count - 1].InPlay = true;
            Card("s6", "Ana Ríos", Ana, false, 4, 6, 2, new float[] { 0, 0, 0, 0, 10, 10 });
            return v;
        }

        UnitPanelView Selected()
        {
            return new UnitPanelView
            {
                Id = selected,
                Soldier = true,
                Rank = 2,
                RankName = "Veterano",
                Skills = new float[] { 20, 0, 0, 0, 0, 10 },
                Name = "Mateo Rojas",
                Stats = new List<StatView>
                {
                    new StatView { Kind = "hp", Tip = "Salud 6/7", Filled = 6, Total = 7 },
                    new StatView { Kind = "ap", Tip = "Acciones 2/2", Filled = 2, Total = 2 },
                    new StatView { Kind = "ammo", Tip = "Fusil de precisión: 3/4 · 2 de repuesto", Filled = 3, Total = 4, Value = "+2" },
                    new StatView { Kind = "aim", Tip = "Puntería con armas de fuego 68", Value = "68" },
                    new StatView { Kind = "melee", Tip = "Puntería cuerpo a cuerpo 87", Value = "87" },
                    new StatView { Kind = "grenade", Tip = "Granadas: 2", Value = "×2" },
                },
                Weapon = "Fusil de precisión",
                WeaponTip = "Fusil de precisión · Pistola · Cuchillo",
            };
        }

        ActionBarView Bar(string owner)
        {
            var bar = new ActionBarView { Lock = owner == "partner" ? "Turno de Ana: puedes apuntar y planear" : owner == "enemy" ? "Turno enemigo" : null };
            AbilityView A(string id, string name, string key, string cost, string desc, bool enabled = true, string menu = null) =>
                new AbilityView { Id = id, Name = name, Hotkey = key, Cost = cost, Description = desc, Enabled = enabled && owner == "mine", Menu = menu };
            var shoot = A("shoot", "Disparar", "1", "1 acción · Acaba el turno", "Dispara al blanco elegido.");
            shoot.Weapon = "rifle";
            shoot.Active = shotState == 1;
            shoot.Stats.Add(new StatPair { Key = "Daño", Value = "4–6" });
            shoot.Stats.Add(new StatPair { Key = "Alcance", Value = "Largo" });
            shoot.Stats.Add(new StatPair { Key = "Ruido", Value = "22" });
            bar.Abilities.Add(shoot);
            bar.Abilities.Add(A("slash", "Cuchillo", "2", "1 acción", "Golpea a quien tengas al lado."));
            var reload = A("reload", "Recargar", "3", "1 acción", "Llena el cargador.", false);
            reload.Reason = "El cargador está lleno.";
            bar.Abilities.Add(reload);
            var watch = A("menu:watch", "Vigilancia", "4", "", "Elige cómo vigilar.");
            watch.Icon = "overwatch";
            bar.Abilities.Add(watch);
            bar.Abilities.Add(A("overwatch", "Vigilancia", "", "Acaba el turno", "Dispara al primero que se mueva a la vista.", true, "menu:watch"));
            bar.Abilities.Add(A("overwatchMelee", "Hoja en alto", "", "Acaba el turno", "Golpea al primero que llegue a tu lado.", true, "menu:watch"));
            var hunker = A("hunker", "Cubrirse", "5", "Acaba el turno", "Más defensa hasta tu próximo turno.");
            hunker.Active = shotState == 3;
            bar.Abilities.Add(hunker);
            var grenade = A("grenade", "Granada", "6", "1 acción", "Explota en un área.");
            grenade.Count = 2;
            bar.Abilities.Add(grenade);
            var medkit = A("firstAid", "Botiquín", "7", "1 acción", "Cura 4.");
            medkit.Depleted = true;
            medkit.Enabled = false;
            bar.Abilities.Add(medkit);
            var stun = A("stunLance", "Aturdir", "8", "1 acción", "Deja al blanco sin su turno.");
            stun.Cooldown = 2;
            stun.Enabled = false;
            bar.Abilities.Add(stun);
            // The doors (our own drawings, LineIcon): one to open beside the soldier, none to close.
            bar.Abilities.Add(A("openDoor", "Abrir puerta", "9", "Gratis", "Abre la puerta de al lado."));
            bar.Abilities.Add(A("closeDoor", "Cerrar puerta", "", "Gratis", "Cierra la puerta de al lado.", false));
            return bar;
        }

        ShotView Shot()
        {
            switch (shotState)
            {
                case 1:
                    return new ShotView
                    {
                        Kind = "attack",
                        Title = "DISPARAR · FUSIL DE PRECISIÓN",
                        Action = "Disparar",
                        TargetName = "Zombi",
                        Index = 0,
                        Total = 3,
                        Hit = 60,
                        Crit = 60,
                        HasDamage = true,
                        Damage = new DamageView { Hit = "4–6", Crit = "6–8", Kill = "Lo mata" },
                        Mods = new List<ModView>
                        {
                            new ModView { Label = "Puntería", Value = 60 },
                            new ModView { Label = "Tiro", Value = 8 },
                            new ModView { Label = "Alcance", Value = -18 },
                            new ModView { Label = "Defensa", Value = 10 },
                        },
                        CritMods = new List<ModView>
                        {
                            new ModView { Label = "Arma", Value = 10 },
                            new ModView { Label = "Flanqueo", Value = 50 },
                        },
                        Noise = "Ruido 22",
                        Locked = turnState == 1 ? "MANDO: ANA" : null,
                    };
                case 2:
                    return new ShotView { Kind = "support", Title = "BOTIQUÍN", Action = "Curar", TargetName = "Freya Lund", Index = 0, Total = 1, Effect = "Cura 4: queda en 6/6." };
                case 3:
                    return new ShotView { Kind = "self", Title = "CUBRIRSE", Action = "Cubrirse", Cost = "Acaba el turno", Effect = "Más defensa hasta tu próximo turno.", Noise = "Sin ruido" };
                default:
                    return null;
            }
        }

        FlagsView Flags()
        {
            var v = new FlagsView();
            v.Flags.Add(new FlagView { Id = "s1", Name = "Mateo", Hp = 6, MaxHp = 7, Cover = 2 });
            v.Flags.Add(new FlagView { Id = "a1", Name = "Zombi", Enemy = true, Hp = 4, MaxHp = 4, Target = shotState == 1, HoverKind = "aim", HoverText = "60 %" });
            v.Flags.Add(new FlagView { Id = "a2", Name = "Chillón", Enemy = true, Hp = 3, MaxHp = 5, Armor = 1, Tags = new List<TagView> { new TagView { Text = "ATURDIDO", Kind = "stun" } } });
            return v;
        }

        /// <summary>The lab's units stand in a row in front of the camera.</summary>
        Vector3? HeadOf(string id)
        {
            var cam = Camera.main;
            if (cam == null) return null;
            var slot = id == "s1" ? -3 : id == "a1" ? 0 : id == "a2" ? 3 : 99;
            if (slot == 99) return null;
            return cam.transform.position + cam.transform.forward * 12 + cam.transform.right * slot;
        }

        /// <summary>The clocks run down over a minute, and start again.</summary>
        ClockView ClockOf(int slot)
        {
            if (turnState > 1) return null;
            var left = 1 - ((Time.time - clockStart) % 60f) / 60f;
            return new ClockView { Shown = true, Left = slot == 0 ? left : Mathf.Min(1, left + 0.2f), Running = true, Color = slot == 0 ? Arturo : Ana };
        }

        // ------------------------------------------------------- handlers

        public void OnAbility(string id) => hud.Toast($"Habilidad: {id}");
        public void OnEndTurn() => hud.Toast("Terminar turno");
        public void OnSelectUnit(string id)
        {
            selected = id;
            Fill();
        }
        public void OnConfirmTarget() => hud.Toast("¡Fuego!");
        public void OnCancel()
        {
            shotState = 0;
            Fill();
        }
        public void OnCycleTarget(int dir) => hud.Toast(dir > 0 ? "Siguiente blanco" : "Blanco anterior");
        public void OnPassCommand() => hud.Toast("Ceder el mando");
        public void OnWatchPartner() => hud.Toast("Ver a Ana");
        public void OnGoToSoldier() => hud.Toast("Ir al soldado");
        public void OnFollowTurn(bool on) => hud.Toast(on ? "Cámara al soldado: sí" : "Cámara al soldado: no");
        public void OnCallAnswer(bool accept)
        {
            call = false;
            Fill();
            hud.Toast(accept ? "Os vais" : "Seguís");
        }
        public void OnIntroClosed() => hud.Log("Empieza la misión.");
        public void OnBannerReleased() => hud.Toast("Aviso soltado");
        public void OnNewMission(bool random) => hud.Toast(random ? "Misión al azar" : "Otra igual");
        public void OnReturnToBase() => hud.Toast("Volver a la base");
        public void OnBackToMenu() => hud.Toast("Volver al menú");
        public void OnSheet(string unit) => hud.Toast($"Hoja de {unit}");
        public void OnFloor(int dir) => hud.Toast(dir > 0 ? "Subir de planta" : "Bajar de planta");
        public void OnMenu() => hud.Toast("Menú");
        public void OnItem(PackOrder order) => hud.Toast($"Mochila: {order.Action} {order.Index} {order.Id} {order.To}");
        public void OnTakeAll(System.Collections.Generic.List<string> ids) => hud.Toast($"Coger {ids.Count}");
        public void OnSwap(string take, int drop) => hud.Toast($"Cambiar {take} por el hueco {drop}");
    }
}
