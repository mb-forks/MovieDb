using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MovieDb
{
    public class MovieDbPersonImageProvider : IRemoteImageProvider, IHasOrder
    {
        private readonly IServerConfigurationManager _config;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IHttpClient _httpClient;

        public MovieDbPersonImageProvider(IServerConfigurationManager config, IJsonSerializer jsonSerializer, IHttpClient httpClient)
        {
            _config = config;
            _jsonSerializer = jsonSerializer;
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
            return item is Person;
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
            var person = (Person)item;
            var id = person.GetProviderId(MetadataProviders.Tmdb);

            if (!string.IsNullOrEmpty(id))
            {
                await MovieDbPersonProvider.Current.EnsurePersonInfo(id, cancellationToken).ConfigureAwait(false);

                var dataFilePath = MovieDbPersonProvider.GetPersonDataFilePath(_config.ApplicationPaths, id);

                var result = _jsonSerializer.DeserializeFromFile<MovieDbPersonProvider.PersonResult>(dataFilePath);

                var images = result.images ?? new MovieDbPersonProvider.Images();

                var tmdbSettings = await MovieDbProvider.Current.GetTmdbSettings(cancellationToken).ConfigureAwait(false);

                var tmdbImageUrl = tmdbSettings.images.GetImageUrl("original");

                return GetImages(images, tmdbImageUrl);
            }

            return new List<RemoteImageInfo>();
        }

        private IEnumerable<RemoteImageInfo> GetImages(MovieDbPersonProvider.Images images, string baseImageUrl)
        {
            var list = new List<RemoteImageInfo>();

            if (images.profiles != null)
            {
                list.AddRange(images.profiles.Select(i => new RemoteImageInfo
                {
                    ProviderName = Name,
                    Type = ImageType.Primary,
                    Width = i.width,
                    Height = i.height,
                    Language = GetLanguage(i),
                    Url = baseImageUrl + i.file_path
                }));
            }

            return list;
        }

        private string GetLanguage(MovieDbPersonProvider.Profile profile)
        {
            return profile.iso_639_1 == null ? null : profile.iso_639_1.ToString();
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
