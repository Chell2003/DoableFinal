using DoableFinal.Models;

public class TimelineAdjustmentService
{
    public void AdjustTaskTimelines(IEnumerable<ProjectTask> tasks)
    {
        ProjectTask? previousTask = null;

        foreach (var task in tasks.OrderBy(t => t.StartDate))
        {
            if (previousTask != null && previousTask.Status != "Completed" && previousTask.DueDate > task.StartDate)
            {
                // Extend the previous task's duration
                previousTask.DueDate = previousTask.DueDate.AddDays((previousTask.DueDate - task.StartDate).Days);

                // Shorten the current task's duration to compensate
                var overlapDays = (previousTask.DueDate - task.StartDate).Days;
                task.StartDate = previousTask.DueDate;
                task.DueDate = task.DueDate.AddDays(-overlapDays);

                // Ensure the task's duration is not negative
                if (task.DueDate < task.StartDate)
                {
                    task.DueDate = task.StartDate;
                }
            }

            previousTask = task;
        }
    }
}