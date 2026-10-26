using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bastion.Client.Audio.Mix;

// Unity's Mixer on a script of guiones/ (mezcla.mjs), into a raw stereo float file: Mezcla <guion.json> <sonido> <out>.
var json = JsonDocument.Parse(File.ReadAllText(args[0])).RootElement;
var dir = args[1];
var samples = new Dictionary<string, Sample?>();
var script = new Script(json.GetProperty("rate").GetInt32(), json.GetProperty("seconds").GetDouble(), json.GetProperty("seed").GetUInt32(),
    json.TryGetProperty("bloque", out var b) ? b.GetInt32() : 128, json.GetProperty("events").EnumerateArray().Select(Event), Load);
var mixer = new Mixer(script.Rate, script.Block, script.Random, Script.Volumes, false);
var output = new float[script.Frames * 2];
var block = new float[script.Block * 2];
for (long f = 0; f < script.Frames; f += script.Block)
{
    script.Run(mixer, f, script.Block);
    mixer.Render(block, 2);
    Array.Copy(block, 0, output, f * 2, Math.Min(block.Length, (script.Frames - f) * 2));
}
var bytes = new byte[output.Length * 4];
Buffer.BlockCopy(output, 0, bytes, 0, bytes.Length);
File.WriteAllBytes(args[2], bytes);

static Script.Event Event(JsonElement e)
{
    var ev = new Script.Event();
    foreach (var p in e.EnumerateObject())
    {
        switch (p.Name)
        {
            case "at": ev.At = p.Value.GetDouble(); break;
            case "do": ev.Do = p.Value.GetString()!; break;
            case "id": ev.Id = p.Value.GetString(); break;
            case "file": ev.File = p.Value.GetString(); break;
            case "bus": ev.Bus = p.Value.GetString(); break;
            case "word": ev.Word = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : null; break;
            case "patch":
                foreach (var v in p.Value.EnumerateObject()) ev.Patch[v.Name] = v.Value.GetSingle();
                break;
            default:
                if (p.Value.ValueKind == JsonValueKind.Number) ev.Numbers[p.Name] = p.Value.GetDouble();
                break;
        }
    }
    return ev;
}

Sample? Load(string file)
{
    if (!samples.TryGetValue(file, out var s)) samples[file] = s = Sample.FromWav(File.ReadAllBytes(Path.Combine(dir, file)));
    return s;
}
