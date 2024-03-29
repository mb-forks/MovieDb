﻿using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.IO;
using MediaBrowser.Common;

using MediaBrowser.Controller.IO;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Extensions;

namespace MovieDb
{
    /// <summary>
    /// Class MovieDbProvider
    /// </summary>
    public class MovieDbProvider : IRemoteMetadataProvider<Movie, MovieInfo>, IHasOrder
    {
        internal static MovieDbProvider Current { get; private set; }

        private readonly IJsonSerializer _jsonSerializer;
        private readonly IHttpClient _httpClient;
        private readonly IFileSystem _fileSystem;
        private readonly IServerConfigurationManager _configurationManager;
        private readonly ILogger _logger;
        private readonly ILocalizationManager _localization;
        private readonly ILibraryManager _libraryManager;
        private readonly IApplicationHost _appHost;

        private readonly CultureInfo _usCulture = new CultureInfo("en-US");

        public MovieDbProvider(IJsonSerializer jsonSerializer, IHttpClient httpClient, IFileSystem fileSystem, IServerConfigurationManager configurationManager, ILogger logger, ILocalizationManager localization, ILibraryManager libraryManager, IApplicationHost appHost)
        {
            _jsonSerializer = jsonSerializer;
            _httpClient = httpClient;
            _fileSystem = fileSystem;
            _configurationManager = configurationManager;
            _logger = logger;
            _localization = localization;
            _libraryManager = libraryManager;
            _appHost = appHost;
            Current = this;
        }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
        {
            return GetMovieSearchResults(searchInfo, cancellationToken);
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetMovieSearchResults(ItemLookupInfo searchInfo, CancellationToken cancellationToken)
        {
            var tmdbId = searchInfo.GetProviderId(MetadataProviders.Tmdb);

            if (!string.IsNullOrEmpty(tmdbId))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var obj = await EnsureMovieInfo(tmdbId, searchInfo.MetadataLanguage, searchInfo.MetadataCountryCode, cancellationToken).ConfigureAwait(false);

                if (obj == null)
                {
                    return new List<RemoteSearchResult>();
                }

                var tmdbSettings = await GetTmdbSettings(cancellationToken).ConfigureAwait(false);

                var tmdbImageUrl = tmdbSettings.images.GetImageUrl("original");

                var remoteResult = new RemoteSearchResult
                {
                    Name = obj.GetTitle(),
                    SearchProviderName = Name,
                    ImageUrl = string.IsNullOrWhiteSpace(obj.poster_path) ? null : tmdbImageUrl + obj.poster_path
                };

                if (!string.IsNullOrWhiteSpace(obj.release_date))
                {
                    DateTimeOffset r;

                    // These dates are always in this exact format
                    if (DateTimeOffset.TryParse(obj.release_date, _usCulture, DateTimeStyles.None, out r))
                    {
                        remoteResult.PremiereDate = r.ToUniversalTime();
                        remoteResult.ProductionYear = remoteResult.PremiereDate.Value.Year;
                    }
                }

                remoteResult.SetProviderId(MetadataProviders.Tmdb, obj.id.ToString(_usCulture));

                if (!string.IsNullOrWhiteSpace(obj.imdb_id))
                {
                    remoteResult.SetProviderId(MetadataProviders.Imdb, obj.imdb_id);
                }

                return new[] { remoteResult };
            }

            return await new MovieDbSearch(_logger, _jsonSerializer, _libraryManager).GetMovieSearchResults(searchInfo, cancellationToken).ConfigureAwait(false);
        }

        public Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
        {
            return GetItemMetadata<Movie>(info, cancellationToken);
        }

        public Task<MetadataResult<T>> GetItemMetadata<T>(ItemLookupInfo id, CancellationToken cancellationToken)
            where T : BaseItem, new()
        {
            var movieDb = new GenericMovieDbInfo<T>(_logger, _jsonSerializer, _libraryManager, _fileSystem);

            return movieDb.GetMetadata(id, cancellationToken);
        }

        public string Name
        {
            get { return "TheMovieDb"; }
        }

        /// <summary>
        /// The _TMDB settings task
        /// </summary>
        private TmdbSettingsResult _tmdbSettings;

        /// <summary>
        /// Gets the TMDB settings.
        /// </summary>
        /// <returns>Task{TmdbSettingsResult}.</returns>
        internal async Task<TmdbSettingsResult> GetTmdbSettings(CancellationToken cancellationToken)
        {
            if (_tmdbSettings != null)
            {
                return _tmdbSettings;
            }

            using (var response = await GetMovieDbResponse(new HttpRequestOptions
            {
                Url = string.Format(TmdbConfigUrl, ApiKey),
                CancellationToken = cancellationToken,
                AcceptHeader = AcceptHeader

            }).ConfigureAwait(false))
            {
                using (var json = response.Content)
                {
                    _tmdbSettings = await _jsonSerializer.DeserializeFromStreamAsync<TmdbSettingsResult>(json).ConfigureAwait(false);

                    return _tmdbSettings;
                }
            }
        }

