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

namespace VilcapShareAppWithApplicant
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
			string functionName = "VilcapShareAppWithApplicant";
			lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
			try
			{
				if (string.IsNullOrEmpty(lockValue))
				{
					context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
					return;
				}

				var fieldIdToSearch = ids.GetFieldId("Applications");
				var filterValue = "vilcapadmin";
				var filter = new Dictionary<int, object>
							{
								{ fieldIdToSearch, filterValue }
							};
				FilterOptions newOptions = new FilterOptions
				{
					Filters = filter,
					Offset = 500
				};
				context.Logger.LogLine("Checking for duplicates");

				var items = await podio.FilterItems(ids.GetFieldId("Admin"), newOptions);
				Item AdminOptionToCheck = await podio.GetItem(items.Items.First().ItemId);

				GrantService serv = new GrantService(podio);
				//Create Email:
				var recipient = check.Field<EmailItemField>(ids.GetFieldId("Applications|Email")).Value.First().Value;
				var orgName = AdminOptionToCheck.Field<TextItemField>(ids.GetFieldId("Admin|Organization Name")).Value;
				var m = $"Invitation to Complete Your Application with {orgName}" +
				"This application will automatically save as you work on it.To access an in-progress";

				//Send email
				var email = recipient;

				List<Ref> people = new List<Ref>();
				Ref person = new Ref();
				person.Type = "mail";
				person.Id = email;
				people.Add(person);
				var message = m;

				await serv.CreateGrant("item", check.ItemId, people, "rate", message);

				Item updateMe = new Item() { ItemId = check.ItemId };
				updateMe.Field<CategoryItemField>(ids.GetFieldId("Applications|Application Status")).OptionText = "New Application";
				await podio.UpdateItem(updateMe, true);
			}
			catch (Exception ex)
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
