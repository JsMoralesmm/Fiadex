namespace Fiadex;

public sealed partial class FiadexDatabase
{
    public bool HasUser()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM Users)";
        return (long)command.ExecuteScalar()! == 1;
    }

    public void CreateAdmin(string username, string password)
    {
        if (HasUser())
        {
            throw new InvalidOperationException("Ya existe una cuenta administradora.");
        }

        PasswordSecurity.ValidatePassword(password);
        var (hash, salt) = PasswordSecurity.HashPassword(password);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Users (Username, PasswordHash, PasswordSalt)
            VALUES ($username, $hash, $salt)
            """;
        command.Parameters.AddWithValue("$username", username.Trim());
        command.Parameters.AddWithValue("$hash", hash);
        command.Parameters.AddWithValue("$salt", salt);
        command.ExecuteNonQuery();
    }

    public bool Authenticate(string username, string password)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT PasswordHash, PasswordSalt, FailedAttempts, IsLocked
            FROM Users WHERE Username = $username
            """;
        command.Parameters.AddWithValue("$username", username.Trim());

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return false;
        }

        if (reader.GetInt64(3) == 1)
        {
            throw new InvalidOperationException("La cuenta está bloqueada. Solicite recuperación al administrador.");
        }

        var valid = PasswordSecurity.VerifyPassword(
            password,
            reader.GetString(1),
            reader.GetString(0));

        reader.Close();

        if (valid)
        {
            using var success = connection.CreateCommand();
            success.CommandText = """
                UPDATE Users SET FailedAttempts = 0, IsLocked = 0, LastLoginAt = $lastLogin
                WHERE Username = $username
                """;
            success.Parameters.AddWithValue("$lastLogin", DateTime.UtcNow.ToString("O"));
            success.Parameters.AddWithValue("$username", username.Trim());
            success.ExecuteNonQuery();
            return true;
        }

        using var attempts = connection.CreateCommand();
        attempts.CommandText = """
            UPDATE Users
            SET FailedAttempts = FailedAttempts + 1,
                IsLocked = CASE WHEN FailedAttempts + 1 >= 5 THEN 1 ELSE 0 END
            WHERE Username = $username
            """;
        attempts.Parameters.AddWithValue("$username", username.Trim());
        attempts.ExecuteNonQuery();
        return false;
    }

    public void ChangePassword(string username, string currentPassword, string newPassword)
    {
        if (!Authenticate(username, currentPassword))
        {
            throw new InvalidOperationException("La contraseña actual no es correcta.");
        }

        PasswordSecurity.ValidatePassword(newPassword);
        var (hash, salt) = PasswordSecurity.HashPassword(newPassword);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Users SET PasswordHash = $hash, PasswordSalt = $salt,
                             FailedAttempts = 0, IsLocked = 0
            WHERE Username = $username
            """;
        command.Parameters.AddWithValue("$hash", hash);
        command.Parameters.AddWithValue("$salt", salt);
        command.Parameters.AddWithValue("$username", username.Trim());
        command.ExecuteNonQuery();
    }

    public void UnlockAccount(string username)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Users SET FailedAttempts = 0, IsLocked = 0
            WHERE Username = $username
            """;
        command.Parameters.AddWithValue("$username", username.Trim());
        command.ExecuteNonQuery();
    }
}
