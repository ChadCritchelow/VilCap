
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

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]


namespace vilcapCopyFileToGoogleDrive
{
    public class RoutedPodioEvent
    {
        public PodioEvent podioEvent { get; set; }
        public string clientId { get; set; }
        public string version { get; set; }
        public string solutionId { get; set; }
        public string environmentId { get; set; }
    };

    public class CopyFileToGoogleDrive
    {

        static string[] Scopes = { DriveService.Scope.Drive };
        static string ApplicationName = "BrickBridgeVilCap";
        static LambdaMemoryStore memoryStore = new LambdaMemoryStore();
        string cId;
        string eId;
        string sId;
        string v;
        string baseUrl;
        string apiKey;
		int fieldId = 0;
		Dictionary<string, string> dict;
		Dictionary<string, string> fullNames;
		RoutedPodioEvent ev;
		public int GetFieldId(string key)
		{
			if (key.Split('|').Count() == 2)
			{
				return Convert.ToInt32(dict[$"{fullNames[ev.environmentId]}|{key}"]);
			}
			else return Convert.ToInt32(dict[key]);
		}
		public static string StripHTML(string input)
		{
			return Regex.Replace(input, "<.*?>", String.Empty);
		}
		public async Task Attempt1(RoutedPodioEvent e, ILambdaContext context)
		{
			ev = e;
			var factory = new AuditedPodioClientFactory(e.solutionId, e.version, e.clientId, e.environmentId);
			var podio = factory.ForClient(e.clientId, e.environmentId);

			Item check = await podio.GetItem(Convert.ToInt32(e.podioEvent.item_id));
			BbcServiceClient bbc = new BbcServiceClient(System.Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), System.Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
			dict = await bbc.GetDictionary(e.clientId, e.environmentId, e.solutionId, e.version);
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

					//TODO: Address date calc
		    switch (check.App.Name)
			{
				case "Admin":
					fieldId = GetFieldId("Admin|Task List Status");
					if (check.Field<CategoryItemField>(fieldId).Options.Any() && check.Field<CategoryItemField>(fieldId).Options.First().Text == "New")
					{
						var revision = await podio.GetRevisionDifference(Convert.ToInt32(check.ItemId), check.CurrentRevision.Revision - 1, check.CurrentRevision.Revision);
						var firstRevision = revision.First();
						if (firstRevision.FieldId == fieldId)
						{
							ViewService viewServ = new ViewService(podio);
							var view = await viewServ.GetView(21310276, "Package");
							FilterOptions op = new FilterOptions();
							op.Filters = view.Filters;
							op.Limit = 500;
							var filter = await podio.FilterItems(21310276, op);

							foreach (var masterItem in filter.Items)
							{
								Item child = new Item();
								//assign fields								
								fieldId = GetFieldId("VC Administration|Master Schedule|Task Name");
								var nameMaster = masterItem.Field<TextItemField>(fieldId);								
								if (nameMaster.Value !=null)
								{
									fieldId = GetFieldId("Task List|Title");
									var nameChild = child.Field<TextItemField>(fieldId);
									nameChild.Value = nameMaster.Value;
								}

								fieldId = GetFieldId("VC Administration|Master Schedule|Description");
								var descrMaster = masterItem.Field<TextItemField>(fieldId);
								if (descrMaster.Value != null)
								{
									fieldId = GetFieldId("Task List|Description");
									var descrChild = child.Field<TextItemField>(fieldId);
									descrChild.Value =StripHTML(descrMaster.Value);
								}

								fieldId = GetFieldId("VC Administration|Master Schedule|Phase");
								var phaseMaster = masterItem.Field<CategoryItemField>(fieldId);
								if(phaseMaster.Options.Any())
								{
									fieldId = GetFieldId("Task List|Phase");
									var phaseChild = child.Field<CategoryItemField>(fieldId);
									phaseChild.OptionText = phaseMaster.Options.First().Text;
								}

								fieldId = GetFieldId("VC Administration|Master Schedule|ESO Member Role");
								var esoMaster = masterItem.Field<CategoryItemField>(fieldId);
								if(esoMaster.Options.Any())
								{
									fieldId = GetFieldId("Task List| ESO Member Role");
									var esoChild = child.Field<CategoryItemField>(fieldId);
									esoChild.OptionText = esoMaster.Options.First().Text;
								}


							}
						}
					}
					break;

				case "Create Workshop":
					
						break;
			}

			
			//get all master schedule items
			//runs 1 time
			
			foreach(var item in fil.Items)
			{
				Item i = new Item();
				//assign fields
				foreach(var embed in item.Field<EmbedItemField>(0).Embeds)
				{
					//copy embed, add to new item
					EmbedService embedServ = new EmbedService(podio);
					//runs approx 130 times
					Embed em = embedServ.AddAnEmbed("").Result;
					i.Field<EmbedItemField>(0).AddEmbed(embed.EmbedId);

				}
				//runs 208x
				await podio.CreateItem(i, 0, false);
				//aprox 339 podio calls for task list
				//754 calls 
			}
		}
        public async System.Threading.Tasks.Task FunctionHandler(RoutedPodioEvent e, ILambdaContext context)
        {
            try
            {
                cId = e.clientId;
                eId = e.environmentId;
                sId = e.solutionId;
                v = e.version;
                baseUrl = System.Environment.GetEnvironmentVariable("BBC_SERVICE_URL");
                apiKey = System.Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY");

                context.Logger.LogLine($"e.version={e.version}");
                context.Logger.LogLine($"e.clientId={e.clientId}");
                context.Logger.LogLine($"e.clientId={e.clientId}");
                context.Logger.LogLine($"e.currentEnvironment.environmentId={e.environmentId}");
                context.Logger.LogLine($"{Newtonsoft.Json.JsonConvert.SerializeObject(e.podioEvent)}");

                var factory = new AuditedPodioClientFactory(e.solutionId, e.version, e.clientId, e.environmentId);
                var podio = factory.ForClient(e.clientId, e.environmentId);

				//runs 208 times
                Item currentItem = await podio.GetItem(int.Parse(e.podioEvent.item_id));

                string serviceAcccount = System.Environment.GetEnvironmentVariable("GOOGLE_SERVICE_ACCOUNT");
                var cred = GoogleCredential.FromJson(serviceAcccount).CreateScoped(Scopes).UnderlyingCredential;
                // Create Drive API service.
                var service = new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = cred,
                    ApplicationName = ApplicationName,
                });

