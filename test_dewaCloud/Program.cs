var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapGet("/api/home", async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.WriteAsync("""
            <!DOCTYPE html>
        <html>
        <head>
            <title>Homepage Demo</title>
            <style>
                body {
                    font-family: Arial, sans-serif;
                    text-align: center;
                    margin-top: 100px;
                }

                h1 {
                    font-size: 48px;
                    color: #2563eb;
                }
            </style>
        </head>
        <body>
            <h1>This is a homepage</h1>
        </body>
        </html>
        """);
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
