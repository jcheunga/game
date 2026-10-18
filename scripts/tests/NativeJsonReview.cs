using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Godot;

/// <summary>
/// Checks that the game's source-generated JSON (scripts/core/GameJson.cs) writes and reads exactly
/// what reflection-based System.Text.Json did with the options each caller used before, so moving
/// to source generation for the iOS NativeAOT build changed no save file, data file or server
/// request. Every type a context lists or reaches is filled with sample values and written both
/// ways, then read back, also with its property names in another case where the context reads any
/// case. The data files and a late-game save are read and written both ways too. Desktop only,
/// since reflection is the reference:
///   godot --headless --path . res://scenes/tests/NativeJsonReview.tscn -- --save-suffix=json-review-1
/// </summary>
public partial class NativeJsonReview : Node
{
    private int _failures;
    private int _sample;

    public override void _Ready() => Callable.From(Run).CallDeferred();
    private void Check(bool ok, string description) { GD.Print($"JSON_CHECK: {(ok ? "PASS" : "FAIL")} {description}"); if (!ok) _failures++; }

    private static JsonSerializerOptions Reflection(JsonSerializerOptions options)
    {
        options.TypeInfoResolver = new DefaultJsonTypeInfoResolver();
        return options;
    }

    private void Run()
    {
        try
        {
            if (!OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--save-suffix=json-review-"))) throw new Exception("Use an isolated json-review save.");
            // The options each context replaced (see the callers' history in git).
            var data = Reflection(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var save = Reflection(new JsonSerializerOptions { WriteIndented = true });
            var compact = Reflection(new JsonSerializerOptions());
            var server = Reflection(new JsonSerializerOptions { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true });
            Compare("DataJson", DataJson.Default, data, anyCase: true);
            Compare("SaveJson", SaveJson.Default, save, anyCase: false);
            Compare("CompactJson", CompactJson.Default, compact, anyCase: false);
            Compare("ServerJson", ServerJson.Default, server, anyCase: true);
            DataFilesReadTheSame(data);
            LateGameSaveIsTheSame(save, compact);
        }
        catch (Exception e) { Check(false, $"Driver error: {e}"); }
        GD.Print($"JSON_REVIEW_RESULT: {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    /// <summary>Writes and reads a filled sample of every type the context knows, both ways.</summary>
    private void Compare(string name, System.Text.Json.Serialization.JsonSerializerContext context, JsonSerializerOptions reflection, bool anyCase)
    {
        var infos = context.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => typeof(JsonTypeInfo).IsAssignableFrom(property.PropertyType))
            .Select(property => (JsonTypeInfo)property.GetValue(context)).ToList();
        var objects = infos.Count(info => info.Kind == JsonTypeInfoKind.Object);
        var written = new List<string>();
        var read = new List<string>();
        var otherCase = new List<string>();
        foreach (var info in infos)
        {
            var sample = Sample(info.Type, 0);
            var expected = JsonSerializer.Serialize(sample, info.Type, reflection);
            var actual = JsonSerializer.Serialize(sample, info);
            if (actual != expected) written.Add($"{Name(info.Type)}: {Clip(expected)} became {Clip(actual)}");
            if (Reread(expected, info, reflection) != expected) read.Add(Name(info.Type));
            if (!anyCase || info.Kind != JsonTypeInfoKind.Object) continue;
            var renamed = Recase(expected);
            var byReflection = JsonSerializer.Serialize(JsonSerializer.Deserialize(renamed, info.Type, reflection), info.Type, reflection);
            if (Reread(renamed, info, reflection) != byReflection) otherCase.Add(Name(info.Type));
        }
        Check(written.Count == 0, $"{name}: its {infos.Count} types ({objects} objects) write what reflection wrote ({string.Join("; ", written)})");
        Check(read.Count == 0, $"{name}: and read it back the same ({string.Join(", ", read)})");
        if (anyCase) Check(otherCase.Count == 0, $"{name}: property names in another case read as reflection read them ({string.Join(", ", otherCase)})");
    }

    /// <summary>Reads json with the generated type info and writes it again with reflection.</summary>
    private static string Reread(string json, JsonTypeInfo info, JsonSerializerOptions reflection) =>
        JsonSerializer.Serialize(JsonSerializer.Deserialize(json, info), info.Type, reflection);

    private void DataFilesReadTheSame(JsonSerializerOptions reflection)
    {
        var files = new List<(string Path, JsonTypeInfo Info)>
        {
            ("res://data/units.json", DataJson.Default.UnitCollection),
            ("res://data/spells.json", DataJson.Default.SpellCollection),
            ("res://data/stages.json", DataJson.Default.StageCollection),
            ("res://data/combat_config.json", DataJson.Default.CombatCollection),
            ("res://data/equipment.json", DataJson.Default.EquipmentCollection),
            ("res://data/shop_products.json", DataJson.Default.ListShopProduct),
        };
        files.AddRange(DirAccess.GetFilesAt("res://data/waves").Where(file => file.EndsWith(".json")).Order()
            .Select(file => ($"res://data/waves/{file}", (JsonTypeInfo)DataJson.Default.WaveVersion)));
        foreach (var (path, info) in files)
        {
            var text = Godot.FileAccess.GetFileAsString(path);
            var expected = JsonSerializer.Serialize(JsonSerializer.Deserialize(text, info.Type, reflection), info.Type, reflection);
            var actual = Reread(text, info, reflection);
            Check(actual == expected && expected.Length > 100, $"{path} reads the same ({expected.Length / 1024.0:0.0} KB as JSON)");
        }
    }

    /// <summary>A real save: late-game progress, as the save file and the account backup write it.</summary>
    private void LateGameSaveIsTheSame(JsonSerializerOptions save, JsonSerializerOptions compact)
    {
        var state = GameState.Instance;
        state.ResetProgress();
        SaveRoundTripReview.FillLateGame(state);
        var data = state.BuildSaveData();
        var file = JsonSerializer.Serialize(data, SaveJson.Default.GameSaveData);
        Check(file == JsonSerializer.Serialize(data, save) && file.Length > 10_000, $"A late-game save file is written as before ({file.Length / 1024.0:0.0} KB)");
        Check(JsonSerializer.Serialize(JsonSerializer.Deserialize(file, SaveJson.Default.GameSaveData), save) == file, "and read back the same");
        Check(JsonSerializer.Serialize(data, CompactJson.Default.GameSaveData) == JsonSerializer.Serialize(data, compact), "The account backup is written as before");
        SaveSystem.Instance.Save(data);
        Check(SaveSystem.Instance.TryLoad(out var loaded) && JsonSerializer.Serialize(loaded, save) == file, "SaveSystem writes and loads it unchanged");
        state.ResetProgress();
    }

    /// <summary>A value of the type with every settable property filled, distinct where it can be.</summary>
    private object Sample(Type type, int depth)
    {
        _sample++;
        if (type == typeof(string)) return $"s{_sample}";
        if (type == typeof(bool)) return true;
        if (type == typeof(int)) return _sample;
        if (type == typeof(long)) return 10_000_000_000L + _sample;
        if (type == typeof(float)) return _sample + 0.5f;
        if (type == typeof(double)) return _sample + 0.25;
        if (type.IsEnum) { var values = Enum.GetValues(type); return values.GetValue(values.Length - 1); }
        if (Nullable.GetUnderlyingType(type) is { } inner) return Sample(inner, depth);
        if (type.IsArray)
        {
            var array = Array.CreateInstance(type.GetElementType()!, depth > 4 ? 0 : 1);
            if (array.Length > 0) array.SetValue(Sample(type.GetElementType()!, depth + 1), 0);
            return array;
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var list = (IList)Activator.CreateInstance(type)!;
            if (depth <= 4) list.Add(Sample(type.GetGenericArguments()[0], depth + 1));
            return list;
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var dictionary = (IDictionary)Activator.CreateInstance(type)!;
            var arguments = type.GetGenericArguments();
            if (depth <= 4) dictionary[Sample(arguments[0], depth + 1)] = Sample(arguments[1], depth + 1);
            return dictionary;
        }
        if (type.IsValueType || type.IsAbstract || type.GetConstructor(Type.EmptyTypes) == null)
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        var value = Activator.CreateInstance(type)!;
        if (depth > 4) return value;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (property.SetMethod is { IsPublic: true } && property.GetIndexParameters().Length == 0)
                property.SetValue(value, Sample(property.PropertyType, depth + 1));
        return value;
    }

    /// <summary>The same JSON with every property name in upper case.</summary>
    private static string Recase(string json)
    {
        static JsonNode Upper(JsonNode node) => node switch
        {
            JsonObject obj => new JsonObject(obj.Select(pair => KeyValuePair.Create(pair.Key.ToUpperInvariant(), pair.Value == null ? null : Upper(pair.Value)))),
            JsonArray array => new JsonArray(array.Select(item => item == null ? null : Upper(item)).ToArray()),
            _ => node.DeepClone(),
        };
        return Upper(JsonNode.Parse(json)!).ToJsonString();
    }

    private static string Name(Type type) => type.IsGenericType
        ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(", ", type.GetGenericArguments().Select(Name))}>"
        : type.DeclaringType != null ? $"{type.DeclaringType.Name}.{type.Name}" : type.Name;
    private static string Clip(string text) => text.Length > 160 ? text[..160] + "…" : text;
}
