//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/InventoryEquipmentSwapRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证背包与所有装备槽可原子交换。
// 2. 验证非法交换不改变任何实例状态。
// 3. 验证满背包与连续交换不会复制或丢失装备。
//
// 不负责：
// × 模拟鼠标轨迹。
// × 验证动画的像素级视觉结果。
//
// 主要依赖：
// InventoryManager
// EquipmentDatabase
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 背包装备直接交换的独立Headless回归入口。
/// </summary>
public partial class InventoryEquipmentSwapRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行装备交换回归，并通过进程退出码返回测试状态。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            Run();
            GD.Print($"INVENTORY_EQUIPMENT_SWAP_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"INVENTORY_EQUIPMENT_SWAP_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run()
    {
        TestCategorySwap(EquipmentSlotCategory.Weapon, EquipmentSlot.Weapon);
        TestCategorySwap(EquipmentSlotCategory.Armor, EquipmentSlot.Armor);
        TestCategorySwap(EquipmentSlotCategory.Vehicle, EquipmentSlot.Vehicle);
        TestCategorySwap(EquipmentSlotCategory.Accessory, EquipmentSlot.Accessory2);
        TestCommonAccessorySlot();
        TestEmptySlotAndWrongType();
        TestOccupiedSlotEntryAndItemDropCallback();
        TestFullBackpackAndRepeatedSwap();
    }

    private void TestCategorySwap(EquipmentSlotCategory category, EquipmentSlot slot)
    {
        ResetRun();
        var pair = FindPair(category);
        var equipped = Add(pair.First);
        var backpack = Add(pair.Second);
        Assert(InventoryManager.EquipToSlot(equipped.InstanceId, slot), $"{category}初始装备失败");

        Assert(InventoryManager.CanSwapEquipment(backpack.InstanceId, equipped.InstanceId), $"{category}交换预检失败");
        Assert(InventoryManager.SwapEquipment(backpack.InstanceId, equipped.InstanceId), $"{category}交换失败");
        Assert(backpack.EquippedSlot == slot, $"{category}背包装备未进入目标槽");
        Assert(equipped.EquippedSlot == null, $"{category}原装备未返回背包");

        Assert(InventoryManager.SwapEquipment(equipped.InstanceId, backpack.InstanceId), $"{category}反向交换失败");
        Assert(equipped.EquippedSlot == slot && backpack.EquippedSlot == null, $"{category}反向交换位置错误");
    }

    private void TestCommonAccessorySlot()
    {
        ResetRun();
        var commonPair = FindPair(EquipmentSlotCategory.Accessory, EquipmentRarity.Common);
        var equipped = Add(commonPair.First);
        var backpack = Add(commonPair.Second);
        Assert(InventoryManager.EquipToSlot(equipped.InstanceId, EquipmentSlot.Accessory5), "普通饰品未能进入普通饰品槽");
        Assert(InventoryManager.SwapEquipment(backpack.InstanceId, equipped.InstanceId), "普通饰品槽交换失败");
        Assert(backpack.EquippedSlot == EquipmentSlot.Accessory5, "普通饰品槽交换结果错误");

        var epic = Add(FindOne(EquipmentSlotCategory.Accessory, EquipmentRarity.Epic));
        var before = SnapshotSlots();
        Assert(!InventoryManager.CanSwapEquipment(epic.InstanceId, backpack.InstanceId), "史诗饰品错误通过普通饰品槽预检");
        Assert(!InventoryManager.SwapEquipment(epic.InstanceId, backpack.InstanceId), "史诗饰品错误进入普通饰品槽");
        AssertSnapshotsEqual(before, SnapshotSlots(), "普通饰品槽拒绝操作后状态发生变化");
    }

    private void TestEmptySlotAndWrongType()
    {
        ResetRun();
        var weapon = Add(FindOne(EquipmentSlotCategory.Weapon));
        Assert(InventoryManager.EquipToSlot(weapon.InstanceId, EquipmentSlot.Weapon), "空武器槽装备失败");

        var armor = Add(FindOne(EquipmentSlotCategory.Armor));
        var before = SnapshotSlots();
        Assert(!InventoryManager.CanSwapEquipment(armor.InstanceId, weapon.InstanceId), "护甲错误通过武器槽交换预检");
        Assert(!InventoryManager.SwapEquipment(armor.InstanceId, weapon.InstanceId), "护甲错误交换到武器槽");
        AssertSnapshotsEqual(before, SnapshotSlots(), "非法类型交换后状态发生变化");
    }

    private void TestFullBackpackAndRepeatedSwap()
    {
        ResetRun();
        var pair = FindPair(EquipmentSlotCategory.Weapon);
        var equipped = Add(pair.First);
        var backpack = Add(pair.Second);
        Assert(InventoryManager.EquipToSlot(equipped.InstanceId, EquipmentSlot.Weapon), "满背包测试初始装备失败");

        var filler = FindOne(EquipmentSlotCategory.Accessory, EquipmentRarity.Common);
        for (var i = 0; i < 100; i++)
        {
            Add(filler);
        }

        var countBefore = InventoryManager.GetAllOwned().Count;
        var idsBefore = SnapshotIds();
        for (var i = 0; i < 50; i++)
        {
            Assert(InventoryManager.SwapEquipment(backpack.InstanceId, equipped.InstanceId), $"连续交换第{i + 1}次失败");
        }

        Assert(InventoryManager.GetAllOwned().Count == countBefore, "连续交换改变了装备总数");
        Assert(idsBefore.SetEquals(SnapshotIds()), "连续交换复制或丢失了装备实例");
        Assert(equipped.EquippedSlot == EquipmentSlot.Weapon && backpack.EquippedSlot == null, "偶数次交换后位置未恢复");
    }

    private void TestOccupiedSlotEntryAndItemDropCallback()
    {
        ResetRun();
        var pair = FindPair(EquipmentSlotCategory.Weapon);
        var equipped = Add(pair.First);
        var backpack = Add(pair.Second);
        Assert(InventoryManager.EquipToSlot(equipped.InstanceId, EquipmentSlot.Weapon), "占用槽测试初始装备失败");

        // 装备槽入口本身也必须走同一交换事务，而不是先卸下旧装备。
        Assert(InventoryManager.EquipToSlot(backpack.InstanceId, EquipmentSlot.Weapon), "占用槽直接替换失败");
        Assert(backpack.EquippedSlot == EquipmentSlot.Weapon && equipped.EquippedSlot == null, "占用槽没有完成引用交换");

        // 验证当前UI实际使用的物品格Drop回调可以完成反方向交换。
        var backpackItemUi = new InventoryItemUI
        {
            CanSwapDropped = InventoryManager.CanSwapEquipment,
            OnSwapDropped = (sourceId, targetId) => InventoryManager.SwapEquipment(sourceId, targetId)
        };
        AddChild(backpackItemUi);
        backpackItemUi.SetItem(equipped);
        var payload = InventoryDragPayload.Create(backpack.InstanceId);
        Assert(backpackItemUi._CanDropData(Vector2.Zero, payload), "背包物品格未接受已装备物品拖拽");
        backpackItemUi._DropData(Vector2.Zero, payload);
        Assert(equipped.EquippedSlot == EquipmentSlot.Weapon && backpack.EquippedSlot == null, "背包物品格反向交换失败");
        backpackItemUi.QueueFree();
    }

    private static (EquipmentDefinition First, EquipmentDefinition Second) FindPair(
        EquipmentSlotCategory category,
        EquipmentRarity? rarity = null)
    {
        EquipmentDefinition? first = null;
        foreach (var definition in EquipmentDatabase.GetAllEquipments())
        {
            if (!IsUsableDefinition(definition) || InventoryManager.GetSlotCategory(definition) != category)
            {
                continue;
            }

            if (rarity.HasValue && definition.Rarity != rarity.Value)
            {
                continue;
            }

            if (first == null)
            {
                first = definition;
                continue;
            }

            return (first, definition);
        }

        throw new InvalidOperationException($"找不到两个可测试的{category}装备。");
    }

    private static EquipmentDefinition FindOne(EquipmentSlotCategory category, EquipmentRarity? rarity = null)
    {
        foreach (var definition in EquipmentDatabase.GetAllEquipments())
        {
            if (IsUsableDefinition(definition)
                && InventoryManager.GetSlotCategory(definition) == category
                && (!rarity.HasValue || definition.Rarity == rarity.Value))
            {
                return definition;
            }
        }

        throw new InvalidOperationException($"找不到可测试的{category}/{rarity}装备。");
    }

    private static bool IsUsableDefinition(EquipmentDefinition definition)
    {
        return definition.Id != EquipmentIds.AttackChip
            && definition.Id != EquipmentIds.ExpansionChip
            && definition.Id != EquipmentIds.MysteriousChip;
    }

    private static OwnedEquipment Add(EquipmentDefinition definition)
    {
        return InventoryManager.AddToInventory(definition.Id, EquipmentGainSource.Developer)
            ?? throw new InvalidOperationException($"测试装备加入失败：{definition.Id}");
    }

    private static void ResetRun()
    {
        GameManager.BeginNewRun();
        GameManager.SelectCharacter(CharacterIds.ZhaoYun);
        InventoryManager.Reset();
    }

    private static Dictionary<Guid, EquipmentSlot?> SnapshotSlots()
    {
        var result = new Dictionary<Guid, EquipmentSlot?>();
        foreach (var item in InventoryManager.GetAllOwned())
        {
            result[item.InstanceId] = item.EquippedSlot;
        }
        return result;
    }

    private static HashSet<Guid> SnapshotIds()
    {
        var result = new HashSet<Guid>();
        foreach (var item in InventoryManager.GetAllOwned())
        {
            result.Add(item.InstanceId);
        }
        return result;
    }

    private void AssertSnapshotsEqual(
        IReadOnlyDictionary<Guid, EquipmentSlot?> expected,
        IReadOnlyDictionary<Guid, EquipmentSlot?> actual,
        string message)
    {
        Assert(expected.Count == actual.Count, message);
        foreach (var pair in expected)
        {
            Assert(actual.TryGetValue(pair.Key, out var slot) && slot == pair.Value, message);
        }
    }

    private void Assert(bool condition, string message)
    {
        _assertionCount++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
