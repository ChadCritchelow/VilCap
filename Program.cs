
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
using System.Collections;
using Task = System.Threading.Tasks.Task;
using File = Google.Apis.Drive.v3.Data.File;
using BrickBridge.Lambda.VilCap;

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

        static string[] Scopes = { DriveService.Scope.DriveReadonly };
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
            List<Task> tasks = new List<Task>();
            foreach (Embed em in parentEmbedField.Embeds)
            {
                tasks.Add(
                    Task.Run(() => { UpdateOneEmbed(service, em, cloneEmbedField, cloneFolderId, podio, e); })
                );
            }
            context.Logger.LogLine($"Count of tasks: {tasks.Count()}");
            await Task.WhenAll(tasks);
        }

        public static async Task IterateAsync(DriveService ds, IEnumerable<Embed> embedList, EmbedItemField embedHere, Podio podio, string subfolderId, RoutedPodioEvent rpe)
        {
            foreach (Embed em in embedList)
            {
                await Task.Run(() => { UpdateOneEmbed(ds, em, embedHere, subfolderId, podio, rpe); });
            }
        }

        public static void UpdateOneEmbed(DriveService ds, Embed embed, EmbedItemField embedHere, string subfolderId, Podio podio, RoutedPodioEvent rpe)
        {
            File original = ds.Files.Get(GetFileIdByTitle(ds, embed.Title)).Execute();
            original.Parents.Clear();
            original.Parents.Add(subfolderId);
            File clone = ds.Files.Copy(original, original.Id).Execute();

            Task.Run(() =>
            {   // Todo Implement: { "type": "anyone", "role": "writer" }
                            Google.Apis.Drive.v3.Data.Permission permission = null;
                new PermissionsResource.CreateRequest(ds, permission, clone.Id).Execute();
            });

            Task.Run(() =>
            {
                Embed newEmbed = new Embed { OriginalUrl = clone.WebViewLink };
                embedHere.AddEmbed(newEmbed.EmbedId);
            });
        }

        public static string GetFileIdByTitle(DriveService ds, string title)
        {
            FilesResource.ListRequest listReq = ds.Files.List();
            listReq.Q = "name='" + title + "'"; // Todo: format         
            return listReq.Execute().Files[0].Id;

        }
    }
}