using System.Collections.Generic;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ------------------------------------------------------------------- night

        /// <summary>Night missions (docs/ALTURA-Y-NOCHE.md): the tag, the briefing's last line, the debrief's, and the rules with their figures (playtest 20/10 #33).</summary>
        public static class NightText
        {
            public const string Label = "De noche";
            public static readonly string Briefing = $"De noche: a oscuras, tus soldados ven {Sight.Night} casillas en vez de {Sight.Day}. Hay más zombis, y el botín vale la mitad más.";
            public const string Report = "Misión de noche: el botín y la recompensa valen la mitad más.";
            /// <summary>The help and the first night's card, a line each.</summary>
            public static readonly IReadOnlyList<string> Rules = new[]
            {
                $"De noche, tus soldados ven {Sight.Night} casillas a oscuras, en vez de {Sight.Day}.",
                $"Una farola alumbra {Content.Night.StreetLight} casillas a su alrededor, y un foco, {Content.Night.Floodlight}. Lo que está iluminado se ve desde {Sight.Day} casillas, como de día.",
                $"Hasta que te descubren, los enemigos notan a un soldado a oscuras a la mitad de distancia: un zombi, a {Sight.ZombieNoticeDark} casillas en vez de {Sight.ZombieNotice}.",
                "Cuando ya te han descubierto, los zombis ven de noche igual que de día.",
            };
        }

        /// <summary>A lit tile under the cursor, at night: «Iluminado: aquí se ve desde 18 casillas, como de día.» (`turns`: a light that goes out; none for lamps and floodlights).</summary>
        public static string LitNote(int? turns = null)
        {
            var lit = $"Iluminado: aquí se ve desde {Sight.Day} casillas, como de día.";
            return turns.GetValueOrDefault() != 0 ? $"{lit} Se apaga en {turns} {(turns == 1 ? "turno" : "turnos")}." : lit;
        }
    }
}
