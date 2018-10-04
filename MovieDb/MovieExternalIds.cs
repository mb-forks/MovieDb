using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace MovieDb
{
    public class MovieDbMovieExternalId : IExternalId
    {
        public const string BaseMovieDbUrl = "https://www.themoviedb.org/";

        public string Name
        {
            get { return "TheMovieDb"; }
        }

        public string Key
        {
            get { return MetadataProviders.Tmdb.ToString(); }
        }

        public string UrlFormatString
        {
            get { return BaseMovieDbUrl + "movie/{0}"; }
        }

        public bool Supports(IHasProviderIds item)
        {
            // Supports images for tv movies
            var tvProgram = item as LiveTvProgram;
            if (tvProgram != null && tvProgram.IsMovie)
            {
                return true;
            }

            return item is Movie || item is MusicVideo || item is Trailer;
        }
    }

    public class MovieDbSeriesExternalId : IExternalId
    {
        public string Name
        {
            get { return "TheMovieDb"; }
        }

        public string Key
        {
            get { return MetadataProviders.Tmdb.ToString(); }
        }

        public string UrlFormatString
        {
            get { return MovieDbMovieExternalId.BaseMovieDbUrl + "tv/{0}"; }
        }

        public bool Supports(IHasProviderIds item)
        {
            return item is Series;
        }
    }

    public class MovieDbMovieCollectionExternalId : IExternalId
    {
        public string Name
        {
            get { return "TheMovieDb Collection"; }
        }

        public string Key
        {
            get { return MetadataProviders.TmdbCollection.ToString(); }
        }

        public string UrlFormatString
        {
            get { return MovieDbMovieExternalId.BaseMovieDbUrl + "collection/{0}"; }
        }

        public bool Supports(IHasProviderIds item)
        {
            return item is Movie || item is MusicVideo || item is Trailer;
        }
    }

    public class MovieDbPersonExternalId : IExternalId
    {
        public string Name
        {
            get { return "TheMovieDb"; }
        }

        public string Key
        {
            get { return MetadataProviders.Tmdb.ToString(); }
        }

        public string UrlFormatString
        {
            get { return MovieDbMovieExternalId.BaseMovieDbUrl + "person/{0}"; }
        }

        public bool Supports(IHasProviderIds item)
        {
            return item is Person;
        }
    }

    public class MovieDbCollectionExternalId : IExternalId
    {
        public string Name
        {
            get { return "TheMovieDb"; }
        }

        public string Key
        {
            get { return MetadataProviders.Tmdb.ToString(); }
        }

        public string UrlFormatString
        {
            get { return MovieDbMovieExternalId.BaseMovieDbUrl + "collection/{0}"; }
        }

        public bool Supports(IHasProviderIds item)
        {
            return item is BoxSet;
        }
    }
}
