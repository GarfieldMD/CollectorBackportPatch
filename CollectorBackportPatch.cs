using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Servers;
using System.Security.Cryptography;

namespace CollectorBackportPatch;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class CollectorBackportPatch(
    ISptLogger<CollectorBackportPatch> logger,
    DatabaseServer databaseServer,
    DatabaseService databaseService
) : IOnLoad
{
    private const string CollectorQuestId = "5c51aac186f77432ea65c552";
    private const string StreamerCaseTpl = "66bc98a01a47be227a5e956e";

    private static readonly string[] V10BackportItems =
    [
        "6937f02dfd6488bb27024839", // ushanka
        "6937edb912d456a817083e82", // mazoni dumbbell
        "6937ecf8628ee476240c07cb", // tigz splint
        "69398e94ca94fd2877039504", // nut sack
    ];

    public Task OnLoad()
    {
        var tables = databaseServer.GetTables();

        // 1) items
        var items = tables.Templates.Items;

        if (!items.TryGetValue(StreamerCaseTpl, out var sic))
        {
            logger.Error($"[CollectorBackportPatch] Streamer case tpl not found: {StreamerCaseTpl}");
            return Task.CompletedTask;
        }

        // Increase streamer case internal size (+1 row)
        try
        {
            var sicGrid = sic.Properties.Grids.First().Properties;
            sicGrid.CellsH += 1;
        }
        catch (Exception e)
        {
            logger.Error($"[CollectorBackportPatch] Failed to resize streamer case grid: {e.Message}");
        }

        // Add items into streamer case filter
        try
        {
            var filter = sic.Properties.Grids.First().Properties.Filters.First().Filter;
            foreach (var mid in V10BackportItems)
            {
                if (!filter.Contains(mid))
                {
                    filter.Add(mid);
                }
            }

            // Colorize allowed items
            foreach (var mid in filter)
            {
                if (items.TryGetValue(mid, out var it))
                {
                    it.Properties.BackgroundColor = "violet";
                }
            }
        }
        catch (Exception e)
        {
            logger.Error($"[CollectorBackportPatch] Failed to patch streamer case filter: {e.Message}");
        }

        // 2) quests
        var quests = tables.Templates.Quests;
        if (!quests.TryGetValue(CollectorQuestId, out var q))
        {
            logger.Error($"[CollectorBackportPatch] Collector quest not found: {CollectorQuestId}");
            return Task.CompletedTask;
        }

        // Find last condition index
        int lastIndex = 0;
        foreach (var c in q.Conditions.AvailableForFinish)
        {
            if (c.Index.HasValue)
            {
                lastIndex = Math.Max(lastIndex, c.Index.Value);
            }
        }

        // We will patch locales lazily via transformers (SPT does this to save memory) :contentReference[oaicite:4]{index=4}
        var locPatchesEn = new Dictionary<string, string>();
        var locPatchesRu = new Dictionary<string, string>();

        // Grab locale db lazy objects
        databaseService.GetLocales().Global.TryGetValue("en", out var locEnLazy);
        databaseService.GetLocales().Global.TryGetValue("ru", out var locRuLazy);

        foreach (var mid in V10BackportItems)
        {
            var subtaskId = NewMongoId24();

            // Add finish condition (HandoverItem, FIR)
            q.Conditions.AvailableForFinish.Add(new()
            {
                ConditionType = "HandoverItem",
                DogtagLevel = 0,
                DynamicLocale = false,
                GlobalQuestCounterId = "",
                Id = subtaskId,
                Index = ++lastIndex,
                IsEncoded = false,
                MaxDurability = 100,
                MinDurability = 0,
                OnlyFoundInRaid = true,
                ParentId = "",
                Target = new([mid], null),
                Value = 1,
                VisibilityConditions = [],
            });

            // Locale: show item name for this subtaskId
            // item name keys are like "{tpl} Name" (same as you were doing)
            if (locEnLazy != null)
            {
                var enDict = locEnLazy.Value;
                if (enDict.TryGetValue($"{mid} Name", out var enName))
                {
                    locPatchesEn[subtaskId] = enName;
                }
            }

            if (locRuLazy != null)
            {
                var ruDict = locRuLazy.Value;
                if (ruDict.TryGetValue($"{mid} Name", out var ruName))
                {
                    locPatchesRu[subtaskId] = ruName;
                }
            }
        }

        // Apply locale patches via transformers
        if (locEnLazy != null && locPatchesEn.Count > 0)
        {
            locEnLazy.AddTransformer(ld =>
            {
                foreach (var kvp in locPatchesEn)
                {
                    ld[kvp.Key] = kvp.Value;
                }
                return ld;
            });
        }

        if (locRuLazy != null && locPatchesRu.Count > 0)
        {
            locRuLazy.AddTransformer(ld =>
            {
                foreach (var kvp in locPatchesRu)
                {
                    ld[kvp.Key] = kvp.Value;
                }
                return ld;
            });
        }

        logger.LogWithColor("[CollectorBackportPatch] Patched Collector + Streamer case", LogTextColor.Green);
        return Task.CompletedTask;
    }

    // Create a 24-char hex string (MongoId-like) without extra deps
    private static string NewMongoId24()
    {
        Span<byte> bytes = stackalloc byte[12];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant(); // 24 hex chars
    }
}