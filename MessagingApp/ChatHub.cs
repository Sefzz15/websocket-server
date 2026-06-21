using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;

public class ChatHub : Hub
{
    // ConnectionId -> username. A user counts as connected while they have at least one
    // live connection, so refreshes and multiple tabs don't create or drop "ghost" users.
    private static readonly ConcurrentDictionary<string, string> connections = new();
    // Set of group names (ConcurrentDictionary used as a thread-safe set).
    private static readonly ConcurrentDictionary<string, byte> groups = new();
    // groupName -> set of member ConnectionIds, so a group can be torn down on delete.
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> groupMembers = new();
    // Channel (group name, or "" for the general chat) -> recent messages.
    private static readonly ConcurrentDictionary<string, List<ChatMessage>> history = new();
    private const int MaxHistoryPerChannel = 200;

    private static List<string> ConnectedUsers() => connections.Values.Distinct().ToList();

    private static void StoreMessage(string channel, string user, string text)
    {
        var list = history.GetOrAdd(channel, _ => new List<ChatMessage>());
        lock (list)
        {
            list.Add(new ChatMessage { User = user, Text = text });
            if (list.Count > MaxHistoryPerChannel)
            {
                list.RemoveAt(0); // keep only the most recent messages
            }
        }
    }

    private static List<ChatMessage> GetHistory(string channel)
    {
        if (history.TryGetValue(channel, out var list))
        {
            lock (list)
            {
                return list.ToList();
            }
        }
        return new List<ChatMessage>();
    }

    public async Task UserConnected(string username)
    {
        bool isNewUser = !connections.Values.Contains(username);
        connections[Context.ConnectionId] = username;

        // Always sync the caller with the current state. This is what lets a refreshing
        // or reconnecting user see everyone who is already online.
        await Clients.Caller.SendAsync("ReceiveGroups", groups.Keys);
        await Clients.Caller.SendAsync("ReceiveConnectedUsers", ConnectedUsers());

        if (isNewUser)
        {
            await Clients.Others.SendAsync("UserConnected", username); // notify everyone else
        }

        await Clients.All.SendAsync("ReceiveConnectedUsers", ConnectedUsers());
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Drop this connection from any groups it had joined.
        foreach (var members in groupMembers.Values)
        {
            members.TryRemove(Context.ConnectionId, out _);
        }

        // Runs whenever a socket drops (refresh, tab close, crash) — no longer relies on
        // the client to announce its own disconnect.
        if (connections.TryRemove(Context.ConnectionId, out var username))
        {
            // Only announce departure once the user's last connection is gone.
            if (!connections.Values.Contains(username))
            {
                await Clients.All.SendAsync("UserDisconnected", username);
            }
            await Clients.All.SendAsync("ReceiveConnectedUsers", ConnectedUsers());
        }

        await base.OnDisconnectedAsync(exception);
    }

    // Disconnection is handled automatically by OnDisconnectedAsync now. Kept as a no-op
    // so the client's existing ngOnDestroy invoke doesn't error; the subsequent connection
    // close is what actually removes the user.
    public Task UserDisconnected(string username) => Task.CompletedTask;

    public async Task SendMessage(string user, string message)
    {
        StoreMessage("", user, message);
        await Clients.All.SendAsync("ReceiveMessage", user, message, ""); // "" = general channel
    }

    public Task<List<string>> GetGroups()
    {
        return Task.FromResult(groups.Keys.ToList());
    }

    // Returns the recent messages for a channel ("" = the general chat) so a client can
    // show existing history when it enters that channel.
    public Task<List<ChatMessage>> GetMessages(string groupName)
    {
        return Task.FromResult(GetHistory(groupName ?? ""));
    }

    public async Task JoinGroup(string username, string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName)) return;

        groups.TryAdd(groupName, 0); // add to list if new
        groupMembers.GetOrAdd(groupName, _ => new ConcurrentDictionary<string, byte>())[Context.ConnectionId] = 0;
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Group(groupName).SendAsync("UserJoinedGroup", username, groupName);
        await Clients.All.SendAsync("ReceiveGroups", groups.Keys); // update groups for all
    }

    public async Task LeaveGroup(string username, string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName)) return;

        if (groupMembers.TryGetValue(groupName, out var members))
        {
            members.TryRemove(Context.ConnectionId, out _);
        }
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        await Clients.Group(groupName).SendAsync("UserLeftGroup", username, groupName);
    }

    public async Task DeleteGroup(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName)) return;

        groups.TryRemove(groupName, out _);
        history.TryRemove(groupName, out _); // discard the deleted group's messages

        // Notify current members while they're still in the SignalR group, then kick them out.
        await Clients.Group(groupName).SendAsync("GroupDeleted", groupName);

        if (groupMembers.TryRemove(groupName, out var members))
        {
            foreach (var connectionId in members.Keys)
            {
                await Groups.RemoveFromGroupAsync(connectionId, groupName);
            }
        }

        await Clients.All.SendAsync("ReceiveGroups", groups.Keys); // refresh everyone's list
    }

    public async Task SendMessageToGroup(string user, string message, string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName)) return;
        StoreMessage(groupName, user, message);
        await Clients.Group(groupName).SendAsync("ReceiveMessage", user, message, groupName);
    }
}

public class ChatMessage
{
    [JsonPropertyName("user")]
    public string User { get; set; } = "";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}
