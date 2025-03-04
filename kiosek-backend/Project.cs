using MySql.Data.MySqlClient;

namespace kiosek_backend;

public class Project(
    Guid id,
    string name,
    string description,
    List<string> tags,
    string thumbnailUrl,
    List<string> mediaUrls,
    char status,
    DateTime created)
{
    public Guid? ID { get; set; } = id;
    public string? Name { get; set; } = name;
    public string? Description { get; set; } = description;
    public List<string>? Tags { get; set; } = tags;
    public string? ThumbnailUrl { get; set; } = thumbnailUrl;
    public List<string>? MediaUrls { get; set; } = mediaUrls;
    public char Status { get; set; } = status;
    public DateTime? Created { get; set; } = created;


    public static void Upload(Project project, string owner)
    {
        //Insert into projects
        MySqlConnection conn = new MySqlConnection(Database.connectionString);
        conn.Open();
        var query =
            "INSERT INTO projects (id, name, description, date, status, author) VALUES (@id, @name, @description, @date, @status, @author);";

        using (MySqlCommand cmd = new MySqlCommand(query, conn))
        {
            cmd.Parameters.AddWithValue("@id", project.ID);
            cmd.Parameters.AddWithValue("@name", project.Name);
            cmd.Parameters.AddWithValue("@description", project.Description);
            cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@status", 'W');
            cmd.Parameters.AddWithValue("@author", owner);

            cmd.ExecuteNonQuery();
        }

        //Insert into thumbnails
        query = "INSERT INTO thumbnails (id,id_pro,name) VALUES (@id,@id_pro,@name);";
        using (MySqlCommand cmd = new MySqlCommand(query, conn))
        {
            cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@id_pro", project.ID);
            cmd.Parameters.AddWithValue("@name", project.ThumbnailUrl);

            cmd.ExecuteNonQuery();
        }

        //Insert into media
        if (project.MediaUrls.Count > 0)
        {
            query = "INSERT INTO media (id,id_pro,name) VALUES";
            for (int i = 0; i < project.MediaUrls.Count; i++)
            {
                query += $" (@id{i},@id_pro,@name{i})";
                query += (i == project.MediaUrls.Count - 1) ? ";" : ",";
            }

            using (MySqlCommand cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id_pro", project.ID);

                for (int i = 0; i < project.MediaUrls.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@id{i}", Guid.NewGuid().ToString());
                    cmd.Parameters.AddWithValue($"@name{i}", project.MediaUrls[i]);
                }

                cmd.ExecuteNonQuery();
            }
        }

        // Bulk check existing tags
        var existingTags = new Dictionary<string, string>();
        if (project.Tags.Count > 0)
        {
            var tagNames = string.Join(",", project.Tags.Select(t => $"'{MySqlHelper.EscapeString(t)}'"));
            query = $"SELECT id, name FROM tags WHERE name IN ({tagNames});";

            using (MySqlCommand cmd = new MySqlCommand(query, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    existingTags[reader["name"].ToString()!] = reader["id"].ToString()!;
                }

                reader.Close();
            }

            // Insert missing tags in bulk
            var newTags = project.Tags.Where(t => !existingTags.ContainsKey(t)).ToList();
            if (newTags.Count > 0)
            {
                query = "INSERT INTO tags (id, name) VALUES";
                for (int i = 0; i < newTags.Count; i++)
                {
                    query += $" (@id{i}, @name{i})";
                    query += (i == newTags.Count - 1) ? ";" : ",";
                }

                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    for (int i = 0; i < newTags.Count; i++)
                    {
                        var newId = Guid.NewGuid().ToString();
                        existingTags[newTags[i]] = newId;

                        cmd.Parameters.AddWithValue($"@id{i}", newId);
                        cmd.Parameters.AddWithValue($"@name{i}", newTags[i]);
                    }

                    cmd.ExecuteNonQuery();
                }

                // Fetch newly inserted tag IDs
                tagNames = string.Join(",", newTags.Select(t => $"'{MySqlHelper.EscapeString(t)}'"));
                query = $"SELECT id, name FROM tags WHERE name IN ({tagNames});";

                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        existingTags[reader["name"].ToString()!] = reader["id"].ToString()!;
                    }

                    reader.Close();
                }
            }

            // Bulk insert into tagged table
            query = "INSERT INTO taged (id, id_pro, id_tag) VALUES";
            for (int i = 0; i < project.Tags.Count; i++)
            {
                query += $" (@id{i}, @id_pro, @id_tag{i})";
                query += (i == project.Tags.Count - 1) ? ";" : ",";
            }

            using (MySqlCommand cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id_pro", project.ID);

                for (int i = 0; i < project.Tags.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@id{i}", Guid.NewGuid().ToString());
                    cmd.Parameters.AddWithValue($"@id_tag{i}", existingTags[project.Tags[i]]);
                }

                cmd.ExecuteNonQuery();
            }
        }

        conn.Clone();
        conn.Dispose();
    }

    public static Project GetProject(string id)
    {
        List<string> tags = Tag.GetAll(id);
        List<string> media = GetProjectMedia(id);

        MySqlConnection conn = new MySqlConnection(Database.connectionString);
        conn.Open();
        var query =
            "SELECT p.id, p.name, t.name, p.description, p.date FROM projects p JOIN thumbnails t ON t.id_pro = p.id WHERE p.id = @id;";

        using (MySqlCommand cmd = new MySqlCommand(query, conn))
        {
            cmd.Parameters.AddWithValue("@id", id);

            using (MySqlDataReader reader = cmd.ExecuteReader())
            {
                reader.Read();
                var p = new Project(
                    Guid.Parse(id),
                    reader.GetString(1),
                    reader.GetString(3),
                    tags,
                    reader.GetString(2),
                    media,
                    'A',
                    reader.GetDateTime(4)
                );

                reader.Close();
                conn.Close();
                conn.Dispose();
                return p;
            }
        }
    }

