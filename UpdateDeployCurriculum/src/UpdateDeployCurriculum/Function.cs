using System;
using System.Linq;
using Amazon.Lambda.Core;
using newVilcapCopyFileToGoogleDrive;
using PodioCore.Comments;
using PodioCore.Items;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using Saasafras;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace UpdateDeployCurriculum
{
    public class Function
    {
        public async System.Threading.Tasks.Task FunctionHandler( RoutedPodioEvent e, ILambdaContext context )
        {
            const int ADMIN_ID = 4610903;
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
            var functionName = "UpdateDeployCurriculum";
            lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
            try
            {
                if( string.IsNullOrEmpty(lockValue) )
                {
                    context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
                    return;
                }

                var revision = await podio.GetRevisionDifference(Convert.ToInt32(check.ItemId), check.CurrentRevision.Revision - 1, check.CurrentRevision.Revision);
                var firstRevision = revision.First();
                if(check.CurrentRevision.CreatedBy.Id.GetValueOrDefault() != ADMIN_ID)
                {
                    context.Logger.LogLine("User 'https://podio.com/users/" + check.CurrentRevision.CreatedBy.Id.GetValueOrDefault()+ "' is not authorized to perform this action.");
                    await new CommentService(podio).AddCommentToObject("item", check.ItemId,
                        $":loudspeaker: User ' https://podio.com/users/" + check.CurrentRevision.CreatedBy.Id.GetValueOrDefault() + " ' is not authorized to perform this action.", hook: false);
                    return;
                }
                switch( firstRevision.Label )
                {
                    case "Deploy Task List":
                        var deployField = check.Field<CategoryItemField>(ids.GetFieldId("Admin|Deploy Task List"));
                        if( firstRevision.FieldId == deployField.FieldId && deployField.Options.Any() && deployField.Options.First().Text == "Deploy" )
                        {
                            var update = new Item() { ItemId = check.ItemId };
                            var tlBatch = update.Field<CategoryItemField>(ids.GetFieldId("Admin|TL Batch"));
                            if( tlBatch.Options.First().Text == "1" )
                            {
                                context.Logger.LogLine("... Reseting batch # field ... ");
                                tlBatch.OptionText = null;
                                await podio.UpdateItem(update, false);
                            }
                            tlBatch.OptionText = "1";
                            await podio.UpdateItem(update, true);
                        }
                        break;
                    case "Deploy Curriculum":
                        deployField = check.Field<CategoryItemField>(ids.GetFieldId("Admin|Deploy Curriculum"));
                        if( firstRevision.FieldId == deployField.FieldId && deployField.Options.Any() && deployField.Options.First().Text == "Deploy" )
                        {
                            var update = new Item() { ItemId = check.ItemId };
                            var wsBatch = update.Field<CategoryItemField>(ids.GetFieldId("Admin|WS Batch"));
                            if (wsBatch.Options.First().Text == "1")
                            {
                                context.Logger.LogLine("... Reseting batch # field ... ");
                                wsBatch.OptionText = null;
                                await podio.UpdateItem(update, false);
                            }
                            wsBatch.OptionText = "1";
                            await podio.UpdateItem(update, true);
                        }
                        break;
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
