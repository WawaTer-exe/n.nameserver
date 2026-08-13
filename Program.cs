using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// Assign community custom private server execution port
builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(20592));
builder.Services.AddCors();

var app = builder.Build();
app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

// --- MULTIPLAYER PHOTON CLOUD CREDENTIALS ---
const string PUN_APP_ID = "c71b73cd-63e9-4d3b-b991-e9fb6a325c70";
const string VOICE_APP_ID = "9da3c796-a2a8-4f67-936f-7f66b1906937";

// --- PERSISTENT STORAGE LAYER ENGINE ---
// Paths are dynamically shifted to look out of the root folder when nested inside src/
string baseRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
string storageDir = Path.Combine(baseRoot, "NameServerStorage");
string profilesDir = Path.Combine(storageDir, "Profiles");
string avatarsDir = Path.Combine(storageDir, "Avatars");
string contentDir = Path.Combine(baseRoot, "content");
string roomsDir = Path.Combine(storageDir, "SavedRooms");

Directory.CreateDirectory(storageDir);
Directory.CreateDirectory(profilesDir);
Directory.CreateDirectory(avatarsDir);
Directory.CreateDirectory(contentDir);
Directory.CreateDirectory(roomsDir);

// Initialize a blank avatar asset file to prevent wristwatch menu loading hangs
string avatarItemsPath = Path.Combine(contentDir, "avatar_items.json");
if (!File.Exists(avatarItemsPath))
{
    File.WriteAllText(avatarItemsPath, "[]");
}

// Static Base Game Maps
var baseMaps = new List<object> {
    new { RoomId = 1, Name = "Orientation", SceneName = "Orientation", MaxPlayers = 20, CreatorPlayerId = 1, SupportsMakerPen = false },
    new { RoomId = 2, Name = "RecCenter", SceneName = "RecCenter", MaxPlayers = 20, CreatorPlayerId = 1, SupportsMakerPen = false },
    new { RoomId = 3, Name = "Paintball", SceneName = "Paintball_Clearcut", MaxPlayers = 8, CreatorPlayerId = 1, SupportsMakerPen = false },
    new { RoomId = 4, Name = "GoldenTrophy", SceneName = "Quest_Castle", MaxPlayers = 4, CreatorPlayerId = 1, SupportsMakerPen = false }
};

var unlockedWardrobeItems = new[] {
    new { Id = 1, ItemId = "hair_classic", Type = 1, FavColor = 4 },
    new { Id = 2, ItemId = "hat_baseball_backward", Type = 2, FavColor = 1 },
    new { Id = 3, ItemId = "shirt_hoodie", Type = 3, FavColor = 2 },
    new { Id = 4, ItemId = "pants_jeans", Type = 4, FavColor = 0 }
};

// --- GLOBAL MEMORY STACKS FOR SOCIAL MATRICES ---
var ActiveSessions = new Dictionary<string, int>(); 
var ActiveParties = new Dictionary<int, List<int>>(); 

// --- HELPER CLASSES FOR SYSTEM RE-SERIALIZATION ---
public class AccountProfile {
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int XP { get; set; } = 95000;
    public int Level { get; set; } = 50;
    public int Credits { get; set; } = 25000;
    public int RegistrationStatus { get; set; } = 2;
    public bool Developer { get; set; } = true;
    public string AssociatedPlatformId { get; set; } = "";
}

public class CustomRoom {
    public int RoomId { get; set; }
    public string Name { get; set; } = "";
    public string SceneName { get; set; } = "DormRoom";
    public int CreatorPlayerId { get; set; }
    public string Description { get; set; } = "A custom sandbox world created on n.NameServers.";
    public int MaxPlayers { get; set; } = 20;
    public bool IsPrivate { get; set; } = false;
}

public class RelationshipModel {
    public int PlayerID { get; set; }
    public int Type { get; set; } = 3; 
    public int Favorited { get; set; } = 0;
    public int Muted { get; set; } = 0;
    public int Ignored { get; set; } = 0;
}

