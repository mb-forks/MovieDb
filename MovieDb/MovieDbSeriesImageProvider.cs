﻿using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
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
    public class MovieDbSeriesImageProvider : IRemoteImageProvider, IHasOrder
    {
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IHttpClient _httpClient;
        private readonly IFileSystem _fileSystem;

        public MovieDbSeriesImageProvider(IJsonSerializer jsonSerializer, IHttpClient httpClient, IFileSystem fileSystem)
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
            return item is Series;
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

            return list;
        }

        /// <summary>
        /// Gets the posters.
        /// </summary>
        /// <param name="images">The images.</param>
        private IEnumerable<MovieDbSeriesProvider.Poster> GetPosters(MovieDbSeriesProvider.Images images)
        {
            return images.posters ?? new List<MovieDbSeriesProvider.Poster>();
        }

        /// <summary>
        /// Gets the backdrops.
        /// </summary>
        /// <param name="images">The images.</param>
        private IEnumerable<MovieDbSeriesProvider.Backdrop> GetBackdrops(MovieDbSeriesProvider.Images images)
        {
            var eligibleBackdrops = images.backdrops == null ? new List<MovieDbSeriesProvider.Backdrop>() :
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
        private async Task<MovieDbSeriesProvider.Images> FetchImages(BaseItem item, string language, string country, IJsonSerializer jsonSerializer,
            CancellationToken cancellationToken)
        {
            var tmdbId = item.GetProviderId(MetadataProviders.Tmdb);

            if (string.IsNullOrEmpty(tmdbId))
            {
                return null;
            }

            await MovieDbSeriesProvider.Current.EnsureSeriesInfo(tmdbId, language, country, cancellationToken).ConfigureAwait(false);

            var path = MovieDbSeriesProvider.Current.GetDataFilePath(tmdbId, language);

            if (!string.IsNullOrEmpty(path))
            {
                var fileInfo = _fileSystem.GetFileInfo(path);

                if (fileInfo.Exists)
                {
                    return jsonSerializer.DeserializeFromFile<MovieDbSeriesProvider.RootObject>(path).images;
                }
            }

            return null;
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
                Url = url
            });
        }
    }
}
