using Amazon.Lambda.Core;
using PodioCore;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using PodioCore.Items;

namespace newVilcapCopyFileToGoogleDrive
{
	class ShareWithCommittee
	{
		public async System.Threading.Tasks.Task _ShareWithCommittee(Item check, ILambdaContext context, Podio podio, GetIds ids)
		{
			//When an item is created in Diligence and Selection:
			var email = check.Field<EmailItemField>(ids.GetFieldId("Diligence and Selection|Shared Email"));
			var sharedBy = "toolkit@vilcap.com";
			var message = "Please rate this application";
			Item updateMe = new Item() { ItemId = check.ItemId };
			updateMe.Field<CategoryItemField>(ids.GetFieldId("Diligence and Selection|Status")).OptionText = "Not Scored";
			await podio.UpdateItem(updateMe, true);
		}
	}
}
