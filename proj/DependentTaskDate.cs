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
	class DependentTaskDate
	{
		
		public async System.Threading.Tasks.Task SetDependentDateParity(Item check, ILambdaContext context, Podio podio, GetIds ids)
		{
			//When an item is updated in Workshop Modules:
			var revision = await podio.GetRevisionDifference
			(
				Convert.ToInt32(check.ItemId),
				check.CurrentRevision.Revision - 1,
				check.CurrentRevision.Revision
			);
			var firstRevision = revision.First();
			var selectionProcess = check.Field<CategoryItemField>(ids.GetFieldId("Workshop Modules|Date"));
			if (firstRevision.FieldId == selectionProcess.FieldId)
			{
				//Get referenced items
				var refs = await podio.GetReferringItems(check.ItemId);
				var taskListRefs = from r in refs
								   where r.App.Name == "Task List"
								   select r;
				foreach (var itemRef in taskListRefs)
				{
					foreach (var item in itemRef.Items)
					{
						Item updateMe = new Item() { ItemId = item.ItemId };
						var updateDate = updateMe.Field<DateItemField>(ids.GetFieldId("Task List|Date"));
						Item checkMe = await podio.GetItem(item.ItemId);
						var moduleDate = check.Field<DateItemField>(ids.GetFieldId("Workshop Modules|Date")).Start;
						var dependantTaskOffsetField =
							check.Field<DurationItemField>(ids.GetFieldId("Workshop Modules|Dependent Task Offset"));
						var duration = checkMe.Field<DurationItemField>(ids.GetFieldId("Task List|Duration"));
						updateDate.Start = moduleDate.Value
							.Subtract(dependantTaskOffsetField.Value.Value);
						updateDate.End = moduleDate.Value
							.Subtract(dependantTaskOffsetField.Value.Value)
							.Add(duration.Value.Value);
						await podio.UpdateItem(updateMe, true);
					}
				}
			}
		}
	}
}
