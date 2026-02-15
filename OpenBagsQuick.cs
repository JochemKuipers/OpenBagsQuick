using System;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace OpenBagsQuick
{
    public class OpenBagsQuick : Mod
    {
        public int Speed = 1;
        public Item LastItem;
        private MethodInfo _itemSlotTryOpenContainer;

        // Variables to track hover state
        private int _hoverContext = -1;
        private int _hoverSlot = -1;
        private Item[] _hoverInv;
        private int _tickCounter;
        private const int TicksPerOpen = 2; // Adjust this value to control open speed
        private const int BaseBagsPerTick = 5; // Base number of bags to open per tick

        public override void Load()
        {
            // Initialize the MethodInfo for TryOpenContainer
            _itemSlotTryOpenContainer = typeof(ItemSlot).GetMethod("TryOpenContainer", BindingFlags.Static | BindingFlags.NonPublic);

            // Hook into the ItemSlot.MouseHover method to detect hover
            On_ItemSlot.MouseHover_ItemArray_int_int += On_ItemSlotOnMouseHover_ItemArray_int_int;

            // Hook into update to process at a consistent tick rate
            On_Player.Update += On_Player_Update;
        }

        public override void Unload()
        {
            // Unhook when the mod is unloaded
            On_ItemSlot.MouseHover_ItemArray_int_int -= On_ItemSlotOnMouseHover_ItemArray_int_int;
            On_Player.Update -= On_Player_Update;
        }

        private void On_ItemSlotOnMouseHover_ItemArray_int_int(On_ItemSlot.orig_MouseHover_ItemArray_int_int orig, Item[] inv, int context, int slot)
        {
            orig(inv, context, slot);

            // Just store which item we're hovering over
            _hoverContext = context;
            _hoverSlot = slot;
            _hoverInv = inv;
        }

        private void On_Player_Update(On_Player.orig_Update orig, Player player, int i)
        {
            orig(player, i);

            // Only process for the local player
            if (i != Main.myPlayer)
                return;

            // Reset hover tracking when not holding right click
            if (!Main.mouseRight)
            {
                Speed = 1;
                LastItem = null;
                _tickCounter = 0;
                return;
            }

            // Check if we're hovering over a valid inventory slot in the player's own inventory
            if (_hoverContext != 0 || _hoverSlot < 0 || _hoverInv == null || _hoverInv != player.inventory)
                return;

            var hoveredItem = _hoverInv[_hoverSlot];

            // Check if the item is right-clickable and a grab bag
            if (!IsGrabBag(hoveredItem))
                return;

            // Use the tick counter to control rate
            _tickCounter++;
            if (_tickCounter < TicksPerOpen / Math.Max(1, Speed)) return;
            _tickCounter = 0;

            // Process bag opening - now opens multiple bags per tick
            var bagsToOpen = BaseBagsPerTick * Speed;
            for (var bagCount = 0; bagCount < bagsToOpen; bagCount++)
            {
                // Stop if we're out of bags
                if (hoveredItem.stack <= 0)
                    break;

                // Try to open the container
                _itemSlotTryOpenContainer.Invoke(null,
                [
                    hoveredItem,
                    player
                ]);
            }

            // Increase speed if opening the same item repeatedly
            if (LastItem != null && LastItem.type == hoveredItem.type)
            {
                Speed = Math.Min(Speed + 1, 10); // Cap the max speed
            }
            LastItem = hoveredItem.Clone();
        }

        // Helper method to check if an item is a grab bag
        private static bool IsGrabBag(Item item)
        {
            // Skip empty items
            if (item.IsAir)
                return false;

            // Check for boss bags
            if (ItemID.Sets.BossBag[item.type])
                return true;

            // Check if the item drops loot when right-clicked
            if (Main.ItemDropsDB.GetRulesForItemID(item.type).Count != 0)
                return true;

            // For modded grab bags, we can check additional conditions
            return item.consumable && item.maxStack > 1 && ItemLoader.CanRightClick(item);
        }
    }
}