using System;
using System.Linq;
using Amazon.Lambda.Core;
using newVilcapCopyFileToGoogleDrive;
using PodioCore.Items;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using Saasafras;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VilcapDependentTaskDate
{
    public class Function
    {
        public async System.Threading.Tasks.Task FunctionHandler( RoutedPodioEvent e, ILambdaContext context )
        {
            var factory = new AuditedPodioClientFactory(e.solutionId, e.version, e.clientId, e.environmentId);
            var podio = factory.ForClient(e.clientId, e.environmentId);
            var check = await podio.GetItem(Convert.ToInt32(e.podioEvent.item_id));
            var saasafrasClient = new SaasafrasClient(Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
            var dictChild = await saasafrasClient.GetDictionary(e.clientId, e.environmentId, e.solutionId, e.version);
            var dictMaster = await saasafrasClient.GetDictionary("vcadministration", "vcadministration", "vilcap", "0.0");
            string lockValue;
            var ids = new GetIds(dictChild, dictMaster, e.environmentId);
            //Make sure to implement by checking to see if Deploy Curriculum has just changed
            //Deploy Curriculum field
            var functionName = "VilcapDependentTaskDate";
            lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
            try
            {
                if( string.IsNullOrEmpty(lockValue) )
                {
                    context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
                    return;
                }
                // When an item is updated in Workshop Modules:
                var revision = await podio.GetRevisionDifference
                (
                    Convert.ToInt32(check.ItemId),
                    check.CurrentRevision.Revision - 1,
                    check.CurrentRevision.Revision
                );
                var firstRevision = revision.First();
                var date = check.Field<DateItemField>(ids.Get("Workshop Modules|Date"));
                if( firstRevision.FieldId == date.FieldId )
                {
                    // Get Dep Tasks
                    var depTasks = check.Field<AppItemField>(ids.Get("Workshop Modules|Dependent Task"));
                    context.Logger.LogLine($"- # of Dep Tasks: {depTasks.Values.Count}");
                    var oldTime = revision.First().From.First.Value<DateTime>("start");
                    var diff = date.Start.Value.Subtract(oldTime);
                    context.Logger.LogLine($"Oldtime: {oldTime} Offset: {diff}");

                    foreach( var depTask in depTasks.Items )
                    {
                        var updateMe = new Item();
                        context.Logger.LogLine($"- Iterating...");
                        updateMe = new Item() { ItemId = depTask.ItemId };
                        var taskDate = updateMe.Field<DateItemField>(ids.Get("Task List|Date"));
                        var checkDate = updateMe.Field<DateItemField>(ids.Get("Task List|Date"));
                        var duration = taskDate.End.GetValueOrDefault() - taskDate.Start.GetValueOrDefault();
                        if( duration.Ticks < 0 ) duration = new TimeSpan(0);
                        context.Logger.LogLine($"Old Task Time: {taskDate.Start.GetValueOrDefault()} Old Task End: {taskDate.End.GetValueOrDefault()}");
                        taskDate.Start = checkDate.Start.GetValueOrDefault().Add(diff);
                        taskDate.End = checkDate.Start.GetValueOrDefault().Add(diff + duration);
                        context.Logger.LogLine($"New Task Time: {taskDate.Start.GetValueOrDefault()} New Task End: {taskDate.End.GetValueOrDefault()}");
                        await podio.UpdateItem(updateMe, true);
                        context.Logger.LogLine($"New Task Time: {taskDate.Start.GetValueOrDefault()} New Task End: {taskDate.End.GetValueOrDefault()}");
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
