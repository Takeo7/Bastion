#nullable enable
// The runs' texts: Lore's since 16/11 (the names read from Lore/; the Sucesos are Lore.RunEvents, and Events is left
// empty for whoever still looks there). The rules don't read them; the refusals, the proposals and the log do.
using System.Collections.Generic;

namespace Bastion.Engine
{
    /// <summary>What the runs say: refusals, proposals, log lines and names (Lore, 16/11).</summary>
    public static class RunTexts
    {
        // --------------------------------------------------------------- refusals

        public const string InFight = "En combate no se puede.";
        public const string NoGear = "Ese objeto ya no está.";
        public const string NotYours = "Solo puedes cambiar el equipo de tus soldados.";
        public const string StashAtBase = "El almacén solo se usa en la base.";
        public const string OnlySquad = "Durante la expedición, solo con los de la escuadra.";
        public const string NoSoldier = "Ese soldado ya no está.";
        public const string NoSlot = "Ese hueco no existe.";
        public const string NoRoom = "No cabe ahí.";
        public const string PileFull = "Esa pila está llena.";
        public const string SlotTaken = "Ese hueco está ocupado.";
        public const string NotAPile = "Eso no se parte.";
        public const string NotAMod = "Eso no es una mejora.";
        public const string NotAWeapon = "Eso no es un arma.";
        public const string ModsOnGuns = "Solo las armas de fuego llevan mejoras.";
        public const string ModOn = "Ya la lleva puesta.";
        public const string ModOff = "No la lleva puesta.";
        public const string UnmountAtBase = "Las mejoras se quitan en la base.";
        public const string StashFull = "No cabe en el almacén.";
        public const string NotUsable = "Eso no se usa así.";
        public const string BetweenNodes = "Solo entre etapas.";
        public const string FullHealth = "No le falta salud.";
        public const string NotInSquad = "No está en la escuadra.";
        public const string NoRun = "No hay ninguna expedición en marcha.";
        public const string RunOn = "Ya hay una expedición en marcha.";
        public const string NoUpgrade = "Esa mejora no existe.";
        public const string UpgradeTop = "Esa mejora ya está al máximo.";
        public const string UpgradeAtBase = "Las mejoras se hacen en la base.";
        public const string EmptySquad = "La escuadra está vacía.";
        public const string PickOwn = "Solo puedes elegir a tus soldados.";
        public const string NotChoosing = "Ahora no toca elegir etapa.";
        public const string NoNode = "Esa etapa no existe.";
        public const string NotNext = "Desde aquí no se llega a esa etapa.";
        public const string NotSeen = "Esa etapa aún no se ve.";
        public const string EndFromMap = "Solo se vuelve a la base desde el mapa.";
        public const string NotAtStop = "No estáis en una parada.";
        public const string NothingToAnswer = "Aquí no hay nada que responder.";
        public const string Answered = "Eso ya está respondido.";
        public const string NoOption = "Esa opción no existe.";
        public const string AnswerFirst = "Antes hay que elegir una opción.";
        public const string StaleAnswer = "Esa parada ya quedó atrás.";
        public const string NotTrade = "Aquí no hay Trueque.";
        public const string NoWare = "Eso ya no está en el Trueque.";
        public const string NoBeds = "No caben más soldados en el Bastión.";
        public const string Pending = "Ya hay una propuesta pendiente.";

        public static string Missing(int supplies) => $"Faltan {supplies} §.";
        public static string Seats(int seats) => $"Solo salen {seats} soldados.";
        public static string PerPlayer(int limit) => $"Máximo {limit} soldados por jugador.";
        public static string NotThere(Gear g) => $"{Capital(The(g))} no va en ese hueco.";
        public static string SwapNoRoom(Gear old, Gear moved) => $"{Capital(The(old))} no cabe donde estaba {The(moved)}.";
        public static string SplitCount(int count) => count == 2 ? "Solo se puede partir 1." : $"Se parten de 1 a {count - 1}.";
        public static string ModsFull(int slots) => $"Lleva ya {slots} mejoras: quita una antes.";
        public static string PackFull(string name) => $"No cabe en la mochila de {name}.";

        // ---------------------------------------------------------- proposals, log

        public const string Welcome = "Empieza la campaña. Equipad a la escuadra y salid.";
        public const string EndRunText = "volver a la base";
        public const string RunEnded = "La escuadra vuelve a la base, y todos se curan.";

