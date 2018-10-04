using System;
using MediaBrowser.Common.Plugins;
using System.IO;
using MediaBrowser.Model.Drawing;

namespace MovieDb
{
    public class Plugin : BasePlugin, IHasThumbImage
    {
        private Guid _id = new Guid("3A63A9F3-810E-44F6-910A-14D6AD1255EC");
        public override Guid Id
        {
            get { return _id; }
        }

        public override string Name
        {
            get { return StaticName; }
        }

        public static string StaticName
        {
            get { return "MovieDb"; }
        }

        public override string Description
        {
            get
            {
                return "MovieDb metadata for movies";
            }
        }

        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".thumb.png");
        }

        public ImageFormat ThumbImageFormat
        {
            get
            {
                return ImageFormat.Png;
            }
        }
    }
}