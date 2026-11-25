using System.Text.Json;
using System.Text.Json.Serialization;
using RoseTrap.Core.Roblox;
using RoseTrap.Core.Settings;

namespace RoseTrap.Core;

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(ClientVersion))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class CoreJson : JsonSerializerContext;