                int taskPId = 0;
                int wMPId = 0;
                int sPId = 0;
                int tLEmbed = 0;
                int wMEmbed = 0;
                int sEmbed = 0;
                context.Logger.LogLine($"{currentItem.ItemId} - Checking which Environment the event is coming from...");
                switch (e.environmentId)
                {
                    case "andela":
                        taskPId = 181590592;
                        wMPId = 181590600;
                        sPId = 181590625;
                        tLEmbed = 179369625;
                        wMEmbed = 179369596;
                        sEmbed = 179369528;
                        break;
                    case "anza":
                        taskPId = 181498980;
                        wMPId = 181500855;
                        sPId = 181500858;
                        tLEmbed = 179365784;
                        wMEmbed = 179365755;
                        sEmbed = 179365687;
                        break;
                    case "bluemoon":
                        taskPId = 181590654;
                        wMPId = 181590679;
                        sPId = 181590680;
                        tLEmbed = 179364988;
                        wMEmbed = 179364959;
                        sEmbed = 179364891;
                        break;
                    case "energygeneration":
                        taskPId = 181590690;
                        wMPId = 181590740;
                        sPId = 181590852;
                        tLEmbed = 177679340;
                        wMEmbed = 177679311;
                        sEmbed = 177679243;
                        break;
                    case "entreprenarium":
                        taskPId = 181590854;
                        wMPId = 181590858;
                        sPId = 181590894;
                        tLEmbed = 179363465;
                        wMEmbed = 179363436;
                        sEmbed = 179363364;
                        break;
                    case "etrilabs":
                        taskPId = 181592133;
                        wMPId = 181592136;
                        sPId = 181592146;
                        tLEmbed = 179362003;
                        wMEmbed = 179362105;
                        sEmbed = 179361986;
                        break;
                    case "globalentrepreneurshipnetwork":
                        taskPId = 181590895;
                        wMPId = 181590903;
                        sPId = 181590904;
                        tLEmbed = 177680698;
                        wMEmbed = 177680669;
                        sEmbed = 177680601;
                        break;
                    case "growthmosaic":
                        taskPId = 181592150;
                        wMPId = 181592152;
                        sPId = 181592153;
                        tLEmbed = 177726305;
                        wMEmbed = 177726276;
                        sEmbed = 177726208;
                        break;
                    case "jokkolabs":
                        taskPId = 181592099;
                        wMPId = 181592104;
                        sPId = 181592106;
                        tLEmbed = 179361207;
                        wMEmbed = 179361158;
                        sEmbed = 179361190;
                        break;
                    case "privatesectorhealthallianceofnigeria":
                        taskPId = 181592154;
                        wMPId = 181592199;
                        sPId = 181592201;
                        tLEmbed = 177726548;
                        wMEmbed = 177726519;
                        sEmbed = 177726451;
                        break;
                    case "southernafricaventurepartnership":
                        taskPId = 181591020;
                        wMPId = 181591505;
                        sPId = 181591506;
                        tLEmbed = 179311616;
                        wMEmbed = 179311718;
                        sEmbed = 179311599;
                        break;
                    case "suguba":
                        taskPId = 181591512;
                        wMPId = 181591533;
                        sPId = 181591534;
                        tLEmbed = 177726706;
                        wMEmbed = 177726584;
                        sEmbed = 177726756;
                        break;
                    case "sycomoreventure":
                        taskPId = 181592205;
                        wMPId = 181592208;
                        sPId = 181592209;
                        tLEmbed = 177727279;
                        wMEmbed = 177727250;
                        sEmbed = 177727182;
                        break;
                    case "theinnovationvillage":
                        taskPId = 181591546;
                        wMPId = 181591552;
                        sPId = 181591566;
                        tLEmbed = 177727512;
                        wMEmbed = 177727405;
                        sEmbed = 177727540;
                        break;
                    case "universityofbritishcolumbia":
                        taskPId = 181591791;
                        wMPId = 181591795;
                        sPId = 181591796;
                        tLEmbed = 178364549;
                        wMEmbed = 178364520;
                        sEmbed = 178364452;
                        break;
                    case "venturesplatform":
                        taskPId = 181591834;
                        wMPId = 181591841;
                        sPId = 181591863;
                        tLEmbed = 177727819;
                        wMEmbed = 177727712;
                        sEmbed = 177727849;
                        break;
                    case "toolkittemplate":
                        taskPId = 180652282;
                        wMPId = 181595831;
                        sPId = 181595828;
                        tLEmbed = 175000509;
                        wMEmbed = 175000486;
                        sEmbed = 177477811;
                        break;
                }
                context.Logger.LogLine($"{currentItem.ItemId} - Environment: {e.environmentId}");


