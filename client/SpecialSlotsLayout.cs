using System;
using System.Collections.Generic;
using EFT.UI.DragAndDrop;
using EFT.InventoryLogic;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHawk.ExtraSpecialSlots.Client;

// Only arranges the native slot roots. No item views, sprites or inventory
// operations are replaced. Not derived from LayoutGroup: it can coexist with
// the disabled native HorizontalLayoutGroup without destroying components.
public sealed class SpecialSlotsLayout : MonoBehaviour, ILayoutGroup, ILayoutElement
{
    private RectOffset _padding = new();
    private float _spacing;
    private bool _ready;
    private bool _applying;
    private readonly List<RectTransform?> _slots = new();
    private Slot[] _boundSlots = Array.Empty<Slot>();
    private SlotView? _template;

    internal void Bind(SlotView template, Slot[] slots)
    {
        _template = template;
        _boundSlots = slots;
    }

    internal void LogBindings()
    {
        Measure();
        var found = 0;
        foreach (var slot in _slots) if (slot != null) found++;
        ExtraSpecialSlotsPlugin.Log.LogInfo($"Special slots: {found}/{_boundSlots.Length} bound views.");
        for (var i = 0; i < _slots.Count; i++)
            if (_slots[i] == null)
                ExtraSpecialSlotsPlugin.Log.LogWarning("Special slot view missing: " + _boundSlots[i].Name);
    }
    private float _requiredHeight;

    public float minWidth => -1;
    public float preferredWidth => -1;
    public float flexibleWidth => -1;
    public float minHeight => _requiredHeight;
    public float preferredHeight => _requiredHeight;
    public float flexibleHeight => -1;
    public int layoutPriority => 1;

    public void CalculateLayoutInputHorizontal() => Measure();
    public void CalculateLayoutInputVertical() => Measure();

    private void Measure()
    {
        if (!_ready) return;
        _slots.Clear();
        // Enumerate actual Slot bindings in inventory order, not arbitrary
        // panel children. Template/header/closed views cannot consume a cell.
        foreach (var boundSlot in _boundSlots)
        {
            RectTransform? match = null;
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                var view = child.GetComponent<SlotView>();
                if (view == null || view == _template || !ReferenceEquals(view.Slot, boundSlot)) continue;
                match = child as RectTransform;
                break;
            }
            // Preserve a missing slot's cell instead of shifting later slots.
            _slots.Add(match);
        }
        var cell = ItemViewFactory.GetCellPixelSize(new IntVec2(1, 1));
        var rows = Math.Max(1, (_slots.Count + 2) / 3);
        _requiredHeight = _padding.vertical + rows * cell.Y + Math.Max(0, rows - 1) * _spacing;
    }

    internal void Initialize(RectOffset padding, float spacing)
    {
        _padding = new RectOffset(padding.left, padding.right, padding.top, padding.bottom);
        _spacing = spacing;
        _ready = true;
    }

    public void SetLayoutHorizontal() => Apply();
    public void SetLayoutVertical() => Apply();

    private void OnEnable()
    {
        Apply();
        if (_ready) LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
    }

    private void OnTransformChildrenChanged()
    {
        if (_ready) LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
    }

    internal void Apply()
    {
        if (!_ready || _applying || !isActiveAndEnabled) return;
        _applying = true;
        try
        {
            var panel = (RectTransform)transform;
            var horizontal = GetComponent<HorizontalLayoutGroup>();
            if (horizontal != null && horizontal.enabled) horizontal.enabled = false;
            var cell = ItemViewFactory.GetCellPixelSize(new IntVec2(1, 1));
            Measure();
            var index = 0;
            foreach (var slot in _slots)
            {
                if (slot == null) { index++; continue; }
                // Match GridLayoutGroup's upper-left placement using each root's
                // own pivot; do not touch the nested artwork transforms.
                slot.anchorMin = slot.anchorMax = new Vector2(0, 1);
                slot.sizeDelta = new Vector2(cell.X, cell.Y);
                slot.anchoredPosition = new Vector2(
                    _padding.left + index % 3 * (cell.X + _spacing) + slot.pivot.x * cell.X,
                    -_padding.top - index / 3 * (cell.Y + _spacing) - (1 - slot.pivot.y) * cell.Y);
                index++;
            }
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _requiredHeight);
        }
        catch (Exception ex)
        {
            ExtraSpecialSlotsPlugin.Log.LogError("Special slots layout failed: " + ex);
            // Stop repeated errors; leave native UI/input alive.
            enabled = false;
        }
        finally { _applying = false; }
    }
}
