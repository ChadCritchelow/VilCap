using System;
using System.Linq;
using Amazon.Lambda.Core;
using BrickBridge.Lambda.VilCap;
using newVilcapCopyFileToGoogleDrive;
using PodioCore.Items;
using PodioCore.Services;
using PodioCore.Utils.ItemFields;
using Saasafras;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VilcapUpdateCompleteTasks
{
    public class Function
    {

        public async System.Threading.Tasks.Task FunctionHandler( RoutedPodioEvent e, ILambdaContext context )
        {
            var factory = new AuditedPodioClientFactory(e.solutionId, e.version, e.clientId, e.environmentId);
            var podio = factory.ForClient(e.clientId, e.environmentId);
            var check = await podio.GetFullItem(Convert.ToInt32(e.podioEvent.item_id));
            var saasafrasClient = new SaasafrasClient(Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
            var dictChild = await saasafrasClient.GetDictionary(e.clientId, e.environmentId, e.solutionId, e.version);
            var dictMaster = await saasafrasClient.GetDictionary("vcadministration", "vcadministration", "vilcap", "0.0");
            string lockValue;
            var ids = new GetIds(dictChild, dictMaster, e.environmentId);

            var functionName = "VilcapUpdateCompleteTasks";
            lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
            context.Logger.LogLine($"1");
            try
            {
                if( string.IsNullOrEmpty(lockValue) )
                {
                    context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
                    return;
                }
                var serv = new TaskService(podio);

                var revision = await podio.GetRevisionDifference
                    (
                    Convert.ToInt32(check.ItemId),
                    check.CurrentRevision.Revision - 1,
                    check.CurrentRevision.Revision
                    );
                var firstRevision = revision.First();

                context.Logger.LogLine($"Checking Completion Status");
                var completionStatus = check.Field<CategoryItemField>(ids.GetFieldId("Task List|Completetion"));
                context.Logger.LogLine("Checking to see if completion was the last field to be updated");
                if( firstRevision.FieldId == completionStatus.FieldId )
                {
                    context.Logger.LogLine("Checking to see if completion==Complete");
                    if( completionStatus.Options.Any() && completionStatus.Options.First().Text == "Complete" )
                    {
                        //mark item tasks as complete
                        var r = $"item:{check.ItemId}";
                        var ts = await serv.GetTasks(reference: r);
                        context.Logger.LogLine($"Iterating thru {ts.Count()} task(s)");
                        foreach( var task in ts )
                        {
                            context.Logger.LogLine($"Attempting to complete task with ID: {task.TaskId}");
                            await serv.CompleteTask(int.Parse(task.TaskId), true, false);
                            context.Logger.LogLine($"Completed task");
                            //send to podio?
                        }
                    }
                }
            }
            catch( Exception ex )
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
