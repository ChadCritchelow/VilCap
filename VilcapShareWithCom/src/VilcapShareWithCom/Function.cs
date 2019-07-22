using System;
using System.Collections.Generic;
using Amazon.Lambda.Core;
using newVilcapCopyFileToGoogleDrive;
using PodioCore.Exceptions;
using PodioCore.Items;
using PodioCore.Models;
using PodioCore.Services;
using PodioCore.Utils.ItemFields;
using Saasafras;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VilcapShareWithCom
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
            //
            var functionName = "VilcapShareWithCom";
            lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
            try
            {
                if( string.IsNullOrEmpty(lockValue) )
                {
                    context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
                    return;
                }
                //When an item is created in Diligence and Selection:
                var em = check.Field<EmailItemField>(ids.Get("Diligence and Selection|Shared Email"));
                var m = "Please rate this application";
                var email = em;
                var updateMe = new Item() { ItemId = check.ItemId };
                updateMe.Field<CategoryItemField>(ids.Get("Diligence and Selection|Status")).OptionText = "Not Scored";
                await podio.UpdateItem(updateMe, true);
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


                var waitSeconds = 5;
            CallPodioG: // Create Grant
                try
                {
                    Console.WriteLine($"Trying to create a Grant ...");
                    await serv.CreateGrant("item", check.ItemId, people, "rate", message);
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

            }
            catch( Exception ex )
            {
                Console.WriteLine($"Exception Details: {ex.Message}");
                throw ex;
            }
            finally
            {
                await saasafrasClient.UnlockFunction(functionName, check.ItemId.ToString(), lockValue);
            }
        }
    }
}