// Helper scanning files on disk to confirm data constraints
public static class ProfileDatabase {
    public static AccountProfile? FindByUsername(string directory, string username) {
        foreach (var file in Directory.GetFiles(directory, "*.json")) {
            try {
                var json = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<AccountProfile>(json);
                if (profile != null && profile.Username.Equals(username, StringComparison.OrdinalIgnoreCase)) {
                    return profile;
                }
            } catch { }
        }
        return null;
    }

    public static AccountProfile? FindById(string directory, int id) {
        foreach (var file in Directory.GetFiles(directory, "*.json")) {
            try {
                var json = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<AccountProfile>(json);
                if (profile != null && profile.Id == id) return profile;
            } catch { }
        }
        return null;
    }
    
    public static List<AccountProfile> GetAllProfiles(string directory) {
        var list = new List<AccountProfile>();
        foreach (var file in Directory.GetFiles(directory, "*.json")) {
            try {
                var json = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<AccountProfile>(json);
                if (profile != null) list.Add(profile);
            } catch { }
        }
        return list;
    }
}

// --- SYSTEM TRAFFIC MONITOR MIDDLEWARE ---
app.Use(async (context, next) =>
{
    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {context.Request.Method} -> {context.Request.Path}");
    await next.Invoke();
});

// --- n.NameServers API ROUTING CORE ---

app.MapGet("/api/config/v2", () => Results.Json(new Dictionary<string, object>
{
    { "App.Photon.ServerAddress", "://photonengine.com" },
    { "App.Photon.Port", "5055" },
    { "App.Matchmaking.Enabled", true },
    { "App.ContentServerUrl", "http://localhost:20592/content" },
    { "App.Leaderboard.Enabled", true },
    { "App.Gifting.Enabled", false },
    { "App.Store.Enabled", true }
}));

app.MapGet("/api/config/v1/amplitude", () => Results.Json(new { ApiKey = "nameservers-mock-key" }));
app.MapGet("/api/versioncheck/v3", () => Results.Json(new { Valid = true, Message = "n.NameServers Checked Successfully" }));

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(contentDir),
    RequestPath = "/content"
});

// Dynamic Identity Gateway Interceptor
app.MapPost("/api/players/v1/v2/platformLogin", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var rawBody = await reader.ReadToEndAsync();
    using var doc = JsonDocument.Parse(rawBody);
    var root = doc.RootElement;

    string platformId = root.TryGetProperty("PlatformId", out var pid) ? pid.GetString() ?? "76561198000000000" : "76561198000000000";
    if (platformId == "0" || string.IsNullOrWhiteSpace(platformId)) platformId = "76561198000000000";

    string profilePath = Path.Combine(profilesDir, $"{platformId}.json");
    string token = $"jwt-token-{platformId}";

    if (!File.Exists(profilePath))
    {
        return Results.Json(new { 
            Token = token, 
            PlayerId = 0, 
            IsNewAccount = true,
            PlatformId = platformId
        });
    }

    var accountData = JsonSerializer.Deserialize<AccountProfile>(await File.ReadAllTextAsync(profilePath));
    if (accountData != null) ActiveSessions[token] = accountData.Id;

    return Results.Json(new { 
        Token = token, 
        PlayerId = accountData?.Id ?? 10001, 
        IsNewAccount = false,
        PlatformId = platformId
    });
});

