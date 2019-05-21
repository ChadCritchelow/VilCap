using Amazon.Lambda.Core;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using System;
using System.Linq;
using PodioCore.Items;
using BrickBridge.Lambda.VilCap;
using newVilcapCopyFileToGoogleDrive;
using Saasafras;
using PodioCore.Services;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using System.Collections.Generic;
using Google.Apis.Drive.v3.Data;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VilcapCreateWorkbook
{
    public class Function
    {

        static LambdaMemoryStore memoryStore = new LambdaMemoryStore();
        public async System.Threading.Tasks.Task FunctionHandler(RoutedPodioEvent e, ILambdaContext context)
        {
            var factory = new AuditedPodioClientFactory(e.solutionId, e.version, e.clientId, e.environmentId);
            var podio = factory.ForClient(e.clientId, e.environmentId);
            Item item = await podio.GetItem(Convert.ToInt32(e.podioEvent.item_id));
            SaasafrasClient saasafrasClient = new SaasafrasClient(Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
            var dictChild = await saasafrasClient.GetDictionary(e.clientId, e.environmentId, e.solutionId, e.version);
            var dictMaster = await saasafrasClient.GetDictionary("vcadministration", "vcadministration", "vilcap", "0.0");
            GetIds ids = new GetIds(dictChild, dictMaster, e.environmentId);
            //GoogleIntegration google = new GoogleIntegration();
            DriveService drive = new DriveService();
            EmbedService embedServ = new EmbedService(podio);

            string functionName = "VilcapCreateWorkbook";
            string lockValue = await saasafrasClient.LockFunction(functionName, item.ItemId.ToString());
            try
            {
                if (string.IsNullOrEmpty(lockValue))
                {
                    context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {item.ItemId}");
                    return;
                }
                var revision = await podio.GetRevisionDifference
                (
                    Convert.ToInt32(item.ItemId),
                    item.CurrentRevision.Revision - 1,
                    item.CurrentRevision.Revision
                );
                var firstRevision = revision.First();

                var materials = item.Field<AppItemField>(ids.GetFieldId("Content"));
                var pages = new List<File>();
                var workbook = new File();
                foreach (Item material in materials.Items)
                {
                    var matEmbed = material.Field<EmbedItemField>(ids.GetFieldId("Materials|Link to Material"));
                    foreach (Embed embed in matEmbed.Embeds)
                    {
                        //pages.Add(google.GetOneFile(drive, embed, e));
                    }
                }
                foreach (File page in pages)
                {
                    //page.
                    //workbook.
                }

            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                await saasafrasClient.UnlockFunction(functionName, item.ItemId.ToString(), lockValue);
            }
        }
    }
}
