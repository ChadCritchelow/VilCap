using Amazon.Lambda.Core;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using System;
using System.Linq;
using PodioCore.Items;
using BrickBridge.Lambda.VilCap;
using newVilcapCopyFileToGoogleDrive;
using Saasafras;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VilcapCreateCompanyProfile
{
    public class Function
    {
		public async System.Threading.Tasks.Task FunctionHandler(RoutedPodioEvent e, ILambdaContext context)
		{
			var factory = new AuditedPodioClientFactory(e.solutionId, e.version, e.clientId, e.environmentId);
			var podio = factory.ForClient(e.clientId, e.environmentId);
			Item submittedApplication = await podio.GetItem(Convert.ToInt32(e.podioEvent.item_id));
			SaasafrasClient saasafrasClient = new SaasafrasClient(Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
			var dictChild = await saasafrasClient.GetDictionary(e.clientId, e.environmentId, e.solutionId, e.version);
			var dictMaster = await saasafrasClient.GetDictionary("vcadministration", "vcadministration", "vilcap", "0.0");
			string lockValue;
			GetIds ids = new GetIds(dictChild, dictMaster, e.environmentId);
			//Make sure to implement by checking to see if Deploy Curriculum has just changed
			//Deploy Curriculum field
			string functionName="VilcapCreateCompanyProfile";
			lockValue = await saasafrasClient.LockFunction(functionName, submittedApplication.ItemId.ToString());
			try
			{
				if (string.IsNullOrEmpty(lockValue))
				{
					context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {submittedApplication.ItemId}");
					return;
				}
				//When an item is updated in Applications:
				var revision = await podio.GetRevisionDifference
					(
					Convert.ToInt32(submittedApplication.ItemId),
                    submittedApplication.CurrentRevision.Revision - 1,
                    submittedApplication.CurrentRevision.Revision
					);
				var firstRevision = revision.First();
				var completionStatus = submittedApplication.Field<CategoryItemField>(ids.GetFieldId("Applications|Complete This Application"));
				if (firstRevision.FieldId == completionStatus.FieldId)
				{
					if (completionStatus.Options.Any() && completionStatus.Options.First().Text == "Submit")
					{
						Item companyProfile = new Item();
                        companyProfile.Field<CategoryItemField>(ids.GetFieldId("Company Profiles|Selection Process")).OptionText = "New Application";
                        companyProfile.Field<AppItemField>(ids.GetFieldId("Company Profiles|Application")).ItemId = submittedApplication.ItemId;

                        #region >>> Copy Values >>>
                        try
                        {
                            companyProfile.Field<LocationItemField>(ids.GetFieldId("Company Profiles|Location")).Values =
                            submittedApplication.Field<LocationItemField>(ids.GetFieldId("Applications|Location")).Values;
                            companyProfile.Field<PhoneItemField>(ids.GetFieldId("Company Profiles|Phone")).Values =
                                submittedApplication.Field<PhoneItemField>(ids.GetFieldId("Applications|Phone")).Values;
                            companyProfile.Field<EmailItemField>(ids.GetFieldId("Company Profiles|Email")).Values =
                                submittedApplication.Field<EmailItemField>(ids.GetFieldId("Applications|Email")).Values;
                            companyProfile.Field<DateItemField>(ids.GetFieldId("Company Profiles|Company Founding Date")).Values =
                                submittedApplication.Field<DateItemField>(ids.GetFieldId("Applications|Company Founding Date ")).Values; // " "
                            companyProfile.Field<TextItemField>(ids.GetFieldId("Company Profiles|Twitter Handle")).Value =
                                submittedApplication.Field<TextItemField>(ids.GetFieldId("Applications|Twitter Handle")).Value;
                            companyProfile.Field<TextItemField>(ids.GetFieldId("Company Profiles|LinkedIn Page")).Value =
                                submittedApplication.Field<TextItemField>(ids.GetFieldId("Applications|LinkedIn Page")).Value;
                            companyProfile.Field<TextItemField>(ids.GetFieldId("Company Profiles|Facebook Page")).Value =
                                submittedApplication.Field<TextItemField>(ids.GetFieldId("Applications|Facebook Page")).Value;

                            var embedField = companyProfile.Field<EmbedItemField>(ids.GetFieldId("Company Profiles|Website")); 
                            var website = submittedApplication.Field<EmbedItemField>(ids.GetFieldId("Applications|Website")).Embeds.FirstOrDefault().ResolvedUrl;
                            Embed em = new Embed
                            {
                                OriginalUrl = website
                            };
                            embedField.Embeds.Append(em);
                        }
                        catch (Exception ex)
                        {
                            context.Logger.LogLine($"!!! Inner Exception: {ex.Message}");
                            throw ex;
                        }
                        #endregion

                        await podio.CreateItem(companyProfile, ids.GetFieldId("Company Profiles"), true);
					}
				}
			}
			catch(Exception ex)
			{
                context.Logger.LogLine($"!!! Outer Exception: {ex.Message}");
                throw ex;
            }
			finally
			{
				await saasafrasClient.UnlockFunction(functionName, submittedApplication.ItemId.ToString(), lockValue);
			}
		}
	}
}
