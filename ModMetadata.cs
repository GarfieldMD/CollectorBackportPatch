using SPTarkov.Server.Core.Models.Spt.Mod;
using Range = SemanticVersioning.Range;

namespace CollectorBackportPatch;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.yourname.collectorbackportpatch";
    public override string Name { get; init; } = "CollectorBackportPatch";
    public override string Author { get; init; } = "you";

    public override List<string>? Contributors { get; init; } = null;

    public override SemanticVersioning.Version Version { get; init; } = new("1.0.0");
    public override Range SptVersion { get; init; } = new("~4.0.13");

    public override List<string>? Incompatibilities { get; init; } = null;
    public override Dictionary<string, Range>? ModDependencies { get; init; } = null;

    public override string? Url { get; init; } = null;

    public override bool? IsBundleMod { get; init; } = false;

    public override string License { get; init; } = "MIT";
}