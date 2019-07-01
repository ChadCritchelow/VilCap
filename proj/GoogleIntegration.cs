using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using PodioCore;
using PodioCore.Exceptions;
using PodioCore.Models;
using PodioCore.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Task = System.Threading.Tasks.Task;
using Microsoft.IdentityModel.Tokens;
//using PdfSharp;


namespace newVilcapCopyFileToGoogleDrive

{
    public class GoogleIntegration
	{
        // Google Drive : DriveService
        

        public string GetSubfolderId(DriveService ds, Podio podio, RoutedPodioEvent e, string parentFolder)
		{
			try
			{
				//Console.WriteLine($"{e.podioEvent.item_id} - EnvID: {e.environmentId}");
				var listReq = ds.Files.List();
				listReq.Q = "name='" + e.environmentId + "'";
				var folderId = "";

				if (listReq.Execute().Files.Any())
				{
					folderId = listReq.Execute().Files[0].Id;
				}
				else if (folderId == "")
				{
					var folder = new File
					{
						Name = e.environmentId,
						MimeType = "application/vnd.google-apps.folder",
					};
                    var F = new File();
                    //F.Name = e.environmentId; // test
                    //F.MimeType = "application/vnd.google-apps.folder"; // test
                    folder.Parents.Add(parentFolder);
					var request = ds.Files.Create(folder);
					request.Fields = "id";

					folderId = request.Execute().Id;
				}
				return folderId;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"ERROR: {e.podioEvent.item_id} - {ex.Message} - {ex.StackTrace} - {ex.InnerException}");
				return null;
			}
		}

		public async System.Threading.Tasks.Task UpdateOneEmbed(DriveService ds, Embed embed, List<Embed> embeds, string subfolderId, Podio podio, RoutedPodioEvent e)
		{
			try
			{
				//Console.WriteLine($"{e.podioEvent.item_id} - Old Embed Link (resolved): {embed.ResolvedUrl}");
				var id = GetDriveId(embed.OriginalUrl, e);
				var original = GetFileByTitle(ds, id, e);
				if (original.Parents == null)
					original.Parents = new List<string>();
				Console.WriteLine($"{e.podioEvent.item_id} -Old File ID: {original.Id}, Name: {original.Name}");
				original.Parents.Clear();
				original.Parents.Add(subfolderId);
				original.Name = e.environmentId + " " + original.Name;

				var clone = ds.Files.Copy(original, id).Execute();

				await Task.Run(() =>
				{
					var p = new Permission
					{
						Role = "writer",
						Type = "anyone"
					};
					new PermissionsResource.CreateRequest(ds, p, clone.Id).Execute();
				});

				await Task.Run(() =>
				{
					var embedServ = new EmbedService(podio);;

					Console.WriteLine($"{e.podioEvent.item_id} - CloneID: {clone.Id}");
					var req = ds.Files.Get(clone.Id);
					req.Fields = "webViewLink";
					clone = req.Execute();
					//runs 130x approx
					var waitSeconds = 5;
					CallPodio:
					Embed em;
					try
					{
						em = embedServ.AddAnEmbed(clone.WebViewLink).Result;
					}
					catch (PodioUnavailableException ex)
					{
						Console.WriteLine($"{ex.Message}");
						Console.WriteLine($"Trying again in {waitSeconds} seconds.");
						for (var i = 0; i < waitSeconds; i++)
						{
							System.Threading.Thread.Sleep(1000);
							Console.WriteLine(".");
						}
						waitSeconds = waitSeconds * 2;
						goto CallPodio;
					}
					//Console.WriteLine($"{e.podioEvent.item_id} - WebViewLink: {clone.WebViewLink}");
					embeds.Add(em);
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine($"{e.podioEvent.item_id} - {ex.Message} - {ex.StackTrace} - {ex.InnerException}");
			}
		}

        public File GetOneFile(DriveService ds, Embed embed, RoutedPodioEvent e)
        {
            try
            {
                var id = GetDriveId(embed.OriginalUrl, e);
                var original = GetFileByTitle(ds, id, e);
                return original;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{e.podioEvent.item_id} - {ex.Message} - {ex.StackTrace} - {ex.InnerException}");
                return null;
            }
        }

        public void AppendOneFile(DriveService ds, RoutedPodioEvent e, File addMe, File book)
        {
            //var reader = PdfSharp.Pdf.IO.PdfReader.Open()
            try
            {
                var export = new FilesResource.ExportRequest(ds, addMe.Id, "application/pdf").Execute();
                //var merged = new File();
                
                //var pdf = new PdfSharp.Pdf.PdfDocument();
                var result = new FilesResource.UpdateRequest(ds, addMe, book.Id).Execute();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{e.podioEvent.item_id} - {ex.Message} - {ex.StackTrace} - {ex.InnerException}");
            }
        }

        public string GetDriveId(string url, RoutedPodioEvent e)
		{
			try
			{
				var substr = url.Split(new char[] { '=', '/', '?' });
				foreach (var s in substr)
				{
					if (s.Length == 44 || s.Length == 33)
					{
						//Console.WriteLine($"{e.podioEvent.item_id} - Found ID: {s} from url: {url}");
						return s;
					}
				}
				Console.WriteLine($"{e.podioEvent.item_id} - Could not find ID for url: {url}");
				return null;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"{e.podioEvent.item_id} - {ex.Message} - {ex.StackTrace} - {ex.InnerException}");
				return null;
			}
		}

		public File GetFileByTitle(DriveService ds, string id, RoutedPodioEvent e)
		{
			try
			{
				var request = ds.Files.Get(id);
				request.Fields = "parents, name";
				var file = request.Execute();
				return file;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"{e.podioEvent.item_id} - {ex.Message} - {ex.StackTrace} - {ex.InnerException}");
				return null;
			}
		}

        // Gmail : 


        /// <summary>
        /// Sends an email.
        /// </summary>
        public void SendEmail(GmailService service, string _userId, string subject, string body, string from, string to, string fromAlias = "", string toAlias = "")
        {
            //_userId = "me";
            Console.WriteLine($"--- Starting SendEmail with userId {_userId}");
            var content =
                $"MIME - Version: 1.0\r\n" +
                $"Subject: {subject}\r\n" +
                $"From: {fromAlias}<{from}>\r\n" +
                $"To: {toAlias}<{to}>\r\n" +
                $"Content - Type: text / plain; charset = \"UTF-8\" \r\n" +
                $"\r\n" +
                $"{body}";

            Console.WriteLine($"--- content: {content}");
            content = Base64UrlEncoder.Encode(content.ToString());
            Console.WriteLine($"--- content (encoded): {content}");
            try
            {
                var message = new Message
                {
                    Raw = content
                };
                Console.WriteLine($"--- messageId: {message.Id}");

                //UsersResource.MessagesResource.SendRequest request = service.Users.Messages.Send(_userId)
                var result = service.Users.Messages.Send(message, _userId).Execute();
                
                Console.WriteLine($"--- result raw: {result.Raw}");
            }
            catch (Exception e)
            {
                Console.WriteLine("An error occurred: " + e.Message);
            }

        }
    }
}
