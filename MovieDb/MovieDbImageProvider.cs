using System.Globalization;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.IO;

namespace MovieDb
{
    class MovieDbImageProvider : IRemoteImageProvider, IHasOrder
    {
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IHttpClient _httpClient;
        private readonly IFileSystem _fileSystem;

        public MovieDbImageProvider(IJsonSerializer jsonSerializer, IHttpClient httpClient, IFileSystem fileSystem)
        {
            _jsonSerializer = jsonSerializer;
            _httpClient = httpClient;
            _fileSystem = fileSystem;
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
            return item is Movie || item is MusicVideo || item is Trailer;
        }

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new List<ImageType>
            {
                ImageType.Primary,
                ImageType.Backdrop
            };
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var list = new List<RemoteImageInfo>();

            var results = await FetchImages(item, null, null, _jsonSerializer, cancellationToken).ConfigureAwait(false);

            if (results == null)
            {
                return list;
            }

            var tmdbSettings = await MovieDbProvider.Current.GetTmdbSettings(cancellationToken).ConfigureAwait(false);

            var tmdbImageUrl = tmdbSettings.images.GetImageUrl("original");

            var supportedImages = GetSupportedImages(item).ToList();

            if (supportedImages.Contains(ImageType.Primary))
            {
                list.AddRange(GetPosters(results).Select(i => new RemoteImageInfo
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
            }

            if (supportedImages.Contains(ImageType.Backdrop))
            {
                list.AddRange(GetBackdrops(results).Select(i => new RemoteImageInfo
                {
                    Url = tmdbImageUrl + i.file_path,
                    CommunityRating = i.vote_average,
                    VoteCount = i.vote_count,
                    Width = i.width,
                    Height = i.height,
                    ProviderName = Name,
                    Type = ImageType.Backdrop,
                    RatingType = RatingType.Score
                }));
            }

            return list;
        }

        /// <summary>
        /// Gets the posters.
        /// </summary>
        /// <param name="images">The images.</param>
        /// <returns>IEnumerable{MovieDbProvider.Poster}.</returns>
        private IEnumerable<MovieDbProvider.Poster> GetPosters(MovieDbProvider.Images images)
        {
            return images.posters ?? new List<MovieDbProvider.Poster>();
        }

        /// <summary>
        /// Gets the backdrops.
        /// </summary>
        /// <param name="images">The images.</param>
        /// <returns>IEnumerable{MovieDbProvider.Backdrop}.</returns>
        private IEnumerable<MovieDbProvider.Backdrop> GetBackdrops(MovieDbProvider.Images images)
        {
            var eligibleBackdrops = images.backdrops == null ? new List<MovieDbProvider.Backdrop>() :
                images.backdrops;

            return eligibleBackdrops.OrderByDescending(i => i.vote_average)
                .ThenByDescending(i => i.vote_count);
        }

        /// <summary>
        /// Fetches the images.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="language">The language.</param>
        /// <param name="jsonSerializer">The json serializer.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{MovieImages}.</returns>
        private async Task<MovieDbProvider.Images> FetchImages(BaseItem item, string language, string preferredMetadataCountry, IJsonSerializer jsonSerializer, CancellationToken cancellationToken)
        {
            var tmdbId = item.GetProviderId(MetadataProviders.Tmdb);
            MovieDbProvider.CompleteMovieData movieInfo;

            if (string.IsNullOrWhiteSpace(tmdbId))
            {
                var imdbId = item.GetProviderId(MetadataProviders.Imdb);
                if (!string.IsNullOrWhiteSpace(imdbId))
                {
                    movieInfo = await MovieDbProvider.Current.FetchMainResult(imdbId, false, language, preferredMetadataCountry, cancellationToken).ConfigureAwait(false);
                    if (movieInfo != null)
                    {
                        return movieInfo.images;
                    }
                }

                return null;
            }

            movieInfo = await MovieDbProvider.Current.EnsureMovieInfo(tmdbId, language, preferredMetadataCountry, cancellationToken).ConfigureAwait(false);

            if (movieInfo != null)
            {
                return movieInfo.images;
            }

            return null;
        }

        public int Order
        {
            get { return 0; }
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClient.GetResponse(new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = url
            });
        }
    }
}
