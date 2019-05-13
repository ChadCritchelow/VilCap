using Amazon.Lambda.Core;
using Amazon.Lambda.CloudWatchEvents;
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
using Task = System.Threading.Tasks.Task;
using Newtonsoft.Json;

namespace VilcapDateAssignTask
{
    public class CloudWatchHandler
    {
        public async Task CloudFunctionHandler(Amazon.Lambda.CloudWatchEvents.ScheduledEvents.ScheduledEvent cw, ILambdaContext context)
        {
            RoutedPodioEvent e = new RoutedPodioEvent();
            e.clientId = "toolkittemplate3";
            e.environmentId = "toolkittemplate3";
            e.solutionId = "vilcap";
            e.version = "0.0";

            var function = new Function();
            await function.FunctionHandler(e, context);

            return;


            var detail = cw.Detail;
            context.Logger.LogLine($"---{cw.Id}");
            //context.Logger.LogLine($"---{detail.ToString()}");

            var factory = new AuditedPodioClientFactory(e.solutionId, e.version, e.clientId, e.environmentId);
            var podio = factory.ForClient(e.clientId, e.environmentId);
            SaasafrasClient saasafrasClient = new SaasafrasClient(System.Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), System.Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
            var dictChild = await saasafrasClient.GetDictionary(e.clientId, e.environmentId, e.solutionId, e.version);
            var dictMaster = await saasafrasClient.GetDictionary("vcadministration", "vcadministration", "vilcap", "0.0");
            string lockValue;
            GetIds ids = new GetIds(dictChild, dictMaster, e.environmentId);
            //Make sure to implement by checking to see if Deploy Curriculum has just changed
            //Deploy Curriculum field
            string functionName = "VilcapDateAssignTask";
            lockValue = await saasafrasClient.LockFunction(functionName, cw.Time.ToString());
            try
            {
                if (string.IsNullOrEmpty(lockValue))
                {
                    context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {cw.Time.ToString()}");
                    return;
                }

                TaskService taskServ = new TaskService(podio);

                var fieldIdToSearch = ids.GetFieldId("Task List|Date");
                var filterValue = DateTime.Now.AddDays(7).Ticks;
                var filter = new Dictionary<int, object>
                            {
                                { fieldIdToSearch, filterValue }
                            };
                FilterOptions newOptions = new FilterOptions
                {
                    Filters = filter,
                };
                context.Logger.LogLine("Checking for duplicates.");


                var filteredItems = await podio.FilterItems(ids.GetFieldId("Task List"), newOptions);
                context.Logger.LogLine("Initial Filter Successful");

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
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                await saasafrasClient.UnlockFunction(functionName, cw.Time.ToString(), lockValue);
            }
        }
    }
}
