using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;

namespace MovieDb
{
    public class MovieDbSeasonImageProvider : IRemoteImageProvider, IHasOrder
    {
        private readonly IHttpClient _httpClient;
        private readonly MovieDbSeasonProvider _seasonProvider;

        public MovieDbSeasonImageProvider(IJsonSerializer jsonSerializer, IServerConfigurationManager configurationManager, IHttpClient httpClient, IFileSystem fileSystem, ILocalizationManager localization, ILogManager logManager)
        {
            _httpClient = httpClient;
            _seasonProvider = new MovieDbSeasonProvider(httpClient, configurationManager, fileSystem, localization, jsonSerializer, logManager);
        }

        public string Name
        {
            get { return ProviderName; }
        }

        public static string ProviderName
        {
            get { return "TheMovieDb"; }
        }

        public bool Supports(BaseItem item)
        {
            return item is Season;
        }

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new List<ImageType>
            {
                ImageType.Primary
            };
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var list = new List<RemoteImageInfo>();

            var season = (Season)item;
            var series = season.Series;

            string seriesTmdbId;
            series.ProviderIds.TryGetValue(MetadataProviders.Tmdb.ToString(), out seriesTmdbId);


            if (!string.IsNullOrWhiteSpace(seriesTmdbId) && season.IndexNumber.HasValue)
            {
                try
                {
                    var seasonInfo = await _seasonProvider.GetSeasonInfo(seriesTmdbId, season.IndexNumber.Value, series.GetPreferredMetadataLanguage(), null, cancellationToken)
                        .ConfigureAwait(false);

                    var tmdbSettings = await MovieDbProvider.Current.GetTmdbSettings(cancellationToken).ConfigureAwait(false);
                    var tmdbImageUrl = tmdbSettings.images.GetImageUrl("original");

                    if (seasonInfo?.images?.posters != null)
                    {
                        foreach (var image in seasonInfo.images.posters)
                        {
                            var remoteImage = new RemoteImageInfo
                            {
                                Url             = tmdbImageUrl + image.file_path,
                                CommunityRating = image.vote_average,
                                VoteCount       = image.vote_count,
                                Width           = image.width,
                                Height          = image.height,
                                Language        = image.iso_639_1,
                                ProviderName    = Name,
                                Type            = ImageType.Primary,
                                RatingType      = RatingType.Score
                            };

                            list.Add(remoteImage);
                        }
                    }

                }
                catch (HttpException)
                {
                    //_logger.Error("No metadata found for {0}", seasonNumber.Value);

                    // ignore
                }
            }

            return list;
        }

        public int Order
        {
            get
            {
                // After tvdb and fanart
                return 2;
            }
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClient.GetResponse(new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url               = url
            });
        }
    }
}
