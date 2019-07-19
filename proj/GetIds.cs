using System;
using System.Collections.Generic;
using System.Linq;

namespace newVilcapCopyFileToGoogleDrive
{
    public class GetIds
    {
        readonly Dictionary<string, string> dictChild;
        readonly Dictionary<string, string> dictMaster;
        readonly string envId;

        public GetIds(Dictionary<string, string> _dictChild, Dictionary<string, string> _dictMaster, string _envId)
        {
            dictChild = _dictChild;
            dictMaster = _dictMaster;
            envId = _envId;
        }
        /// <summary>
        /// Returns an appId or fieldId
        /// </summary>
        /// <param name="key">Some permutation of "WorkspaceName|ApplicationName|FieldName"</param>
		public int Get(string key) => key.Split('|').Count() < 3 ? Convert.ToInt32(dictChild[$"{dictChild[$"{envId}-FN"]}|{key}"]) : Convert.ToInt32(dictMaster[key]);

        public string GetLongName(string key) => dictChild[key];
    }
}
