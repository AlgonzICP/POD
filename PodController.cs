using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace VerificadorPOD_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PodController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post([FromForm] IFormFile imagen, [FromForm] string transportista)
    {
        if (imagen == null || string.IsNullOrWhiteSpace(transportista))
            return BadRequest("Imagen o transportista no válidos.");

        var zonasRecorte = new Dictionary<string, Rectangle>
        {
            { "correosped", new Rectangle(19, 1, 691, 98) },
            { "chrono",     new Rectangle(21, 114, 351, 44) },
            { "gls",        new Rectangle(511, 193, 245, 168) },
            { "seur",       new Rectangle(474, 274, 393, 191) }
        };

        if (!zonasRecorte.ContainsKey(transportista.ToLower()))
            return BadRequest("Transportista no reconocido.");

        Rectangle zona = zonasRecorte[transportista.ToLower()];
        string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jpeg");

using (var image = Image.Load<Rgba32>(imagen.OpenReadStream()))
{
    image.Mutate(x => x.Crop(zona));
    image.Save(tempPath); // ✅ ahora sí se guarda bien
}

        byte[] imageBytes = await System.IO.File.ReadAllBytesAsync(tempPath);
        string base64Image = Convert.ToBase64String(imageBytes);

        var requestBody = new
        {
            model = "gpt-4.1",
            messages = new object[]
            {
                new {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = "Extrae el nombre completo y el DNI del destinatario de este POD. Responde solo con:\nNombre: <nombre>\nDNI: <dni>" },
                        new {
                            type = "image_url",
                            image_url = new {
                                url = $"data:image/jpeg;base64,{base64Image}"
                            }
                        }
                    }
                }
            },
            max_tokens = 300
        };

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new InvalidOperationException("Falta OPENAI_API_KEY");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await httpClient.PostAsync(
            "https://api.openai.com/v1/chat/completions",
            new StringContent(System.Text.Json.JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json")
        );

        string result = await response.Content.ReadAsStringAsync();

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(result);
            string content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return Ok(content);
        }
        catch
        {
            return BadRequest("Error procesando respuesta de OpenAI: " + result);
        }
    }
}

builder.Services.AddCors(options =>
{
  options.AddPolicy("AllowFrontend", policy =>
  {
    policy.WithOrigins("https://tu-proyecto.vercel.app")
          .AllowAnyHeader()
          .AllowAnyMethod();
  });
});

var app = builder.Build();

app.UseCors("AllowFrontend");
app.MapControllers();
