using System;
using System.Linq;
using Amazon.Lambda.Core;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using PodioCore;
using PodioCore.Comments;
using PodioCore.Items;
using PodioCore.Models;
using PodioCore.Services;
using PodioCore.Utils.ItemFields;
using Saasafras;
using Task = System.Threading.Tasks.Task;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]


namespace newVilcapCopyFileToGoogleDrive
{
    public class newVilcapCopyFileToGoogleDrive
    {

        #region  // Utility Vars //
        // private
        private static readonly string[] Scopes = { DriveService.Scope.Drive };
        private static readonly string ApplicationName = "BrickBridgeVilCap";
        //private Dictionary<string, string> dictChild;
        //private Dictionary<string, string> dictMaster;
        private string commentText = null;

        // public 

        public static readonly int MASTER_SCHEDULE_APP = 21481130;
        public static readonly int MASTER_CONTENT_APP = 21310273;
        public static readonly int MASTER_SURVEY_APP = 21389770;
        public ILambdaContext context { get; set; }
        public Podio podio { get; set; }
        public Item item { get; set; }
        public RoutedPodioEvent e { get; set; }
        public DriveService service { get; set; }
        public GetIds ids { get; set; }
        public GoogleIntegration google { get; set; }
        public PreSurvAndExp pre { get; set; }
        public ViewService viewServ { get; set; }
        #endregion

