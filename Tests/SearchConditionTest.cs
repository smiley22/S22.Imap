using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace S22.Imap.Test {
	/// <summary>
	/// Contains unit tests for SearchCondition.
	/// </summary>
	[TestClass]
	public class SearchConditionTest {
		/// <summary>
		/// Tests for constructing IMAP Search strings.
		/// </summary>
		[TestMethod]
		public void SearchConditions() {
			var dict = new Dictionary<string, SearchCondition>() {
				{ "(From \"foo@bar.com\") (Larger 1024)",
					SearchCondition.From("foo@bar.com").And(SearchCondition.Larger(1024))
				},
				{ "Or (Unanswered) (Flagged)",
					SearchCondition.Unanswered().Or(SearchCondition.Flagged())
				},
				{ "Or ((Subject {12}\r\n重要郵件) (SentBefore \"20-Dec-2012\")) (Unseen)",
					SearchCondition.Subject("重要郵件").And(SearchCondition
					.SentBefore(new DateTime(2012, 12, 20))).Or(SearchCondition.Unseen())
				}
			};
			foreach (KeyValuePair<string, SearchCondition> p in dict) {
				Assert.IsTrue(p.Key.Equals(p.Value.ToString(),
					StringComparison.InvariantCultureIgnoreCase));
			}
		}
	}
}
