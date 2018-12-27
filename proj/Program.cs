
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

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]


namespace newVilcapCopyFileToGoogleDrive
{
    public class RoutedPodioEvent
    {
        public PodioEvent podioEvent { get; set; }
        public string clientId { get; set; }
        public string version { get; set; }
        public string solutionId { get; set; }
        public string environmentId { get; set; }
    };

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
		public int GetFieldId(string key)
		{
			var parts = key.Split('|');
			if (parts.Count() < 3)
			{
				return Convert.ToInt32(dictChild[$"{fullNames[ev.environmentId]}|{key}"]);
			}
			else 
				return Convert.ToInt32(dictMaster[key]);

		}
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
			ViewService viewServ;
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
				{"toolkittemplate" ,"VC Toolkit Template"}

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
			switch (check.App.Name)
			{
				case "Admin":
					
					var TlStatusId = GetFieldId("Admin|Task List Status");
					var startDateId = GetFieldId("Admin|Program Start Date");
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
								viewServ = new ViewService(podio);
								context.Logger.LogLine("Got View Service");
								var views = await viewServ.GetViews(21310276);//VC Admin Master Schedule App
								var view = from v in views
										   where v.Name == "Package"
										   select v;
								context.Logger.LogLine("Got View");
								op = new FilterOptions();
								op.Filters = view.First().Filters;
								if(check.Field<CategoryItemField>(TlStatusId).Options.First().Text == "1")
								{
									context.Logger.LogLine("Grabbing items 1-42");
									op.Offset = 0;
									op.Limit = 35;
									filter = await podio.FilterItems(21310276, op);
									commentText = "Batch 1 finished";
								}
								else if(check.Field<CategoryItemField>(TlStatusId).Options.First().Text == "2")
								{
									context.Logger.LogLine("Grabbing items 43-84");
									op.Offset = 35;
									op.Limit = 35;
									filter = await podio.FilterItems(21310276, op);
									commentText = "Batch 2 finished";
								}
								else if(check.Field<CategoryItemField>(TlStatusId).Options.First().Text == "3")
								{
									context.Logger.LogLine("Grabbing items 85-126");
									op.Offset = 70;
									op.Limit = 35;
									filter = await podio.FilterItems(21310276, op);
									commentText = "Batch 3 finished";
								}
								else if(check.Field<CategoryItemField>(TlStatusId).Options.First().Text == "4")
								{
									context.Logger.LogLine("Grabbing items 127-168 with links");
									op.Offset = 105;
									op.Limit = 35;
									filter = await podio.FilterItems(21310276, op);
									commentText = "Batch 4 finished";
								}
								else if(check.Field<CategoryItemField>(TlStatusId).Options.First().Text == "5")
								{
									context.Logger.LogLine("Grabbing items 169-all with links");
									op.Offset = 140;
									op.Limit = 35;
									filter = await podio.FilterItems(21310276, op);
									commentText = "Batch 5 finished";
								}
								else
								{
									context.Logger.LogLine("Grabbing items 169-all with links");
									op.Offset = 175;
									op.Limit = 35;
									filter = await podio.FilterItems(21310276, op);
									commentText = "Batch 6 finished";
								}
								
								context.Logger.LogLine($"Items in filter:{filter.Items.Count()}");
								int count = 0;
								foreach (var masterItem in filter.Items)
								{
									count += 1;
									context.Logger.LogLine($"On item #: {count}");
									Item child = new Item();

                                    //--- Assign Fields ---//	
                                    fieldId = GetFieldId("VC Administration|Master Schedule|Task Name");
                                    var nameMaster = masterItem.Field<TextItemField>(fieldId);
                                    if (nameMaster.Value != null)
                                    {
                                        fieldId = GetFieldId("Task List|Title");
                                        var nameChild = child.Field<TextItemField>(fieldId);
                                        nameChild.Value = nameMaster.Value;
                                    }
                                    context.Logger.LogLine($"Added field:{nameMaster.Label}");
                                    fieldId = GetFieldId("VC Administration|Master Schedule|Desciption");
                                    var descrMaster = masterItem.Field<TextItemField>(fieldId);
                                    if (descrMaster.Value != null)
                                    {
                                        fieldId = GetFieldId("Task List|Description");
                                        var descrChild = child.Field<TextItemField>(fieldId);
                                        descrChild.Value = StripHTML(descrMaster.Value);
                                    }
                                    context.Logger.LogLine($"Added field:{descrMaster.Label}");
                                    fieldId = GetFieldId("VC Administration|Master Schedule|Phase");
                                    var phaseMaster = masterItem.Field<CategoryItemField>(fieldId);
                                    if (phaseMaster.Options.Any())
                                    {
                                        fieldId = GetFieldId("Task List|Phase");
                                        var phaseChild = child.Field<CategoryItemField>(fieldId);
                                        phaseChild.OptionText = phaseMaster.Options.First().Text;
                                    }
                                    context.Logger.LogLine($"Added field:{phaseMaster.Label}");
                                    fieldId = GetFieldId("VC Administration|Master Schedule|ESO Member Role");
                                    var esoMaster = masterItem.Field<CategoryItemField>(fieldId);
                                    if (esoMaster.Options.Any())
                                    {
                                        fieldId = GetFieldId("Task List|ESO Member Role");
                                        var esoChild = child.Field<CategoryItemField>(fieldId);
                                        esoChild.OptionText = esoMaster.Options.First().Text;
                                    }
                                    context.Logger.LogLine($"Added field:{esoMaster.Label}");
                                    fieldId = GetFieldId("VC Administration|Master Schedule|Project");
                                    var projectMaster = masterItem.Field<CategoryItemField>(fieldId);
                                    if (projectMaster.Options.Any())
                                    {
                                        fieldId = GetFieldId("Task List|Project");
                                        var projectChild = child.Field<CategoryItemField>(fieldId);
                                        projectChild.OptionText = projectMaster.Options.First().Text;
                                    }
                                    context.Logger.LogLine($"Added field:{projectMaster.Label}");

                                    fieldId = GetFieldId("VC Administration|Master Schedule|Base Workshop Association");
                                    var wsMaster = masterItem.Field<CategoryItemField>(fieldId);
                                    if (wsMaster.Options.Any())
                                    {
                                        fieldId = GetFieldId("Task List|WS Association");
                                        var wsChild = child.Field<TextItemField>(fieldId);
                                        wsChild.Value = wsMaster.Options.First().Text;
                                        fieldId = GetFieldId("Task List|Parent WS");
                                        var parentChild = child.Field<CategoryItemField>(fieldId);
                                        parentChild.OptionText = wsMaster.Options.First().Text;
                                    }
                                    context.Logger.LogLine($"Added field:{wsMaster.Label}");
                                    fieldId = GetFieldId("VC Administration|Master Schedule|Weeks Off-Set");
                                    var offsetMaster = masterItem.Field<NumericItemField>(fieldId);
                                    if (offsetMaster.Value.HasValue)
                                    {
                                        fieldId = GetFieldId("Task List|Week Offset");
                                        var offsetChild = child.Field<NumericItemField>(fieldId);
                                        offsetChild.Value = offsetMaster.Value;
                                        fieldId = GetFieldId("Task List|Weeks Before WS");
                                        var weeksChild = child.Field<NumericItemField>(fieldId);
                                        weeksChild.Value = offsetMaster.Value;
                                    }
                                    context.Logger.LogLine($"Added field:{offsetMaster.Label}");
                                    fieldId = GetFieldId("Task List|Completetion");
                                    var comChild = child.Field<CategoryItemField>(fieldId);
                                    comChild.OptionText = "Incomplete";
                                    context.Logger.LogLine($"Added field: Completion");

									fieldId = GetFieldId("VC Administration|Master Schedule|Duration (Days)");
									var durMaster = masterItem.Field<NumericItemField>(fieldId);
									if (durMaster.Value.HasValue)
									{
										fieldId = GetFieldId("Task List|Duration (days)");
										var durChild = child.Field<NumericItemField>(fieldId);
										durChild.Value = durMaster.Value;
									}
									context.Logger.LogLine($"Added field:{durMaster.Label}");
									fieldId = GetFieldId("VC Administration|Master Schedule|Dependancy");
									var depMaster = masterItem.Field<TextItemField>(fieldId);
									if (depMaster.Value != null)
									{
										fieldId = GetFieldId("Task List|Additional Dependencies");
										var depChild = child.Field<TextItemField>(fieldId);
										depChild.Value = depMaster.Value;
									}
									context.Logger.LogLine($"Added field:{depMaster.Label}");
									fieldId = GetFieldId("VC Administration|Master Schedule|Gdrive Link");
									var embedMaster = masterItem.Field<EmbedItemField>(fieldId);
									fieldId = GetFieldId("Task List|Linked Files");
									var embedChild = child.Field<EmbedItemField>(fieldId);
									List<Embed> embeds = new List<Embed>();
									string parentFolderId = System.Environment.GetEnvironmentVariable("GOOGLE_PARENT_FOLDER_ID");
									var cloneFolderId = GetSubfolderId(service, podio, e, parentFolderId);//TODO:
									foreach (var em in embedMaster.Embeds)
									{
										if (em.OriginalUrl.Contains(".google."))
										{
											await UpdateOneEmbed(service, em, embeds, cloneFolderId, podio, e);
										}
                                        //else          // Hold for 2.0 //
                                        //{
                                        //    await NonGDriveCopy(em, embeds, podio, e);
                                        //}
                                    }
                                    foreach (var embed in embeds)
									{
										embedChild.AddEmbed(embed.EmbedId);
									}
									context.Logger.LogLine($"Added field:{embedMaster.Label}");
									var taskListAppId = GetFieldId("Task List");

									await podio.CreateItem(child, taskListAppId, true);//child task list appId
									context.Logger.LogLine($"Created item #{count}");
								}
								CommentService comm = new CommentService(podio);
								await comm.AddCommentToObject("item", check.ItemId, commentText,hook:true);
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
						else if (firstRevision.FieldId == startDateId)
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
								dynamic previous = firstRevision.From; // Prevents the function from running every time the date is changed 
								context.Logger.LogLine("Attempting to log previous value");
								context.Logger.LogLine(previous.value.status);
								if (previous.value.status == null)
								{
									//--- Create Program Budget Template (Expendatures) ---//
									viewServ = new ViewService(podio);
									context.Logger.LogLine("Got View Service");
									var views = await viewServ.GetViews(21481130); //VC Admin Master Schedule App
								    var view = from v in views
											   where v.Name == "Workshop Associations"
											   select v;
									context.Logger.LogLine("Got View");
									op = new FilterOptions();
									op.Filters = view.First().Filters;
									op.Limit = 500;
									filter = await podio.FilterItems(21481130, op);
									foreach (var master in filter.Items)
									{
										Item child = new Item();

										fieldId = GetFieldId("VC Administration|Expenditures Curation|Purpose");
										var purposeMaster = master.Field<TextItemField>(fieldId);
										if (purposeMaster.Value != null)
										{
											fieldId = GetFieldId("Expenditures|Purpose");
											var purposeChild = child.Field<TextItemField>(fieldId);
											purposeChild.Value = purposeMaster.Value;
										}

										fieldId = GetFieldId("VC Administration|Expenditures Curation|Workshop Associations");
										var waMaster = master.Field<CategoryItemField>(fieldId);
										if (waMaster.Options.Any())
										{
											fieldId = GetFieldId("Expenditures|Workshop Associations");
											var waChild = child.Field<CategoryItemField>(fieldId);
											waChild.OptionText = waMaster.Options.First().Text;
										}

										fieldId = GetFieldId("VC Administration|Expenditures Curation|Expense Type");
										var expMaster = master.Field<CategoryItemField>(fieldId);
										if (expMaster.Options.Any())
										{
											fieldId = GetFieldId("Expenditures|Task List");
											var expChild = child.Field<CategoryItemField>(fieldId);
											expChild.OptionText = expMaster.Options.First().Text;
										}

										fieldId = GetFieldId("VC Administration|Expenditures Curation|Amount");
										var amountMaster = master.Field<MoneyItemField>(fieldId);
										if (amountMaster.Value.HasValue)
										{
											fieldId = GetFieldId("Expenditures|Amount");
											var amountChild = child.Field<MoneyItemField>(fieldId);
											amountChild.Value = amountMaster.Value;
										}
										fieldId = GetFieldId("Admin|Program Manager");
										var managerMaster = check.Field<ContactItemField>(fieldId);
										if (managerMaster.Contacts.Any())
										{
											fieldId = GetFieldId("Expenditures|Spender");
											var managerChild = child.Field<ContactItemField>(fieldId);
											List<int> cs = new List<int>();
											foreach (var contact in managerMaster.Contacts)
											{
												cs.Add(contact.ProfileId);
												managerChild.ContactIds = cs;
											}
										}
										fieldId = GetFieldId("Expenditures|Status");
										var status = child.Field<CategoryItemField>(fieldId);
										status.OptionText = "Template";

										await podio.CreateItem(child, GetFieldId($"Expenditures"), false);
									}

									//--- Create Pre-Workshop Surveys ---//
									viewServ = new ViewService(podio);
									context.Logger.LogLine("Got View Service");
									views = await viewServ.GetViews(21389770); //VC Admin Master Schedule App
									view = from v in views
											   where v.Name == "PreWS"
											   select v;
									context.Logger.LogLine("Got View");
									op = new FilterOptions();
									op.Filters = view.First().Filters;
									op.Limit = 500;
									filter = await podio.FilterItems(21389770, op);

									foreach (var master in filter.Items)
									{
										Item child = new Item();
										fieldId = GetFieldId("VC Administration|Survey|Title");
										var titleMaster = master.Field<TextItemField>(fieldId);
										if (titleMaster.Value != null)
										{
											fieldId = GetFieldId("Survey|Title");
											var titleChild = child.Field<TextItemField>(fieldId);
											titleChild.Value = titleMaster.Value;
										}

										fieldId = GetFieldId("VC Administration|Survey|Notes");
										var notesMaster = master.Field<TextItemField>(fieldId);
										if (notesMaster.Value != null)
										{
											fieldId = GetFieldId("Survey|Notes");
											var notesChild = child.Field<TextItemField>(fieldId);
											notesChild.Value = StripHTML(notesMaster.Value);
										}

										fieldId = GetFieldId("VC Administration|Survey|Related Workshop");
										var relMaster = master.Field<CategoryItemField>(fieldId);
										if (relMaster.Options.Any())
										{
											fieldId = GetFieldId("Survey|Related Workshop");
											var relChild = child.Field<CategoryItemField>(fieldId);
											relChild.OptionText = relMaster.Options.First().Text;
										}

										fieldId = GetFieldId("VC Administration|Survey|Gdrive Survey");
										var embedMaster = master.Field<EmbedItemField>(fieldId);
										fieldId = GetFieldId("Surveys|Link to Survey");
										var embedChild = child.Field<EmbedItemField>(fieldId);
										List<Embed> embeds = new List<Embed>();
										string parentFolderId = System.Environment.GetEnvironmentVariable("GOOGLE_PARENT_FOLDER_ID");
										var cloneFolderId = GetSubfolderId(service, podio, e, parentFolderId);
										foreach (var em in embedMaster.Embeds)
										{
											if (em.OriginalUrl.Contains(".google."))
											{
												await UpdateOneEmbed(service, em, embeds, cloneFolderId, podio, e);
											}
                                            //else          // Hold for 2.0 //
                                            //{
                                            //    await NonGDriveCopy(em, embeds, podio, e);
                                            //}
                                        }
                                        foreach (var embed in embeds)
										{
											embedChild.AddEmbed(embed.EmbedId);
										}
										await podio.CreateItem(child, GetFieldId("Survey"), false);
									}
								}
							}
							catch(Exception ex)
							{
								throw ex;
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
						fieldId = GetFieldId("Create Workshop|Workshop Type");
						var checkType = check.Field<CategoryItemField>(fieldId);
						op = new FilterOptions();
						op.Limit = 500;
						int textFieldId = GetFieldId("VC Administration|Content Curation |Workshop Type (Text)");

						var filterConditions = new Dictionary<string, string>
					    {
						    {textFieldId.ToString(), checkType.Options.First().Text} 
					    };
						op.Filters = filterConditions;
						filter = await podio.FilterItems(21310273, op);

                        var baseDT = check.Field<DateItemField>(GetFieldId("Create Workshop|Date")).Start;
                        int childDTF = GetFieldId("Workshop Modules|Date");
                        int offsetF = GetFieldId("Workshop Modules|Minute Offset");
                        int durationF = GetFieldId("VC Administration|Content Curation |Duration");

                        foreach (var master in filter.Items)
						{
							Item child = new Item();
							fieldId = GetFieldId("Workshop Modules|Workshop");
							var ws = child.Field<AppItemField>(fieldId);
							ws.ItemId = check.ItemId;//TODO

							fieldId = GetFieldId("VC Administration|Content Curation |Workshop Detail Title");
							var titleMaster = master.Field<TextItemField>(fieldId);
							if (titleMaster.Value != null)
							{
								fieldId = GetFieldId("Workshop Modules|Title");
								var titleChild = child.Field<TextItemField>(fieldId);
								titleChild.Value = titleMaster.Value;
							}

							fieldId = GetFieldId("VC Administration|Content Curation |Purpose");
							var descMaster = master.Field<TextItemField>(fieldId);
							if (descMaster.Value != null)
							{
								fieldId = GetFieldId("Workshop Modules|Description");
								var descChild = child.Field<TextItemField>(fieldId);
								descChild.Value = StripHTML(descMaster.Value);
							}

							fieldId = GetFieldId("VC Administration|Content Curation |Duration");
							var durMaster = master.Field<DurationItemField>(fieldId);
							context.Logger.LogLine($"Master Duration: {durMaster.Value.Value}");
							if (durMaster.Value!=null)
							{
								context.Logger.LogLine("Status was not null");
								fieldId = GetFieldId("Workshop Modules|Duration");
								var durChild = child.Field<DurationItemField>(fieldId);
                                durChild.Value = durMaster.Value.Value.Duration(); // durChild.Value.Value.Add(durMaster.Value.Value);? durChild.Value = durMaster.Value;?
                                context.Logger.LogLine($"Child Duration: {durChild.Value.Value}");
							}
							var offsetMaster = master.Field<NumericItemField>(GetFieldId("VC Administration|Content Curation |Minute Offset"));
							if(offsetMaster.Value!=null)
							{
								fieldId = GetFieldId("Workshop Modules|Minute Offset");
								var offsetChild = child.Field<NumericItemField>(fieldId);
								offsetChild.Value = offsetMaster.Value;
							}
							context.Logger.LogLine("Checking Date information");
                            double minutes =Convert.ToDouble(child.Field<NumericItemField>(offsetF).Value);
							context.Logger.LogLine($"Minutes: {minutes}");
                            child.Field<DateItemField>(childDTF).Start = baseDT.Value.AddMinutes(minutes);
							context.Logger.LogLine($"Child Start Date: {child.Field<DateItemField>(childDTF).Start}");
							minutes = master.Field<DurationItemField>(durationF).Value.Value.TotalMinutes;
							context.Logger.LogLine($"New minutes: {minutes}");
							child.Field<DateItemField>(childDTF).End = child.Field<DateItemField>(childDTF).Start.Value.AddMinutes(minutes);
							context.Logger.LogLine($"Child date end: {child.Field<DateItemField>(childDTF).End}");
							
						    fieldId = GetFieldId("VC Administration|Content Curation |Entrepreneur Pre-Work Required");
							var workMaster = master.Field<TextItemField>(fieldId);
							if (workMaster.Value != null)
							{
								fieldId = GetFieldId("Workshop Modules|Entrepreneur Pre-work Required");
								var workChild = child.Field<TextItemField>(fieldId);
								workChild.Value = workMaster.Value;
							}

							fieldId = GetFieldId("VC Administration|Content Curation |Materials Required");
							var matsMaster = master.Field<TextItemField>(fieldId);
							if (matsMaster.Value != null)
							{
								fieldId = GetFieldId("Workshop Modules|Additional Materials Required");
								var matsChild = child.Field<TextItemField>(fieldId);
								matsChild.Value = matsMaster.Value;
							}

							fieldId = GetFieldId("VC Administration|Content Curation |Mentors Required");
							var mentMaster = master.Field<TextItemField>(fieldId);
							if (mentMaster.Value != null)
							{
								fieldId = GetFieldId("Workshop Modules|Mentors Required");
								var mentChild = child.Field<TextItemField>(fieldId);
								mentChild.Value = mentMaster.Value;
							}

							fieldId = GetFieldId("VC Administration|Content Curation |GDrive File Name");
							var embedMaster = master.Field<EmbedItemField>(fieldId);
							fieldId = GetFieldId("Workshop Modules|Link to Material");
							var embedChild = child.Field<EmbedItemField>(fieldId);
							List<Embed> embeds = new List<Embed>();
							string parentFolderId = System.Environment.GetEnvironmentVariable("GOOGLE_PARENT_FOLDER_ID");
							var cloneFolderId = GetSubfolderId(service, podio, e, parentFolderId);
							foreach (var em in embedMaster.Embeds)
							{
								if (em.OriginalUrl.Contains(".google."))
								{
									await UpdateOneEmbed(service, em, embeds, cloneFolderId, podio, e);
								}
							}
							foreach (var embed in embeds)
							{
								embedChild.AddEmbed(embed.EmbedId);
							}
							//TODO: Add embed fields 
							await podio.CreateItem(child, GetFieldId("Workshop Modules"), true);
						}

						// Create surveys //
						var parts = checkType.Options.First().Text.Split('/');
						if (parts[1].Trim() == "Day 1")
						{
							string filterValue = parts[0].Trim();
							op = new FilterOptions();
							op.Limit = 500;

							filterConditions = new Dictionary<string, string>
							{
								{GetFieldId("VC Administration|Survey|[WS]").ToString(), filterValue }
							};
							op.Filters = filterConditions;
							
							filter = await podio.FilterItems(21389770, op);

							foreach (var master in filter.Items)
							{

								Item child = new Item();
								fieldId = GetFieldId("VC Administration|Survey|Title");
								var titleMaster = master.Field<TextItemField>(fieldId);
								if (titleMaster.Value != null)
								{
									fieldId = GetFieldId("Surveys|Title");
									var titleChild = child.Field<TextItemField>(fieldId);
									titleChild.Value = titleMaster.Value;
								}

								fieldId = GetFieldId("VC Administration|Survey|Notes");
								var notesMaster = master.Field<TextItemField>(fieldId);
								if (notesMaster.Value != null)
								{
									fieldId = GetFieldId("Surveys|Notes");
									var notesChild = child.Field<TextItemField>(fieldId);
									notesChild.Value = StripHTML(notesMaster.Value);
								}

								fieldId = GetFieldId("VC Administration|Survey|Related Workshop");
								var relMaster = master.Field<CategoryItemField>(fieldId);
								if (relMaster.Options.Any())
								{
									fieldId = GetFieldId("Surveys|Related Workshop");
									var relChild = child.Field<CategoryItemField>(fieldId);
									relChild.OptionText = relMaster.Options.First().Text;
								}

								fieldId = GetFieldId("VC Administration|Survey|Gdrive Survey");
								var embedMaster = master.Field<EmbedItemField>(fieldId);
								fieldId = GetFieldId("Surveys|Link to Survey");
								var embedChild = child.Field<EmbedItemField>(fieldId);
								var embeds = new List<Embed>();
								var parentFolderId = System.Environment.GetEnvironmentVariable("GOOGLE_PARENT_FOLDER_ID");
								var cloneFolderId = GetSubfolderId(service, podio, e, parentFolderId);
								foreach (var em in embedMaster.Embeds)
								{
									if (em.OriginalUrl.Contains(".google."))
									{
										await UpdateOneEmbed(service, em, embeds, cloneFolderId, podio, e);
									}
								}
								foreach (var embed in embeds)
								{
									embedChild.AddEmbed(embed.EmbedId);
								}
								//embed fields
								await podio.CreateItem(child, GetFieldId("Surveys"), false);
							}
						}
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


        private static string GetSubfolderId(DriveService ds, Podio podio, RoutedPodioEvent e, string parentFolder)
        {
            try
            {
                Console.WriteLine($"{e.podioEvent.item_id} - EnvID: {e.environmentId}");
                FilesResource.ListRequest listReq = ds.Files.List();
                listReq.Q = "name='" + e.environmentId + "'";
                string folderId = "";

                if (listReq.Execute().Files.Any())
                {
                    folderId = listReq.Execute().Files[0].Id;
                }
                else if (folderId == "")
                {
                    File folder = new File
                    {
                        Name = e.environmentId,
                        MimeType = "application/vnd.google-apps.folder",
                    };
                    folder.Parents.Add(parentFolder);
                    var request = ds.Files.Create(folder);
                    request.Fields = "id";

                    folderId = request.Execute().Id;
                }
                return folderId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{e.podioEvent.item_id} - {ex.Message} - {ex.StackTrace} - {ex.InnerException}");
				return null;
            }
        }

        public static async Task UpdateOneEmbed(DriveService ds, Embed embed, List<Embed> embeds, string subfolderId, Podio podio, RoutedPodioEvent e)
        {
            try
            {
                Console.WriteLine($"{e.podioEvent.item_id} - Old Embed Link (resolved): {embed.ResolvedUrl}");
                Console.WriteLine($"{e.podioEvent.item_id} - Old Embed Link (original): {embed.OriginalUrl}");
                var id = GetDriveId(embed.OriginalUrl, e);
                Console.WriteLine($"{e.podioEvent.item_id} - ID that we pull from the URL: {id}");
                File original = GetFileByTitle(ds, id, e);
                if (original.Parents == null)
                    original.Parents = new List<string>();
                Console.WriteLine($"{e.podioEvent.item_id} - ID from the file itself: {original.Id}, Name: {original.Name}");
                original.Parents.Clear();
                original.Parents.Add(subfolderId);
                original.Name = e.environmentId + " " + original.Name;

                File clone = ds.Files.Copy(original, id).Execute();

                await Task.Run(() =>
                {
                    Permission p = new Permission
                    {
                        Role = "writer",
                        Type = "anyone"
                    };
                    new PermissionsResource.CreateRequest(ds, p, clone.Id).Execute();
                });

                await Task.Run(() =>
                {
                    PodioCore.Services.EmbedService embedServ = new EmbedService(podio);
                    Console.WriteLine($"{e.podioEvent.item_id} - Adding embed thru service");

                    Console.WriteLine($"{e.podioEvent.item_id} - CloneID: {clone.Id}");
                    var req = ds.Files.Get(clone.Id);
                    req.Fields = "webViewLink";
                    clone = req.Execute();
					//runs 130x approx
                    Embed em = embedServ.AddAnEmbed(clone.WebViewLink).Result;
                    Console.WriteLine($"{e.podioEvent.item_id} - New Embed Link (resolved): {em.ResolvedUrl}");
                    Console.WriteLine($"{e.podioEvent.item_id} - New Embed Link (original): {em.OriginalUrl}");
                    Console.WriteLine($"{e.podioEvent.item_id} - New Embed added");
                    Console.WriteLine($"{e.podioEvent.item_id} - WebViewLink: {clone.WebViewLink}");
                    embeds.Add(em);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{e.podioEvent.item_id} - {ex.Message} - {ex.StackTrace} - {ex.InnerException}");
            }
        }

        public static string GetDriveId(string url,RoutedPodioEvent e)
        {
            try
            {
                Console.WriteLine($"{e.podioEvent.item_id} - Attempting to get the ID from URL: {url}");
                string[] substr = url.Split(new char[] { '=', '/','?' });
                foreach (string s in substr)
                {
                    if (s.Length == 44||s.Length==33)
                    {
                        Console.WriteLine($"{e.podioEvent.item_id} - Found ID: {s} from url: {url}");
                        return s;
                    }
                }
                Console.WriteLine($"{e.podioEvent.item_id} - Could not find ID for url: {url}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{e.podioEvent.item_id} - {ex.Message} - {ex.StackTrace} - {ex.InnerException}");
				return null;
            }
        }

        public static File GetFileByTitle(DriveService ds, string id,RoutedPodioEvent e)
        {
            try
            {
                var request = ds.Files.Get(id);
                request.Fields = "parents, name";
                var file = request.Execute();
                return file;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{e.podioEvent.item_id} - {ex.Message} - {ex.StackTrace} - {ex.InnerException}");
				return null;
            }
        }

        //public static async Task NonGDriveCopy(Embed embed, List<Embed> embeds, Podio podio, RoutedPodioEvent e)  // Hold for 2.0 //
        //{
        //    try
        //    {
        //        Console.WriteLine($"{e.podioEvent.item_id} - Direct URL Embed Link (resolved): {embed.ResolvedUrl}");
        //        Console.WriteLine($"{e.podioEvent.item_id} - Direct URL Embed Link (original): {embed.OriginalUrl}");
        //        await Task.Run(() =>
        //        {
        //            embeds.Add(embed);
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"{e.podioEvent.item_id} - {ex.Message} - {ex.StackTrace} - {ex.InnerException}");
        //        throw ex;
        //    }
        //}

    }
}