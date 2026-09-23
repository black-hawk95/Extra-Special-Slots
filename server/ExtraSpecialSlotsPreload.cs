using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace BlackHawk.ExtraSpecialSlots;

/// <summary>
/// Adds missing SpecialSlot4-6 to the compatible Pockets equipment
/// template without replacing or modifying SpecialSlot1-3.
///
/// Compatibility design:
/// - SVM: we do not touch the pocket definitions or SVM-owned settings.
/// - SpecialSlots: we do not replace its existing three slots or its filters.
///   BlackHawk-created slots deep-clone the current SpecialSlot3 definition,
///   including its current filters, without widening them.
/// - Re-running is idempotent: existing SpecialSlot5-6 are detected and kept. SpecialSlot4 is preserved for TSC/other mods.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Preload + 1)]
public sealed class ExtraSpecialSlotsPreload(TemplateTable templateTable, ISptLogger<ExtraSpecialSlotsPreload> logger) : IOnLoad
{
    private static readonly string[] CompatiblePocketsTemplateIds = ["627a4e6b255f7527fb05a0f6", "65e080be269cbd5c5005e529"];

    private static readonly string[] BlackHawkExtraSlotIds = ["SpecialSlot4", "SpecialSlot5", "SpecialSlot6"];

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        logger.Info("[ExtraSpecialSlots] OnLoadAsync entered.");
        cancellationToken.ThrowIfCancellationRequested();

        var config = ExtraSpecialSlotsConfig.Load(logger);
        var tscInstalled = IsTscInstalled();
        var specialSlotsInstalled = IsSpecialSlotsInstalled();
        var tscOwnsSlot4 = tscInstalled && config.TscOwnsSlot4;
        logger.Info($"[ExtraSpecialSlots] TSC detected = {tscInstalled}; SpecialSlot4 owner = {(tscOwnsSlot4 ? "TSC" : "ExtraSpecialSlots")}.");
        logger.Info($"[ExtraSpecialSlots] SpecialSlots (jbs4bmx) detected = {specialSlotsInstalled}. ExtraSpecialSlots does not widen filters; SpecialSlots may apply its own configured filters later in its PostDBModLoader stage.");

        var processed = 0;
        foreach (var pocketsId in CompatiblePocketsTemplateIds)
        {
            var equipmentTemplate = FindPocketsTemplate(templateTable.Items, pocketsId);
            if (equipmentTemplate is null)
            {
                logger.Warning($"[ExtraSpecialSlots] Pockets template {pocketsId} not found.");
                continue;
            }

            var slots = GetSlots(equipmentTemplate);
            if (slots is null)
            {
                logger.Error($"[ExtraSpecialSlots] Pockets {pocketsId}: Properties.Slots unavailable.");
                continue;
            }

            var existingIds = slots.Cast<object>().Select(GetSlotId).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            logger.Info($"[ExtraSpecialSlots] Pockets {pocketsId}: special slots = [{string.Join(", ", existingIds.Where(x => x.StartsWith("SpecialSlot", StringComparison.OrdinalIgnoreCase)).OrderBy(x => x))}].");

            var source = slots.Cast<object>().FirstOrDefault(x => string.Equals(GetSlotId(x), "SpecialSlot3", StringComparison.OrdinalIgnoreCase))
                ?? slots.Cast<object>().FirstOrDefault(x => string.Equals(GetSlotId(x), "SpecialSlot1", StringComparison.OrdinalIgnoreCase));
            if (source is null)
            {
                logger.Error($"[ExtraSpecialSlots] Pockets {pocketsId}: SpecialSlot1/3 not found; no changes made.");
                continue;
            }

            foreach (var id in BlackHawkExtraSlotIds)
            {
                if (id == "SpecialSlot4" && tscOwnsSlot4)
                {
                    logger.Info($"[ExtraSpecialSlots] Pockets {pocketsId}: Server config assigns SpecialSlot4 to TSC; leaving it untouched.");
                    continue;
                }
                if (existingIds.Contains(id))
                {
                    logger.Info($"[ExtraSpecialSlots] Pockets {pocketsId}: {id} already exists; preserving it.");
                    continue;
                }
                var clone = DeepCloneSlot(source, id);
                if (clone is null)
                {
                    logger.Error($"[ExtraSpecialSlots] Pockets {pocketsId}: failed to clone {id}.");
                    continue;
                }
                slots.Add(clone);
                existingIds.Add(id);
                logger.Info($"[ExtraSpecialSlots] Pockets {pocketsId}: added {id}.");
            }
            processed++;
        }

