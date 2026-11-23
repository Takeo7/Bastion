using System;
using System.Collections.Generic;

namespace Bastion.Engine
{
    public static partial class Lore
    {
        // ----------------------------------------------------------------- premise

        /// <summary>
        /// The card before the first mission: who the players are and why they go out. One paragraph per line; the key words
        /// in **bold** (playtest 20/10 #15, UI renders it). Two short paragraphs, true to the runs (Producto's review, 23/11
        /// #13: it covered the soldiers and was the first screen's longest text): a knife each and nothing else
        /// (CreateRunsCampaign), and La Lata a motorbike for two (Runs.Seats).
        /// </summary>
        public static class Premise
        {
            public const string Title = "El claro";
            public static readonly IReadOnlyList<string> Lines = new[]
            {
                "Atalaya, la vieja estación militar, **cayó en otoño**. Los que salimos con vida hemos parado en un claro del pinar, sobre Metsovo, con **un cuchillo cada uno** y nada más.",
                "Este invierno, la nieve ha enterrado a muchos muertos: **es el momento de salir** a por armas, medicinas y suministros. Por ahora, **La Lata** es una moto, y caben dos.",
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
