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
using System.Text.RegularExpressions;

namespace newVilcapCopyFileToGoogleDrive
{
    class PreSurvAndExp
	{
		
		public static string StripHTML(string input)
		{
			return Regex.Replace(input, "<.*?>", String.Empty);
		}

		public async System.Threading.Tasks.Task CreateExpendituresAndPreWSSurvs(ILambdaContext context, Podio podio, ViewService viewServ, Item check, RoutedPodioEvent e, DriveService service,GetIds ids,GoogleIntegration google)
		{


			try
			{
				int fieldId = 0;
				context.Logger.LogLine("Creating Expenditures and Pre WS Surveys");
				//--- Create Program Budget Template (Expendatures) ---//
				viewServ = new ViewService(podio);
				context.Logger.LogLine("Got View Service");
				var views = await viewServ.GetViews(21481130); //VC Admin Master Schedule App
				var view = from v in views
						   where v.Name == "Workshop Associations"
						   select v;
				context.Logger.LogLine("Got View");
				var op = new FilterOptions();
				op.Filters = view.First().Filters;
				op.Limit = 500;
				var filter = await podio.FilterItems(21481130, op);
				foreach (var master in filter.Items)
				{
					Item child = new Item();

					fieldId = ids.GetFieldId("VC Administration|Expenditures Curation |Purpose");
					var purposeMaster = master.Field<TextItemField>(fieldId);
					if (purposeMaster.Value != null)
					{
						fieldId = ids.GetFieldId("Expenditures|Purpose");
						var purposeChild = child.Field<TextItemField>(fieldId);
						purposeChild.Value = purposeMaster.Value;
					}

					fieldId = ids.GetFieldId("VC Administration|Expenditures Curation |Workshop Associations");
					var waMaster = master.Field<CategoryItemField>(fieldId);
					if (waMaster.Options.Any())
					{
						fieldId = ids.GetFieldId("Expenditures|Workshop Associations");
						var waChild = child.Field<CategoryItemField>(fieldId);
						waChild.OptionText = waMaster.Options.First().Text;
					}

					fieldId = ids.GetFieldId("VC Administration|Expenditures Curation |Expense Type");
					var expMaster = master.Field<CategoryItemField>(fieldId);
					if (expMaster.Options.Any())
					{
						fieldId = ids.GetFieldId("Expenditures|Expense Type");
						var expChild = child.Field<CategoryItemField>(fieldId);
						expChild.OptionText = expMaster.Options.First().Text;
					}

					fieldId = ids.GetFieldId("VC Administration|Expenditures Curation |Amount");
					var amountMaster = master.Field<MoneyItemField>(fieldId);
					if (amountMaster.Value.HasValue)
					{
						fieldId = ids.GetFieldId("Expenditures|Amount");
						var amountChild = child.Field<MoneyItemField>(fieldId);
						amountChild.Value = amountMaster.Value;
					}
					fieldId = ids.GetFieldId("Admin|Program Manager");
					var managerMaster = check.Field<ContactItemField>(fieldId);
					if (managerMaster.Contacts.Any())
					{
						fieldId = ids.GetFieldId("Expenditures|Spender");
						var managerChild = child.Field<ContactItemField>(fieldId);
						List<int> cs = new List<int>();
						foreach (var contact in managerMaster.Contacts)
						{
							cs.Add(contact.ProfileId);
							managerChild.ContactIds = cs;
						}
					}
					fieldId = ids.GetFieldId("Expenditures|Status");
					var status = child.Field<CategoryItemField>(fieldId);
					status.OptionText = "Template";
					int waitSeconds = 5;
					CallPodio:
					try
					{
						await podio.CreateItem(child, ids.GetFieldId($"Expenditures"), false);
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

				//--- Create Pre-Workshop Surveys ---//
				context.Logger.LogLine("Creating surveys");
				viewServ = new ViewService(podio);
				context.Logger.LogLine("Got View Service");
				views = await viewServ.GetViews(21389770); //VC Admin Master Schedule App
				view = from v in views
					   where v.Name == "PreWS"
					   select v;
				context.Logger.LogLine("Got View");
				op = new FilterOptions();
				op.Filters = view.First().Filters;
				op.Limit = 500;
				filter = await podio.FilterItems(21389770, op);

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
					List<Embed> embeds = new List<Embed>();
					string parentFolderId = Environment.GetEnvironmentVariable("GOOGLE_PARENT_FOLDER_ID");
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
			catch (Exception ex)
			{
				context.Logger.LogLine(ex.Message);
			}
		}
	}
}
