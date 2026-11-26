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
            throw new InvalidOperationException("Usuario o contraseña incorrectos.");
        }

        var hash = reader.GetString(0);
        var salt = reader.GetString(1);
        var needsRehash = PasswordSecurity.NeedsRehash(hash);
        var valid = PasswordSecurity.VerifyPassword(password, salt, hash);

        reader.Close();

        if (valid)
        {
            if (needsRehash)
            {
                using var rehash = connection.CreateCommand();
                rehash.CommandText = """
                    UPDATE Users SET PasswordHash = $hash
                    WHERE Username = $username
                    """;
                rehash.Parameters.AddWithValue("$hash", PasswordSecurity.RehashPassword(password, salt));
                rehash.Parameters.AddWithValue("$username", username.Trim());
                rehash.ExecuteNonQuery();
            }

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

    public bool VerifyCredentials(string username, string password)
    {
        return VerifyCredentials(username, password, allowLocked: false);
    }

    public bool IsAccountLocked(string username)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT IsLocked FROM Users WHERE Username = $username
            """;
        command.Parameters.AddWithValue("$username", username.Trim());

        var result = command.ExecuteScalar();
        return result is long isLocked && isLocked == 1;
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

    public bool UnlockAccount(string username, string password)
    {
        // La cuenta bloqueada debe poder verificarse para permitir su recuperación.
        if (!VerifyCredentials(username, password, allowLocked: true))
        {
            return false;
        }

        ClearLock(username);
        return true;
    }

    private bool VerifyCredentials(string username, string password, bool allowLocked)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT PasswordHash, PasswordSalt, IsLocked
            FROM Users WHERE Username = $username
            """;
        command.Parameters.AddWithValue("$username", username.Trim());

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return false;
        }

        if (!allowLocked && reader.GetInt64(2) == 1)
        {
            return false;
        }

        return PasswordSecurity.VerifyPassword(
            password,
            reader.GetString(1),
            reader.GetString(0));
    }

    private void ClearLock(string username)
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
