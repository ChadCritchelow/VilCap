using System.Linq;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;

namespace newVilcapCopyFileToGoogleDrive
{
    public class Util
    {
        public static void Init( RoutedPodioEvent e )
        {
            //var factory = new AuditedPodioClientFactory(e.solutionId, e.version, e.clientId, e.environmentId);
            //var podio = factory.ForClient(e.clientId, e.environmentId);
            //var item = await podio.GetItem(Convert.ToInt32(e.podioEvent.item_id));
            //var saasafrasClient = new SaasafrasClient(Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
            //var dictChild = await saasafrasClient.GetDictionary(e.clientId, e.environmentId, e.solutionId, e.version);
            //var dictMaster = await saasafrasClient.GetDictionary("vcadministration", "vcadministration", "vilcap", "0.0");
        }
        public static void MatchFields(Item to, Item from, GetIds ids)
        {
            foreach( var textField in to.Fields.OfType<TextItemField>() )
            {
                var cField = to.Field<TextItemField>(ids.GetFieldId("Workshop Modules|" + textField.Label));
                cField.Value = textField.Value;
            }
            foreach( var numericField in from.Fields.OfType<NumericItemField>() )
            {
                var cField = to.Field<NumericItemField>(ids.GetFieldId("Workshop Modules|" + numericField.Label));
                cField.Value = numericField.Value;
            }
            foreach( var numericField in from.Fields.OfType<NumericItemField>() )
            {
                var cField = to.Field<NumericItemField>(ids.GetFieldId("Workshop Modules|" + numericField.Label));
                cField.Value = numericField.Value;
            }

        }

        public static void GetBatch( WorkshopModules2 workshop, newVilcapCopyFileToGoogleDrive vilcap )
        {
            //var batchId = ids.GetFieldId("Admin|WS Batch");
            //var batch = check.Field<CategoryItemField>(batchId).Options.First().Text;
            //int.TryParse(batch, out batchNum);

            //var startDateId = ids.GetFieldId("Admin|Program Start Date");
            //var startDate = new DateTime(check.Field<DateItemField>(startDateId).Start.Value.Ticks);

            //var packageId = ids.GetFieldId("Admin|Curriculum Package");
            //var package = check.Field<CategoryItemField>(packageId).Options.First().Text;
            //context.Logger.LogLine($"Curriculum Batch '{batch}'");

            //var viewServ = new ViewService(vilcap.podio);
            ////context.Logger.LogLine("Got View Service");
            //var views = await viewServ.GetViews(WorkshopModules2.MASTER_CONTENT_APP);
            //var view = from v in views
            //           where v.Name == $"{package} Batch {batchNum}"
            //           select v;
            //context.Logger.LogLine($"Got View '{package}'");

            //var op = new FilterOptions { Filters = view.First().Filters };
            //context.Logger.LogLine($"Filter: ({op.Filters.ToStringOrNull()}) ");
            //op.SortBy = SORT_ID_FIELD; // fieldId of Package Sequence (num) from Content_Curation_
            //op.SortDesc = false;
            //op.Limit = LIMIT;
        }
    }
}
