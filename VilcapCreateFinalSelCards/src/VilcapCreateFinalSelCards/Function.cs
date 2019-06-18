using Amazon.Lambda.Core;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using System;
using System.Linq;
using PodioCore.Items;
using BrickBridge.Lambda.VilCap;
using newVilcapCopyFileToGoogleDrive;
using Saasafras;
using PodioCore.Services;
using System.Collections.Generic;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VilcapCreateFinalSelCards
{
    /// <summary>
    /// {ESO}|Company Profiles|Selection Process --> ITEM.UPDATE --> "Semi-Finalist" OR "Finalist"
    /// </summary>
    public class Function
    {
		public async System.Threading.Tasks.Task FunctionHandler(RoutedPodioEvent e, ILambdaContext context)
		{
			var factory = new AuditedPodioClientFactory(e.solutionId, e.version, e.clientId, e.environmentId);
			var podio = factory.ForClient(e.clientId, e.environmentId);
			Item check = await podio.GetItem(Convert.ToInt32(e.podioEvent.item_id));
			SaasafrasClient saasafrasClient = new SaasafrasClient(Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
			var dictChild = await saasafrasClient.GetDictionary(e.clientId, e.environmentId, e.solutionId, e.version);
			var dictMaster = await saasafrasClient.GetDictionary("vcadministration", "vcadministration", "vilcap", "0.0");

			string lockValue;
			GetIds ids = new GetIds(dictChild, dictMaster, e.environmentId);
			//Deploy Curriculum field
			string functionName="VilcapCreateFinalSelCards";
			lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
			try
			{
				if (string.IsNullOrEmpty(lockValue))
				{
					context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
					return;
				}
				//When an item is updated in Company Profiles and:

				var revision = await podio.GetRevisionDifference
				(
				Convert.ToInt32(check.ItemId),
				check.CurrentRevision.Revision - 1,
				check.CurrentRevision.Revision
				);
				var firstRevision = revision.First();
				var selectionProcess = check.Field<CategoryItemField>(ids.GetFieldId("Company Profiles|Selection Process"));
				if (firstRevision.FieldId == selectionProcess.FieldId)
				{
					if (selectionProcess.Options.Any() &&
						(selectionProcess.Options.First().Text == "Semi-Finalist" ||
						selectionProcess.Options.First().Text == "Finalist"))
					{
						//Get view "Program Support":
						ViewService viewServ = new ViewService(podio);
						ItemService filterServ = new ItemService(podio);
						string selectionRound = "";
						string viewName = "";
						switch (selectionProcess.Options.First().Text)
						{
							case "Finalist":

								viewName = "Selection Committee - Final";
								selectionRound = "Final Round";
								break;
							case "Semi-Finalist":
								viewName = "Selection Committee - Semi Final";
								selectionRound = "Semi-Final Round";
								break;
						}
						var views=await viewServ.GetViews(ids.GetFieldId("Program Support"));
						var view = from v in views
									where v.Name == viewName
									select v;
						var viewItems = await filterServ.FilterItemsByView(ids.GetFieldId("Program Support"), int.Parse(view.First().ViewId), limit: 500);
						foreach (var item in viewItems.Items)
						{
							Item create = new Item();
							create.Field<CategoryItemField>(ids.GetFieldId("Diligence and Selection|Selection Round")).OptionText
								= selectionRound;
							create.Field<AppItemField>(ids.GetFieldId("Diligence and Selection|Company")).ItemId = check.ItemId;
							create.Field<AppItemField>(ids.GetFieldId("Diligence and Selection|Selection Comittee Member")).ItemId = item.ItemId;
							create.Field<EmailItemField>(ids.GetFieldId("Diligence and Selection|Shared Email")).Value =
								item.Field<EmailItemField>(ids.GetFieldId("Program Support|Email")).Value;
							var card = await podio.CreateItem(create, ids.GetFieldId("Diligence and Selection"), true);

                            GrantService serv = new GrantService(podio);
                            //Create Email:
                            var recipient = item.Field<EmailItemField>(ids.GetFieldId("Program Support|Email")).Value.FirstOrDefault().Value;
                            var orgName = create.Field<TextItemField>(ids.GetFieldId("Diligence and Selection|Company")).Value;
                            var m = $"Please Rate the {selectionRound} Company: {orgName}. \n" +
                            "You can view all of your Podio items by at: <https://podio.com/vilcapcom/organization/grants>.\n " +
                            "Please bookmark this link before changing your email notification settings.";

                            //Send email
                            var email = recipient;

                            List<Ref> people = new List<Ref>();
                            Ref person = new Ref
                            {
                                Type = "mail",
                                Id = email
                            };
                            people.Add(person);

                            await serv.CreateGrant("item", check.ItemId, people, "rate", m);
                            // await serv.CreateGrant("item", card, people, "rate", m);
                        }
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
