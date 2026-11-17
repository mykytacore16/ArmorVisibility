using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace ArmorVisibility
{
	/// <summary>
	/// Draws a small "eye" toggle on the armor, vanity armor and vanity accessory slots and
	/// handles clicks on it (blocking the slot's own click so the item isn't picked up).
	/// Uses only On_ detours of public methods, which chain with other mods' hooks.
	/// </summary>
	public class ArmorVisibilitySystem : ModSystem
	{
		internal static ModKeybind[] ToggleKeys = new ModKeybind[3];

		private const int SlotCount = 20; // Player.armor length

		// Eye rectangles from the latest draw of each slot (UI coordinates), indexed by armor slot.
		private static readonly Rectangle[] eyeRects = new Rectangle[SlotCount];
		private static readonly uint[] eyeDrawnAt = new uint[SlotCount];

		private static readonly string[] armorKeys = { "Head", "Body", "Legs" };

		public override void Load()
		{
			ToggleKeys[0] = KeybindLoader.RegisterKeybind(Mod, "ToggleHead", "None");
			ToggleKeys[1] = KeybindLoader.RegisterKeybind(Mod, "ToggleBody", "None");
			ToggleKeys[2] = KeybindLoader.RegisterKeybind(Mod, "ToggleLegs", "None");

			// Hiding vanity accessories: skip their visual update. Runs on every client for every
			// player, so other players see it too (the mask is synced).
			On_Player.UpdateVisibleAccessory += UpdateVisibleAccessory;

			if (Main.dedServ)
				return;

			On_ItemSlot.Draw_SpriteBatch_ItemArray_int_int_Vector2_Color += DrawSlot;
			On_ItemSlot.OverrideHover_ItemArray_int_int += OverrideHover;
			On_ItemSlot.LeftClick_ItemArray_int_int += LeftClick;
			On_ItemSlot.RightClick_ItemArray_int_int += RightClick;
			On_ItemSlot.MouseHover_ItemArray_int_int += MouseHover;
		}

		public override void Unload()
		{
			ToggleKeys = new ModKeybind[3];
		}

		private static void UpdateVisibleAccessory(On_Player.orig_UpdateVisibleAccessory orig, Player self, int itemSlot, Item item, bool modded)
		{
			if (!modded && itemSlot >= 13 && itemSlot < SlotCount
				&& self.GetModPlayer<ArmorVisibilityPlayer>().IsHidden(itemSlot))
				return;
			orig(self, itemSlot, item, modded);
		}

		internal static string StateText(int armorSlot, bool hidden)
		{
			string name = armorSlot switch
			{
				< 3 => Language.GetTextValue($"Mods.ArmorVisibility.UI.{armorKeys[armorSlot]}"),
				< 13 => Language.GetTextValue($"Mods.ArmorVisibility.UI.Vanity{armorKeys[armorSlot - 10]}"),
				_ => Language.GetTextValue("Mods.ArmorVisibility.UI.VanityAccessory"),
			};
			return Language.GetTextValue(hidden ? "Mods.ArmorVisibility.UI.Hidden" : "Mods.ArmorVisibility.UI.Visible", name);
		}

		private static bool IsOurSlot(Item[] inv, int context, int slot)
		{
			bool contextOk = slot switch
			{
				>= 0 and <= 2 => context == ItemSlot.Context.EquipArmor,
				>= 10 and <= 12 => context == ItemSlot.Context.EquipArmorVanity,
				>= 13 and < SlotCount => context == ItemSlot.Context.EquipAccessoryVanity,
				_ => false,
			};
			return contextOk && !Main.gameMenu && inv == Main.LocalPlayer.armor;
		}

		/// <summary>True if the mouse is on any eye drawn recently (eyes can overlap a neighbouring slot).</summary>
		private static bool MouseOnAnyEye()
		{
			for (int i = 0; i < SlotCount; i++)
			{
				if (Main.GameUpdateCount - eyeDrawnAt[i] <= 2 && eyeDrawnAt[i] != 0
					&& eyeRects[i].Contains(Main.mouseX, Main.mouseY))
					return true;
			}
			return false;
		}

		private static bool BlockSlot(Item[] inv) => !Main.gameMenu && inv == Main.LocalPlayer.armor && MouseOnAnyEye();

		private static void DrawSlot(On_ItemSlot.orig_Draw_SpriteBatch_ItemArray_int_int_Vector2_Color orig,
			SpriteBatch spriteBatch, Item[] inv, int context, int slot, Vector2 position, Color lightColor)
		{
			orig(spriteBatch, inv, context, slot, position, lightColor);

			if (!Main.playerInventory || !IsOurSlot(inv, context, slot))
				return;

			var modPlayer = Main.LocalPlayer.GetModPlayer<ArmorVisibilityPlayer>();
			bool hidden = modPlayer.IsHidden(slot);
			Texture2D tex = (hidden ? TextureAssets.InventoryTickOff : TextureAssets.InventoryTickOn).Value;

			// Same spot as vanilla's accessory toggle (AccessorySlotLoader.DrawVisibility:
			// x + 34, y - 2): the top-right corner, sitting on the slot border.
			var rect = new Rectangle((int)position.X + 34, (int)position.Y - 2, tex.Width, tex.Height);
			eyeRects[slot] = rect;
			eyeDrawnAt[slot] = Main.GameUpdateCount;

			// Hover/click is handled here because part of the eye lies outside the slot,
			// where the slot's own hover code isn't called. The slot hooks below only block
			// the slot's click/tooltip while the mouse is on an eye.
			if (rect.Contains(Main.mouseX, Main.mouseY) && !PlayerInput.IgnoreMouseInterface)
			{
				Main.LocalPlayer.mouseInterface = true;
				if (Main.mouseLeft && Main.mouseLeftRelease)
				{
					Main.mouseLeftRelease = false;
					modPlayer.Toggle(slot);
					hidden = !hidden;
					SoundEngine.PlaySound(SoundID.MenuTick);
				}
				Main.HoverItem = new Item();
				Main.hoverItemName = StateText(slot, hidden);
			}

			spriteBatch.Draw(tex, rect.TopLeft(), Color.White * 0.7f);
		}

		private static void OverrideHover(On_ItemSlot.orig_OverrideHover_ItemArray_int_int orig, Item[] inv, int context, int slot)
		{
			if (BlockSlot(inv))
				return;
			orig(inv, context, slot);
		}

		private static void LeftClick(On_ItemSlot.orig_LeftClick_ItemArray_int_int orig, Item[] inv, int context, int slot)
		{
			if (BlockSlot(inv))
				return; // the click is handled in DrawSlot
			orig(inv, context, slot);
		}

		private static void RightClick(On_ItemSlot.orig_RightClick_ItemArray_int_int orig, Item[] inv, int context, int slot)
		{
			if (BlockSlot(inv))
				return;
			orig(inv, context, slot);
		}

		private static void MouseHover(On_ItemSlot.orig_MouseHover_ItemArray_int_int orig, Item[] inv, int context, int slot)
		{
			if (BlockSlot(inv))
				return; // tooltip is set in DrawSlot
			orig(inv, context, slot);
		}
	}
}
