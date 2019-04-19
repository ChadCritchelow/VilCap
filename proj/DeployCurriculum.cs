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
	class DeployCurriculum
	{
		public async System.Threading.Tasks.Task _DeployCurriculum(Item check, ILambdaContext context, Podio podio, GetIds ids)
		{
			//Make sure to implement by checking to see if Deploy Curriculum has just changed
			//Deploy Curriculum field
			var deployField = check.Field<CategoryItemField>(ids.GetFieldId("Admin|Deploy Curriculum"));
			if(deployField.Options.Any()&&deployField.Options.First().Text=="Deploy")
			{
				//item to update:
				Item update = new Item() { ItemId = check.ItemId };
				//WS Batch field
				var wsBatch = update.Field<CategoryItemField>(ids.GetFieldId("Admin|WS Batch"));
				wsBatch.OptionText = "1";
				await podio.UpdateItem(update, true);
			}
		}
	}
}
