
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
        public async Task<int> GetID(string key)
        {
            BbcServiceClient bbc = new BbcServiceClient(baseUrl, apiKey);//TODO
            var x = await bbc.GetKey(cId,eId,sId,v, key);
            return int.Parse(x);
        }
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

            
            context.Logger.LogLine($"cloneFolderId={currentItem.App.Name}");
            ItemService itemService = new ItemService(podio);
            var parentId = Convert.ToInt32(currentItem.Field<TextItemField>(await GetID("VC Toolkit Template|Task List|Parent ID")).Value);
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
                    CHILD_EMBED_FIELD= await GetID("VC Toolkit Template|Task List|Linked Files");
                    PARENT_EMBED_FIELD = "link";
                    break;

                case "Workshop Modules":
                    context.Logger.LogLine("App was Workshop Modules");
                    CHILD_EMBED_FIELD = await GetID("VC Toolkit Template|Workshop Modules|Link to Material");
                    PARENT_EMBED_FIELD = "gdrive-file-name";
                    break;
                case "Surveys":
                    context.Logger.LogLine("App was Link to Survey");
                    CHILD_EMBED_FIELD = await GetID("VC Toolkit Template|Surveys|Link to Survey");
                    PARENT_EMBED_FIELD = "gdrive-survey";
                    break;
                    
            }

            context.Logger.LogLine("Continuing after switch statement");
            EmbedItemField parentEmbedField = parentItem.Field<EmbedItemField>(PARENT_EMBED_FIELD);
            
            IEnumerable<Embed> parentEmbeds = parentEmbedField.Embeds;
            List<Embed> embeds= new List<Embed>();
            context.Logger.LogLine($"{parentEmbeds.Count()} files on master item");
            var cloneFolderId = GetSubfolderId(service, podio, e, "1m0sPA-z8NXmkinz1xvdbZB7CxvGj9ozk");//TODO:
            foreach (Embed em in parentEmbedField.Embeds)
            {
                context.Logger.LogLine($"Running method \"UpdateOneEmbed\" on {em.Title}");
                UpdateOneEmbed(service, em, embeds, cloneFolderId, podio, e);
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

        public static void UpdateOneEmbed(DriveService ds, Embed embed, List<Embed> embeds, string subfolderId, Podio podio, RoutedPodioEvent e)
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
            original.Name = "###" + original.Name;

            File clone = ds.Files.Copy(original, id).Execute();

            Task.Run(() =>
            {
                Permission p = new Permission
                {
                    Role = "writer",
                    Type = "anyone"
                };
                new PermissionsResource.CreateRequest(ds, p, clone.Id).Execute();
            });

            Task.Run(() =>
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
            string[] substr = url.Split(new char[] { '=', '/' });
            foreach (string s in substr) if (s.Length == 44) return s;
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
            request.Fields = "parents";
            var file = request.Execute();
            return file;
        }

    }
}