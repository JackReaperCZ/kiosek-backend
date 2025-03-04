using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Exception = System.Exception;


namespace kiosek_backend;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddAuthorization();

        // Allow CORS from any origin
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials().AllowAnyOrigin();
            });
        });

        builder.Services.AddOpenApi();

        var app = builder.Build();
        
        // Enable CORS
        app.UseCors("AllowAll");

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "uploads")),
            RequestPath = "/uploads"
        });

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }


        app.MapGet("/api/tags", async (HttpContext httpContext) =>
        {
            // Extract the token by removing the "Bearer " prefix
            string token = Validation.ValidateHeader(httpContext);

            if (token == null) return Results.Unauthorized();

            if (!Validation.CheckToken(token))
            {
                return Results.Unauthorized();
            }

            List<Tag> tags = Tag.GetAll();

            // Extract the names of the tags
            var tagNames = tags.Select(tag => tag.Name).ToList();

            // Return the names as a JSON response
            return Results.Ok(tagNames);
        });

        app.MapGet("/api/check/tags", async (HttpContext httpContext) =>
        {
            // Extract the token by removing the "Bearer " prefix
            string token = Validation.ValidateHeader(httpContext);

            if (token == null) return Results.Unauthorized();

            if (!Validation.CheckToken(token) && Validation.IsAdmin(token))
            {
                return Results.Unauthorized();
            }

            List<Tag> tags = Tag.GetAll();

            // Return the names as a JSON response
            return Results.Ok(JsonSerializer.Serialize(tags));
        });

        app.MapPost("/api/check/tags/review", async (HttpContext httpContext) =>
        {
            // Extract the token by removing the "Bearer " prefix
            string token = Validation.ValidateHeader(httpContext);

            if (token == null) return Results.Unauthorized();

            if (!Validation.CheckToken(token) && Validation.IsAdmin(token))
            {
                return Results.Unauthorized();
            }
            
            var id = httpContext.Request.Query["id"].ToString();
            if (!string.IsNullOrEmpty(id))
            {
                if (!Validation.ValidateIdTag(id)) return Results.BadRequest();
                
                using var reader = new StreamReader(httpContext.Request.Body);
                var body = await reader.ReadToEndAsync();

                try
                {
                    using var jsonDoc = JsonDocument.Parse(body);
                    var root = jsonDoc.RootElement;

                    bool approved = root.GetProperty("approved").GetBoolean();

                    Tag.UpdateTagStatus(id, approved);

                    // Send an email or something

                    return Results.Ok("OK"); // Return the response directly
                }
                catch (Exception e)
                {
                    return Results.BadRequest(); // Directly return BadRequest without writing to response manually
                }
            }

            return Results.BadRequest(); // Return BadRequest if 'id' is not present
        });

        app.MapGet("/api/check/projects", async (HttpContext httpContext) =>
        {
            // Extract the token by removing the "Bearer " prefix
            string token = Validation.ValidateHeader(httpContext);

            if (token == null) return Results.Unauthorized();

            if (!Validation.CheckToken(token) && Validation.IsAdmin(token))
            {
                return Results.Unauthorized();
            }

            var id = httpContext.Request.Query["id"].ToString(); // Get 'id' from query parameters
            var examine = httpContext.Request.Query["examine"].ToString();

            bool exam = false;

            if (!string.IsNullOrEmpty(examine))
            {
                try
                {
                    exam = bool.Parse(examine);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Invalid examine parameter.");
                }
            }

            if (!string.IsNullOrEmpty(id))
            {
                if (!Validation.ValidateId(id)) return Results.BadRequest();
                if (exam)
                {
                    Project project = Project.GetProject(id);

                    var projectForm = new
                    {
                        id = project.ID.ToString(),
                        name = project.Name,
                        description = project.Description,
                        tags = JsonSerializer.Serialize(project.Tags),
                        thumbnail = project.ThumbnailUrl,
                        media = JsonSerializer.Serialize(project.MediaUrls)
                    };

                    return Results.Ok(projectForm); // Directly return Ok with the project form
                }
                else
                {
                    Project project = Project.GetCheckProject(id);

                    var status = project.Status switch
                    {
                        'W' => "Waiting for checkup.",
                        'D' => "Denied.",
                        'A' => "Approved.",
                        _ => "Unknown."
                    };

                    var projectForm = new
                    {
                        id = id,
                        name = project.Name,
                        thumbnail = project.ThumbnailUrl,
                        status = status
                    };

                    return Results.Ok(projectForm); // Return the status of the project as JSON
                }
            }
            else
            {
                List<string> projects = Project.GetCheckProjects();
                return Results.Ok(projects); // Return the list of projects
            }
        });

        app.MapPost("/api/check/projects/review", async (HttpContext httpContext) =>
        {
            // Extract the token by removing the "Bearer " prefix
            string token = Validation.ValidateHeader(httpContext);

            if (token == null) return Results.Unauthorized();

            if (!Validation.CheckToken(token) && Validation.IsAdmin(token))
            {
                return Results.Unauthorized();
            }

            var id = httpContext.Request.Query["id"].ToString();
            if (!string.IsNullOrEmpty(id))
            {
                if (!Validation.ValidateId(id)) return Results.BadRequest();
                using var reader = new StreamReader(httpContext.Request.Body);
                var body = await reader.ReadToEndAsync();

                try
                {
                    using var jsonDoc = JsonDocument.Parse(body);
                    var root = jsonDoc.RootElement;

                    bool approved = root.GetProperty("approved").GetBoolean();

                    char status = approved ? 'A' : 'D';

                    Project.UpdateProjectStatus(id, status);

                    return Results.Ok("OK"); // Return the response directly
                }
                catch (Exception e)
                {
                    return Results.BadRequest(); // Directly return BadRequest
                }
            }

            return Results.BadRequest(); // Return BadRequest if 'id' is not present
        });

        app.MapPost("/api/project", async (HttpContext httpContext) =>
        {
            string? token = Validation.ValidateHeader(httpContext);
            if (token == null) return Results.Unauthorized();
            if (!Validation.CheckToken(token)) return Results.Unauthorized();

            try
            {
                var form = await httpContext.Request.ReadFormAsync();

                // Extract basic data
                var name = form["name"].ToString();
                var description = form["description"].ToString();
                var tagsJson = form["tags"].ToString();

                // Validate required fields
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(description))
                {
                    return Results.BadRequest(new { message = "Name and description are required." });
                }

                var tags = JsonSerializer.Deserialize<List<string>>(tagsJson) ?? new List<string>();

                // Handle thumbnail upload
                var thumbnailFile = form.Files["thumbnail"];
                string? thumbnailPath = null;
                if (thumbnailFile != null)
                {
                    var uploadDir = Path.Combine("uploads", "thumbnails");
                    Directory.CreateDirectory(uploadDir);
                    thumbnailPath = Path.Combine(uploadDir, Guid.NewGuid() + Path.GetExtension(thumbnailFile.FileName));

                    using (var stream = new FileStream(thumbnailPath, FileMode.Create))
                    {
                        await thumbnailFile.CopyToAsync(stream);
                    }
                }

                // Handle media files upload
                var mediaPaths = new List<string>();
                foreach (var file in form.Files.Where(f => f.Name.StartsWith("media_")))
                {
                    var uploadDir = Path.Combine("uploads", "media");
                    Directory.CreateDirectory(uploadDir);
                    var mediaPath = Path.Combine(uploadDir, Guid.NewGuid() + Path.GetExtension(file.FileName));

                    using (var stream = new FileStream(mediaPath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    mediaPaths.Add(mediaPath);
                }

                var project = new Project(
                    Guid.NewGuid(),
                    name,
                    description,
                    tags,
                    thumbnailPath,
                    mediaPaths,
                    'W',
                    DateTime.MinValue
                );

                string owner = Validation.GetUsername(token);
                if (owner == null) return Results.Unauthorized();

                Project.Upload(project, owner);

                return Results.Ok(new { message = "Project submitted successfully" });
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        app.MapPost("/api/auth/login", async (HttpContext httpContext) =>
        {
            using var reader = new StreamReader(httpContext.Request.Body);
            var body = await reader.ReadToEndAsync();

            try
            {
                using var jsonDoc = JsonDocument.Parse(body);
                var root = jsonDoc.RootElement;

                string username = root.GetProperty("username").GetString() ?? "";
                string password = root.GetProperty("password").GetString() ?? "";

                User user = await UserDataFetcher.GetUserDataAsync(username, password);

                if (user == null)
                {
                    Console.WriteLine("Failed to find user");
                    return Results.Unauthorized();
                }

                string token = Validation.CreateAToken(user);

                var response = new
                {
                    name = user.Name,
                    token = token
                };

                return Results.Json(response);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return Results.Unauthorized();
            }
        });

        app.MapGet("/api/auth/validate", async (HttpContext httpContext) =>
        {
            // Extract the token by removing the "Bearer " prefix
            string token = Validation.ValidateHeader(httpContext);

            if (token == null) return Results.Unauthorized();

            if (Validation.CheckToken(token))
            {
                return Results.Ok();
            }

            return Results.Unauthorized();
        });

        app.MapGet("/api/auth/validateOwner", async (HttpContext httpContext) =>
        {
            var id = httpContext.Request.Query["id"].ToString();
            if (!string.IsNullOrEmpty(id))
            {
                if (!Validation.ValidateId(id)) return Results.BadRequest();
                // Extract the token by removing the "Bearer " prefix
                string token = Validation.ValidateHeader(httpContext);

                if (token == null) return Results.Unauthorized();

                if (Validation.CheckToken(token) && Validation.IsOwner(id, token))
                {
                    return Results.Ok();
                }
            }

            return Results.Unauthorized();
        });

        app.MapPost("/api/project/update", async (HttpContext httpContext) =>
        {
            string? token = Validation.ValidateHeader(httpContext);
            if (token == null || !Validation.CheckToken(token)) return Results.Unauthorized();

            var id = httpContext.Request.Query["id"].ToString();
            if (string.IsNullOrEmpty(id)) return Results.BadRequest();
            if (!Validation.ValidateId(id)) return Results.BadRequest();

            try
            {
                var form = await httpContext.Request.ReadFormAsync();

                var name = form["name"].ToString();
                var description = form["description"].ToString();
                var tagsJson = form["tags"].ToString();
                var changedThumbnail = form["changedThumbnail"].ToString();
                var removedMediaJson = form["removedMedia"].ToString();

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(description))
                {
                    return Results.BadRequest(new { message = "Name and description are required." });
                }

                bool changeThumbnail = !string.IsNullOrWhiteSpace(changedThumbnail) && Boolean.Parse(changedThumbnail);
                var tags = JsonSerializer.Deserialize<List<string>>(tagsJson) ?? new List<string>();
                var removedMedia = JsonSerializer.Deserialize<List<string>>(removedMediaJson) ?? new List<string>();

                string? thumbnailPath = null;
                if (changeThumbnail)
                {
                    var thumbnailFile = form.Files["thumbnail"];
                    if (thumbnailFile != null)
                    {
                        var uploadDir = Path.Combine("uploads", "thumbnails");
                        Directory.CreateDirectory(uploadDir);
                        thumbnailPath = Path.Combine(uploadDir,
                            Guid.NewGuid() + Path.GetExtension(thumbnailFile.FileName));

                        using (var stream = new FileStream(thumbnailPath, FileMode.Create))
                        {
                            await thumbnailFile.CopyToAsync(stream);
                        }
                    }
                }

                var mediaPaths = new List<string>();
                foreach (var file in form.Files.Where(f => f.Name.StartsWith("addedMedia_")))
                {
                    var uploadDir = Path.Combine("uploads", "media");
                    Directory.CreateDirectory(uploadDir);
                    var mediaPath = Path.Combine(uploadDir, Guid.NewGuid() + Path.GetExtension(file.FileName));

                    using (var stream = new FileStream(mediaPath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    mediaPaths.Add(mediaPath);
                }

                Project project = new Project(Guid.Parse(id), name, description, tags, null, null, 'W',
                    DateTime.MinValue);
                string owner = Validation.GetUsername(token);
                if (owner == null) return Results.Unauthorized();

                Project.UpdateProject(project, removedMedia, mediaPaths, thumbnailPath);

                return Results.Ok(new { message = "Project updated successfully" });
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        app.MapGet("/api/auth/validate/admin", async (HttpContext httpContext) =>
        {
            // Extract the token by removing the "Bearer " prefix
            string token = Validation.ValidateHeader(httpContext);

            if (token == null) return Results.Unauthorized();

            if (Validation.CheckToken(token) && Validation.IsAdmin(token))
            {
                return Results.Ok();
            }

            return Results.Unauthorized();
        });

        app.MapGet("/api/preview/projects", async (HttpContext httpContext) =>
        {
            List<Project> projects = Project.GetPreviewProjects();

            // Select only the desired properties to return
            var previewProjects = projects.Select(project => new
            {
                project.ID,
                project.Name,
                project.ThumbnailUrl,
                project.Tags,
                project.Created
            });

            // Return the data
            return Results.Ok(previewProjects);
        });

        app.MapGet("/api/preview/my-projects", async (HttpContext httpContext) =>
        {
            string? token = Validation.ValidateHeader(httpContext);
            if (token == null) return Results.Unauthorized();
            if (!Validation.CheckToken(token)) return Results.Unauthorized();

            List<Project> projects = Project.GetPreviewProjects(token);

            // Select only the desired properties to return
            var previewProjects = projects.Select(project =>
            {
                var status = "";

                switch (project.Status)
                {
                    case 'A':
                        status = "Approved.";
                        break;
                    case 'D':
                        status = "Denied.";
                        break;
                    case 'W':
                        status = "Waiting for check.";
                        break;
                    default:
                        status = "Unknown.";
                        break;
                }

                return new
                {
                    project.ID,
                    project.Name,
                    project.ThumbnailUrl,
                    project.Tags,
                    status,
                    project.Created
                };
            });

            // Return the data
            return Results.Ok(previewProjects);
        });

        app.MapGet("/api/project", async (HttpContext httpContext) =>
        {
            var id = httpContext.Request.Query["id"].ToString();
            if (!string.IsNullOrEmpty(id))
            {
                if (!Validation.ValidateId(id)) return Results.BadRequest();
                if (!Project.IsProjectActive(id)) return Results.BadRequest();

                Project project = Project.GetProject(id);

                var projectForm = new
                {
                    id = project.ID.ToString(),
                    name = project.Name,
                    description = project.Description,
                    tags = JsonSerializer.Serialize(project.Tags),
                    thumbnail = project.ThumbnailUrl,
                    media = JsonSerializer.Serialize(project.MediaUrls)
                };

                return Results.Ok(projectForm);
            }

            return Results.BadRequest();
        });

        app.MapGet("/api/project/edit", async (HttpContext httpContext) =>
        {
            var id = httpContext.Request.Query["id"].ToString();
            string token = Validation.ValidateHeader(httpContext);
            if (token == null) return Results.Unauthorized();

            if (!string.IsNullOrEmpty(id))
            {
                if (!Validation.ValidateId(id)) return Results.BadRequest();
                if (!Validation.IsOwner(id, token)) return Results.Unauthorized();

                Project project = Project.GetProject(id);

                var projectForm = new
                {
                    id = project.ID.ToString(),
                    name = project.Name,
                    description = project.Description,
                    tags = JsonSerializer.Serialize(project.Tags),
                    thumbnail = project.ThumbnailUrl,
                    media = JsonSerializer.Serialize(project.MediaUrls)
                };

                return Results.Ok(projectForm);
            }

            return Results.BadRequest();
        });

        app.Run();
    }
}