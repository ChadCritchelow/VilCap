using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace newVilcapCopyFileToGoogleDrive
{
    public class GetIds
	{
		Dictionary<string, string> dictChild;
		Dictionary<string, string> dictMaster;
		Dictionary<string, string> fullNames;
		string envId;

		public GetIds(
			Dictionary<string, string> _dictChild,
			Dictionary<string, string> _dictMaster,
			string _envId)
		{
			dictChild = _dictChild;
			dictMaster = _dictMaster;
            fullNames = new Dictionary<string, string>()
            {
                {"toolkittemplate3", "VC Toolkit Template 3" }
            };
            envId = _envId;
		}
		public int GetFieldId(string key)
		{
			var parts = key.Split('|');
			if (parts.Count() < 3)
			{
				return Convert.ToInt32(dictChild[$"{fullNames[envId]}|{key}"]);
			}
			else
				return Convert.ToInt32(dictMaster[key]);

		}
	}
}
