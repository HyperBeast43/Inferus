using Content.Client._Floof.Lobby.UI;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Utility;

// ReSharper disable once CheckNamespace
namespace Content.Client.Lobby.UI;

/// <summary>
/// Floofstation extensions for the profile editor (loadout metadata editor + copy-to-all).
/// </summary>
public sealed partial class HumanoidProfileEditor
{
    private void OpenLoadoutFloof(
        JobPrototype? jobProto,
        RoleLoadout roleLoadout,
        RoleLoadoutPrototype roleLoadoutProto,
        ICommonSession session,
        IDependencyCollection collection)
    {
        if (_loadoutWindow == null)
            return;

        _loadoutWindow.OnRequestLoadoutMetadataEdit += (groupProto, loadoutProto) =>
        {
            if (!roleLoadout.SelectedLoadouts.TryGetValue(groupProto, out var group)
                || group.Find(it => it.Prototype == loadoutProto) is not { } loadout)
                return;

            var dlg = new LoadoutMetadataEditorDialog(loadout, loadoutProto, groupProto);
            dlg.OnSave += args =>
            {
                var (newLoadout, copyMetadataToAll, copyLoadoutToAll) = args;

                if (!roleLoadout.SelectedLoadouts.TryGetValue(groupProto, out var newGroup))
                    return;

                newGroup.RemoveAll(it => it.Prototype == loadoutProto);
                newGroup.Add(newLoadout);
                Profile = Profile?.WithLoadout(roleLoadout);

                if (copyMetadataToAll && Profile is not null)
                {
                    foreach (var (_, otherRoleLoadout) in Profile.Loadouts)
                    {
                        if (!_prototypeManager.TryIndex(otherRoleLoadout.Role, out var otherRoleProto))
                            continue;

                        foreach (var otherGroupId in otherRoleProto.Groups)
                        {
                            var otherLoadouts = otherRoleLoadout.SelectedLoadouts.GetOrNew(otherGroupId);

                            // Update existing selection, or add if "copy loadout everywhere" is checked
                            if (otherLoadouts.RemoveAll(it => it.Prototype == loadoutProto) > 0 || copyLoadoutToAll)
                                otherLoadouts.Add(newLoadout);
                        }

                        otherRoleLoadout.EnsureValid(Profile, session, collection);
                        Profile = Profile.WithLoadout(otherRoleLoadout);
                    }
                }

                _loadoutWindow.RefreshLoadouts(roleLoadout, session, collection);
                SetDirty();
                ReloadPreview();
            };
            dlg.OpenCentered();
        };
    }
}