        public const string BaseMovieDbUrl = "https://api.themoviedb.org/";

        private const string TmdbConfigUrl = BaseMovieDbUrl + "3/configuration?api_key={0}";
        private const string GetMovieInfo3 = BaseMovieDbUrl + @"3/movie/{0}?api_key={1}&append_to_response=casts,releases,images,keywords,trailers";

        internal static string ApiKey = "f6bd687ffa63cd282b6ff2c6877f2669";
        internal static string AcceptHeader = "application/json,image/*";

        /// <summary>
        /// Gets the movie data path.
        /// </summary>
        /// <param name="appPaths">The app paths.</param>
        /// <param name="tmdbId">The TMDB id.</param>
        /// <returns>System.String.</returns>
        internal static string GetMovieDataPath(IApplicationPaths appPaths, string tmdbId)
        {
            var dataPath = GetMoviesDataPath(appPaths);

            return Path.Combine(dataPath, tmdbId);
        }

        internal static string GetMoviesDataPath(IApplicationPaths appPaths)
        {
            var dataPath = Path.Combine(appPaths.CachePath, "tmdb-movies2");

            return dataPath;
        }

        /// <summary>
        /// Downloads the movie info.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="preferredMetadataLanguage">The preferred metadata language.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        internal async Task<CompleteMovieData> DownloadMovieInfo(string id, string preferredMetadataLanguage, string preferredMetadataCountry, CancellationToken cancellationToken)
        {
            var mainResult = await FetchMainResult(id, true, preferredMetadataLanguage, preferredMetadataCountry, cancellationToken).ConfigureAwait(false);

            if (mainResult == null) return null;

            var dataFilePath = GetDataFilePath(id, preferredMetadataLanguage);

            _fileSystem.CreateDirectory(_fileSystem.GetDirectoryName(dataFilePath));

            _jsonSerializer.SerializeToFile(mainResult, dataFilePath);

            return mainResult;
        }

        internal Task<CompleteMovieData> EnsureMovieInfo(string tmdbId, string language, string country, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(tmdbId))
            {
                throw new ArgumentNullException("tmdbId");
            }

            var path = GetDataFilePath(tmdbId, language);

            var fileInfo = _fileSystem.GetFileSystemInfo(path);

            if (fileInfo.Exists)
            {
                // If it's recent or automatic updates are enabled, don't re-download
                if ((DateTimeOffset.UtcNow - _fileSystem.GetLastWriteTimeUtc(fileInfo)).TotalDays <= 2)
                {
                    return Task.FromResult(_jsonSerializer.DeserializeFromFile<CompleteMovieData>(fileInfo.FullName));
                }
            }

            return DownloadMovieInfo(tmdbId, language, country, cancellationToken);
        }

        internal string GetDataFilePath(string tmdbId, string preferredLanguage)
        {
            if (string.IsNullOrEmpty(tmdbId))
            {
                throw new ArgumentNullException("tmdbId");
            }

            var path = GetMovieDataPath(_configurationManager.ApplicationPaths, tmdbId);

            if (string.IsNullOrWhiteSpace(preferredLanguage))
            {
                preferredLanguage = "alllang";
            }

            var filename = string.Format("all-{0}.json", preferredLanguage);

            return Path.Combine(path, filename);
        }

        public static string GetImageLanguagesParam(string preferredLanguage, string preferredCountry)
        {
            var languages = new List<string>();

            if (!string.IsNullOrEmpty(preferredLanguage))
            {
                preferredLanguage = NormalizeLanguage(preferredLanguage, preferredCountry);

                languages.Add(preferredLanguage);

                if (preferredLanguage.Length == 5) // like en-US
                {
                    // Currenty, TMDB supports 2-letter language codes only
                    // They are planning to change this in the future, thus we're
                    // supplying both codes if we're having a 5-letter code.
                    languages.Add(preferredLanguage.Substring(0, 2));
                }
            }

            languages.Add("null");

            if (!string.Equals(preferredLanguage, "en", StringComparison.OrdinalIgnoreCase))
            {
                languages.Add("en");
            }

            return string.Join(",", languages.ToArray(languages.Count));
        }

