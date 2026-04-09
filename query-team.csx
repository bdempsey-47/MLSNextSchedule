#r "nuget: Microsoft.Data.SqlClient, 6.0.1"
using Microsoft.Data.SqlClient;

var conn = new SqlConnection("Server=tcp:yss-sql-prod.database.windows.net,1433;Initial Catalog=yss-prod;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");
conn.AccessToken = Environment.GetEnvironmentVariable("AZURE_SQL_ACCESS_TOKEN");
conn.Open();

var cmd = new SqlCommand("SELECT Id, Name, Program, EloRating FROM Teams WHERE Name LIKE '%Long Island%'", conn);
var r = cmd.ExecuteReader();
while(r.Read())
    Console.WriteLine($"Id={r["Id"]}, Name={r["Name"]}, Program={r["Program"]}, EloRating={r["EloRating"]}");
r.Close();
conn.Close();
