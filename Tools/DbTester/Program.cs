using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

class Program
{
	static async Task<int> Main(string[] args)
	{
		var connectionString = "Server=(localdb)\\mssqllocaldb;Database=DoableFinal;Trusted_Connection=True;MultipleActiveResultSets=true;ConnectRetryCount=5;ConnectRetryInterval=10;Encrypt=False";

		Console.WriteLine("Using connection: " + connectionString);

		try
		{
			await using var conn = new SqlConnection(connectionString);
			await conn.OpenAsync();

			// get an existing user id
			var getUserCmd = new SqlCommand("SELECT TOP 1 Id FROM AspNetUsers", conn);
			var userIdObj = await getUserCmd.ExecuteScalarAsync();
			if (userIdObj == null)
			{
				Console.WriteLine("No users found in AspNetUsers table. Cannot create ticket without CreatedById.");
				return 2;
			}
			var userId = userIdObj.ToString();
			Console.WriteLine("Found user id: " + userId);

			// insert ticket
			var insertCmd = new SqlCommand(@"
INSERT INTO Tickets (Title, Description, Priority, Status, Type, CreatedById, CreatedAt)
VALUES (@title, @desc, @priority, @status, @type, @createdById, GETUTCDATE())", conn);

			insertCmd.Parameters.AddWithValue("@title", "Test Insert from DbTester " + DateTime.UtcNow.ToString("s"));
			insertCmd.Parameters.AddWithValue("@desc", "Automated test insert to verify DB persistence.");
			insertCmd.Parameters.AddWithValue("@priority", "Low");
			insertCmd.Parameters.AddWithValue("@status", "Open");
			insertCmd.Parameters.AddWithValue("@type", "Support");
			insertCmd.Parameters.AddWithValue("@createdById", userId);

			var rows = await insertCmd.ExecuteNonQueryAsync();
			Console.WriteLine($"Insert rows affected: {rows}");

			// show most recent tickets
			var selectCmd = new SqlCommand("SELECT TOP 5 Id, Title, CreatedById, CreatedAt FROM Tickets ORDER BY CreatedAt DESC", conn);
			await using var reader = await selectCmd.ExecuteReaderAsync();
			Console.WriteLine("Recent tickets:");
			while (await reader.ReadAsync())
			{
				Console.WriteLine($"Id={reader.GetInt32(0)}, Title={reader.GetString(1)}, CreatedById={reader.GetString(2)}, CreatedAt={reader.GetDateTime(3)}");
			}

			return 0;
		}
		catch (Exception ex)
		{
			Console.WriteLine("Exception: " + ex.Message);
			Console.WriteLine(ex.StackTrace);
			return 1;
		}
	}
}
