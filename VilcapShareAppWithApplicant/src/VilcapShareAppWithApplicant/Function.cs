using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Core;
using newVilcapCopyFileToGoogleDrive;
using PodioCore.Items;
using PodioCore.Models;
using PodioCore.Models.Request;
using PodioCore.Services;
using PodioCore.Utils.ItemFields;
using Saasafras;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VilcapShareAppWithApplicant
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
            var functionName = "VilcapShareAppWithApplicant";
            lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
            try
            {
                if( string.IsNullOrEmpty(lockValue) )
                {
                    context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
                    return;
                }

                var fieldIdToSearch = ids.GetFieldId("Admin");
                var newOptions = new FilterOptions
                {
                    Limit = 1
                };
                context.Logger.LogLine("Checking for duplicates");

                var items = await podio.FilterItems(ids.GetFieldId("Admin"), newOptions);
                var AdminOptionToCheck = await podio.GetItem(items.Items.First().ItemId);

                var serv = new GrantService(podio);
                //Create Email:
                var recipient = check.Field<EmailItemField>(ids.GetFieldId("Applications|Email")).Value.First().Value;
                var orgName = AdminOptionToCheck.Field<TextItemField>(ids.GetFieldId("Admin|Organization Name")).Value;
                var m = $"Invitation to Complete Your Application with {orgName}. " +
                "This application will automatically save as you work on it. You are advised " +
                "to either save your invitation email or bookmark your in-progress application for easy access. " +
                "You can view all of your Podio items by following the following link : <https://podio.com/vilcapcom/organization/grants>";

                //Send email
                var email = recipient;

                var people = new List<Ref>();
                var person = new Ref
                {
                    Type = "mail",
                    Id = email
                };
                people.Add(person);
                var message = m;

                await serv.CreateGrant("item", check.ItemId, people, "view", message);

                var updateMe = new Item() { ItemId = check.ItemId };
                updateMe.Field<CategoryItemField>(ids.GetFieldId("Applications|Application Status")).OptionText = "New Application";
                await podio.UpdateItem(updateMe, true);
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
