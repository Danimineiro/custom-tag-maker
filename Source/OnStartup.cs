using System.Collections.Generic;
using Verse;

namespace CTM
{
	[StaticConstructorOnStartup]
	public static class OnStartup
	{
		static OnStartup()
		{
			for (int i = 0; i < TagSettings.keys.Count; i++)
			{
				if (!TagSettings.modListPair.ContainsKey(TagSettings.keys[i]))
				{
					TagSettings.modListPair.Add(TagSettings.keys[i], new List<string>());
				}

				TagSettings.modListPair[TagSettings.keys[i]].Add(TagSettings.values[i]);
			}
		}
	}
}
