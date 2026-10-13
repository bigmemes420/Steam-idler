using SteamKit2;
using SteamKit2.Internal;

namespace WinFormsApp1;

internal sealed class SteamIdle : IDisposable
{
    public const int MaxGames = 34;

    private readonly SteamClient _client = new();
    private readonly CallbackManager _callbacks;
    private readonly SteamUser _user;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _pump;

    private TaskCompletionSource<string?>? _logon;
    private string? _pendingAuthCode;
    private string? _pendingTwoFactorCode;
    private bool _connected;

    public SteamIdle()
    {
        _callbacks = new CallbackManager(_client);
        _user = _client.GetHandler<SteamUser>()!;
        _callbacks.Subscribe<SteamClient.ConnectedCallback>(_ => OnConnected());
        _callbacks.Subscribe<SteamClient.DisconnectedCallback>(_ => OnDisconnected());
        _callbacks.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        _pump = Task.Run(Pump);
    }

    public bool IsLoggedOn { get; private set; }

    public IReadOnlyList<uint> Playing { get; private set; } = [];

    public Task<string?> LogOnAsync(string username, string password, string? authCode, string? twoFactorCode, CancellationToken cancellationToken)
    {
        if (IsLoggedOn)
        {
            return Task.FromResult<string?>(null);
        }

        _username = username;
        _password = password;
        _pendingAuthCode = authCode;
        _pendingTwoFactorCode = twoFactorCode;
        _logon = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _client.Connect();
        return _logon.Task.WaitAsync(cancellationToken);
    }

    public void SetPlaying(IReadOnlyList<uint> appIds)
    {
        if (!IsLoggedOn)
        {
            throw new InvalidOperationException("Not logged on to Steam.");
        }

        var message = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);
        foreach (var appId in appIds.Take(MaxGames))
        {
            message.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed { game_id = appId });
        }

        _client.Send(message);
        Playing = appIds.Take(MaxGames).ToArray();
    }

    public void Dispose()
    {
        if (IsLoggedOn)
        {
            try
            {
                SetPlaying([]);
                _user.LogOff();
            }
            catch (Exception)
            {
                // The connection is going away either way.
            }
        }

        _shutdown.Cancel();
        _client.Disconnect();
        _pump.Wait(TimeSpan.FromSeconds(2));
        _shutdown.Dispose();
    }

    private void Pump()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            _callbacks.RunWaitCallbacks(TimeSpan.FromMilliseconds(250));
        }
    }

    private void OnConnected()
    {
        _connected = true;
        _user.LogOn(new SteamUser.LogOnDetails
        {
            Username = _username,
            Password = _password,
            AuthCode = _pendingAuthCode,
            TwoFactorCode = _pendingTwoFactorCode,
            ShouldRememberPassword = false,
            ClientOSType = EOSType.Windows10,
        });
    }

    private string? _username;
    private string? _password;

    private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
    {
        if (callback.Result == EResult.OK)
        {
            IsLoggedOn = true;
            _logon?.TrySetResult(null);
            return;
        }

        IsLoggedOn = false;
        _logon?.TrySetResult(callback.Result.ToString());
    }

    private void OnDisconnected()
    {
        var wasConnected = _connected;
        _connected = false;
        IsLoggedOn = false;
        if (!wasConnected)
        {
            _logon?.TrySetResult("Could not connect to Steam.");
        }
    }
}
