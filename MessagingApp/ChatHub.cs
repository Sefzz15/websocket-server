using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

public class ChatHub : Hub
{
    // ConnectionId -> username. A user counts as connected while they have at least one
    // live connection, so refreshes and multiple tabs don't create or drop "ghost" users.
    private static readonly ConcurrentDictionary<string, string> connections = new();
    private static HashSet<string> groups = new HashSet<string>(); // store group names

    private static List<string> ConnectedUsers() => connections.Values.Distinct().ToList();

    public async Task UserConnected(string username)
    {
        bool isNewUser = !connections.Values.Contains(username);
        connections[Context.ConnectionId] = username;

        // Always sync the caller with the current state. This is what lets a refreshing
        // or reconnecting user see everyone who is already online.
        await Clients.Caller.SendAsync("ReceiveGroups", groups);
        await Clients.Caller.SendAsync("ReceiveConnectedUsers", ConnectedUsers());

        if (isNewUser)
        {
            await Clients.Others.SendAsync("UserConnected", username); // notify everyone else
        }

        await Clients.All.SendAsync("ReceiveConnectedUsers", ConnectedUsers());
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
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
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }

    public Task<List<string>> GetGroups()
    {
        return Task.FromResult(groups.ToList());
    }

    public async Task JoinGroup(string username, string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName)) return;

        groups.Add(groupName); // add to list if new
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Group(groupName).SendAsync("UserJoinedGroup", username, groupName);
        await Clients.All.SendAsync("ReceiveGroups", groups); // update groups for all
    }

    public async Task LeaveGroup(string username, string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName)) return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        await Clients.Group(groupName).SendAsync("UserLeftGroup", username, groupName);
    }

    public async Task SendMessageToGroup(string user, string message, string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName)) return;
        await Clients.Group(groupName).SendAsync("ReceiveMessage", user, message);
    }
}
