using System;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace ConfigurationUI
{
    /// <summary>Spin exact JSON decimals without rounding the untouched digits through a float/double.</summary>
    public static class JsonNumberStepper
    {
        private static readonly Regex Parts = new Regex(@"^(-?)([0-9]+)(?:\.([0-9]+))?(?:[eE]([+-]?[0-9]+))?$", RegexOptions.CultureInvariant);
        private static (BigInteger coefficient, int power) Read(string value)
        {
            var match = Parts.Match(value);
            if (!match.Success) throw new ArgumentException("Enter a JSON number.");
            int exponent = match.Groups[4].Success ? int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) : 0;
            string fraction = match.Groups[3].Value;
            return (BigInteger.Parse(match.Groups[1].Value + match.Groups[2].Value + fraction, CultureInfo.InvariantCulture), exponent - fraction.Length);
        }
        public static JToken Step(JToken value, string step, int direction, int stepPower = 0)
        {
            var a = Read(value.ToString()); var b = Read(step); b = (b.coefficient, b.power + stepPower); int power = Math.Min(a.power, b.power);
            if (Math.Abs((long)power) > 4096) throw new ArgumentException("This exponent exceeds the spin range. Edit its exact value in JSON.");
            if (Math.Abs((long)a.power - b.power) > 4096) throw new ArgumentException("This number exceeds the spin control range. Its exact value remains editable in JSON.");
            BigInteger coefficient = a.coefficient * BigInteger.Pow(10, a.power - power) + direction * b.coefficient * BigInteger.Pow(10, b.power - power);
            if (coefficient.IsZero) return new JRaw("0");
            bool negative = coefficient.Sign < 0; string digits = BigInteger.Abs(coefficient).ToString(CultureInfo.InvariantCulture);
            string result;
            if (power >= 0) result = digits + new string('0', power);
            else
            {
                int point = digits.Length + power;
                result = point > 0 ? digits.Insert(point, ".") : "0." + new string('0', -point) + digits;
                result = result.TrimEnd('0').TrimEnd('.');
            }
            return new JRaw((negative ? "-" : "") + result);
        }
    }
}
