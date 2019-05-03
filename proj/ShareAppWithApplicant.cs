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
	class ShareAppWithApplicant
	{
		public async System.Threading.Tasks.Task ShareApp(Item check, ILambdaContext context, Podio podio, GetIds ids)
		{
			//When a new item is created in Applications:
			SearchService searchServ = new SearchService(podio);

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
			Item AdminOptionToCheck = await podio.GetItem(items.Items.First().ItemId);

			GrantService serv = new GrantService(podio);
			//Create Email:
			var recipient = check.Field<EmailItemField>(ids.GetFieldId("Applications|Email")).Value.First().Value;
			var orgName = AdminOptionToCheck.Field<TextItemField>(ids.GetFieldId("Admin|Organization Name")).Value;
			var m = $"Invitation to Complete Your Application with {orgName}" +
			"This application will automatically save as you work on it.To access an in-progress";

			//Send email
			var email = recipient;

			List<Ref> people = new List<Ref>();
			Ref person = new Ref();
			person.Type = "mail";
			person.Id = email;
			people.Add(person);
			var message = m;

			await serv.CreateGrant("item", check.ItemId, people, "rate", message);

			Item updateMe = new Item() { ItemId = check.ItemId };
			updateMe.Field<CategoryItemField>(ids.GetFieldId("Applications|Application Status")).OptionText="New Application";
			await podio.UpdateItem(updateMe, true);
		}
	}
}
