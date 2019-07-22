using System;
using System.Collections.Generic;
using System.Linq;
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

namespace VilcapShareWithEnt
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
            var functionName = "VilcapShareWithEnt";
            lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
            try
            {
                if( string.IsNullOrEmpty(lockValue) )
                {
                    context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
                    return;
                }
                //When an item is created in Entrepreneurs:
                var email = check.Field<EmailItemField>(ids.Get("Entrepreneurs|Entrepreneur Email")).Value.First().Value;
                var m = $"Please create an account and tell us about your time at {check.Field<AppItemField>(ids.Get("Entrepreneurs|Company")).Items.First().Title}";
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
