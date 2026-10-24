using System.Collections.Generic;
using System.IO;
using Bastion.Engine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The sample campaign the 0.10.1's references were shot on (engine-cs/golden/campaign/muestra.json, the same as
    /// .dev-snapshots/ui/estrategia-0101/partida-muestra.json's: version 25, day 20, two players), read into the C#
    /// CampaignState with the network's JSON and the campaign's two unions (as CampaignJsonTests reads it). Its seats as
    /// the 0.10.1 had them: Ana in slot 0, Arturo in slot 1, and the shots taken as Arturo with Ana away.
    /// </summary>
    public static class SampleCampaign
    {
        static JsonSerializer serializer;

        public static string Path => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../engine-cs/golden/campaign/muestra.json"));

        public static CampaignState Load()
        {
            var json = JObject.Parse(File.ReadAllText(Path));
            return json["campaign"].ToObject<CampaignState>(Serializer());
        }

        public static List<SeatInfo> Seats(bool anaHere = false) => new List<SeatInfo>
        {
            new SeatInfo { Slot = 0, Name = "Ana", Color = Content.PlayerColors[0], Connected = anaHere },
            new SeatInfo { Slot = 1, Name = "Arturo", Color = Content.PlayerColors[1], Connected = true },
        };

        static JsonSerializer Serializer()
        {
            if (serializer != null) return serializer;
            var net = Bastion.Net.Json.Settings;
            var settings = new JsonSerializerSettings
            {
                ContractResolver = net.ContractResolver,
                NullValueHandling = net.NullValueHandling,
                MissingMemberHandling = net.MissingMemberHandling,
                MaxDepth = net.MaxDepth,
            };
            foreach (var c in net.Converters) settings.Converters.Add(c);
            settings.Converters.Add(new Bastion.Net.TagConverter<ProposalAction>("type", a => a.Type));
            settings.Converters.Add(new Bastion.Net.TagConverter<CampaignCommand>("type", c => c.Type));
            return serializer = JsonSerializer.Create(settings);
        }
    }
}
