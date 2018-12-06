
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

            var cloneFolderId = currentItem.App.Name;
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
            EmbedItemField cloneEmbedField = clone.Field<EmbedItemField>(CHILD_EMBED_FIELD);
            IEnumerable<Embed> parentEmbeds = parentEmbedField.Embeds;
            context.Logger.LogLine($"{parentEmbeds.Count()} files on master item");
            foreach (Embed em in parentEmbedField.Embeds)
            {
                context.Logger.LogLine($"Running method \"UpdateOneEmbed\" on {em.Title}");
                UpdateOneEmbed(service, em, cloneEmbedField, cloneFolderId, podio, e);
            }
            context.Logger.LogLine("Updating item in Podio");
            await podio.UpdateItem(clone, false);
        }

        private static string GetSubfolderId(DriveService ds, Podio podio, RoutedPodioEvent e, string parentFolder)
        {
            FilesResource.ListRequest listReq = ds.Files.List();
            listReq.Q = "name='" + e.environmentId + "'";
            string folderId="";

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
                folderId = ds.Files.Create(folder).Execute().Id;
            }
            return folderId;
        }

        public static void UpdateOneEmbed(DriveService ds, Embed embed, EmbedItemField embedHere, string subfolderId, Podio podio, RoutedPodioEvent e)
        {
            Console.WriteLine($"Embed description: {embed.Description}");
            Console.WriteLine($"Embed Provider name: {embed.ProviderName}");
            Console.WriteLine($"Count of files on embed: {embed.Files.Count}");
            var id = embed.OriginalUrl.Remove(0, 35);//@"https://docs.google.com/document/d/";
            id = id.Remove(id.Length - 5, 5);
            File original = GetFileByTitle(ds, id);
            if (original.Parents == null)
                original.Parents = new List<string>();
            original.Parents.Clear();
            original.Parents.Add(subfolderId);
            original.Name = "###" + original.Name;

            File clone = ds.Files.Copy(original, id).Execute();

            Task.Run(() =>
            {
                Permission permission = new Permission
                {
                    Role = "writer",
                    Type = "anyone"
                };
                new PermissionsResource.CreateRequest(ds, permission, clone.Id).Execute();
            });

            Task.Run(() =>
            {
                Embed newEmbed = new Embed { OriginalUrl = clone.WebViewLink };
                embedHere.AddEmbed(newEmbed.EmbedId);
            });
            
        }

        public static File GetFileByTitle(DriveService ds, string id)
        {
            FilesResource.ListRequest listReq = ds.Files.List();
            var request = ds.Files.Get(id);
            request.Fields = "parents";
            var file = request.Execute();
            return file;
        }

    }
}