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
    class WorkshopModules2
	{
		PodioCollection<Item> filter;
		public static string StripHTML(string input)
		{
			return Regex.Replace(input, "<.*?>", String.Empty);
		}
		public async System.Threading.Tasks.Task CreateWorkshopModules2(ILambdaContext context, Podio podio, Item check, RoutedPodioEvent e, DriveService service, GetIds ids, GoogleIntegration google,PreSurvAndExp pre)
		{
			string commentText;
			var batchId = ids.GetFieldId("Admin|WS Batch");
            string batch = check.Field<CategoryItemField>(batchId).Options.First().Text;
            var startDateId = ids.GetFieldId("Admin|Program Start Date");
            DateTime startDate = (DateTime)check.Field<DateItemField>(startDateId).StartDate;
            var packageId = ids.GetFieldId("Admin|Curriculum Package");
            string package = check.Field<CategoryItemField>(packageId).Options.First().Text;
            int fieldId = 0;

            context.Logger.LogLine($"Curriculum Batch '{batch}'");
			var viewServ = new ViewService(podio);
			context.Logger.LogLine("Got View Service");
			var views = await viewServ.GetViews(21310273);//VC Admin Content Curation  App
            var view = from v in views
                       where v.Name == package
                       select v;
			context.Logger.LogLine($"Got View '{package}'");
            var op = new FilterOptions{ Filters = view.First().Filters };
            context.Logger.LogLine($"Made var '{op.ToString()}'");
            op.Limit = 25;
            context.Logger.LogLine($"Limit: 25");

            switch (batch)
            {
                case "1":
                    context.Logger.LogLine("Grabbing items 1-25");
                    op.Offset = 0;
                    filter = await podio.FilterItems(21310273, op);
                    commentText = "WS Batch 1 finished";
                    break;
                case "2":
                    context.Logger.LogLine("Grabbing items 26-50");
                    op.Offset = 25;
                    filter = await podio.FilterItems(21310273, op);
                    commentText = "WS Batch 2 finished";
                    break;
                case "3":
                    context.Logger.LogLine("Grabbing items 51-75");
                    op.Offset = 50;
                    filter = await podio.FilterItems(21310273, op);
                    commentText = "WS Batch 3 finished";
                    break;
                default:
                    context.Logger.LogLine("ERROR Invalid Batch #");
                    commentText = "WS Batch # not recognized";
                    break;
            }
           
			context.Logger.LogLine($"Items in filter:{filter.Items.Count()}");

            //int childDTF = ids.GetFieldId("Workshop Modules|Date");
            //int offsetF = ids.GetFieldId("Workshop Modules|Minute Offset");
            //int durationF = ids.GetFieldId("VC Administration|Content Curation |Duration");
            int count = 0;
            int day = -1;
            TimeSpan timeFromStart = new TimeSpan(0);
            foreach (var master in filter.Items)
			{
                count += 1;
                context.Logger.LogLine($"On item #: {count}");
                Item child = new Item();
                if (day == -1)
                {
                    fieldId = ids.GetFieldId("VC Administration|Content Curation |Workshop Day");
                    var dayMaster = master.Field<CategoryItemField>(fieldId);
                    Int32.TryParse(dayMaster.Options.First().Text.Split("Day ")[1], out day);
                }

                //--- Assign Fields ---//
                fieldId = ids.GetFieldId("VC Administration|Content Curation |Workshop Detail Title");
                var titleMaster = master.Field<TextItemField>(fieldId);
                if (titleMaster.Value != null)
                {
                    fieldId = ids.GetFieldId("Workshop Modules|Title");
                    var titleChild = child.Field<TextItemField>(fieldId);
                    titleChild.Value = titleMaster.Value;
                }

                fieldId = ids.GetFieldId("VC Administration|Content Curation |Purpose");
                var descMaster = master.Field<TextItemField>(fieldId);
                if (descMaster.Value != null)
                {
                    fieldId = ids.GetFieldId("Workshop Modules|Description");
                    var descChild = child.Field<TextItemField>(fieldId);
                    descChild.Value = StripHTML(descMaster.Value);
                }

                var offsetMaster = master.Field<NumericItemField>(ids.GetFieldId("VC Administration|Content Curation |Minute Offset"));
                if (offsetMaster.Value != null)
                {
                    fieldId = ids.GetFieldId("Workshop Modules|Minute Offset");
                    var offsetChild = child.Field<NumericItemField>(fieldId);
                    offsetChild.Value = offsetMaster.Value;
                }
                context.Logger.LogLine("Checking Date information [disabled]");
                //double minutes = Convert.ToDouble(child.Field<NumericItemField>(offsetF).Value);
                //context.Logger.LogLine($"Minutes: {minutes}");
                //child.Field<DateItemField>(childDTF).Start = baseDT.Value.AddMinutes(minutes);
                //context.Logger.LogLine($"Child Start Date: {child.Field<DateItemField>(childDTF).Start}");
                //minutes = master.Field<DurationItemField>(durationF).Value.Value.TotalMinutes;
                //context.Logger.LogLine($"New minutes: {minutes}");
                //child.Field<DateItemField>(childDTF).End = child.Field<DateItemField>(childDTF).Start.Value.AddMinutes(minutes);
                //context.Logger.LogLine($"Child date end: {child.Field<DateItemField>(childDTF).End}");

                fieldId = ids.GetFieldId("VC Administration|Content Curation |Entrepreneur Pre-Work Required");
                var workMaster = master.Field<TextItemField>(fieldId);
                if (workMaster.Value != null)
                {
                    fieldId = ids.GetFieldId("Workshop Modules|Entrepreneur Pre-work Required");
                    var workChild = child.Field<TextItemField>(fieldId);
                    workChild.Value = workMaster.Value;
                }

                fieldId = ids.GetFieldId("VC Administration|Content Curation |Materials Required");
                var matsMaster = master.Field<TextItemField>(fieldId);
                if (matsMaster.Value != null)
                {
                    fieldId = ids.GetFieldId("Workshop Modules|Additional Materials Required");
                    var matsChild = child.Field<TextItemField>(fieldId);
                    matsChild.Value = matsMaster.Value;
                }

                fieldId = ids.GetFieldId("VC Administration|Content Curation |Mentors Required");
                var mentMaster = master.Field<TextItemField>(fieldId);
                if (mentMaster.Value != null)
                {
                    fieldId = ids.GetFieldId("Workshop Modules|Mentors Required");
                    var mentChild = child.Field<TextItemField>(fieldId);
                    mentChild.Value = mentMaster.Value;
                }

                //** Date Calcs **//
                
                fieldId = ids.GetFieldId("VC Administration|Content Curation |Package Sequence");
                var seqMaster = master.Field<CategoryItemField>(fieldId);
                fieldId = ids.GetFieldId("VC Administration|Content Curation |Duration");
                var durMaster = master.Field<DurationItemField>(fieldId);
                context.Logger.LogLine($"Master Duration: {durMaster.Value.Value}");
                if (durMaster.Value != null)
                {
                    context.Logger.LogLine("Status was not null");
                    fieldId = ids.GetFieldId("Workshop Modules|Duration");
                    var durChild = child.Field<DurationItemField>(fieldId);
                    durChild.Value = durMaster.Value.Value.Duration(); // durChild.Value.Value.Add(durMaster.Value.Value);? durChild.Value = durMaster.Value;?
                    context.Logger.LogLine($"Child Duration: {durChild.Value.Value}");

                    DateTime childDateTimeStart = startDate.Add(timeFromStart);
                    DateTime childDateTimeEnd = childDateTimeStart.Add(durChild.Value.Value.Duration());
                    timeFromStart = timeFromStart.Add(durChild.Value.Value.Duration());

                    fieldId = ids.GetFieldId("Workshop Modules|Time");
                    var childTime = child.Field<DateItemField>(fieldId);
                    childTime.Start = childDateTimeStart;
                    childTime.End = childDateTimeEnd;
                }
                //****///

                fieldId = ids.GetFieldId("VC Administration|Content Curation |GDrive File Name");
                var embedMaster = master.Field<EmbedItemField>(fieldId);
                fieldId = ids.GetFieldId("Workshop Modules|Link to Material");
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
				var taskListAppId = ids.GetFieldId("Workshop Modules");
				int waitSeconds = 5;
				CallPodio:
				try
				{
					await podio.CreateItem(child, taskListAppId, true);//child Workshop Modules appId
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
			if (check.Field<CategoryItemField>(batchId).Options.First().Text == "1")
			{
				await pre.CreateExpendituresAndPreWSSurvs(context,podio,viewServ,check,e,service,ids,google);
			}
			await comm.AddCommentToObject("item", check.ItemId, commentText, hook: true);

		}
	}
}

