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
	class AppStatus
	{
		public async System.Threading.Tasks.Task UpdateAppStatus(Item check, ILambdaContext context, Podio podio, GetIds ids)
		{
			//when an item is updated im applications:
			var revision = await podio.GetRevisionDifference
				(
				Convert.ToInt32(check.ItemId),
				check.CurrentRevision.Revision - 1,
				check.CurrentRevision.Revision
				);
			var firstRevision = revision.First();
			var completionStatus = check.Field<CategoryItemField>(ids.GetFieldId("Applications|Complete This Application"));
			if (firstRevision.FieldId == completionStatus.FieldId)
			{
				if (completionStatus.Options.Any() && completionStatus.Options.First().Text == "Submit")
				{
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

					//assign tasks:
					TaskService taskServ = new TaskService(podio);

					var programAssociates = AdminOptionToCheck.Field<ContactItemField>(ids.GetFieldId("Admin|Programs Associate(s)"));
					var title = "Review Completed Application for "+
						$"{check.Field<TextItemField>(ids.GetFieldId("Applications|Company Name")).Value}";

					var date = DateTime.Now.AddDays(5);
					TaskCreateUpdateRequest t = new TaskCreateUpdateRequest();
					t.Description = title;
					List<int> cIds = new List<int>();
					foreach (var contact in programAssociates.Contacts)
					{
						cIds.Add(Convert.ToInt32(contact.UserId));
					}
					t.SetResponsible(cIds);
					t.DueDate = date;
					var task = await taskServ.CreateTask(t);
					await taskServ.AssignTask(int.Parse(task.First().TaskId));//neccessary?
				}
			}
		}
	}
}