public static List<Project> GetPreviewProjects()
{
    List<Project> projects = new List<Project>();
    MySqlConnection conn = new MySqlConnection(Database.connectionString);
    conn.Open();
    var query =
        "SELECT p.id, p.name, t.name, p.date FROM projects p JOIN thumbnails t ON t.id_pro = p.id WHERE p.status = 'A';";

    using (MySqlCommand cmd = new MySqlCommand(query, conn))
    {
        using (MySqlDataReader reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                // Check for DBNull and handle it accordingly.
                var id = reader.IsDBNull(0) ? Guid.Empty : Guid.Parse(reader.GetString(0));
                var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                var thumbnailName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                var projectDate = reader.IsDBNull(3) ? default(DateTime) : reader.GetDateTime(3);

                projects.Add(new Project(
                    id,
                    name,
                    null,  // Assuming this is intended to be null, handle as needed
                    Tag.GetAll(id.ToString()), // Ensure Tag.GetAll is handling cases where the id is empty or invalid
                    thumbnailName,
                    null,  // Assuming this is intended to be null, handle as needed
                    'A',
                    projectDate
                ));
            }

            reader.Close();
            conn.Close();
            conn.Dispose();

            return projects;
        }
    }
}


    public static List<Project> GetPreviewProjects(string token)
    {
        string author = Validation.GetUsername(token);
        if (author == null) return new List<Project>();

        var projects = new List<Project>();

        using (MySqlConnection conn = new MySqlConnection(Database.connectionString))
        {
            conn.Open();
            var query = "SELECT p.id, p.name, t.name, p.date, p.status " +
                        "FROM projects p " +
                        "JOIN thumbnails t ON t.id_pro = p.id " +
                        "WHERE p.author = @author;";

            using (MySqlCommand cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@author", author);

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        projects.Add(new Project(
                            Guid.Parse(reader.GetString(0)), // ID
                            reader.GetString(1), // Name
                            null, // Description (not fetched)
                            Tag.GetAll(reader.GetString(0)), // Tags
                            reader.GetString(2), // Thumbnail
                            null, // Media (not fetched)
                            reader.GetChar(4), // Status
                            reader.GetDateTime(3) // Date
                        ));
                    }
                }
            }
        }

        return projects;
    }

    public static void UpdateProjectStatus(string id, char status)
    {
        using (MySqlConnection conn = new MySqlConnection(Database.connectionString))
        {
            conn.Open();
            using (var transaction = conn.BeginTransaction())
            {
                try
                {
                    var query = "UPDATE projects SET status = @status WHERE id = @id;";
                    using (var cmd = new MySqlCommand(query, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Parameters.AddWithValue("@status", status);

                        cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }

    public static List<string> GetCheckProjects()
    {
        var projects = new List<string>();

        using (MySqlConnection conn = new MySqlConnection(Database.connectionString))
        {
            conn.Open();
            var query = "SELECT id FROM projects;";

            using (MySqlCommand cmd = new MySqlCommand(query, conn))
            using (MySqlDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    projects.Add(reader.GetString(0));
                }
            }
        }

        return projects;
    }

    public static Project? GetCheckProject(string id)
    {
        using (MySqlConnection conn = new MySqlConnection(Database.connectionString))
        {
            conn.Open();
            var query = "SELECT p.id, p.name, t.name, p.status FROM projects p " +
                        "JOIN thumbnails t ON t.id_pro = p.id WHERE p.id = @id;";

            using (MySqlCommand cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new Project(
                            Guid.Parse(reader.GetString(0)), // ID
                            reader.GetString(1), // Name
                            null, // Description (not fetched)
                            null, // Tags (not fetched)
                            reader.GetString(2), // Thumbnail
                            null, // Media (not fetched)
                            reader.GetChar(3), // Status
                            DateTime.MinValue // Date (not fetched)
                        );
                    }
                }
            }
        }

        return null;
    }

    public static List<string> GetProjectMedia(string id)
    {
        var media = new List<string>();

        using (MySqlConnection conn = new MySqlConnection(Database.connectionString))
        {
            conn.Open();
            var query = "SELECT name FROM media WHERE id_pro = @id;";

            using (MySqlCommand cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        media.Add(reader.GetString(0));
                    }
                }
            }
        }

        return media;
    }

    public static bool IsProjectActive(string id)
    {
        using (MySqlConnection conn = new MySqlConnection(Database.connectionString))
        {
            conn.Open();
            var query = "SELECT status FROM projects WHERE id = @id LIMIT 1;";

            using (MySqlCommand cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return reader.GetString(0) == "A";
                    }
                }
            }
        }

        return false;
    }

    public static void UpdateProject(Project project, List<string> removedMedia, List<string> addedMedia,
        string? thumbnail = null)
    {
        using (MySqlConnection conn = new MySqlConnection(Database.connectionString))
        {
            conn.Open();
            using (var transaction = conn.BeginTransaction())
            {
                try
                {
                    using (var cmd = new MySqlCommand(
                               "UPDATE projects SET name = @name, description = @description, status = @status WHERE id = @id;",
                               conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@id", project.ID);
                        cmd.Parameters.AddWithValue("@name", project.Name);
                        cmd.Parameters.AddWithValue("@description", project.Description);
                        cmd.Parameters.AddWithValue("@status", 'W');

                        cmd.ExecuteNonQuery();
                    }

                    if (removedMedia.Any())
                    {
                        string query = "DELETE FROM media WHERE id_pro = @id AND name IN (" +
                                       string.Join(",", removedMedia.Select((_, i) => "@name" + i)) + ");";

                        using (var cmd = new MySqlCommand(query, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@id", project.ID);
                            for (int i = 0; i < removedMedia.Count; i++)
                            {
                                cmd.Parameters.AddWithValue("@name" + i, removedMedia[i].Replace('/', '\\'));
                            }

                            cmd.ExecuteNonQuery();

                            foreach (var media in removedMedia)
                            {
                                if (File.Exists(media))
                                {
                                    File.Delete(media);
                                }
                            }
                        }
                    }

                    if (addedMedia.Any())
                    {
                        string query = "INSERT INTO media (id, id_pro, name) VALUES " + string.Join(",",
                            addedMedia.Select((_, i) => "(@id" + i + ", @id_pro, @name" + i + ")")) + ";";
                        using (var cmd = new MySqlCommand(query, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@id_pro", project.ID);
                            for (int i = 0; i < addedMedia.Count; i++)
                            {
                                cmd.Parameters.AddWithValue("@id" + i, Guid.NewGuid().ToString());
                                cmd.Parameters.AddWithValue("@name" + i, addedMedia[i].Replace('/', '\\'));
                            }

                            cmd.ExecuteNonQuery();
                        }
                    }

                    if (thumbnail != null)
                    {
                        using (var cmd = new MySqlCommand("UPDATE thumbnails SET name = @name WHERE id_pro = @id;",
                                   conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@id", project.ID);
                            cmd.Parameters.AddWithValue("@name", thumbnail);
                            
                            cmd.ExecuteNonQuery();
                        }
                    }

                    //Get all tags
                    List<string> tags = Tag.GetAll().Select(p => p.Name).ToList();

                    //Insert missing tags
                    List<string> ids = new List<string>();
                    List<string> missingTags = project.Tags.Except(tags).ToList();
                    if (missingTags.Any())
                    {
                        using (var cmd = new MySqlCommand("INSERT INTO tags (id, name, added) VALUES " + string.Join(
                                       ",",
                                       missingTags.Select((_, i) => "(@id" + i + ", @name" + i + ", @added)")) + ";",
                                   conn,
                                   transaction))
                        {
                            cmd.Parameters.AddWithValue("@added", 0);
                            for (int i = 0; i < missingTags.Count; i++)
                            {
                                ids.Add(Guid.NewGuid().ToString());
                                cmd.Parameters.AddWithValue("@id" + i, ids[i]);
                                cmd.Parameters.AddWithValue("@name" + i, missingTags[i]);
                            }

                            cmd.ExecuteNonQuery();
                        }
                    }

                    //delete all connection between project and tags
                    using (var cmd = new MySqlCommand("DELETE FROM taged WHERE id_pro = @id_pro;", conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@id_pro", project.ID);
                        cmd.ExecuteNonQuery();
                    }
                    
                    using (var cmd = new MySqlCommand("SELECT id FROM tags WHERE name IN(" +
                                                      string.Join(",", project.Tags.Select((_, i) => "@name" + i)) +
                                                      ");", conn, transaction))
                    {
                        for (int i = 0; i < project.Tags.Count; i++)
                        {
                            cmd.Parameters.AddWithValue("@name" + i, tags[i]);
                        }

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                ids.Add(reader.GetString(0));
                            }
                        }
                    }

                    using (var cmd = new MySqlCommand("INSERT INTO taged (id, id_pro, id_tag) VALUES " + string.Join(
                                   ",",
                                   ids.Select((_, i) => "(@id" + i + ", @id_pro, @id_tag" + i + ")")) + ";", conn,
                               transaction))
                    {
                        cmd.Parameters.AddWithValue("@id_pro", project.ID);
                        for (int i = 0; i < ids.Count; i++)
                        {
                            cmd.Parameters.AddWithValue("@id" + i, Guid.NewGuid().ToString());
                            cmd.Parameters.AddWithValue("@id_tag" + i, ids[i]);
                        }

                        cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }
}