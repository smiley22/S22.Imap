using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace S22.Imap.Test {
	/// <summary>
	/// Contains unit tests for parsing IMAP body structures.
	/// </summary>
	[TestClass]
	public class BodystructureTest {
		/// <summary>
		/// Tests for parsing IMAP body structures.
		/// </summary>
		[TestMethod]
		public void ParseBodystructure() {
			foreach (KeyValuePair<string, Bodypart[]> pair in dict) {
				Bodypart[] parts = Bodystructure.Parse(pair.Key);

				// Must have same number of body parts
				Assert.AreEqual<int>(parts.Length, pair.Value.Length);

				for (int i = 0; i < parts.Length; i++)
					Assert.IsTrue(Equals(parts[i], pair.Value[i]),
						"Not equal at index " + i + " for body structure " + pair.Key);
			}
		}

		/// <summary>
		/// Passing malformed body structures to Bodystructure.Parse should raise
		/// a FormatException.
		/// </summary>
		[TestMethod]
		[ExpectedException(typeof(FormatException))]
		public void ThrowOnInvalidBodystructure() {
			string invalidBodystructure = "((NIL (123) ABC))";

			Bodystructure.Parse(invalidBodystructure);
		}

		/// <summary>
		/// Compares two Bodypart instances for value equality.
		/// </summary>
		/// <param name="a">The first Bodypart instance.</param>
		/// <param name="b">The second Bodypart instance. </param>
		/// <returns>True if both instances are semantically equal,
		/// otherwise false.</returns>
		bool Equals(Bodypart a, Bodypart b) {
			bool equal =
				(a.Description == b.Description) &&
				(a.Disposition.Filename == b.Disposition.Filename) &&
				(a.Disposition.Type == b.Disposition.Type) &&
				(a.Disposition.Attributes.Count == b.Disposition.Attributes.Count) &&
				(a.Encoding == b.Encoding) &&
				(a.Id == b.Id) &&
				(a.Language == b.Language) &&
				(a.Lines == b.Lines) &&
				(a.Location == b.Location) &&
				(a.Md5 == b.Md5) &&
				(a.PartNumber == b.PartNumber) &&
				(a.Size == b.Size) &&
				(a.Subtype == b.Subtype) &&
				(a.Type == b.Type) &&
				(a.Parameters.Count == b.Parameters.Count);
			if (!equal)
				return false;
			foreach (string key in a.Parameters.Keys) {
				if (a.Parameters[key] != b.Parameters[key])
					return false;
			}
			foreach (string key in a.Disposition.Attributes.Keys) {
				if (a.Disposition.Attributes[key] != b.Disposition.Attributes[key])
					return false;
			}
			return true;
		}

		#region Bodystructures
		/// <summary>
		/// Various body structures collected from various IMAP servers.
		/// </summary>
		Dictionary<string, Bodypart[]> dict = new Dictionary<string, Bodypart[]>() {
			{
				"\"TEXT\" \"PLAIN\" (\"CHARSET\" \"UTF-8\") NIL NIL " +
				"\"QUOTED-PRINTABLE\" 7388 179 NIL NIL NIL",
				new Bodypart[] {
					new Bodypart("1") { Type = ContentType.Text, Subtype = "PLAIN",
					 Encoding = ContentTransferEncoding.QuotedPrintable, Size = 7388,
					 Lines = 179, Parameters = new Dictionary<string,string>() {
						 {"CHARSET", "UTF-8"} }
					}
				}
			},
			{
				"(\"TEXT\" \"PLAIN\" (\"CHARSET\" \"UTF-8\") NIL NIL " +
				"\"QUOTED-PRINTABLE\" 3781 90 NIL NIL NIL)(\"TEXT\" " +
				"\"HTML\" (\"CHARSET\" \"UTF-8\") NIL NIL \"QUOTED-PRINTABLE\"" +
				" 17192 661 NIL NIL NIL) \"ALTERNATIVE\" (\"BOUNDARY\" " +
				"\"----=_Part_769816_15327486.1278964428218\") NIL NIL",
				new Bodypart[] {
					new Bodypart("1") { Type = ContentType.Text, Subtype = "PLAIN",
					 Parameters = new Dictionary<string,string>() { {"CHARSET", "UTF-8"} },
					 Encoding = ContentTransferEncoding.QuotedPrintable, Size = 3781,
					 Lines = 90},
					new Bodypart("2") { Type = ContentType.Text, Subtype = "HTML",
					 Parameters = new Dictionary<string,string>() { {"CHARSET", "UTF-8"} },
					 Encoding = ContentTransferEncoding.QuotedPrintable, Size = 17192,
					 Lines = 661}
				}
			},
			{
				"(\"TEXT\" \"PLAIN\" (\"CHARSET\" \"us-ascii\") NIL NIL " +
				"\"7BIT\" 236 10 NIL NIL NIL)((\"TEXT\" \"HTML\" (\"CHARSET\" " +
				"\"us-ascii\") NIL NIL \"QUOTED-PRINTABLE\" 2777 45 NIL NIL NIL)" +
				"(\"IMAGE\" \"JPEG\" (\"NAME\" \"00A8F6006C84(IPCAM)_m20111104125749.jpg\") " +
				"\"<07FC85EF-3DE2-4C3F-8CCD-6AE64F1E569C>\" NIL \"BASE64\" 53545 NIL (\"INLINE\" " +
				"(\"FILENAME\" \"00A8F6006C84(IPCAM)_m20111104125749.jpg\")) NIL)(\"IMAGE\" " +
				"\"JPEG\" (\"NAME\" \"00A8F6006C84(IPCAM)_m20111104125750.jpg\") " +
				"\"<90026F09-AF81-4184-BB80-5F3FA69D0DA2>\" NIL \"BASE64\" 52746 NIL (\"INLINE\" " +
				"(\"FILENAME\" \"00A8F6006C84(IPCAM)_m20111104125750.jpg\")) NIL)(\"IMAGE\" \"JPEG\" " +
				"(\"NAME\" \"00A8F6006C84(IPCAM)_m20111104125751.jpg\") " +
				"\"<C71DFCE2-467C-44F1-BA4A-82CBB4A1F403>\" NIL \"BASE64\" 51433 NIL (\"INLINE\" " +
				"(\"FILENAME\" \"00A8F6006C84(IPCAM)_m20111104125751.jpg\")) NIL)(\"IMAGE\" \"JPEG\" " +
				"(\"NAME\" \"00A8F6006C84(IPCAM)_m20111104125752.jpg\") " +
				"\"<4DE3058B-6921-40B5-B28E-97C0F08675FE>\" NIL \"BASE64\" 43669 NIL (\"INLINE\" " +
				"(\"FILENAME\" \"00A8F6006C84(IPCAM)_m20111104125752.jpg\")) NIL)(\"IMAGE\" \"JPEG\" " +
				"(\"NAME\" \"00A8F6006C84(IPCAM)_m20111104125753.jpg\") " +
				"\"<D2E61C4B-AAC7-4D04-8534-2880E94B3CD4>\" NIL \"BASE64\" 53099 NIL (\"INLINE\" " +
				"(\"FILENAME\" \"00A8F6006C84(IPCAM)_m20111104125753.jpg\")) NIL)(\"IMAGE\" \"JPEG\" " +
				"(\"NAME\" \"00A8F6006C84(IPCAM)_m20111104125754.jpg\") " +
				"\"<14E42852-1097-48E9-926B-10A84D438A99>\" NIL \"BASE64\" 52613 NIL (\"INLINE\" " +
				"(\"FILENAME\" \"00A8F6006C84(IPCAM)_m20111104125754.jpg\")) NIL) \"RELATED\" " +
				"(\"BOUNDARY\" \"Apple-Mail-71-651448002\" \"TYPE\" \"text/html\") NIL NIL) " +
				"\"ALTERNATIVE\" (\"BOUNDARY\" \"Apple-Mail-70-651448002\") NIL NIL",
				new Bodypart[] {
					new Bodypart("1") { Type = ContentType.Text, Subtype = "PLAIN",
 						Parameters = new Dictionary<string,string>() { {"CHARSET", "us-ascii"} },
					  Encoding = ContentTransferEncoding.Bit7, Size = 236, Lines = 10
					},
					new Bodypart("2.1") { Type = ContentType.Text, Subtype = "HTML",
					  Parameters = new Dictionary<string,string>() { {"CHARSET", "us-ascii"} },
						Encoding = ContentTransferEncoding.QuotedPrintable, Size = 2777,
						Lines = 45
					},
					new Bodypart("2.2") { Type = ContentType.Image, Subtype = "JPEG",
						Parameters = new Dictionary<string,string>() {
						{"NAME", "00A8F6006C84(IPCAM)_m20111104125749.jpg"} },
						Id = "<07FC85EF-3DE2-4C3F-8CCD-6AE64F1E569C>",
						Encoding = ContentTransferEncoding.Base64, Size = 53545,
					  Disposition = new ContentDisposition() {
							Filename = "00A8F6006C84(IPCAM)_m20111104125749.jpg",
							Type = ContentDispositionType.Inline,
							Attributes = new Dictionary<string,string>() {
							{"FILENAME", "00A8F6006C84(IPCAM)_m20111104125749.jpg"} }
						}
					},
					new Bodypart("2.3") { Type = ContentType.Image, Subtype = "JPEG",
						Parameters = new Dictionary<string,string>() {
						{"NAME", "00A8F6006C84(IPCAM)_m20111104125750.jpg"} },
						Id = "<90026F09-AF81-4184-BB80-5F3FA69D0DA2>",
						Encoding = ContentTransferEncoding.Base64, Size = 52746,
					  Disposition = new ContentDisposition() {
							Filename = "00A8F6006C84(IPCAM)_m20111104125750.jpg",
							Type = ContentDispositionType.Inline,
							Attributes = new Dictionary<string,string>() {
							{"FILENAME", "00A8F6006C84(IPCAM)_m20111104125750.jpg"} }
						}
					},
					new Bodypart("2.4") { Type = ContentType.Image, Subtype = "JPEG",
						Parameters = new Dictionary<string,string>() {
						{"NAME", "00A8F6006C84(IPCAM)_m20111104125751.jpg"} },
						Id = "<C71DFCE2-467C-44F1-BA4A-82CBB4A1F403>",
						Encoding = ContentTransferEncoding.Base64, Size = 51433,
					  Disposition = new ContentDisposition() {
							Filename = "00A8F6006C84(IPCAM)_m20111104125751.jpg",
							Type = ContentDispositionType.Inline,
							Attributes = new Dictionary<string,string>() {
							{"FILENAME", "00A8F6006C84(IPCAM)_m20111104125751.jpg"} }
						}
					},					
					new Bodypart("2.5") { Type = ContentType.Image, Subtype = "JPEG",
						Parameters = new Dictionary<string,string>() {
						{"NAME", "00A8F6006C84(IPCAM)_m20111104125752.jpg"} },
						Id = "<4DE3058B-6921-40B5-B28E-97C0F08675FE>",
						Encoding = ContentTransferEncoding.Base64, Size = 43669,
					  Disposition = new ContentDisposition() {
							Filename = "00A8F6006C84(IPCAM)_m20111104125752.jpg",
							Type = ContentDispositionType.Inline,
							Attributes = new Dictionary<string,string>() {
							{"FILENAME", "00A8F6006C84(IPCAM)_m20111104125752.jpg"} }
						}
					},						
					new Bodypart("2.6") { Type = ContentType.Image, Subtype = "JPEG",
						Parameters = new Dictionary<string,string>() {
						{"NAME", "00A8F6006C84(IPCAM)_m20111104125753.jpg"} },
						Id = "<D2E61C4B-AAC7-4D04-8534-2880E94B3CD4>",
						Encoding = ContentTransferEncoding.Base64, Size = 53099,
					  Disposition = new ContentDisposition() {
							Filename = "00A8F6006C84(IPCAM)_m20111104125753.jpg",
							Type = ContentDispositionType.Inline,
							Attributes = new Dictionary<string,string>() {
							{"FILENAME", "00A8F6006C84(IPCAM)_m20111104125753.jpg"} }
						}
					},
					new Bodypart("2.7") { Type = ContentType.Image, Subtype = "JPEG",
						Parameters = new Dictionary<string,string>() {
						{"NAME", "00A8F6006C84(IPCAM)_m20111104125754.jpg"} },
						Id = "<14E42852-1097-48E9-926B-10A84D438A99>",
						Encoding = ContentTransferEncoding.Base64, Size = 52613,
					  Disposition = new ContentDisposition() {
							Filename = "00A8F6006C84(IPCAM)_m20111104125754.jpg",
							Type = ContentDispositionType.Inline,
							Attributes = new Dictionary<string,string>() {
							{"FILENAME", "00A8F6006C84(IPCAM)_m20111104125754.jpg"} }
						}
					},						
				}
			},
			{
				"(((\"TEXT\" \"PLAIN\" (\"CHARSET\" \"iso-8859-1\") NIL NIL \"QUOTED-PRINTABLE\" " +
				"730 56 NIL NIL NIL)(\"TEXT\" \"HTML\" (\"CHARSET\" \"iso-8859-1\") NIL NIL " +
				"\"QUOTED-PRINTABLE\" 7011 169 NIL NIL NIL) \"ALTERNATIVE\" (\"BOUNDARY\" " +
				"\"----=_NextPart_002_00EC_01CC9AD4.C6FF0DD0\") NIL NIL)(\"IMAGE\" \"JPEG\" " +
				"(\"NAME\" \"image001.jpg\") NIL NIL \"BASE64\" 101479 NIL NIL NIL) " +
				"\"RELATED\" (\"BOUNDARY\" \"----=_NextPart_001_00EB_01CC9AD4.C6FF0DD0\") NIL " +
				"NIL)(\"APPLICATION\" \"PDF\" (\"NAME\" \"EXPOSEE_OBJ_75[2](1).PDF\") NIL NIL " +
				"\"BASE64\" 273842 NIL NIL NIL) \"MIXED\" (\"BOUNDARY\" " +
				"\"----=_NextPart_000_00EA_01CC9AD4.C6FF0DD0\") NIL NIL",
				new Bodypart[] {
					new Bodypart("1.1.1") { Type = ContentType.Text, Subtype = "PLAIN",
						Parameters = new Dictionary<string,string>() { {"CHARSET", "iso-8859-1"} },
						Encoding = ContentTransferEncoding.QuotedPrintable, Size = 730, Lines = 56
					},
					new Bodypart("1.1.2") { Type = ContentType.Text, Subtype = "HTML",
						Parameters = new Dictionary<string,string>() { {"CHARSET", "iso-8859-1"} },
						Encoding = ContentTransferEncoding.QuotedPrintable, Size = 7011,
						Lines = 169
					},
					new Bodypart("1.2") { Type = ContentType.Image, Subtype = "JPEG",
						Parameters = new Dictionary<string,string>() { {"NAME", "image001.jpg"} },
						Encoding = ContentTransferEncoding.Base64, Size = 101479
					},
					new Bodypart("2") { Type = ContentType.Application, Subtype = "PDF",
						Parameters = new Dictionary<string,string>() {
						{"NAME", "EXPOSEE_OBJ_75[2](1).PDF"} },
						Encoding = ContentTransferEncoding.Base64, Size = 273842
					}
				}
			},
			{
				"(\"TEXT\" \"PLAIN\" (\"CHARSET\" \"us-ascii\") NIL NIL \"7BIT\" 4 2 NIL NIL NIL)" +
				"(\"APPLICATION\" \"PDF\" (\"NAME\" \"Einfuhrung i. d. Automatentheorie, Formale " +
				"Sprachen und Komplexitatstheorie - *ISBN 3-8273-7020-5* - (C) 2002 by Pearson " +
				"Studium <DDT>.pdf\") NIL NIL \"BASE64\" 6236202 NIL (\"ATTACHMENT\" (\"FILENAME\" " +
				"\"=?ISO-8859-1?Q?Einf=FChrung_i._d._Automatentheorie,?= =?ISO-8859-1?Q?_Formale_" +
				"Sprachen_und_Komplexit=E4ts?= =?ISO-8859-1?Q?theorie_-_*ISBN_3-8273-7020-5*_-_" +
				"=A9?= =?ISO-8859-1?Q?_2002_by_Pearson_Studium_<DDT>.pdf?=\")) NIL)(\"TEXT\" " +
				"\"PLAIN\" (\"CHARSET\" \"us-ascii\") NIL NIL \"7BIT\" 30 3 NIL NIL NIL) \"MIXED\" " +
				"(\"BOUNDARY\" \"Apple-Mail-2D9D90A6-E720-4FC2-B1D3-2E127A12A479\") NIL NIL",
				new Bodypart[] {
					new Bodypart("1") { Type = ContentType.Text, Subtype = "PLAIN",
						Parameters = new Dictionary<string,string>() { {"CHARSET", "us-ascii"} },
						Encoding = ContentTransferEncoding.Bit7, Size = 4, Lines = 2
					},
					new Bodypart("2") { Type = ContentType.Application, Subtype = "PDF",
						Parameters = new Dictionary<string,string>() { {"NAME", "Einfuhrung i. d. " +
							"Automatentheorie, Formale Sprachen und Komplexitatstheorie - " +
							"*ISBN 3-8273-7020-5* - (C) 2002 by Pearson Studium <DDT>.pdf"} },
						Encoding = ContentTransferEncoding.Base64, Size = 6236202,
						Disposition = new ContentDisposition() {
							Type = ContentDispositionType.Attachment,
							Filename = "=?ISO-8859-1?Q?Einf=FChrung_i._d._Automatentheorie,?= " +
							"=?ISO-8859-1?Q?_Formale_Sprachen_und_Komplexit=E4ts?= =?ISO-8859-1?Q?" +
							"theorie_-_*ISBN_3-8273-7020-5*_-_=A9?= =?ISO-8859-1?Q?_2002_by_Pearson_" +
							"Studium_<DDT>.pdf?=",
							Attributes = new Dictionary<string,string>() {
								{	"FILENAME", "=?ISO-8859-1?Q?Einf=FChrung_i._d._Automatentheorie,?= " +
									"=?ISO-8859-1?Q?_Formale_Sprachen_und_Komplexit=E4ts?= =?ISO-8859-1?Q?" +
									"theorie_-_*ISBN_3-8273-7020-5*_-_=A9?= =?ISO-8859-1?Q?_2002_by_Pearson_" +
									"Studium_<DDT>.pdf?="
								}
							}
						}
					},
					new Bodypart("3") { Type = ContentType.Text, Subtype = "PLAIN",
						Parameters = new Dictionary<string,string>() { {"CHARSET", "us-ascii"} },
						Encoding = ContentTransferEncoding.Bit7, Size = 30, Lines = 3
					}
				}
			},
			{
				"((\"TEXT\" \"PLAIN\" (\"CHARSET\" \"ISO-8859-1\") NIL NIL \"QUOTED-PRINTABLE\" " +
				"780 27 NIL NIL NIL)(\"TEXT\" \"HTML\" (\"CHARSET\" \"ISO-8859-1\") NIL NIL " +
				"\"QUOTED-PRINTABLE\" 1849 34 NIL NIL NIL) \"ALTERNATIVE\" (\"BOUNDARY\" " +
				"\"0016364c769b92372504b17b63af\") NIL NIL)(\"IMAGE\" \"GIF\" (\"NAME\" " +
				"\"342.gif\") \"<342@goomoji.gmail>\" NIL \"BASE64\" 282 NIL NIL NIL) " +
				"\"RELATED\" (\"BOUNDARY\" \"0016364c769b92372b04b17b63b0\") NIL NIL",
				new Bodypart[] {
					new Bodypart("1.1") { Type = ContentType.Text, Subtype = "PLAIN",
						Parameters = new Dictionary<string,string>() { {"CHARSET", "ISO-8859-1"} },
						Encoding = ContentTransferEncoding.QuotedPrintable, Size = 780,
						Lines = 27
					},
					new Bodypart("1.2") { Type = ContentType.Text, Subtype = "HTML",
						Parameters = new Dictionary<string,string>() { {"CHARSET", "ISO-8859-1"} },
						Encoding = ContentTransferEncoding.QuotedPrintable, Size = 1849,
						Lines = 34
					},
					new Bodypart("2") { Type = ContentType.Image, Subtype = "GIF",
						Parameters = new Dictionary<string,string>() { {"NAME", "342.gif"} },
						Id = "<342@goomoji.gmail>", Encoding = ContentTransferEncoding.Base64,
						Size = 282
					}
				}
			},
			{
				"\"TEXT\" \"PLAIN\" (\"CHARSET\" \"iso-8859-1\" \"FORMAT\" \"flowed\" " +
				"\"REPLY-TYPE\" \"original\") NIL NIL \"8BIT\" 6041 144 NIL NIL NIL",
				new Bodypart[] {
					new Bodypart("1") { Type = ContentType.Text, Subtype = "PLAIN",
						 Parameters = new Dictionary<string, string>() {
						 {"CHARSET", "iso-8859-1"}, {"FORMAT", "flowed"},
						 {"REPLY-TYPE", "original"} },
						 Encoding = ContentTransferEncoding.Bit8, Size = 6041,
						 Lines = 144
					}
				}
			},
			{
				"(((\"TEXT\" \"PLAIN\" (\"CHARSET\" \"shift-jis\") NIL NIL \"BASE64\" " +
				"2010 58 NIL NIL NIL)(\"TEXT\" \"HTML\" (\"CHARSET\" \"shift-jis\") NIL NIL " +
				"\"BASE64\" 7179 140 NIL NIL NIL) \"ALTERNATIVE\" (\"BOUNDARY\" " +
				"\"_000_EB0020BBA0510C4FB8044BB536D65F38718E570740XPERToutlawwi_\") NIL NIL)" +
				"(\"IMAGE\" \"JPEG\" (\"NAME\" \"image001.jpg\") \"<image001.jpg@01CC3678.F1BEA3C0>\" " +
				"\"image001.jpg\" \"BASE64\" 5225 NIL (\"INLINE\" (\"CREATION-DATE\" " +
				"\"Wed, 29 Jun 2011 16:24:04 GMT\" \"FILENAME\" \"image001.jpg\" " +
				"\"MODIFICATION-DATE\" \"Wed, 29 Jun 2011 16:24:04 GMT\" \"SIZE\" \"3866\")) NIL) " +
				"\"RELATED\" (\"BOUNDARY\" \"_005_EB0020BBA0510C4FB8044BB536D65F38718E570740XPERToutlawwi_\" " +
				"\"TYPE\" \"multipart/alternative\") NIL NIL)(\"APPLICATION\" \"PDF\" " +
				"(\"NAME\" \"=?shift-jis?B?keWKd4LMg3aDi4Nag5ODZYNWg4eDk4FFj0iCUYJPglCCUA==?=\") NIL " +
				"\"=?shift-jis?B?keWKd4LMg3aDi4Nag5ODZYNWg4eDk4FFj0iCUYJPglCCUA==?=\" \"BASE64\" 1168622 " +
				"NIL (\"ATTACHMENT\" (\"CREATION-DATE\" \"Wed, 29 Jun 2011 16:19:24 GMT\" \"FILENAME\" " +
				"\"=?shift-jis?B?keWKd4LMg3aDi4Nag5ODZYNWg4eDk4FFj0iCUYJPglCCUA==?=\" \"MODIFICATION-DATE\" " +
				"\"Wed, 29 Jun 2011 16:19:24 GMT\" \"SIZE\" \"865083\")) NIL) \"MIXED\" " +
				"(\"BOUNDARY\" \"_006_EB0020BBA0510C4FB8044BB536D65F38718E570740XPERToutlawwi_\") NIL NIL",
				new Bodypart[] {
					new Bodypart("1.1.1") { Type = ContentType.Text, Subtype = "PLAIN",
						Parameters = new Dictionary<string,string>() { {"CHARSET", "shift-jis"} },
						Encoding = ContentTransferEncoding.Base64, Size = 2010, Lines = 58
					},
					new Bodypart("1.1.2") { Type = ContentType.Text, Subtype = "HTML",
						Parameters = new Dictionary<string,string>() { {"CHARSET", "shift-jis"} },
						Encoding = ContentTransferEncoding.Base64, Size = 7179, Lines = 140
					},
					new Bodypart("1.2") { Type = ContentType.Image, Subtype = "JPEG",
						Parameters = new Dictionary<string,string>() { {"NAME", "image001.jpg"} },
						Id = "<image001.jpg@01CC3678.F1BEA3C0>",
						Description = "image001.jpg", Encoding = ContentTransferEncoding.Base64,
						Size = 5225, Disposition = new ContentDisposition() {
							Type = ContentDispositionType.Inline, Filename = "image001.jpg",
							Attributes = new Dictionary<string,string>() {
								{ "CREATION-DATE", "Wed, 29 Jun 2011 16:24:04 GMT" },
								{ "FILENAME", "image001.jpg" },
								{ "MODIFICATION-DATE", "Wed, 29 Jun 2011 16:24:04 GMT" },
								{ "SIZE", "3866" }
							}
						}
					},
					new Bodypart("2") { Type = ContentType.Application, Subtype = "PDF",
						Parameters = new Dictionary<string,string>() {
							{"NAME", "=?shift-jis?B?keWKd4LMg3aDi4Nag5ODZYNWg4eDk4FFj0iCUYJPglCCUA==?="}
						},
						Description = "=?shift-jis?B?keWKd4LMg3aDi4Nag5ODZYNWg4eDk4FFj0iCUYJPglCCUA==?=",
						Encoding = ContentTransferEncoding.Base64, Size = 1168622,
						Disposition = new ContentDisposition() {
							Type = ContentDispositionType.Attachment,
							Filename = "=?shift-jis?B?keWKd4LMg3aDi4Nag5ODZYNWg4eDk4FFj0iCUYJPglCCUA==?=",
							Attributes = new Dictionary<string,string>() {
								{ "CREATION-DATE", "Wed, 29 Jun 2011 16:19:24 GMT" },
								{ "FILENAME", "=?shift-jis?B?keWKd4LMg3aDi4Nag5ODZYNWg4eDk4FFj0iCUYJPglCCUA==?=" },
								{ "MODIFICATION-DATE", "Wed, 29 Jun 2011 16:19:24 GMT" },
								{ "SIZE", "865083" }
							}
						}
					}
				}
			}
		};
		#endregion
	}
}
