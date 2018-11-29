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
using Permission = Google.Apis.Drive.v3.Data.Permission;

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



            const string COMPANY_DIRECTORY_ID = "1m0sPA-z8NXmkinz1xvdbZB7CxvGj9ozk"; // All company folders will be added here

            string PTR_FIELD = "000000000"; // ExID of master item, located on child
            var PARENT_EMBED_FIELD = "link";
            var CHILD_EMBED_FIELD = "link-to-material";
            int int_app_id = int.Parse(rpe.podioEvent.app_id); // const, master-schedule: 21310276
            var CLIENT_SECRET = "JqvyW2a3SdhRzD7BUkYvJ66UI6nNkuVQfRZZXAcZGi5JksFVTiCtzkTIUek2CR3h"; //ex

            IAccessTokenProvider CLIENT_ID = null;

            Podio podio = new Podio(CLIENT_ID, CLIENT_SECRET);
            string cloneFolderId = GetSubfolderId(service, podio, rpe, COMPANY_DIRECTORY_ID);
            ItemService itemService = new ItemService(podio);
            Item parentItem = await itemService.GetItemByExternalId(int_app_id, PTR_FIELD);
            EmbedItemField parentEmbedField = parentItem.Field<EmbedItemField>(PARENT_EMBED_FIELD);
            EmbedItemField cloneEmbedField = rpe.currentItem.Field<EmbedItemField>(CHILD_EMBED_FIELD);
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

        private static string GetSubfolderId(DriveService ds, Podio podio, RoutedPodioEvent rpe, string parentFolder)
        {
            FilesResource.ListRequest listReq = ds.Files.List();
            listReq.Q = "name='" + rpe.currentEnvironment.name + "'";
            var folderId = listReq.Execute().Files[0].Id;

            if(folderId == null)
            {
                File folder = new File
                {
                    Name = rpe.currentEnvironment.name,
                    MimeType = "application/vnd.google-apps.folder",
                };
                folder.Parents.Add(parentFolder);
                folderId = ds.Files.Create(folder).Execute().Id;
            }
            return folderId;
        }

        private static void UpdateOneEmbed(DriveService ds, Embed embed, EmbedItemField embedHere, string subfolderId, Podio podio, RoutedPodioEvent rpe)
        {
            FilesResource.ListRequest listReq = ds.Files.List();
            listReq.Q = "name='" + embed.Title + "'";
            listReq.OrderBy = "createdTime";
            File original = ds.Files.Get(listReq.Execute().Files[0].Id).Execute();
            original.Parents.Clear();
            original.Parents.Add(subfolderId);
            original.Name = "###" + original.Name;
            File clone = ds.Files.Copy(original, original.Id).Execute();

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
        
    }
}