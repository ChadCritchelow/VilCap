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

namespace VilcapShareWithEnt
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
			string lockValue;
			GetIds ids = new GetIds(dictChild, dictMaster, e.environmentId);
			//Make sure to implement by checking to see if Deploy Curriculum has just changed
			//Deploy Curriculum field
			string functionName="VilcapShareWithEnt";
			lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
			try
			{
				if (string.IsNullOrEmpty(lockValue))
				{
					context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
					return;
				}
				//When an item is created in Entrepreneurs:
				var email = check.Field<EmailItemField>(ids.GetFieldId("Entrepreneurs|Entrepreneur Email")).Value.First().Value;
				var m = $"Please create an account and tell us about your time at {check.Field<AppItemField>(ids.GetFieldId("Entrepreneurs|Company")).Items.First().Title}";
				GrantService serv = new GrantService(podio);
				//Send email


				List<Ref> people = new List<Ref>();
				Ref person = new Ref();
				person.Type = "mail";
				person.Id = email;
				people.Add(person);
				var message = m;

				await serv.CreateGrant("item", check.ItemId, people, "rate", message);
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