// Interactive Account Creation and Duplicate Handle verification blocker
app.MapPost("/api/players/v1/v2/create", async (HttpRequest request, HttpContext context) => 
{
    using var reader = new StreamReader(request.Body);
    var rawBody = await reader.ReadToEndAsync();
    using var doc = JsonDocument.Parse(rawBody);
    var root = doc.RootElement;

    string requestedUsername = root.TryGetProperty("Username", out var userProp) ? userProp.GetString() ?? "" : "";
    string requestedDisplayName = root.TryGetProperty("DisplayName", out var dispProp) ? dispProp.GetString() ?? requestedUsername : requestedUsername;

    string authHeader = context.Request.Headers["Authorization"].ToString();
    string platformId = authHeader.Replace("Bearer jwt-token-", "").Trim();
    if (string.IsNullOrWhiteSpace(platformId)) platformId = "76561198000000000";

    Console.WriteLine($"[REGISTRATION] Checking allocation availability for username: @{requestedUsername}");

    var existingMatch = ProfileDatabase.FindByUsername(profilesDir, requestedUsername);
    if (existingMatch != null)
    {
        context.Response.StatusCode = 400;
        return Results.Json(new { Error = "Username is already taken!" });
    }

    var assignedId = new Random().Next(10000, 99999);
    var freshProfile = new AccountProfile {
        Id = assignedId,
        Username = requestedUsername,
        DisplayName = requestedDisplayName,
        AssociatedPlatformId = platformId
    };

    string savePath = Path.Combine(profilesDir, $"{platformId}.json");
    await File.WriteAllTextAsync(savePath, JsonSerializer.Serialize(freshProfile));

    string avatarPath = Path.Combine(avatarsDir, $"{assignedId}.json");
    var baseOutfit = new Dictionary<string, object> {
        { "SkinColor", 2 }, { "FaceColor", 0 },
        { "Hair", new { ItemId = "hair_classic", Color = 4 } },
        { "Hat", new { ItemId = "hat_baseball_backward", Color = 1 } },
        { "Shirt", new { ItemId = "shirt_hoodie", Color = 2 } },
        { "Face", new { Eyes = 1, Mouth = 2 } },
        { "Gloves", new { ItemId = "none", Color = 0 } }
    };
  ```csharp
await File.WriteAllTextAsync(avatarPath, JsonSerializer.Serialize(baseOutfit));
ActiveSessions[$"jwt-token-{platformId}"] = assignedId;
return Results.Json(new { Success = true, PlayerId = assignedId, Token = $"jwt-token-{platformId}" });
});

app.MapPost("/api/v1/auth/login", () => Results.Json(new { Success = true, Token = "jwt-fallback-pass", PlayerId = 10001 }));

app.MapGet("/api/players/v1/me", async (HttpContext context) => {
    string authHeader = context.Request.Headers["Authorization"].ToString();
    string platformId = authHeader.Replace("Bearer jwt-token-", "").Trim();
    if (string.IsNullOrWhiteSpace(platformId) || authHeader == "") platformId = "76561198000000000";
    string profilePath = Path.Combine(profilesDir, $"{platformId}.json");
    if (!File.Exists(profilePath)) {
        return Results.Json(new AccountProfile { Id = 10001, Username = "Unregistered", DisplayName = "New Coach" });
    }
    var rawJson = await File.ReadAllTextAsync(profilePath);
    return Results.Content(rawJson, "application/json");
});

// Wardrobe Layout Handlers
app.MapGet("/api/avatar/v2", async (HttpContext context) => {
    string authHeader = context.Request.Headers["Authorization"].ToString();
    string platformId = authHeader.Replace("Bearer jwt-token-", "").Trim();
    if (string.IsNullOrWhiteSpace(platformId) || authHeader == "") platformId = "76561198000000000";
    string profilePath = Path.Combine(profilesDir, $"{platformId}.json");
    int playerId = 10001;
    if (File.Exists(profilePath)) {
        var account = JsonSerializer.Deserialize(await File.ReadAllTextAsync(profilePath));
        playerId = account?.Id ?? 10001;
    }
    string userAvatarPath = Path.Combine(avatarsDir, $"{playerId}.json");
    if (!File.Exists(userAvatarPath)) return Results.Json(new { SkinColor = 2, FaceColor = 0 });
    var rawJson = await File.ReadAllTextAsync(userAvatarPath);
    return Results.Content(rawJson, "application/json");
});

app.MapPost("/api/avatar/v2/set", async (HttpRequest request, HttpContext context) => {
    string authHeader = context.Request.Headers["Authorization"].ToString();
    string platformId = authHeader.Replace("Bearer jwt-token-", "").Trim();
    if (string.IsNullOrWhiteSpace(platformId) || authHeader == "") platformId = "76561198000000000";
    string profilePath = Path.Combine(profilesDir, $"{platformId}.json");
    int playerId = 10001;
    if (File.Exists(profilePath)) {
        var account = JsonSerializer.Deserialize(await File.ReadAllTextAsync(profilePath));
        playerId = account?.Id ?? 10001;
    }
    using var streamReader = new StreamReader(request.Body);
    var updatedBody = await streamReader.ReadToEndAsync();
    string userAvatarPath = Path.Combine(avatarsDir, $"{playerId}.json");
    await File.WriteAllTextAsync(userAvatarPath, updatedBody);
    return Results.Json(new { Success = true });
});

app.MapGet("/api/inventory/v1/get", () => Results.Json(unlockedWardrobeItems));

app.MapGet("/api/inventory/v1/currency", async (HttpContext context) => {
    string authHeader = context.Request.Headers["Authorization"].ToString();
    string platformId = authHeader.Replace("Bearer jwt-token-", "").Trim();
    if (string.IsNullOrWhiteSpace(platformId) || authHeader == "") platformId = "76561198000000000";
    string profilePath = Path.Combine(profilesDir, $"{platformId}.json");
    int balance = 25000;
    if (File.Exists(profilePath)) {
        var account = JsonSerializer.Deserialize(await File.ReadAllTextAsync(profilePath));
        balance = account?.Credits ?? 25000;
    }
    return Results.Json(new { CurrencyType = 0, Balance = balance });
});

// Maker Pen Custom Room Engines
app.MapGet("/api/rooms/v1/featured", () => {
    var compositeList = new List(baseMaps);
    foreach (var file in Directory.GetFiles(roomsDir, "*.json")) {
        try {
            var rawMeta = File.ReadAllText(file);
            var roomObj = JsonSerializer.Deserialize(rawMeta);
            if (roomObj != null) compositeList.Add(roomObj);
        } catch { }
    }
    return Results.Json(compositeList);
});

app.MapGet("/api/rooms/v2/myrooms", () => Results.Json(Array.Empty()));

app.MapPost("/api/rooms/v4/create", async (HttpRequest request, HttpContext context) => {
    using var reader = new StreamReader(request.Body);
    var rawBody = await reader.ReadToEndAsync();
    using var doc = JsonDocument.Parse(rawBody);
    var root = doc.RootElement;
    string requestedName = root.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() ?? "NewRoom" : "NewRoom";
    string targetScene = root.TryGetProperty("SceneName", out var sceneProp) ? sceneProp.GetString() ?? "DormRoom" : "DormRoom";
    string authHeader = context.Request.Headers["Authorization"].ToString();
    string platformId = authHeader.Replace("Bearer jwt-token-", "").Trim();
    int playerId = 10001;
    string profilePath = Path.Combine(profilesDir, $"{platformId}.json");
    if (File.Exists(profilePath)) {
        var account = JsonSerializer.Deserialize(await File.ReadAllTextAsync(profilePath));
        playerId = account?.Id ?? 10001;
    }
    int generatedRoomId = new Random().Next(20000, 99999);
    var newWorldManifest = new CustomRoom { RoomId = generatedRoomId, Name = requestedName, SceneName = targetScene, CreatorPlayerId = playerId };
    string roomMetaPath = Path.Combine(roomsDir, $"{generatedRoomId}.json");
    await File.WriteAllTextAsync(roomMetaPath, JsonSerializer.Serialize(newWorldManifest));
    return Results.Json(newWorldManifest);
});

app.MapPost("/api/rooms/v2/save", async (HttpRequest request) => {
    string? roomIdQuery = request.Query["roomId"];
    if (string.IsNullOrWhiteSpace(roomIdQuery)) roomIdQuery = "latest_upload";
    string targetBinarySavePath = Path.Combine(roomsDir, $"{roomIdQuery}.room");
    using var fileStream = File.Create(targetBinarySavePath);
    await request.Body.CopyToAsync(fileStream);
    return Results.Json(new { Success = true });
});

// --- SOCIAL AND FRIENDING SOCIAL MATRIX ENGINE ---
app.MapGet("/api/relationships/v1/get", (HttpContext context) => {
    string authHeader = context.Request.Headers["Authorization"].ToString();
    string platformId = authHeader.Replace("Bearer jwt-token-", "").Trim();
    int currentUserId = 10001;
    string currentProfilePath = Path.Combine(profilesDir, $"{platformId}.json");
    if (File.Exists(currentProfilePath)) {
        var account = JsonSerializer.Deserialize(File.ReadAllText(currentProfilePath));
        currentUserId = account?.Id ?? 10001;
    }
    var dynamicFriendships = new List();
    var totalAccounts = ProfileDatabase.GetAllProfiles(profilesDir);
    foreach (var account in totalAccounts) {
        if (account.Id != currentUserId) {
            dynamicFriendships.Add(new RelationshipModel { PlayerID = account.Id });
        }
    }
    return Results.Json(dynamicFriendships);
});

app.MapPost("/api/players/v1/search", async (HttpRequest request) => {
    using var reader = new StreamReader(request.Body);
    var rawBody = await reader.ReadToEndAsync();
    using var doc = JsonDocument.Parse(rawBody);
    string searchVal = doc.RootElement.TryGetProperty("Query", out var q) ? q.GetString() ?? "" : "";
    var allProfiles = ProfileDatabase.GetAllProfiles(profilesDir);
    var matchedProfiles = allProfiles.Where(p => p.Username.Contains(searchVal, StringComparison.OrdinalIgnoreCase)).Take(10);
    return Results.Json(matchedProfiles);
});

app.MapPost("/api/players/v1/list", async (HttpRequest request) => {
    using var reader = new StreamReader(request.Body);
    var rawBody = await reader.ReadToEndAsync();
    var playerIds = JsonSerializer.Deserialize<List>(rawBody) ?? new List();
    var compositeProfilesList = new List();
    foreach (var id in playerIds) {
        var profile = ProfileDatabase.FindById(profilesDir, id);
        if (profile != null) compositeProfilesList.Add(profile);
    }
    return Results.Json(compositeProfilesList);
});

// --- PARTYING AND GROUP INVITE INTERFACES ---
app.MapPost("/api/relationships/v1/partyInvite", async (HttpRequest request, HttpContext context) => {
    using var reader = new StreamReader(request.Body);
    var rawBody = await reader.ReadToEndAsync();
    using var doc = JsonDocument.Parse(rawBody);
    int targetFriendId = doc.RootElement.TryGetProperty("PlayerId", out var pid) ? pid.GetInt32() : 0;
    string authHeader = context.Request.Headers["Authorization"].ToString();
    string platformId = authHeader.Replace("Bearer jwt-token-", "").Trim();
    int currentUserId = 10001;
    string currentProfilePath = Path.Combine(profilesDir, $"{platformId}.json");
    if (File.Exists(currentProfilePath)) {
        var account = JsonSerializer.Deserialize(File.ReadAllText(currentProfilePath));
        currentUserId = account?.Id ?? 10001;
    }
    if (!ActiveParties.ContainsKey(currentUserId)) {
        ActiveParties[currentUserId] = new List { currentUserId };
    }
    if (!ActiveParties[currentUserId].Contains(targetFriendId) && targetFriendId != 0) {
        ActiveParties[currentUserId].Add(targetFriendId);
    }
    return Results.Json(new { Success = true });
});

app.MapGet("/api/relationships/v1/party", (HttpContext context) => {
    string authHeader = context.Request.Headers["Authorization"].ToString();
    string platformId = authHeader.Replace("Bearer jwt-token-", "").Trim();
    int currentUserId = 10001;
    string currentProfilePath = Path.Combine(profilesDir, $"{platformId}.json");
    if (File.Exists(currentProfilePath)) {
        var account = JsonSerializer.Deserialize(File.ReadAllText(currentProfilePath));
        currentUserId = account?.Id ?? 10001;
    }
    foreach (var party in ActiveParties) {
        if (party.Value.Contains(currentUserId)) {
            return Results.Json(new { GroupId = party.Key, LeaderId = party.Key, Members = party.Value });
        }
    }
    return Results.Json(new { GroupId = 0, LeaderId = 0, Members = new List() });
});

// Matchmaking Router Room joining
app.MapPost("/api/matchmaking/v1/join", async (HttpRequest request) => {
    var dataPayload = await request.ReadFromJsonAsync<Dictionary<string, string>>();
    string selectedScene = dataPayload != null && dataPayload.TryGetValue("RoomName", out var name) ? name : "RecCenter";
    var seed = new Random();
    return Results.Json(new {
        Success = true,
        RoomInstance = new {
            RoomInstanceId = seed.Next(1000, 9999),
            RoomId = seed.Next(1, 10),
            Name = $"{selectedScene}-{seed.Next(10, 99)}",
            RoomCode = $"NS-{seed.Next(1000, 9999)}",
            PlayerCount = 1,
            MaxPlayers = 20,
            SceneName = selectedScene,
            PhotonRegion = "us",
            IsPrivate = false,
            CustomProperties = new Dictionary<string, string> {
                { "pun_id", PUN_APP_ID },
                { "voice_id", VOICE_APP_ID }
            }
        }
    });
});

app.MapPost("/api/matchmaking/v1/switch", () => Results.Json(new { Success = true }));

app.MapGet("/api/relationships/v1/get", () => Results.Json(Array.Empty()));

app.MapGet("/", () => "n.NameServers Master Multi-User Social Framework Online.");

Console.WriteLine("====================================================");
Console.WriteLine("REPOSITORY RUNNING: n.NameServers Social Matrix Active");
Console.WriteLine("SYSTEM CODES: Friend Matrices & Party Invites Fully Functional");
Console.WriteLine("====================================================");
// =========================================================================
// 🎛️ COMPLETE INTERACTIVE CONSOLE ADMIN PANEL (above?();)
// =========================================================================
_ = Task.Run(async () => {
    // 2-second boot buffer to let the server print its launch logs first
    await Task.Delay(2000); 

    while (true)
    {
        string? input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input)) continue;

        var parts = input.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) continue;
        
        string cmd = parts[0].ToLower();

        if (cmd == "help")
        {
            Console.WriteLine("\n[n.NameServers ADMIN INTERFACE COMMANDS]");
            Console.WriteLine("  list                  - Lists every registered profile signature saved on disk.");
            Console.WriteLine("  givecreds <name> <val>- Directly updates a specific player's token currency wallet balance.");
            Console.WriteLine("  setlevel <name> <lvl> - Modifies player XP parameters and increments Watch leveling display.");
            Console.WriteLine("  devflag <name> <t/f>  - Toggles the developer/moderator permissions tags on player watches.");
            Console.WriteLine("  broadcast <message>   - Sends a global text notification alert to all active player watches.");
            Console.WriteLine("  ban <username>        - Ban-locks an account and terminates device authorization.");
            Console.WriteLine("  unban <username>      - Removes ban restrictions from a specific username.");
            Console.WriteLine("  shutdown              - Safely disconnects the server routes and closes the application.");
        }
        else if (cmd == "list")
        {
            string profilesPath = Path.Combine(AppContext.BaseDirectory, "NameServerStorage", "Profiles");
            if (!Directory.Exists(profilesPath))
            {
                Console.WriteLine("[ADMIN INFO] No player profiles folder found yet. Run the game to generate accounts.");
                continue;
            }
            foreach (var file in Directory.GetFiles(profilesPath, "*.json"))
            {
                try {
                    using var doc = JsonDocument.Parse(File.ReadAllText(file));
                    var root = doc.RootElement;
                    Console.WriteLine($"  ID: {root.GetProperty("Id")} | @{root.GetProperty("Username")} | Tokens: {root.GetProperty("Credits")} | Lvl: {root.GetProperty("Level")}");
                } catch { }
            }
        }
        else if (cmd == "broadcast" && parts.Length >= 2)
        {
            string globalMessage = string.Join(" ", parts.Skip(1));
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n[BROADCAST] Sending server-wide alert: \"{globalMessage}\"");
            Console.ResetColor();

            // Overrides the client version check endpoint so the message flashes onto player watches
            app.MapGet("/api/versioncheck/v3", () => Results.Json(new { 
                Valid = true, 
                Message = $"ALERT: {globalMessage}" 
            }));
        }
        else if (cmd == "shutdown")
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n====================================================");
            Console.WriteLine("[SHUTDOWN] Closing server routes and stopping port 20592 cleanly...");
            Console.WriteLine("====================================================");
            Console.ResetColor();
            await Task.Delay(1000);
            Environment.Exit(0);
        }
        else if ((cmd == "givecreds" || cmd == "setlevel" || cmd == "devflag" || cmd == "ban" || cmd == "unban") && parts.Length >= 2)
        {
            string targetUser = parts[1];
            string valueInput = parts.Length >= 3 ? parts[2] : "";
            string profilesPath = Path.Combine(AppContext.BaseDirectory, "NameServerStorage", "Profiles");

            if (!Directory.Exists(profilesPath)) { Console.WriteLine("[ADMIN ERROR] No data profiles folder found."); continue; }

            string? targetFile = Directory.GetFiles(profilesPath, "*.json")
                .FirstOrDefault(f => {
                    try {
                        using var doc = JsonDocument.Parse(File.ReadAllText(f));
                        return doc.RootElement.GetProperty("Username").GetString()?.Equals(targetUser, StringComparison.OrdinalIgnoreCase) ?? false;
                    } catch { return false; }
                });

            if (targetFile != null)
            {
                try
                {
                    var rawJson = File.ReadAllText(targetFile);
                    var jsonDict = JsonSerializer.Deserialize<Dictionary<string, object>>(rawJson);

                    if (jsonDict != null)
                    {
                        if (cmd == "givecreds" && int.TryParse(valueInput, out int amt))
                        {
                            long currentCredits = jsonDict.ContainsKey("Credits") ? Convert.ToInt64(jsonDict["Credits"].ToString()) : 0;
                            jsonDict["Credits"] = currentCredits + amt;
                            Console.WriteLine($"[ADMIN SUCCESS] Granted {amt} tokens to @{targetUser}.");
                        }
                        else if (cmd == "setlevel" && int.TryParse(valueInput, out int lvl))
                        {
                            jsonDict["Level"] = lvl;
                            jsonDict["XP"] = lvl * 2000;
                            Console.WriteLine($"[ADMIN SUCCESS] Updated level for @{targetUser} to: Lvl {lvl}");
                        }
                        else if (cmd == "devflag" && bool.TryParse(valueInput, out bool dev))
                        {
                            jsonDict["Developer"] = dev;
                            Console.WriteLine($"[ADMIN SUCCESS] Set developer flag for @{targetUser} to: {dev}");
                        }
                        else if (cmd == "ban")
                        {
                            string platformId = jsonDict.ContainsKey("AssociatedPlatformId") ? jsonDict["AssociatedPlatformId"].ToString() ?? "" : "";
                            if (!string.IsNullOrWhiteSpace(platformId)) BannedPlatforms.Add(platformId);
                            Console.WriteLine($"[ADMIN SUCCESS] Blacklisted device footprint for @{targetUser}.");
                        }
                        else if (cmd == "unban")
                        {
                            string platformId = jsonDict.ContainsKey("AssociatedPlatformId") ? jsonDict["AssociatedPlatformId"].ToString() ?? "" : "";
                            if (!string.IsNullOrWhiteSpace(platformId)) BannedPlatforms.Remove(platformId);
                            Console.WriteLine($"[ADMIN SUCCESS] Lifted ban blocks from @{targetUser}.");
                        }

                        File.WriteAllText(targetFile, JsonSerializer.Serialize(jsonDict));
                    }
                }
                catch (Exception ex) { Console.WriteLine($"[ADMIN ERROR] Failed to update user save file: {ex.Message}"); }
            }
            else Console.WriteLine($"[ADMIN ERROR] Username '@{targetUser}' not found.");
        }
app.Run();
```


 
