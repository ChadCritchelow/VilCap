using System;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Core;
using PodioCore.Utils.ItemFields;
using PodioCore.Items;
using PodioCore.Models;
using BrickBridge.Lambda.VilCap;
using Task = System.Threading.Tasks.Task;
using PodioCore.Models.Request;
using System.Text.RegularExpressions;
using PodioCore.Utils;
using Saasafras;
using PodioCore.Comments;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]


namespace newVilcapCopyFileToGoogleDrive
{


    public class newVilcapCopyFileToGoogleDrive
    {
		
        static string[] Scopes = { DriveService.Scope.Drive };
        static string ApplicationName = "BrickBridgeVilCap";
        static LambdaMemoryStore memoryStore = new LambdaMemoryStore();
		int fieldId = 0;
		Dictionary<string, string> dictChild;
		Dictionary<string, string> dictMaster;
		Dictionary<string, string> fullNames;
		RoutedPodioEvent ev;
		string commentText=null;

		public static string StripHTML(string input)
		{
			return Regex.Replace(input, "<.*?>", String.Empty);
		}
		
		public async Task FunctionHandler(RoutedPodioEvent e, ILambdaContext context)
		{
			string lockValue;
			ev = e;
			var factory = new AuditedPodioClientFactory(e.solutionId, e.version, e.clientId, e.environmentId);
			var podio = factory.ForClient(e.clientId, e.environmentId);
			context.Logger.LogLine("Getting Podio Instance");
			Item check = await podio.GetItem(Convert.ToInt32(e.podioEvent.item_id));
			context.Logger.LogLine($"Got item with ID: {check.ItemId}");
            SaasafrasClient saasafrasClient = new SaasafrasClient(System.Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), System.Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
            context.Logger.LogLine("Getting BBC Client Instance");
			dictChild = await saasafrasClient.GetDictionary(e.clientId, e.environmentId, e.solutionId, e.version);
			dictMaster = await saasafrasClient.GetDictionary("vcadministration", "vcadministration", "vilcap", "0.0");
			context.Logger.LogLine("Got dictionary");
			var functionName = "newVilcapCopyFileToGoogleDrive";
			fullNames = new Dictionary<string, string>()
			{
				{"andela" ,"Andela"},
				{"anza" ,"Anza"},
				{"bluemoon" ,"blueMoon"},
				{"energygeneration" ,"Energy Generation"},
                {"energygeneration2", "Energy Generation 2" },
				{"entreprenarium" ,"Entreprenarium"},
				{"etrilabs" ,"Etrilabs"},
				{"globalentrepreneurshipnetwork" ,"Global Entrepreneurship Network (GEN) Freetown"},
				{"growthmosaic" ,"Growth Mosaic"},
				{"jokkolabs" ,"Jokkolabs"},
				{"privatesectorhealthallianceofnigeria" ,"Private Sector Health Alliance of Nigeria"},
				{"southernafricaventurepartnership" ,"Southern Africa Venture Partnership (SAVP)"},
				{"suguba" ,"Suguba"},
				{"sycomoreventure" ,"Sycomore Venture"},
				{"theinnovationvillage" ,"The Innovation Village"},
				{"universityofbritishcolumbia" ,"University of British Columbia"},
				{"venturesplatform" ,"Ventures Platform"},
				{"toolkittemplate" ,"VC Toolkit Template"},
                {"toolkittemplate2", "VC Toolkit Template 2" },
                {"usfintech2019" ,"US Fintech 2019" },
                {"useducation2019", "US Education 2019" },
                {"wepowerenvironment" ,"WePower" },
                {"middlegameventures", "Middlegame Ventures" }//,
                //{"testuseducation2019", "TEST - US Education 2019" }
            };

			string serviceAcccount = System.Environment.GetEnvironmentVariable("GOOGLE_SERVICE_ACCOUNT");
			var cred = GoogleCredential.FromJson(serviceAcccount).CreateScoped(Scopes).UnderlyingCredential;
			var service = new DriveService(new BaseClientService.Initializer()
			{
				HttpClientInitializer = cred,
				ApplicationName = ApplicationName,
			});
			context.Logger.LogLine("Established google connection");
			//TODO: Address date calc
			context.Logger.LogLine($"App: {check.App.Name}");

			GoogleIntegration google = new GoogleIntegration();
			PreSurvAndExp pre = new PreSurvAndExp();
			GetIds ids = new GetIds(dictChild,dictMaster,fullNames,e);
            CommentService comm = new CommentService(podio);
            Survey s = new Survey();

            switch (check.App.Name)
            {
                case "Create Workshop": 
                    #region // create legacy workshops //

                    lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
                    try
                    {
                        if (string.IsNullOrEmpty(lockValue))
                        {
                            context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
                            return;
                        }
                        context.Logger.LogLine($"Lock Value: {lockValue}");
                        fieldId = ids.GetFieldId("Create Workshop|Workshop Type");
                        var checkType = check.Field<CategoryItemField>(fieldId);

                        WorkshopModules ws = new WorkshopModules();
                        await ws.CreateWorkShopModules(ids, check, podio, context, service, google, e, checkType);

                        // Create surveys //
                        await s.CreateSurveys(checkType, ids, podio, google, service, e, context);
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                    finally
                    {
                        await saasafrasClient.UnlockFunction(functionName, check.ItemId.ToString(), lockValue);
                    }
                    break;
                #endregion

                case "Admin": // everything else

                    var revision = await podio.GetRevisionDifference(Convert.ToInt32(check.ItemId), check.CurrentRevision.Revision - 1, check.CurrentRevision.Revision);
                    var firstRevision = revision.First();
                    context.Logger.LogLine($"Last Revision field: {firstRevision.Label}");

                    switch (firstRevision.Label)
                    {

                        case "WS Batch":
                            #region // Create Workshops //
                            var wsBatchId = ids.GetFieldId("Admin|WS Batch");
                            if (check.Field<CategoryItemField>(wsBatchId).Options.Any())
                            {
                                context.Logger.LogLine($"Running 'WS Batch {check.Field<CategoryItemField>(wsBatchId).Options.First().Text}'");
                                int nextBatch = -1;
                                lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());

                                try
                                {
                                    if (string.IsNullOrEmpty(lockValue))
                                    {
                                        context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
                                        return;
                                    }
                                    context.Logger.LogLine($"Lock Value: {lockValue}");
                                    
                                    WorkshopModules2 wm = new WorkshopModules2();
                                    nextBatch = await wm.CreateWorkshopModules2(context, podio, check, e, service, ids, google, pre);
                                    
                                    if (nextBatch > 1)
                                    {
                                        commentText = $"WS Batch {nextBatch-1} Completed.";
                                        check.Field<CategoryItemField>(ids.GetFieldId("Admin|WS Batch")).OptionText = $"{nextBatch}";
                                        await saasafrasClient.UnlockFunction(functionName, check.ItemId.ToString(), lockValue);
                                        await comm.AddCommentToObject("item", check.ItemId, commentText, hook: true);
                                        //await podio.UpdateItem(check, hook: true);
                                        return;
                                    } else if (nextBatch == -1)
                                    {
                                        commentText = $":loudspeaker: All WS Batches Completed!";
                                        await comm.AddCommentToObject("item", check.ItemId, commentText, hook: false);
                                    }
                                }

                                catch (Exception ex)
                                {
                                    context.Logger.LogLine($"Exception Details: {ex} - {ex.Data} - {ex.HelpLink} - {ex.HResult} - {ex.InnerException} " +
                                        $"- {ex.Message} - {ex.Source} - {ex.StackTrace} - {ex.TargetSite}");
                                    commentText = "Sorry, something went wrong. Please try again in 5 minutes or contact the administrator.";
                                    await comm.AddCommentToObject("item", check.ItemId, $":loudspeaker: {commentText}", hook: false);
                                    
                                }

                                finally {
                                    await saasafrasClient.UnlockFunction(functionName, check.ItemId.ToString(), lockValue);
                                }
                            }
                            break;
                        #endregion
                        case "Deploy Addons":
                            #region // Deploy Addon Modules //
                            var aoBatchId = ids.GetFieldId("Admin|Deploy Addons");
                            if (check.Field<CategoryItemField>(aoBatchId).Options.Any())
                            {
                                context.Logger.LogLine($"Running 'WS Batch {check.Field<CategoryItemField>(aoBatchId).Options.First().Text}'");
                                int nextBatch = -1;
                                lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());

                                try
                                {
                                    if (string.IsNullOrEmpty(lockValue))
                                    {
                                        context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
                                        return;
                                    }
                                    context.Logger.LogLine($"Lock Value: {lockValue}");

                                    Addons ao = new Addons();
                                    nextBatch = await ao.CreateAddons(context, podio, check, e, service, ids, google, pre);
                                    break;
                                }

                                catch (Exception ex)
                                {
                                    context.Logger.LogLine($"Exception Details: {ex} - {ex.Data} - {ex.HelpLink} - {ex.HResult} - {ex.InnerException} " +
                                        $"- {ex.Message} - {ex.Source} - {ex.StackTrace} - {ex.TargetSite}");
                                    commentText = "Sorry, something went wrong. Please try again in 5 minutes or contact the administrator.";
                                    await comm.AddCommentToObject("item", check.ItemId, $":loudspeaker: {commentText}", hook: false);
                                }

                                finally
                                {
                                    await saasafrasClient.UnlockFunction(functionName, check.ItemId.ToString(), lockValue);
                                }
                            }
                            break;
                        #endregion

                        //case "Deploy Task List":
                        //    var deploy = ids.GetFieldId("Admin|Deploy Task List");
                        //    if (check.Field<CategoryItemField>(deploy).Options.Any());
                        //    break;

                        case "TL Batch":
                            #region // Create Task List //
                            var tlBatchId = ids.GetFieldId("Admin|TL Batch");
                            if (check.Field<CategoryItemField>(tlBatchId).Options.Any())
                            {
                                context.Logger.LogLine($"Running 'TL Batch {check.Field<CategoryItemField>(tlBatchId).Options.First().Text}'");
                                int nextBatch = -1;
                                lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
                                try
                                {
                                    if (string.IsNullOrEmpty(lockValue))
                                    {
                                        context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
                                        return;
                                    }
                                    context.Logger.LogLine($"Lock Value: {lockValue}");

                                    TaskList2 tl = new TaskList2();
                                    nextBatch = await tl.CreateTaskLists(context, podio, check, e, service, ids, google, pre);

                                    if (nextBatch > 1)
                                    {
                                        commentText = $"TL Batch {nextBatch - 1} Completed.";
                                        check.Field<CategoryItemField>(ids.GetFieldId("Admin|TL Batch")).OptionText = $"{nextBatch}";
                                        await saasafrasClient.UnlockFunction(functionName, check.ItemId.ToString(), lockValue);
                                        await comm.AddCommentToObject("item", check.ItemId, commentText, hook: true);
                                        //await podio.UpdateItem(check, hook: true);
                                        return;
                                    }
                                    else if (nextBatch == -1)
                                    {
                                        commentText = $":loudspeaker: All TL Batches Completed!";
                                        await comm.AddCommentToObject("item", check.ItemId, commentText, hook: false);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    context.Logger.LogLine($"Exception Details: {ex} - {ex.Data} - {ex.HelpLink} - {ex.HResult} - {ex.InnerException} " +
                                        $"- {ex.Message} - {ex.Source} - {ex.StackTrace} - {ex.TargetSite}");
                                    commentText = "Sorry, something went wrong. Please try again in 5 minutes or contact the administrator.";
                                    await comm.AddCommentToObject("item", check.ItemId, $":loudspeaker: {commentText}", hook: false);
                                }
                                finally
                                {
                                    await saasafrasClient.UnlockFunction(functionName, check.ItemId.ToString(), lockValue);
                                }
                            }
                            break;
                        #endregion

                        case "Hidden Status":
                            #region // Create Legacy task lists //
                            var TlStatusId = ids.GetFieldId("Admin|Hidden Status");
                            context.Logger.LogLine($"Value checking for: '(Legacy) Task List {check.Field<CategoryItemField>(TlStatusId).Options.FirstOrDefault().Text}'");
                            if (check.Field<CategoryItemField>(TlStatusId).Options.Any())
                            {
                                lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
                                try
                                {
                                    if (string.IsNullOrEmpty(lockValue))
                                    {
                                        context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
                                        return;
                                    }
                                    context.Logger.LogLine($"Lock Value: {lockValue}");
                                    context.Logger.LogLine("Satisfied conditions, TaskList");
                                    TaskList tl = new TaskList();
                                    await tl.CreateTaskLists(context, podio, check, e, service, ids, google, pre);
                                }
                                catch (Exception ex)
                                {
                                    context.Logger.LogLine($"Exception Details: {ex} - {ex.Data} - {ex.HelpLink} - {ex.HResult} - {ex.InnerException} " +
                                        $"- {ex.Message} - {ex.Source} - {ex.StackTrace} - {ex.TargetSite}");
                                }
                                finally
                                {
                                    await saasafrasClient.UnlockFunction(functionName, check.ItemId.ToString(), lockValue);
                                }
                            }
                            break;
                        #endregion

                        default:
                            context.Logger.LogLine($"NO ACTION: Value '{firstRevision.Label}' not Recognized.");
                            break;
                    }
                    break;
            }
        }
    }
}