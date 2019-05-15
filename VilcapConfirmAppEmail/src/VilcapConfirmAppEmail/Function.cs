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

namespace VilcapConfirmAppEmail
{
	public class Function
	{

		/// <summary>
		/// A simple function that takes a string and does a ToUpper
		/// </summary>
		/// <param name="input"></param>
		/// <param name="context"></param>
		/// <returns></returns>
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
			string functionName = "VilcapConfirmAppEmail";
			lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
			try
			{
				if (string.IsNullOrEmpty(lockValue))
				{
					context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
					return;
				}
				var revision = await podio.GetRevisionDifference
				(
				Convert.ToInt32(check.ItemId),
				check.CurrentRevision.Revision - 1,
				check.CurrentRevision.Revision
				);
				var firstRevision = revision.First();
				var complete = check.Field<CategoryItemField>(ids.GetFieldId("Applications|Complete This Application"));
				if (firstRevision.FieldId == complete.FieldId)
				{
					if (complete.Options.Any() && complete.Options.First().Text == "Submit")
					{
						var recipient = check.Field<EmailItemField>(ids.GetFieldId("Applications|Email")).Value.First().Value;
						//get admin item to get program manager name
						var items = await podio.FilterItems(ids.GetFieldId("Admin"), new FilterOptions() { Limit = 1 });
						var adminItem = await podio.GetItem(items.Items.First().ItemId);
						var fromName = adminItem.Field<ContactItemField>(ids.GetFieldId("Admin|Program Manager")).Contacts.First().Name;
						var subject = "Thank you for submitting your application!";
						var messageBody = $"Thank you for submitting your application to {ids.GetLongName($"{e.environmentId}-FN")}'s Future of Work" +
							" and Learning Program 2019. We will be reviewing your application and following up in the " +
							"coming weeks regarding next steps. If you do have questions, please feel free to email me at" +
							" stephen.wemple@vilcap.com.";
                        context.Logger.LogLine(messageBody);
						GrantService serv = new GrantService(podio);
						List<Ref> people = new List<Ref>();
						Ref person = new Ref();
						person.Type = "mail";
						person.Id = recipient;
						people.Add(person);
						var grant = await serv.CreateGrant("item", check.ItemId, people, "view", messageBody);
                        context.Logger.LogLine("Re-Granted");
                        await serv.RemoveGrant("item", check.ItemId, people.First().Id);
                        context.Logger.LogLine("De-Granted");
                        await serv.CreateGrant("item", check.ItemId, people, messageBody);
                        context.Logger.LogLine("Re-Re-Granted");
                    }
				}
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
