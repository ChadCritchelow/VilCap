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

			string commentText;
            int fieldId = 0;
            int count = 0;
            var batch = check.Field<CategoryItemField>(ids.GetFieldId("Admin|TL Batch")).Options.First().Text;
            var tlPackageId = ids.GetFieldId("Admin|Task List Selection");
            var tlPackageName = check.Field<CategoryItemField>(tlPackageId).Options.First().Text;
            const int PARTITIONS = 5;

            // Get Timespans //

            var programDeId = ids.GetFieldId("Admin|Program Design");
            var programDeStart = new DateTime(check.Field<DateItemField>(programDeId).Start.Value.Ticks);
            var programDeTSpan = (check.Field<DateItemField>(programDeId).End.Value - programDeStart) / PARTITIONS;
            var recruitmeId = ids.GetFieldId("Admin|Recruitment Phase");
            var recruitmeStart = new DateTime(check.Field<DateItemField>(recruitmeId).Start.Value.Ticks);
            var recruitmeTSpan = (check.Field<DateItemField>(recruitmeId).End.Value - recruitmeStart) / PARTITIONS;
            var selectionId = ids.GetFieldId("Admin|Selection");
            var selectionStart = new DateTime(check.Field<DateItemField>(selectionId).Start.Value.Ticks);
            var selectionTSpan = (check.Field<DateItemField>(selectionId).End.Value - selectionStart) / PARTITIONS;
            var workshopOId = ids.GetFieldId("Admin|Workshop Operations");
            var workshopOStart = new DateTime(check.Field<DateItemField>(workshopOId).Start.Value.Ticks);
            var workshopOTSpan = (check.Field<DateItemField>(workshopOId).End.Value - workshopOStart) / PARTITIONS;

            // Get View //

            var viewServ = new ViewService(podio);
			context.Logger.LogLine("Got View Service ...");
			var views = await viewServ.GetViews(21310276); //VC Admin Master Schedule App
            var view = from v in views
                       where v.Name == tlPackageName
                       select v;
            context.Logger.LogLine($"Got View '{tlPackageName}'");
            var op = new FilterOptions{ Filters = view.First().Filters };
            op.Limit = 25;

            // Get Batch //

            switch (batch)
            {
                case "1":
                    context.Logger.LogLine("Grabbing items 1-25");
                    op.Offset = 0;
                    filter = await podio.FilterItems(21310276, op);
                    commentText = "TL Batch 1 finished";
                    break;
                case "2":
                    context.Logger.LogLine("Grabbing items 26-50");
                    op.Offset = 25;
                    filter = await podio.FilterItems(21310276, op);
                    commentText = "TL Batch 2 finished";
                    break;
                case "3":
                    context.Logger.LogLine("Grabbing items 51-75");
                    op.Offset = 50;
                    filter = await podio.FilterItems(21310276, op);
                    commentText = "TL Batch 3 finished";
                    break;
                default:
                    context.Logger.LogLine("ERROR: Invalid Batch #");
                    commentText = "TL Batch # not recognized";
                    break;
            }
            context.Logger.LogLine($"Items in filter:{filter.Items.Count()}");

            // Main Loop //
        
            foreach (var master in filter.Items)
			{

                // Setup //

                count += 1;
				context.Logger.LogLine($"On item #: {count}");
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
                var assignmentVal = 0;
                Int32.TryParse(assignment.Options.First().Text, out assignmentVal);
                assignmentVal--;

                if (true)
                {
                    fieldId = ids.GetFieldId("Task List|Date");
                    var date = child.Field<DateItemField>(fieldId);
                    switch (phaseMaster.Options.First().Text)
                    {
                        case "Program Design":
                            date.Start = programDeStart.Add(programDeTSpan * assignmentVal);
                            date.End = date.Start.Value.AddDays(durMaster);
                            break;
                        case "Recruitment Phase":
                            date.Start = recruitmeStart.Add(recruitmeTSpan * assignmentVal);
                            date.End = date.Start.Value.AddDays(durMaster);
                            break;
                        case "Recruitment":
                            date.Start = selectionStart.Add(selectionTSpan * assignmentVal);
                            date.End = date.Start.Value.AddDays(durMaster);
                            break;
                        case "Workshop Operations":
                            date.Start = workshopOStart.Add(workshopOTSpan * assignmentVal);
                            date.End = date.Start.Value.AddDays(durMaster);
                            break;
                        default:
                            break;
                    }
                    context.Logger.LogLine($"Scheduled for {date.Start.ToString()} - {date.End.ToString()}");
                }

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
				}
				context.Logger.LogLine($"Added field:{embedMaster.Label}");
				var taskListAppId = ids.GetFieldId("Task List");
				int waitSeconds = 5;
				CallPodio:
				try
				{
					await podio.CreateItem(child, taskListAppId, true); //child task list appId
				}
				catch (PodioUnavailableException ex)
				{
					context.Logger.LogLine($"{ex.Message}");
					context.Logger.LogLine($"Trying again in {waitSeconds} seconds.");
					for (int i = 0; i < waitSeconds; i++)
					{
						System.Threading.Thread.Sleep(1000);
						context.Logger.LogLine(".");
					}
					waitSeconds = waitSeconds * 2;
					goto CallPodio;
				}
				context.Logger.LogLine($"Created item #{count}");
			}

			CommentService comm = new CommentService(podio);
			await comm.AddCommentToObject("item", check.ItemId, commentText, hook: true);

		}
	}
}