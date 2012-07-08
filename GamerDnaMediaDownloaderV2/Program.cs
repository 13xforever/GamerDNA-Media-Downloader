using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Raven.Client;
using Raven.Client.Linq;
using Raven.Client.Embedded;

namespace GamerDnaMediaDownloaderV2
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.SetBufferSize(150, 9999);
			using (var store = new EmbeddableDocumentStore { DataDirectory = "LocalCache" })
			{
				store.Initialize();
				Settings.Load();
				bool mediaPageUpdaterIsRunning = true;
				bool mediaInfoUpdaterIsRunning = true;
				var mediaPageListProcessor = new Thread(() => RefreshMediaPages(store));
				var mediaPageProcessor = new Thread(()=>GetMediaInfo(store, ref mediaPageUpdaterIsRunning));
				var mediaRetriever = new Thread(()=>RetrieveMedia(store, ref mediaInfoUpdaterIsRunning));

				mediaPageListProcessor.Start();
				mediaPageProcessor.Start();
				mediaRetriever.Start();

				mediaPageListProcessor.Join();
				mediaPageUpdaterIsRunning = false;
				mediaPageProcessor.Join();
				mediaInfoUpdaterIsRunning = false;
				mediaRetriever.Join();
				Settings.Save();
			}
			Log.Info("Press any key to exit...");
			Console.ReadKey();
		}

		private static void RefreshMediaPages(IDocumentStore store)
		{
			Log.Debug("Media page list processor started...");
			foreach (var url in MediaPageGetter.GetMediaPages(store))
			{
				using (var session = store.OpenSession())
				{
					if (session.Query<Media>().Any(media => media.PageUrl == url)) continue;

					session.Store(new Media {PageUrl = url});
					session.SaveChanges();
					Log.Info("Got new media page: {0}", url.AbsoluteUri);
				}
			}
			Log.Debug("Media page list processor finished...");
		}

		private static void GetMediaInfo(IDocumentStore store, ref bool higherLevelProcessorIsRunning)
		{
			Log.Debug("Media info getter started...");
			RavenQueryStatistics stats;
			do
			{
				using (var session = store.OpenSession())
				{
					var list = session.Query<Media>().Statistics(out stats).Where(media => !media.Processed).Take(100).ToList();
					Parallel.ForEach(list, MediaPageGetter.GetImageInfo);
					session.SaveChanges();
				}
				if (stats.TotalResults == 0 && higherLevelProcessorIsRunning)
					Thread.Sleep(100);
			} while (stats.TotalResults > 0 || stats.IsStale);
			Log.Debug("Media info getter finished...");
		}

		private static void RetrieveMedia(IDocumentStore store, ref bool higherLevelProcessorIsRunning)
		{
			Log.Debug("Media retriever started...");
			RavenQueryStatistics unsavedStats;
			bool unprocessed;
			do
			{
				using (var session = store.OpenSession())
				{
					var list = session.Query<Media>().Statistics(out unsavedStats).Where(media => media.Processed && !media.Saved).Take(100).ToList();
					Parallel.ForEach(list, MediaPageGetter.SaveMedia);
					session.SaveChanges();
					unprocessed = session.Query<Media>().Any(media => !media.Processed);
					if ((unsavedStats.TotalResults == 0 || !unprocessed) && higherLevelProcessorIsRunning)
						Thread.Sleep(100);
				}
			} while (unsavedStats.TotalResults > 0 || unprocessed || unsavedStats.IsStale);
			Log.Debug("Media retriever finished...");
		}
	}
}
