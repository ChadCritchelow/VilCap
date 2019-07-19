using System;
using System.Linq;
using Amazon.Lambda.Core;
using newVilcapCopyFileToGoogleDrive;
using PodioCore.Items;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using Saasafras;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VilcapCreateCompanyProfile
{
    public class Function
    {
        public async System.Threading.Tasks.Task FunctionHandler( RoutedPodioEvent e, ILambdaContext context )
        {
            #region // Generic Setup //
            var factory = new AuditedPodioClientFactory(e.solutionId, e.version, e.clientId, e.environmentId);
            var podio = factory.ForClient(e.clientId, e.environmentId);
            var submittedApplication = await podio.GetItem(Convert.ToInt32(e.podioEvent.item_id));
            var saasafrasClient = new SaasafrasClient(Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
            var dictChild = await saasafrasClient.GetDictionary(e.clientId, e.environmentId, e.solutionId, e.version);
            var dictMaster = await saasafrasClient.GetDictionary("vcadministration", "vcadministration", "vilcap", "0.0");
            string lockValue;
            var ids = new GetIds(dictChild, dictMaster, e.environmentId);
            var functionName = "VilcapCreateCompanyProfile";
            lockValue = await saasafrasClient.LockFunction(functionName, submittedApplication.ItemId.ToString());
            if( string.IsNullOrEmpty(lockValue) )
            {
                context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {submittedApplication.ItemId}");
                return;
            }
            #endregion
            try
            {
                
                //When an item is updated in Applications:
                var revision = await podio.GetRevisionDifference
                    (
                    Convert.ToInt32(submittedApplication.ItemId),
                    submittedApplication.CurrentRevision.Revision - 1,
                    submittedApplication.CurrentRevision.Revision
                    );
                var firstRevision = revision.First();
                var completionStatus = submittedApplication.Field<CategoryItemField>(ids.Get("Applications|Complete This Application"));
                if( firstRevision.FieldId == completionStatus.FieldId )
                {
                    if( completionStatus.Options.Any() && completionStatus.Options.First().Text == "Submit" )
                    {
                        var companyProfile = new Item();
                        companyProfile.Field<CategoryItemField>(ids.Get("Company Profiles|Selection Process")).OptionText = "New Application";
                        companyProfile.Field<AppItemField>(ids.Get("Company Profiles|Application")).ItemId = submittedApplication.ItemId;

                        #region // Copy Values //
                        try
                        {
                            companyProfile.Field<LocationItemField>(ids.Get("Company Profiles|Location")).Values =
                            submittedApplication.Field<LocationItemField>(ids.Get("Applications|Location")).Values;
                            companyProfile.Field<PhoneItemField>(ids.Get("Company Profiles|Phone")).Values =
                                submittedApplication.Field<PhoneItemField>(ids.Get("Applications|Phone")).Values;
                            companyProfile.Field<EmailItemField>(ids.Get("Company Profiles|Email")).Values =
                                submittedApplication.Field<EmailItemField>(ids.Get("Applications|Email")).Values;
                            companyProfile.Field<DateItemField>(ids.Get("Company Profiles|Company Founding Date")).Values =
                                submittedApplication.Field<DateItemField>(ids.Get("Applications|Company Founding Date ")).Values;

                            var embedField = companyProfile.Field<EmbedItemField>(ids.Get("Company Profiles|Website"));
                            var website = submittedApplication.Field<EmbedItemField>(ids.Get("Applications|Website")).Embeds.FirstOrDefault().ResolvedUrl;
                            website = submittedApplication.Field<EmbedItemField>(ids.Get("Applications|Company Website")) != null
                                ? submittedApplication.Field<EmbedItemField>(ids.Get("Applications|Company Website")).Embeds.FirstOrDefault().ResolvedUrl
                                : submittedApplication.Field<EmbedItemField>(ids.Get("Applications|Website")).Embeds.FirstOrDefault().ResolvedUrl;
                            var em = new Embed { OriginalUrl = website };
                            embedField.Embeds.Append(em);
                        }
                        catch( Exception ex )
                        {
                            context.Logger.LogLine($"!!! Inner Exception: {ex.Message}");
                            throw ex;
                        }
                        #endregion

                        await podio.CreateItem(companyProfile, ids.Get("Company Profiles"), true);
                    }
                }
            }
            catch( Exception ex )
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
