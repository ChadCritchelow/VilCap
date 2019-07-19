using System.Linq;
using System.Threading.Tasks;
using PodioCore;
using PodioCore.Items;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
//using idk = System.Threading.Tasks.Task<PodioCore.Models.Item>;

namespace newVilcapCopyFileToGoogleDrive
{
    public static class Materials
    {
        public static async Task<Item> Copy( Item from, GetIds ids, Podio podio, int appId )
        {
            var clone = new Item();

            //--- Text ---//
            if( from.Field<TextItemField>(ids.Get("VC Administration|Material Curation|Title")).Value != null )
            {
                clone.Field<TextItemField>(ids.Get("Materials|Title")).Value =
                    from.Field<TextItemField>(ids.Get("VC Administration|Material Curation|Title")).Value;
            }
            if( from.Field<TextItemField>(ids.Get("VC Administration|Material Curation|Description")).Value != null )
            {
                clone.Field<TextItemField>(ids.Get("Materials|Description")).Value =
                from.Field<TextItemField>(ids.Get("VC Administration|Material Curation|Description")).Value;
            }
            //--- Category ---//
            if( from.Field<CategoryItemField>(ids.Get("VC Administration|Material Curation|Type")).Options.First() != null )
            {
                clone.Field<CategoryItemField>(ids.Get("Materials|Type")).OptionText =
                    from.Field<CategoryItemField>(ids.Get("VC Administration|Material Curation|Type")).Options.First().Text;
            }

            var createdItemId = await podio.CreateItem(clone, appId, true);
            return await podio.GetFullItem(createdItemId);
        }
    }
}
