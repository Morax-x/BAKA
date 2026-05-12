using System;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace BugalterProject.Data;

public static class DatabaseService
{
    private const string DatabaseConnectionString =
        "Server=127.0.0.1;Port=3306;Database=baka_db;User ID=baka_app;Password=baka123;SslMode=None;";

    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static bool _schemaReady;

    public static async Task<AuthResult> LoginUserAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return new AuthResult(false, "Completeaza email-ul si parola.", null);
        }

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                @"SELECT user_id, first_name, last_name, email, role
                  FROM users
                  WHERE email = @email AND password = @password
                  LIMIT 1;",
                connection);
            command.Parameters.AddWithValue("@email", email);
            command.Parameters.AddWithValue("@password", password);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var user = new AppUser(
                    reader.GetInt32("user_id"),
                    reader.GetString("first_name"),
                    reader.GetString("last_name"),
                    reader.GetString("email"),
                    reader.GetString("role"));

                return new AuthResult(true, "Autentificarea a fost realizata cu succes.", user);
            }

            return new AuthResult(false, "Datele de autentificare sunt incorecte.", null);
        }
        catch (Exception ex)
        {
            return new AuthResult(false, $"Eroare la baza de date: {ex.Message}", null);
        }
    }

    public static async Task<AuthResult> RegisterUserAsync(
        string firstName,
        string lastName,
        string email,
        string password,
        string captcha,
        string expectedCaptcha,
        bool policyAccepted)
    {
        var validation = ValidateRegistration(firstName, lastName, email, password, captcha, expectedCaptcha, policyAccepted);
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
                return new AuthResult(false, "Email-ul este deja folosit.", null);
            }

            await using var insert = new MySqlCommand(
                @"INSERT INTO users (first_name, last_name, email, password, role)
                  VALUES (@first_name, @last_name, @email, @password, 'user');",
                connection);
            insert.Parameters.AddWithValue("@first_name", firstName);
            insert.Parameters.AddWithValue("@last_name", lastName);
            insert.Parameters.AddWithValue("@email", email);
            insert.Parameters.AddWithValue("@password", password);

            await insert.ExecuteNonQueryAsync();

            await using var userLookup = new MySqlCommand(
                @"SELECT user_id, first_name, last_name, email, role
                  FROM users
                  WHERE email = @email
                  LIMIT 1;",
                connection);
            userLookup.Parameters.AddWithValue("@email", email);

            await using var reader = await userLookup.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var user = new AppUser(
                    reader.GetInt32("user_id"),
                    reader.GetString("first_name"),
                    reader.GetString("last_name"),
                    reader.GetString("email"),
                    reader.GetString("role"));

                return new AuthResult(true, "Inregistrarea a fost realizata cu succes.", user);
            }

            return new AuthResult(true, "Inregistrarea a fost realizata cu succes.", null);
        }
        catch (Exception ex)
        {
            return new AuthResult(false, $"Eroare la baza de date: {ex.Message}", null);
        }
    }

    public static async Task<OperationResult> DeleteCurrentUserAsync(int userId)
    {
        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var deleteCommands = new[]
                {
                    "DELETE FROM expenses WHERE user_id = @user_id;",
                    "DELETE FROM debts WHERE user_id = @user_id;",
                    "DELETE FROM utilities WHERE user_id = @user_id;",
                    "DELETE FROM users WHERE user_id = @user_id;"
                };

                foreach (var commandText in deleteCommands)
                {
                    await using var command = new MySqlCommand(commandText, connection, transaction);
                    command.Parameters.AddWithValue("@user_id", userId);
                    await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return new OperationResult(true, "Contul a fost sters cu succes.");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Eroare la stergerea contului: {ex.Message}");
        }
    }

    private static AuthResult ValidateRegistration(
        string firstName,
        string lastName,
        string email,
        string password,
        string captcha,
        string expectedCaptcha,
        bool policyAccepted)
    {
        if (string.IsNullOrWhiteSpace(firstName) ||
            string.IsNullOrWhiteSpace(lastName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            return new AuthResult(false, "Completeaza toate campurile.", null);
        }

        if (!email.Contains('@') || !email.Contains('.'))
        {
            return new AuthResult(false, "Introdu un email valid.", null);
        }

        if (!string.Equals(captcha.Trim(), expectedCaptcha.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return new AuthResult(false, "Captcha introdus este gresit.", null);
        }

        if (!policyAccepted)
        {
            return new AuthResult(false, "Accepta politica companiei.", null);
        }

        return new AuthResult(true, "Datele sunt valide.", null);
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
                        password VARCHAR(100) NOT NULL,
                        role VARCHAR(20) NOT NULL DEFAULT 'user'
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

                await EnsureUsersRoleColumnAsync(databaseConnection);

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

                await using var seedAdmin = new MySqlCommand(
                    @"INSERT IGNORE INTO users (first_name, last_name, email, password, role)
                      VALUES ('Admin', 'BAKA', 'admin@baka.md', 'admin123', 'admin');",
                    databaseConnection);
                await seedAdmin.ExecuteNonQueryAsync();

                await using var seedUser = new MySqlCommand(
                    @"INSERT IGNORE INTO users (first_name, last_name, email, password, role)
                      VALUES ('Utilizator', 'BAKA', 'user@baka.md', 'user123', 'user');",
                    databaseConnection);
                await seedUser.ExecuteNonQueryAsync();
            }

            _schemaReady = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    private static async Task EnsureUsersRoleColumnAsync(MySqlConnection connection)
    {
        await using var checkColumn = new MySqlCommand(
            @"SELECT COUNT(*)
              FROM INFORMATION_SCHEMA.COLUMNS
              WHERE TABLE_SCHEMA = DATABASE()
                AND TABLE_NAME = 'users'
                AND COLUMN_NAME = 'role';",
            connection);

        var columnExists = Convert.ToInt64(await checkColumn.ExecuteScalarAsync());
        if (columnExists == 0)
        {
            await using var alter = new MySqlCommand(
                "ALTER TABLE users ADD COLUMN role VARCHAR(20) NOT NULL DEFAULT 'user' AFTER password;",
                connection);
            await alter.ExecuteNonQueryAsync();
        }
    }
}
