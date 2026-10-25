using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ConfigurationUI
{
    /// <summary>Keep numeric lexemes intact, including values beyond double/decimal precision.</summary>
    public static class LosslessJson
    {
        private static readonly Regex Number = new Regex(@"\G-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?", RegexOptions.CultureInvariant);
        public static JToken Parse(string text) => new Parser(text).ReadDocument();
        // JRaw preserves source numeric lexemes for editing, but JTokenReader cannot
        // convert Raw directly to typed numeric fields. Cross the runtime boundary
        // through JSON text without replacing or normalizing the editor document.
        public static T ToRuntime<T>(JToken value) => JsonConvert.DeserializeObject<T>(value.ToString(Formatting.None));
        public static string Kind(JToken value)
        {
            if (value == null) return "missing";
            if (value.Type == JTokenType.Raw)
                return Regex.IsMatch(value.ToString(), @"^-?(0|[1-9][0-9]*)$") ? "integer" : "number";
            switch (value.Type)
            {
                case JTokenType.Object: return "object";
                case JTokenType.Array: return "array";
                case JTokenType.Boolean: return "boolean";
                case JTokenType.Integer: return "integer";
                case JTokenType.Float: return "number";
                case JTokenType.Null: return "null";
                default: return "string";
            }
        }
        public static double Numeric(JToken value) => double.Parse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture);
        public static bool Equivalent(JToken a, JToken b)
        {
            if (a == null || b == null) return a == b;
            if (a.Type == JTokenType.Raw || b.Type == JTokenType.Raw)
                return a.ToString(Formatting.None) == b.ToString(Formatting.None);
            return JToken.DeepEquals(a, b);
        }
        private sealed class Parser
        {
            private readonly string text;
            private int at;
            public Parser(string text) { this.text = text ?? throw new ArgumentNullException(nameof(text)); }
            private void Space() { while (at < text.Length && (text[at] == ' ' || text[at] == '\r' || text[at] == '\n' || text[at] == '\t')) at++; }
            private Exception Error(string message) => new JsonReaderException(message + " at character " + at + ".");
            private bool Take(char c) { Space(); if (at < text.Length && text[at] == c) { at++; return true; } return false; }
            private void Require(char c) { if (!Take(c)) throw Error("Expected '" + c + "'"); }
            private string String()
            {
                Space(); int start = at; Require('"');
                while (at < text.Length)
                {
                    char c = text[at++];
                    if (c == '"') return JsonConvert.DeserializeObject<string>(text.Substring(start, at - start));
                    if (c < 32) throw Error("Unescaped control character");
                    if (c == '\\')
                    {
                        if (at == text.Length || "\"\\/bfnrtu".IndexOf(text[at]) < 0) throw Error("Invalid escape");
                        if (text[at++] == 'u')
                        {
                            for (int i = 0; i < 4; i++)
                                if (at >= text.Length || !Uri.IsHexDigit(text[at++])) throw Error("Invalid Unicode escape");
                        }
                    }
                }
                throw Error("Unterminated string");
            }
            private JToken Value(int depth)
            {
                if (depth > 128) throw Error("Maximum nesting depth is 128");
                Space(); if (at == text.Length) throw Error("Expected a value");
                if (text[at] == '"') return new JValue(String());
                if (Take('{'))
                {
                    var obj = new JObject(); if (Take('}')) return obj;
                    do
                    {
                        string name = String(); Require(':');
                        if (obj.Property(name) != null) throw Error("Duplicate property '" + name + "' cannot be edited losslessly");
                        obj.Add(name, Value(depth + 1));
                        if (Take('}')) return obj;
                        Require(',');
                    } while (true);
                }
                if (Take('['))
                {
                    var array = new JArray(); if (Take(']')) return array;
                    do { array.Add(Value(depth + 1)); if (Take(']')) return array; Require(','); } while (true);
                }
                foreach (string keyword in new[] { "true", "false", "null" })
                    if (at + keyword.Length <= text.Length && string.CompareOrdinal(text, at, keyword, 0, keyword.Length) == 0)
                    { at += keyword.Length; return keyword == "null" ? JValue.CreateNull() : new JValue(keyword == "true"); }
                Match match = Number.Match(text, at);
                if (!match.Success) throw Error("Invalid JSON value");
                at += match.Length;
                return new JRaw(match.Value);
            }
            public JToken ReadDocument()
            {
                JToken value = Value(0); Space();
                if (at != text.Length) throw Error("Unexpected trailing content");
                return value;
            }
        }
    }
}
