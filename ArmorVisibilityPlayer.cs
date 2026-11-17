using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ArmorVisibility
{
	public class ArmorVisibilityPlayer : ModPlayer
	{
		/// <summary>
		/// Bit N set = Player.armor[N] is hidden.
		/// Used slots: 0-2 armor, 10-12 vanity armor, 13-19 vanity accessories.
		/// </summary>
		public int Mask;

		public static bool IsToggleableSlot(int armorSlot)
			=> armorSlot is >= 0 and <= 2 or >= 10 and <= 19;

		public bool IsHidden(int armorSlot) => (Mask & (1 << armorSlot)) != 0;

		public void Toggle(int armorSlot)
		{
			Mask ^= 1 << armorSlot;			// Change is picked up by SendClientChanges and synced automatically.
		}

		public override void FrameEffects()
		{
			// Runs after vanilla (and earlier mods) picked head/body/legs, including vanity overrides.
			// Only items that are really shown are replaced; transformations and other mods'
			// overrides are left alone.
			Player.head = Resolve(Player.head, Player.armor[0].headSlot, Player.armor[10].headSlot, 0);
			Player.body = Resolve(Player.body, Player.armor[1].bodySlot, Player.armor[11].bodySlot, 1);
			Player.legs = Resolve(Player.legs, Player.armor[2].legSlot, Player.armor[12].legSlot, 2);
		}

		private int Resolve(int shown, int functional, int vanity, int slot)
		{
			bool hideArmor = IsHidden(slot);
			bool hideVanity = IsHidden(slot + 10);

			if (hideVanity && vanity >= 0 && shown == vanity)
				return functional >= 0 && !hideArmor ? functional : -1; // fall back to the armor piece
			if (hideArmor && functional >= 0 && shown == functional && (vanity < 0 || hideVanity))
				return -1;
			return shown;
		}

		public override void SaveData(TagCompound tag)
		{
			if (Mask != 0)
				tag["hiddenMask"] = Mask;
		}

		public override void LoadData(TagCompound tag)
		{
			if (tag.ContainsKey("hiddenMask"))
				Mask = tag.GetInt("hiddenMask");
			else
				Mask = tag.GetByte("hidden"); // v1.0 format: bits 0-2 = armor slots 0-2
		}

		public void SendSync(int toWho, int fromWho)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)ArmorVisibility.MessageType.SyncVisibility);
			packet.Write((byte)Player.whoAmI);
			packet.Write(Mask);
			packet.Send(toWho, fromWho);
		}

		public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
		{
			SendSync(toWho, fromWho);
		}

		public override void CopyClientState(ModPlayer targetCopy)
		{
			((ArmorVisibilityPlayer)targetCopy).Mask = Mask;
		}

		public override void SendClientChanges(ModPlayer clientPlayer)
		{
			if (((ArmorVisibilityPlayer)clientPlayer).Mask != Mask)
				SendSync(-1, -1);
		}

		public override void ProcessTriggers(Terraria.GameInput.TriggersSet triggersSet)
		{
			for (int i = 0; i < 3; i++)
			{
				if (ArmorVisibilitySystem.ToggleKeys[i]?.JustPressed == true)
				{
					Toggle(i);
					Main.NewText(ArmorVisibilitySystem.StateText(i, IsHidden(i)), 180, 200, 255);
				}
			}
		}
	}
}
