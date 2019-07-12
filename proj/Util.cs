using System.Linq;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;

namespace newVilcapCopyFileToGoogleDrive
{
    class Util
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
    }
}
