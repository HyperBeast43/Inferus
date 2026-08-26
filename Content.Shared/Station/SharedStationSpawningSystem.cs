using System.Linq;
using Content.Shared._Floof.Paint;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Collections;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
#region Starlight
using Content.Shared.Preferences;
using Content.Shared._Starlight.Roles;
using Content.Shared.Containers.ItemSlots;
#endregion

namespace Content.Shared.Station;

public abstract partial class SharedStationSpawningSystem : EntitySystem
{
    [Dependency] protected IPrototypeManager PrototypeManager = default!;
    [Dependency] protected ISharedAdminLogManager _adminLogger = default!; // Starlight
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] protected InventorySystem InventorySystem = default!;
    [Dependency] private SharedHandsSystem _handsSystem = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private SharedTransformSystem _xformSystem = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!; // Starlight
    [Dependency] private SharedColorPaintSystem _colorPaint = default!; // Floofstation

    private EntityQuery<HandsComponent> _handsQuery;
    private EntityQuery<InventoryComponent> _inventoryQuery;
    private EntityQuery<StorageComponent> _storageQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<ItemSlotsComponent> _itemSlotsQuery; // Starlight

    public override void Initialize()
    {
        base.Initialize();
        _handsQuery = GetEntityQuery<HandsComponent>();
        _inventoryQuery = GetEntityQuery<InventoryComponent>();
        _storageQuery = GetEntityQuery<StorageComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _itemSlotsQuery = GetEntityQuery<ItemSlotsComponent>(); // Starlight
    }

    /// <summary>
    ///     Equips the data from a `RoleLoadout` onto an entity.
    /// </summary>
    public void EquipRoleLoadout(EntityUid entity, RoleLoadout loadout, RoleLoadoutPrototype roleProto, HumanoidCharacterProfile? profile = null) =>
        EquipRoleLoadout(entity, loadout, roleProto, profile, null); // Starlight edit

    internal void EquipRoleLoadout(EntityUid entity, RoleLoadout loadout, RoleLoadoutPrototype roleProto, HumanoidCharacterProfile? profile, PriorityStorageEquipContext? priorityContext) // Starlight
    {
        // Starlight Start
        var appliedLoadout = EnsureComp<AppliedRoleLoadoutComponent>(entity);
        appliedLoadout.Loadout = loadout;
        appliedLoadout.Profile = profile;

        if (StarlightEquipRoleLoadout(entity, loadout, [], roleProto, priorityContext)) // Starlight
        {
            EquipRoleName(entity, loadout, roleProto);
            return;
        }
        // Starlight end

        foreach (var group in loadout.SelectedLoadouts.OrderBy(x => roleProto.Groups.FindIndex(e => e == x.Key)))
        {
            foreach (var items in group.Value)
            {
                if (!PrototypeManager.TryIndex(items.Prototype, out var loadoutProto))
                {
                    Log.Error($"Unable to find loadout prototype for {items.Prototype}");
                    continue;
                }

                // Floofstation
                var spawned = EquipStartingGear(entity, loadoutProto, raiseEvent: false, priorityContext: priorityContext);
                if (spawned.Count == 1 && spawned[0] is { Valid: true } spawnedEntity)
                    ApplyCustomLoadoutMetadata(spawnedEntity, items);
                else if (items.HasCustomMetadata)
                    Log.Warning($"Refusing to apply custom metadata to a multi-item loadout: {loadoutProto}");
            }
        }

        EquipRoleName(entity, loadout, roleProto);
    }

    private void ApplyCustomLoadoutMetadata(EntityUid spawnedEntity, Loadout loadout)
    {
        if (!Exists(spawnedEntity) || Deleted(spawnedEntity))
            return;

        const int MaxNameLength = 96;
        const int MaxDescLength = 512;

        var md = MetaData(spawnedEntity);

        if (loadout.NameOverride is { } customName)
        {
            customName = FormattedMessage.RemoveMarkupPermissive(customName);
            _metadata.SetEntityName(spawnedEntity, customName[..Math.Min(customName.Length, MaxNameLength)], md);
        }

        if (loadout.DescriptionOverride is { } customDesc)
        {
            customDesc = FormattedMessage.RemoveMarkupPermissive(customDesc);
            _metadata.SetEntityDescription(spawnedEntity, customDesc[..Math.Min(customDesc.Length, MaxDescLength)], md);
        }

        if (loadout.ColorOverride is { } customColor && HasComp<ItemComponent>(spawnedEntity))
        {
            var parsedColor = Color.FromHex(customColor, Color.White);
            if (parsedColor.A < 1f)
                parsedColor = Color.Pink;

            parsedColor = SharedColorPaintSystem.ClampBrightness(parsedColor, 0.25f, 1f);
            _colorPaint.Paint(null, null, spawnedEntity, parsedColor);
        }
    }

    public void EquipRoleName(EntityUid entity, RoleLoadout loadout, RoleLoadoutPrototype roleProto)
    {
        string? name = null;

        if (roleProto.CanCustomizeName)
            name = loadout.EntityName;

        if (string.IsNullOrEmpty(name) && PrototypeManager.Resolve(roleProto.NameDataset, out var nameData))
            name = Loc.GetString(_random.Pick(nameData.Values));

        if (!string.IsNullOrEmpty(name))
            _metadata.SetEntityName(entity, name);
    }

    public List<EntityUid> EquipStartingGear(EntityUid entity, LoadoutPrototype loadout, bool raiseEvent = true) =>
        EquipStartingGear(entity, loadout, raiseEvent, null); // Starlight

    internal List<EntityUid> EquipStartingGear(EntityUid entity, LoadoutPrototype loadout, bool raiseEvent, PriorityStorageEquipContext? priorityContext) // Starlight
    {
        EquipStartingGear(entity, loadout.StartingGear, raiseEvent, priorityContext);
        return EquipStartingGear(entity, (IEquipmentLoadout) loadout, raiseEvent, priorityContext);
    }

    public void EquipStartingGear(EntityUid entity, ProtoId<StartingGearPrototype>? startingGear, bool raiseEvent = true) =>
        EquipStartingGear(entity, startingGear, raiseEvent, null); // Starlight

    internal void EquipStartingGear(EntityUid entity, ProtoId<StartingGearPrototype>? startingGear, bool raiseEvent, PriorityStorageEquipContext? priorityContext) // Starlight
    {
        PrototypeManager.Resolve(startingGear, out var gearProto);
        EquipStartingGear(entity, gearProto, raiseEvent, priorityContext);
    }

    public void EquipStartingGear(EntityUid entity, StartingGearPrototype? startingGear, bool raiseEvent = true) =>
        EquipStartingGear(entity, startingGear, raiseEvent, null); // Starlight

    internal void EquipStartingGear(EntityUid entity, StartingGearPrototype? startingGear, bool raiseEvent, PriorityStorageEquipContext? priorityContext) =>
        EquipStartingGear(entity, (IEquipmentLoadout?) startingGear, raiseEvent, priorityContext); // Starlight

    public List<EntityUid> EquipStartingGear(EntityUid entity, IEquipmentLoadout? startingGear, bool raiseEvent = true) =>
        EquipStartingGear(entity, startingGear, raiseEvent, null); // Starlight

    internal List<EntityUid> EquipStartingGear(EntityUid entity, IEquipmentLoadout? startingGear, bool raiseEvent, PriorityStorageEquipContext? priorityContext) // Starlight
    {
        var spawned = new List<EntityUid>();

        if (startingGear == null)
            return spawned;

        var xform = _xformQuery.GetComponent(entity);

        if (InventorySystem.TryGetSlots(entity, out var slotDefinitions))
        {
            var gearLeftToBeIssued = startingGear.Equipment.ToDictionary(); // Starlight
            foreach (var slot in slotDefinitions)
            {
                var equipmentStr = startingGear.GetGear(slot.Name);
                if (!string.IsNullOrEmpty(equipmentStr))
                {
                    var equipmentEntity = Spawn(equipmentStr, xform.Coordinates);
                    spawned.Add(equipmentEntity); // Floofstation
                    InventorySystem.TryEquip(entity, equipmentEntity, slot.Name, silent: true, force: true);
                    gearLeftToBeIssued.Remove(slot.Name); // Starlight
                }
            }

            // Starlight Start
            foreach (var item in gearLeftToBeIssued)
            {
                var leftover = Spawn(item.Value, xform.Coordinates);
                spawned.Add(leftover); // Floofstation
            }
            // Starlight End
        }

        if (_handsQuery.TryComp(entity, out var handsComponent))
        {
            var inhand = startingGear.Inhand;
            var coords = xform.Coordinates;
            foreach (var prototype in inhand)
            {
                var inhandEntity = Spawn(prototype, coords);
                spawned.Add(inhandEntity); // Floofstation

                if (_handsSystem.TryGetEmptyHand((entity, handsComponent), out var emptyHand))
                {
                    if (_handsSystem.TryPickup(entity, inhandEntity, emptyHand, checkActionBlocker: false, handsComp: handsComponent)) // Starlight
                        priorityContext?.IssuedGear.Add(inhandEntity); // Starlight
                }
            }
        }

        if (startingGear.Storage.Count > 0)
        {
            #region Starlight
            _inventoryQuery.TryComp(entity, out var inventoryComp);
            EquipStorageGear(entity, startingGear, inventoryComp, priorityContext);
            #endregion
        }

        if (raiseEvent)
        {
            var ev = new StartingGearEquippedEvent(entity);
            RaiseLocalEvent(entity, ref ev);
        }

        return spawned; // Floofstation
    }

    public string? GetGearForSlot(RoleLoadout? loadout, string slot)
    {
        if (loadout == null)
            return null;

        foreach (var group in loadout.SelectedLoadouts)
        {
            foreach (var items in group.Value)
            {
                if (!PrototypeManager.Resolve(items.Prototype, out var loadoutPrototype))
                    return null;

                var gear = ((IEquipmentLoadout) loadoutPrototype).GetGear(slot);
                if (gear != string.Empty)
                    return gear;
            }
        }

        return null;
    }

    // Starlight start
    public bool StarlightEquipRoleLoadout(EntityUid entity, RoleLoadout loadout, IEnumerable<IEquipmentLoadout> otherStartingGear, RoleLoadoutPrototype roleProto) =>
        StarlightEquipRoleLoadout(entity, loadout, otherStartingGear, roleProto, null); // Starlight

    internal bool StarlightEquipRoleLoadout(EntityUid entity, RoleLoadout loadout, IEnumerable<IEquipmentLoadout> otherStartingGear, RoleLoadoutPrototype roleProto, PriorityStorageEquipContext? priorityContext) // Starlight
    {
        // Pair gear with optional loadout metadata for custom name/desc/color
        var allStartingGear = new List<(IEquipmentLoadout Gear, Loadout? Meta)>();

        foreach (var group in loadout.SelectedLoadouts.OrderBy(x => roleProto.Groups.FindIndex(e => e == x.Key)))
        {
            foreach (var items in group.Value)
            {
                if (!PrototypeManager.TryIndex(items.Prototype, out var loadoutProto))
                {
                    Log.Error($"Unable to find loadout prototype for {items.Prototype}");
                    continue;
                }

                if (loadoutProto.StartingGear is not null)
                {
                    PrototypeManager.Resolve(loadoutProto.StartingGear, out var gearProto);
                    if (gearProto is IEquipmentLoadout equipmentProto)
                        allStartingGear.Add((equipmentProto, null));
                }

                allStartingGear.Add((loadoutProto, items));
            }
        }

        foreach (var other in otherStartingGear)
            allStartingGear.Add((other, null));

        var xform = _xformQuery.GetComponent(entity);
        var coords = xform.Coordinates;
        var spawnedByGear = new Dictionary<IEquipmentLoadout, List<EntityUid>>();

        if (InventorySystem.TryGetSlots(entity, out var slotDefinitions))
        {
            foreach (var (startingGear, _) in allStartingGear)
            {
                var equipmentRemaining = startingGear.Equipment.ToList();
                foreach (var slot in slotDefinitions)
                {
                    var equipmentStr = startingGear.GetGear(slot.Name);
                    if (!string.IsNullOrEmpty(equipmentStr))
                    {
                        if (slot.Name == "back" && slot.Whitelist?.Tags?.Contains("CorgiWearable") == true)
                            equipmentStr = "ClothingBagPet";
                        var equipmentEntity = Spawn(equipmentStr, xform.Coordinates);
                        InventorySystem.TryEquip(entity, equipmentEntity, slot.Name, silent: true, force: true);
                        spawnedByGear.GetOrNew(startingGear).Add(equipmentEntity);
                    }

                    equipmentRemaining.Remove(equipmentRemaining.FirstOrDefault(a => a.Key == slot.Name));
                }

                foreach (var equipment in equipmentRemaining)
                {
                    var equipmentEntity = Spawn(equipment.Value, xform.Coordinates);
                    spawnedByGear.GetOrNew(startingGear).Add(equipmentEntity);
                }
            }
        }

        if (_handsQuery.TryComp(entity, out var handsComponent))
        {
            foreach (var (startingGear, _) in allStartingGear)
            {
                foreach (var prototype in startingGear.Inhand)
                {
                    var inhandEntity = Spawn(prototype, coords);
                    spawnedByGear.GetOrNew(startingGear).Add(inhandEntity);

                    if (_handsSystem.TryGetEmptyHand((entity, handsComponent), out var emptyHand))
                    {
                        if (_handsSystem.TryPickup(entity, inhandEntity, emptyHand, checkActionBlocker: false, handsComp: handsComponent))
                            priorityContext?.IssuedGear.Add(inhandEntity);
                    }
                }
            }
        }

        _inventoryQuery.TryComp(entity, out var inventoryComp);

        foreach (var (startingGear, _) in allStartingGear)
        {
            EquipStorageGear(entity, startingGear, inventoryComp, priorityContext);
        }

        // Floofstation – apply metadata to single-entity loadout selections
        foreach (var (startingGear, meta) in allStartingGear)
        {
            if (meta is null || !meta.HasCustomMetadata)
                continue;

            if (!spawnedByGear.TryGetValue(startingGear, out var spawned) || spawned.Count != 1)
            {
                Log.Warning($"Refusing to apply custom metadata to a multi-item or storage-only loadout: {meta.Prototype}");
                continue;
            }

            ApplyCustomLoadoutMetadata(spawned[0], meta);
        }

        return true;
    }

    #region Starlight
    private void EquipStorageGear(EntityUid entity,
        IEquipmentLoadout startingGear,
        InventoryComponent? inventoryComp,
        PriorityStorageEquipContext? priorityContext)
    {
        var coords = _xformSystem.GetMapCoordinates(entity);

        foreach (var (slotName, entProtos) in startingGear.Storage)
        {
            if (entProtos == null || entProtos.Count == 0)
                continue;

            var prioritize = priorityContext != null && slotName == "back";
            EntityUid? slotEntity = null;
            if (inventoryComp != null)
                InventorySystem.TryGetSlotEntity(entity, slotName, out slotEntity, inventoryComponent: inventoryComp);

            if (slotEntity != null && _storageQuery.TryComp(slotEntity, out var storage))
            {
                foreach (var entProto in entProtos)
                {
                    var spawnedEntity = Spawn(entProto, coords);

                    if (prioritize)
                    {
                        if (!TryInsertPriorityStorageGear(entity,
                                (slotEntity.Value, storage),
                                spawnedEntity,
                                priorityContext!))
                        {
                            TryPlacePriorityGearInHands(entity,
                                spawnedEntity,
                                slotName,
                                priorityContext!,
                                failedStorage: slotEntity.Value);
                        }
                    }
                    else
                    {
                        _storage.Insert(slotEntity.Value,
                            spawnedEntity,
                            out _,
                            storageComp: storage,
                            playSound: false);
                    }
                }
            }
            else if (!prioritize && slotEntity != null && _itemSlotsQuery.TryComp(slotEntity, out var itemSlots))
            {
                foreach (var entProto in entProtos)
                {
                    var spawnedEntity = Spawn(entProto, coords);
                    Entity<ItemSlotsComponent?> typed = (slotEntity.Value, itemSlots);
                    InsertIntoItemSlots(typed, spawnedEntity);
                }
            }
            else if (prioritize)
            {
                foreach (var entProto in entProtos)
                {
                    var spawnedEntity = Spawn(entProto, coords);
                    TryPlacePriorityGearInHands(entity, spawnedEntity, slotName, priorityContext!);
                }
            }
        }
    }

    private bool TryInsertPriorityStorageGear(EntityUid entity,
        Entity<StorageComponent> storage,
        EntityUid gear,
        PriorityStorageEquipContext priorityContext)
    {
        if (!_storage.CanInsert(storage,
                gear,
                out _,
                storage.Comp,
                ignoreStacks: true,
                ignoreLocation: true))
        {
            return false;
        }

        if (TryInsertEntireStorageItem(storage, gear, priorityContext))
            return true;

        var displacedItems = new List<(EntityUid Item, ItemStorageLocation Location)>();
        foreach (var storedItem in storage.Comp.Container.ContainedEntities.ToArray())
        {
            if (priorityContext.IssuedGear.Contains(storedItem) ||
                !storage.Comp.StoredItems.TryGetValue(storedItem, out var location))
            {
                continue;
            }

            _xformSystem.DropNextTo(storedItem, entity);
            displacedItems.Add((storedItem, location));

            if (!TryInsertEntireStorageItem(storage, gear, priorityContext))
                continue;

            foreach (var (item, _) in displacedItems)
            {
                _adminLogger.Add(LogType.AntagSelection,
                    LogImpact.Low,
                    $"{ToPrettyString(entity):target} had {ToPrettyString(item):item} dropped from {ToPrettyString(storage):storage} to make room for antagonist gear {ToPrettyString(gear):item}");
            }

            return true;
        }

        foreach (var (item, location) in displacedItems)
        {
            if (_storage.InsertAt(storage.AsNullable(),
                    item,
                    location,
                    out _,
                    playSound: false,
                    stackAutomatically: false))
            {
                continue;
            }

            _adminLogger.Add(LogType.AntagSelection,
                LogImpact.Low,
                $"{ToPrettyString(entity):target} had {ToPrettyString(item):item} left on the ground after it could not be restored to {ToPrettyString(storage):storage} following a failed attempt to make room for antagonist gear {ToPrettyString(gear):item}");
        }

        return false;
    }

    private bool TryInsertEntireStorageItem(Entity<StorageComponent> storage,
        EntityUid gear,
        PriorityStorageEquipContext priorityContext)
    {
        if (!_storage.CanInsert(storage, gear, out _, storage.Comp, ignoreStacks: true) ||
            !_storage.Insert(storage,
                gear,
                out _,
                storageComp: storage.Comp,
                playSound: false,
                stackAutomatically: false))
        {
            return false;
        }

        priorityContext.IssuedGear.Add(gear);
        return true;
    }

    private bool TryPlacePriorityGearInHands(EntityUid entity,
        EntityUid gear,
        string slotName,
        PriorityStorageEquipContext priorityContext,
        EntityUid? failedStorage = null)
    {
        var storageFailure = failedStorage == null
            ? $"their {slotName} slot had no storage"
            : $"it could not fit in {ToPrettyString(failedStorage.Value):storage}";

        if (_handsQuery.TryComp(entity, out var hands))
        {
            if (_handsSystem.TryPickupAnyHand(entity,
                    gear,
                    checkActionBlocker: false,
                    animate: false,
                    handsComp: hands))
            {
                priorityContext.IssuedGear.Add(gear);
                _adminLogger.Add(LogType.AntagSelection,
                    LogImpact.Low,
                    $"{ToPrettyString(entity):target} had antagonist gear {ToPrettyString(gear):item} placed in a hand because {storageFailure}");
                return true;
            }

            foreach (var hand in _handsSystem.EnumerateHands((entity, hands)))
            {
                if (!_handsSystem.TryGetHeldItem((entity, hands), hand, out var heldItem) ||
                    priorityContext.IssuedGear.Contains(heldItem.Value))
                {
                    continue;
                }

                if (!_handsSystem.TryForcePickup((entity, hands),
                        gear,
                        hand,
                        checkActionBlocker: false,
                        animate: false,
                        handsComp: hands))
                {
                    continue;
                }

                priorityContext.IssuedGear.Add(gear);
                _adminLogger.Add(LogType.AntagSelection,
                    LogImpact.Low,
                    $"{ToPrettyString(entity):target} had {ToPrettyString(heldItem):item} dropped from a hand so antagonist gear {ToPrettyString(gear):item} could be held because {storageFailure}");
                return true;
            }
        }

        _adminLogger.Add(LogType.AntagSelection,
            LogImpact.Low,
            $"{ToPrettyString(entity):target} had antagonist gear {ToPrettyString(gear):item} left on the ground because {storageFailure} and no hand could hold it");
        return false;
    }
    #endregion

    private void InsertIntoItemSlots(Entity<ItemSlotsComponent?> typed, EntityUid entity)
    {
        bool foundEmpty = _itemSlots.TryInsertEmpty(typed, entity, null, excludeUserAudio: true, suppressSound: true);

        if (!foundEmpty)
        {
            bool foundSlot = _itemSlots.TryGetAvailableSlot(typed, entity, null, out var writeSlot, emptyOnly: false, allowSwap: false);
            if (foundSlot)
            {
                _itemSlots.TryInsert(typed, writeSlot!, entity, null, excludeUserAudio: true, suppressSound: true);
            }
            else
            {
                foundSlot = _itemSlots.TryGetAvailableSlot(typed, entity, null, out var writeSlotSwap, emptyOnly: false, allowSwap: true);
                if (foundSlot)
                {
                    var xform = _xformQuery.GetComponent(entity);
                    var gotDeletable = _itemSlots.TryEject(typed, writeSlotSwap!, null, out var removedItem, excludeUserAudio: true, xform.Coordinates, suppressSound: true);
                    if (gotDeletable)
                        QueueDel(removedItem);

                    _itemSlots.TryInsert(typed, writeSlotSwap!, entity, null, excludeUserAudio: true, suppressSound: true);
                }
            }
        }
    }
    // Starlight end
}

#region Starlight
internal sealed class PriorityStorageEquipContext
{
    public readonly HashSet<EntityUid> IssuedGear = [];
}
#endregion
