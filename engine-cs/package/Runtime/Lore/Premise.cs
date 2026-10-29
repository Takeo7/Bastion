using System;
using System.Collections.Generic;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ----------------------------------------------------------------- premise

        /// <summary>The card before the first mission: who the players are and why they fight. One paragraph per line; the key words in **bold** (playtest 20/10 #15, UI renders it).</summary>
        public static class Premise
        {
            public const string Title = "El claro";
            public static readonly IReadOnlyList<string> Lines = new[]
            {
                "La infestación parece haber aflojado este invierno. Las nevadas han dejado a muchos caminantes sepultados y, **aunque salir sigue siendo peligroso**, quizá sea el momento de volver a intentarlo.",
                "Atalaya, la vieja estación militar, **cayó en otoño**. Los que salimos con vida hemos parado en un claro del pinar, sobre Metsovo, con una autocaravana y lo puesto. Necesitamos **comida, medicinas y suministros** si queremos llegar todos vivos al final de la temporada.",
                "Toca volver a sacar **La Lata**, ese trasto forrado con chapas que nos sacó de Atalaya, y salir a buscar lo que nos hace falta.",
            };
        }

        /// <summary>Where an ordinary operation goes, by kind of map: it gives the mission its name («La gasolinera de Sappada»).</summary>
        private static readonly IReadOnlyDictionary<string, string[]> MissionPlaces = new Dictionary<string, string[]>
        {
            ["city"] = new[] { "El mercado", "La comisaría", "El colegio", "El hospital", "La estación de autobuses", "El centro comercial", "Los juzgados", "La biblioteca" },
            ["wilds"] = new[] { "La granja", "El camping", "El pinar", "La ermita", "La casa de campo" },
            ["facility"] = new[] { "La conservera", "La nave de transportes", "La planta química", "El almacén frigorífico", "La fábrica de piensos" },
        };

        /// <summary>What a sabotage's objective is called on the field, by its alarm (playtest 2026-10-15, #14; Lore's to change).</summary>
        public static readonly IReadOnlyDictionary<string, string> AlarmNames = new Dictionary<string, string>
        {
            ["police"] = "Coche patrulla",
            ["ambulance"] = "Ambulancia",
            ["factory"] = "Alarma de la nave",
        };

        /// <summary>The scenarios' places (playtest 2026-10-14, #25): a forest's service area, a town's industrial estate.</summary>
        private static readonly IReadOnlyDictionary<string, string[]> SitePlaces = new Dictionary<string, string[]>
        {
            ["roadside"] = new[] { "El área de servicio", "El motel", "El aserradero", "La gasolinera" },
            ["industrial"] = new[] { "El polígono", "Los muelles de carga", "Las naves", "El depósito de contenedores" },
        };

        /// <summary>An ordinary operation's name: a place in the zone, «El colegio de Tarbes», «La granja del Pantano Gris».</summary>
        public static string MissionName(string biome, string zone, long seed, string? site = null)
        {
            var places = site != null ? SitePlaces[site] : MissionPlaces[biome];
            return $"{places[(int)(Math.Abs(seed) % places.Length)]} {OfZone(zone)}";
        }
    }
}
