using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace BugalterProject.Data;

public sealed record OperationResult(bool Success, string Message);

public static class DatabaseService
{
    private const string ServerConnectionString =
        "Server=127.0.0.1;Port=3306;User ID=root;Password=;SslMode=None;";

    private const string DatabaseName = "baka_db";

    private const string DatabaseConnectionString =
        "Server=127.0.0.1;Port=3306;Database=baka_db;User ID=root;Password=;SslMode=None;";

    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static bool _schemaReady;

    public static async Task<OperationResult> RegisterUserAsync(
        string firstName,
        string lastName,
        string email,
        string password,
        string captcha,
        bool policyAccepted)
    {
        var validation = ValidateRegistration(firstName, lastName, email, password, captcha, policyAccepted);
        if (!validation.Success)
        {
            return validation;
        }

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            await using var emailCheck = new MySqlCommand(
                "SELECT COUNT(*) FROM users WHERE email = @email;",
                connection);
            emailCheck.Parameters.AddWithValue("@email", email);

            var emailCount = Convert.ToInt64(await emailCheck.ExecuteScalarAsync());
            if (emailCount > 0)
            {
                return new OperationResult(false, "Email-ul este deja folosit.");
            }

            await using var insert = new MySqlCommand(
                @"INSERT INTO users (first_name, last_name, email, password)
                  VALUES (@first_name, @last_name, @email, @password);",
                connection);
            insert.Parameters.AddWithValue("@first_name", firstName);
            insert.Parameters.AddWithValue("@last_name", lastName);
            insert.Parameters.AddWithValue("@email", email);
            insert.Parameters.AddWithValue("@password", password);

            await insert.ExecuteNonQueryAsync();

            return new OperationResult(true, "Inregistrarea a fost realizata cu succes.");
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Eroare la baza de date: {ex.Message}");
        }
    }

    private static OperationResult ValidateRegistration(
        string firstName,
        string lastName,
        string email,
        string password,
        string captcha,
        bool policyAccepted)
    {
        if (string.IsNullOrWhiteSpace(firstName) ||
            string.IsNullOrWhiteSpace(lastName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            return new OperationResult(false, "Completeaza toate campurile.");
        }

        if (!email.Contains('@') || !email.Contains('.'))
        {
            return new OperationResult(false, "Introdu un email valid.");
        }

        if (!string.Equals(captcha.Trim(), "BAKA", StringComparison.OrdinalIgnoreCase))
        {
            return new OperationResult(false, "Captcha introdus este gresit.");
        }

        if (!policyAccepted)
        {
            return new OperationResult(false, "Accepta politica companiei.");
        }

        return new OperationResult(true, "Datele sunt valide.");
    }

    public static async Task EnsureSchemaAsync()
    {
        if (_schemaReady)
        {
            return;
        }

        await SchemaLock.WaitAsync();
        try
        {
            if (_schemaReady)
            {
                return;
            }

            await using (var serverConnection = new MySqlConnection(ServerConnectionString))
            {
                await serverConnection.OpenAsync();

                await using var createDatabase = new MySqlCommand(
                    $"CREATE DATABASE IF NOT EXISTS {DatabaseName} CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;",
                    serverConnection);
                await createDatabase.ExecuteNonQueryAsync();
            }

            await using (var databaseConnection = new MySqlConnection(DatabaseConnectionString))
            {
                await databaseConnection.OpenAsync();

                var schemaCommands = new[]
                {
                    @"CREATE TABLE IF NOT EXISTS users (
                        user_id INT AUTO_INCREMENT PRIMARY KEY,
                        first_name VARCHAR(50) NOT NULL,
                        last_name VARCHAR(50) NOT NULL,
                        email VARCHAR(100) NOT NULL UNIQUE,
                        password VARCHAR(100) NOT NULL
                    );",
                    @"CREATE TABLE IF NOT EXISTS categories (
                        category_id INT AUTO_INCREMENT PRIMARY KEY,
                        category_name VARCHAR(50) NOT NULL UNIQUE
                    );",
                    @"CREATE TABLE IF NOT EXISTS expenses (
                        expense_id INT AUTO_INCREMENT PRIMARY KEY,
                        user_id INT NOT NULL,
                        category_id INT NOT NULL,
                        amount DECIMAL(10,2) NOT NULL,
                        description VARCHAR(100),
                        expense_date DATE NOT NULL,
                        FOREIGN KEY (user_id) REFERENCES users(user_id),
                        FOREIGN KEY (category_id) REFERENCES categories(category_id)
                    );",
                    @"CREATE TABLE IF NOT EXISTS debts (
                        debt_id INT AUTO_INCREMENT PRIMARY KEY,
                        user_id INT NOT NULL,
                        person_name VARCHAR(100) NOT NULL,
                        debt_type ENUM('eu_datorez', 'mie_datoreaza') NOT NULL,
                        amount DECIMAL(10,2) NOT NULL,
                        due_date DATE NOT NULL,
                        FOREIGN KEY (user_id) REFERENCES users(user_id)
                    );",
                    @"CREATE TABLE IF NOT EXISTS utilities (
                        utility_id INT AUTO_INCREMENT PRIMARY KEY,
                        user_id INT NOT NULL,
                        utility_name ENUM('apa', 'lumina', 'gaz', 'internet') NOT NULL,
                        amount DECIMAL(10,2) NOT NULL,
                        utility_date DATE NOT NULL,
                        FOREIGN KEY (user_id) REFERENCES users(user_id)
                    );"
                };

                foreach (var commandText in schemaCommands)
                {
                    await using var command = new MySqlCommand(commandText, databaseConnection);
                    await command.ExecuteNonQueryAsync();
                }

                await using var seedCategories = new MySqlCommand(
                    @"INSERT IGNORE INTO categories (category_name) VALUES
                      ('Mancare'),
                      ('Transport'),
                      ('Abonamente'),
                      ('Servicii comunale'),
                      ('Sanatate'),
                      ('Altele');",
                    databaseConnection);
                await seedCategories.ExecuteNonQueryAsync();
            }

            _schemaReady = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }
}
