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
	class DateAssignTask
	{
		public async System.Threading.Tasks.Task AssignTask(Item check, ILambdaContext context, Podio podio, GetIds ids)
		{
			TaskService taskServ = new TaskService(podio);

			var fieldIdToSearch = ids.GetFieldId("Task List|Date");
			var filterValue = DateTime.Now.AddDays(7);
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

			var filteredItems = await podio.FilterItems(ids.GetFieldId("Task List"), newOptions);

			var furtherFilteredItems = from f in filteredItems.Items
									   where 
									   f.Field<CategoryItemField>(ids.GetFieldId("Task List|Completetion")).Options.Any()
									   && 
									   f.Field<CategoryItemField>(ids.GetFieldId("Task List|Completetion")).Options.First().Text != "Complete"
									   select f;

			foreach(var item in furtherFilteredItems)
			{
				var responsibleMember = item.Field<ContactItemField>(ids.GetFieldId("Task List|Responsible Member"));
				var title = item.Field<TextItemField>(ids.GetFieldId("Task List|Title"));
				var date = item.Field<DateItemField>(ids.GetFieldId("Task List|Date"));
				var description = item.Field<TextItemField>(ids.GetFieldId("Task List|Description"));
				TaskCreateUpdateRequest t = new TaskCreateUpdateRequest();
				t.Description = title.Value;
				List<int> cIds = new List<int>();
				foreach(var contact in responsibleMember.Contacts)
				{
					cIds.Add(Convert.ToInt32(contact.UserId));
				}
				t.SetResponsible(cIds);
				t.DueDate = date.Start;
				t.Text = description.Value;
				var task=await taskServ.CreateTask(t);
				await taskServ.AssignTask(int.Parse(task.First().TaskId));//neccessary?
			}



		}
	}
}
