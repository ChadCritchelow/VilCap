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
using BrickBridge.Lambda.VilCap;
using newVilcapCopyFileToGoogleDrive;
using Saasafras;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VilcapUpdateCompleteTasks
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
			string functionName="VilcapUpdateCompleteTasks";
			lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
			try
			{
				if (string.IsNullOrEmpty(lockValue))
				{
					context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
					return;
				}
				TaskService serv = new TaskService(podio);
				var revision = await podio.GetRevisionDifference
					(
					Convert.ToInt32(check.ItemId),
					check.CurrentRevision.Revision - 1,
					check.CurrentRevision.Revision
					);
				var firstRevision = revision.First();
				var completionStatus = check.Field<CategoryItemField>(ids.GetFieldId("Task List|Completetion"));
                context.Logger.LogLine($"Checking Completion Status");
                if (firstRevision.FieldId == completionStatus.FieldId)
				{
					if (completionStatus.Options.Any() && completionStatus.Options.First().Text == "Complete")
					{
                        context.Logger.LogLine($"Status == Complete");
                        //mark item tasks as complete
                        foreach (var task in check.Tasks)
						{
                            context.Logger.LogLine($"Iterating ... ");
                            await serv.CompleteTask(int.Parse(task.TaskId));
							//send to podio?
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
