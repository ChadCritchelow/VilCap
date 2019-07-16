using System.Linq;
using PodioCore;
using PodioCore.Models;
using PodioCore.Services;
using PodioCore.Utils.ItemFields;
using newVilcapCopyFileToGoogleDrive;


namespace newVilcapCopyFileToGoogleDrive
{
    public class Util
    {
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

        public static async void GetBatch(WorkshopModules2 workshop, newVilcapCopyFileToGoogleDrive vilcap)
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
