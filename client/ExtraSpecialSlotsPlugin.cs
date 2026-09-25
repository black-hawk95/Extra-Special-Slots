using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Bootstrap;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHawk.ExtraSpecialSlots.Client;

[BepInPlugin(Guid, Name, Version)]
[BepInDependency("com.fika.headless", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class ExtraSpecialSlotsPlugin : BaseUnityPlugin
{
    public const string Guid = "blackhawk.extraspecialslots.client";
    public const string Name = "ExtraSpecialSlots";
    public const string Version = "1.1.1";

    internal static ManualLogSource Log = null!;
    private static ExtraSpecialSlotsPlugin? _instance;

    // Official Fika Headless BepInEx plugin GUID.
    // Fika Headless itself registers as: [BepInPlugin("com.fika.headless", ...)].
    private const string FikaHeadlessGuid = "com.fika.headless";

    private void Awake()
    {
        Log = Logger;
        _instance = this;

        // Headless safety guard: the client DLL may be present by mistake on a
        // Fika Headless install. Detect Fika Headless through BepInEx's plugin
        // registry and become completely inert before installing any UI patch.
        if (Chainloader.PluginInfos.ContainsKey(FikaHeadlessGuid))
        {
            Log.LogInfo($"{Name} {Version}: Fika Headless detected; client/UI plugin disabled. Server-side ExtraSpecialSlots remains unaffected.");
            enabled = false;
            return;
        }

        try
        {
            var harmony = new Harmony(Guid);
            harmony.CreateClassProcessor(typeof(CreateSlotsPatch)).Patch();
            Log.LogInfo($"{Name} {Version}: native-slot layout enabled.");
        }
        catch (Exception e)
        {
            Log.LogError($"{Name}: failed to install Special Slots UI patch: {e}");
        }
    }

    private static readonly FieldInfo? SpecialSlotPanelField =
        AccessTools.Field(typeof(SearchableSlotView), "_specSlotsPanel");

    private static readonly FieldInfo? SpecialSlotTemplateField =
        AccessTools.Field(typeof(SearchableSlotView), "_specSlotTemplate");

    private static bool HasSixSpecialSlots(Item item)
    {
        return item is CompoundItem compound &&
               compound.Slots.Count(slot =>
                   slot != null &&
                   slot.Name?.StartsWith("SpecialSlot", StringComparison.OrdinalIgnoreCase) == true) >= 6;
    }

    // Keep the native layout component alive. Destroying/replacing it required
    // delayed sizing in 1.1.0; this layout supplies bounds synchronously instead.
    private static void ArrangeAsGrid(RectTransform panel, SlotView template, Slot[] slots)
    {
        if (_instance == null || !_instance.isActiveAndEnabled) return;
        var layout = panel.GetComponent<SpecialSlotsLayout>();
        if (layout == null)
        {
            var horizontal = panel.GetComponent<HorizontalLayoutGroup>();
            var padding = horizontal != null ? horizontal.padding : new RectOffset();
            var spacing = horizontal != null ? horizontal.spacing : 0f;
            if (horizontal != null) horizontal.enabled = false;
            layout = panel.gameObject.AddComponent<SpecialSlotsLayout>();
            layout.Initialize(padding, spacing);
        }
        layout.Bind(template, slots);
        layout.Apply();
        layout.LogBindings();
        LayoutRebuilder.MarkLayoutForRebuild(panel);
    }

    [HarmonyPatch(typeof(SearchableSlotView), "CreateSlots")]
    private static class CreateSlotsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(SearchableSlotView __instance, Item __0)
        {
            try
            {
                if (!HasSixSpecialSlots(__0))
                    return;

                var panel = SpecialSlotPanelField?.GetValue(__instance) as RectTransform;
                if (panel == null)
                {
                    Log.LogWarning("ExtraSpecialSlots: SearchableSlotView._specSlotsPanel was not found.");
                    return;
                }

                var template = SpecialSlotTemplateField?.GetValue(__instance) as SlotView;
                if (template == null)
                {
                    Log.LogWarning("ExtraSpecialSlots: special slot template unavailable; leaving native layout intact.");
                    return;
                }
                var slots = ((CompoundItem)__0).Slots.Where(slot => slot != null &&
                    slot.Name?.StartsWith("SpecialSlot", StringComparison.OrdinalIgnoreCase) == true).ToArray();
                ArrangeAsGrid(panel, template, slots);
            }
            catch (Exception e)
            {
                // An optional UI layout must never abort EFT screen initialization.
                Log.LogError($"ExtraSpecialSlots: failed to schedule special slots layout: {e}");
            }
        }
    }
}

