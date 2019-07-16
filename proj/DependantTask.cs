using System;
using System.Collections.Generic;
using Amazon.Lambda.Core;
using Google.Apis.Drive.v3;
using PodioCore;
using PodioCore.Exceptions;
using PodioCore.Items;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using System.Linq;
using Task = System.Threading.Tasks.Task;

namespace newVilcapCopyFileToGoogleDrive
{
    class DependantTask
    {
        // Public Vars //
        int fieldId;
        Item child = new Item();


        public async Task CreateDependantTask(ILambdaContext context, Podio podio, Item trigger, Item master, RoutedPodioEvent e, DriveService service, GetIds ids, GoogleIntegration google)
        {

            //--- Assign Fields ---//	

            fieldId = ids.GetFieldId("VC Administration|Master Schedule|Task Name");
            var nameMaster = master.Field<TextItemField>(fieldId);
            if (nameMaster.Value != null)
            {
                fieldId = ids.GetFieldId("Task List|Title");
                var nameChild = child.Field<TextItemField>(fieldId);
                nameChild.Value = nameMaster.Value;
            }

            fieldId = ids.GetFieldId("VC Administration|Master Schedule|Desciption");
            var descrMaster = master.Field<TextItemField>(fieldId);
            if (descrMaster.Value != null)
            {
                fieldId = ids.GetFieldId("Task List|Description");
                var descrChild = child.Field<TextItemField>(fieldId);
                //descrChild.Value = StripHTML(descrMaster.Value);
                descrChild.Value = descrMaster.Value;
            }

            fieldId = ids.GetFieldId("VC Administration|Master Schedule|Phase");
            var phaseMaster = master.Field<CategoryItemField>(fieldId);
            if (phaseMaster.Options.Any())
            {
                fieldId = ids.GetFieldId("Task List|Phase");
                var phaseChild = child.Field<CategoryItemField>(fieldId);
                phaseChild.OptionText = phaseMaster.Options.First().Text;
            }

            fieldId = ids.GetFieldId("VC Administration|Master Schedule|ESO Member Role");
            var esoMaster = master.Field<CategoryItemField>(fieldId);
            if (esoMaster.Options.Any())
            {
                fieldId = ids.GetFieldId("Task List|ESO Member Role");
                var esoChild = child.Field<CategoryItemField>(fieldId);
                esoChild.OptionText = esoMaster.Options.First().Text;
            }

            fieldId = ids.GetFieldId("Task List|Completetion");
            var comChild = child.Field<CategoryItemField>(fieldId);
            comChild.OptionText = "Incomplete";

            fieldId = ids.GetFieldId("VC Administration|Master Schedule|Dependancy");
            var depMaster = master.Field<TextItemField>(fieldId);
            if (depMaster.Value != null)
            {
                fieldId = ids.GetFieldId("Task List|Additional Dependencies");
                var depChild = child.Field<TextItemField>(fieldId);
                depChild.Value = depMaster.Value;
            }

            // GDrive Integration //

            fieldId = ids.GetFieldId("VC Administration|Master Schedule|Gdrive Link");
            var embedMaster = master.Field<EmbedItemField>(fieldId);
            fieldId = ids.GetFieldId("Task List|Linked Files");
            var embedChild = child.Field<EmbedItemField>(fieldId);
            var embeds = new List<Embed>();
            var parentFolderId = Environment.GetEnvironmentVariable("GOOGLE_PARENT_FOLDER_ID");
            var cloneFolderId = google.GetSubfolderId(service, podio, e, parentFolderId); //TODO:

            foreach (var em in embedMaster.Embeds)
            {
                if (em.OriginalUrl.Contains(".google."))
                {
                    await google.UpdateOneEmbed(service, em, embeds, cloneFolderId, podio, e);
                }
                //else          // Hold for 2.0 //
                //{
                //	NonGdriveLinks nonG = new NonGdriveLinks();
                //	await nonG.NonGDriveCopy(em, embeds, podio, e);
                //}
            }

            // Create the actual Podio Item //

            var taskListAppId = ids.GetFieldId("Task List");
            var waitSeconds = 5;
            CallPodio:
            try
            {
                await podio.CreateItem(child, taskListAppId, true); //child task list appId
            }
            catch (PodioUnavailableException ex)
            {
                context.Logger.LogLine($"EXCEPTION '{ex.Message}'! Trying again in {waitSeconds} seconds ...");
                for (var i = 0; i < waitSeconds; i++)
                {
                    System.Threading.Thread.Sleep(1000);
                    context.Logger.LogLine(".");
                }
                waitSeconds *= 2;
                goto CallPodio;
            }
        }

    }
}