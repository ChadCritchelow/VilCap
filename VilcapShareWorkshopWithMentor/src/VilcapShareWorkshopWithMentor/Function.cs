using System;
using System.Collections.Generic;
using System.Linq;

using Amazon.Lambda.Core;
using newVilcapCopyFileToGoogleDrive;
using PodioCore.Items;
using PodioCore.Models;
using PodioCore.Services;
using PodioCore.Utils.ItemFields;
using Saasafras;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VilcapShareWorkshopWithMentor
{
    public class Function
    {
        public async System.Threading.Tasks.Task FunctionHandler(RoutedPodioEvent e, ILambdaContext context)
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
            var functionName = "VilcapShareWorkshopWithMentor";
            lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
            try
            {
                if (string.IsNullOrEmpty(lockValue))
                {
                    context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
                    return;
                }

                //When an item is created in Entrepreneurs:
                var email = check.Field<EmailItemField>(ids.Get("Program Support|Email")).Value.First().Value;
                var m = $"Event attendance confirmation: {check.Field<AppItemField>(ids.Get("Program Support|Workshop Sessions")).Items.First().Title}";
                var serv = new GrantService(podio);
                //Send email

                var people = new List<Ref>();
                var person = new Ref
                {
                    Type = "mail",
                    Id = email
                };
                people.Add(person);
                var message = m;

                await serv.CreateGrant("item", check.ItemId, people, "view", message);
            }

            catch (Exception ex)
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
//