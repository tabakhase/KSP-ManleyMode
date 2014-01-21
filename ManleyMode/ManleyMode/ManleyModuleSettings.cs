using System;
using UnityEngine;

namespace ManleyMode
{
	public class ManleyModuleSettings
	{
		private static Settings mInstance;
		public static Settings Instance
		{
			get
			{
				return mInstance = mInstance ?? Settings.Load();
			}
		}
	}

	public class Settings
	{
		[Persistent] public bool ShowInNoUiMode = true;
		
		private static String File { 
			get { return KSPUtil.ApplicationRootPath + "/GameData/ManleyMode/ManleyMode_Settings.cfg"; }
		}

		public void Save()
		{
			try
			{
				ConfigNode save = new ConfigNode();
				ConfigNode.CreateConfigFromObject(this, 0, save);
				save.Save(File);
			}
			catch (Exception e) { Debug.Log("An error occurred while attempting to save: " + e.Message); }
		}

		public static Settings Load()
		{
			ConfigNode load = ConfigNode.Load(File);
			Settings settings = new Settings();
			if (load == null)
			{
				settings.Save();
				return settings;
			}
			ConfigNode.LoadObjectFromConfig(settings, load);

			return settings;
		}
	}
}

