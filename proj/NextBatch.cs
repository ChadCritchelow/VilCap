using Amazon.Lambda.Core;
using PodioCore;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using PodioCore.Items;
using System.Text.RegularExpressions;

namespace newVilcapCopyFileToGoogleDrive
{
	class NextBatch
	{
		public async System.Threading.Tasks.Task GoToNextBatch(Item check, ILambdaContext context, Podio podio, GetIds ids, Comment commentToCheck)
		{
			var commentPieces = commentToCheck.Value.Split(" ");
			var type = commentPieces[0];
			var batch = commentPieces[2];
			bool isNum = Regex.IsMatch(batch, @"^\d+$");
			Item updateMe = new Item() { ItemId = check.ItemId };
			int currentBatch;
			if (isNum)
			{
				try
				{
					int.TryParse(batch, out currentBatch);
				}
				catch(Exception ex)
				{
					context.Logger.LogLine("It seems batch number could not be found");
					throw ex;
				}
				switch(type)
				{
					case "WS":
						context.Logger.LogLine("Type==WS");
						var wsBatchField = updateMe.Field<CategoryItemField>(ids.GetFieldId("Admin|WS Batch"));
						wsBatchField.OptionText = (currentBatch++).ToString();
						break;
					case "TL":
						context.Logger.LogLine("Type==TL");
						var tlBatchField = updateMe.Field<CategoryItemField>(ids.GetFieldId("Admin|TL Batch"));
						tlBatchField.OptionText = (currentBatch++).ToString();
						break;
				}
				await podio.UpdateItem(updateMe, true);
			}
		}
	}
}
