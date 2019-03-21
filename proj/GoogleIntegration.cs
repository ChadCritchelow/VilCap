﻿using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using PodioCore;
using PodioCore.Exceptions;
using PodioCore.Models;
using PodioCore.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace newVilcapCopyFileToGoogleDrive
{
	class GoogleIntegration
	{
		public string GetSubfolderId(DriveService ds, Podio podio, RoutedPodioEvent e, string parentFolder)
		{
			try
			{
				//Console.WriteLine($"{e.podioEvent.item_id} - EnvID: {e.environmentId}");
				FilesResource.ListRequest listReq = ds.Files.List();
				listReq.Q = "name='" + e.environmentId + "'";
				string folderId = "";

				if (listReq.Execute().Files.Any())
				{
					folderId = listReq.Execute().Files[0].Id;
				}
				else if (folderId == "")
				{
					File folder = new File
					{
						Name = e.environmentId,
						MimeType = "application/vnd.google-apps.folder",
					};
                    File F = new File();
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
				File original = GetFileByTitle(ds, id, e);
				if (original.Parents == null)
					original.Parents = new List<string>();
				Console.WriteLine($"{e.podioEvent.item_id} -Old File ID: {original.Id}, Name: {original.Name}");
				original.Parents.Clear();
				original.Parents.Add(subfolderId);
				original.Name = e.environmentId + " " + original.Name;

				File clone = ds.Files.Copy(original, id).Execute();

				await System.Threading.Tasks.Task.Run(() =>
				{
					Permission p = new Permission
					{
						Role = "writer",
						Type = "anyone"
					};
					new PermissionsResource.CreateRequest(ds, p, clone.Id).Execute();
				});

				await System.Threading.Tasks.Task.Run(() =>
				{
					PodioCore.Services.EmbedService embedServ = new EmbedService(podio);;

					Console.WriteLine($"{e.podioEvent.item_id} - CloneID: {clone.Id}");
					var req = ds.Files.Get(clone.Id);
					req.Fields = "webViewLink";
					clone = req.Execute();
					//runs 130x approx
					int waitSeconds = 5;
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
						for (int i = 0; i < waitSeconds; i++)
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

		public string GetDriveId(string url, RoutedPodioEvent e)
		{
			try
			{
				string[] substr = url.Split(new char[] { '=', '/', '?' });
				foreach (string s in substr)
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
	}
}
