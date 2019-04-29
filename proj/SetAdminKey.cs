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
	class AdminKey
	{
		public async System.Threading.Tasks.Task SetAdminKey(Item check, ILambdaContext context, Podio podio, GetIds ids)
		{
			//When a new item in Admin is created:
			Item updateMe = new Item() { ItemId = check.ItemId };

			//Field to update:
			var searchKey = updateMe.Field<TextItemField>(ids.GetFieldId("Admin|Search Key"));
			searchKey.Value = "vilcapadmin";
			await podio.UpdateItem(updateMe,true);
		}
	}
}