        public static string UpgradeText(string id, int price) => $"hacer la mejora «{UpgradeNames[id]}» ({price} §)";
        public static string StartRunText(int chapter) => $"salir de expedición al capítulo {chapter}";
        public static string ChooseNodeText(RunNode n) => $"elegir la etapa «{FightName(n)}»";
        public static string AnswerText(StopState stop, int option) => stop.Survivor != null ? $"{(option == 0 ? "que se una" : "que no se una")} {stop.Survivor.Name}" : $"elegir «{EventOption(stop.Event ?? "", option)}»";
        public static string Upgraded(string id, int level) => $"Nueva mejora: {UpgradeNames[id]}, nivel {level}.";
        public static string RunStarted(int chapter) => $"Sale la escuadra: expedición al capítulo {chapter}.";
        public static string ChapterDone(int chapter) => $"Capítulo {chapter} superado. La escuadra vuelve a la base.";
        /// <summary>Someone joins (a Rescate, a Superviviente, a Suceso): with the squad, or at the base when it goes full (`inSquad` false).</summary>
        public static string SurvivorJoins(string name, bool inSquad = true) => inSquad ? $"{name} se une a la escuadra." : $"{name} se une al Bastión y espera en la base: la escuadra va completa.";
        public static string Recruit(string name) => $"{name} se une al Bastión como recluta: no quedaban soldados vivos.";
        public const string Wiped = "Cae toda la escuadra. Lo que llevaban se pierde.";
        public static string Reward(int supplies, int lost) => lost > 0 ? $"La recompensa: {supplies} §. No caben {lost} §, y se quedan allí." : $"La recompensa: {supplies} §.";
        /// <summary>A fight's name, for the room and the map's card: what the node is, and where.</summary>
        public static string FightName(RunNode n) => n.Site != null && Places.Names.TryGetValue(n.Site, out var site) ? $"{NodeNames[n.Kind]} · {site}" : NodeNames[n.Kind];
        /// <summary>The same with the chapter's boss by name (H6, Lore.StageName): «El Toro · Fábrica».</summary>
        public static string FightName(RunNode n, int chapter) => n.Site != null && Places.Names.TryGetValue(n.Site, out var site) ? $"{Lore.StageName(n.Kind, chapter)} · {site}" : Lore.StageName(n.Kind, chapter);

        // ------------------------------------------------------------------ names

        /// <summary>The stages' names, by NodeKinds: Lore's (Lore.StageNames; the boss's own, Lore.StageName with the chapter).</summary>
        public static readonly IReadOnlyDictionary<string, string> NodeNames = Lore.StageNames;

        /// <summary>The upgrades' names, by UpgradeIds: Lore's.</summary>
        public static readonly IReadOnlyDictionary<string, string> UpgradeNames = Lore.UpgradeNames;

        /// <summary>An event's title and its two options, by Content.RunEvents' id: none here any more (Lore.RunEvents has them).</summary>
        public static readonly IReadOnlyDictionary<string, (string Title, string Option0, string Option1)> Events = new Dictionary<string, (string Title, string Option0, string Option1)>
        {
        };

        /// <summary>What happened, by StopState.outcome.</summary>
        public static readonly IReadOnlyDictionary<string, string> Outcomes = new Dictionary<string, string>
        {
            ["refuge.rest"] = "La escuadra descansa, y todos se curan del todo.",
            ["survivor.yes"] = "Se une a la escuadra.",
            ["survivor.base"] = "Se une al Bastión y espera en la base: la escuadra va completa.",
            ["survivor.no"] = "Sigue su camino.",
        };

        public static string EventOption(string id, int option)
        {
            // H4: Lore's events (Lore.RunEvents), by their button.
            if (Lore.RunEvents.TryGetValue(id, out var told) && option >= 0 && option < told.Options.Count) return told.Options[option].Label;
            return Events.TryGetValue(id, out var e) ? (option == 0 ? e.Option0 : e.Option1) : "";
        }

        /// <summary>A gear with its article, for the refusals: «el fusil de asalto», «la medicina» (Lore.GearThe, one of it).</summary>
        public static string The(Gear g)
        {
            return Lore.GearThe(g.Kind, g.Type);
        }

        private static string Capital(string s)
        {
            return s.Length > 0 ? char.ToUpperInvariant(s[0]) + s.Substring(1) : s;
        }
    }
}
