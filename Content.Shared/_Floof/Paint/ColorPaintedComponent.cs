using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Floof.Paint;

/// <summary>
/// Applied to an entity that has been painted using spray paint (NOT the spray painter tool).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ColorPaintedComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#2cdbd5");

    /// <summary>
    /// Used to restore the original color when the component is removed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color BeforeColor;

    [DataField, AutoNetworkedField]
    public bool Enabled;

    // Not using ProtoId because ShaderPrototype lives in Robust.Client
    [DataField, AutoNetworkedField]
    public string ShaderName = "Greyscale";
}

[Serializable, NetSerializable]
public enum PaintVisuals : byte
{
    Painted,
}
