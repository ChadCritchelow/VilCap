
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
using PodioCore.Comments;
using System.Collections;
using Task = System.Threading.Tasks.Task;
using File = Google.Apis.Drive.v3.Data.File;
using BrickBridge.Lambda.VilCap;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]


namespace vilcapCopyFileToGoogleDrive
{
    public class CopyFileToGoogleDrive:saasafrasLambdaBaseFunction.Function
    {
        private int GetfieldId(string key)
        {
            var field = vilcapSpaces[key];
            return int.Parse(field);
        }

        private Dictionary<string, string> vilcapSpaces;
        int fieldId;

        static string[] Scopes = { DriveService.Scope.DriveReadonly };
        static string ApplicationName = "VilCap";
        static LambdaMemoryStore memoryStore = new LambdaMemoryStore();
        
        public override async System.Threading.Tasks.Task InnerHandler(RoutedPodioEvent e, ILambdaContext context)
        {
            context.Logger.LogLine($"Podio proxy url: {Config.PODIO_PROXY_URL}, bbc service url: {Config.BBC_SERVICE_URL}, bbc service api key: {Config.BBC_SERVICE_API_KEY}");
            System.Environment.SetEnvironmentVariable("PODIO_PROXY_URL", Config.PODIO_PROXY_URL);
            System.Environment.SetEnvironmentVariable("BBC_SERVICE_URL", Config.BBC_SERVICE_URL);
            System.Environment.SetEnvironmentVariable("BBC_SERVICE_API_KEY", Config.BBC_SERVICE_API_KEY);
            context.Logger.LogLine($"e.version={e.version}");
            context.Logger.LogLine($"e.clientId={e.clientId}");
            context.Logger.LogLine($"e.clientId={e.clientId}");
            context.Logger.LogLine($"e.currentEnvironment.environmentId={e.currentEnvironment.environmentId}");
            var factory = new AuditedPodioClientFactory(e.appId, e.version, e.clientId, e.currentEnvironment.environmentId);
            var podio = factory.ForClient(e.clientId, e.currentEnvironment.environmentId);

            vilcapSpaces = e.currentEnvironment.deployments.First(a => a.appId == "vilcap").deployedSpaces;

            fieldId = 0;

            //These are stored in AWS Lambda
            string client_id = System.Environment.GetEnvironmentVariable("GOOGLE_API_CLIENT_ID");
            string client_secret = System.Environment.GetEnvironmentVariable("GOOGLE_API_CLIENT_SECRET");

            UserCredential credential;
            var secrets = new ClientSecrets
            {
                ClientId = client_id,
                ClientSecret = client_secret
            };
            credential = GoogleWebAuthorizationBroker.AuthorizeAsync(secrets, Scopes, "user", System.Threading.CancellationToken.None, memoryStore).Result;

            // Create Drive API service.
            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            //var APP_ID = e.podioEvent.app_id; // task-list-cuanfh
            //var I_ID = e.podioEvent.item_id;
            //var R_ID = e.podioEvent.item_revision_id;
            //var X_ID = e.podioEvent.external_id;
            //var CLIENT_SECRET = "JqvyW2a3SdhRzD7BUkYvJ66UI6nNkuVQfRZZXAcZGi5JksFVTiCtzkTIUek2CR3h"; //ex
            //var CLIENT_WS_ID = e.clientId;
            var cloneFolderId = e.currentItem.App.Name;
            //IAccessTokenProvider CLIENT_ID = null;

            ItemService itemService = new ItemService(podio);
            fieldId = GetfieldId("VC Toolkit Template|Task List|Parent ID");//add in the field ID's key for "Parent ID"
            var parentId=Convert.ToInt32( e.currentItem.Field<TextItemField>(fieldId).Value);
            Item parentItem = await itemService.GetItem(parentId);
            Item clone = new Item { ItemId=e.currentItem.ItemId};

            //TODO: Add in multi app functionality when deployed spaces dict is ready to go
            var PARENT_EMBED_FIELD = "link";
            var CHILD_EMBED_FIELD = "linked-files";
            EmbedItemField parentEmbedField = parentItem.Field<EmbedItemField>(PARENT_EMBED_FIELD);
            EmbedItemField cloneEmbedField = clone.Field<EmbedItemField>(CHILD_EMBED_FIELD);
            IEnumerable<Embed> parentEmbeds = parentEmbedField.Embeds;

            List<Task> tasks = new List<Task>();
            foreach (Embed em in parentEmbedField.Embeds)
            {
                  tasks.Add( 
                      Task.Run(() => { UpdateOneEmbed(service, em, cloneEmbedField, cloneFolderId, podio, e); }) 
                  );
            }
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