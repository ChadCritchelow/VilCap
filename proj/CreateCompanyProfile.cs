using Amazon.Lambda.Core;
using PodioCore;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using PodioCore.Items;

namespace newVilcapCopyFileToGoogleDrive
{
	class CreateCompanyProfile
	{
		public async System.Threading.Tasks.Task CreateProfile(Item check, ILambdaContext context, Podio podio, GetIds ids)
		{
			//When an item is updated in Applications:
			var revision = await podio.GetRevisionDifference
				(
				Convert.ToInt32(check.ItemId),
				check.CurrentRevision.Revision - 1,
				check.CurrentRevision.Revision
				);
			var firstRevision = revision.First();
			var completionStatus = check.Field<CategoryItemField>(ids.GetFieldId("Applications|Application Status"));
			if (firstRevision.FieldId == completionStatus.FieldId)
			{
				if (completionStatus.Options.Any() && completionStatus.Options.First().Text == "Application Complete")
				{
					Item companyProfile = new Item();
					companyProfile.Field<CategoryItemField>(ids.GetFieldId("Selection Process *")).OptionText = "New Application";
					companyProfile.Field<AppItemField>(ids.GetFieldId("Application")).ItemId = check.ItemId;
					await podio.CreateItem(companyProfile, ids.GetFieldId("Company Profiles"), true);
				}
			}
		}
	}
}
