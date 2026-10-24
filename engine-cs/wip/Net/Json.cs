using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// command or a campaign's by `type`, a proposal's action by `type`, a real-time order by `kind`) is read by its tag
    /// into its class; written, its tag goes with it.
    /// </summary>
    public static class Json
    {
        public static readonly JsonSerializerSettings Settings = Make(false);

        public static readonly JsonSerializer Serializer = JsonSerializer.Create(Settings);

        /// <summary>What a player sends (ReadFromPlayer): as Settings, but whole numbers where they go (WholeNumbers).</summary>
        private static readonly JsonSerializerSettings FromPlayer = Make(true);

        private static JsonSerializerSettings Make(bool wholeNumbers)
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
            settings.Converters.Add(new TagConverter<CampaignCommand>("type", (c) => c.Type));
            settings.Converters.Add(new TagConverter<ProposalAction>("type", (a) => a.Type));
            settings.Converters.Add(new TagConverter<ClientMessage>("t", (m) => m.T));
            settings.Converters.Add(new TagConverter<ServerMessage>("t", (m) => m.T));
            if (wholeNumbers) settings.Converters.Add(new WholeNumbers());
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

        /// <summary>
        /// A message from a player, untrusted (the room's): as Read, but a number that isn't whole where a whole one goes
        /// is refused, as protocol.ts's z.number().int() does, not rounded as Newtonsoft would.
        /// </summary>
        public static T ReadFromPlayer<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, FromPlayer) ?? throw new JsonSerializationException("Vacío.");
        }
    }

    /// <summary>
    /// int and long, and their nullables, only from a whole number (2 or 2.0, as JavaScript reads both). Read from the
    /// text, Newtonsoft refuses 1.5 for an int; but a union is read through its JObject (TagConverter), and from there it
    /// takes 1.5 for 2 and "2" for 2: every message and order is one. Only for what players send (Json.ReadFromPlayer):
    /// the states and the saves are the engine's own, and big.
    /// </summary>
    public sealed class WholeNumbers : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(int) || objectType == typeof(int?) || objectType == typeof(long) || objectType == typeof(long?);

        public override bool CanWrite => false;

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            var type = Nullable.GetUnderlyingType(objectType) ?? objectType;
            switch (reader.TokenType)
            {
                case JsonToken.Null when type != objectType:
                    return null;
                // Past an int (or a long): OverflowException, a message not to be read either.
                case JsonToken.Integer when reader.Value is long whole:
                    return type == typeof(int) ? (object)checked((int)whole) : whole;
                case JsonToken.Float:
                    var n = Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture);
                    if (Math.Floor(n) != n || double.IsInfinity(n)) break;
                    return type == typeof(int) ? (object)checked((int)n) : checked((long)n);
            }
            throw new JsonSerializationException($"Se esperaba un número entero ({reader.TokenType} {reader.Value}).");
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
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
