using Amazon.Lambda.Core;
using newVilcapCopyFileToGoogleDrive;
using PodioCore.Exceptions;
using PodioCore.Items;
using PodioCore.Models;
using PodioCore.Models.Request;
using PodioCore.Services;
using PodioCore.Utils.ItemFields;
using Saasafras;
using System;
using System.Collections.Generic;
using System.Linq;
using Task = System.Threading.Tasks.Task;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VilcapShareAppWithApplicant
{
    public class Function
    {
        public async Task FunctionHandler(RoutedPodioEvent e, ILambdaContext context)
        {
            #region // Generic Setup //
            var factory = new AuditedPodioClientFactory(e.solutionId, e.version, e.clientId, e.environmentId);
            var podio = factory.ForClient(e.clientId, e.environmentId);
            var check = await podio.GetItem(Convert.ToInt32(e.podioEvent.item_id));
            var saasafrasClient = new SaasafrasClient(Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
            var dictChild = await saasafrasClient.GetDictionary(e.clientId, e.environmentId, e.solutionId, e.version);
            var dictMaster = await saasafrasClient.GetDictionary("vcadministration", "vcadministration", "vilcap", "0.0");
            string lockValue;
            var ids = new GetIds(dictChild, dictMaster, e.environmentId);
            var functionName = "VilcapShareAppWithApplicant";
            lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
            if (string.IsNullOrEmpty(lockValue))
            {
                context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
                return;
            }
            #endregion
            try
            {
                var fieldIdToSearch = ids.Get("Admin");
                var newOptions = new FilterOptions { Limit = 1 };
                var items = await podio.FilterItems(ids.Get("Admin"), newOptions);
                var AdminOptionToCheck = await podio.GetItem(items.Items.First().ItemId);
                var serv = new GrantService(podio);

                //Create Email:
                var orgName = AdminOptionToCheck.Field<TextItemField>(ids.Get("Admin|Organization Name")).Value;
                var email = check.Field<EmailItemField>(ids.Get("Applications|Email")).Value.First().Value;
                var people = new List<Ref> { new Ref { Type = "mail", Id = email } };
                var message = $"Invitation to Complete Your Application with {orgName}. " +
                "This application will automatically save as you work on it. You are advised " +
                "to either save your invitation email or bookmark your in-progress application for easy access. " +
                "You can view all of your Podio items by following the following link : <https://podio.com/vilcapcom/organization/grants>";

                //Send Email:

                var waitSeconds = 5;
                var updateMe = new Item() { ItemId = check.ItemId };
                updateMe.Field<CategoryItemField>(ids.Get("Applications|Application Status")).OptionText = "New Application";
            CallPodioG:
                try
                {
                    Console.WriteLine($"Trying to create a Grant ...");
                    await serv.CreateGrant("item", check.ItemId, people, "view", message);
                }
                catch (PodioUnavailableException ex)
                {
                    Console.WriteLine($"{ex.Message}");
                    Console.WriteLine($"Trying again in {waitSeconds} seconds.");
                    for (var i = 0; i < waitSeconds; i++)
                    {
                        System.Threading.Thread.Sleep(1000);
                        Console.WriteLine(".");
                    }
                    waitSeconds *= 2;
                    goto CallPodioG;
                }
                Console.WriteLine($"Created grant");

            CallPodioU:
                try
                {
                    Console.WriteLine($"Trying to update an Item ...");
                    await podio.UpdateItem(updateMe, true);
                }
                catch (PodioUnavailableException ex)
                {
                    Console.WriteLine($"{ex.Message}");
                    Console.WriteLine($"Trying again in {waitSeconds} seconds.");
                    for (var i = 0; i < waitSeconds; i++)
                    {
                        System.Threading.Thread.Sleep(1000);
                        Console.WriteLine(".");
                    }
                    waitSeconds *= 2;
                    goto CallPodioU;
                }
                Console.WriteLine($"... Updated Item");

                
                
            }
            catch (Exception ex) { throw ex; }
            finally { await saasafrasClient.UnlockFunction(functionName, check.ItemId.ToString(), lockValue); }
        }
    }
}
