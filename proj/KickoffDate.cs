using Amazon.Lambda.Core;
using PodioCore;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using PodioCore.Items;
using PodioCore.Models.Request;

namespace newVilcapCopyFileToGoogleDrive
{
	class KickoffDate
	{
		public async System.Threading.Tasks.Task KickOffDateChanged(Item check, ILambdaContext context, Podio podio, GetIds ids)
		{
			var revision = await podio.GetRevisionDifference
			(
			Convert.ToInt32(check.ItemId),
			check.CurrentRevision.Revision - 1,
			check.CurrentRevision.Revision
			);
			var firstRevision = revision.First();
			var date = check.Field<DateItemField>(ids.GetFieldId("Workshop Modules|Date"));
			var calendarColor = check.Field<CategoryItemField>(ids.GetFieldId("Workshop Modules|Calendar Color"));
			if (firstRevision.FieldId == date.FieldId)
			{
				if (calendarColor.Options.Any() &&
					(calendarColor.Options.First().Text == "Date Manager" ||
					calendarColor.Options.First().Text == "Addon Date Manager"))
				{
					dynamic previous = firstRevision.From[0];
					if(previous.value!=null)
					{
						var offset = date.Start.Value.Subtract(previous.value.start);//check to see if this works
						var fieldIdToSearch = ids.GetFieldId("Workshop Modules|Day #");
						var filterValue = check.Field<CategoryItemField>(ids.GetFieldId("Workshop Modules|Day Number")).Options.First().Text;
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
						foreach(var item in items.Items)
						{
							Item updateMe= new Item();
							var checkCalendarColor =check.Field<CategoryItemField>(ids.GetFieldId("Workshop Modules|Calendar Color"));
							var foreachItemCalendarColor = item.Field<CategoryItemField>(ids.GetFieldId("Workshop Modules|Calendar Color"));
							if(
								(checkCalendarColor.Options.Any()&&
								checkCalendarColor.Options.First().Text=="Date Manager"&&
								(!foreachItemCalendarColor.Options.Any()||
								foreachItemCalendarColor.Options.First().Text=="Module"))
								||
								(checkCalendarColor.Options.Any()&&
								checkCalendarColor.Options.First().Text=="Addon Date Manager"&&
								foreachItemCalendarColor.Options.Any()&&
								foreachItemCalendarColor.Options.First().Text=="Addon")
							  )
							{
								updateMe = new Item() { ItemId = item.ItemId };
								var updateDate= updateMe.Field<DateItemField>(ids.GetFieldId("Workshop Modules|Date"));
								var checkDate = item.Field<DateItemField>(ids.GetFieldId("Workshop Modules|Date"));
								var duration = item.Field<DurationItemField>(ids.GetFieldId("Workshop Modules|Duration"));
								updateDate.Start = checkDate.Start.Value.Add(offset);
								updateDate.End = checkDate.Start.Value.Add(offset + duration.Value);
							}
							await podio.UpdateItem(updateMe, true);
						}
					}
				}
			}
		}
	}
}
