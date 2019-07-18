using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Core;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using newVilcapCopyFileToGoogleDrive;
using PodioCore.Items;
using PodioCore.Services;
using PodioCore.Utils.ItemFields;
using Saasafras;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VilcapCreateWorkbook
{
    public class Function
    {
        public async System.Threading.Tasks.Task FunctionHandler( RoutedPodioEvent e, ILambdaContext context )
        {
            var factory = new AuditedPodioClientFactory(e.solutionId, e.version, e.clientId, e.environmentId);
            var podio = factory.ForClient(e.clientId, e.environmentId);
            var item = await podio.GetItem(Convert.ToInt32(e.podioEvent.item_id));
            var saasafrasClient = new SaasafrasClient(Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
            var dictChild = await saasafrasClient.GetDictionary(e.clientId, e.environmentId, e.solutionId, e.version);
            var dictMaster = await saasafrasClient.GetDictionary("vcadministration", "vcadministration", "vilcap", "0.0");
            var ids = new GetIds(dictChild, dictMaster, e.environmentId);
            //GoogleIntegration google = new GoogleIntegration();
            var drive = new DriveService();
            var embedServ = new EmbedService(podio);

            var functionName = "VilcapCreateWorkbook";
            var lockValue = await saasafrasClient.LockFunction(functionName, item.ItemId.ToString());
            try
            {
                if( string.IsNullOrEmpty(lockValue) )
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

                var materials = item.Field<AppItemField>(ids.Get("Content"));
                var pages = new List<File>();
                var workbook = new File();
                foreach( var material in materials.Items )
                {
                    var matEmbed = material.Field<EmbedItemField>(ids.Get("Materials|Link to Material"));
                    foreach( var embed in matEmbed.Embeds )
                    {
                        //pages.Add(google.GetOneFile(drive, embed, e));
                    }
                }
                foreach( var page in pages )
                {
                    //page.
                    //workbook.
                }

            }
            catch( Exception ex )
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
