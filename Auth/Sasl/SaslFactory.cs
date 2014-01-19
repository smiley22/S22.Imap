using System;
using System.Collections.Generic;

namespace S22.Imap.Auth.Sasl {
	/// <summary>
	/// A factory class for producing instances of Sasl mechanisms.
	/// </summary>
	internal static class SaslFactory {
		/// <summary>
		/// A dictionary of Sasl mechanisms registered with the factory class.
		/// </summary>
		static Dictionary<string, Type> Mechanisms {
			get;
			set;
		}

		/// <summary>
		/// Creates an instance of the Sasl mechanism with the specified
		/// name.
		/// </summary>
		/// <param name="name">The name of the Sasl mechanism of which an
		/// instance will be created.</param>
		/// <returns>An instance of the Sasl mechanism with the specified name.</returns>
		/// <exception cref="ArgumentNullException">Thrown if the name parameter
		/// is null.</exception>
		/// <exception cref="SaslException">Thrown if the Sasl mechanism with the
		/// specified name is not registered with Sasl.SaslFactory.</exception>
		public static SaslMechanism Create(string name) {
			name.ThrowIfNull("name");
			if (!Mechanisms.ContainsKey(name)) {
				throw new SaslException("A Sasl mechanism with the specified name " +
					"is not registered with Sasl.SaslFactory.");
			}
			Type t = Mechanisms[name];
			object o = Activator.CreateInstance(t, true);
			return o as SaslMechanism;
		}

		/// <summary>
		/// Registers a Sasl mechanism with the factory using the specified name.
		/// </summary>
		/// <param name="name">The name with which to register the Sasl mechanism
		/// with the factory class.</param>
		/// <param name="t">The type of the class implementing the Sasl mechanism.
		/// The implementing class must be a subclass of Sasl.SaslMechanism.</param>
		/// <exception cref="ArgumentNullException">Thrown if the name or the t
		/// parameter is null.</exception>
		/// <exception cref="ArgumentException">Thrown if the class represented
		/// by the specified type does not derive from Sasl.SaslMechanism.</exception>
		/// <exception cref="SaslException">Thrown if the Sasl mechanism could not
		/// be registered with the factory. Refer to the inner exception for error
		/// details.</exception>
		public static void Add(string name, Type t) {
			name.ThrowIfNull("name");
			t.ThrowIfNull("t");
			if (!t.IsSubclassOf(typeof(SaslMechanism))) {
				throw new ArgumentException("The type t must be a subclass " +
					"of Sasl.SaslMechanism");
			}
			try {
				Mechanisms.Add(name, t);
			} catch (Exception e) {
				throw new SaslException("Registration of Sasl mechanism failed.", e);
			}
		}

		/// <summary>
		/// Static class constructor. Initializes static properties.
		/// </summary>
		static SaslFactory() {
			Mechanisms = new Dictionary<string, Type>(
				StringComparer.InvariantCultureIgnoreCase);

			// Could be moved to App.config to support SASL "plug-in" mechanisms.
			var list = new Dictionary<string, Type>() {
				{ "Plain", typeof(Sasl.Mechanisms.SaslPlain) },
				{ "CramMd5", typeof(Sasl.Mechanisms.SaslCramMd5) },
				{ "DigestMd5", typeof(Sasl.Mechanisms.SaslDigestMd5) },
				{ "OAuth", typeof(Sasl.Mechanisms.SaslOAuth) },
				{ "OAuth2", typeof(Sasl.Mechanisms.SaslOAuth2) },
				{ "Ntlm", typeof(Sasl.Mechanisms.SaslNtlm) },
				{ "Ntlmv2", typeof(Sasl.Mechanisms.SaslNtlmv2) },
				{ "ScramSha1", typeof(Sasl.Mechanisms.SaslScramSha1) },
				// SRP is not supported in the .NET 3.5 configuration of the library because it requires
				// the System.Numerics namespace which has only been part of .NET since version 4.
#if !NET35
				{ "Srp", typeof(Sasl.Mechanisms.SaslSrp) }
#endif
			};
			foreach (string key in list.Keys)
				Mechanisms.Add(key, list[key]);
		}
	}
}
