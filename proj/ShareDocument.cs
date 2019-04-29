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
	class ShareDocument
	{
		public async System.Threading.Tasks.Task ShareDoc(Item check, ILambdaContext context, Podio podio, GetIds ids)
		{
			//when an item is created in cohort documents:
			var refs = await podio.GetReferringItems(check.ItemId);
			var refFromCompanyProfile = from r in refs
										where r.App.Name == "Company Profiles"
										select r;
			GrantService serv = new GrantService(podio);

			Contact contact = new Contact();
			foreach (var reference in refFromCompanyProfile)
			{
				var item = await podio.GetItem(reference.Items.First().ItemId);
				var email = item.Field<EmailItemField>(ids.GetFieldId("Company Profiles|Email"));

				List<Ref> people = new List<Ref>();
				Ref person = new Ref();
				person.Type = "mail";
				person.Id = email.Value.First().Value;
				people.Add(person);
				var description = check.Field<TextItemField>(ids.GetFieldId("Cohort Documents|Docment Desciption")).Value;
				var message = $"Thank you for sending us your documents {description}.Please follow this link to view your submission.";
 
				await serv.CreateGrant("item", check.ItemId, people,"rate",message);
				if(string.IsNullOrEmpty( description))
				{
					var docName = check.Files[0].Name;
					Item updateMe = new Item() { ItemId = check.ItemId };
					description=updateMe.Field<TextItemField>(ids.GetFieldId("Cohort Documents|Docment Desciption")).Value;
					await podio.UpdateItem(updateMe, true);
				}
			}						  
		}
	}
}
