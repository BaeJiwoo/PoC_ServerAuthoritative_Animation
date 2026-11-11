using ServerAuthoritativeAnimation.Core.Animation;
using ServerAuthoritativeAnimation.Server.Networking;

var builder = WebApplication.CreateBuilder(args);
var animationPath = builder.Configuration["animation"] ??
    Path.Combine(AppContext.BaseDirectory, "Animations", "BobWalkForward.json");
var animation = BakedAnimationClip.Load(animationPath);
builder.Services.AddSingleton(animation);
builder.Services.AddSingleton<WebSocketCharacterSession>();

var app = builder.Build();
app.Logger.LogInformation("Loaded {Animation}: {Bones} transforms, {Duration}s, skeleton {Id}",
    animation.Name, animation.BoneCount, animation.DurationSeconds, animation.Id);

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30),
});

app.MapGet("/", () => Results.Ok(new
{
    service = "ServerAuthoritativeAnimation",
    websocket = "/ws",
    tickRate = WebSocketCharacterSession.TickRate,
    animation = animation.Name,
    skeletonId = animation.Id,
    boneCount = animation.BoneCount,
}));

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("A WebSocket upgrade request is required.");
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var session = context.RequestServices.GetRequiredService<WebSocketCharacterSession>();
    await session.RunAsync(socket, context.RequestAborted);
});

app.Run();

public partial class Program;
