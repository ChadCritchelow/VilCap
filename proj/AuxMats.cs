using Amazon.Lambda.Core;
using Google.Apis.Drive.v3;
using PodioCore;
using PodioCore.Items;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using System.Linq;
using System.Text.RegularExpressions;

namespace newVilcapCopyFileToGoogleDrive
{
    class AuxMats
    {

        public static string StripHTML(string input)
        {
            return Regex.Replace(input, "<.*?>", string.Empty);
        }

        public static async System.Threading.Tasks.Task CreateAuxMats(ILambdaContext context, Podio podio, Item check, RoutedPodioEvent e, DriveService service, GetIds ids, GoogleIntegration google, Item masterMat)
        {

            #region // Setup //

            Item masterM = new Item();
            context.Logger.LogLine("Creating empty master item");
            masterM = await podio.GetItem(masterMat.ItemId);
            context.Logger.LogLine("Got master item");
            Item cloneM = new Item();
            #endregion

            #region // Assign AuxMat Fields //

            var nameMasterMValue = masterM.Field<TextItemField>(ids.GetFieldId("VC Administration|Auxillary Material Curation|Task Name")).Value;
            if (nameMasterMValue != null)
            {
                var nameCloneT = cloneM.Field<TextItemField>(ids.GetFieldId("Task List|Title"));
                nameCloneT.Value = $"{nameMasterMValue}";
            }

            var descrMasterM = masterM.Field<TextItemField>(ids.GetFieldId("VC Administration|Auxillary Material Curation|Desciption"));
            if (descrMasterM.Value != null)
            {
                var descrCloneM = cloneM.Field<TextItemField>(ids.GetFieldId("Task List|Description"));
                descrCloneM.Value = StripHTML(descrMasterM.Value);
            }

            var typeMasterM = masterM.Field<CategoryItemField>(ids.GetFieldId("VC Administration|Auxillary Material Curation|Priority"));
            if (typeMasterM.Options.Any())
            {
                var typeCloneM = cloneM.Field<CategoryItemField>(ids.GetFieldId("Task List|Priority"));
                typeCloneM.OptionText = typeMasterM.Options.First().Text;
            }
            #endregion
        }
    }
}

