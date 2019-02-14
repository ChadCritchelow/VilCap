using System;
using System.Collections.Generic;
using System.Text;
using Amazon.Lambda.Core;
using Google.Apis.Drive.v3;
using PodioCore;
using PodioCore.Exceptions;
using PodioCore.Items;
using PodioCore.Models;
using PodioCore.Models.Request;
using PodioCore.Services;
using PodioCore.Utils.ItemFields;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PodioCore.Utils;
using PodioCore.Comments;

namespace newVilcapCopyFileToGoogleDrive
{
	class TaskList
	{
		PodioCollection<Item> filter;
		public static string StripHTML(string input)
		{
			return Regex.Replace(input, "<.*?>", String.Empty);
		}
		public async System.Threading.Tasks.Task CreateTaskLists(ILambdaContext context, Podio podio, Item check, RoutedPodioEvent e, DriveService service, GetIds ids, GoogleIntegration google,PreSurvAndExp pre)
		{
			string commentText;
			var TlStatusId = ids.GetFieldId("Admin|Hidden Status");
			var startDateId = ids.GetFieldId("Admin|Program Start Date");
            var packageId = ids.GetFieldId("Admin|Curriculum Package");
            int fieldId = 0;

			context.Logger.LogLine("Satisfied conditions, Task List Function");
			var viewServ = new ViewService(podio);
			context.Logger.LogLine("Got View Service");
			var views = await viewServ.GetViews(21310276);//VC Admin Master Schedule App
            var view = from v in views
                       where v.Name == check.Field<CategoryItemField>(packageId).Options.First().Text;
                       select v;
			context.Logger.LogLine("Got View");
			var op = new FilterOptions();
			op.Filters = view.First().Filters;
			if (check.Field<CategoryItemField>(TlStatusId).Options.First().Text == "1")
			{
				context.Logger.LogLine("Grabbing items 1-30");
				op.Offset = 0;
				op.Limit = 30;
				filter = await podio.FilterItems(21310276, op);
				commentText = "Batch 1 finished";
			}
			else if (check.Field<CategoryItemField>(TlStatusId).Options.First().Text == "2")
			{
				context.Logger.LogLine("Grabbing items 31-60");
				op.Offset = 30;
				op.Limit = 30;
				filter = await podio.FilterItems(21310276, op);
				commentText = "Batch 2 finished";
			}
			else if (check.Field<CategoryItemField>(TlStatusId).Options.First().Text == "3")
			{
				context.Logger.LogLine("Grabbing items 61-90");
				op.Offset = 60;
				op.Limit = 30;
				filter = await podio.FilterItems(21310276, op);
				commentText = "Batch 3 finished";
			}
			else if (check.Field<CategoryItemField>(TlStatusId).Options.First().Text == "4")
			{
				context.Logger.LogLine("Grabbing items 91-120 with links");
				op.Offset = 90;
				op.Limit = 30;
				filter = await podio.FilterItems(21310276, op);
				commentText = "Batch 4 finished";
			}
			else if (check.Field<CategoryItemField>(TlStatusId).Options.First().Text == "5")
			{
				context.Logger.LogLine("Grabbing items 121-150 with links");
				op.Offset = 120;
				op.Limit = 30;
				filter = await podio.FilterItems(21310276, op);
				commentText = "Batch 5 finished";
			}
			else if (check.Field<CategoryItemField>(TlStatusId).Options.First().Text == "6")
			{
				context.Logger.LogLine("Grabbing items 151-180 with links");
				op.Offset = 150;
				op.Limit = 30;
				filter = await podio.FilterItems(21310276, op);
				commentText = "Batch 6 finished";
			}
			else if (check.Field<CategoryItemField>(TlStatusId).Options.First().Text == "7")
            {
				context.Logger.LogLine("Grabbing items 181-210 with links");
				op.Offset = 180;
				op.Limit = 30;
				filter = await podio.FilterItems(21310276, op);
				commentText = "Batch 7 finished";
			}
            else
            {
                context.Logger.LogLine("Grabbing nothing --- undefined input");
                commentText = "";
            }
			context.Logger.LogLine($"Items in filter:{filter.Items.Count()}");
            int count = 0;
			foreach (var masterItem in filter.Items)
			{
                count += 1;
				context.Logger.LogLine($"On item #: {count}");
				Item child = new Item();
                

				//--- Assign Fields ---//	
				fieldId = ids.GetFieldId("VC Administration|Master Schedule|Task Name");
				var nameMaster = masterItem.Field<TextItemField>(fieldId);
				if (nameMaster.Value != null)
				{
					fieldId = ids.GetFieldId("Task List|Title");
					var nameChild = child.Field<TextItemField>(fieldId);
					nameChild.Value = nameMaster.Value;
				}
				context.Logger.LogLine($"Added field:{nameMaster.Label}");
				fieldId = ids.GetFieldId("VC Administration|Master Schedule|Desciption");
				var descrMaster = masterItem.Field<TextItemField>(fieldId);
				if (descrMaster.Value != null)
				{
					fieldId = ids.GetFieldId("Task List|Description");
					var descrChild = child.Field<TextItemField>(fieldId);
					descrChild.Value = StripHTML(descrMaster.Value);
				}
				context.Logger.LogLine($"Added field:{descrMaster.Label}");
				fieldId = ids.GetFieldId("VC Administration|Master Schedule|Phase");
				var phaseMaster = masterItem.Field<CategoryItemField>(fieldId);
				if (phaseMaster.Options.Any())
				{
					fieldId = ids.GetFieldId("Task List|Phase");
					var phaseChild = child.Field<CategoryItemField>(fieldId);
					phaseChild.OptionText = phaseMaster.Options.First().Text;
				}
				context.Logger.LogLine($"Added field:{phaseMaster.Label}");
				fieldId = ids.GetFieldId("VC Administration|Master Schedule|ESO Member Role");
				var esoMaster = masterItem.Field<CategoryItemField>(fieldId);
				if (esoMaster.Options.Any())
				{
					fieldId = ids.GetFieldId("Task List|ESO Member Role");
					var esoChild = child.Field<CategoryItemField>(fieldId);
					esoChild.OptionText = esoMaster.Options.First().Text;
				}
				context.Logger.LogLine($"Added field:{esoMaster.Label}");
				fieldId = ids.GetFieldId("VC Administration|Master Schedule|Project");
				var projectMaster = masterItem.Field<CategoryItemField>(fieldId);
				if (projectMaster.Options.Any())
				{
					fieldId = ids.GetFieldId("Task List|Project");
					var projectChild = child.Field<CategoryItemField>(fieldId);
					projectChild.OptionText = projectMaster.Options.First().Text;
				}
				context.Logger.LogLine($"Added field:{projectMaster.Label}");

				fieldId = ids.GetFieldId("VC Administration|Master Schedule|Base Workshop Association");
				var wsMaster = masterItem.Field<CategoryItemField>(fieldId);
				if (wsMaster.Options.Any())
				{
					fieldId = ids.GetFieldId("Task List|WS Association");
					var wsChild = child.Field<TextItemField>(fieldId);
					wsChild.Value = wsMaster.Options.First().Text;
					fieldId = ids.GetFieldId("Task List|Parent WS");
					var parentChild = child.Field<CategoryItemField>(fieldId);
					parentChild.OptionText = wsMaster.Options.First().Text;
				}
				context.Logger.LogLine($"Added field:{wsMaster.Label}");
				fieldId = ids.GetFieldId("VC Administration|Master Schedule|Weeks Off-Set");
				var offsetMaster = masterItem.Field<NumericItemField>(fieldId);
				if (offsetMaster.Value.HasValue)
				{
					fieldId = ids.GetFieldId("Task List|Week Offset");
					var offsetChild = child.Field<NumericItemField>(fieldId);
					offsetChild.Value = offsetMaster.Value;
					fieldId = ids.GetFieldId("Task List|Weeks Before WS");
					var weeksChild = child.Field<NumericItemField>(fieldId);
					weeksChild.Value = offsetMaster.Value;
				}
				context.Logger.LogLine($"Added field:{offsetMaster.Label}");
				fieldId = ids.GetFieldId("Task List|Completetion");
				var comChild = child.Field<CategoryItemField>(fieldId);
				comChild.OptionText = "Incomplete";
				context.Logger.LogLine($"Added field: Completion");

				fieldId = ids.GetFieldId("VC Administration|Master Schedule|Duration (Days)");
				var durMaster = masterItem.Field<NumericItemField>(fieldId);
				if (durMaster.Value.HasValue)
				{
					fieldId = ids.GetFieldId("Task List|Duration (days)");
					var durChild = child.Field<NumericItemField>(fieldId);
					durChild.Value = durMaster.Value;
				}
				context.Logger.LogLine($"Added field:{durMaster.Label}");
				fieldId = ids.GetFieldId("VC Administration|Master Schedule|Dependancy");
				var depMaster = masterItem.Field<TextItemField>(fieldId);
				if (depMaster.Value != null)
				{
					fieldId = ids.GetFieldId("Task List|Additional Dependencies");
					var depChild = child.Field<TextItemField>(fieldId);
					depChild.Value = depMaster.Value;
				}
				context.Logger.LogLine($"Added field:{depMaster.Label}");
				fieldId = ids.GetFieldId("VC Administration|Master Schedule|Gdrive Link");
				var embedMaster = masterItem.Field<EmbedItemField>(fieldId);
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
					await podio.CreateItem(child, taskListAppId, true);//child task list appId
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
			if (check.Field<CategoryItemField>(TlStatusId).Options.First().Text == "1")
			{
				await pre.CreateExpendituresAndPreWSSurvs(context,podio,viewServ,check,e,service,ids,google);
			}
			await comm.AddCommentToObject("item", check.ItemId, commentText, hook: true);

				}
			}
		}

