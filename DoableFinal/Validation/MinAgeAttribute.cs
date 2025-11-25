using System;
using System.ComponentModel.DataAnnotations;

namespace DoableFinal.Validation
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class MinAgeAttribute : ValidationAttribute
    {
        private readonly int _minAge;

        public MinAgeAttribute(int minAge)
        {
            _minAge = minAge;
        }

        public override bool IsValid(object value)
        {
            if (value == null)
            {
                // If value is not supplied, don't invalidate here (use [Required] for mandatory check)
                return true;
            }

            if (value is DateTime dt)
            {
                var today = DateTime.Today;
                var age = today.Year - dt.Year;
                if (dt.Date > today.AddYears(-age)) age--;
                return age >= _minAge;
            }

            return false;
        }
    }
}
