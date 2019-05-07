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

namespace VilcapCreateSetDefaults
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
			string functionName = "VilcapCreateSetDefaults";
			lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
			try
			{
				if (string.IsNullOrEmpty(lockValue))
				{
					context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
					return;
				}
				//On Creation of a Company Profile:

				//get referenced items from applications app:
				Item checkApp = new Item();
				var itemRef = check.Field<AppItemField>(ids.GetFieldId("Company Profiles|Application"));
				
				Item updateApp = new Item() { ItemId = itemRef.Items.First().ItemId };
				updateApp.Field<CategoryItemField>(ids.GetFieldId("Applications|Application Status")).OptionText = "Company Profile Created";
				await podio.UpdateItem(updateApp, true);
				checkApp = await podio.GetItem(itemRef.Items.First().ItemId);
				context.Logger.LogLine($"Item ID in foreach: {itemRef.Items.First().ItemId}");

				context.Logger.LogLine($"Application ITem ID: {checkApp.ItemId}");
				Item updateCompanyProfile = new Item() { ItemId = check.ItemId };
				updateCompanyProfile.Field<PhoneItemField>(ids.GetFieldId("Company Profiles|Phone")).Value =
					checkApp.Field<PhoneItemField>(ids.GetFieldId("Applications|Phone")).Value;
				updateCompanyProfile.Field<EmailItemField>(ids.GetFieldId("Company Profiles|Email")).Value =
					checkApp.Field<EmailItemField>(ids.GetFieldId("Applications|Email")).Value;
				if (checkApp.Field<DateItemField>(ids.GetFieldId("Applications|Company Founding Date ")).Start != null)
				{
					updateCompanyProfile.Field<DateItemField>(ids.GetFieldId("Company Profiles|Company Founding Date")).Start =
						checkApp.Field<DateItemField>(ids.GetFieldId("Applications|Company Founding Date ")).Start;
				}
				var emails = checkApp.Field<EmailItemField>(ids.GetFieldId("Applications|Email")).Value;
				foreach (var email in emails)
				{
					Item entrepreneur = new Item();
					entrepreneur.Field<AppItemField>(ids.GetFieldId("Entrepreneurs|Company")).ItemId = check.ItemId;
					List<EmailPhoneFieldResult> emailList = new List<EmailPhoneFieldResult>();
					emailList.Add(email);
					entrepreneur.Field<EmailItemField>(ids.GetFieldId("Entrepreneurs|Entrepreneur Email")).Value =emailList;
					await podio.CreateItem(entrepreneur, ids.GetFieldId("Entrepreneurs"), true);
				}
				await podio.UpdateItem(updateCompanyProfile, true);
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
