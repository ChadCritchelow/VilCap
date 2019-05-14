using Amazon.Lambda.Core;
using PodioCore.Utils.ItemFields;
using System;
using System.Collections.Generic;
using System.Linq;
using PodioCore.Items;
using BrickBridge.Lambda.VilCap;
using newVilcapCopyFileToGoogleDrive;
using Saasafras;
using PodioCore.Models.Request;
using PodioCore.Services;
using Task = System.Threading.Tasks.Task;
using PodioCore.Models;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VilcapDateAssignTask
{
    public class Function
    {

		static LambdaMemoryStore memoryStore = new LambdaMemoryStore();
        Dictionary<string, string> dictChild;
        Dictionary<string, string> dictMaster;
        Dictionary<string, string> fullNames;

        public async Task FunctionHandler(RoutedPodioEvent e, ILambdaContext context)
        {
            //var awsClient = new Amazon.Lambda.AmazonLambdaClient();
            //InvokeRequest request = new InvokeRequest { FunctionName = "FunctionHandler" };
            //await awsClient.InvokeAsync(request);
            context.Logger.LogLine("Recieved Routed Podio Event");

            var factory = new AuditedPodioClientFactory(e.solutionId, e.version, e.clientId, e.environmentId);
            var podio = factory.ForClient(e.clientId, e.environmentId);
            SaasafrasClient saasafrasClient = new SaasafrasClient(System.Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), System.Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
            var dictChild = await saasafrasClient.GetDictionary(e.clientId, e.environmentId, e.solutionId, e.version);
            string lockValue;
            GetIds ids = new GetIds(dictChild, dictMaster, e.environmentId);

            string functionName = "VilcapDateAssignTask";
            lockValue = await saasafrasClient.LockFunction(functionName, e.clientId);
            try
            {
                if (string.IsNullOrEmpty(lockValue))
                {
                    context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {e.clientId}");
                    return;
                }

                var taskServ = new TaskService(podio);
                var itemServ = new ItemService(podio);

                var fieldIdToSearch = ids.GetFieldId("Task List|Date");
                var filterValue = DateTime.Now.AddDays(7).Ticks;

                var viewServ = new ViewService(podio);
                context.Logger.LogLine("Got View Service ...");
                var views = await viewServ.GetViews(22708289);
                var view = from v in views
                           where v.Name == "[TaskAutomation]"
                           select v;
                context.Logger.LogLine($"Got View '[TaskAutomation]' ...");
                var op = new FilterOptions { Filters = view.First().Filters };
                var filter = await podio.FilterItems(22708289, op);
                context.Logger.LogLine($"Items in filter:{filter.Items.Count()}");

                foreach (var item in filter.Items)
                {
                    var responsibleMember = item.Field<ContactItemField>(ids.GetFieldId("Task List|Responsible Member"));
                    var title = item.Field<TextItemField>(ids.GetFieldId("Task List|Title"));
                    var date = item.Field<DateItemField>(ids.GetFieldId("Task List|Date"));
                    var description = item.Field<TextItemField>(ids.GetFieldId("Task List|Description"));
                    
                    var t = new TaskCreateUpdateRequest
                    {
                        Description = title.Value
                    };
                    List<int> cIds = new List<int>();
                    foreach (var contact in responsibleMember.Contacts)
                    {
                        cIds.Add(Convert.ToInt32(contact.UserId));
                    }
                    t.Private = false;
                    t.RefType = "item";
                    t.Id = item.ItemId;
                    t.SetResponsible(cIds);
                    t.DueDate = date.StartDate.GetValueOrDefault();
                    var task = await taskServ.CreateTask(t);
                    await taskServ.AssignTask(int.Parse(task.First().TaskId)); //neccessary?
                    context.Logger.LogLine($"Assigned Task");

                    var updateMe = new Item() { ItemId = item.ItemId };
                    var dupecheck = item.Field<CategoryItemField>(ids.GetFieldId("Task List|Task Assigned?"));
                    dupecheck.OptionText = "Yes";
                    await itemServ.UpdateItem(updateMe, hook: false);
                    context.Logger.LogLine($"Updated Item");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                await saasafrasClient.UnlockFunction(functionName, e.clientId, lockValue);
            }
        }
    }
}
