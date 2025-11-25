using System;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace DoableFinal.Validation
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class TinAttribute : ValidationAttribute
    {
        private static readonly Regex _patternA = new Regex("^\\d{3}-\\d{3}-\\d{3}$", RegexOptions.Compiled);
        private static readonly Regex _patternB = new Regex("^\\d{3}-\\d{3}-\\d{3}-\\d{3}$", RegexOptions.Compiled);

        public override bool IsValid(object value)
        {
            if (value == null) return true; // Let [Required] handle presence
            var s = value as string;
            if (string.IsNullOrWhiteSpace(s)) return true;
            return _patternA.IsMatch(s) || _patternB.IsMatch(s);
        }
    }
}
