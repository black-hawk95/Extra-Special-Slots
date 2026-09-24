using System;
using System.Collections;
using System.Collections.Generic;
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
    public const string Version = "1.1.0";

    internal static ManualLogSource Log = null!;
    private static ExtraSpecialSlotsPlugin? _instance;
    private static readonly HashSet<int> PendingPanels = new();

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
    /// Tarkov also constructs this panel while opening the in-raid transit
    /// transfer screen. DestroyImmediate is forbidden during that UI callback:
    /// Unity leaves the horizontal group in place, rejects the grid group, and
    /// the resulting exception prevents the screen (and its controls) from
    /// finishing initialization. Remove the old group normally and install
    /// the grid on the following frame instead.
    /// </summary>
    private static void ArrangeAsGrid(RectTransform panel)
    {
        if (_instance == null || !_instance.isActiveAndEnabled)
            return;

        var panelId = panel.GetInstanceID();
        if (!PendingPanels.Add(panelId))
            return;

        try
        {
            var grid = panel.GetComponent<GridLayoutGroup>();
            var horizontal = panel.GetComponent<HorizontalLayoutGroup>();
            var sourcePadding = grid != null ? grid.padding : horizontal?.padding;
            var padding = sourcePadding != null
                ? new RectOffset(sourcePadding.left, sourcePadding.right, sourcePadding.top, sourcePadding.bottom)
                : new RectOffset();
            var spacing = grid != null ? grid.spacing.x : horizontal?.spacing ?? 0f;

            if (horizontal != null)
            {
                horizontal.enabled = false;
                UnityEngine.Object.Destroy(horizontal);
            }

            _instance.StartCoroutine(InstallGridNextFrame(panel, panelId, padding, spacing));
        }
        catch
        {
            PendingPanels.Remove(panelId);
            throw;
        }
    }

    private static IEnumerator InstallGridNextFrame(RectTransform panel, int panelId, RectOffset padding, float spacing)
    {
        // Destroy(Component) completes at the end of the frame. Adding another
        // LayoutGroup before then fails even if the old group is disabled.
        yield return null;

        try
        {
            if (panel != null)
            {
                if (panel.GetComponent<HorizontalLayoutGroup>() != null)
                {
                    Log.LogWarning("ExtraSpecialSlots: horizontal layout still present; skipping grid conversion.");
                }
                else
                {
                    var grid = panel.GetComponent<GridLayoutGroup>() ?? panel.gameObject.AddComponent<GridLayoutGroup>();
                    if (grid == null)
                    {
                        Log.LogWarning("ExtraSpecialSlots: could not install special slots grid.");
                    }
                    else
                    {
                        grid.padding = padding;
                        grid.spacing = new Vector2(spacing, spacing);

                        var cellPixels = ItemViewFactory.GetCellPixelSize(new IntVec2(1, 1));
                        grid.cellSize = new Vector2(cellPixels.X, cellPixels.Y);
                        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
                        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                        grid.constraintCount = 3;

                        var rows = Math.Max(1, (panel.childCount + 2) / 3);
                        var height = grid.padding.vertical
                                     + rows * grid.cellSize.y
                                     + Math.Max(0, rows - 1) * grid.spacing.y;
                        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

                        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
                        if (panel.parent is RectTransform parent)
                            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Log.LogError($"ExtraSpecialSlots: failed to arrange special slots: {e}");
        }
        finally
        {
            PendingPanels.Remove(panelId);
        }
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

                ArrangeAsGrid(panel);
            }
            catch (Exception e)
            {
                // An optional UI layout must never abort EFT screen initialization.
                Log.LogError($"ExtraSpecialSlots: failed to schedule special slots layout: {e}");
            }
        }
    }
}
