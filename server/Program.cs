using CrownroadServer;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var dbPath = app.Configuration["DbPath"] ?? "crownroad.db";
Database.Configure($"Data Source={dbPath}");
Database.Initialize();

app.MapPost("/player-profile", Endpoints.PlayerProfile);
app.MapPost("/challenge-sync", Endpoints.ChallengeSync);
app.MapGet("/challenge-boards", Endpoints.ChallengeLeaderboard);
app.MapGet("/challenge-feed", Endpoints.ChallengeFeed);
app.MapGet("/challenge-rooms", Endpoints.RoomDirectory);
app.MapPost("/challenge-room-create", Endpoints.RoomCreate);
app.MapPost("/challenge-room-join", Endpoints.RoomJoin);
app.MapGet("/challenge-room-session", Endpoints.RoomSession);
app.MapPost("/challenge-room-action", Endpoints.RoomAction);
app.MapPost("/challenge-room-result", Endpoints.RoomResult);
app.MapGet("/challenge-room-scoreboard", Endpoints.RoomScoreboard);
app.MapPost("/challenge-room-telemetry", Endpoints.RoomTelemetry);
app.MapPost("/challenge-room-leave", Endpoints.RoomLeave);
app.MapPost("/challenge-room-report", Endpoints.RoomReport);
app.MapPost("/challenge-room-matchmake", Endpoints.RoomMatchmake);
app.MapPost("/challenge-room-seat-lease", Endpoints.RoomSeatLease);

app.Run();
