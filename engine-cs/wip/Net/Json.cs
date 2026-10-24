using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bastion.Engine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Bastion.Net
{
    /// <summary>
    /// The JSON the network, the saves and the recorded games read and write (engine-cs README): the same as
    /// TypeScript's. Properties in camelCase (`Unit.Hp` is `hp`), but a dictionary's keys as they are (they are ids:
    /// `fragGrenade`); nulls left out on writing, and missing or null the same on reading. A union (an event by `t`, a
    /// command by `type`, a real-time order by `kind`) is read by its tag into its class; written, its tag goes with it.
    /// </summary>
    public static class Json
    {
        public static readonly JsonSerializerSettings Settings = Make();

        public static readonly JsonSerializer Serializer = JsonSerializer.Create(Settings);

        private static JsonSerializerSettings Make()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false, OverrideSpecifiedNames = true } },
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                Formatting = Formatting.None,
                // Untrusted input from a player: no type names, nothing deeper than any real state goes.
                TypeNameHandling = TypeNameHandling.None,
                MaxDepth = 64,
            };
            settings.Converters.Add(new TagConverter<GameEvent>("t", (e) => e.T));
            settings.Converters.Add(new TagConverter<Command>("type", (c) => c.Type));
            settings.Converters.Add(new TagConverter<RtOrder>("kind", (o) => o.Kind));
            settings.Converters.Add(new TagConverter<WipCampaignCommand>("type", (c) => c.Type));
            settings.Converters.Add(new TagConverter<ClientMessage>("t", (m) => m.T));
            settings.Converters.Add(new TagConverter<ServerMessage>("t", (m) => m.T));
            return settings;
        }

        public static string Write(object value)
        {
            return JsonConvert.SerializeObject(value, Settings);
        }

        public static T Read<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, Settings) ?? throw new JsonSerializationException("Vacío.");
        }
    }

    /// <summary>
    /// A union of classes told apart by a tag field (`t`, `type`, `kind`): on reading, the class whose tag it is. Its
    /// classes are found once, by reflection, among the concrete ones deriving from `TBase` in its assembly and this one;
    /// each has a constructor without arguments, and its tag is a fixed property. Writing is left to the serializer: the
    /// tag is a property like the others.
    /// </summary>
    public sealed class TagConverter<TBase> : JsonConverter where TBase : class
    {
        private readonly string field;
        private readonly Dictionary<string, Type> byTag;

        public TagConverter(string field, Func<TBase, string> tagOf)
        {
            this.field = field;
            byTag = new Dictionary<string, Type>();
            var assemblies = new[] { typeof(TBase).Assembly, typeof(TagConverter<>).Assembly }.Distinct();
            foreach (var type in assemblies.SelectMany((a) => a.GetTypes()))
            {
                if (type.IsAbstract || !typeof(TBase).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) == null) continue;
                var instance = (TBase)Activator.CreateInstance(type)!;
                byTag[tagOf(instance)] = type;
            }
        }

        /// <summary>The tags it knows: one per class.</summary>
        public IReadOnlyCollection<string> Tags => byTag.Keys;

        public override bool CanConvert(Type objectType) => objectType == typeof(TBase);

        public override bool CanWrite => false;

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var o = JObject.Load(reader);
            var tag = (string?)o[field];
            if (tag == null || !byTag.TryGetValue(tag, out var type)) throw new JsonSerializationException($"{typeof(TBase).Name}: «{field}» desconocido ({tag ?? "ninguno"}).");
            // The concrete class is not TBase: the serializer reads it plainly, without coming back here.
            return o.ToObject(type, serializer);
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }
    }
}
