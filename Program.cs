using System.Diagnostics;
using System.Drawing;
using System.Security.Cryptography;
using static System.Net.Mime.MediaTypeNames;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// --------------------------------------------------------------------------
// 1. ENDPOINT UTAMA: Informasi Server & Container (Sangat berguna untuk Horizontal Scaling)
// --------------------------------------------------------------------------
app.MapGet("/", async context =>
{
    var process = Process.GetCurrentProcess();
    var machineName = Environment.MachineName;
    var processorCount = Environment.ProcessorCount;
    var workingSetMb = process.WorkingSet64 / (1024 * 1024);

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.WriteAsync($$"""
        <!DOCTYPE html>
        <html lang="id">
        <head>
            <meta charset="UTF-8">
            <title>Dewacloud Scaling Demo</title>
            <style>
                body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background: #f8fafc; color: #1e293b; padding: 40px; text-align: center; }
                .card { background: white; max-width: 600px; margin: 0 auto; padding: 30px; border-radius: 12px; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1); border: 1px solid #e2e8f0; }
                h1 { color: #2563eb; margin-bottom: 8px; }
                .badge { background: #dbeafe; color: #1e40af; padding: 6px 14px; border-radius: 20px; font-weight: bold; display: inline-block; margin-bottom: 20px; }
                .metric { display: flex; justify-content: space-between; padding: 10px 0; border-bottom: 1px solid #f1f5f9; text-align: left; }
                .metric-label { color: #64748b; font-weight: 500; }
                .metric-value { font-weight: bold; color: #0f172a; }
                .btn { display: inline-block; margin-top: 15px; padding: 10px 20px; background: #2563eb; color: white; text-decoration: none; border-radius: 6px; font-weight: 600; }
            </style>
        </head>
        <body>
            <div class="card">
                <h1>Dewacloud Node Monitor</h1>
                <div class="badge">Node ID: {{machineName}}</div>
                <div class="metric"><span class="metric-label">Host / Container Name:</span><span class="metric-value">{{machineName}}</span></div>
                <div class="metric"><span class="metric-label">Tersedia CPU Core:</span><span class="metric-value">{{processorCount}} Cores</span></div>
                <div class="metric"><span class="metric-label">Penggunaan Memori (RAM):</span><span class="metric-value">{{workingSetMb}} MB</span></div>
                <div class="metric"><span class="metric-label">Sistem Operasi:</span><span class="metric-value">{{Environment.OSVersion}}</span></div>
                <br>
                <a href="/stress/cpu?seconds=10&threads=4" class="btn" target="_blank">Simulasi Beban CPU (10 detik)</a>
            </div>
        </body>
        </html>
        """);
});

// --------------------------------------------------------------------------
// 2. ENDPOINT STRESS CPU: Memaksa CPU Server ke 100% (Untuk Vertical / Horizontal Auto-scale)
// Contoh Panggilan: GET /stress/cpu?seconds=15&threads=4
// --------------------------------------------------------------------------
app.MapGet("/stress/cpu", (int? seconds, int? threads) =>
{
    int durationSec = seconds ?? 10;
    int threadCount = threads ?? Environment.ProcessorCount;

    var watch = Stopwatch.StartNew();
    var tasks = new List<Task>();

    for (int i = 0; i < threadCount; i++)
    {
        tasks.Add(Task.Run(() =>
        {
            var endAt = DateTime.UtcNow.AddSeconds(durationSec);
            using var sha256 = SHA256.Create();
            byte[] buffer = new byte[1024];

            // Loop komputasi berat tanpa sleep sampai batas waktu habis
            while (DateTime.UtcNow < endAt)
            {
                Random.Shared.NextBytes(buffer);
                _ = sha256.ComputeHash(buffer);
            }
        }));
    }

    Task.WaitAll(tasks.ToArray());
    watch.Stop();

    return Results.Ok(new
    {
        Status = "Sukses Menjalankan Stress Test CPU",
        ServerNode = Environment.MachineName,
        DurasiDetik = durationSec,
        ThreadDijalankan = threadCount,
        WaktuEksekusiMs = watch.ElapsedMilliseconds,
        Pesan = "Gunakan endpoint ini saat load testing dengan Apache JMeter atau k6!"
    });
});

// --------------------------------------------------------------------------
// 3. ENDPOINT STRESS MEMORY (RAM): Meralokasi Memori dalam Ukuran MB
// Contoh Panggilan: GET /stress/memory?megabytes=250
// --------------------------------------------------------------------------
app.MapGet("/stress/memory", (int? megabytes) =>
{
    int targetMb = megabytes ?? 256;
    List<byte[]> memoryHold = new List<byte[]>();

    try
    {
        for (int i = 0; i < targetMb; i++)
        {
            // Alokasi 1MB per iterasi
            memoryHold.Add(new byte[1024 * 1024]);
        }

        long allocatedMb = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);

        return Results.Ok(new
        {
            Status = "Alokasi Memori Berhasil",
            ServerNode = Environment.MachineName,
            MemoriDialokasikanMB = targetMb,
            TotalRAMTerpakaiMB = allocatedMb
        });
    }
    catch (OutOfMemoryException)
    {
        return Results.Problem($"Server kehabisan RAM saat mencoba mengalokasikan {targetMb} MB! Perlu Vertical Scaling (Tambah Cloudlet RAM).");
    }
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();