                Dictionary<string, int> fieldIds = new Dictionary<string, int>()
            {
                {$"Task List Parent ID",taskPId },
                {$"Workshop Modules Parent ID", wMPId },
                {$"Survey Parent ID",sPId },
                {$"Task List Embed", tLEmbed},
                {$"Workshop Modules Embed", wMEmbed },
                {$"Survey Embed", sEmbed }
            };
                foreach (KeyValuePair<string, int> keyVal in fieldIds)
                {
                    context.Logger.LogLine($"{currentItem.ItemId} - {keyVal.Key}'s fieldId: {keyVal.Value}");
                }

                context.Logger.LogLine($"{currentItem.ItemId} - cloneFolderId={currentItem.App.Name}");

                var parentId = 0;

                string PARENT_EMBED_FIELD = "";
                int CHILD_EMBED_FIELD = 0;
                context.Logger.LogLine($"{currentItem.ItemId} - Checking app name");
                switch (currentItem.App.Name)
                {
                    case "Task List":
                        CHILD_EMBED_FIELD = fieldIds["Task List Embed"];
                        PARENT_EMBED_FIELD = "link";
						parentId = Convert.ToInt32(currentItem.Field<TextItemField>(fieldIds["Task List Parent ID"]).Value);
						break;

                    case "Workshop Modules":
                        CHILD_EMBED_FIELD = fieldIds["Workshop Modules Embed"];
                        PARENT_EMBED_FIELD = "gdrive-file-name";
						parentId = Convert.ToInt32(currentItem.Field<TextItemField>(fieldIds["Workshop Modules Parent ID"]).Value);
						break;
                    case "Surveys":
                        CHILD_EMBED_FIELD = fieldIds["Survey Embed"];
                        PARENT_EMBED_FIELD = "gdrive-survey";
						parentId = Convert.ToInt32(currentItem.Field<TextItemField>(fieldIds["Survey Parent ID"]).Value);
						break;
                }
				//runs 208 times
				Item parentItem = await podio.GetItem(parentId);
				Item clone = new Item { ItemId = currentItem.ItemId };
				context.Logger.LogLine($"{currentItem.ItemId} - App name was: {currentItem.App.Name}");


