using System;
using System.Collections.Generic;

namespace S22.Imap {
	/// <summary>
	/// Possible values for the "Type" property of the Bodypart class.
	/// For a detailed description of MIME Media Types refer to
	/// RFC 2046.
	/// </summary>
	public enum ContentType {
		/// <summary>
		/// The "text" media type is intended for sending material which
		/// is principally textual in form.
		/// </summary>
		Text,
		/// <summary>
		/// A media type of "image" indicates that the body contains an image.
		/// The subtype names the specific image format. 
		/// </summary>
		Image,
		/// <summary>
		/// A media type of "audio" indicates that the body contains audio
		/// data.
		/// </summary>
		Audio,
		/// <summary>
		/// A media type of "video" indicates that the body contains a
		/// time-varying-picture image, possibly with color and coordinated sound.
		/// </summary>
		Video,
		/// <summary>
		/// The "application" media type is to be used for discrete data which do
		/// not fit in any of the other categories, and particularly for data to
		/// be processed by some type of application program.
		/// </summary>
		Application,
		/// <summary>
		/// The media type value is unknown or could not be determined.
		/// </summary>
		Other
	}

	internal static class ContentTypeMap {
		public static ContentType fromString(string contentType) {
			Dictionary<string, ContentType> Map =
			new Dictionary<string, ContentType>(StringComparer.OrdinalIgnoreCase) {
				{ "Text", ContentType.Text },
				{ "Image",	ContentType.Image },
				{ "Audio",	ContentType.Audio },
				{ "Video", ContentType.Video },
				{ "Application", ContentType.Application },
				{ "Other", ContentType.Other }
			};
			try {
				return Map[contentType];
			} catch (KeyNotFoundException) {
				return ContentType.Other;
			}
		}
	}
}
