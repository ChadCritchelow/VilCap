using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace newVilcapCopyFileToGoogleDrive
{
    class GetIds
	{
		Dictionary<string, string> dictChild;
		Dictionary<string, string> dictMaster;
		Dictionary<string, string> fullNames;
		RoutedPodioEvent ev;


		public GetIds(
			Dictionary<string, string> _dictChild,
			Dictionary<string, string> _dictMaster,
			Dictionary<string, string> _fullNames,
			RoutedPodioEvent _ev)
		{
			dictChild = _dictChild;
			dictMaster = _dictMaster;
			fullNames = _fullNames;
			ev = _ev;
		}
		public int GetFieldId(string key)
		{
			var parts = key.Split('|');
			if (parts.Count() < 3)
			{
				return Convert.ToInt32(dictChild[$"{fullNames[ev.environmentId]}|{key}"]);
			}
			else
				return Convert.ToInt32(dictMaster[key]);

		}
	}
}
