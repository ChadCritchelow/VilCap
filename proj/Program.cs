
using System;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using System.Threading.Tasks;
using PodioCore;
using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Core;
using BrickBridge.Models;
using PodioCore.Utils.ItemFields;
using PodioCore.Items;
using BrickBridge;
using PodioCore.Models;
using BrickBridge.Lambda.VilCap;
using Task = System.Threading.Tasks.Task;
using File = Google.Apis.Drive.v3.Data.File;
using Permission = Google.Apis.Drive.v3.Data.Permission;
using PodioCore.Services;
using PodioCore.Models.Request;
using System.Text.RegularExpressions;
using System.Collections;
using PodioCore.Utils;
using PodioCore.Comments;
using PodioCore.Exceptions;

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
			BbcServiceClient bbc = new BbcServiceClient(System.Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), System.Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
			context.Logger.LogLine("Getting BBC Client Instance");
			dictChild = await bbc.GetDictionary(e.clientId, e.environmentId, e.solutionId, e.version);
			dictMaster = await bbc.GetDictionary("vcadministration", "vcadministration", "vilcap", "0.0");
			context.Logger.LogLine("Got dictionary");
			FilterOptions op;
			PodioCollection<Item> filter;
			var functionName = "newVilcapCopyFileToGoogleDrive";
			fullNames = new Dictionary<string, string>()
			{
				{"andela" ,"Andela"},
				{"anza" ,"Anza"},
				{"bluemoon" ,"blueMoon"},
				{"energygeneration" ,"Energy Generation"},
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
                {"usfintech2019" ,"US Fintech 2019" },
                {"wepower" ,"WePower" }
            };

			string serviceAcccount = System.Environment.GetEnvironmentVariable("GOOGLE_SERVICE_ACCOUNT");
			var cred = GoogleCredential.FromJson(serviceAcccount).CreateScoped(Scopes).UnderlyingCredential;
			// Create Drive API service.
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
			WorkshopModules ws = new WorkshopModules();
			Survey s = new Survey();

			switch (check.App.Name)
			{
				case "Admin":

					var TlStatusId = ids.GetFieldId("Admin|Hidden Status");
					var startDateId = ids.GetFieldId("Admin|Program Start Date");
					context.Logger.LogLine($"Value checking for: {check.Field<CategoryItemField>(TlStatusId).Options.First().Text}");
					if (check.Field<CategoryItemField>(TlStatusId).Options.Any())
					{

						var revision = await podio.GetRevisionDifference(Convert.ToInt32(check.ItemId), check.CurrentRevision.Revision - 1, check.CurrentRevision.Revision);
						var firstRevision = revision.First();
						context.Logger.LogLine($"Last Revision field: {firstRevision.Label}");
						if (firstRevision.FieldId == TlStatusId)
						{

							lockValue = await bbc.LockFunction(functionName, check.ItemId.ToString());

							try
							{
								if (string.IsNullOrEmpty(lockValue))
								{
									context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
									return;
								}
								context.Logger.LogLine($"Lock Value: {lockValue}");
								context.Logger.LogLine("Satisfied conditions, Task List Function");
								TaskList tl = new TaskList();
								await tl.CreateTaskLists(context,podio,check,e,service,ids,google,pre);
							}
							catch (Exception ex)
							{
								context.Logger.LogLine($"Exception Details: {ex} - {ex.Data} - {ex.HelpLink} - {ex.HResult} - {ex.InnerException} " +
									$"- {ex.Message} - {ex.Source} - {ex.StackTrace} - {ex.TargetSite}");
							}
							finally
							{
								await bbc.UnlockFunction(functionName, check.ItemId.ToString(), lockValue);
							}
						}
					}
					break;
			
				case "Create Workshop":
					//create workshops
					lockValue = await bbc.LockFunction(functionName, check.ItemId.ToString());

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

						
						await ws.CreateWorkShopModules(ids, check, podio, context, service, google, e,checkType);
						// Create surveys //
						await s.CreateSurveys(checkType, ids, podio, google, service, e, context);
					}
					catch(Exception ex)
					{
						throw ex;
					}
					finally
					{
						await bbc.UnlockFunction(functionName, check.ItemId.ToString(), lockValue);
					}
					break;
			}
			
	    }
    }
}