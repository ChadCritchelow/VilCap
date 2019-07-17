using System;
using System.Linq;
using System.Text.RegularExpressions;
using Amazon.Lambda.Core;
using newVilcapCopyFileToGoogleDrive;
using PodioCore.Comments;
using PodioCore.Items;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using Saasafras;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace CommentNextBatch
{
    public class CurrentEnvironment
    {
        public string environmentId { get; set; }
    }
    public class PodioCommentEvent
    {
        public string type { get; set; }
        public string item_id { get; set; }
        public string comment_id { get; set; }
    }
    public class RoutedCommentEvent
    {
        public CurrentEnvironment currentEnvironment { get; set; }
        public PodioCommentEvent podioEvent { get; set; }
        public string clientId { get; set; }
        public string version { get; set; }
        public string appId { get; set; }
    }

    /// <summary>
    /// Admin|comment.create -->
    /// Increments the TL or WS batch counter
    /// </summary>
    public class Function
    {
        public async System.Threading.Tasks.Task FunctionHandler( RoutedCommentEvent e, ILambdaContext context )
        {
            #region //Required code for all Vilcap Lambda Functions//
            context.Logger.LogLine(Newtonsoft.Json.JsonConvert.SerializeObject(e));
            context.Logger.LogLine(Newtonsoft.Json.JsonConvert.SerializeObject(e));
            var factory = new AuditedPodioClientFactory(e.appId, e.version, e.clientId, e.currentEnvironment.environmentId);
            var podio = factory.ForClient(e.clientId, e.currentEnvironment.environmentId);
            var check = await podio.GetItem(Convert.ToInt32(e.podioEvent.item_id));
            var saasafrasClient = new SaasafrasClient(Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
            var dictChild = await saasafrasClient.GetDictionary(e.clientId, e.currentEnvironment.environmentId, e.appId, e.version);
            var dictMaster = await saasafrasClient.GetDictionary("vcadministration", "vcadministration", "vilcap", "0.0");
            string lockValue;
            var ids = new GetIds(dictChild, dictMaster, e.currentEnvironment.environmentId);
            var functionName = "CommentNextBatch";
            lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
            #endregion
            try
            {
                #region //Locker//
                if( string.IsNullOrEmpty(lockValue) )
                {
                    context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
                    return;
                }
                #endregion
                var serve = new CommentService(podio);
                var commentToCheck = await serve.GetComment(int.Parse(e.podioEvent.comment_id));
                var fieldId = 0;
                var commentPieces = commentToCheck.Value.Split(" ");
                var type = commentPieces[0];
                var batch = commentPieces[2];
                var isNum = Regex.IsMatch(batch, @"^\d+$");
                var updateMe = new Item() { ItemId = check.ItemId };
                int currentBatch;
                if( isNum )
                {
                    try
                    {
                        int.TryParse(batch, out currentBatch);
                        context.Logger.LogLine($"Current batch: {currentBatch}");
                    }
                    catch( Exception ex )
                    {
                        context.Logger.LogLine("It seems batch number could not be found");
                        throw ex;
                    }
                    switch( type )
                    {
                        case "WS":
                            #region //update WS Batch number//
                            context.Logger.LogLine("Type==WS");
                            context.Logger.LogLine("Getting Field ID");
                            fieldId = ids.GetFieldId("Admin|WS Batch");
                            context.Logger.LogLine($"Field ID: {fieldId}");
                            var wsBatchField = updateMe.Field<CategoryItemField>(fieldId);
                            context.Logger.LogLine("Got Field");
                            context.Logger.LogLine("Adding 1 to current batch");
                            wsBatchField.OptionText = (++currentBatch).ToString();
                            context.Logger.LogLine($"New Batch Value: {currentBatch}");
                            break;
                        #endregion
                        case "TL":
                            #region //update TL Batch number//
                            context.Logger.LogLine("Type==TL");
                            context.Logger.LogLine("Getting Field ID");
                            fieldId = ids.GetFieldId("Admin|TL Batch");
                            context.Logger.LogLine($"Field ID: {fieldId}");
                            var tlBatchField = updateMe.Field<CategoryItemField>(fieldId);
                            context.Logger.LogLine("Got Field");
                            context.Logger.LogLine("Adding 1 to current batch");
                            tlBatchField.OptionText = (++currentBatch).ToString();
                            context.Logger.LogLine($"New Batch Value: {currentBatch}");
                            break;
                            #endregion
                    }
                    context.Logger.LogLine($"Field count on item we're updating: {updateMe.Fields.Count()}");
                    await podio.UpdateItem(updateMe, true);
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
