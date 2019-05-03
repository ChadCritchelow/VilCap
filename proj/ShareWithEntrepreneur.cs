using Amazon.Lambda.Core;
using PodioCore;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using PodioCore.Items;
using PodioCore.Services;

namespace newVilcapCopyFileToGoogleDrive
{
	class ShareWithEntrepreneur
	{
		public async System.Threading.Tasks.Task _ShareWithEntrepreneur(Item check, ILambdaContext context, Podio podio, GetIds ids)
		{
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
	}
}
