using System;
using Microsoft.Data.Sqlite;

var path = @"E:\100.Work\NestCoreProject\AutoCinema\autocinema.db";
using var connection = new SqliteConnection($"Data Source={path}");
connection.Open();

var command = connection.CreateCommand();
command.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
using var reader = command.ExecuteReader();
while (reader.Read())
{
    Console.WriteLine($"Table: {reader.GetString(0)}");
}
