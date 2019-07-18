using PodioCore.Exceptions;
using PodioCore.Items;
using PodioCore.Models;
using PodioCore.Models.Request;
using PodioCore.Utils.ItemFields;
using System;
using System.Collections.Generic;
using System.Linq;
using Task = System.Threading.Tasks.Task;

namespace newVilcapCopyFileToGoogleDrive
{
    public class PreSurvAndExp
	{        
        public static async Task CreateExpendituresAndPreWSSurvs( newVilcapCopyFileToGoogleDrive vilcap )
        {
            vilcap.context.Logger.LogLine("Creating Expenditures and Pre WS Surveys");
            try
			{
				var fieldId = 0;
				//--- Create Program Budget Template (Expendatures) ---//
				var views = await vilcap.viewServ.GetViews(newVilcapCopyFileToGoogleDrive.MASTER_SCHEDULE_APP);
				var view = from v in views
						   where v.Name == "Workshop Associations"
						   select v;
                var filter = await vilcap.podio.FilterItems(21481130, new FilterOptions
                {
                    Filters = view.First().Filters,
                    Limit = 500
                });
				foreach (var master in filter.Items)
				{
					var child = new Item();

					fieldId = vilcap.ids.Get("VC Administration|Expenditures Curation |Purpose");
                    child.Field<TextItemField>(vilcap.ids.Get("Expenditures|Purpose")).Value = master.Field<TextItemField>(fieldId).Value ;

					fieldId = vilcap.ids.Get("VC Administration|Expenditures Curation |Workshop Associations");
					var waMaster = master.Field<CategoryItemField>(fieldId);
					if (waMaster.Options.Any())
					{
						fieldId = vilcap.ids.Get("Expenditures|Workshop Associations");
						var waChild = child.Field<CategoryItemField>(fieldId);
						waChild.OptionText = waMaster.Options.First().Text;
					}

					fieldId = vilcap.ids.Get("VC Administration|Expenditures Curation |Expense Type");
					var expMaster = master.Field<CategoryItemField>(fieldId);
					if (expMaster.Options.Any())
					{
						fieldId = vilcap.ids.Get("Expenditures|Expense Type");
						var expChild = child.Field<CategoryItemField>(fieldId);
						expChild.OptionText = expMaster.Options.First().Text;
					}

					fieldId = vilcap.ids.Get("VC Administration|Expenditures Curation |Amount");
					var amountMaster = master.Field<MoneyItemField>(fieldId);
					if (amountMaster.Value.HasValue)
					{
						fieldId = vilcap.ids.Get("Expenditures|Amount");
						var amountChild = child.Field<MoneyItemField>(fieldId);
						amountChild.Value = amountMaster.Value;
					}
					fieldId = vilcap.ids.Get("Admin|Program Manager");
					var managerMaster = vilcap.item.Field<ContactItemField>(fieldId);
					if (managerMaster.Contacts.Any())
					{
						fieldId = vilcap.ids.Get("Expenditures|Spender");
						var managerChild = child.Field<ContactItemField>(fieldId);
						var cs = new List<int>();
						foreach (var contact in managerMaster.Contacts)
						{
							cs.Add(contact.ProfileId);
							managerChild.ContactIds = cs;
						}
					}
					fieldId = vilcap.ids.Get("Expenditures|Status");
					var status = child.Field<CategoryItemField>(fieldId);
					status.OptionText = "Template";
					var waitSeconds = 5;
					CallPodio:
					try
					{
						await vilcap.podio.CreateItem(child, vilcap.ids.Get($"Expenditures"), false);
					}
					catch (PodioUnavailableException ex)
					{
						vilcap.context.Logger.LogLine($"{ex.Message}");
						vilcap.context.Logger.LogLine($"Trying again in {waitSeconds} seconds.");
						for (var i = 0; i < waitSeconds; i++)
						{
							System.Threading.Thread.Sleep(1000);
							vilcap.context.Logger.LogLine(".");
						}
						waitSeconds *= 2;
						goto CallPodio;
					}

				}

				//--- Create Pre-Workshop Surveys ---//
				vilcap.context.Logger.LogLine("Creating surveys");
				views = await vilcap.viewServ.GetViews(newVilcapCopyFileToGoogleDrive.MASTER_SURVEY_APP);
				view = from v in views
					   where v.Name == "PreWS"
					   select v;
                filter = await vilcap.podio.FilterItems(newVilcapCopyFileToGoogleDrive.MASTER_SURVEY_APP, new FilterOptions
                {
                    Filters = view.First().Filters,
                    Limit = 500
                });

				foreach (var master in filter.Items)
				{
					var child = new Item();
					fieldId = vilcap.ids.Get("VC Administration|Survey|Title");
					var titleMaster = master.Field<TextItemField>(fieldId);
					if (titleMaster.Value != null)
					{
						fieldId = vilcap.ids.Get("Surveys|Title");
						var titleChild = child.Field<TextItemField>(fieldId);
						titleChild.Value = titleMaster.Value;
					}

					fieldId = vilcap.ids.Get("VC Administration|Survey|Notes");
					var notesMaster = master.Field<TextItemField>(fieldId);
					if (notesMaster.Value != null)
					{
						fieldId = vilcap.ids.Get("Surveys|Notes");
						var notesChild = child.Field<TextItemField>(fieldId);
                        //notesChild.Value = StripHTML(notesMaster.Value);
                        notesChild.Value = notesMaster.Value;
                    }

					fieldId = vilcap.ids.Get("VC Administration|Survey|Related Workshop");
					var relMaster = master.Field<CategoryItemField>(fieldId);
					if (relMaster.Options.Any())
					{
						fieldId = vilcap.ids.Get("Surveys|Related Workshop");
						var relChild = child.Field<CategoryItemField>(fieldId);
						relChild.OptionText = relMaster.Options.First().Text;
					}

					fieldId = vilcap.ids.Get("VC Administration|Survey|Gdrive Survey");
					var embedMaster = master.Field<EmbedItemField>(fieldId);
					fieldId = vilcap.ids.Get("Surveys|Link to Survey");
					var embedChild = child.Field<EmbedItemField>(fieldId);
					var embeds = new List<Embed>();
					var parentFolderId = Environment.GetEnvironmentVariable("GOOGLE_PARENT_FOLDER_ID");
					var cloneFolderId = vilcap.google.GetSubfolderId(vilcap.service, vilcap.podio, vilcap.e, parentFolderId);
					foreach (var em in embedMaster.Embeds)
					{
						if (em.OriginalUrl.Contains(".vilcap.google."))
						{
							await vilcap.google.UpdateOneEmbed(vilcap.service, em, embeds, cloneFolderId, vilcap.podio, vilcap.e);
						}
						else          // Hold for 2.0 //
						{
							var nonG = new NonGdriveLinks();
							await nonG.NonGDriveCopy(em, embeds, vilcap.podio, vilcap.e);
						}
					}
					foreach (var embed in embeds)
					{
						embedChild.AddEmbed(embed.EmbedId);
					}
					var waitSeconds = 5;
					CallPodio:
					try
					{
						await vilcap.podio.CreateItem(child, vilcap.ids.Get("Surveys"), false);
					}
					catch (PodioUnavailableException ex)
					{
						vilcap.context.Logger.LogLine($"{ex.Message}");
						vilcap.context.Logger.LogLine($"Trying again in {waitSeconds} seconds.");
						for (var i = 0; i < waitSeconds; i++)
						{
							System.Threading.Thread.Sleep(1000);
							vilcap.context.Logger.LogLine(".");
						}
						waitSeconds *= 2;
						goto CallPodio;
					}

				}
			}
			catch (Exception ex)
			{
				vilcap.context.Logger.LogLine(ex.Message);
			}
		}
	}
}
