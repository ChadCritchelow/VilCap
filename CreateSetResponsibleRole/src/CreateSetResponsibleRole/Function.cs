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
            #region // Generic Setup //
            var factory = new AuditedPodioClientFactory(e.solutionId, e.version, e.clientId, e.environmentId);
            var podio = factory.ForClient(e.clientId, e.environmentId);
            var item = await podio.GetItem(Convert.ToInt32(e.podioEvent.item_id));
            var saasafrasClient = new SaasafrasClient(Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
            var dictChild = await saasafrasClient.GetDictionary(e.clientId, e.environmentId, e.solutionId, e.version);
            var dictMaster = await saasafrasClient.GetDictionary("vcadministration", "vcadministration", "vilcap", "0.0");
            string lockValue;
            var ids = new GetIds(dictChild, dictMaster, e.environmentId);
            var functionName = "CreateSetResponsibleRole";
            lockValue = await saasafrasClient.LockFunction(functionName, item.ItemId.ToString());
            if( string.IsNullOrEmpty(lockValue) )
            {
                context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {item.ItemId}");
                return;
            }
            #endregion

            try
            {
                //context.Logger.LogLine("Checking for duplicates");
                var items = await podio.FilterItems(ids.Get("Admin"), new FilterOptions { Limit = 1 });
                var AdminOptionToCheck = await podio.GetItem(items.Items.First().ItemId);
                var contactids = new List<int>();
                var esoMemberRole = item.Field<CategoryItemField>(ids.Get("Task List|ESO Member Role"));
                if( esoMemberRole.Options.Any() )
                {
                    var responsibleMember = new Item() { ItemId = item.ItemId }.Field<ContactItemField>(ids.Get("Task List|Responsible Member"));
                    var esoValue = esoMemberRole.Options.First().Text;
                    switch( esoValue )
                    {
                        default:
                            break;
                        case "Programs Associate":
                        case "Investment Analyst":
                        case "Program Manager":
                        case "Program Director":
                            responsibleMember.ContactIds =
                                AdminOptionToCheck.Field<ContactItemField>(ids.Get($"Admin|{esoValue}")).Contacts.Select(X => X.ProfileId);
                            break;
                    }
                    await podio.UpdateItem(new Item() { ItemId = item.ItemId }, true);
                }
            }
            catch( Exception ex )
            {
                throw ex;
            }
            finally
            {
                await saasafrasClient.UnlockFunction(functionName, item.ItemId.ToString(), lockValue);
            }
        }
    }
}
