using System;
using System.Linq;
using Amazon.Lambda.Core;
using newVilcapCopyFileToGoogleDrive;
using PodioCore.Comments;
using PodioCore.Items;
using PodioCore.Models.Request;
using PodioCore.Utils.ItemFields;
using Saasafras;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer( typeof( Amazon.Lambda.Serialization.Json.JsonSerializer ) )]

namespace VilcapConfirmAppEmail
{

    /// <summary>
    /// Application|item.update -->
    /// Sends a thank-you on completion
    /// </summary>
    public class Function
    {

        public async System.Threading.Tasks.Task FunctionHandler( RoutedPodioEvent e, ILambdaContext context )
        {
            var factory = new AuditedPodioClientFactory( e.solutionId, e.version, e.clientId, e.environmentId );
            var podio = factory.ForClient( e.clientId, e.environmentId );
            var check = await podio.GetItem( Convert.ToInt32( e.podioEvent.item_id ) );
            var saasafrasClient = new SaasafrasClient( System.Environment.GetEnvironmentVariable( "BBC_SERVICE_URL" ), System.Environment.GetEnvironmentVariable( "BBC_SERVICE_API_KEY" ) );
            var dictChild = await saasafrasClient.GetDictionary( e.clientId, e.environmentId, e.solutionId, e.version );
            var dictMaster = await saasafrasClient.GetDictionary( "vcadministration", "vcadministration", "vilcap", "0.0" );

            string lockValue;
            var ids = new GetIds( dictChild, dictMaster, e.environmentId );
            //Make sure to implement by checking to see if Deploy Curriculum has just changed
            //Deploy Curriculum field
            var functionName = "VilcapConfirmAppEmail";
            lockValue = await saasafrasClient.LockFunction( functionName, check.ItemId.ToString() );
            try
            {
                if( string.IsNullOrEmpty( lockValue ) )
                {
                    context.Logger.LogLine( $"Failed to acquire lock for {functionName} and id {check.ItemId}" );
                    return;
                }
                var revision = await podio.GetRevisionDifference
                (
                Convert.ToInt32( check.ItemId ),
                check.CurrentRevision.Revision - 1,
                check.CurrentRevision.Revision
                );
                var firstRevision = revision.First();
                var complete = check.Field<CategoryItemField>( ids.GetFieldId( "Applications|Complete This Application" ) );
                if( firstRevision.FieldId == complete.FieldId )
                {
                    if( complete.Options.Any() && complete.Options.First().Text == "Submit" )
                    {
                        var recipient = check.Field<EmailItemField>( ids.GetFieldId( "Applications|Email" ) ).Value.First().Value;
                        //get admin item to get program manager name
                        var items = await podio.FilterItems( ids.GetFieldId( "Admin" ), new FilterOptions() { Limit = 1 } );
                        var adminItem = await podio.GetItem( items.Items.First().ItemId );
                        var fromName = adminItem.Field<ContactItemField>( ids.GetFieldId( "Admin|Program Manager" ) ).Contacts.First().Name;
                        //var subject = "Thank you for submitting your application!";
                        var messageBody = $"Thank you for submitting your application to {ids.GetLongName( $"{e.environmentId}-FN" )}'s Future of Work" +
                            " and Learning Program 2019. We will be reviewing your application and following up in the " +
                            "coming weeks regarding next steps. If you do have questions, please feel free to email me at " +
                        "[stephen.wemple@vilcap.com](mailto: stephen.wemple@vilcap.com)";
                        var comm = new CommentService( podio );
                        await comm.AddCommentToObject( "item", check.ItemId, messageBody );
                    }
                }
            }
            catch( Exception ex )
            {
                throw ex;
            }
            finally
            {
                await saasafrasClient.UnlockFunction( functionName, check.ItemId.ToString(), lockValue );
            }
        }
    }
}
