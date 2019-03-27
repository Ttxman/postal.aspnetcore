using System;
using System.Collections.Generic;
using System.IO;
using MimeKit;
using MimeKit.Utils;

namespace Postal
{
    /// <summary>
    /// Used by the <see cref="HtmlExtensions.EmbedImage"/> helper method.
    /// It generates the <see cref="MimeEntity "/> objects need to embed images into an email.
    /// </summary>
    public class ImageEmbedder
    {
        internal static string ViewDataKey = "Postal.ImageEmbedder";


        public BodyBuilder Builder { get; } = new BodyBuilder();
        /// <summary>
        /// Creates a new <see cref="ImageEmbedder"/>.
        /// </summary>
        public ImageEmbedder()
        {
        }

        readonly Dictionary<string, MimeEntity> images = new Dictionary<string, MimeEntity>();

        /// <summary>
        /// Gets if any images have been referenced.
        /// </summary>
        public bool HasImages => images.Count > 0;

        /// <summary>
        /// Records a reference to the given image.
        /// </summary>
        /// <param name="imagePathOrUrl">The image path or URL.</param>
        /// <param name="contentType">The content type of the image e.g. "image/png". If null, then content type is determined from the file name extension.</param>
        /// <returns>A <see cref="Attachment"/> representing the embedded image.</returns>
        public MimeEntity ReferenceImage(string imagePathOrUrl, string contentType = null)
        {

            if (images.TryGetValue(imagePathOrUrl, out var resource))
                return resource;

            var filename = Path.GetFileName(imagePathOrUrl);
            contentType = contentType ?? MimeTypes.GetMimeType(filename);

            if (!ContentType.TryParse(contentType, out var ct))
                throw new FormatException($"content type '{contentType}' for resource '{imagePathOrUrl}' is not valid");

            if (Uri.IsWellFormedUriString(imagePathOrUrl, UriKind.Absolute))
            {
                var client = new System.Net.WebClient();
                var bytes = client.DownloadData(imagePathOrUrl);
                resource = Builder.LinkedResources.Add(filename, bytes, ct);
            }
            else
            {
                resource = Builder.LinkedResources.Add(imagePathOrUrl, ct);
            }
            resource.ContentId = MimeUtils.GenerateMessageId();

            images[imagePathOrUrl] = resource;
            return resource;
        }
    }
}
