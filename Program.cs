
using Task = System.Threading.Tasks.Task;
using File = Google.Apis.Drive.v3.Data.File;
using Permission = Google.Apis.Drive.v3.Data.Permission;
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
            Console.WriteLine("LETS GO");
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
            credential = GoogleWebAuthorizationBroker.AuthorizeAsync(secrets, Scopes, "toolkit@vilcap.com", System.Threading.CancellationToken.None, memoryStore).Result;

            // Create Drive API service.
            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });


            const string COMPANY_DIRECTORY_ID = "1m0sPA-z8NXmkinz1xvdbZB7CxvGj9ozk";

            //var CLIENT_SECRET = "JqvyW2a3SdhRzD7BUkYvJ66UI6nNkuVQfRZZXAcZGi5JksFVTiCtzkTIUek2CR3h"; 
            //IAccessTokenProvider CLIENT_ID = null;
            //TODO: Add in multi app functionality when deployed spaces dict is ready to go

            var PARENT_EMBED_FIELD = "link";
            var CHILD_EMBED_FIELD = "linked-files";
            
            Podio podio = new Podio(CLIENT_ID, CLIENT_SECRET);
            string cloneFolderId = GetSubfolderId(service, podio, e, COMPANY_DIRECTORY_ID);
            ItemService itemService = new ItemService(podio);
            fieldId = GetfieldId("VC Toolkit Template|Task List|Parent ID"); //add in the field ID's key for "Parent ID"

            var parentId = Convert.ToInt32(e.currentItem.Field<TextItemField>(fieldId).Value);
            Item parentItem = await itemService.GetItem(parentId);
            Item clone = new Item { ItemId = e.currentItem.ItemId };
            EmbedItemField parentEmbedField = parentItem.Field<EmbedItemField>(PARENT_EMBED_FIELD);
            EmbedItemField cloneEmbedField = e.currentItem.Field<EmbedItemField>(CHILD_EMBED_FIELD);
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

        private static string GetSubfolderId(DriveService ds, Podio podio, RoutedPodioEvent e, string parentFolder)
        {
            FilesResource.ListRequest listReq = ds.Files.List();
            listReq.Q = "name='" + e.currentEnvironment.name + "'";
            var folderId = listReq.Execute().Files[0].Id;

            if(folderId == null)
            {
                File folder = new File
                {
                    Name = e.currentEnvironment.name,
                    MimeType = "application/vnd.google-apps.folder",
                };
                folder.Parents.Add(parentFolder);
                folderId = ds.Files.Create(folder).Execute().Id;
            }
            return folderId;
        }

        public static void UpdateOneEmbed(DriveService ds, Embed embed, EmbedItemField embedHere, string subfolderId, Podio podio, RoutedPodioEvent e)
        {
            File original = GetFileByTitle(ds, embed.Title).Execute();
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

        public static File GetFileByTitle(DriveService ds, string title)
        {
            FilesResource.ListRequest listReq = ds.Files.List();
            listReq.Q = "name='" + title + "'";
            listReq.orderBy = "createdTime";
            return ds.Files.Get(listReq.Execute().Files[0].Id).Execute();
        }
        
    }
}