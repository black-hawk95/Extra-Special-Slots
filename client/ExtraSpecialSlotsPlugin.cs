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
    public const string Version = "1.0.0";

    internal static ManualLogSource Log = null!;

    // Official Fika Headless BepInEx plugin GUID.
    // Fika Headless itself registers as: [BepInPlugin("com.fika.headless", ...)].
    private const string FikaHeadlessGuid = "com.fika.headless";

    private void Awake()
    {
        Log = Logger;

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
            Log.LogInfo($"{Name} {Version}: patched SearchableSlotView.CreateSlots.");
        }
        catch (Exception e)
        {
            Log.LogError($"{Name}: failed to install Special Slots UI patch: {e}");
        }
    }

    private static readonly FieldInfo? SpecialSlotPanelField =
        AccessTools.Field(typeof(SearchableSlotView), "_specSlotsPanel");

    private static bool HasSixSpecialSlots(Item item)
    {
        return item is CompoundItem compound &&
               compound.Slots.Count(slot =>
                   slot != null &&
                   slot.Name?.StartsWith("SpecialSlot", StringComparison.OrdinalIgnoreCase) == true) >= 6;
    }

    /// <summary>
    /// This is intentionally the same UI strategy used by Salco's Comfort Kit:
    /// patch SearchableSlotView.CreateSlots, get the private _specSlotsPanel,
    /// replace its HorizontalLayoutGroup with a 3-column GridLayoutGroup, and
    /// resize only the panel's VERTICAL axis before rebuilding the layout.
    ///
    /// Do not set the panel width and do not scan the scene hierarchy.
    /// </summary>
    private static void ArrangeAsGrid(RectTransform panel)
    {
        var grid = panel.GetComponent<GridLayoutGroup>();

        if (grid == null)
        {
            var horizontal = panel.GetComponent<HorizontalLayoutGroup>();
            var oldPadding = horizontal != null ? horizontal.padding : null;
            var oldSpacing = horizontal != null ? horizontal.spacing : 0f;

            if (horizontal != null)
                UnityEngine.Object.DestroyImmediate(horizontal);

            grid = panel.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = oldPadding != null
                ? new RectOffset(oldPadding.left, oldPadding.right, oldPadding.top, oldPadding.bottom)
                : new RectOffset();
            grid.spacing = new Vector2(oldSpacing, oldSpacing);
        }

        // Comfort Kit uses EFT's own 1x1 inventory-cell pixel size rather than
        // guessing from transforms. This keeps the SPEC slots identical to EFT.
        var cellPixels = ItemViewFactory.GetCellPixelSize(new IntVec2(1, 1));
        grid.cellSize = new Vector2(cellPixels.X, cellPixels.Y);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        var rows = Math.Max(1, (panel.childCount + 3 - 1) / 3);
        var height = grid.padding.vertical
                     + rows * grid.cellSize.y
                     + Math.Max(0, rows - 1) * grid.spacing.y;

        // Important: only increase vertical space. Changing width is what caused
        // the inventory to become horizontally scrollable in the earlier builds.
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        if (panel.parent is RectTransform parent)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
    }

    [HarmonyPatch(typeof(SearchableSlotView), "CreateSlots")]
    private static class CreateSlotsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(SearchableSlotView __instance, Item __0)
        {
            if (!HasSixSpecialSlots(__0))
                return;

            var panel = SpecialSlotPanelField?.GetValue(__instance) as RectTransform;
            if (panel == null)
            {
                Log.LogWarning("ExtraSpecialSlots: SearchableSlotView._specSlotsPanel was not found.");
                return;
            }

            ArrangeAsGrid(panel);
        }
    }
}