        logger.Info($"[ExtraSpecialSlots] Preload complete. Processed {processed} Pockets template(s). " +
                    (tscOwnsSlot4 ? "TSC owns SpecialSlot4; ExtraSpecialSlots owns missing SpecialSlot5-6." : "ExtraSpecialSlots owns missing SpecialSlot4-6."));
        return Task.CompletedTask;
    }

    private static object? FindPocketsTemplate(object itemsTable, string pocketsTemplateId)
    {
        if (itemsTable is not IEnumerable items)
            return null;

        foreach (var entry in items)
        {
            object? value = entry;
            object? key = null;

            if (entry is DictionaryEntry de)
            {
                key = de.Key;
                value = de.Value;
            }
            else if (entry is not null)
            {
                key = entry.GetType().GetProperty("Key")?.GetValue(entry);
                value = entry.GetType().GetProperty("Value")?.GetValue(entry) ?? entry;
            }

            if (value is null)
                continue;

            var valueId = GetMember(value, "Id", "ID", "id", "_id")?.ToString();
            if (!string.Equals(key?.ToString(), pocketsTemplateId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(valueId, pocketsTemplateId, StringComparison.OrdinalIgnoreCase))
                continue;

            return value;
        }

        return null;
    }

    private static IList? GetSlots(object template)
    {
        // EFT/SPT template objects have a _props/Props object containing Slots.
        var props = GetMember(template, "Properties", "Props", "_props", "properties", "_properties");
        if (props is null)
            return null;

        var slots = GetMember(props, "Slots", "_slots");
        return slots as IList;
    }

    private static string? GetSlotId(object slot)
    {
        var value = GetMember(slot, "Name", "name", "_name", "Id", "ID", "id", "_id", "SlotId", "slotId", "_slotId");
        return value?.ToString();
    }


    private static bool IsSpecialSlotsInstalled()
    {
        return AppDomain.CurrentDomain.GetAssemblies().Any(a =>
        {
            var name = a.GetName().Name ?? string.Empty;
            return name.Contains("SpecialSlots", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("ExtraSpecialSlots", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool IsTscInstalled()
    {
        return AppDomain.CurrentDomain.GetAssemblies().Any(a =>
        {
            var name = a.GetName().Name ?? string.Empty;
            return name.Contains("TacticalServicesControl", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Tylevo.TacticalServicesControl", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static object? DeepCloneSlot(object source, string newId)
    {
        var clone = ShallowClone(source);
        if (clone is null)
            return null;

        // Clone the nested Properties -> Filters -> Filter/ExcludedFilter graph.
        // v9 used MemberwiseClone only, which shared these mutable collections with
        // SpecialSlot3 and could accidentally change vanilla/TSC slot filters.
        var sourceProps = GetMember(source, "Properties", "Props", "_props", "properties", "_properties");
        if (sourceProps is not null)
        {
            var propsClone = ShallowClone(sourceProps);
            if (propsClone is not null)
            {
                var sourceFilters = GetMember(sourceProps, "Filters", "_filters") as IEnumerable;
                if (sourceFilters is not null)
                {
                    var filtersClone = CloneListLike(GetMember(sourceProps, "Filters", "_filters"));
                    if (filtersClone is IList targetFilters)
                    {
                        targetFilters.Clear();
                        foreach (var sourceFilter in sourceFilters)
                        {
                            if (sourceFilter is null) continue;
                            var filterClone = ShallowClone(sourceFilter);
                            if (filterClone is null) continue;

                            CloneCollectionMember(sourceFilter, filterClone, "Filter", "_filter");
                            CloneCollectionMember(sourceFilter, filterClone, "ExcludedFilter", "_excludedFilter");
                            targetFilters.Add(filterClone);
                        }
                        SetMemberObject(propsClone, filtersClone, "Filters", "_filters");
                    }
                }
                SetMemberObject(clone, propsClone, "Properties", "Props", "_props", "properties", "_properties");
            }
        }

        if (!SetMember(clone, newId, "Name", "name", "_name", "Id", "ID", "id", "_id", "SlotId", "slotId", "_slotId"))
            return null;

        // Keep the cloned vanilla/SVM filter graph exactly as-is.
        // Do not widen, clear, or otherwise rewrite allowed/excluded item filters.
        return clone;
    }

    private static object? ShallowClone(object source)
    {
        var memberwise = source.GetType().GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
        return memberwise?.Invoke(source, null);
    }

    private static void CloneCollectionMember(object source, object clone, params string[] names)
    {
        var original = GetMember(source, names);
        var copied = CloneListLike(original);
        if (copied is not null)
            SetMemberObject(clone, copied, names);
    }

    private static object? CloneListLike(object? source)
    {
        if (source is not IEnumerable enumerable)
            return null;
        try
        {
            var copy = Activator.CreateInstance(source.GetType());
            if (copy is IList list)
            {
                foreach (var value in enumerable) list.Add(value);
                return copy;
            }
        }
        catch { }
        return null;
    }

    private static object? GetMember(object obj, params string[] names)
    {
        var type = obj.GetType();

        foreach (var name in names)
        {
            var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop?.CanRead == true)
                return prop.GetValue(obj);

            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field is not null)
                return field.GetValue(obj);
        }

        return null;
    }

    private static bool SetMemberObject(object obj, object value, params string[] names)
    {
        var type = obj.GetType();
        foreach (var name in names)
        {
            var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop?.CanWrite == true && prop.PropertyType.IsInstanceOfType(value))
            {
                prop.SetValue(obj, value);
                return true;
            }
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field is not null && field.FieldType.IsInstanceOfType(value))
            {
                field.SetValue(obj, value);
                return true;
            }
        }
        return false;
    }

    private static bool SetMember(object obj, string value, params string[] names)
    {
        var type = obj.GetType();

        foreach (var name in names)
        {
            var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop?.CanWrite == true && prop.PropertyType == typeof(string))
            {
                prop.SetValue(obj, value);
                return true;
            }

            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field?.FieldType == typeof(string))
            {
                field.SetValue(obj, value);
                return true;
            }
        }

        // Last-resort shape-based replacement: find the string member whose
        // current value is an existing SpecialSlot ID.
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.FieldType == typeof(string) &&
                string.Equals(field.GetValue(obj)?.ToString(), "SpecialSlot3", StringComparison.OrdinalIgnoreCase))
            {
                field.SetValue(obj, value);
                return true;
            }
        }

        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (prop.PropertyType == typeof(string) && prop.CanWrite &&
                string.Equals(prop.GetValue(obj)?.ToString(), "SpecialSlot3", StringComparison.OrdinalIgnoreCase))
            {
                prop.SetValue(obj, value);
                return true;
            }
        }

        return false;
    }
}
