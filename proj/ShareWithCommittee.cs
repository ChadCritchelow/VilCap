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
	class ShareWithCommittee
	{
		public async System.Threading.Tasks.Task _ShareWithCommittee(Item check, ILambdaContext context, Podio podio, GetIds ids)
		{
			//When an item is created in Diligence and Selection:
			var e = check.Field<EmailItemField>(ids.GetFieldId("Diligence and Selection|Shared Email"));
			var m = "Please rate this application";
			var email = e;
			Item updateMe = new Item() { ItemId = check.ItemId };
			updateMe.Field<CategoryItemField>(ids.GetFieldId("Diligence and Selection|Status")).OptionText = "Not Scored";
			await podio.UpdateItem(updateMe, true);
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
