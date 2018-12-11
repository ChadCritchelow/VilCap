
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
        //public async Task<int> GetID(string key)
        //{
        //    BbcServiceClient bbc = new BbcServiceClient(baseUrl, apiKey);//TODO
        //    var x = await bbc.GetKey(cId,eId,sId,v, key);
        //    return int.Parse(x);
        //}
        public async System.Threading.Tasks.Task FunctionHandler(RoutedPodioEvent e, ILambdaContext context)
        {
            cId = e.clientId;
            eId = e.environmentId;
            sId = e.solutionId;
            v = e.version;
            baseUrl = System.Environment.GetEnvironmentVariable("BBC_SERVICE_URL");
            apiKey = System.Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY");
            Console.WriteLine("LETS GO");
            context.Logger.LogLine($"e.version={e.version}");
            context.Logger.LogLine($"e.clientId={e.clientId}");
            context.Logger.LogLine($"e.clientId={e.clientId}");
            context.Logger.LogLine($"e.currentEnvironment.environmentId={e.environmentId}");
            context.Logger.LogLine($"{Newtonsoft.Json.JsonConvert.SerializeObject(e.podioEvent)}");
            //started here
            var factory = new AuditedPodioClientFactory(e.solutionId, e.version, e.clientId, e.environmentId);
            var podio = factory.ForClient(e.clientId, e.environmentId);
                       
            //vilcapSpaces = e.currentEnvironment.deployments.First(a => a.appId == "vilcap").deployedSpaces;

            Item currentItem = await podio.GetItem(int.Parse(e.podioEvent.item_id));

            string serviceAcccount = System.Environment.GetEnvironmentVariable("GOOGLE_SERVICE_ACCOUNT");
            var cred = GoogleCredential.FromJson(serviceAcccount).CreateScoped(Scopes).UnderlyingCredential;
            // Create Drive API service.
            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = cred,
                ApplicationName = ApplicationName,
            });

            //Anza only
            int taskPId=0;
            int wMPId=0;
            int sPId=0;
            int tLEmbed=0;
            int wMEmbed=0;
            int sEmbed=0;
            
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
            
            
            Dictionary<string, int> fieldIds = new Dictionary<string, int>()
            {
                {$"Task List Parent ID",taskPId },
                {$"Workshop Modules Parent ID", wMPId },
                {$"Survey Parent ID",sPId },
                {$"Task List Embed", tLEmbed},
                {$"Workshop Modules Embed", wMEmbed },
                {$"Survey Embed", sEmbed }
            };
            
            context.Logger.LogLine($"cloneFolderId={currentItem.App.Name}");
            ItemService itemService = new ItemService(podio);
            var parentId = 0;
            //search for correct parent item based on title????
            if(currentItem.App.Name=="Task List")
            {
                parentId = Convert.ToInt32(currentItem.Field<TextItemField>(fieldIds["Task List Parent ID"]).Value);
            }
            else if (currentItem.App.Name=="Workshop Modules")
            {
                parentId = Convert.ToInt32(currentItem.Field<TextItemField>(fieldIds["Workshop Modules Parent ID"]).Value);
            }
            else 
            {
                parentId = Convert.ToInt32(currentItem.Field<TextItemField>(fieldIds["Survey Parent ID"]).Value);
            }
            
            Item parentItem = await itemService.GetItem(parentId);
            Item clone = new Item { ItemId = currentItem.ItemId };

            //TODO: Add in multi app functionality when deployed spaces dict is ready to go
            string PARENT_EMBED_FIELD="";
            int CHILD_EMBED_FIELD=0;
            context.Logger.LogLine("Checking app name");
            switch(currentItem.App.Name)
            {
                case "Task List":
                    context.Logger.LogLine("App was Task List");
                    CHILD_EMBED_FIELD= fieldIds["Task List Embed"];
                    PARENT_EMBED_FIELD = "link";
                    break;

                case "Workshop Modules":
                    context.Logger.LogLine("App was Workshop Modules");
                    CHILD_EMBED_FIELD = fieldIds["Workshop Modules Embed"];
                    PARENT_EMBED_FIELD = "gdrive-file-name";
                    break;
                case "Surveys":
                    context.Logger.LogLine("App was Link to Survey");
                    CHILD_EMBED_FIELD = fieldIds["Survey Embed"];
                    PARENT_EMBED_FIELD = "gdrive-survey";
                    break;
                    
            }

            context.Logger.LogLine("Continuing after switch statement");
            EmbedItemField parentEmbedField = parentItem.Field<EmbedItemField>(PARENT_EMBED_FIELD);
            
            IEnumerable<Embed> parentEmbeds = parentEmbedField.Embeds;
            List<Embed> embeds= new List<Embed>();
            context.Logger.LogLine($"{parentEmbeds.Count()} files on master item");
            var cloneFolderId = GetSubfolderId(service, podio, e, "1m0sPA-z8NXmkinz1xvdbZB7CxvGj9ozk");//TODO:
            context.Logger.LogLine("Foreaching thru parent item embeds");
            foreach (Embed em in parentEmbedField.Embeds)
            {
                context.Logger.LogLine($"Original embed url: {em.OriginalUrl}");
                if (em.OriginalUrl.Contains(".google."))
                {
                    context.Logger.LogLine($"Running method \"UpdateOneEmbed\" on {em.Title}");
                    await UpdateOneEmbed(service, em, embeds, cloneFolderId, podio, e);
                }
            }
            context.Logger.LogLine("Updating item in Podio");
            //context.Logger.LogLine($"Embed Field Count for item we're updating {cloneEmbedField.Embeds.Count()}");
            EmbedItemField cloneEmbedField = clone.Field<EmbedItemField>(CHILD_EMBED_FIELD);
            context.Logger.LogLine($"Embed Count in list: {embeds.Count}");
            foreach (var embed in embeds)
            {
                context.Logger.LogLine($"Embed ID: {embed.EmbedId}");
                cloneEmbedField.AddEmbed(embed.EmbedId);
            }
            await podio.UpdateItem(clone, false);
        }

        //public static async Task IterateAsync(DriveService ds, IEnumerable<Embed> embedList, EmbedItemField embedHere, Podio podio, string subfolderId, RoutedPodioEvent e)

        private static string GetSubfolderId(DriveService ds, Podio podio, RoutedPodioEvent e, string parentFolder)
        {
            Console.WriteLine($"EnvID: {e.environmentId}");
            FilesResource.ListRequest listReq = ds.Files.List();
            listReq.Q = "name='" + e.environmentId + "'";
            string folderId="";

            if (listReq.Execute().Files.Any())
            {
                 folderId = listReq.Execute().Files[0].Id;
            }
            else if (folderId == "")
            {
                //await Task.Run(() => { UpdateOneEmbed(ds, em, embedHere, subfolderId, podio, e); });
                File folder = new File
                {
                    Name = e.environmentId,
                    MimeType = "application/vnd.google-apps.folder",
                };
                folder.Parents.Add(parentFolder);
                var request=ds.Files.Create(folder);
                request.Fields = "id";

                folderId = request.Execute().Id;
            }
            return folderId;
        }

        public static async Task UpdateOneEmbed(DriveService ds, Embed embed, List<Embed> embeds, string subfolderId, Podio podio, RoutedPodioEvent e)
        {

       //     File original = GetFile(ds, embed.Title);          
            var id = GetDriveId(embed.OriginalUrl);
            Console.WriteLine($"ID that we pull from the URL: {id}");
            File original=GetFileByTitle(ds,id);
            if (original.Parents == null)
                original.Parents = new List<string>();
            Console.WriteLine($"ID from the file itself: {original.Id}, Name: {original.Name}");
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
                Console.WriteLine("Adding embed thru service");
                
                Console.WriteLine($"CloneID: {clone.Id}");
                var req=ds.Files.Get(clone.Id);
                req.Fields = "webViewLink";
                clone=req.Execute();
                Embed em= embedServ.AddAnEmbed(clone.WebViewLink).Result;
                Console.WriteLine($"Embed Link: {em.OriginalUrl}");
                Console.WriteLine("Embed added");
                Console.WriteLine($"WebViewLink: {clone.WebViewLink}");
                embeds.Add(em);
            });
            
        }
        public static string GetDriveId(string url)
        {
            Console.WriteLine($"Attempting to get the ID from URL: {url}");
            string[] substr = url.Split(new char[] { '=', '/' });
            foreach (string s in substr)
            {
                if (s.Length == 44)
                {
                    Console.WriteLine($"Found ID: {s} from url: {url}");
                    return s;
                }
            }
            Console.WriteLine($"Could not find ID for url: {url}");
            return null;
        }

        //public static File GetFile(DriveService ds, string title)
        //{
        //    FilesResource.ListRequest listReq = ds.Files.List();
        //    listReq.Q = "name='" + title + "'"; // Todo: format 
        //    listReq.orderBy = "createdTime";
        //    return ds.Files.Get(listReq.Execute().Files[0].Id).Execute();
        //}

        public static File GetFileByTitle(DriveService ds, string id)
        {
            var request = ds.Files.Get(id);
            request.Fields = "parents, name";
            var file = request.Execute();
            return file;
        }

    }
}