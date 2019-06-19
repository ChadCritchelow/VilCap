using System;
using System.Collections.Generic;
using Amazon.Lambda.Core;
using Google.Apis.Drive.v3;
using PodioCore;
using PodioCore.Exceptions;
using PodioCore.Items;
using PodioCore.Models;
using PodioCore.Models.Request;
using PodioCore.Utils.ItemFields;
using System.Linq;
using System.Text.RegularExpressions;

namespace newVilcapCopyFileToGoogleDrive
{
    class Survey
	{
		public static string StripHTML(string input)
		{
			return Regex.Replace(input, "<.*?>", String.Empty);
		}
		public async System.Threading.Tasks.Task CreateSurveys(CategoryItemField checkType, GetIds ids,Podio podio,GoogleIntegration google,DriveService service,RoutedPodioEvent e,ILambdaContext context)
		{
			int fieldId = 0;
			var parts = checkType.Options.First().Text.Split('/');
			if (parts[1].Trim() == "Day 1")
			{
				string filterValue = parts[0].Trim();
				var op = new FilterOptions();
				op.Limit = 500;

				var filterConditions = new Dictionary<string, string>
							{
								{ids.GetFieldId("VC Administration|Survey|[WS]").ToString(), filterValue }
							};
				op.Filters = filterConditions;

				var filter = await podio.FilterItems(21389770, op);

				foreach (var master in filter.Items)
				{

					Item child = new Item();
					fieldId = ids.GetFieldId("VC Administration|Survey|Title");
					var titleMaster = master.Field<TextItemField>(fieldId);
					if (titleMaster.Value != null)
					{
						fieldId = ids.GetFieldId("Surveys|Title");
						var titleChild = child.Field<TextItemField>(fieldId);
						titleChild.Value = titleMaster.Value;
					}

					fieldId = ids.GetFieldId("VC Administration|Survey|Notes");
					var notesMaster = master.Field<TextItemField>(fieldId);
					if (notesMaster.Value != null)
					{
						fieldId = ids.GetFieldId("Surveys|Notes");
						var notesChild = child.Field<TextItemField>(fieldId);
						//notesChild.Value = StripHTML(notesMaster.Value);
                        notesChild.Value = notesMaster.Value;
                    }

					fieldId = ids.GetFieldId("VC Administration|Survey|Related Workshop");
					var relMaster = master.Field<CategoryItemField>(fieldId);
					if (relMaster.Options.Any())
					{
						fieldId = ids.GetFieldId("Surveys|Related Workshop");
						var relChild = child.Field<CategoryItemField>(fieldId);
						relChild.OptionText = relMaster.Options.First().Text;
					}

					fieldId = ids.GetFieldId("VC Administration|Survey|Gdrive Survey");
					var embedMaster = master.Field<EmbedItemField>(fieldId);
					fieldId = ids.GetFieldId("Surveys|Link to Survey");
					var embedChild = child.Field<EmbedItemField>(fieldId);
					var embeds = new List<Embed>();
					var parentFolderId = Environment.GetEnvironmentVariable("GOOGLE_PARENT_FOLDER_ID");
					var cloneFolderId = google.GetSubfolderId(service, podio, e, parentFolderId);
					foreach (var em in embedMaster.Embeds)
					{
						if (em.OriginalUrl.Contains(".google."))
						{
							await google.UpdateOneEmbed(service, em, embeds, cloneFolderId, podio, e);
						}
						else          // Hold for 2.0 //
						{
							NonGdriveLinks nonG = new NonGdriveLinks();
							await nonG.NonGDriveCopy(em, embeds, podio, e);
						}
					}
					foreach (var embed in embeds)
					{
						embedChild.AddEmbed(embed.EmbedId);
					}
					//embed fields

					int waitSeconds = 5;
					CallPodio:
					try
					{
						await podio.CreateItem(child, ids.GetFieldId("Surveys"), false);
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
}
