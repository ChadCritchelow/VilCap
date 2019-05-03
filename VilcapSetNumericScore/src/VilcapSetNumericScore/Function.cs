using Amazon.Lambda.Core;
using PodioCore;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using PodioCore.Items;
using BrickBridge.Lambda.VilCap;
using newVilcapCopyFileToGoogleDrive;
using Saasafras;
using System.Text.RegularExpressions;
using PodioCore.Services;
using PodioCore.Models.Request;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VilcapSetNumericScore
{
    public class Function
    {
		static LambdaMemoryStore memoryStore = new LambdaMemoryStore();
		public async System.Threading.Tasks.Task FunctionHandler(RoutedPodioEvent e, ILambdaContext context)
		{
			var factory = new AuditedPodioClientFactory(e.solutionId, e.version, e.clientId, e.environmentId);
			var podio = factory.ForClient(e.clientId, e.environmentId);
			Item check = await podio.GetItem(Convert.ToInt32(e.podioEvent.item_id));
			SaasafrasClient saasafrasClient = new SaasafrasClient(System.Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), System.Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
			var dictChild = await saasafrasClient.GetDictionary(e.clientId, e.environmentId, e.solutionId, e.version);
			var dictMaster = await saasafrasClient.GetDictionary("vcadministration", "vcadministration", "vilcap", "0.0");
			var fullNames = new Dictionary<string, string>()
			{
				{"toolkittemplate3", "VC Toolkit Template 3" }
			};
			string lockValue;
			GetIds ids = new GetIds(dictChild, dictMaster, fullNames, e);
			//Make sure to implement by checking to see if Deploy Curriculum has just changed
			//Deploy Curriculum field
			string functionName="VilcapSetNumericScore";
			lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
			try
			{
				if (string.IsNullOrEmpty(lockValue))
				{
					context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
					return;
				}
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
			catch(Exception ex)
			{
				throw ex;
			}
			finally
			{
				await saasafrasClient.UnlockFunction(functionName, check.ItemId.ToString(), lockValue);
			}
		}
	}
}
