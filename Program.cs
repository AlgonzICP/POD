var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(8080); // 👈 Puerto requerido por Render
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("https://frontend-verificador-pod.vercel.app") // ✅ tu frontend en Vercel
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Servicios de la app
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Entorno de desarrollo
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

// 👇 CORS debe ir antes de Authorization y MapControllers
app.UseCors("AllowFrontend");

app.UseAuthorization();
app.MapControllers();

app.Run();
