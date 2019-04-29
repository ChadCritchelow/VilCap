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
	class NumericScore
	{
		public async System.Threading.Tasks.Task SetNumericScore(Item check, ILambdaContext context, Podio podio, GetIds ids)
		{
			//when an item in diligence and selection is updated:
			var revision = await podio.GetRevisionDifference
			(
				Convert.ToInt32(check.ItemId),
				check.CurrentRevision.Revision - 1,
				check.CurrentRevision.Revision
			);
			var firstRevision = revision.First();
			var selectionProcess = check.Field<CategoryItemField>(ids.GetFieldId("Diligence and Selection|Score"));
			if (firstRevision.FieldId == selectionProcess.FieldId)
			{
				var selectionRound = check.Field<CategoryItemField>(ids.GetFieldId("Diligence and Selection|Selection Round")).Options.First().Text;
				string semiFinalScore = "";
				string finalScore = "";
				string status = "Complete";
				Item updateMe = new Item() { ItemId = check.ItemId };
				switch (selectionRound)
				{
					case "Semi-Final Round":
						semiFinalScore = check.Field<CategoryItemField>(ids.GetFieldId("Diligence and Selection|Score")).Options.First().Text;
						finalScore = null;
						updateMe.Field<NumericItemField>(ids.GetFieldId("Diligence and Selection|Score (Numeric) Semi-Final")).Value =
							int.Parse(semiFinalScore);
						updateMe.Field<NumericItemField>(ids.GetFieldId("Diligence and Selection|Score (Numeric) Final")).Status = null;
						break;
					case "Final Round":
						semiFinalScore = null;
						finalScore = check.Field<CategoryItemField>(ids.GetFieldId("Diligence and Selection|Score")).Options.First().Text;
						updateMe.Field<NumericItemField>(ids.GetFieldId("Diligence and Selection|Score (Numeric) Semi-Final")).Status = null;
						updateMe.Field<NumericItemField>(ids.GetFieldId("Diligence and Selection|Score (Numeric) Final")).Value = int.Parse(finalScore);
						break;
				}
				updateMe.Field<CategoryItemField>(ids.GetFieldId("Diligence and Selection|Status")).OptionText = status;
				await podio.UpdateItem(updateMe, true);

			}
		}
	}
}
