using Amazon.Lambda.Core;
using PodioCore;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using PodioCore.Items;
using PodioCore.Services;

namespace newVilcapCopyFileToGoogleDrive
{//Handles final and semi-final selection cards
	class FinalSelectionCards
	{
		public async System.Threading.Tasks.Task CreateFinalSelectionCards(Item check, ILambdaContext context, Podio podio, GetIds ids)
		{
			//When an item is updated in Company Profiles and:

			var revision = await podio.GetRevisionDifference
			(
			Convert.ToInt32(check.ItemId),
			check.CurrentRevision.Revision - 1,
			check.CurrentRevision.Revision
			);
			var firstRevision = revision.First();
			var selectionProcess = check.Field<CategoryItemField>(ids.GetFieldId("Company Profiles|Selection Process"));
			if (firstRevision.FieldId == selectionProcess.FieldId)
			{
				if (selectionProcess.Options.Any() &&
					(selectionProcess.Options.First().Text == "Semi-Finalist"||
					selectionProcess.Options.First().Text == "Finalist"))
				{
					//Get view "Program Support":
					ViewService viewServ = new ViewService(podio);
					ItemService filterServ = new ItemService(podio);
					View view = new View();
					string selectionRound="";
					switch(selectionProcess.Options.First().Text)
					{
						case "Finalist":
							view = await viewServ.GetView(ids.GetFieldId("Program Support"), "Selection Committee - Final");
							selectionRound = "Final Round";
							break;
						case "Semi-Finalist":
							view = await viewServ.GetView(ids.GetFieldId("Program Support"), "Selection Committee - Semi Final");
							selectionRound = "Semi-Final Round";
							break;
					}
					
					var viewItems = await filterServ.FilterItemsByView(ids.GetFieldId("Program Support"), int.Parse(view.ViewId), limit: 500);
					foreach (var item in viewItems.Items)
					{
						Item create = new Item();
						create.Field<CategoryItemField>(ids.GetFieldId("Diligence and Selection|Selection Round")).OptionText
							= selectionRound;
						create.Field<AppItemField>(ids.GetFieldId("Diligence and Selection|Company")).ItemId = check.ItemId;
						create.Field<AppItemField>(ids.GetFieldId("Diligence and Selection|Selection Comittee Member")).ItemId=item.ItemId;
						create.Field<EmailItemField>(ids.GetFieldId("Diligence and Selection|Shared Email")).Value =
							item.Field<EmailItemField>(ids.GetFieldId("Program Support|Email")).Value;
						await podio.CreateItem(create, ids.GetFieldId("Diligence and Selection"), true);
					}
				}
			}
		}
	}
}
