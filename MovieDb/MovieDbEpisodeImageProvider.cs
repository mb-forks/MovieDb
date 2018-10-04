using MediaBrowser.Model.IO;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Globalization;

namespace MovieDb
{
    public class MovieDbEpisodeImageProvider :
            MovieDbProviderBase,
            IRemoteImageProvider, 
            IHasOrder
    {
        public MovieDbEpisodeImageProvider(IHttpClient httpClient, IServerConfigurationManager configurationManager, IJsonSerializer jsonSerializer, IFileSystem fileSystem, ILocalizationManager localization, ILogManager logManager)
            : base(httpClient, configurationManager, jsonSerializer, fileSystem, localization, logManager)
        {}

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new List<ImageType>
            {
                ImageType.Primary
            };
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var episode = (Episode)item;
            var series = episode.Series;

            var seriesId = series != null ? series.GetProviderId(MetadataProviders.Tmdb) : null;

            var list = new List<RemoteImageInfo>();

            if (string.IsNullOrEmpty(seriesId))
            {
                return list;
            }

            var seasonNumber = episode.ParentIndexNumber;
            var episodeNumber = episode.IndexNumber;

            if (!seasonNumber.HasValue || !episodeNumber.HasValue)
            {
                return list;
            }

            var language = item.GetPreferredMetadataLanguage();

            var response = await GetEpisodeInfo(seriesId, seasonNumber.Value, episodeNumber.Value, language, item.GetPreferredMetadataCountryCode(), cancellationToken).ConfigureAwait(false);

            var tmdbSettings = await MovieDbProvider.Current.GetTmdbSettings(cancellationToken).ConfigureAwait(false);

            var tmdbImageUrl = tmdbSettings.images.GetImageUrl("original");

            list.AddRange(GetPosters(response.images).Select(i => new RemoteImageInfo
            {
                Url = tmdbImageUrl + i.file_path,
                CommunityRating = i.vote_average,
                VoteCount = i.vote_count,
                Width = i.width,
                Height = i.height,
                Language = i.iso_639_1,
                ProviderName = Name,
                Type = ImageType.Primary,
                RatingType = RatingType.Score
            }));

            return list;
        }

        private IEnumerable<Still> GetPosters(Images images)
        {
            return images.stills ?? new List<Still>();
        }


        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return GetResponse(url, cancellationToken);
        }

        public string Name
        {
            get { return "TheMovieDb"; }
        }

        public bool Supports(BaseItem item)
        {
            return item is Episode;
        }

        public int Order
        {
            get
            {
                // After tvdb
                return 1;
            }
        }
    }
}
