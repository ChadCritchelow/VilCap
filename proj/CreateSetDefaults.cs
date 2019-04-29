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
	class CreateSetDefaults
	{
		public async System.Threading.Tasks.Task SetDefaults(Item check, ILambdaContext context, Podio podio, GetIds ids)
		{
			//On Creation of a Company Profile:

			//get referenced items from applications app:
			Item checkApp= new Item();
			var refs=await podio.GetReferringItems(check.ItemId);

			var refsFromApplications = from r in refs
									   where r.App.Name == "Applications"
									   select r;
			foreach (var itemRef in refsFromApplications)
			{
				foreach (var app in itemRef.Items)
				{
					Item updateApp = new Item() { ItemId = app.ItemId };
					updateApp.Field<CategoryItemField>(ids.GetFieldId("Applications|Application Status")).OptionText = "Company Profile Created";
					await podio.UpdateItem(updateApp, true);
					checkApp =await podio.GetItem(app.ItemId);
				}
			}
			Item updateCompanyProfile = new Item() { ItemId = check.ItemId };
			updateCompanyProfile.Field<PhoneItemField>(ids.GetFieldId("Company Profiles|Phone")).Value =
				checkApp.Field<PhoneItemField>(ids.GetFieldId("Applications|Phone")).Value;
			updateCompanyProfile.Field<EmailItemField>(ids.GetFieldId("Company Profiles|Email")).Value =
				checkApp.Field<EmailItemField>(ids.GetFieldId("Applications|Email")).Value;
			updateCompanyProfile.Field<DateItemField>(ids.GetFieldId("Company Profiles|Company Founding Date")).Start =
				checkApp.Field<DateItemField>(ids.GetFieldId("Applications|Company Founding Date")).Start;

			var emails = check.Field<EmailItemField>(ids.GetFieldId("Company Profiles|Email")).Value;
			foreach(var email in emails)
			{
				Item entrepreneur = new Item();
				entrepreneur.Field<AppItemField>(ids.GetFieldId("Entrepreneurs|Company *")).ItemId = check.ItemId;
				entrepreneur.Field<EmailItemField>(ids.GetFieldId("Entrepreneurs|Entrepreneur Email")).Value =
					check.Field<EmailItemField>(ids.GetFieldId("Company Profiles|Email")).Value;
				await podio.CreateItem(entrepreneur, ids.GetFieldId("Entrepreneurs"), true);
			}

		}
	}
}
