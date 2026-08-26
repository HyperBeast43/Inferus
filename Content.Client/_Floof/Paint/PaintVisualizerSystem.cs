using Content.Shared._Floof.Paint;
using Content.Shared.Clothing;
using Content.Shared.Hands;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Floof.Paint;

/// <summary>
/// Applies greyscale + color tint to painted entities.
/// Uses LayerSet* APIs only — never assigns Layer.Shader and Layer.ShaderPrototype
/// together, which triggers DebugAssertException in SpriteSystem.RenderLayer
/// </summary>
public sealed class ColorPaintedVisualizerSystem : VisualizerSystem<ColorPaintedComponent>
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ColorPaintedComponent, HeldVisualsUpdatedEvent>(OnHeldVisualsUpdated);
        SubscribeLocalEvent<ColorPaintedComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ColorPaintedComponent, EquipmentVisualsUpdatedEvent>(OnEquipmentVisualsUpdated);
    }

    protected override void OnAppearanceChange(EntityUid uid, ColorPaintedComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null
            || !_appearance.TryGetData(uid, PaintVisuals.Painted, out bool _))
            return;

        // Snapshot original color once so we can restore it on removal
        if (component.BeforeColor == default)
            component.BeforeColor = args.Sprite.Color;

        ApplyPaint(args.Sprite, component);
    }

    private void OnShutdown(EntityUid uid, ColorPaintedComponent component, ref ComponentShutdown args)
    {
        if (Terminating(uid) || !TryComp(uid, out SpriteComponent? sprite))
            return;

        ClearPaint(sprite, component);
    }

    private void OnHeldVisualsUpdated(EntityUid uid, ColorPaintedComponent component, HeldVisualsUpdatedEvent args) =>
        UpdateMappedLayers(component, args.RevealedLayers, args.User);

    private void OnEquipmentVisualsUpdated(EntityUid uid, ColorPaintedComponent component, EquipmentVisualsUpdatedEvent args) =>
        UpdateMappedLayers(component, args.RevealedLayers, args.Equipee);

    private void UpdateMappedLayers(ColorPaintedComponent component, HashSet<string> layers, EntityUid entity)
    {
        if (layers.Count == 0 || !TryComp(entity, out SpriteComponent? sprite))
            return;

        foreach (var revealed in layers)
        {
            if (!sprite.LayerMapTryGet(revealed, out var layerIndex))
                continue;

            // Only set shader if the layer doesn't already have one from another system
            if (!string.IsNullOrEmpty(component.ShaderName)
                && sprite.TryGetLayer(layerIndex, out var layer)
                && layer.Shader is null)
            {
                sprite.LayerSetShader(layerIndex, component.ShaderName);
            }

            sprite.LayerSetColor(layerIndex, component.Color);
        }
    }

    /// <summary>
    /// Applies greyscale shader + tint color to every visible layer.
    /// </summary>
    private static void ApplyPaint(SpriteComponent sprite, ColorPaintedComponent component)
    {
        var i = 0;
        foreach (var _ in sprite.AllLayers)
        {
            if (sprite.TryGetLayer(i, out var layer) && layer.Visible)
            {
                // Skip layers that already have a different shader (e.g. displacement)
                if (layer.Shader is null || layer.ShaderPrototype == component.ShaderName)
                {
                    if (!string.IsNullOrEmpty(component.ShaderName))
                        sprite.LayerSetShader(i, component.ShaderName);

                    sprite.LayerSetColor(i, component.Color);
                }
            }

            i++;
        }
    }

    /// <summary>
    /// Removes the paint shader and restores the pre-paint color.
    /// </summary>
    private static void ClearPaint(SpriteComponent sprite, ColorPaintedComponent component)
    {
        var restoreColor = component.BeforeColor != default ? component.BeforeColor : Color.White;

        var i = 0;
        foreach (var _ in sprite.AllLayers)
        {
            if (sprite.TryGetLayer(i, out var layer))
            {
                // Only clear layers we actually painted
                if (layer.ShaderPrototype == component.ShaderName)
                    sprite.LayerSetShader(i, null, null);

                // Restore color only if it still matches the paint color
                if (layer.Color == component.Color)
                    sprite.LayerSetColor(i, restoreColor);
            }

            i++;
        }
    }
}
