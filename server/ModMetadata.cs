using System.Collections.Generic;
using SemanticVersioning;
using SPTarkov.Server.Core.Models.Spt.Mod;

namespace BlackHawk.ExtraSpecialSlots;

public sealed record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.blackhawk.extraspecialslots";
    public string Name { get; init; } = "ExtraSpecialSlots";
    public string Author { get; init; } = "BLACK HAWK";
    public SemanticVersioning.Version Version { get; init; } = new("1.0.1");
    public SemanticVersioning.Range SptVersion { get; init; } = new(">=4.1.6 <4.1.7");
    public string License { get; init; } = "MIT";
    public string? Url { get; init; } = null;
    public List<string> Contributors { get; init; } = new();
    public List<string> Incompatibilities { get; init; } = new();
    public Dictionary<string, SemanticVersioning.Range> ModDependencies { get; init; } = new();
    public bool HasPrepatcher { get; init; } = false;
}
