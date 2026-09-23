using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.Common.Models.Logging;

namespace BlackHawk.ExtraSpecialSlots;

public sealed class ExtraSpecialSlotsConfig
{
    [JsonPropertyName("SpecialSlot4Owner")]
    public string SpecialSlot4Owner { get; set; } = "ExtraSpecialSlots";

    public bool TscOwnsSlot4 => string.Equals(SpecialSlot4Owner?.Trim(), "TSC", StringComparison.OrdinalIgnoreCase);

    public static ExtraSpecialSlotsConfig Load(ISptLogger<ExtraSpecialSlotsPreload> logger)
    {
        var config = new ExtraSpecialSlotsConfig();
        try
        {
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
            var path = Path.Combine(assemblyDir, "config.jsonc");
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            if (File.Exists(path))
                config = JsonSerializer.Deserialize<ExtraSpecialSlotsConfig>(File.ReadAllText(path), options) ?? config;
            else
                logger.Warning($"[ExtraSpecialSlots] config.jsonc not found at {path}; using defaults.");

            if (!string.Equals(config.SpecialSlot4Owner, "ExtraSpecialSlots", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(config.SpecialSlot4Owner, "TSC", StringComparison.OrdinalIgnoreCase))
            {
                logger.Warning($"[ExtraSpecialSlots] Invalid SpecialSlot4Owner '{config.SpecialSlot4Owner}'. Using ExtraSpecialSlots.");
                config.SpecialSlot4Owner = "ExtraSpecialSlots";
            }
        }
        catch (Exception e)
        {
            logger.Error($"[ExtraSpecialSlots] Could not load config.jsonc; using defaults. {e.Message}");
        }
        return config;
    }
}
