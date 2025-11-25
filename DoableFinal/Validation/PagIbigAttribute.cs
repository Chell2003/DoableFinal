using System;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace DoableFinal.Validation
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class PagIbigAttribute : ValidationAttribute
    {
        private static readonly Regex _regex = new Regex("^\\d{12}$", RegexOptions.Compiled);

        public override bool IsValid(object value)
        {
            if (value == null) return true; // Let [Required] handle presence
            var s = value as string;
            if (string.IsNullOrWhiteSpace(s)) return true;
            return _regex.IsMatch(s);
        }
    }
}
