using Verse;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using Verse.Steam;
using Steamworks;
using UnityEngine;
using System.Linq;
using RimWorld;
using System.Threading;

namespace CustomTagMaker
{
	[StaticConstructorOnStartup]
	public static class Patcher
	{
		static Patcher()
		{
			var harmony = new Harmony("dani.morePrecepts.diversityThoughtPatcher");
			harmony.PatchAll();

			foreach(ModContentPack modContentPack in LoadedModManager.RunningModsListForReading.Where(mod => !mod.ModMetaData.CanToUploadToWorkshop()))
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
	
	[StaticConstructorOnStartup]
	public static class DicBuilder
    {
		static DicBuilder()
        {
			for (int i = 0; i < TagSettings.keys.Count; i++ )
            {
				if (!TagSettings.modListPair.ContainsKey(TagSettings.keys[i]))
				{
					TagSettings.modListPair.Add(TagSettings.keys[i], new List<string>());
				}

				TagSettings.modListPair[TagSettings.keys[i]].Add(TagSettings.values[i]);
			}
        }
    }

	public class TagSettings : ModSettings
	{
		public static Dictionary<string, List<string>> modListPair = new Dictionary<string, List<string>>();

		public static List<string> keys = new List<string>();
		public static List<string> values = new List<string>();

		static TagSettings() { }

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref keys, "keys");
			Scribe_Collections.Look(ref values, "values");
			Scribe_Values.Look(ref TagMod.offlineMode, "displayLocalMods");
		}
	}

	class TagMod : Mod
	{
		public static TagSettings settings;

		public static bool offlineMode = false;
		private readonly static float offset = 10f;
		private float scrollInRectHeight = float.MaxValue;
		private readonly Vector2 scrollBarArrowMod = new Vector2(0, 50);
		private Vector2 scrollPos = new Vector2();
		private bool updateScrollViewHeight = true;
		private string selectedMod;

		public TagMod(ModContentPack modContentPack) : base(modContentPack)
		{
			settings = GetSettings<TagSettings>();
		}

		public override string SettingsCategory()
		{
			return "[DN] Custom Tag Maker";
		}

        public override void WriteSettings()
		{
			TagSettings.keys.Clear();
			TagSettings.values.Clear();

			foreach (KeyValuePair<string, List<string>> keyValuePair in TagSettings.modListPair)
            {
				foreach (string str in keyValuePair.Value)
				{
					TagSettings.keys.Add(keyValuePair.Key);
					TagSettings.values.Add(str);
				}
			}

			settings.Write();
		}

        public override void DoSettingsWindowContents(Rect inRect)
		{
			GameFont prevFont = Text.Font;
			TextAnchor textAnchor = Text.Anchor;

			Event e = Event.current;
			if (e.isScrollWheel)
			{
				scrollPos += e.delta;
			}
			else if (e.isKey)
			{
				if (e.type != EventType.KeyUp)
				{
					switch (e.keyCode)
					{
						case KeyCode.DownArrow:
							scrollPos += scrollBarArrowMod;
							break;
						case KeyCode.UpArrow:

							scrollPos -= scrollBarArrowMod;
							break;
						default:
							break;
					}
				}
			}

			Listing_Standard ls = new Listing_Standard();
			ls.Begin(inRect);

			Rect DisplayAllLoadedModsRect = new Rect(0f, 0f, inRect.width / 2f - offset, 50f);
			Rect SelectModButtonRect = new Rect(DisplayAllLoadedModsRect.width + offset, 0f, inRect.width - (DisplayAllLoadedModsRect.width + offset), 50f);
			Rect ScrollInRect = new Rect(0f, 0f, inRect.width, scrollInRectHeight);

			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.UpperCenter;

			Widgets.CheckboxLabeled(DisplayAllLoadedModsRect, "Display all loaded local mods:\n\n", ref offlineMode, false, null, null, true);

			Text.Font = prevFont;
			Text.Anchor = textAnchor;

			if (Widgets.ButtonText(SelectModButtonRect, "Selected Mod:\n" + (selectedMod ?? "Click to select a mod")))
			{
				List<FloatMenuOption> list = new List<FloatMenuOption>();
				foreach (ModContentPack Content in LoadedModManager.RunningModsListForReading.Where(modContentPack => modContentPack.ModMetaData.Source == ContentSource.ModsFolder))
				{
					if (Content.ModMetaData.CanToUploadToWorkshop() || offlineMode)
					{
						list.Add(new FloatMenuOption(Content.Name, delegate ()
						{
							selectedMod = Content.Name;
							updateScrollViewHeight = true;
						}));
					}
				}
				if (!offlineMode || list.NullOrEmpty()) list.Add(new FloatMenuOption("If your mod isn't displayed here, try pressing the button again. (Steam slow)", null));
				Find.WindowStack.Add(new FloatMenu(list));
				
			}

			if (selectedMod != null)
			{
				inRect.height = 500;
				Widgets.BeginScrollView(inRect, ref scrollPos, ScrollInRect);

				Listing_Standard inner = new Listing_Standard();
				inner.Begin(ScrollInRect);
				inner.Gap();

				if (TagSettings.modListPair.ContainsKey(selectedMod))
				{

					for (int i = 0; i < TagSettings.modListPair[selectedMod].Count; i++)
					{
						string tag = TagSettings.modListPair[selectedMod][i];

						string temp = inner.TextEntry(tag);
						if (temp != null && temp != "")
						{
							TagSettings.modListPair[selectedMod][i] = temp;
						}
						else if (temp != null && temp == "")
						{
							TagSettings.modListPair[selectedMod].RemoveAt(i);
						}
					}
					string newTag = inner.TextEntry("");

					if (newTag != "")
					{
						TagSettings.modListPair[selectedMod].Add(newTag);
						updateScrollViewHeight = true;
					} 
				}
                else
                {
					TagSettings.modListPair.Add(selectedMod, new List<string>());
					updateScrollViewHeight = true;
				}


				scrollInRectHeight = inner.CurHeight;
				if (updateScrollViewHeight)
				{
					updateScrollViewHeight = !updateScrollViewHeight;
					scrollInRectHeight = float.MaxValue;
				}

				inner.End();
				Widgets.EndScrollView();
			}

			ls.End();
		}
	}
}
