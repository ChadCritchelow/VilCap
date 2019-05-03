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
using PodioCore.Models.Request;

namespace newVilcapCopyFileToGoogleDrive
{
	class ResponsibleRole
	{
		public async System.Threading.Tasks.Task SetResponsibleRole(ILambdaContext context,Item check, Podio podio, GetIds ids)
		{
			//When Item is created in Task List:
			//TODO: would love to get rid of search service.. run options by John

			var fieldIdToSearch = ids.GetFieldId("Applications");
			var filterValue = "vilcapadmin";
			var filter = new Dictionary<int, object>
							{
								{ fieldIdToSearch, filterValue }
							};
			FilterOptions newOptions = new FilterOptions
			{
				Filters = filter,
				Offset = 500
			};
			context.Logger.LogLine("Checking for duplicates");

			var items = await podio.FilterItems(ids.GetFieldId("Admin"), newOptions);
			Item AdminOptionToCheck =await podio.GetItem(items.Items.First().ItemId);
			Item CheckScheduleItem = check;
			Item UpdateScheduleItem = new Item() { ItemId = check.ItemId };
			List<int> contactids = new List<int>();
			var esoMemberRole = CheckScheduleItem.Field<CategoryItemField>(ids.GetFieldId("ESO Member Role"));
			if(esoMemberRole.Options.Any())
			{
				var responsibleMember = UpdateScheduleItem.Field<ContactItemField>(ids.GetFieldId("Task List|Responsible Member"));
				var esoValue = esoMemberRole.Options.First().Text;
				switch(esoValue)
				{
					case "Programs Associate":

						var programAssociates = AdminOptionToCheck.Field<ContactItemField>(ids.GetFieldId("Admin|Program Associate(s)"));
						foreach(var contact in programAssociates.Contacts)
						{
							contactids.Add(contact.ProfileId);
						}
						responsibleMember.ContactIds = contactids;
						break;
					case "Investments Analyst":
						var InvestmentsAnalysts = AdminOptionToCheck.Field<ContactItemField>(ids.GetFieldId("Admin|Investments Analyst(s)"));
						foreach (var contact in InvestmentsAnalysts.Contacts)
						{
							contactids.Add(contact.ProfileId);
						}
						responsibleMember.ContactIds = contactids;
						break;
					case "Program Manager":
						var programManagers = AdminOptionToCheck.Field<ContactItemField>(ids.GetFieldId("Admin|Program Manager(s)"));
						foreach (var contact in programManagers.Contacts)
						{
							contactids.Add(contact.ProfileId);
						}
						responsibleMember.ContactIds = contactids;
						break;
					case "Program Director":
						var programDirectors = AdminOptionToCheck.Field<ContactItemField>(ids.GetFieldId("Admin|Program Director(s)"));
						foreach (var contact in programDirectors.Contacts)
						{
							contactids.Add(contact.ProfileId);
						}
						responsibleMember.ContactIds = contactids;
						break;
				}
				await podio.UpdateItem(UpdateScheduleItem, true);
			}
			
		}
	}
}


