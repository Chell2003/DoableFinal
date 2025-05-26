using System;
using System.ComponentModel.DataAnnotations;
using DoableFinal.Models;
using DoableFinal.Data;
using Microsoft.EntityFrameworkCore;

namespace DoableFinal.Validation
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ProjectDateRangeAttribute : ValidationAttribute
    {
        private readonly string _projectIdPropertyName;
        private readonly bool _isStartDate;

        public ProjectDateRangeAttribute(string projectIdPropertyName, bool isStartDate)
        {
            _projectIdPropertyName = projectIdPropertyName;
            _isStartDate = isStartDate;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var date = (DateTime)value;
            var projectIdProperty = validationContext.ObjectType.GetProperty(_projectIdPropertyName);
            var projectId = (int)projectIdProperty.GetValue(validationContext.ObjectInstance);

            var dbContext = (ApplicationDbContext)validationContext.GetService(typeof(ApplicationDbContext));
            var project = dbContext.Projects.Find(projectId);

            if (project == null)
                return new ValidationResult("Project not found");

            if (_isStartDate)
            {
                if (date < project.StartDate)
                    return new ValidationResult("Task start date cannot be before project start date");
                if (project.EndDate.HasValue && date > project.EndDate.Value)
                    return new ValidationResult("Task start date cannot be after project end date");
            }
            else
            {
                if (date < project.StartDate)
                    return new ValidationResult("Task due date cannot be before project start date");
                if (project.EndDate.HasValue && date > project.EndDate.Value)
                    return new ValidationResult("Task due date cannot be after project end date");
            }

            return ValidationResult.Success;
        }
    }
}
