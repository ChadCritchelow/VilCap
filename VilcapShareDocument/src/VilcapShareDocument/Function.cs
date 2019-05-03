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

namespace VilcapShareDocument
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
			string functionName = "VilcapShareDocument";
			lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
			try
			{
				if (string.IsNullOrEmpty(lockValue))
				{
					context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
					return;
				}
				//when an item is created in cohort documents:
				var refs = await podio.GetReferringItems(check.ItemId);
				var refFromCompanyProfile = from r in refs
											where r.App.Name == "Company Profiles"
											select r;
				GrantService serv = new GrantService(podio);
				foreach (var reference in refFromCompanyProfile)
				{
					var item = await podio.GetItem(reference.Items.First().ItemId);
					var email = item.Field<EmailItemField>(ids.GetFieldId("Company Profiles|Email"));

					List<Ref> people = new List<Ref>();
					Ref person = new Ref();
					person.Type = "mail";
					person.Id = email.Value.First().Value;
					people.Add(person);
					var description = check.Field<TextItemField>(ids.GetFieldId("Cohort Documents|Docment Desciption")).Value;
					var message = $"Thank you for sending us your documents {description}.Please follow this link to view your submission.";

					await serv.CreateGrant("item", check.ItemId, people, "rate", message);
					if (string.IsNullOrEmpty(description))
					{
						var docName = check.Files[0].Name;
						Item updateMe = new Item() { ItemId = check.ItemId };
						description = updateMe.Field<TextItemField>(ids.GetFieldId("Cohort Documents|Docment Desciption")).Value;
						await podio.UpdateItem(updateMe, true);
					}
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
