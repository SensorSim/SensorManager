using Microsoft.EntityFrameworkCore;
using SensorManager.Data;
using SensorManager.Dtos;
using SensorManager.Models;
using SensorManager.Services;
using Confluent.Kafka;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<SensorDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"));
});

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Postgres")!);

// Kafka producer for config-change events
builder.Services.AddSingleton<IProducer<string, string>>(_ =>
{
    var bootstrapServers = builder.Configuration["Kafka:BootstrapServers"]
                           ?? builder.Configuration["Kafka__BootstrapServers"]
                           ?? "localhost:9092";

    var config = new ProducerConfig
    {
        BootstrapServers = bootstrapServers,
        EnableIdempotence = true
    };

    return new ProducerBuilder<string, string>(config).Build();
});

builder.Services.AddScoped<ISensorConfigEventPublisher, SensorConfigEventPublisher>();

var app = builder.Build();

// Ensure DB schema exists (works WITHOUT migrations; safe for docker-compose & k8s)
for (var i = 0; i < 30; i++)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SensorDbContext>();
        db.Database.EnsureCreated();
        break;
    }
    catch
    {
        Thread.Sleep(1000);
    }
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Ok(new { service = "sensor-manager", status = "ok" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapHealthChecks("/health/ready");

// ----------------------
// CRUD: Sensor Definitions
// ----------------------

app.MapGet("/sensors", async (
    SensorDbContext db,
    string? sensorType,
    bool? enabled,
    bool? simulate,
    int page = 1,
    int pageSize = 50) =>
{
    page = page <= 0 ? 1 : page;
    pageSize = pageSize <= 0 ? 50 : Math.Min(pageSize, 500);

    var q = db.Sensors.AsNoTracking();

    if (!string.IsNullOrWhiteSpace(sensorType))
        q = q.Where(s => s.SensorType == sensorType);

    if (enabled is not null)
        q = q.Where(s => s.Enabled == enabled);

    if (simulate is not null)
        q = q.Where(s => s.Simulate == simulate);

    var total = await q.CountAsync();

    var items = await q
        .OrderBy(s => s.SensorId)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(s => new SensorDefinitionOut(
            s.Id,
            s.SensorId,
            s.SensorType,
            s.Unit,
            s.OperatingMin,
            s.OperatingMax,
            s.WarningMin,
            s.WarningMax,
            s.IntervalMs,
            s.Enabled,
            s.Simulate,
            s.UpdatedAt))
        .ToListAsync();

    return Results.Ok(new { page, pageSize, total, items });
});

app.MapGet("/sensors/{id:guid}", async (Guid id, SensorDbContext db) =>
{
    var s = await db.Sensors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    if (s is null) return Results.NotFound();

    return Results.Ok(new SensorDefinitionOut(
        s.Id, s.SensorId, s.SensorType, s.Unit,
        s.OperatingMin, s.OperatingMax, s.WarningMin, s.WarningMax,
        s.IntervalMs, s.Enabled, s.Simulate, s.UpdatedAt));
});

// Handy lookup by sensorId (string)
app.MapGet("/sensors/by-sensorId/{sensorId}", async (string sensorId, SensorDbContext db) =>
{
    var s = await db.Sensors.AsNoTracking().FirstOrDefaultAsync(x => x.SensorId == sensorId);
    if (s is null) return Results.NotFound();

    return Results.Ok(new SensorDefinitionOut(
        s.Id, s.SensorId, s.SensorType, s.Unit,
        s.OperatingMin, s.OperatingMax, s.WarningMin, s.WarningMax,
        s.IntervalMs, s.Enabled, s.Simulate, s.UpdatedAt));
});

app.MapPost("/sensors", async (
    SensorDefinitionIn input,
    SensorDbContext db,
    ISensorConfigEventPublisher publisher,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(input.SensorId)) return Results.BadRequest("sensorId is required");
    if (string.IsNullOrWhiteSpace(input.SensorType)) return Results.BadRequest("sensorType is required");
    if (string.IsNullOrWhiteSpace(input.Unit)) return Results.BadRequest("unit is required");

    var exists = await db.Sensors.AnyAsync(s => s.SensorId == input.SensorId, ct);
    if (exists) return Results.Conflict($"SensorId '{input.SensorId}' already exists");

    var s = new SensorDefinition
    {
        SensorId = input.SensorId,
        SensorType = input.SensorType,
        Unit = input.Unit,
        OperatingMin = input.OperatingMin,
        OperatingMax = input.OperatingMax,
        WarningMin = input.WarningMin,
        WarningMax = input.WarningMax,
        IntervalMs = input.IntervalMs <= 0 ? 1000 : input.IntervalMs,
        Enabled = input.Enabled,
        Simulate = input.Simulate,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    db.Sensors.Add(s);
    await db.SaveChangesAsync(ct);

    var outDto = new SensorDefinitionOut(
        s.Id, s.SensorId, s.SensorType, s.Unit,
        s.OperatingMin, s.OperatingMax, s.WarningMin, s.WarningMax,
        s.IntervalMs, s.Enabled, s.Simulate, s.UpdatedAt);

    await publisher.PublishAsync(
        new SensorConfigChangedEvent("created", s.SensorId, DateTimeOffset.UtcNow, outDto),
        ct);

    return Results.Created($"/sensors/{s.Id}", outDto);
});

app.MapPut("/sensors/{id:guid}", async (
    Guid id,
    SensorDefinitionIn input,
    SensorDbContext db,
    ISensorConfigEventPublisher publisher,
    CancellationToken ct) =>
{
    var s = await db.Sensors.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (s is null) return Results.NotFound();

    // SensorId immutable
    s.SensorType = input.SensorType;
    s.Unit = input.Unit;
    s.OperatingMin = input.OperatingMin;
    s.OperatingMax = input.OperatingMax;
    s.WarningMin = input.WarningMin;
    s.WarningMax = input.WarningMax;
    s.IntervalMs = input.IntervalMs <= 0 ? s.IntervalMs : input.IntervalMs;
    s.Enabled = input.Enabled;
    s.Simulate = input.Simulate;
    s.UpdatedAt = DateTimeOffset.UtcNow;

    await db.SaveChangesAsync(ct);

    var outDto = new SensorDefinitionOut(
        s.Id, s.SensorId, s.SensorType, s.Unit,
        s.OperatingMin, s.OperatingMax, s.WarningMin, s.WarningMax,
        s.IntervalMs, s.Enabled, s.Simulate, s.UpdatedAt);

    await publisher.PublishAsync(
        new SensorConfigChangedEvent("updated", s.SensorId, DateTimeOffset.UtcNow, outDto),
        ct);

    return Results.Ok(outDto);
});

// Update by sensorId (string)
app.MapPut("/sensors/by-sensorId/{sensorId}", async (
    string sensorId,
    SensorDefinitionIn input,
    SensorDbContext db,
    ISensorConfigEventPublisher publisher,
    CancellationToken ct) =>
{
    var s = await db.Sensors.FirstOrDefaultAsync(x => x.SensorId == sensorId, ct);
    if (s is null) return Results.NotFound();

    s.SensorType = input.SensorType;
    s.Unit = input.Unit;
    s.OperatingMin = input.OperatingMin;
    s.OperatingMax = input.OperatingMax;
    s.WarningMin = input.WarningMin;
    s.WarningMax = input.WarningMax;
    s.IntervalMs = input.IntervalMs <= 0 ? s.IntervalMs : input.IntervalMs;
    s.Enabled = input.Enabled;
    s.Simulate = input.Simulate;
    s.UpdatedAt = DateTimeOffset.UtcNow;

    await db.SaveChangesAsync(ct);

    var outDto = new SensorDefinitionOut(
        s.Id, s.SensorId, s.SensorType, s.Unit,
        s.OperatingMin, s.OperatingMax, s.WarningMin, s.WarningMax,
        s.IntervalMs, s.Enabled, s.Simulate, s.UpdatedAt);

    await publisher.PublishAsync(
        new SensorConfigChangedEvent("updated", s.SensorId, DateTimeOffset.UtcNow, outDto),
        ct);

    return Results.Ok(outDto);
});

app.MapDelete("/sensors/{id:guid}", async (
    Guid id,
    SensorDbContext db,
    ISensorConfigEventPublisher publisher,
    CancellationToken ct) =>
{
    var s = await db.Sensors.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (s is null) return Results.NotFound();

    var sensorId = s.SensorId;

    db.Sensors.Remove(s);
    await db.SaveChangesAsync(ct);

    await publisher.PublishAsync(
        new SensorConfigChangedEvent("deleted", sensorId, DateTimeOffset.UtcNow, null),
        ct);

    return Results.NoContent();
});

// Delete by sensorId (string)
app.MapDelete("/sensors/by-sensorId/{sensorId}", async (
    string sensorId,
    SensorDbContext db,
    ISensorConfigEventPublisher publisher,
    CancellationToken ct) =>
{
    var s = await db.Sensors.FirstOrDefaultAsync(x => x.SensorId == sensorId, ct);
    if (s is null) return Results.NotFound();

    db.Sensors.Remove(s);
    await db.SaveChangesAsync(ct);

    await publisher.PublishAsync(
        new SensorConfigChangedEvent("deleted", sensorId, DateTimeOffset.UtcNow, null),
        ct);

    return Results.NoContent();
});

app.Run();
