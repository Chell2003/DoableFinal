using DoableFinal.Models;

public class TimelineAdjustmentService
{
    public void AdjustTaskTimelines(IEnumerable<ProjectTask> tasks)
    {
        var orderedTasks = tasks.OrderBy(t => t.StartDate).ToList();
        var now = DateTime.UtcNow;

        for (int i = 0; i < orderedTasks.Count; i++)
        {
            var currentTask = orderedTasks[i];

            // Check if task is overdue and not completed
            if (currentTask.Status != "Completed" && currentTask.DueDate < now)
            {
                // Extend overdue task by one day
                currentTask.DueDate = currentTask.DueDate.AddDays(1);

                // If there are subsequent tasks, adjust their timelines
                if (i < orderedTasks.Count - 1)
                {
                    for (int j = i + 1; j < orderedTasks.Count; j++)
                    {
                        var nextTask = orderedTasks[j];

                        // Only adjust tasks that haven't been completed
                        if (nextTask.Status != "Completed")
                        {
                            // Shift the task's timeline by one day
                            nextTask.StartDate = nextTask.StartDate.AddDays(-1);
                            nextTask.DueDate = nextTask.DueDate.AddDays(-1);

                            // Ensure task duration doesn't become negative
                            if (nextTask.StartDate >= nextTask.DueDate)
                            {
                                nextTask.DueDate = nextTask.StartDate.AddDays(1);
                            }
                        }
                    }
                }
            }
        }
    }
}