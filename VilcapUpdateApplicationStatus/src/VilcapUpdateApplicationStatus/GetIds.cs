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
                {"toolkittemplate3", "Toolkit Template 3" },
                {"testuseducation2019", "TEST - US Education 2019" },
                // Older
                {"andela" ,"Andela"},
                {"anza" ,"Anza"},
                {"bluemoon" ,"blueMoon"},
                {"energygeneration" ,"Energy Generation"},
                {"energygeneration2", "Energy Generation 2" },
                {"entreprenarium" ,"Entreprenarium"},
                {"etrilabs" ,"Etrilabs"},
                {"globalentrepreneurshipnetwork" ,"Global Entrepreneurship Network (GEN) Freetown"},
                {"growthmosaic" ,"Growth Mosaic"},
                {"jokkolabs" ,"Jokkolabs"},
                {"privatesectorhealthallianceofnigeria" ,"Private Sector Health Alliance of Nigeria"},
                {"southernafricaventurepartnership" ,"Southern Africa Venture Partnership (SAVP)"},
                {"suguba" ,"Suguba"},
                {"sycomoreventure" ,"Sycomore Venture"},
                {"theinnovationvillage" ,"The Innovation Village"},
                {"universityofbritishcolumbia" ,"University of British Columbia"},
                {"venturesplatform" ,"Ventures Platform"},
                {"toolkittemplate" ,"VC Toolkit Template"},
                {"toolkittemplate2", "VC Toolkit Template 2" },
                {"usfintech2019" ,"US Fintech 2019" },
                {"useducation2019", "US Education 2019" },
                {"wepowerenvironment" ,"WePower" },
                {"middlegameventures", "Middlegame Ventures" }
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