        public async Task FunctionHandler( RoutedPodioEvent e, ILambdaContext context )
        {
            podio = new AuditedPodioClientFactory(e.solutionId, e.version, e.clientId, e.environmentId).ForClient(e.clientId, e.environmentId);
            item = await podio.GetItem(Convert.ToInt32(e.podioEvent.item_id));
            //context.Logger.LogLine($"Got item with ID: {item.ItemId}");
            var saasafrasClient = new SaasafrasClient(Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
            var functionName = "VilcapDeploy";

            var cred = GoogleCredential.FromJson(Environment.GetEnvironmentVariable("GOOGLE_SERVICE_ACCOUNT")).CreateScoped(Scopes).UnderlyingCredential;
            service = new DriveService(new BaseClientService.Initializer() { HttpClientInitializer = cred, ApplicationName = ApplicationName });

            google = new GoogleIntegration();
            //var saasGoogleIntegration = new SaasafrasGoogleIntegration();
            pre = new PreSurvAndExp();
            ids = new GetIds(
                await saasafrasClient.GetDictionary(e.clientId, e.environmentId, e.solutionId, e.version),
                await saasafrasClient.GetDictionary("vcadministration", "vcadministration", "vilcap", "0.0"),
                e.environmentId);
            var comm = new CommentService(podio);
            viewServ = new ViewService(podio);

            // Main Process //

            var revision = await podio.GetRevisionDifference(Convert.ToInt32(item.ItemId), item.CurrentRevision.Revision - 1, item.CurrentRevision.Revision);
            var firstRevision = revision.First();
            context.Logger.LogLine($"Last Revision field: {firstRevision.Label}");

            var buttonPresser = item.CurrentRevision.CreatedBy;
            //context.Logger.LogLine($"Item updated by {buttonPresser.Name} (Should be 'Vilcap Admin')");
            if( buttonPresser.Id.GetValueOrDefault() != 4610903 )
            {
                context.Logger.LogLine("User ' https://podio.com/users/" + buttonPresser.Id + " ' is not authorized to perform this action.");
                return;
            }
            var lockString = item.ItemId.ToString() + "_" + firstRevision.Label;
            var lockValue = await saasafrasClient.LockFunction(functionName, lockString);
            if( string.IsNullOrEmpty(lockValue) )
            {
                context.Logger.LogLine($"Failed to acquire lock for {functionName} | {lockString}");
                return;
            }
            context.Logger.LogLine($"Lock Value: {lockValue}");

            switch( firstRevision.Label )
            {

                case "WS Batch":
                    #region // Create Workshops //
                    var wsBatchId = ids.Get("Admin|WS Batch");
                    if( item.Field<CategoryItemField>(wsBatchId).Options.Any() )
                    {
                        context.Logger.LogLine($"Running 'WS Batch {item.Field<CategoryItemField>(wsBatchId).Options.First().Text}'");
                        var nextBatch = -1;
                        //lockValue = await saasafrasClient.LockFunction(functionName, item.ItemId.ToString());

                        try
                        {
                            var wm = new WorkshopModules2();
                            nextBatch = await wm.CreateWorkshopModules2(this);

                            if( nextBatch > 1 )
                            {
                                commentText = $"WS Batch {nextBatch - 1} Completed.";
                                item.Field<CategoryItemField>(ids.Get("Admin|WS Batch")).OptionText = $"{nextBatch}";
                                await saasafrasClient.UnlockFunction(functionName, lockString, lockValue);
                                await comm.AddCommentToObject("item", item.ItemId, commentText, hook: true);
                                //await podio.UpdateItem(item, hook: true);
                                return;
                            }
                            else if( nextBatch == -1 )
                            {
                                commentText = $":loudspeaker: All WS Batches Completed!";
                                await comm.AddCommentToObject("item", item.ItemId, commentText, hook: false);
                            }
                        }

                        catch( Exception ex )
                        {
                            context.Logger.LogLine($"Exception Details: {ex} - {ex.Data} - {ex.HelpLink} - {ex.HResult} - {ex.InnerException} " +
                                $"- {ex.Message} - {ex.Source} - {ex.StackTrace} - {ex.TargetSite}");
                            commentText = "Sorry, something went wrong. Please try again in 5 minutes or contact the administrator.";
                            await comm.AddCommentToObject("item", item.ItemId, $":loudspeaker: {commentText}", hook: false);

                        }

                        finally
                        {
                            await saasafrasClient.UnlockFunction(functionName, lockString, lockValue);
                        }
                    }
                    break;
                #endregion

                case "Deploy Addons":
                    #region // Deploy Addon Modules //
                    var aoBatchId = ids.Get("Admin|Deploy Addons");
                    if( item.Field<CategoryItemField>(aoBatchId).Options.Any() )
                    {
                        context.Logger.LogLine($"Running 'WS Batch {item.Field<CategoryItemField>(aoBatchId).Options.First().Text}'");
                        var nextBatch = -1;
                        //lockValue = await saasafrasClient.LockFunction(functionName, item.ItemId.ToString());

                        try
                        {


                            var ao = new Addons();
                            nextBatch = await ao.CreateAddons(context, podio, item, e, service, ids, google, pre);
                            break;
                        }

                        catch( Exception ex )
                        {
                            context.Logger.LogLine($"Exception Details: {ex} - {ex.Data} - {ex.HelpLink} - {ex.HResult} - {ex.InnerException} " +
                                $"- {ex.Message} - {ex.Source} - {ex.StackTrace} - {ex.TargetSite}");
                            commentText = "Sorry, something went wrong. Please try again in 5 minutes or contact the administrator.";
                            await comm.AddCommentToObject("item", item.ItemId, $":loudspeaker: {commentText}", hook: false);
                        }

                        finally
                        {
                            await saasafrasClient.UnlockFunction(functionName, lockString, lockValue);
                        }
                    }
                    break;
                #endregion

                //case "Deploy Task List":
                //    var deploy = ids.GetFieldId("Admin|Deploy Task List");
                //    if (item.Field<CategoryItemField>(deploy).Options.Any());
                //    break;

                case "TL Batch":
                    #region // Create Task List //
                    var tlBatchId = ids.Get("Admin|TL Batch");
                    if( item.Field<CategoryItemField>(tlBatchId).Options.Any() )
                    {
                        context.Logger.LogLine($"Running 'TL Batch {item.Field<CategoryItemField>(tlBatchId).Options.First().Text}'");
                        var nextBatch = -1;
                        //lockValue = await saasafrasClient.LockFunction(functionName, item.ItemId.ToString());
                        try
                        {
                            var tl = new TaskList2();
                            nextBatch = await tl.CreateTaskLists(this);

                            if( nextBatch > 1 )
                            {
                                commentText = $"TL Batch {nextBatch - 1} Completed.";
                                item.Field<CategoryItemField>(ids.Get("Admin|TL Batch")).OptionText = $"{nextBatch}";
                                await saasafrasClient.UnlockFunction(functionName, lockString, lockValue);
                                await comm.AddCommentToObject("item", item.ItemId, commentText, hook: true);
                                //await podio.UpdateItem(item, hook: true);
                                return;
                            }
                            else if( nextBatch == -1 )
                            {
                                commentText = $":loudspeaker: All TL Batches Completed!";
                                await comm.AddCommentToObject("item", item.ItemId, commentText, hook: false);
                            }
                        }
                        catch( Exception ex )
                        {
                            context.Logger.LogLine($"Exception Details: {ex} - {ex.Data} - {ex.HelpLink} - {ex.HResult} - {ex.InnerException} " +
                                $"- {ex.Message} - {ex.Source} - {ex.StackTrace} - {ex.TargetSite}");
                            commentText = "Sorry, something went wrong. Please try again in 5 minutes or contact the administrator.";
                            await comm.AddCommentToObject("item", item.ItemId, $":loudspeaker: {commentText}", hook: false);
                        }
                        finally
                        {
                            await saasafrasClient.UnlockFunction(functionName, lockString, lockValue);
                        }
                    }
                    break;
                #endregion

                default:
                    context.Logger.LogLine($"NO ACTION: Value '{firstRevision.Label}' not Recognized.");
                    await saasafrasClient.UnlockFunction(functionName, lockString, lockValue);
                    break;
            }
        }
    }
}