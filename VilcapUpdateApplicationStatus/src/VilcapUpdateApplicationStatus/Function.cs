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

namespace VilcapUpdateApplicationStatus
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
			string lockValue;
			GetIds ids = new GetIds(dictChild, dictMaster, e);
			//Make sure to implement by checking to see if Deploy Curriculum has just changed
			//Deploy Curriculum field
			string functionName = "VilcapUpdateApplicationStatus";
			lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
			try
			{
				if (string.IsNullOrEmpty(lockValue))
				{
					context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
					return;
				}
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
						var title = "Review Completed Application for " +
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
