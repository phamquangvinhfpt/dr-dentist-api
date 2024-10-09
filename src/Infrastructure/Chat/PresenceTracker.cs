namespace FSH.WebApi.Infrastructure.Chat;

public class PresenceTracker
{
    private static readonly Dictionary<string, List<string>> OnlineUsers =
        new Dictionary<string, List<string>>();

    public Task<(bool IsOnline, string[] OnlineUsers)> UserConnected(string username, string connectionId)
    {
        bool isOnline = false;
        lock (OnlineUsers)
        {
            if (OnlineUsers.ContainsKey(username))
            {
                OnlineUsers[username].Add(connectionId);
            }
            else
            {
                OnlineUsers.Add(username, new List<string> { connectionId });
                isOnline = true;
            }
        }
        return Task.FromResult((isOnline, GetOnlineUsersInternal()));
    }

    public Task<(bool IsOffline, string[] OnlineUsers)> UserDisconnected(string username, string connectionId)
    {
        bool isOffline = false;
        lock (OnlineUsers)
        {
            if (!OnlineUsers.ContainsKey(username)) return Task.FromResult((isOffline, GetOnlineUsersInternal()));
            OnlineUsers[username].Remove(connectionId);
            if (OnlineUsers[username].Count == 0)
            {
                OnlineUsers.Remove(username);
                isOffline = true;
            }
        }
        return Task.FromResult((isOffline, GetOnlineUsersInternal()));
    }

    public Task<string[]> GetOnlineUsers()
    {
        return Task.FromResult(GetOnlineUsersInternal());
    }

    private string[] GetOnlineUsersInternal()
    {
        string[] onlineUsers;
        lock (OnlineUsers)
        {
            onlineUsers = OnlineUsers.OrderBy(k => k.Key).Select(k => k.Key).ToArray();
        }
        return onlineUsers;
    }

    public Task<List<string>> GetConnectionsForUser(string username)
    {
        List<string> connectionIds;
        lock (OnlineUsers)
        {
            connectionIds = OnlineUsers.GetValueOrDefault(username);
        }
        return Task.FromResult(connectionIds);
    }
}