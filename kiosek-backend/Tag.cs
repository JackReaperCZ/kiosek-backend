using System.Data;
using MySql.Data.MySqlClient;

namespace kiosek_backend;

public class Tag(string id, string name, bool added)
{
    public string ID { get; set; } = id;
    public string Name { get; set; } = name;
    public bool Added { get; set; } = added;

    public static List<Tag> GetAll()
    {
        MySqlConnection conn = new MySqlConnection(Database.connectionString);
        conn.Open();
        var query = "SELECT * FROM tags;";
        List<Tag> tags = new List<Tag>();

        using (MySqlCommand cmd = new MySqlCommand(query, conn))
        {
            using (MySqlDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    tags.Add(new Tag(
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetBoolean(2)
                    ));
                }

                reader.Close();
            }
        }

        conn.Close();
        conn.Dispose();
        return tags;
    }
    
    public static List<string> GetAll(string id)
    {
        MySqlConnection conn = new MySqlConnection(Database.connectionString);
        conn.Open();
        var query = "SELECT t.name FROM tags t JOIN taged ta ON t.id = ta.id_tag WHERE ta.id_pro = @id;";
        List<string> tags = new List<string>();

        using (MySqlCommand cmd = new MySqlCommand(query, conn))
        {
            cmd.Parameters.AddWithValue("@id", id);

            using (MySqlDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    tags.Add(reader.GetString(0));
                }

                reader.Close();
            }
        }

        conn.Close();
        conn.Dispose();
        return tags;
    }
    public static void UpdateTagStatus(string id, bool status)
    {
        MySqlConnection conn = new MySqlConnection(Database.connectionString);
        conn.Open();
        var query = (status) ? "UPDATE tags SET added = @added WHERE id = @id;" : "DELETE FROM tags WHERE id = @id;";

        using (MySqlCommand cmd = new MySqlCommand(query, conn))
        {
            cmd.Parameters.AddWithValue("@id", id);
            
            if (status) cmd.Parameters.AddWithValue("@added", 1);
            
            cmd.ExecuteNonQuery();

            conn.Close();
            conn.Dispose();
        }
    }
}