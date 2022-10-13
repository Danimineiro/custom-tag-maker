using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Steamworks;
using Verse;
using Verse.Steam;

namespace CTM
{
	[StaticConstructorOnStartup]
	public static class Patcher
	{
		static Patcher()
		{
			var harmony = new Harmony("dani.morePrecepts.diversityThoughtPatcher");
			harmony.PatchAll();

			foreach (ModContentPack modContentPack in LoadedModManager.RunningModsListForReading.Where(mod => !mod.ModMetaData.CanToUploadToWorkshop()))
			{
				modContentPack.ModMetaData.CanToUploadToWorkshop();
			}
		}
	}

	[HarmonyPatch]
	class Patches
	{
		[HarmonyPrefix]
		[HarmonyPatch(typeof(Workshop), "SetWorkshopItemDataFrom")]
		public static bool SetWorkshopItemDataFrom(UGCUpdateHandle_t updateHandle, WorkshopItemHook hook, bool creating)
		{
			hook.PrepareForWorkshopUpload();
			SteamUGC.SetItemTitle(updateHandle, hook.Name);
			if (creating)
			{
				SteamUGC.SetItemDescription(updateHandle, hook.Description);
			}
			if (!File.Exists(hook.PreviewImagePath))
			{
				Log.Warning("Missing preview file at " + hook.PreviewImagePath);
			}
			else
			{
				SteamUGC.SetItemPreview(updateHandle, hook.PreviewImagePath);
			}
			IList<string> tags = hook.Tags;
			foreach (System.Version version in hook.SupportedVersions)
			{
				tags.Add(version.Major + "." + version.Minor);
			}

			if (TagSettings.modListPair.ContainsKey(hook.Name))
			{

				foreach (string tag in TagSettings.modListPair.TryGetValue(hook.Name))
				{
					tags.Add(tag);
				}
			}

			SteamUGC.SetItemTags(updateHandle, tags);
			SteamUGC.SetItemContent(updateHandle, hook.Directory.FullName);

			return false;
		}
	}
}
