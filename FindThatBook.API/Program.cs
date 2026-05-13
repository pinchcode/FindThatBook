using FindThatBook.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddHttpClient<IGeminiService, GeminiService>();

builder.Services.AddHttpClient<IOpenLibraryService, OpenLibraryService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.Add("User-Agent", "FindThatBook/1.0 (book-search-app)");
});

builder.Services.AddScoped<IBookSearchService, BookSearchService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevClient", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("DevClient");
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseHttpsRedirection();
app.MapControllers();

// SPA fallback: serve React index.html for non-API routes in production
app.MapFallbackToFile("index.html");

app.Run();
