using Amazon.Lambda.Core;
using PodioCore;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using PodioCore.Items;

namespace newVilcapCopyFileToGoogleDrive
{
	class ShareWithEntrepreneur
	{
		public async System.Threading.Tasks.Task _ShareWithEntrepreneur(Item check, ILambdaContext context, Podio podio, GetIds ids)
		{
			//When an item is created in Entrepreneurs:
			var email = check.Field<EmailItemField>(ids.GetFieldId("Entrepreneurs|Entrepreneur Email")).Value.First().Value;
			var sharedBy = "toolkit@vilcap.com";
			var message = $"Please create an account and tell us about your time at {check.Field<AppItemField>(ids.GetFieldId("Entrepreneurs|Company")).Items.First().Title}";
		}
	}
}
