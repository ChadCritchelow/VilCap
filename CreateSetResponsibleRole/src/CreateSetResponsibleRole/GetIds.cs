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
		/// <summary>
		/// This method returns the field or AppId related to the given key parameter
		/// </summary>
		/// <param name="key">Can contain 1 or 2 parts seperated by a '|'. If you are wanting an App ID use only the name of the app. If you are wanting a Field ID use the name of the app, followed by '|', followed by the field label name.</param>
		/// <returns></returns>
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
