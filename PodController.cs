using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Net.Http.Headers;

namespace VerificadorPOD_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PodController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post(
        [FromForm] IFormFile imagen,
        [FromForm] string transportista,
        [FromForm] string nombreCliente)
    {
        Console.WriteLine("🚀 Petición recibida en /api/pod");

        if (imagen == null || string.IsNullOrWhiteSpace(transportista) || string.IsNullOrWhiteSpace(nombreCliente))
        {
            Console.WriteLine("❌ Imagen, transportista o nombre del cliente no válidos.");
            return BadRequest("Imagen, transportista o nombre del cliente no válidos.");
        }

        Console.WriteLine($"✅ Transportista recibido: {transportista}");
        Console.WriteLine($"✅ Nombre del cliente esperado: {nombreCliente}");

        var zonasRecorte = new Dictionary<string, Rectangle>
        {
            { "correosped", new Rectangle(19, 1, 691, 98) },
            { "chrono",     new Rectangle(21, 114, 351, 44) },
            { "gls",        new Rectangle(511, 193, 245, 168) },
            { "seur",       new Rectangle(474, 274, 393, 191) }
        };

        if (!zonasRecorte.ContainsKey(transportista.ToLower()))
        {
            Console.WriteLine("❌ Transportista no reconocido.");
            return BadRequest("Transportista no reconocido.");
        }

        Rectangle zona = zonasRecorte[transportista.ToLower()];
        string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jpeg");

        try
        {
            Console.WriteLine("🖼️ Iniciando recorte de imagen...");
            using (var image = Image.Load<Rgba32>(imagen.OpenReadStream()))
            {
                Console.WriteLine($"📏 Dimensiones de imagen: ancho={image.Width}, alto={image.Height}");
                Console.WriteLine($"📐 Zona de recorte: {zona}");

                if (zona.Right > image.Width || zona.Bottom > image.Height)
                {
                    Console.WriteLine("❌ Zona de recorte fuera de los límites de la imagen.");
                    return BadRequest("La imagen es demasiado pequeña o no tiene la zona esperada.");
                }

                image.Mutate(x => x.Crop(zona));
                image.Save(tempPath);
            }
            Console.WriteLine("✅ Imagen recortada y guardada temporalmente en: " + tempPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Error al procesar la imagen:");
            Console.WriteLine(ex.ToString());
            return BadRequest("Error al procesar la imagen: " + ex.Message);
        }

        byte[] imageBytes = await System.IO.File.ReadAllBytesAsync(tempPath);
        string base64Image = Convert.ToBase64String(imageBytes);

        string prompt = $"En esta imagen de un POD, extrae el nombre completo y DNI del destinatario. El nombre esperado es: \"{nombreCliente}\". Al final indica si coinciden exactamente (Sí o No). Formato:\nNombre: ...\nDNI: ...\nCoincide: ...";

        var requestBody = new
        {
            model = "gpt-4.1",
            messages = new object[]
            {
                new {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt },
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

        var apiKey = "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";

        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        Console.WriteLine("📡 Enviando solicitud a OpenAI...");

        var response = await httpClient.PostAsync(
            "https://api.openai.com/v1/chat/completions",
            new StringContent(System.Text.Json.JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json")
        );

        Console.WriteLine($"📬 Respuesta HTTP recibida de OpenAI: {response.StatusCode}");
        string result = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("❌ Error de OpenAI:\n" + result);
            return BadRequest("Error de OpenAI: " + result);
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(result);
            string content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            Console.WriteLine("✅ Resultado recibido de OpenAI:\n" + content);

            // Eliminar imagen temporal
            System.IO.File.Delete(tempPath);

            return Ok(new
            {
                resultado = content,
                nombreEsperado = nombreCliente
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Error al analizar la respuesta de OpenAI: " + ex.Message);
            Console.WriteLine("🔍 Contenido recibido:\n" + result);
            return BadRequest("Error procesando respuesta de OpenAI: " + ex.Message);
        }
    }
}
