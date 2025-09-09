using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
public class ChatHub : Hub
{
    private static List<string> connectedUsers = new List<string>();

    public async Task UserConnected(string username)
    {
        if (!connectedUsers.Contains(username))
        {
            connectedUsers.Add(username);
            await Clients.All.SendAsync("UserConnected", username); // Send notification to all users
            await Clients.All.SendAsync("ReceiveConnectedUsers", connectedUsers); // Update the list of connected users
        }
    }

    public async Task UserDisconnected(string username)
    {
        if (connectedUsers.Contains(username))
        {
            connectedUsers.Remove(username);
            await Clients.All.SendAsync("UserDisconnected", username); // Send notification to all users
            await Clients.All.SendAsync("ReceiveConnectedUsers", connectedUsers); // Update the list of connected users
        }
    }

    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }

    public async Task JoinGroup(string username, string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName)) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Group(groupName).SendAsync("UserJoinedGroup", username, groupName);
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
