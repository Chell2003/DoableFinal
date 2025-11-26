using System;
using System.ComponentModel.DataAnnotations;

namespace DoableFinal.Validation
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class RequiredIfAttribute : ValidationAttribute
    {
        private readonly string _propertyName;
        private readonly string[] _allowedValues;

        public RequiredIfAttribute(string propertyName, string[] allowedValues)
        {
            _propertyName = propertyName;
            _allowedValues = allowedValues;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            // Get the property we're checking against
            var property = validationContext.ObjectType.GetProperty(_propertyName);
            if (property == null)
            {
                return ValidationResult.Success;
            }

            var propertyValue = property.GetValue(validationContext.ObjectInstance);
            
            // If propertyValue is null or empty, no validation needed
            if (propertyValue == null)
            {
                return ValidationResult.Success;
            }

            string roleValue = propertyValue.ToString();
            
            // Check if the property value is in our allowed values
            bool shouldBeRequired = _allowedValues.Contains(roleValue);

            if (shouldBeRequired)
            {
                // If required, check that the current value is not null or empty
                if (value == null)
                {
                    return new ValidationResult(ErrorMessage ?? $"This field is required.");
                }

                if (value is string str && string.IsNullOrWhiteSpace(str))
                {
                    return new ValidationResult(ErrorMessage ?? $"This field is required.");
                }
            }

            return ValidationResult.Success;
        }
    }
}
