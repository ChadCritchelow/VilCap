using System;
using System.Collections.Generic;
using System.Linq;

namespace newVilcapCopyFileToGoogleDrive
{
    public class GetIds
	{
		Dictionary<string, string> dictChild;
		Dictionary<string, string> dictMaster;
		string envId;

		public GetIds(
			Dictionary<string, string> _dictChild,
			Dictionary<string, string> _dictMaster,
			string _envId)
		{
			dictChild = _dictChild;
			dictMaster = _dictMaster;
            envId = _envId;
		}
        /// <summary>
        /// Returns an appId or fieldId
        /// </summary>
        /// <param name="key">Some permutation of "WorkspaceName|ApplicationName|FieldName"</param>
		public int GetFieldId(string key)
		{
			var parts = key.Split('|');
			if (parts.Count() < 3)
			{
				return Convert.ToInt32(dictChild[$"{dictChild[$"{envId}-FN"]}|{key}"]);
			}
			else
            {
                return Convert.ToInt32(dictMaster[key]);
            }
        }
		public string GetLongName(string key)
		{
			return dictChild[key];
		}
	}
}
