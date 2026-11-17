using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ArmorVisibility
{
	public class ArmorVisibility : Mod
	{
		internal enum MessageType : byte
		{
			SyncVisibility
		}

		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			var type = (MessageType)reader.ReadByte();
			switch (type)
			{
				case MessageType.SyncVisibility:
					int player = reader.ReadByte();
					int mask = reader.ReadInt32();
					if (Main.netMode == NetmodeID.Server)
						player = whoAmI; // a client may only change its own state

					var modPlayer = Main.player[player].GetModPlayer<ArmorVisibilityPlayer>();
					modPlayer.Mask = mask;

					if (Main.netMode == NetmodeID.Server)
						modPlayer.SendSync(-1, whoAmI); // relay to everyone else
					break;
				default:
					Logger.Warn($"Unknown message type {type}");
					break;
			}
		}
	}
}
