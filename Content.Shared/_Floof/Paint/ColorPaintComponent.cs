using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Paint;

/// <summary>
/// Entity that, when used on another entity, will paint it.
/// Port of EE spray paint with Floof fixes (whitelist/blacklist, pre-doafter checks, etc.).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ColorPaintComponent : Component
{
    /// <summary>
    /// Sound played when paint is applied.
    /// </summary>
    [DataField]
    public SoundSpecifier Spray = new SoundPathSpecifier("/Audio/Effects/spray2.ogg");

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityWhitelist? Whitelist;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityWhitelist? Blacklist;

    /// <summary>
    /// How long the do-after takes (seconds).
    /// </summary>
    [DataField]
    public int Delay = 2;

    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#c62121");

    /// <summary>
    /// Solution on the entity that contains the paint reagent.
    /// </summary>
    [DataField]
    public string Solution = "drink";

    /// <summary>
    /// Reagent that is consumed as paint.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype> Reagent = "SpaceGlue";

    /// <summary>
    /// Reagent consumption per successful use.
    /// </summary>
    [DataField]
    public FixedPoint2 ConsumptionUnit = FixedPoint2.New(5);

    [DataField]
    public TimeSpan DurationPerUnit = TimeSpan.FromSeconds(6);
}
