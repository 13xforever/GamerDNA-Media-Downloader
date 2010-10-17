using System;
using System.Threading;
using System.Threading.Tasks;

namespace GamerDnaMediaDownloader
{
	class Program
	{
		static void Main(string[] args)
		{
			var mediaPageListProcessor = new Thread(RefreshMediaPages);
			var mediaPageProcessor = new Thread(GetMediaInfo);
			var mediaRetriever = new Thread(RetrieveMedia);

			mediaPageListProcessor.Start();
			mediaPageProcessor.Start();

			mediaPageListProcessor.Join();
			mediaPageProcessor.Join();

			Log.Info("Press any key to exit...");
			Console.ReadKey();
		}

		private static void RefreshMediaPages()
		{
			Log.Debug("Media page list processor started...");
			foreach (var url in MediaPageGetter.GetMediaPages())
				if (CacheBag.AddNewMediaPage(url))
					Log.Info("Got new media page: {0}", url.AbsoluteUri);
				else
					Log.Warning("Got old media page: {0}", url.AbsoluteUri);
			Log.Debug("Media page list processor finished...");
		}

		private static void GetMediaInfo()
		{
			Log.Debug("Media info getter started...");
			Parallel.ForEach(CacheBag.GetUnprocessedPages(), MediaPageGetter.GetImageInfo);
			Log.Debug("Media info getter finished...");
		}
		private static void RetrieveMedia()
		{
			Log.Debug("Media retriever started...");
			Parallel.ForEach(CacheBag.GetUnsavedMediaInfos(), MediaPageGetter.SaveMedia);
			Log.Debug("Media retriever finished...");
		}
	}
}
