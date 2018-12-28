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
	class WorkshopModules
	{
		public static string StripHTML(string input)
		{
			return Regex.Replace(input, "<.*?>", String.Empty);
		}
		public async System.Threading.Tasks.Task CreateWorkShopModules(GetIds ids, Item check, Podio podio, ILambdaContext context, DriveService service,GoogleIntegration google,RoutedPodioEvent e,CategoryItemField checkType)
		{
			int fieldId = 0;
			var op = new FilterOptions();
			op.Limit = 500;
			int textFieldId = ids.GetFieldId("VC Administration|Content Curation |Workshop Type (Text)");

			var filterConditions = new Dictionary<string, string>
						{
							{textFieldId.ToString(), checkType.Options.First().Text}
						};
			op.Filters = filterConditions;
			var filter = await podio.FilterItems(21310273, op);

			var baseDT = check.Field<DateItemField>(ids.GetFieldId("Create Workshop|Date")).Start;
			int childDTF = ids.GetFieldId("Workshop Modules|Date");
			int offsetF = ids.GetFieldId("Workshop Modules|Minute Offset");
			int durationF = ids.GetFieldId("VC Administration|Content Curation |Duration");

			foreach (var master in filter.Items)
			{
				Item child = new Item();
				fieldId = ids.GetFieldId("Workshop Modules|Workshop");
				var ws = child.Field<AppItemField>(fieldId);
				ws.ItemId = check.ItemId;//TODO

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
				}
				var offsetMaster = master.Field<NumericItemField>(ids.GetFieldId("VC Administration|Content Curation |Minute Offset"));
				if (offsetMaster.Value != null)
				{
					fieldId = ids.GetFieldId("Workshop Modules|Minute Offset");
					var offsetChild = child.Field<NumericItemField>(fieldId);
					offsetChild.Value = offsetMaster.Value;
				}
				context.Logger.LogLine("Checking Date information");
				double minutes = Convert.ToDouble(child.Field<NumericItemField>(offsetF).Value);
				context.Logger.LogLine($"Minutes: {minutes}");
				child.Field<DateItemField>(childDTF).Start = baseDT.Value.AddMinutes(minutes);
				context.Logger.LogLine($"Child Start Date: {child.Field<DateItemField>(childDTF).Start}");
				minutes = master.Field<DurationItemField>(durationF).Value.Value.TotalMinutes;
				context.Logger.LogLine($"New minutes: {minutes}");
				child.Field<DateItemField>(childDTF).End = child.Field<DateItemField>(childDTF).Start.Value.AddMinutes(minutes);
				context.Logger.LogLine($"Child date end: {child.Field<DateItemField>(childDTF).End}");

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

				fieldId = ids.GetFieldId("VC Administration|Content Curation |GDrive File Name");
				var embedMaster = master.Field<EmbedItemField>(fieldId);
				fieldId = ids.GetFieldId("Workshop Modules|Link to Material");
				var embedChild = child.Field<EmbedItemField>(fieldId);
				List<Embed> embeds = new List<Embed>();
				string parentFolderId = System.Environment.GetEnvironmentVariable("GOOGLE_PARENT_FOLDER_ID");
				var cloneFolderId = google.GetSubfolderId(service, podio, e, parentFolderId);
				foreach (var em in embedMaster.Embeds)
				{
					if (em.OriginalUrl.Contains(".google."))
					{
						await google.UpdateOneEmbed(service, em, embeds, cloneFolderId, podio, e);
					}
				}
				foreach (var embed in embeds)
				{
					embedChild.AddEmbed(embed.EmbedId);
				}
				//TODO: Add embed fields 

				int waitSeconds = 5;
				CallPodio:
				try
				{
					await podio.CreateItem(child, ids.GetFieldId("Workshop Modules"), true);
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
			}

		}
	}
}
