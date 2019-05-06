using Amazon.Lambda.Core;
using PodioCore;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using PodioCore.Items;
using BrickBridge.Lambda.VilCap;
using newVilcapCopyFileToGoogleDrive;
using Saasafras;
using System.Text.RegularExpressions;
using PodioCore.Services;
using PodioCore.Models.Request;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VilcapKickoffDateChanged
{
	public class Function
	{
		static LambdaMemoryStore memoryStore = new LambdaMemoryStore();
		public async System.Threading.Tasks.Task FunctionHandler(RoutedPodioEvent e, ILambdaContext context)
		{
			var factory = new AuditedPodioClientFactory(e.solutionId, e.version, e.clientId, e.environmentId);
			var podio = factory.ForClient(e.clientId, e.environmentId);
			Item check = await podio.GetItem(Convert.ToInt32(e.podioEvent.item_id));
			SaasafrasClient saasafrasClient = new SaasafrasClient(System.Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), System.Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
			var dictChild = await saasafrasClient.GetDictionary(e.clientId, e.environmentId, e.solutionId, e.version);
			var dictMaster = await saasafrasClient.GetDictionary("vcadministration", "vcadministration", "vilcap", "0.0");
			var fullNames = new Dictionary<string, string>()
			{
				{"toolkittemplate3", "VC Toolkit Template 3" }
			};
			string lockValue;
			GetIds ids = new GetIds(dictChild, dictMaster, fullNames, e);
			//Make sure to implement by checking to see if Deploy Curriculum has just changed
			//Deploy Curriculum field
			string functionName = "VilcapKickoffDateChanged";
			lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
			try
			{
				if (string.IsNullOrEmpty(lockValue))
				{
					context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
					return;
				}

                var date = check.Field<DateItemField>(ids.GetFieldId("Workshop Modules|Date"));
                var calendarColor = check.Field<CategoryItemField>(ids.GetFieldId("Workshop Modules|Calendar Color"));
                var revision = await podio.GetRevisionDifference
			    (
			    Convert.ToInt32(check.ItemId),
			    check.CurrentRevision.Revision - 1,
			    check.CurrentRevision.Revision
			    );

                if (revision.First().Label == "Date" 
                    && calendarColor.Options.Any() 
                    && (calendarColor.Options.First().Text == "Date Manager" ||
                        calendarColor.Options.First().Text == "Addon Date Manager"))
				{

                    context.Logger.LogLine($"Module Type: {calendarColor.Options.First().Text}");
                    context.Logger.LogLine($"Object Type: {revision.First().From.ToString()}");
                    //if(changedField.From[0] != null) { 
                    //if (previous.value != null)
                    {
                        DateTime oldTime = revision.First().From.First.Value<DateTime>("start"); // Array position of "start"
                        TimeSpan diff = date.Start.Value.Subtract(oldTime);
                        context.Logger.LogLine($"Got Values");
                        
                        //context.Logger.LogLine($"{itemFrom.value.toString()}");
                        //context.Logger.LogLine($"date.state.val: {date.Start.Value.Ticks}");
                        //var offset = date.Start.Value.Subtract(itemFrom.value.start);//check to see if this works
                        //context.Logger.LogLine($"offset: {offset.toString()}");
                        var fieldIdToSearch = ids.GetFieldId("Workshop Modules|Day #");
						var filterValue = check.Field<CategoryItemField>(ids.GetFieldId("Workshop Modules|Day Number")).Options.First().Text;
						var filter = new Dictionary<int, object>
						{
							{ fieldIdToSearch, filterValue }
						};
						FilterOptions newOptions = new FilterOptions
						{
							Filters = filter
						};
						context.Logger.LogLine("Checking for duplicates");


                        var workshopAppId = ids.GetFieldId("Workshop Modules");
                        var items = await podio.FilterItems(workshopAppId, newOptions);
                        context.Logger.LogLine("Got Items");
                        foreach (var item in items.Items)
                        {
                            Item updateMe = new Item();
                            var checkCalendarColor = check.Field<CategoryItemField>(ids.GetFieldId("Workshop Modules|Calendar Color"));
                            var foreachItemCalendarColor = item.Field<CategoryItemField>(ids.GetFieldId("Workshop Modules|Calendar Color"));
                            if (
                                (checkCalendarColor.Options.Any() &&
                                checkCalendarColor.Options.First().Text == "Date Manager" &&
                                (!foreachItemCalendarColor.Options.Any() ||
                                foreachItemCalendarColor.Options.First().Text == "Module"))
                                ||
                                (checkCalendarColor.Options.Any() &&
                                checkCalendarColor.Options.First().Text == "Addon Date Manager" &&
                                foreachItemCalendarColor.Options.Any() &&
                                foreachItemCalendarColor.Options.First().Text == "Addon")
                                )
                            {
                                updateMe = new Item() { ItemId = item.ItemId };
                                var updateDate = updateMe.Field<DateItemField>(ids.GetFieldId("Workshop Modules|Date"));
                                var checkDate = item.Field<DateItemField>(ids.GetFieldId("Workshop Modules|Date"));
                                var duration = item.Field<DurationItemField>(ids.GetFieldId("Workshop Modules|Duration"));
                                updateDate.Start = checkDate.Start.Value.Add(diff);
                                updateDate.End = checkDate.Start.Value.Add(diff + duration.Value.Value);
                            }
                            await podio.UpdateItem(updateMe, true);
                        }
					}
				}
			}
			catch(Exception ex)
			{
				throw ex;
			}
			finally
			{
				await saasafrasClient.UnlockFunction(functionName, check.ItemId.ToString(), lockValue);
			}
		}
	}
}