                EmbedItemField parentEmbedField = parentItem.Field<EmbedItemField>(PARENT_EMBED_FIELD);

                IEnumerable<Embed> parentEmbeds = parentEmbedField.Embeds;
                List<Embed> embeds = new List<Embed>();
                context.Logger.LogLine($"{currentItem.ItemId} - {parentEmbeds.Count()} embeds on master item");
                string parentFolderId = System.Environment.GetEnvironmentVariable("GOOGLE_PARENT_FOLDER_ID");
                var cloneFolderId = GetSubfolderId(service, podio, e, parentFolderId);//TODO:
                context.Logger.LogLine($"{currentItem.ItemId} - Foreaching thru parent item embeds");
                foreach (Embed em in parentEmbedField.Embeds)
                {
                    context.Logger.LogLine($"{currentItem.ItemId} - Original embed url: {em.OriginalUrl}");
                    context.Logger.LogLine($"{currentItem.ItemId} - Resolved embed url: {em.ResolvedUrl}");
                    if (em.OriginalUrl.Contains(".google."))
                    {
                        context.Logger.LogLine($"{currentItem.ItemId} - Running method \"UpdateOneEmbed\" on {em.OriginalUrl}");
                        await UpdateOneEmbed(service, em, embeds, cloneFolderId, podio, e);
                    }
                }
                context.Logger.LogLine($"{currentItem.ItemId} - Updating item in Podio");
                EmbedItemField cloneEmbedField = clone.Field<EmbedItemField>(CHILD_EMBED_FIELD);
                context.Logger.LogLine($"{currentItem.ItemId} - Embed Count in list: {embeds.Count}");
                foreach (var embed in embeds)
                {
                    context.Logger.LogLine($"{currentItem.ItemId} - Embed ID: {embed.EmbedId}");
                    cloneEmbedField.AddEmbed(embed.EmbedId);
                }
				//runs 208 times
                await podio.UpdateItem(clone, false);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"{e.podioEvent.item_id} - {ex.Message} - {ex.StackTrace} - {ex.InnerException}");
                throw ex;
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
                throw ex;
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
                File original = GetFileByTitle(ds, id,e);
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
                throw ex;
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
                throw ex;
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
                throw ex;
            }
        }

    }
}