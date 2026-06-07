using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

public class LootBagSaver : IModApi
{
    public void InitMod(Mod _modInstance)
    {
        var harmony = new Harmony("com.qol.lootbagsaver");
        harmony.PatchAll();
        Debug.Log("[LootBagSaver] Mod loaded. Container contents will be dropped on destruction.");
    }
}

/// <summary>
/// Intercepts World.RemoveTileEntity — the single authoritative call made whenever
/// a tile entity is permanently removed from the world (block destroyed, chunk unloaded
/// with destruction, etc.).  This is stable across all 7DTD 2.X.X versions because
/// it is a core world-management method that has not changed signature since A20.
///
/// Loot containers that are opened/looted normally are NOT affected because only
/// their item arrays are modified; the tile entity itself stays until the block is gone.
/// </summary>
[HarmonyPatch(typeof(World), "RemoveTileEntity")]
public class Patch_World_RemoveTileEntity
{
    static void Prefix(World __instance, Chunk _chunk, TileEntity _te)
    {
        // Only act on loot containers (covers TileEntitySecureLootContainer too,
        // since it inherits from TileEntityLootContainer)
        if (!(__instance is WorldBase world)) return;
        if (!(__instance.IsRemote() == false)) return; // server-side only
        if (!(_te is TileEntityLootContainer lootContainer)) return;

        ItemStack[] items = lootContainer.GetItems();
        if (items == null || items.Length == 0) return;

        Vector3 dropPos = new Vector3(
            _te.ToWorldPos().x + 0.5f,
            _te.ToWorldPos().y + 0.1f,
            _te.ToWorldPos().z + 0.5f
        );

        bool hadItems = false;
        foreach (ItemStack stack in items)
        {
            if (stack == null || stack.IsEmpty()) continue;
            hadItems = true;
            DropItemStack(__instance, dropPos, stack.Clone());
        }

        if (hadItems)
        {
            // Tell the Reset patch this position was already handled
            Patch_TileEntityLootContainer_Reset.MarkHandled(_te.ToWorldPos());
            Debug.Log($"[LootBagSaver] Saved container contents at {_te.ToWorldPos()} — items dropped as entities.");
        }
    }

    private static void DropItemStack(World world, Vector3 pos, ItemStack stack)
    {
        // Spread items slightly so they don't all occupy the exact same position
        Vector3 spread = new Vector3(
            UnityEngine.Random.Range(-0.3f, 0.3f),
            0f,
            UnityEngine.Random.Range(-0.3f, 0.3f)
        );

        EntityItem entityItem = (EntityItem)EntityFactory.CreateEntity(
            new EntityCreationData
            {
                entityClass   = EntityClass.FromString("item"),
                id            = EntityFactory.nextEntityID++,
                itemStack     = stack,
                pos           = pos + spread,
                rot           = new Vector3(0f, (float)UnityEngine.Random.Range(0, 360), 0f),
                lifetime      = 600f,   // 10 minutes before despawn
                belongsPlayerId = -1
            }
        );

        if (entityItem == null) return;

        world.SpawnEntityInWorld(entityItem);
    }
}

/// <summary>
/// Secondary safety net: if a TileEntityLootContainer is reset/cleared while its
/// block is in the process of being destroyed (stability collapse, explosion),
/// we capture items before they are wiped.
///
/// Uses a thread-local flag to avoid double-dropping when RemoveTileEntity
/// already handled it first.
/// </summary>
[HarmonyPatch(typeof(TileEntityLootContainer), "Reset")]
public class Patch_TileEntityLootContainer_Reset
{
    // Track tile entity positions we already handled via RemoveTileEntity
    private static readonly HashSet<Vector3i> _alreadyHandled = new HashSet<Vector3i>();

    public static void MarkHandled(Vector3i pos) => _alreadyHandled.Add(pos);

    static void Prefix(TileEntityLootContainer __instance)
    {
        // Reset is also called during normal gameplay (e.g. after respawn timers)
        // so we only act when the owning block no longer exists in the world
        World world = GameManager.Instance?.World;
        if (world == null) return;
        if (world.IsRemote()) return;

        Vector3i blockPos = __instance.ToWorldPos();

        if (_alreadyHandled.Remove(blockPos)) return;

        // Check if the block at this position is still a valid container block
        BlockValue bv = world.GetBlock(blockPos);
        if (bv.Block is BlockEntityData) return; // block still there — normal Reset, skip

        // Block is gone — this Reset is part of destruction; drop contents
        ItemStack[] items = __instance.GetItems();
        if (items == null) return;

        Vector3 dropPos = new Vector3(blockPos.x + 0.5f, blockPos.y + 0.1f, blockPos.z + 0.5f);
        bool hadItems = false;

        foreach (ItemStack stack in items)
        {
            if (stack == null || stack.IsEmpty()) continue;
            hadItems = true;
            DropItemStack(world, dropPos, stack.Clone());
        }

        if (hadItems)
            Debug.Log($"[LootBagSaver] (Reset fallback) Saved contents at {blockPos}.");
    }

    private static void DropItemStack(World world, Vector3 pos, ItemStack stack)
    {
        Vector3 spread = new Vector3(
            UnityEngine.Random.Range(-0.3f, 0.3f),
            0f,
            UnityEngine.Random.Range(-0.3f, 0.3f)
        );

        EntityItem entityItem = (EntityItem)EntityFactory.CreateEntity(
            new EntityCreationData
            {
                entityClass     = EntityClass.FromString("item"),
                id              = EntityFactory.nextEntityID++,
                itemStack       = stack,
                pos             = pos + spread,
                rot             = new Vector3(0f, (float)UnityEngine.Random.Range(0, 360), 0f),
                lifetime        = 600f,
                belongsPlayerId = -1
            }
        );

        if (entityItem != null)
            world.SpawnEntityInWorld(entityItem);
    }
}
