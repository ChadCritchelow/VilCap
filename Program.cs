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

        private async Task<string> FunctionHandler(RoutedPodioEvent rpe, ILambdaContext context)
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

            const int VC_ADMIN = 6145742; // VC Administration Workspace (21310276?)
            string PTR_FIELD = "000000000"; // ExID of master item, located on child
            var PARENT_EMBED_FIELD = "link";
            var CHILD_EMBED_FIELD = "link-to-material";
            var APP_ID = rpe.podioEvent.app_id; // task-list-cuanfh
            var I_ID = rpe.podioEvent.item_id;
            var R_ID = rpe.podioEvent.item_revision_id;
            var X_ID = rpe.podioEvent.external_id;
            var CLIENT_SECRET = "JqvyW2a3SdhRzD7BUkYvJ66UI6nNkuVQfRZZXAcZGi5JksFVTiCtzkTIUek2CR3h"; //ex
            var CLIENT_WS_ID = rpe.clientId;
            //var CLONE_FOLDER_ID = " ######### "; // rpe.currentEnvironment.name?
            IAccessTokenProvider CLIENT_ID = null;

            Podio podio = new Podio(CLIENT_ID, CLIENT_SECRET);
            ItemService itemService = new ItemService(podio);
            Item parentItem = await itemService.GetItemByExternalId(VC_ADMIN, PTR_FIELD);
            Item clone = rpe.currentItem;
            EmbedItemField parentEmbedField = parentItem.Field<EmbedItemField>(PARENT_EMBED_FIELD);
            EmbedItemField cloneEmbedField = clone.Field<EmbedItemField>(CHILD_EMBED_FIELD);
            IEnumerable<Embed> parentEmbeds = parentEmbedField.Embeds;

            await IterateAsync(service, parentEmbeds, cloneEmbedField, podio, rpe.currentEnvironment.name, rpe); //Option 2
            return "";
        }

        private static async Task IterateAsync(DriveService ds, IEnumerable<Embed> embedList, EmbedItemField embedHere, Podio podio, string childFolder, RoutedPodioEvent rpe)
        {
            //IEnumerable<Task> tasks;
            foreach (Embed em in embedList)
            {
                await Task.Run(() => { UpdateOneEmbed(ds, em, embedHere, childFolder, podio, rpe); });
            }
        }

        // Returns a STRING link to the new file
        private static string CopyFile(DriveService ds, string originalId, string subfolderId)
        {
            File original = ds.Files.Get(originalId).Execute();
            original.Parents.Clear();
            original.Parents.Add(subfolderId);
            return ds.Files.Copy(original, original.Id).Execute().WebViewLink;
        }

        // Returns the FILE matching the provided title
        private static File GetFileId(DriveService ds, string title)
        {
            var request = ds.Files.List();
            request.Q = "title='" + title + "'"; // Todo: format
            return request.Execute().Files[0];
        }

        private static void UpdateOneEmbed(DriveService ds, Embed embed, EmbedItemField embedHere, string childFolder, Podio podio, RoutedPodioEvent rpe)
        {
            var original = GetFileId(ds, embed.Title);
            File file = original;
            file.Parents[0] = childFolder;
            var request = ds.Files.Copy(file, original.Id);
            var result = request.Execute().WebContentLink;
        }

        
    }
}