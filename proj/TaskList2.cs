using System;
using System.Collections.Generic;
using Amazon.Lambda.Core;
using Google.Apis.Drive.v3;
using PodioCore;
using PodioCore.Exceptions;
using PodioCore.Items;
using PodioCore.Models;
using PodioCore.Models.Request;
using PodioCore.Services;
using PodioCore.Utils.ItemFields;
using System.Linq;
using System.Text.RegularExpressions;
using PodioCore.Utils;
using PodioCore.Comments;
using static PodioCore.Utils.ItemFields.CategoryItemField;

namespace newVilcapCopyFileToGoogleDrive
{
    class TaskList2
	{

		PodioCollection<Item> filter;
		public static string StripHTML(string input)
		{
			return Regex.Replace(input, "<.*?>", String.Empty);
		}
		public async System.Threading.Tasks.Task CreateTaskLists(ILambdaContext context, Podio podio, Item check, RoutedPodioEvent e, DriveService service, GetIds ids, GoogleIntegration google,PreSurvAndExp pre)
		{

            // Admin/Utility vars //

            const int PARTITIONS = 5;
            const int LIMIT = 25;
            const int MAX_BATCHES = 10;
            const int MASTER_SCHEDULE_APP = 21310276;

            string commentText;
            int count = 0;
            int fieldId = 0;
            var batchId = ids.GetFieldId("Admin|TL Batch");
            var batch = check.Field<CategoryItemField>(batchId).Options.First().Text;
            Int32.TryParse(batch, out int batchNum);
            var tlPackageId = ids.GetFieldId("Admin|Task List Selection");
            var tlPackageName = check.Field<CategoryItemField>(tlPackageId).Options.First().Text;

            // Generate a rough calendar based on dates in the Admin app  //

            Scheduler scheduler = new Scheduler(context, podio, check, e, ids, PARTITIONS);

            // Get/Create View //

            var viewServ = new ViewService(podio);
			context.Logger.LogLine("Got View Service ...");
			var views = await viewServ.GetViews(MASTER_SCHEDULE_APP);
            var view = from v in views
                       where v.Name == tlPackageName
                       select v;

            if (view.Any())
            {
                context.Logger.LogLine($"Got View '{tlPackageName}' ...");
            }
            else
            {
                context.Logger.LogLine($"Creating View '{tlPackageName}' ...");
                var viewReq = new ViewCreateUpdateRequest();
                viewReq.Name = $"AWS - {tlPackageName}";
                viewReq.SortBy = "174999400"; // fieldId of "Title"
                viewReq.Filters = new Dictionary<string, object>
                {
                    {"185003953" /*Curriculum package*/, tlPackageName }
                };
                var viewId = await viewServ.CreateView(MASTER_SCHEDULE_APP, viewReq);
                view = from v in views
                       where v.Name == viewReq.Name
                       select v;
                context.Logger.LogLine($"Got new View '{viewReq.Name}' ...");
            }

            var op = new FilterOptions{ Filters = view.First().Filters };
            op.Limit = LIMIT;

            // Get Batch //

            if (0 <= batchNum && batchNum <= MAX_BATCHES)
            {
                op.Offset = op.Limit * (batchNum - 1);
                context.Logger.LogLine($"Grabbing Items {op.Offset.Value + 1}-{op.Offset.Value + LIMIT} ...");
                filter = await podio.FilterItems(MASTER_SCHEDULE_APP, op);
                context.Logger.LogLine($"Items in filter:{filter.Items.Count()}");
                commentText = $"TL Batch {batch} finished.";
            }
            else
            {
                context.Logger.LogLine("WARNING: No items found for batch!");
                commentText = "TL Batch # not recognized.";
            }

            // Main Loop //
        
            foreach (var master in filter.Items)
			{

                // Setup //

                count += 1;
				context.Logger.LogLine($"On item #{count} ...");
				Item child = new Item();

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
					descrChild.Value = StripHTML(descrMaster.Value);
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

                // Date Calcs //

                fieldId = ids.GetFieldId("VC Administration|Master Schedule|Duration (Days)");
                var durMaster = master.Field<NumericItemField>(fieldId).Value.Value;
                fieldId = ids.GetFieldId("VC Administration|Master Schedule|Assignment Group");
                var assignment = master.Field<CategoryItemField>(fieldId);
                Int32.TryParse(assignment.Options.First().Text, out int assignmentVal);

                child = scheduler.SetDate(child, ids, phaseMaster.Options.First().Text, assignmentVal, durMaster);
                
                // GDrive Integration //

                fieldId = ids.GetFieldId("VC Administration|Master Schedule|Gdrive Link");
				var embedMaster = master.Field<EmbedItemField>(fieldId);
				fieldId = ids.GetFieldId("Task List|Linked Files");
				var embedChild = child.Field<EmbedItemField>(fieldId);
				List<Embed> embeds = new List<Embed>();
				string parentFolderId = System.Environment.GetEnvironmentVariable("GOOGLE_PARENT_FOLDER_ID");
				var cloneFolderId = google.GetSubfolderId(service, podio, e, parentFolderId);//TODO:

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
				foreach (var embed in embeds)
				{
					embedChild.AddEmbed(embed.EmbedId);
                    context.Logger.LogLine($"... Added field:{embedMaster.Label} ...");
                }
				
				var taskListAppId = ids.GetFieldId("Task List");
				int waitSeconds = 5;
				CallPodio:
				try
				{
					await podio.CreateItem(child, taskListAppId, true); //child task list appId
				}
				catch (PodioUnavailableException ex)
				{
					context.Logger.LogLine($"EXCEPTION '{ex.Message}'! Trying again in {waitSeconds} seconds ...");
					for (int i = 0; i < waitSeconds; i++)
					{
						System.Threading.Thread.Sleep(1000);
						context.Logger.LogLine(".");
					}
					waitSeconds = waitSeconds * 2;
					goto CallPodio;
				}
				context.Logger.LogLine($"... Created item #{count}");
			}

            // Update Admin Item for next Batch

            CommentService comm = new CommentService(podio);
            if (count == LIMIT)
            {
                batchNum++;
                batchId = ids.GetFieldId("Admin|TL Batch");
                check.Field<CategoryItemField>(batchId).OptionText = $"{ batchNum }";
                await podio.UpdateItem(check, hook: true);    
            }
            else
            {
                commentText += " All Tasklist items added!";
            }
			await comm.AddCommentToObject("item", check.ItemId, commentText, hook: true);  

        }
	}
}