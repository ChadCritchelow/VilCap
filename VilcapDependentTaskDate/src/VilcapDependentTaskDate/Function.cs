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

namespace VilcapDependentTaskDate
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
			string functionName = "VilcapDependentTaskDate";
			lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
			try
			{
				if (string.IsNullOrEmpty(lockValue))
				{
					context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
					return;
				}
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
