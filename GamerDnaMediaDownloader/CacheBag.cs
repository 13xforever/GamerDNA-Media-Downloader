using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace GamerDnaMediaDownloader
{
	internal static class CacheBag
	{
		private static readonly LocalCacheEntities Cache = new LocalCacheEntities();

		public static bool AddNewMediaPage(Uri url)
		{
			if (ContainsMediaPage(url)) return false;

			var mediaInfo = new MediaInfo {MediaPageUrl = url.AbsoluteUri};
			Cache.AddToMediaInfoes(mediaInfo);
			Cache.SaveChanges();
			return true;
		}

		public static bool ContainsMediaPage(Uri url)
		{
			int records = Cache.MediaInfoes.Count(i => i.MediaPageUrl == url.AbsoluteUri);
			if (records > 1) throw new InvalidOperationException("Corrupted cache");
			return records == 1;
		}

		public static bool IsProcessionFinished()
		{
			var status = new LocalCacheEntities().ProcessionStatus.First();
			return status.Finished;
		}

		public static int GetStartingPage()
		{
			var status = new LocalCacheEntities().ProcessionStatus.First();
			return status.LastMediaListPage;
		}

		public static void UpdateProcessionStatus(int currentPage)
		{
			var status = new LocalCacheEntities().ProcessionStatus.First();
			status.LastMediaListPage = currentPage;
			Cache.SaveChanges();
		}

		public static void MarkProcessionStatusAsFinished()
		{
			var status = new LocalCacheEntities().ProcessionStatus.First();
			status.Finished = true;
			Cache.SaveChanges();
		}

		public static IEnumerable<MediaInfo> GetUnprocessedPages()
		{
			var entities = new LocalCacheEntities();
			var unprocessdPages = entities.MediaInfoes.Where(info => !info.Processed);
			foreach (var page in unprocessdPages)
			{
				yield return page;
				entities.SaveChanges();
			}
		}

		public static IEnumerable<MediaInfo> GetUnsavedMediaInfos()
		{
			var entities = new LocalCacheEntities();
			var unprocessdPages = entities.MediaInfoes.Where(info => info.Processed && !info.Saved);
			foreach (var page in unprocessdPages)
			{
				yield return page;
				entities.SaveChanges();
			}
		}
	}
}