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

namespace VilcapShareDocument
{
    /// <summary>
    /// Runs on Cohort Documement|item.create
    /// </summary>
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
            var functionName = "VilcapShareDocument";
            lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
            try
            {
                if( string.IsNullOrEmpty(lockValue) )
                {
                    context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
                    return;
                }

                var serv = new GrantService(podio);
                var people = new List<Ref>();
                var entrepreneurs = check.Field<AppItemField>(ids.Get("Cohort Documents|Entreprenuers"));
                context.Logger.LogLine($"--- count: {entrepreneurs.Items.Count()}");

                foreach( var entrepreneur in entrepreneurs.Items )
                {
                    var item = await podio.GetItem(entrepreneur.ItemId);
                    var fieldId = ids.Get("Entrepreneurs|Entrepreneur Email");
                    var emailField = item.Field<EmailItemField>(fieldId);
                    var email = emailField.Value.FirstOrDefault().Value;
                    var person = new Ref
                    {
                        Type = "mail",
                        Id = email
                    };
                    people.Add(person);
                    context.Logger.LogLine($"--- Added Email: {email}");
                }

                var description = check.Field<TextItemField>(ids.Get("Cohort Documents|Docment Desciption")).Value;
                var message = $"Thank you for sending us your documents {description}. Please follow this link to view your submission.";
                await serv.CreateGrant("item", check.ItemId, people, "view", message);

                context.Logger.LogLine("--- Created grant(s)");
                if( string.IsNullOrEmpty(description) )
                {
                    var docName = check.Files[0].Name;
                    var updateMe = new Item() { ItemId = check.ItemId };
                    description = updateMe.Field<TextItemField>(ids.Get("Cohort Documents|Docment Desciption")).Value;
                    await podio.UpdateItem(updateMe, true);
                }
                context.Logger.LogLine("--- ok");

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
