using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DSharpPlus.Entities;

namespace Bot_NetCore.Entities;

public class FindTeamClient : IDisposable
{
    private const int Game = 11137;
    private const string Region = "ru";
    private const string Language = "ru";
    private const string CreateEndpoint = "https://api.discord.band/v1/findteam/create";
    private const string CloseEndpoint = "https://api.discord.band/v1/findteam/close";
    
    private readonly HttpClient _client;
    private readonly string _token;

    public FindTeamClient(string token)
    {
        _client = new HttpClient();
        _token = token;
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
    
    public async Task CreateAsync(string inviteUrl, ulong userId, int time = 1800)
    {
        var request = new HttpRequestMessage
        {
            Content = PrepareCreateJsonContent(inviteUrl, userId, time),
            Method = HttpMethod.Post,
            RequestUri = new Uri(CreateEndpoint)
        };
        
        request.Headers.Add("Authorization", $"Bearer {_token}");
        
        var responseMessage = await _client.SendAsync(request);
        if (responseMessage.StatusCode != HttpStatusCode.OK)
            throw new Exception("Error while creating invite on FindTeam");
    }

    public async Task CloseAsync(ulong userId)
    {
        var request = new HttpRequestMessage
        {
            Content = PrepareCloseJsonContent(userId),
            Method = HttpMethod.Post,
            RequestUri = new Uri(CloseEndpoint)
        };
        
        
        request.Headers.Add("Authorization", $"Bearer {_token}");
        var responseMessage = await _client.SendAsync(request);
        if (responseMessage.StatusCode != HttpStatusCode.OK)
            throw new Exception("Error while closing invite on FindTeam");
    }

    private JsonContent PrepareCreateJsonContent(string inviteUrl, ulong userId, int time)
    {
        var obj = new FindTeamCreateRequest
        {
            Game = Game,
            Invite = inviteUrl,
            Language = Language,
            Region = Region,
            Time = time,
            UserId = userId.ToString()
        };

        return JsonContent.Create(obj);
    }

    private JsonContent PrepareCloseJsonContent(ulong userId)
    {
        var obj = new FindTeamCloseRequest
        {
            UserId = userId.ToString()
        };
        
        return JsonContent.Create(obj);
    }
}

public struct FindTeamCreateRequest
{
    [JsonPropertyName("game")]
    public int Game;
    
    [JsonPropertyName("region")]
    public string Region;
    
    [JsonPropertyName("lang")]
    public string Language;
    
    [JsonPropertyName("invite")]
    public string Invite;
    
    [JsonPropertyName("user_id")]
    public string UserId;
    
    [JsonPropertyName("time")]
    public int Time;
}

public struct FindTeamCloseRequest
{
    [JsonPropertyName("user_id")]
    public string UserId;
}