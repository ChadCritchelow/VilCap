using System;
using System.IO;
using System.Collections.Generic;
using Task = System.Threading.Tasks.Task;
using Amazon.Lambda.Core;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Drive.v3.Data;
using System.Threading.Tasks;
using PodioCore;
using PodioCore.Utils.ItemFields;
using PodioCore.Models;
<<<<<<< HEAD
                        //using PodioCore.Services;
=======
>>>>>>> 4053751... Changes for Google API Authentication
using BrickBridge.Models;
using PodioCore.Items;

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
<<<<<<< HEAD
            const int VC_ADMIN = 6145742; // VC Administration Workspace (21310276?)
            string PTR_FIELD = " ######### "; // ExID of master item, located on child
=======
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
>>>>>>> 4053751... Changes for Google API Authentication
            var PARENT_EMBED_FIELD = "link";
            var CHILD_EMBED_FIELD = "link-to-material";
            var APP_ID = rpe.podioEvent.app_id; // task-list-cuanfh" //ex
            var I_ID = rpe.podioEvent.item_id;
            var R_ID = rpe.podioEvent.item_revision_id;
            var X_ID = rpe.podioEvent.external_id;
            var CLIENT_SECRET = "JqvyW2a3SdhRzD7BUkYvJ66UI6nNkuVQfRZZXAcZGi5JksFVTiCtzkTIUek2CR3h"; //ex
            var CLIENT_WS_ID = rpe.clientId;
            var CLONE_FOLDER_ID = " ######### "; // rpe.currentEnvironment.name?;
            IAccessTokenProvider CLIENT_ID = null;

            Podio podio = new Podio(CLIENT_ID, CLIENT_SECRET);
            ItemService itemService = new ItemService(podio);
            Item parentItem = await itemService.GetItemByExternalId(VC_ADMIN, PTR_FIELD);
            Item clone = rpe.currentItem;
            EmbedItemField parentEmbedField = parentItem.Field<EmbedItemField>(PARENT_EMBED_FIELD);
            EmbedItemField cloneEmbedField = clone.Field<EmbedItemField>(CHILD_EMBED_FIELD);
            IEnumerable<Embed> parentEmbeds = parentEmbedField.Embeds;

            DriveService drive = new DriveService();

            await IterateAsync(drive, parentEmbeds, cloneEmbedField, podio, CLONE_FOLDER_ID, rpe); //Option 2



<<<<<<< HEAD
                        //Task.WhenAll()
                        //Item childItem = await itemService.GetItemByExternalId(CLIENT_WS_ID, X_ID);
                        //UpdateEmbeds(drive, parentEmbeds, cloneEmbedField, rpe.currentEnvironment.name); //Option 1
                        //var fieldId = 0;
                        //Item updateItem = new Item();
                        //updateItem.ItemId = rpe.currentItem.ItemId;
                        //fieldId = GetFileId()
        }

        private static async Task<string> UpdateOneEmbed(DriveService service, Embed embed, EmbedItemField embedHere, string childFolder, Podio podio, RoutedPodioEvent rpe)
        {
            // GET the original file
            FilesResource.ListRequest list = service.Files.List();
            list.Q = "title='" + rpe.currentItem.Title + "'"; // Todo: format
            File content = list.Execute().Items[0];
           
            content.Parents[0].Id = childFolder; // Tell Google to copy into the client's subfolder
            FilesResource.CopyRequest copy = service.Files.Copy(file, original.Id); 
            copy.Convert = true;
            var result = request.Execute().AlternateLink;
=======
            await IterateAsync(drive, parentEmbeds, cloneEmbedField, podio, rpe.currentEnvironment.name, rpe); //Option 2




            //var fieldId = 0;
            //Item updateItem = new Item();
            //updateItem.ItemId = rpe.currentItem.ItemId;
            //fieldId = GetFileId()
            return "";
        }

        // Returns the FILE matching the provided title
        private static File GetFileId(DriveService service, string title)
        {
            var request = service.Files.List();
            request.Q = "title='" + title + "'"; // Todo: format
            return request.Execute().Files[0];
        }

        // Returns a STRING link to the new file
        private static string CopyFile(DriveService service, string title, string childFolder)
        {
            var original = GetFileId(service, title);
            File file = original;
            file.Parents[0] = childFolder;
            var request = service.Files.Copy(file, original.Id);
            //request.Convert = true;//?? I don't know what this is for John
            return request.Execute().WebContentLink;//maybe WebViewLink?
        }

        // OPTION 1
        // Adds copies of each of the linked files from the parent's
        // emed field to the clone's embed field
        /*
        private static async void UpdateEmbeds(DriveService service, IEnumerable<Embed> embedList, EmbedItemField embedHere, string childFolder)
        {
            foreach (Embed e in embedList)
            {
                Embed em = new Embed();
                em.OriginalUrl = CopyFile(service, e.Title, childFolder);
                embedHere.AddEmbed(em.EmbedId);
            }
            //Todo: Podio update item calls

            //Todo: Determine O(n) of calls
        }
        */
        private static void UpdateOneEmbed(DriveService service, Embed embed, EmbedItemField embedHere, string childFolder, Podio podio, RoutedPodioEvent rpe)
        {
            var original = GetFileId(service, embed.Title);
            File file = original;
            file.Parents[0] = childFolder;
            var request = service.Files.Copy(file, original.Id);
            //request.Convert = true;??
            var result = request.Execute().WebContentLink;
>>>>>>> 4053751... Changes for Google API Authentication
        }

        // OPTION 2
        private static async Task IterateAsync(DriveService service, IEnumerable<Embed> embedList, EmbedItemField embedHere, Podio podio, string childFolder, RoutedPodioEvent rpe)
        {
            IEnumerable<Task> tasks;
            foreach (Embed em in embedList)
            {
                await Task.Run(() => { UpdateOneEmbed(service, em, embedHere, childFolder, podio, rpe); });
            }

        }

                        // Returns the FILE matching the provided title
                        /*
                        private static File GetFileId(DriveService service, string title)
                        {
                            var request = service.Files.List();
                            request.Q = "title='" + title + "'"; // Todo: format
                            return request.Execute().Items[0];
                        }
                        */

    }
}

                        // Returns a STRING link to the new file
                        /*
                        private static string CopyFile(DriveService service, string title, string childFolder)
                        {
                            var original = GetFileId(service, title);
                            File file = original;
                            file.Parents[0].Id = childFolder;
                            var request = service.Files.Copy(file, original.Id);
                            request.Convert = true;
                            return request.Execute().AlternateLink;
                        }
                        */

                        // OPTION 1
                        // Adds copies of each of the linked files from the parent's
                        // embed field to the clone's embed field
                        /*
                        private static async void UpdateEmbeds(DriveService service, IEnumerable<Embed> embedList, EmbedItemField embedHere, string childFolder)
                        {
                            foreach (Embed e in embedList)
                            {
                                Embed em = new Embed();
                                em.OriginalUrl = CopyFile(service, e.Title, childFolder);
                                embedHere.AddEmbed(em.EmbedId);
                            }
                            //Todo: Podio update item calls

                            //Todo: Determine O(n) of calls
                        }
                        */

                        /*{
                            "fileId": "1zLAsLbN6lkvTtSafnu9kG7jdV76e80d6-JveBj8r3ks",
                            "convert": true,
                            "resource": {
                                "title": "FileFromAPI",
                                "parents": [{
                                    "id": "1QyvnXx21CfXV0xlyQtD_zhBQGT1_CtKp"
                                }]
                         }*/
