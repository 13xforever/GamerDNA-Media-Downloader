using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlServerCe;
using System.IO;
using System.Xml.Linq;
using GamerDnaMediaDownloaderV2;
using Raven.Client.Embedded;

namespace SqlCeToRavenDbExporter
{
	class Program
	{
		private const string ConnectionString = @"Data Source=LocalCache.sdf;Max Database Size=4000";

		static void Main(string[] args)
		{
			Console.WriteLine("Starting import...");
			var hash = new HashSet<string>();
			using (var sqlCeConnection = new SqlCeConnection(ConnectionString))
			{
				sqlCeConnection.Open();
				using (var store = new EmbeddableDocumentStore { DataDirectory = @"LocalCache", RunInMemory = false })
				{
					store.Initialize();
					using (var sqlCeCommand = new SqlCeCommand("select * from [MediaInfo]", sqlCeConnection))
					using (var reader = sqlCeCommand.ExecuteReader(CommandBehavior.SequentialAccess))
						while (reader.Read())
						{
							var pageUrl = (string)reader["MediaPageUrl"];
							if (!hash.Add(pageUrl))
								Console.WriteLine("Duplicate of " + pageUrl);
							var media = new Media
							            	{
							            		PageUrl = new Uri(pageUrl),
							            		MediaUrl = (reader["MediaUrl"] as string).Return(s => new Uri(s)),
							            		Title = reader["Title"] as string,
							            		Description = reader["Description"] as string,
							            		LocalPath = reader["LocalFilename"] as string,
							            		Processed = (bool)reader["Processed"],
							            		Saved = (bool)reader["Saved"],
							            	};
							using (var session = store.OpenSession())
							{
								session.Store(media);
								session.SaveChanges();
							}
						}
				}

				using (var sqlCeCommand = new SqlCeCommand("select * from [ProcessionStatus]", sqlCeConnection))
				using (var reader = sqlCeCommand.ExecuteReader(CommandBehavior.SequentialAccess))
				{
					if (reader.Read())
					{
						var root = new XElement("settings");
						root.Add(new XAttribute("startPage", (int)reader["LastMediaListPage"]));
						root.Add(new XAttribute("finished", (bool)reader["Finished"]));
						var doc = new XDocument();
						doc.Add(root);

						using (var stream = File.Open("settings.xml", FileMode.Create, FileAccess.Write, FileShare.None))
							doc.Save(stream);
					}

				}

			}
			Console.WriteLine("Done");
		}
	}
}
