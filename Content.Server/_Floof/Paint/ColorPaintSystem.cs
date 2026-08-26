using Content.Shared._Floof.Paint;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Sprite;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;

namespace Content.Server._Floof.Paint;

/// <summary>
/// Colors a target and consumes reagent on each successful paint.
/// Floof port of the EE spray-paint system with pre-doafter validation fixes.
/// </summary>
public sealed class ColorPaintSystem : SharedColorPaintSystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly OpenableSystem _openable = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ColorPaintComponent, AfterInteractEvent>(OnInteract);
        SubscribeLocalEvent<ColorPaintComponent, PaintDoAfterEvent>(OnPaint);
        SubscribeLocalEvent<ColorPaintComponent, GetVerbsEvent<UtilityVerb>>(OnPaintVerb);
    }

    private void OnInteract(Entity<ColorPaintComponent> entity, ref AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target is not { Valid: true } target)
            return;

        PrepPaint(entity, target, args.User);
    }

    private void OnPaintVerb(Entity<ColorPaintComponent> entity, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var target = args.Target;
        var user = args.User;
        var verb = new UtilityVerb()
        {
            Act = () => PrepPaint(entity, target, user),
            Text = Loc.GetString("paint-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/_Floof/Interface/VerbIcons/paint.svg.192dpi.png")),
        };
        args.Verbs.Add(verb);
    }

    private void PrepPaint(Entity<ColorPaintComponent> entity, EntityUid target, EntityUid user)
    {
        // Validate before starting the do-after so the player isn't left hanging
        if (!CanPaint(entity, target, user, out var reason))
        {
            if (reason != null)
                _popup.PopupEntity(reason, user, user);
            return;
        }

        _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, user, entity.Comp.Delay, new PaintDoAfterEvent(), entity, target, entity)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnPaint(Entity<ColorPaintComponent> entity, ref PaintDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { Valid: true } target)
            return;

        // Re-validate after the do-after (state may have changed)
        if (!CanPaint(entity, target, args.User, out var reason))
        {
            if (reason != null)
                _popup.PopupEntity(reason, args.User, args.User);
            return;
        }

        Paint(entity, target, args.User);
        args.Handled = true;
    }

    public void Paint(Entity<ColorPaintComponent> entity, EntityUid target, EntityUid user)
    {
        if (!CanPaint(entity, target, user, out var reason))
        {
            if (reason != null)
                _popup.PopupEntity(reason, user, user);
            return;
        }

        if (!TryConsumePaint(entity))
            return;

        Paint(entity.Comp.Whitelist, entity.Comp.Blacklist, target, entity.Comp.Color);
        _audio.PlayPvs(entity.Comp.Spray, entity);
        _popup.PopupEntity(Loc.GetString("paint-success", ("target", target)), user, user, PopupType.Medium);
    }

    /// <summary>
    /// Checks whether the target can be painted. Returns false and a localized reason if not.
    /// </summary>
    public bool CanPaint(Entity<ColorPaintComponent> paint, EntityUid target, EntityUid user, out string? reason)
    {
        if (_openable.IsClosed(paint))
        {
            reason = Loc.GetString("paint-closed", ("used", paint));
            return false;
        }

        if (!_solutionContainer.TryGetSolution(paint.Owner, paint.Comp.Solution, out _, out var solution)
            || solution.Volume <= 0)
        {
            reason = Loc.GetString("paint-empty", ("used", paint));
            return false;
        }

        if (HasComp<ColorPaintedComponent>(target) || HasComp<RandomSpriteComponent>(target))
        {
            reason = Loc.GetString("paint-failure-painted", ("target", target));
            return false;
        }

        if (_whitelist.IsWhitelistFail(paint.Comp.Whitelist, target)
            || _whitelist.IsWhitelistPass(paint.Comp.Blacklist, target))
        {
            reason = Loc.GetString("paint-failure", ("target", target));
            return false;
        }

        reason = null;
        return true;
    }

    private bool TryConsumePaint(Entity<ColorPaintComponent> reagent)
    {
        if (!_solutionContainer.TryGetSolution(reagent.Owner, reagent.Comp.Solution, out _, out var solution))
            return false;

        var quantity = solution.RemoveReagent(reagent.Comp.Reagent, reagent.Comp.ConsumptionUnit);
        return quantity > 0;
    }
}
