using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IClock, SystemClock>();
var app = builder.Build();
var api = app.MapGroup("/api");
api.MapGet("/time", ([FromServices] IClock clock) => clock.Now());
api.MapPost("/time", ReadTime);
// No Run/Start: acceptance only compiles and statically indexes this fixture.
static string ReadTime([FromServices] IClock clock) => clock.Now();
public interface IClock { string Now(); }
public sealed class SystemClock : IClock { public string Now() => DateTimeOffset.UtcNow.ToString("O"); }
