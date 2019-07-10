using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PodioCore;
using PodioCore.Items;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
//using idk = System.Threading.Tasks.Task<PodioCore.Models.Item>;

namespace newVilcapCopyFileToGoogleDrive
{
    class Materials
    {
        public static async Task<Item> Copy(Item from, Item to, GetIds ids, Podio podio, int appId)
        {
            var clone = new Item();

            //--- Text ---//
            if ( from.Field<TextItemField>(ids.GetFieldId("VC Administration|Material Curation|Title")).Value != null)
                clone.Field<TextItemField>(ids.GetFieldId("Materials|Title")).Value = 
                    from.Field<TextItemField>(ids.GetFieldId("VC Administration|Material Curation|Title")).Value;
            if( from.Field<TextItemField>(ids.GetFieldId("VC Administration|Material Curation|Description")).Value != null )
                clone.Field<TextItemField>(ids.GetFieldId("Materials|Description")).Value =
                from.Field<TextItemField>(ids.GetFieldId("VC Administration|Material Curation|Description")).Value;
            //--- Category ---//
            if( from.Field<CategoryItemField>(ids.GetFieldId("VC Administration|Material Curation|Type")).Options.First() != null )
                clone.Field<CategoryItemField>(ids.GetFieldId("Materials|Type")).OptionText =
                    from.Field<CategoryItemField>(ids.GetFieldId("VC Administration|Material Curation|Type")).Options.First().Text;

            var createdItemId = await podio.CreateItem(clone, appId, true);
            return await podio.GetFullItem(createdItemId);
        }    
    }
}
