### Introduction

This repository contains an easy-to-use and well-documented .NET assembly for communicating with and
receiving electronic mail from an Internet Message Access Protocol (IMAP) server.


### Usage & Examples

To use the library add the S22.Imap.dll assembly to your project references in Visual Studio. Here's
a simple example that initializes a new instance of the ImapClient class and connects to Gmail's
IMAP server:

	using System;
	using S22.Imap;

	namespace Test {
		class Program {
			static void Main(string[] args) {
				/* connect on port 993 using SSL */
				using (ImapClient Client = new ImapClient("imap.gmail.com", 993, true))
				{
					Console.WriteLine("We are connected!");
				}
			}
		}
	}

Please see the [documentation](http://smiley22.github.com/S22.Imap/Documentation/) for further details on using
the classes and methods exposed by the S22.Imap namespace. Plenty of example codes are provided.


### Credits

This library is copyright © 2012 Torben Könke.

Parts of this library are heavily based on the AE.Net.Mail project (copyright © 2012 Andy Edinborough).


### License

This library is released under the [MIT license](https://github.com/smiley22/S22.Imap/blob/master/License.md).


### Bug reports

Please send your bug reports and questions to [smileytwentytwo@gmail.com](mailto:smileytwentytwo@gmail.com) or create a new
issue on the GitHub project homepage.
