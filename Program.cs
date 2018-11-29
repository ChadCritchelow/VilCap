using System.Collections.Generic;
using Amazon.Lambda.Core;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using System.Threading.Tasks;
using PodioCore;
using PodioCore.Utils.ItemFields;
using PodioCore.Models;
using BrickBridge.Models;
using PodioCore.Items;

using Task = System.Threading.Tasks.Task;
using File = Google.Apis.Drive.v3.Data.File;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]


namespace BrickBridge.Lambda.VilCap
{
    public class CopyFileToGoogleDrive
    {

        static string[] Scopes = { DriveService.Scope.DriveReadonly };
        static string ApplicationName = "VilCap";
        static LambdaMemoryStore memoryStore = new LambdaMemoryStore();

        private async void FunctionHandler(RoutedPodioEvent rpe, ILambdaContext context)
        {
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

            const int VC_ADMIN = 6145742; // VC Administration Workspace space_id
            string PTR_FIELD = "000000000"; // ExID of master item, located on child
            var PARENT_EMBED_FIELD = "link";
            var CHILD_EMBED_FIELD = "link-to-material";
            var APP_ID = rpe.podioEvent.app_id; // task-list-cuanfh? master-schedule:21310276
            var I_ID = rpe.podioEvent.item_id;
            var R_ID = rpe.podioEvent.item_revision_id;
            var X_ID = rpe.podioEvent.external_id;
            var CLIENT_SECRET = "JqvyW2a3SdhRzD7BUkYvJ66UI6nNkuVQfRZZXAcZGi5JksFVTiCtzkTIUek2CR3h"; //ex
            var CLIENT_WS_ID = rpe.clientId;
            var cloneFolderId = " ######### "; // rpe.currentEnvironment.name?
            IAccessTokenProvider CLIENT_ID = null;

            Podio podio = new Podio(CLIENT_ID, CLIENT_SECRET);
            ItemService itemService = new ItemService(podio);
            Item parentItem = await itemService.GetItemByExternalId(VC_ADMIN, PTR_FIELD);
            Item clone = rpe.currentItem;
            EmbedItemField parentEmbedField = parentItem.Field<EmbedItemField>(PARENT_EMBED_FIELD);
            EmbedItemField cloneEmbedField = clone.Field<EmbedItemField>(CHILD_EMBED_FIELD);
            IEnumerable<Embed> parentEmbeds = parentEmbedField.Embeds;

            List<Task> tasks = new List<Task>();
            foreach (Embed em in parentEmbedField.Embeds)
            {
                  tasks.Add( 
                      Task.Run(() => { UpdateOneEmbed(service, em, cloneEmbedField, cloneFolderId, podio, rpe); }) 
                  );
            }
            await Task.WhenAll(tasks);
        }

        private static void UpdateOneEmbed(DriveService ds, Embed embed, EmbedItemField embedHere, string subfolderId, Podio podio, RoutedPodioEvent rpe)
        {
            FilesResource.ListRequest listReq = ds.Files.List();
            listReq.Q = "name='" + embed.Title + "'";
            listReq.OrderBy = "createdTime";
            File original = ds.Files.Get(listReq.Execute().Files[0].Id).Execute();
            original.Parents.Clear();
            original.Parents.Add(subfolderId);
            File clone = ds.Files.Copy(original, original.Id).Execute();

            Task.Run(() =>
            {   /* Todo Implement: { "type": "anyone", "role": "writer" } */
                Google.Apis.Drive.v3.Data.Permission permission = null; 
                new PermissionsResource.CreateRequest(ds, permission, clone.Id).Execute();
            });

            Task.Run(() =>
            {
                Embed newEmbed = new Embed { OriginalUrl = clone.WebViewLink };
                embedHere.AddEmbed(newEmbed.EmbedId);
            });
        }
        
    }
}