        public static string NormalizeLanguage(string language, string country)
        {
            if (!string.IsNullOrEmpty(language))
            {
                if (string.Equals(language, "es", StringComparison.OrdinalIgnoreCase) && string.Equals(country, "mx", StringComparison.OrdinalIgnoreCase))
                {
                    return "es-MX";
                }

                // They require this to be uppercase
                // https://emby.media/community/index.php?/topic/32454-fr-follow-tmdbs-new-language-api-update/?p=311148
                var parts = language.Split('-');

                if (parts.Length == 2)
                {
                    language = parts[0] + "-" + parts[1].ToUpper();
                }
            }

            return language;
        }

        /// <summary>
        /// Fetches the main result.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="isTmdbId">if set to <c>true</c> [is TMDB identifier].</param>
        /// <param name="language">The language.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>Task{CompleteMovieData}.</returns>
        internal async Task<CompleteMovieData> FetchMainResult(string id, bool isTmdbId, string language, string country, CancellationToken cancellationToken)
        {
            var url = string.Format(GetMovieInfo3, id, ApiKey);

            if (!string.IsNullOrEmpty(language))
            {
                url += string.Format("&language={0}", NormalizeLanguage(language, country));

                // Get images in english and with no language
                url += "&include_image_language=" + GetImageLanguagesParam(language, country);
            }

            CompleteMovieData mainResult;

            cancellationToken.ThrowIfCancellationRequested();

            // Cache if not using a tmdbId because we won't have the tmdb cache directory structure. So use the lower level cache.
            var cacheMode = isTmdbId ? CacheMode.None : CacheMode.Unconditional;
            var cacheLength = TimeSpan.FromDays(3);

            try
            {
                using (var response = await GetMovieDbResponse(new HttpRequestOptions
                {
                    Url = url,
                    CancellationToken = cancellationToken,
                    AcceptHeader = AcceptHeader,
                    CacheMode = cacheMode,
                    CacheLength = cacheLength

                }).ConfigureAwait(false))
                {
                    using (var json = response.Content)
                    {
                        mainResult = await _jsonSerializer.DeserializeFromStreamAsync<CompleteMovieData>(json).ConfigureAwait(false);
                    }
                }
            }
            catch (HttpException ex)
            {
                // Return null so that callers know there is no metadata for this id
                if (ex.StatusCode.HasValue && ex.StatusCode.Value == HttpStatusCode.NotFound)
                {
                    return null;
                }

                throw;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // If the language preference isn't english, then have the overview fallback to english if it's blank
            if (mainResult != null &&
                string.IsNullOrEmpty(mainResult.overview) &&
                !string.IsNullOrEmpty(language) &&
                !string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Info("MovieDbProvider couldn't find meta for language " + language + ". Trying English...");

                url = string.Format(GetMovieInfo3, id, ApiKey) + "&language=en";

                if (!string.IsNullOrEmpty(language))
                {
                    // Get images in english and with no language
                    url += "&include_image_language=" + GetImageLanguagesParam(language, country);
                }

                using (var response = await GetMovieDbResponse(new HttpRequestOptions
                {
                    Url = url,
                    CancellationToken = cancellationToken,
                    AcceptHeader = AcceptHeader,
                    CacheMode = cacheMode,
                    CacheLength = cacheLength

                }).ConfigureAwait(false))
                {
                    using (var json = response.Content)
                    {
                        var englishResult = await _jsonSerializer.DeserializeFromStreamAsync<CompleteMovieData>(json).ConfigureAwait(false);

                        mainResult.overview = englishResult.overview;
                    }
                }
            }

            return mainResult;
        }

        private static long _lastRequestTicks;
        // The limit is 40 requests per 10 seconds
        private static int requestIntervalMs = 300;

        /// <summary>
        /// Gets the movie db response.
        /// </summary>
        internal async Task<HttpResponseInfo> GetMovieDbResponse(HttpRequestOptions options)
        {
            var delayTicks = (requestIntervalMs * 10000) - (DateTimeOffset.UtcNow.Ticks - _lastRequestTicks);
            var delayMs = Math.Min(delayTicks / 10000, requestIntervalMs);

            if (delayMs > 0)
            {
                _logger.Debug("Throttling Tmdb by {0} ms", delayMs);
                await Task.Delay(Convert.ToInt32(delayMs)).ConfigureAwait(false);
            }

            _lastRequestTicks = DateTimeOffset.UtcNow.Ticks;

            options.BufferContent = true;
            options.UserAgent = "Emby/" + _appHost.ApplicationVersion;

            return await _httpClient.SendAsync(options, "GET").ConfigureAwait(false);
        }

        /// <summary>
        /// Class TmdbTitle
        /// </summary>
        internal class TmdbTitle
        {
            /// <summary>
            /// Gets or sets the iso_3166_1.
            /// </summary>
            /// <value>The iso_3166_1.</value>
            public string iso_3166_1 { get; set; }
            /// <summary>
            /// Gets or sets the title.
            /// </summary>
            /// <value>The title.</value>
            public string title { get; set; }
        }

        /// <summary>
        /// Class TmdbAltTitleResults
        /// </summary>
        internal class TmdbAltTitleResults
        {
            /// <summary>
            /// Gets or sets the id.
            /// </summary>
            /// <value>The id.</value>
            public int id { get; set; }
            /// <summary>
            /// Gets or sets the titles.
            /// </summary>
            /// <value>The titles.</value>
            public List<TmdbTitle> titles { get; set; }
        }

        internal class BelongsToCollection
        {
            public int id { get; set; }
            public string name { get; set; }
            public string poster_path { get; set; }
            public string backdrop_path { get; set; }
        }

        internal class GenreItem
        {
            public int id { get; set; }
            public string name { get; set; }
        }

        internal class ProductionCompany
        {
            public string name { get; set; }
            public int id { get; set; }
        }

        internal class ProductionCountry
        {
            public string iso_3166_1 { get; set; }
            public string name { get; set; }
        }

        internal class SpokenLanguage
        {
            public string iso_639_1 { get; set; }
            public string name { get; set; }
        }

        internal class Cast
        {
            public int id { get; set; }
            public string name { get; set; }
            public string character { get; set; }
            public int order { get; set; }
            public int cast_id { get; set; }
            public string profile_path { get; set; }
        }

        internal class Crew
        {
            public int id { get; set; }
            public string name { get; set; }
            public string department { get; set; }
            public string job { get; set; }
            public string profile_path { get; set; }
        }

        internal class Casts
        {
            public List<Cast> cast { get; set; }
            public List<Crew> crew { get; set; }
        }

        internal class Country
        {
            public string iso_3166_1 { get; set; }
            public string certification { get; set; }
            public DateTimeOffset release_date { get; set; }
        }

        internal class Releases
        {
            public List<Country> countries { get; set; }
        }

        internal class Backdrop
        {
            public string file_path { get; set; }
            public int width { get; set; }
            public int height { get; set; }
            public object iso_639_1 { get; set; }
            public double aspect_ratio { get; set; }
            public double vote_average { get; set; }
            public int vote_count { get; set; }
        }

        internal class Poster
        {
            public string file_path { get; set; }
            public int width { get; set; }
            public int height { get; set; }
            public string iso_639_1 { get; set; }
            public double aspect_ratio { get; set; }
            public double vote_average { get; set; }
            public int vote_count { get; set; }
        }

        internal class Images
        {
            public List<Backdrop> backdrops { get; set; }
            public List<Poster> posters { get; set; }
        }

        internal class Keyword
        {
            public int id { get; set; }
            public string name { get; set; }
        }

        internal class Keywords
        {
            public List<Keyword> keywords { get; set; }
        }

        internal class Youtube
        {
            public string name { get; set; }
            public string size { get; set; }
            public string source { get; set; }
        }

        internal class Trailers
        {
            public List<object> quicktime { get; set; }
            public List<Youtube> youtube { get; set; }
        }

        internal class CompleteMovieData
        {
            public bool adult { get; set; }
            public string backdrop_path { get; set; }
            public BelongsToCollection belongs_to_collection { get; set; }
            public int budget { get; set; }
            public List<GenreItem> genres { get; set; }
            public string homepage { get; set; }
            public int id { get; set; }
            public string imdb_id { get; set; }
            public string original_title { get; set; }
            public string original_name { get; set; }
            public string overview { get; set; }
            public double popularity { get; set; }
            public string poster_path { get; set; }
            public List<ProductionCompany> production_companies { get; set; }
            public List<ProductionCountry> production_countries { get; set; }
            public string release_date { get; set; }
            public int revenue { get; set; }
            public int runtime { get; set; }
            public List<SpokenLanguage> spoken_languages { get; set; }
            public string status { get; set; }
            public string tagline { get; set; }
            public string title { get; set; }
            public string name { get; set; }
            public double vote_average { get; set; }
            public int vote_count { get; set; }
            public Casts casts { get; set; }
            public Releases releases { get; set; }
            public Images images { get; set; }
            public Keywords keywords { get; set; }
            public Trailers trailers { get; set; }

            public string GetOriginalTitle()
            {
                return original_name ?? original_title;
            }

            public string GetTitle()
            {
                return name ?? title ?? GetOriginalTitle();
            }
        }

        public int Order
        {
            get
            {
                return 1;
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
