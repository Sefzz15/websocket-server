using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
public class ChatHub : Hub
{
    private static List<string> connectedUsers = new List<string>();

    // Όταν συνδέεται ένας χρήστης
    public async Task UserConnected(string username)
    {
        if (!connectedUsers.Contains(username))
        {
            connectedUsers.Add(username);
            await Clients.All.SendAsync("UserConnected", username); // Στέλνουμε στους clients τη νέα σύνδεση
            await Clients.All.SendAsync("ReceiveConnectedUsers", connectedUsers); // Ενημερώνουμε τη λίστα των συνδεδεμένων χρηστών
        }
    }

    // Όταν αποσυνδέεται ένας χρήστης
    public async Task UserDisconnected(string username)
    {
        if (connectedUsers.Contains(username))
        {
            connectedUsers.Remove(username);
            await Clients.All.SendAsync("UserDisconnected", username); // Ενημερώνουμε τους χρήστες για την αποσύνδεση
            await Clients.All.SendAsync("ReceiveConnectedUsers", connectedUsers); // Ενημερώνουμε τη λίστα των συνδεδεμένων χρηστών
        }
    }

    // Αποστολή μηνύματος
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}
