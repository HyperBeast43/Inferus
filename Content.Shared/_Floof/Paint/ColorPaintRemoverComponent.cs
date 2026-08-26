using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Floof.Paint;

/// <summary>
/// Removes paint from an entity that was painted with spray paint (typically added to soap).
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(ColorPaintRemoverSystem))]
public sealed partial class ColorPaintRemoverComponent : Component
{
    /// <summary>
    /// Sound played when the target is cleaned.
    /// </summary>
    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Effects/Fluids/watersplash.ogg");

    [DataField]
    public float CleanDelay = 2f;
}
