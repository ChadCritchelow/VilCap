using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Core;
using newVilcapCopyFileToGoogleDrive;
using PodioCore.Items;
using PodioCore.Models.Request;
using PodioCore.Services;
using PodioCore.Utils.ItemFields;
using Saasafras;
using Task = System.Threading.Tasks.Task;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VilcapCreateView
{
    public class CreateView
    {
        private static readonly int LIMIT = 30;
        private static readonly string functionName = "VilcapCreateView";
        private static readonly int CONTENT_CURATION = 21310273;
        private static readonly int MASTER_SCHEDULE = 21310276;
        private static readonly int CONTENT_TEST_ITEM = 589;
        private static readonly int CONTENT_PACKAGE_FIELD = 184034632;
        private static readonly string CONTENT_SORT_ID_FIELD = "188139930";



        public async Task FunctionHandler( RoutedPodioEvent e, ILambdaContext context )
        {
            var podio = new AuditedPodioClientFactory(e.solutionId, e.version, e.clientId, e.environmentId).ForClient(e.clientId, e.environmentId);
            var saasafrasClient = new SaasafrasClient(Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
            var dictChild = await saasafrasClient.GetDictionary(e.clientId, e.environmentId, e.solutionId, e.version);
            var dictMaster = await saasafrasClient.GetDictionary("vcadministration", "vcadministration", "vilcap", "0.0");
            var ids = new GetIds(dictChild, dictMaster, e.environmentId);
            var lockValue = await saasafrasClient.LockFunction(functionName, "!" + functionName);
            if( string.IsNullOrEmpty(lockValue) )
            {
                context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {"!" + functionName}");
                return;
            }
            try
            {
                var test = await podio.GetAppItem(CONTENT_CURATION, CONTENT_TEST_ITEM);
                var packageField = test.Field<CategoryItemField>(CONTENT_PACKAGE_FIELD);
                var packages = packageField.Options;
                var viewServ = new ViewService(podio);
                var views = await viewServ.GetViews(CONTENT_CURATION);
                foreach( var package in packages )
                {
                    var match = from v in views
                                where v.Name.Split(" Batch")[0] == $"{package.Text}"
                                where v.Name.Split(" Batch").Count() == 2
                                select v;
                    if( match.Any() ) { continue; }
                    var filteredItems = await podio.FilterItems(CONTENT_CURATION, new FilterOptions
                    {
                        Filters = new Dictionary<string, object> { { CONTENT_PACKAGE_FIELD.ToString(), new int[] { package.Id.Value } } }
                    });
                    var batchNum = 1;
                    for( var c = 1 ; c <= filteredItems.Total ; c += LIMIT )
                    {
                        var request = new ViewCreateUpdateRequest // ~ var op = new FilterOptions
                        {
                            Name = $"入 {package.Text} Batch {batchNum}",
                            Private = true,
                            SortDesc = false,
                            SortBy = CONTENT_SORT_ID_FIELD,
                            Filters = new Dictionary<string, object>
                            {
                                { CONTENT_PACKAGE_FIELD.ToString(), new int[]{ package.Id.Value } },
                                { CONTENT_SORT_ID_FIELD.ToString(), new { from = c, to = c + LIMIT} }
                            }
                        };
                        var newViewId = await viewServ.CreateView(CONTENT_CURATION, request);
                        context.Logger.LogLine($"Created View with Id {newViewId}");
                        batchNum++;
                    }
                }
            }
            catch( Exception ex )
            {
                throw ex;
            }
            finally
            {
                await saasafrasClient.UnlockFunction(functionName, "!" + functionName, lockValue);
            }
        }
    }
}
