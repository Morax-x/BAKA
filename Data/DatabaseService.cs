using System;
using System.Collections.Generic;
using System.Linq;
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

    public static async Task<List<AppUser>> GetAllUsersAsync()
    {
        var users = new List<AppUser>();

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                @"SELECT user_id, first_name, last_name, email, role
                  FROM users
                  ORDER BY user_id;",
                connection);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(new AppUser(
                    reader.GetInt32("user_id"),
                    reader.GetString("first_name"),
                    reader.GetString("last_name"),
                    reader.GetString("email"),
                    reader.GetString("role")));
            }
        }
        catch
        {
            return users;
        }

        return users;
    }

    public static async Task<List<ExpenseCategory>> GetExpenseCategoriesAsync()
    {
        var categories = new List<ExpenseCategory>();

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                @"SELECT category_id, category_name
                  FROM categories
                  ORDER BY category_name;",
                connection);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new ExpenseCategory(
                    reader.GetInt32("category_id"),
                    reader.GetString("category_name")));
            }
        }
        catch
        {
            return categories;
        }

        return categories;
    }

    public static async Task<List<ExpenseItem>> GetExpensesAsync(int? userId = null)
    {
        var expenses = new List<ExpenseItem>();

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            var sql = userId.HasValue
                ? @"SELECT e.expense_id,
                           e.user_id,
                           CONCAT(u.first_name, ' ', u.last_name) AS owner_name,
                           e.category_id,
                           c.category_name,
                           e.amount,
                           e.description,
                           e.expense_date
                    FROM expenses e
                    INNER JOIN users u ON e.user_id = u.user_id
                    INNER JOIN categories c ON e.category_id = c.category_id
                    WHERE e.user_id = @user_id
                    ORDER BY e.expense_date DESC, e.expense_id DESC;"
                : @"SELECT e.expense_id,
                           e.user_id,
                           CONCAT(u.first_name, ' ', u.last_name) AS owner_name,
                           e.category_id,
                           c.category_name,
                           e.amount,
                           e.description,
                           e.expense_date
                    FROM expenses e
                    INNER JOIN users u ON e.user_id = u.user_id
                    INNER JOIN categories c ON e.category_id = c.category_id
                    ORDER BY e.expense_date DESC, e.expense_id DESC;";

            await using var command = new MySqlCommand(sql, connection);
            if (userId.HasValue)
            {
                command.Parameters.AddWithValue("@user_id", userId.Value);
            }

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(new ExpenseItem(
                    reader.GetInt32("expense_id"),
                    reader.GetInt32("user_id"),
                    reader.GetString("owner_name"),
                    reader.GetInt32("category_id"),
                    reader.GetString("category_name"),
                    reader.GetDecimal("amount"),
                    reader.IsDBNull(reader.GetOrdinal("description")) ? string.Empty : reader.GetString("description"),
                    reader.GetDateTime("expense_date")));
            }
        }
        catch
        {
            return expenses;
        }

        return expenses;
    }

    public static async Task<OperationResult> AddExpenseAsync(
        int userId,
        int categoryId,
        decimal amount,
        string description,
        DateTime expenseDate)
    {
        if (userId <= 0 || categoryId <= 0 || amount <= 0)
        {
            return new OperationResult(false, "Datele pentru cheltuiala sunt invalide.");
        }

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                @"INSERT INTO expenses (user_id, category_id, amount, description, expense_date)
                  VALUES (@user_id, @category_id, @amount, @description, @expense_date);",
                connection);

            command.Parameters.AddWithValue("@user_id", userId);
            command.Parameters.AddWithValue("@category_id", categoryId);
            command.Parameters.AddWithValue("@amount", amount);
            command.Parameters.AddWithValue("@description", string.IsNullOrWhiteSpace(description) ? DBNull.Value : description.Trim());
            command.Parameters.AddWithValue("@expense_date", expenseDate.Date);

            await command.ExecuteNonQueryAsync();
            return new OperationResult(true, "Cheltuiala a fost adaugata cu succes.");
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Eroare la adaugarea cheltuielii: {ex.Message}");
        }
    }

    public static async Task<OperationResult> UpdateExpenseAsync(
        int expenseId,
        int categoryId,
        decimal amount,
        string description,
        DateTime expenseDate)
    {
        if (expenseId <= 0 || categoryId <= 0 || amount <= 0)
        {
            return new OperationResult(false, "Datele pentru actualizare sunt invalide.");
        }

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                @"UPDATE expenses
                  SET category_id = @category_id,
                      amount = @amount,
                      description = @description,
                      expense_date = @expense_date
                  WHERE expense_id = @expense_id;",
                connection);

            command.Parameters.AddWithValue("@expense_id", expenseId);
            command.Parameters.AddWithValue("@category_id", categoryId);
            command.Parameters.AddWithValue("@amount", amount);
            command.Parameters.AddWithValue("@description", string.IsNullOrWhiteSpace(description) ? DBNull.Value : description.Trim());
            command.Parameters.AddWithValue("@expense_date", expenseDate.Date);

            var affectedRows = await command.ExecuteNonQueryAsync();
            if (affectedRows == 0)
            {
                return new OperationResult(false, "Cheltuiala nu a fost gasita.");
            }

            return new OperationResult(true, "Cheltuiala a fost actualizata cu succes.");
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Eroare la actualizarea cheltuielii: {ex.Message}");
        }
    }

    public static async Task<OperationResult> DeleteExpenseAsync(int expenseId)
    {
        if (expenseId <= 0)
        {
            return new OperationResult(false, "Cheltuiala invalida.");
        }

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                "DELETE FROM expenses WHERE expense_id = @expense_id;",
                connection);
            command.Parameters.AddWithValue("@expense_id", expenseId);

            var affectedRows = await command.ExecuteNonQueryAsync();
            if (affectedRows == 0)
            {
                return new OperationResult(false, "Cheltuiala nu a fost gasita.");
            }

            return new OperationResult(true, "Cheltuiala a fost stearsa cu succes.");
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Eroare la stergerea cheltuielii: {ex.Message}");
        }
    }

    public static async Task<OperationResult> ClearHistoryAsync(int? userId = null)
    {
        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var commands = userId.HasValue
                    ? new[]
                    {
                        "DELETE FROM expenses WHERE user_id = @user_id;",
                        "DELETE FROM debts WHERE user_id = @user_id AND is_settled = 1;",
                        "DELETE FROM utilities WHERE user_id = @user_id AND is_paid = 1;"
                    }
                    : new[]
                    {
                        "DELETE FROM expenses;",
                        "DELETE FROM debts WHERE is_settled = 1;",
                        "DELETE FROM utilities WHERE is_paid = 1;"
                    };

                foreach (var commandText in commands)
                {
                    await using var command = new MySqlCommand(commandText, connection, transaction);
                    if (userId.HasValue)
                    {
                        command.Parameters.AddWithValue("@user_id", userId.Value);
                    }

                    await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return new OperationResult(true, "Istoricul a fost golit cu succes.");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Eroare la golirea istoricului: {ex.Message}");
        }
    }

    public static async Task<List<DebtItem>> GetDebtsAsync(int? userId = null, bool includeSettled = false)
    {
        var debts = new List<DebtItem>();

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            var sql = userId.HasValue
                ? @"SELECT d.debt_id,
                           d.user_id,
                           CONCAT(u.first_name, ' ', u.last_name) AS owner_name,
                           d.person_name,
                           d.debt_type,
                           d.amount,
                           d.original_amount,
                           d.due_date,
                           d.is_settled,
                           d.settled_date
                    FROM debts d
                    INNER JOIN users u ON d.user_id = u.user_id
                    WHERE d.user_id = @user_id
                      AND (@include_settled = 1 OR d.is_settled = 0)
                    ORDER BY d.due_date ASC, d.debt_id DESC;"
                : @"SELECT d.debt_id,
                           d.user_id,
                           CONCAT(u.first_name, ' ', u.last_name) AS owner_name,
                           d.person_name,
                           d.debt_type,
                           d.amount,
                           d.original_amount,
                           d.due_date,
                           d.is_settled,
                           d.settled_date
                    FROM debts d
                    INNER JOIN users u ON d.user_id = u.user_id
                    WHERE (@include_settled = 1 OR d.is_settled = 0)
                    ORDER BY d.due_date ASC, d.debt_id DESC;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@include_settled", includeSettled);
            if (userId.HasValue)
            {
                command.Parameters.AddWithValue("@user_id", userId.Value);
            }

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                debts.Add(new DebtItem(
                    reader.GetInt32("debt_id"),
                    reader.GetInt32("user_id"),
                    reader.GetString("owner_name"),
                    reader.GetString("person_name"),
                    reader.GetString("debt_type"),
                    reader.GetDecimal("amount"),
                    reader.GetDecimal("original_amount"),
                    reader.GetDateTime("due_date"),
                    reader.GetBoolean("is_settled"),
                    reader.IsDBNull(reader.GetOrdinal("settled_date")) ? null : reader.GetDateTime("settled_date")));
            }
        }
        catch
        {
            return debts;
        }

        return debts;
    }

    public static async Task<OperationResult> AddDebtAsync(
        int userId,
        string personName,
        string debtType,
        decimal amount,
        DateTime dueDate)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(personName) || amount <= 0)
        {
            return new OperationResult(false, "Datele pentru datorie sunt invalide.");
        }

        if (!string.Equals(debtType, "eu_datorez", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(debtType, "mie_datoreaza", StringComparison.OrdinalIgnoreCase))
        {
            return new OperationResult(false, "Tipul datoriei este invalid.");
        }

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                @"INSERT INTO debts (user_id, person_name, debt_type, amount, original_amount, due_date, is_settled, settled_date)
                  VALUES (@user_id, @person_name, @debt_type, @amount, @original_amount, @due_date, 0, NULL);",
                connection);

            command.Parameters.AddWithValue("@user_id", userId);
            command.Parameters.AddWithValue("@person_name", personName.Trim());
            command.Parameters.AddWithValue("@debt_type", debtType.ToLowerInvariant());
            command.Parameters.AddWithValue("@amount", amount);
            command.Parameters.AddWithValue("@original_amount", amount);
            command.Parameters.AddWithValue("@due_date", dueDate.Date);

            await command.ExecuteNonQueryAsync();
            return new OperationResult(true, "Datoria a fost adaugata cu succes.");
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Eroare la adaugarea datoriei: {ex.Message}");
        }
    }

    public static async Task<OperationResult> UpdateDebtAsync(
        int debtId,
        string personName,
        string debtType,
        decimal amount,
        DateTime dueDate)
    {
        if (debtId <= 0 || string.IsNullOrWhiteSpace(personName) || amount <= 0)
        {
            return new OperationResult(false, "Datele pentru actualizare sunt invalide.");
        }

        if (!string.Equals(debtType, "eu_datorez", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(debtType, "mie_datoreaza", StringComparison.OrdinalIgnoreCase))
        {
            return new OperationResult(false, "Tipul datoriei este invalid.");
        }

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                @"UPDATE debts
                  SET person_name = @person_name,
                      debt_type = @debt_type,
                      amount = @amount,
                      original_amount = @original_amount,
                      due_date = @due_date
                  WHERE debt_id = @debt_id;",
                connection);

            command.Parameters.AddWithValue("@debt_id", debtId);
            command.Parameters.AddWithValue("@person_name", personName.Trim());
            command.Parameters.AddWithValue("@debt_type", debtType.ToLowerInvariant());
            command.Parameters.AddWithValue("@amount", amount);
            command.Parameters.AddWithValue("@original_amount", amount);
            command.Parameters.AddWithValue("@due_date", dueDate.Date);

            var affectedRows = await command.ExecuteNonQueryAsync();
            if (affectedRows == 0)
            {
                return new OperationResult(false, "Datoria nu a fost gasita.");
            }

            return new OperationResult(true, "Datoria a fost actualizata cu succes.");
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Eroare la actualizarea datoriei: {ex.Message}");
        }
    }

    public static async Task<OperationResult> DeleteDebtAsync(int debtId)
    {
        if (debtId <= 0)
        {
            return new OperationResult(false, "Datoria este invalida.");
        }

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                "DELETE FROM debts WHERE debt_id = @debt_id;",
                connection);
            command.Parameters.AddWithValue("@debt_id", debtId);

            var affectedRows = await command.ExecuteNonQueryAsync();
            if (affectedRows == 0)
            {
                return new OperationResult(false, "Datoria nu a fost gasita.");
            }

            return new OperationResult(true, "Datoria a fost stearsa cu succes.");
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Eroare la stergerea datoriei: {ex.Message}");
        }
    }

    public static async Task<OperationResult> MarkDebtSettledAsync(int debtId)
    {
        if (debtId <= 0)
        {
            return new OperationResult(false, "Datoria este invalida.");
        }

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                @"UPDATE debts
                  SET is_settled = 1,
                      settled_date = @settled_date
                  WHERE debt_id = @debt_id;",
                connection);
            command.Parameters.AddWithValue("@debt_id", debtId);
            command.Parameters.AddWithValue("@settled_date", DateTime.Today);

            var affectedRows = await command.ExecuteNonQueryAsync();
            if (affectedRows == 0)
            {
                return new OperationResult(false, "Datoria nu a fost gasita.");
            }

            return new OperationResult(true, "Datoria a fost marcata ca stinsa.");
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Eroare la inchiderea datoriei: {ex.Message}");
        }
    }

    public static async Task<OperationResult> ApplyDebtPaymentAsync(int debtId, decimal paymentAmount)
    {
        if (debtId <= 0 || paymentAmount <= 0)
        {
            return new OperationResult(false, "Suma pentru plata este invalida.");
        }

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                await using var load = new MySqlCommand(
                    @"SELECT amount, original_amount, is_settled
                      FROM debts
                      WHERE debt_id = @debt_id
                      LIMIT 1;",
                    connection,
                    transaction);
                load.Parameters.AddWithValue("@debt_id", debtId);

                await using var reader = await load.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return new OperationResult(false, "Datoria nu a fost gasita.");
                }

                var currentAmount = reader.GetDecimal("amount");
                var originalAmount = reader.GetDecimal("original_amount");
                var isSettled = reader.GetBoolean("is_settled");
                await reader.CloseAsync();

                if (isSettled)
                {
                    return new OperationResult(false, "Datoria este deja stinsa.");
                }

                var remainingAmount = currentAmount - paymentAmount;
                var shouldSettle = remainingAmount <= 0;
                var amountToStore = shouldSettle ? 0m : remainingAmount;

                await using var update = new MySqlCommand(
                    @"UPDATE debts
                      SET amount = @amount,
                          original_amount = @original_amount,
                          is_settled = @is_settled,
                          settled_date = @settled_date
                      WHERE debt_id = @debt_id;",
                    connection,
                    transaction);
                update.Parameters.AddWithValue("@amount", amountToStore);
                update.Parameters.AddWithValue("@original_amount", originalAmount <= 0 ? currentAmount : originalAmount);
                update.Parameters.AddWithValue("@is_settled", shouldSettle);
                update.Parameters.AddWithValue("@settled_date", shouldSettle ? DateTime.Today : DBNull.Value);
                update.Parameters.AddWithValue("@debt_id", debtId);
                await update.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                return shouldSettle
                    ? new OperationResult(true, "Datoria a fost stinsa complet.")
                    : new OperationResult(true, $"Plata a fost aplicata. Restul ramas este {amountToStore:0.00} MDL.");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Eroare la aplicarea platii: {ex.Message}");
        }
    }

    public static async Task<List<UtilityItem>> GetUtilitiesAsync(int? userId = null, bool includePaid = false)
    {
        var utilities = new List<UtilityItem>();

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            var sql = userId.HasValue
                ? @"SELECT ut.utility_id,
                           ut.user_id,
                           CONCAT(u.first_name, ' ', u.last_name) AS owner_name,
                           ut.utility_name,
                           ut.amount,
                           ut.utility_date,
                           ut.is_paid,
                           ut.paid_date
                    FROM utilities ut
                    INNER JOIN users u ON ut.user_id = u.user_id
                    WHERE ut.user_id = @user_id
                      AND (@include_paid = 1 OR ut.is_paid = 0)
                    ORDER BY ut.utility_date DESC, ut.utility_id DESC;"
                : @"SELECT ut.utility_id,
                           ut.user_id,
                           CONCAT(u.first_name, ' ', u.last_name) AS owner_name,
                           ut.utility_name,
                           ut.amount,
                           ut.utility_date,
                           ut.is_paid,
                           ut.paid_date
                    FROM utilities ut
                    INNER JOIN users u ON ut.user_id = u.user_id
                    WHERE (@include_paid = 1 OR ut.is_paid = 0)
                    ORDER BY ut.utility_date DESC, ut.utility_id DESC;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@include_paid", includePaid);
            if (userId.HasValue)
            {
                command.Parameters.AddWithValue("@user_id", userId.Value);
            }

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                utilities.Add(new UtilityItem(
                    reader.GetInt32("utility_id"),
                    reader.GetInt32("user_id"),
                    reader.GetString("owner_name"),
                    reader.GetString("utility_name"),
                    reader.GetDecimal("amount"),
                    reader.GetDateTime("utility_date"),
                    reader.GetBoolean("is_paid"),
                    reader.IsDBNull(reader.GetOrdinal("paid_date")) ? null : reader.GetDateTime("paid_date")));
            }
        }
        catch
        {
            return utilities;
        }

        return utilities;
    }

    public static async Task<OperationResult> AddUtilityAsync(
        int userId,
        string utilityName,
        decimal amount,
        DateTime utilityDate)
    {
        if (userId <= 0 || amount <= 0)
        {
            return new OperationResult(false, "Datele pentru plata comunala sunt invalide.");
        }

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                @"INSERT INTO utilities (user_id, utility_name, amount, utility_date, is_paid, paid_date)
                  VALUES (@user_id, @utility_name, @amount, @utility_date, 0, NULL);",
                connection);

            command.Parameters.AddWithValue("@user_id", userId);
            command.Parameters.AddWithValue("@utility_name", utilityName.ToLowerInvariant());
            command.Parameters.AddWithValue("@amount", amount);
            command.Parameters.AddWithValue("@utility_date", utilityDate.Date);

            await command.ExecuteNonQueryAsync();
            return new OperationResult(true, "Plata comunala a fost adaugata cu succes.");
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Eroare la adaugarea platii comunale: {ex.Message}");
        }
    }

    public static async Task<OperationResult> UpdateUtilityAsync(
        int utilityId,
        string utilityName,
        decimal amount,
        DateTime utilityDate)
    {
        if (utilityId <= 0 || amount <= 0)
        {
            return new OperationResult(false, "Datele pentru actualizare sunt invalide.");
        }

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                @"UPDATE utilities
                  SET utility_name = @utility_name,
                      amount = @amount,
                      utility_date = @utility_date
                  WHERE utility_id = @utility_id;",
                connection);

            command.Parameters.AddWithValue("@utility_id", utilityId);
            command.Parameters.AddWithValue("@utility_name", utilityName.ToLowerInvariant());
            command.Parameters.AddWithValue("@amount", amount);
            command.Parameters.AddWithValue("@utility_date", utilityDate.Date);

            var affectedRows = await command.ExecuteNonQueryAsync();
            if (affectedRows == 0)
            {
                return new OperationResult(false, "Plata comunala nu a fost gasita.");
            }

            return new OperationResult(true, "Plata comunala a fost actualizata cu succes.");
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Eroare la actualizarea platii comunale: {ex.Message}");
        }
    }

    public static async Task<OperationResult> DeleteUtilityAsync(int utilityId)
    {
        if (utilityId <= 0)
        {
            return new OperationResult(false, "Plata comunala este invalida.");
        }

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                "DELETE FROM utilities WHERE utility_id = @utility_id;",
                connection);
            command.Parameters.AddWithValue("@utility_id", utilityId);

            var affectedRows = await command.ExecuteNonQueryAsync();
            if (affectedRows == 0)
            {
                return new OperationResult(false, "Plata comunala nu a fost gasita.");
            }

            return new OperationResult(true, "Plata comunala a fost stearsa cu succes.");
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Eroare la stergerea platii comunale: {ex.Message}");
        }
    }

    public static async Task<OperationResult> MarkUtilityPaidAsync(int utilityId)
    {
        if (utilityId <= 0)
        {
            return new OperationResult(false, "Plata comunala este invalida.");
        }

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                @"UPDATE utilities
                  SET is_paid = 1,
                      paid_date = @paid_date
                  WHERE utility_id = @utility_id;",
                connection);
            command.Parameters.AddWithValue("@utility_id", utilityId);
            command.Parameters.AddWithValue("@paid_date", DateTime.Today);

            var affectedRows = await command.ExecuteNonQueryAsync();
            if (affectedRows == 0)
            {
                return new OperationResult(false, "Plata comunala nu a fost gasita.");
            }

            return new OperationResult(true, "Plata comunala a fost marcata ca achitata.");
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Eroare la achitarea platii comunale: {ex.Message}");
        }
    }

    public static async Task<OperationResult> ApplyUtilityPaymentAsync(int utilityId, decimal paymentAmount)
    {
        if (utilityId <= 0 || paymentAmount <= 0)
        {
            return new OperationResult(false, "Suma pentru plata este invalida.");
        }

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                await using var load = new MySqlCommand(
                    @"SELECT amount, is_paid
                      FROM utilities
                      WHERE utility_id = @utility_id
                      LIMIT 1;",
                    connection,
                    transaction);
                load.Parameters.AddWithValue("@utility_id", utilityId);

                await using var reader = await load.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return new OperationResult(false, "Plata comunala nu a fost gasita.");
                }

                var currentAmount = reader.GetDecimal("amount");
                var isPaid = reader.GetBoolean("is_paid");
                await reader.CloseAsync();

                if (isPaid)
                {
                    return new OperationResult(false, "Plata comunala este deja achitata.");
                }

                var remainingAmount = currentAmount - paymentAmount;
                var shouldClose = remainingAmount <= 0;
                var amountToStore = shouldClose ? 0m : remainingAmount;

                await using var update = new MySqlCommand(
                    @"UPDATE utilities
                      SET amount = @amount,
                          is_paid = @is_paid,
                          paid_date = @paid_date
                      WHERE utility_id = @utility_id;",
                    connection,
                    transaction);
                update.Parameters.AddWithValue("@amount", amountToStore);
                update.Parameters.AddWithValue("@is_paid", shouldClose);
                update.Parameters.AddWithValue("@paid_date", shouldClose ? DateTime.Today : DBNull.Value);
                update.Parameters.AddWithValue("@utility_id", utilityId);
                await update.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                return shouldClose
                    ? new OperationResult(true, "Plata comunala a fost achitata complet.")
                    : new OperationResult(true, $"Plata a fost aplicata. Restul ramas este {amountToStore:0.00} MDL.");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Eroare la aplicarea platii: {ex.Message}");
        }
    }

    public static async Task<decimal> GetCurrentBudgetAsync(int userId)
    {
        if (userId <= 0)
        {
            return 0m;
        }

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                "SELECT current_budget FROM users WHERE user_id = @user_id LIMIT 1;",
                connection);
            command.Parameters.AddWithValue("@user_id", userId);

            var value = await command.ExecuteScalarAsync();
            return value is null || value == DBNull.Value ? 0m : Convert.ToDecimal(value);
        }
        catch
        {
            return 0m;
        }
    }

    public static async Task<OperationResult> UpdateCurrentBudgetAsync(int userId, decimal budget)
    {
        if (userId <= 0 || budget < 0)
        {
            return new OperationResult(false, "Buget invalid.");
        }

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                "UPDATE users SET current_budget = @budget WHERE user_id = @user_id;",
                connection);
            command.Parameters.AddWithValue("@budget", budget);
            command.Parameters.AddWithValue("@user_id", userId);

            var affectedRows = await command.ExecuteNonQueryAsync();
            if (affectedRows == 0)
            {
                return new OperationResult(false, "Utilizatorul nu a fost gasit.");
            }

            return new OperationResult(true, "Bugetul a fost actualizat.");
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Eroare la actualizarea bugetului: {ex.Message}");
        }
    }

    public static async Task<DashboardData> GetDashboardDataAsync(int userId)
    {
        try
        {
            var budget = await GetCurrentBudgetAsync(userId);
            var expenses = await GetExpensesAsync(userId);
            var debts = await GetDebtsAsync(userId);
            var utilities = await GetUtilitiesAsync(userId);

            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var nextMonthStart = monthStart.AddMonths(1);

            var currentMonthExpenses = expenses
                .Where(expense => expense.ExpenseDate >= monthStart && expense.ExpenseDate < nextMonthStart)
                .ToList();

            var currentMonthUtilities = utilities
                .Where(utility => utility.UtilityDate >= monthStart && utility.UtilityDate < nextMonthStart)
                .ToList();

            var totalPayableDebts = debts
                .Where(debt => debt.DebtType == "eu_datorez")
                .Sum(debt => debt.Amount);

            var totalReceivableDebts = debts
                .Where(debt => debt.DebtType == "mie_datoreaza")
                .Sum(debt => debt.Amount);

            var upcomingObligations = debts
                .Where(debt => debt.DueDate >= today)
                .Select(debt => new DashboardObligation(
                    debt.PersonName,
                    debt.DebtType == "eu_datorez" ? "Datorie de achitat" : "Suma de recuperat",
                    debt.Amount,
                    debt.DueDate))
                .Concat(utilities.Select(utility => new DashboardObligation(
                    CapitalizeUtilityName(utility.UtilityName),
                    "Serviciu comunal de achitat",
                    utility.Amount,
                    utility.UtilityDate)))
                .OrderBy(item => item.DueDate)
                .Take(4)
                .ToList();

            var categoryTotals = currentMonthExpenses
                .GroupBy(expense => expense.CategoryName)
                .Select(group => new DashboardCategoryTotal(group.Key, group.Sum(item => item.Amount)))
                .Concat(currentMonthUtilities.Count == 0
                    ? Enumerable.Empty<DashboardCategoryTotal>()
                    : new[]
                    {
                        new DashboardCategoryTotal(
                            "Servicii comunale",
                            currentMonthUtilities.Sum(item => item.Amount))
                    })
                .OrderByDescending(item => item.Amount)
                .ToList();

            return new DashboardData(
                budget,
                totalPayableDebts,
                totalReceivableDebts,
                currentMonthExpenses.Sum(expense => expense.Amount) + currentMonthUtilities.Sum(utility => utility.Amount),
                upcomingObligations,
                categoryTotals);
        }
        catch
        {
            return new DashboardData(0m, 0m, 0m, 0m, new List<DashboardObligation>(), new List<DashboardCategoryTotal>());
        }
    }

    private static string CapitalizeUtilityName(string utilityName)
    {
        if (string.IsNullOrWhiteSpace(utilityName))
        {
            return "Serviciu";
        }

        return char.ToUpperInvariant(utilityName[0]) + utilityName[1..].ToLowerInvariant();
    }

    public static async Task<OperationResult> UpdateUserRoleAsync(int userId, string role)
    {
        if (userId <= 0)
        {
            return new OperationResult(false, "Utilizator invalid.");
        }

        if (!string.Equals(role, "user", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return new OperationResult(false, "Rol invalid.");
        }

        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                "UPDATE users SET role = @role WHERE user_id = @user_id;",
                connection);
            command.Parameters.AddWithValue("@role", role.ToLowerInvariant());
            command.Parameters.AddWithValue("@user_id", userId);

            var affectedRows = await command.ExecuteNonQueryAsync();
            if (affectedRows == 0)
            {
                return new OperationResult(false, "Utilizatorul nu a fost gasit.");
            }

            return new OperationResult(true, "Rolul utilizatorului a fost actualizat.");
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Eroare la actualizarea rolului: {ex.Message}");
        }
    }

    public static async Task<OperationResult> DeleteUserByIdAsync(int userId)
    {
        if (userId <= 0)
        {
            return new OperationResult(false, "Utilizator invalid.");
        }

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
                return new OperationResult(true, "Utilizatorul a fost sters cu succes.");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Eroare la stergerea utilizatorului: {ex.Message}");
        }
    }

    public static async Task<AdminStats> GetAdminStatsAsync()
    {
        try
        {
            await EnsureSchemaAsync();

            await using var connection = new MySqlConnection(DatabaseConnectionString);
            await connection.OpenAsync();

            async Task<int> CountAsync(string sql)
            {
                await using var command = new MySqlCommand(sql, connection);
                return Convert.ToInt32(await command.ExecuteScalarAsync());
            }

            var totalUsers = await CountAsync("SELECT COUNT(*) FROM users;");
            var adminUsers = await CountAsync("SELECT COUNT(*) FROM users WHERE LOWER(role) = 'admin';");
            var totalExpenses = await CountAsync("SELECT COUNT(*) FROM expenses;");
            var totalDebts = await CountAsync("SELECT COUNT(*) FROM debts;");
            var totalUtilities = await CountAsync("SELECT COUNT(*) FROM utilities;");

            return new AdminStats(totalUsers, adminUsers, totalExpenses, totalDebts, totalUtilities);
        }
        catch
        {
            return new AdminStats(0, 0, 0, 0, 0);
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
                        current_budget DECIMAL(10,2) NOT NULL DEFAULT 0,
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
                        original_amount DECIMAL(10,2) NOT NULL DEFAULT 0,
                        is_settled TINYINT(1) NOT NULL DEFAULT 0,
                        settled_date DATE NULL,
                        due_date DATE NOT NULL,
                        FOREIGN KEY (user_id) REFERENCES users(user_id)
                    );",
                    @"CREATE TABLE IF NOT EXISTS utilities (
                        utility_id INT AUTO_INCREMENT PRIMARY KEY,
                        user_id INT NOT NULL,
                        utility_name ENUM('apa', 'lumina', 'gaz', 'internet') NOT NULL,
                        amount DECIMAL(10,2) NOT NULL,
                        is_paid TINYINT(1) NOT NULL DEFAULT 0,
                        paid_date DATE NULL,
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
                await EnsureUsersCurrentBudgetColumnAsync(databaseConnection);
                await EnsureDebtsStatusColumnsAsync(databaseConnection);
                await EnsureUtilitiesStatusColumnsAsync(databaseConnection);

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

    private static async Task EnsureUsersCurrentBudgetColumnAsync(MySqlConnection connection)
    {
        await using var checkColumn = new MySqlCommand(
            @"SELECT COUNT(*)
              FROM INFORMATION_SCHEMA.COLUMNS
              WHERE TABLE_SCHEMA = DATABASE()
                AND TABLE_NAME = 'users'
                AND COLUMN_NAME = 'current_budget';",
            connection);

        var columnExists = Convert.ToInt64(await checkColumn.ExecuteScalarAsync());
        if (columnExists == 0)
        {
            await using var alter = new MySqlCommand(
                "ALTER TABLE users ADD COLUMN current_budget DECIMAL(10,2) NOT NULL DEFAULT 0 AFTER password;",
                connection);
            await alter.ExecuteNonQueryAsync();
        }
    }

    private static async Task EnsureDebtsStatusColumnsAsync(MySqlConnection connection)
    {
        await EnsureColumnAsync(connection, "debts", "original_amount",
            "ALTER TABLE debts ADD COLUMN original_amount DECIMAL(10,2) NOT NULL DEFAULT 0 AFTER amount;");
        await EnsureColumnAsync(connection, "debts", "is_settled",
            "ALTER TABLE debts ADD COLUMN is_settled TINYINT(1) NOT NULL DEFAULT 0 AFTER original_amount;");
        await EnsureColumnAsync(connection, "debts", "settled_date",
            "ALTER TABLE debts ADD COLUMN settled_date DATE NULL AFTER is_settled;");

        await using var backfillOriginalAmount = new MySqlCommand(
            "UPDATE debts SET original_amount = amount WHERE original_amount <= 0;",
            connection);
        await backfillOriginalAmount.ExecuteNonQueryAsync();
    }

    private static async Task EnsureUtilitiesStatusColumnsAsync(MySqlConnection connection)
    {
        await EnsureColumnAsync(connection, "utilities", "is_paid",
            "ALTER TABLE utilities ADD COLUMN is_paid TINYINT(1) NOT NULL DEFAULT 0 AFTER amount;");
        await EnsureColumnAsync(connection, "utilities", "paid_date",
            "ALTER TABLE utilities ADD COLUMN paid_date DATE NULL AFTER is_paid;");
    }

    private static async Task EnsureColumnAsync(MySqlConnection connection, string tableName, string columnName, string alterSql)
    {
        await using var checkColumn = new MySqlCommand(
            @"SELECT COUNT(*)
              FROM INFORMATION_SCHEMA.COLUMNS
              WHERE TABLE_SCHEMA = DATABASE()
                AND TABLE_NAME = @table_name
                AND COLUMN_NAME = @column_name;",
            connection);
        checkColumn.Parameters.AddWithValue("@table_name", tableName);
        checkColumn.Parameters.AddWithValue("@column_name", columnName);

        var columnExists = Convert.ToInt64(await checkColumn.ExecuteScalarAsync());
        if (columnExists == 0)
        {
            await using var alter = new MySqlCommand(alterSql, connection);
            await alter.ExecuteNonQueryAsync();
        }
    }
}
