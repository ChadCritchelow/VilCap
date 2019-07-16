using PodioCore.Comments;
using PodioCore.Exceptions;
using PodioCore.Items;
using PodioCore.Models;
using PodioCore.Models.Request;
using PodioCore.Services;
using PodioCore.Utils;
using PodioCore.Utils.ItemFields;
using System;
using System.Collections.Generic;
using System.Linq;

namespace newVilcapCopyFileToGoogleDrive
{
    class TaskList2
    {

        PodioCollection<Item> filter;
        public async System.Threading.Tasks.Task<int> CreateTaskLists(newVilcapCopyFileToGoogleDrive vilcap)
        {

            // Admin/Utility vars //

            const int PARTITIONS = 5;
            const int LIMIT = 25;
            const int MAX_BATCHES = 10;
            const int MASTER_SCHEDULE_APP = 21310276;
            var batchNum = -1;

            var comm = new CommentService(vilcap.podio);
            string commentText;
            var count = 0;
            var fieldId = 0;
            var batchId = vilcap.ids.GetFieldId("Admin|TL Batch");
            var batch = vilcap.item.Field<CategoryItemField>(batchId).Options.First().Text;
            int.TryParse(batch, out batchNum);
            var tlPackageId = vilcap.ids.GetFieldId("Admin|Task List Selection");
            var tlPackageName = vilcap.item.Field<CategoryItemField>(tlPackageId).Options.First().Text;


            // Generate a rough calendar based on dates in the Admin app  //

            var scheduler = new Scheduler(vilcap.item, vilcap.e, vilcap.ids, PARTITIONS);

            // Get/Create View //

            var viewServ = new ViewService(vilcap.podio);
            vilcap.context.Logger.LogLine("Got View Service ...");
            var views = await viewServ.GetViews(MASTER_SCHEDULE_APP);
            var view = from v in views
                       where v.Name == tlPackageName
                       select v;

            if (view.Any())
            {
                vilcap.context.Logger.LogLine($"Got View '{tlPackageName}' ...");
            }
            else
            {
                vilcap.context.Logger.LogLine($"Creating View '{tlPackageName}' ...");
                var viewReq = new ViewCreateUpdateRequest
                {
                    Name = $"AWS - {tlPackageName}",
                    SortBy = "174999400", // fieldId of "Title"
                    Filters = new Dictionary<string, object>
                    {
                        {"185003953" /*Curriculum Package field*/, tlPackageName }
                    }
                };
                var viewId = await viewServ.CreateView(MASTER_SCHEDULE_APP, viewReq);
                view = from v in views
                       where v.Name == viewReq.Name
                       select v;
                vilcap.context.Logger.LogLine($"Got new View '{viewReq.Name}' ...");
            }

            var op = new FilterOptions { Filters = view.First().Filters };
            op.Limit = LIMIT;

            // Get Batch //

            if (0 <= batchNum && batchNum <= MAX_BATCHES)
            {
                op.Offset = op.Limit * (batchNum - 1);
                vilcap.context.Logger.LogLine($"Grabbing Items {op.Offset.Value + 1}-{op.Offset.Value + LIMIT} ...");
                filter = await vilcap.podio.FilterItems(MASTER_SCHEDULE_APP, op);
                vilcap.context.Logger.LogLine($"Items in filter:{filter.Items.Count()}");
                commentText = $"TL Batch {batch} finished.";
            }
            else
            {
                vilcap.context.Logger.LogLine("WARNING: No items found for batch!");
                commentText = "TL Batch # not recognized.";
            }

            // Main Loop //

            foreach (var master in filter.Items)
            {

                // Setup //

                count += 1;
                vilcap.context.Logger.LogLine($"On item #{count} ...");
                var child = new Item();

                //--- Assign Fields ---//	

                fieldId = vilcap.ids.GetFieldId("VC Administration|Master Schedule|Task Name");
                var nameMaster = master.Field<TextItemField>(fieldId);
                if (nameMaster.Value != null)
                {
                    fieldId = vilcap.ids.GetFieldId("Task List|Title");
                    var nameChild = child.Field<TextItemField>(fieldId);
                    nameChild.Value = nameMaster.Value;
                }

                fieldId = vilcap.ids.GetFieldId("VC Administration|Master Schedule|Desciption");
                var descrMaster = master.Field<TextItemField>(fieldId);
                if (descrMaster.Value != null)
                {
                    fieldId = vilcap.ids.GetFieldId("Task List|Description");
                    var descrChild = child.Field<TextItemField>(fieldId);
                    //descrChild.Value = StripHTML(descrMaster.Value);
                    descrChild.Value = descrMaster.Value;
                }

                fieldId = vilcap.ids.GetFieldId("VC Administration|Master Schedule|Phase");
                var phaseMaster = master.Field<CategoryItemField>(fieldId);
                if (phaseMaster.Options.Any())
                {
                    fieldId = vilcap.ids.GetFieldId("Task List|Phase");
                    var phaseChild = child.Field<CategoryItemField>(fieldId);
                    phaseChild.OptionText = phaseMaster.Options.First().Text;
                }

                fieldId = vilcap.ids.GetFieldId("VC Administration|Master Schedule|ESO Member Role");
                var esoMaster = master.Field<CategoryItemField>(fieldId);
                if (esoMaster.Options.Any())
                {
                    fieldId = vilcap.ids.GetFieldId("Task List|ESO Member Role");
                    var esoChild = child.Field<CategoryItemField>(fieldId);
                    esoChild.OptionText = esoMaster.Options.First().Text;
                }

                fieldId = vilcap.ids.GetFieldId("Task List|Completetion");
                var comChild = child.Field<CategoryItemField>(fieldId);
                comChild.OptionText = "Incomplete";

                fieldId = vilcap.ids.GetFieldId("VC Administration|Master Schedule|Dependancy");
                var depMaster = master.Field<TextItemField>(fieldId);
                if (depMaster.Value != null)
                {
                    fieldId = vilcap.ids.GetFieldId("Task List|Additional Dependencies");
                    var depChild = child.Field<TextItemField>(fieldId);
                    depChild.Value = depMaster.Value;
                }

                // Date Calcs //

                fieldId = vilcap.ids.GetFieldId("VC Administration|Master Schedule|Duration (Days)");
                var durMaster = master.Field<NumericItemField>(fieldId).Value.GetValueOrDefault(0.0);
                fieldId = vilcap.ids.GetFieldId("VC Administration|Master Schedule|Assignment Group");
                var assignment = master.Field<CategoryItemField>(fieldId);

                if (!assignment.Options.Any())
                {
                    continue;
                }
                Int32.TryParse(assignment.Options.First().Text, out var assignmentVal);
                child = scheduler.SetDate(child, vilcap.ids, phaseMaster.Options.First().Text, assignmentVal, durMaster);

                // GDrive Integration //

                fieldId = vilcap.ids.GetFieldId("VC Administration|Master Schedule|Gdrive Link");
                var embedMaster = master.Field<EmbedItemField>(fieldId);
                fieldId = vilcap.ids.GetFieldId("Task List|Linked Files");
                var embedChild = child.Field<EmbedItemField>(fieldId);
                var embeds = new List<Embed>();
                var parentFolderId = Environment.GetEnvironmentVariable("GOOGLE_PARENT_FOLDER_ID");
                var cloneFolderId = vilcap.google.GetSubfolderId(vilcap.service, vilcap.podio, vilcap.e, parentFolderId);//TODO:

                foreach (var em in embedMaster.Embeds)
                {
                    if (em.OriginalUrl.Contains(".google."))
                    {
                        await vilcap.google.UpdateOneEmbed(vilcap.service, em, embeds, cloneFolderId, vilcap.podio, vilcap.e);
                    }
                    //else          // Hold for 2.0 //
                    //{
                    //	NonGdriveLinks nonG = new NonGdriveLinks();
                    //	await nonG.NonGDriveCopy(em, embeds, vilcap.podio, e);
                    //}
                }

                foreach (var embed in embeds)
                {
                    embedChild.AddEmbed(embed.EmbedId);
                    vilcap.context.Logger.LogLine($"... Added field:{embedMaster.Label} ...");
                }

                // Child Item Creation //

                var taskListAppId = vilcap.ids.GetFieldId("Task List");
                var waitSeconds = 5;
            CallPodio:
                try
                {
                    await vilcap.podio.CreateItem(child, taskListAppId, true); //child task list appId
                }
                catch (PodioUnavailableException ex)
                {
                    vilcap.context.Logger.LogLine($"EXCEPTION '{ex.Message}'! Trying again in {waitSeconds} seconds ...");
                    for (var i = 0; i < waitSeconds; i++)
                    {
                        System.Threading.Thread.Sleep(1000);
                        vilcap.context.Logger.LogLine(".");
                    }
                    waitSeconds *= 2;
                    goto CallPodio;
                }
                vilcap.context.Logger.LogLine($"... Created item #{count}");
            }

            // Update Admin Item for next Batch //


            if (count == LIMIT)
            {
                return ++batchNum;
                //vilcap.item.Field<CategoryItemField>(vilcap.ids.GetFieldId("Admin|TL Batch")).OptionText = $"{ batchNum }";
                //await vilcap.podio.UpdateItem(vilcap.item, hook: true);    
                //ItemService iserv = new ItemService(vilcap.podio);
                //await iserv.UpdateItem(vilcap.item);
            }
            else
            {
                commentText += " All Tasklist items added!";
                return -1;
            }

        }
    }
}