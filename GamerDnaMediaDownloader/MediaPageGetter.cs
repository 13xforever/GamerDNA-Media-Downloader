using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;

namespace GamerDnaMediaDownloader
{
	internal static class MediaPageGetter
	{
		internal static IEnumerable<Uri> GetMediaPages(int startPage)
		{
			const string baseUrl = "http://13xforever.gamerdna.com/media/";
			string mediaListUrl = baseUrl;
			if (startPage > 1) mediaListUrl += string.Format("?page={0}", startPage);
			var searchResultElement = new XElement("div");
			while (!string.IsNullOrEmpty(mediaListUrl))
			{
				Log.Info("Processing page {0} of media list", startPage);
				string pageContent = GetPageContent(mediaListUrl);
				const string divListContainer = "<div class=\"corners-center\">";
				int searchResultStart = pageContent.IndexOf(divListContainer);
				searchResultStart = pageContent.IndexOf(divListContainer, searchResultStart + 1);
				if (searchResultStart != -1)
				{
					int searchResultEnd = pageContent.IndexOf("<div class=\"corners-bottom\"", searchResultStart + 1);
					if (searchResultEnd == -1)
						Log.Warning("Can't find the end of xml subtree.");
					else
					{
						var mediaInfos = Enumerable.Empty<XElement>();
						try
						{
							searchResultElement = XElement.Parse(Sanitize(pageContent.Substring(searchResultStart, searchResultEnd - searchResultStart)));
							mediaInfos = searchResultElement.XPathSelectElements("/div[@class='media_list']/div[@class='list_body']/div[@class='media_item_container']/div[@class='media_item']/div[@class='media_wrapper']/a");
						}
						catch (Exception e)
						{
							Log.Error("Error while processing page {0} of user list: {1}", startPage, e.Message);
						}
						foreach (var mediaInfo in mediaInfos)
							yield return mediaInfo.Attribute("href").Return(attr => new Uri(attr.Value));
					}
				}
				XElement nextPageLink = searchResultElement.XPathSelectElement("//a[@class='next_page']");
				mediaListUrl = nextPageLink == null ? null : string.Format("{0}?page={1}", baseUrl, ++startPage);
			}
		}

		internal static Media GetImageUrl(Uri mediaPageUrl)
		{
			string pageContent = GetPageContent(mediaPageUrl.AbsoluteUri);
			int searchResultStart = pageContent.IndexOf("<div id=\"bd_shell\">");
			if (searchResultStart == -1)
			{
				Log.Error("Can't find content for page {0}", mediaPageUrl.AbsoluteUri);
				return null;
			}

			int searchResultEnd = pageContent.IndexOf("<!-- End Container -->", searchResultStart + 1);
			if (searchResultEnd == -1)
			{
				Log.Warning("Can't find the end of xml subtree.");
				return null;
			}
			
			try
			{
				var resultElement = XElement.Parse(Sanitize(pageContent.Substring(searchResultStart, searchResultEnd - searchResultStart)));
				var title = resultElement.XPathSelectElement("/div[@class='two_col_shell content_hd']/div[@class='col_primary']/div[@class='source']/div[@class='info']/h1");
				var description = resultElement.XPathSelectElement("/div[@class='two_col_shell content_hd']/div[@class='col_primary']/p");
				var mediaUrl = resultElement.XPathSelectElement("/div[@class='content_bd']/a");
				return new Media
				       	{
				       		MediaPageUrl = mediaPageUrl,
				       		MediaUrl = mediaUrl.Return(url => url.Attribute("href")).Return(attr => new Uri(attr.Value)),
				       		Title = title.Value.Trim(),
				       		Description = description.Return(e =>
				       		                                 	{
				       		                                 		var r = e.CreateReader();
				       		                                 		r.MoveToContent();
				       		                                 		return r.ReadInnerXml();
				       		                                 	}),
				       	};
			}
			catch (Exception e)
			{
				Log.Error("Error while processing page {0}: {1}", mediaPageUrl.AbsoluteUri, e.Message);
				return null;
			}
		}

		private static string GetPageContent(string url)
		{
			string pageContent = "";
			WebRequest request = WebRequest.Create(url);
			WebResponse response = request.GetResponse();

			if (response != null)
				using (Stream stream = response.GetResponseStream())
					if (stream != null)
						using (var reader = new StreamReader(stream, Encoding.UTF8))
							pageContent = reader.ReadToEnd();
			return pageContent;
		}

		private static string Sanitize(string content)
		{
			var sb = new StringBuilder(content);
			sb.Replace("<br>", "<br />");
			sb.Replace("&network", "&amp;network");
			sb.Replace("&url", "&amp;url");
			sb.Replace("&title", "&amp;title");
			sb.Replace("&laquo;", "&#xbb;");
			sb.Replace("&raquo;", "&#xab;");
			sb.Replace("&hellip;", "&#x2026;");
			return sb.ToString();
		}
	}
}
