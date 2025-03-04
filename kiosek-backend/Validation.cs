using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MySql.Data.MySqlClient;

namespace kiosek_backend;

public class Validation
{
    public static Dictionary<string, User> tokens = new Dictionary<string, User>();

    private const int ExpirationDays = 1;

    private const string SecretKey = 
        @"251a553c59d9e385759a8d4b6dc361fff7013d854407d5813b5a8b07d212c3b8dfea7d24a7c962a7962598fd45083430a19b1342fdb01e0dd12e7815c2b12f31e1f34ce8aab1889bce5924539d71d6a876e8dc7337dc17da21c57ecbbe5fdf0065239010e08784502bc2c31c913147480b414472ad23d305901d0bd800ab20562cb6695bfebb9578c43f9543c31b5538ff41346bf5ac97ba273b66a0a127c4248bb52d77b7f0d30d7e375d5f67f548992c64c9c61bd9ee76fd80d01d362e62f4a174d416b54e230b2722161d5ff11f2c066c1338e99ce36e7c9a0441c6664ad532c388e5b16a4d7deb7bb63ae1d3996f6a0e05f7a7e41478382c94ce4936ee61";

    public static string CreateAToken(User user)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Username),
            new Claim("name", user.Name), // Avoid using "sub" twice
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "http://localhost:5148",
            audience: "http://localhost:3000",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(ExpirationDays),
            signingCredentials: credentials
        );

        string tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        tokens.Add(tokenString, user);

        return tokenString;
    }
    public static bool CheckToken(string token)
    {
        // Check if the token exists in our in-memory dictionary.
        if (!tokens.ContainsKey(token))
            return false;

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(SecretKey);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = "http://localhost:5148",
            ValidateAudience = true,
            ValidAudience = "http://localhost:3000",
            ValidateLifetime = true, // This ensures the token hasn't expired.
            ClockSkew = TimeSpan.Zero // Optional: eliminate clock skew allowance.
        };

        try
        {
            // Validate the token. If it's expired or otherwise invalid, this call will throw.
            tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
            return true;
        }
        catch (SecurityTokenExpiredException)
        {
            // Optionally, remove expired token from the dictionary.
            tokens.Remove(token);
            return false;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }
    public static string? GetUsername(string token)
    {
        if (!tokens.ContainsKey(token)) return null;
        return tokens[token].Username;
    }
    public static bool IsAdmin(string token)
    {
        if (!tokens.TryGetValue(token, out var user)) return false;
        string username = user.Username;

        using (MySqlConnection conn = new MySqlConnection(Database.connectionString))
        {
            conn.Open();
            var query = "SELECT 1 FROM admins WHERE username = @username LIMIT 1;";

            using (MySqlCommand cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@username", username);

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    return reader.HasRows; // Efficiently checks if a row exists
                }
            }
        }
    }

    public static string? ValidateHeader(HttpContext httpContext)
        {
            if (!httpContext.Request.Headers.ContainsKey("Authorization"))
            {
                return null;
            }

            // Retrieve the header value
            string authHeader = httpContext.Request.Headers["Authorization"];

            // Ensure the header starts with "Bearer "
            const string bearerPrefix = "Bearer ";
            if (!authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Extract the token by removing the "Bearer " prefix
            return authHeader.Substring(bearerPrefix.Length).Trim();
        }
    public static bool IsOwner(string id, string token)
    {
        if (!tokens.TryGetValue(token, out var user)) return false;
        string username = user.Username;

        using (MySqlConnection conn = new MySqlConnection(Database.connectionString))
        {
            conn.Open();
            var query = "SELECT 1 FROM projects WHERE author = @author AND id = @id LIMIT 1;";

            using (MySqlCommand cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@author", username);
                cmd.Parameters.AddWithValue("@id", id);

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    return reader.HasRows; // Checks if any row exists
                }
            }
        }
    }


    public static bool ValidateId(string id)
    {
        using (MySqlConnection conn = new MySqlConnection(Database.connectionString))
        {
            conn.Open();
            var query = "SELECT id FROM projects WHERE id = @id LIMIT 1;";

            using (MySqlCommand cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    return reader.HasRows;  // Efficiently checks if any row exists
                }
            }
        }
    }
    public static bool ValidateIdTag(string id)
    {
        using (MySqlConnection conn = new MySqlConnection(Database.connectionString))
        {
            conn.Open();
            var query = "SELECT id FROM tags WHERE id = @id LIMIT 1;";

            using (MySqlCommand cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    return reader.HasRows;  // Efficiently checks if any row exists
                }
            }
        }
    }

    public static string GetOwner(string id)
    {
        MySqlConnection conn = new MySqlConnection(Database.connectionString);
        conn.Open();
        var query = "SELECT author FROM projects WHERE id = @id;";
        using (MySqlCommand cmd = new MySqlCommand(query, conn))
        {
            MySqlDataReader reader = cmd.ExecuteReader();
            reader.Read();

            
            
            reader.Close();
            reader.Dispose();
            conn.Close();
            conn.Dispose();

            return reader.GetString(0);
        }
    }
}