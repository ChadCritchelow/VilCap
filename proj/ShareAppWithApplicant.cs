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

			//Create Email:
			var recipient = check.Field<EmailItemField>(ids.GetFieldId("Applications|Email")).Value.First().Value;
			var sharedBy = "toolkit@vilcap.com";
			var orgName = AdminOptionToCheck.Field<TextItemField>(ids.GetFieldId("Admin|Organization Name")).Value;
			var message = $"Invitation to Complete Your Application with {orgName}" +
			"This application will automatically save as you work on it.To access an in-progress";

			//Send email
			ItemService shareServ = new ItemService(podio);

			Item updateMe = new Item() { ItemId = check.ItemId };
			updateMe.Field<CategoryItemField>(ids.GetFieldId("Applications|Application Status")).OptionText="New Application";
			await podio.UpdateItem(updateMe, true);
		}
	}
}
