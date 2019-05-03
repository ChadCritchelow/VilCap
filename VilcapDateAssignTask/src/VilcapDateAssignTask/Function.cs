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
using PodioCore.Models.Request;
using PodioCore.Services;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VilcapDateAssignTask
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
			string functionName="VilcapDateAssignTask";
			lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
			try
			{
				if (string.IsNullOrEmpty(lockValue))
				{
					context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
					return;
				}

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

				foreach (var item in furtherFilteredItems)
				{
					var responsibleMember = item.Field<ContactItemField>(ids.GetFieldId("Task List|Responsible Member"));
					var title = item.Field<TextItemField>(ids.GetFieldId("Task List|Title"));
					var date = item.Field<DateItemField>(ids.GetFieldId("Task List|Date"));
					var description = item.Field<TextItemField>(ids.GetFieldId("Task List|Description"));
					TaskCreateUpdateRequest t = new TaskCreateUpdateRequest();
					t.Description = title.Value;
					List<int> cIds = new List<int>();
					foreach (var contact in responsibleMember.Contacts)
					{
						cIds.Add(Convert.ToInt32(contact.UserId));
					}
					t.SetResponsible(cIds);
					t.DueDate = date.Start;
					t.Text = description.Value;
					var task = await taskServ.CreateTask(t);
					await taskServ.AssignTask(int.Parse(task.First().TaskId));//neccessary?
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
