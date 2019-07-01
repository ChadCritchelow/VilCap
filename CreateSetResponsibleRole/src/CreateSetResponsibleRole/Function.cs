// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Core;
using newVilcapCopyFileToGoogleDrive;
using PodioCore.Items;
using PodioCore.Models;
using PodioCore.Models.Request;
using PodioCore.Utils.ItemFields;
using Saasafras;


[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace CreateSetResponsibleRole
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
            var functionName = "CreateSetResponsibleRole";
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
                var CheckScheduleItem = check;
                var UpdateScheduleItem = new Item() { ItemId = check.ItemId };
                var contactids = new List<int>();
                var esoMemberRole = CheckScheduleItem.Field<CategoryItemField>(ids.GetFieldId("Task List|ESO Member Role"));
                if( esoMemberRole.Options.Any() )
                {
                    var responsibleMember = UpdateScheduleItem.Field<ContactItemField>(ids.GetFieldId("Task List|Responsible Member"));
                    var esoValue = esoMemberRole.Options.First().Text;
                    switch( esoValue )
                    {
                        case "Programs Associate":

                            var programAssociates = AdminOptionToCheck.Field<ContactItemField>(ids.GetFieldId("Admin|Program Associate"));
                            foreach( var contact in programAssociates.Contacts )
                            {
                                contactids.Add(contact.ProfileId);
                            }
                            responsibleMember.ContactIds = contactids;
                            break;
                        case "Investment Analyst":
                            var InvestmentsAnalysts = AdminOptionToCheck.Field<ContactItemField>(ids.GetFieldId("Admin|Investments Analyst"));
                            foreach( var contact in InvestmentsAnalysts.Contacts )
                            {
                                contactids.Add(contact.ProfileId);
                            }
                            responsibleMember.ContactIds = contactids;
                            break;
                        case "Program Manager":
                            var programManagers = AdminOptionToCheck.Field<ContactItemField>(ids.GetFieldId("Admin|Program Manager"));
                            foreach( var contact in programManagers.Contacts )
                            {
                                contactids.Add(contact.ProfileId);
                            }
                            responsibleMember.ContactIds = contactids;
                            break;
                        case "Program Director":
                            var programDirectors = AdminOptionToCheck.Field<ContactItemField>(ids.GetFieldId("Admin|Program Director"));
                            foreach( var contact in programDirectors.Contacts )
                            {
                                contactids.Add(contact.ProfileId);
                            }
                            responsibleMember.ContactIds = contactids;
                            break;
                    }
                    await podio.UpdateItem(UpdateScheduleItem, true);
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
