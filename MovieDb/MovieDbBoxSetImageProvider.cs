using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MovieDb
{
    class MovieDbBoxSetImageProvider : IRemoteImageProvider, IHasOrder
    {
        private readonly IHttpClient _httpClient;

        public MovieDbBoxSetImageProvider(IHttpClient httpClient)
        {
            _httpClient = httpClient;
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
            return item is BoxSet;
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
            var tmdbId = item.GetProviderId(MetadataProviders.Tmdb);

            if (!string.IsNullOrEmpty(tmdbId))
            {
                var mainResult = await MovieDbBoxSetProvider.Current.GetMovieDbResult(tmdbId, null, null, cancellationToken).ConfigureAwait(false);

                if (mainResult != null)
                {
                    var tmdbSettings = await MovieDbProvider.Current.GetTmdbSettings(cancellationToken).ConfigureAwait(false);

                    var tmdbImageUrl = tmdbSettings.images.GetImageUrl("original");

                    return GetImages(mainResult, tmdbImageUrl);
                }
            }

            return new List<RemoteImageInfo>();
        }

        private IEnumerable<RemoteImageInfo> GetImages(MovieDbBoxSetProvider.RootObject obj, string baseUrl)
        {
            var list = new List<RemoteImageInfo>();

            var images = obj.images ?? new MovieDbBoxSetProvider.Images();

            list.AddRange(GetPosters(images).Select(i => new RemoteImageInfo
            {
                Url = baseUrl + i.file_path,
                CommunityRating = i.vote_average,
                VoteCount = i.vote_count,
                Width = i.width,
                Height = i.height,
                Language = i.iso_639_1,
                ProviderName = Name,
                Type = ImageType.Primary,
                RatingType = RatingType.Score
            }));

            list.AddRange(GetBackdrops(images).Select(i => new RemoteImageInfo
            {
                Url = baseUrl + i.file_path,
                CommunityRating = i.vote_average,
                VoteCount = i.vote_count,
                Width = i.width,
                Height = i.height,
                ProviderName = Name,
                Type = ImageType.Backdrop,
                RatingType = RatingType.Score
            }));

            return list;
        }

        /// <summary>
        /// Gets the posters.
        /// </summary>
        /// <param name="images">The images.</param>
        /// <returns>IEnumerable{MovieDbProvider.Poster}.</returns>
        private IEnumerable<MovieDbBoxSetProvider.Poster> GetPosters(MovieDbBoxSetProvider.Images images)
        {
            return images.posters ?? new List<MovieDbBoxSetProvider.Poster>();
        }

        /// <summary>
        /// Gets the backdrops.
        /// </summary>
        /// <param name="images">The images.</param>
        /// <returns>IEnumerable{MovieDbProvider.Backdrop}.</returns>
        private IEnumerable<MovieDbBoxSetProvider.Backdrop> GetBackdrops(MovieDbBoxSetProvider.Images images)
        {
            var eligibleBackdrops = images.backdrops == null ? new List<MovieDbBoxSetProvider.Backdrop>() :
                images.backdrops;

            return eligibleBackdrops.OrderByDescending(i => i.vote_average)
                .ThenByDescending(i => i.vote_count);
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
