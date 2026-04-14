using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using PdfEditorApi.Data;
using PdfEditorApi.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 52_428_800;
});

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton<IPdfEditorService, PdfEditorService>();
builder.Services.AddScoped<ISOPWorkflowService, SOPWorkflowService>();
builder.Services.AddDbContext<SOPDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("PDFReaderDB"));
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52_428_800;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SOPDbContext>();
    await DatabaseSchemaBootstrap.EnsureSopInstanceColumnsAsync(db);
    await DatabaseSchemaBootstrap.EnsureInitiatorUserExistsAsync(db);
    await SOPSeedData.EnsureSeededAsync(db);
}

app.UseCors("ReactApp");
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.MapControllers();
app.Run();
