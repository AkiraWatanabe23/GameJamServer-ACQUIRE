using Network;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class ServerRunner : MonoBehaviour
{
    [Tooltip("最大同時接続数")]
    [Range(1, 20)]
    [SerializeField]
    private int _maxConnectCount = 5;
    [SerializeField]
    private ServerView _serverView = new();
    [SerializeField]
    private ServerModel _serverModel = new();
    [SerializeField]
    private int _port = 7000;

    [Header("For Editor Test")]
    [Tooltip("サーバーの自動閉鎖時間（秒、テスト時）")]
    [SerializeField]
    private float _serverCloseTime = 10f;

    private Stream _responseOutput = default;
    private HttpListener _listener = default;
    private IPAddress _selfIPAddress = IPAddress.Any;

    protected IPAddress SelfIPAddress
    {
        get => _selfIPAddress;
        private set
        {
            _selfIPAddress = value;
            _serverView.GetServerIPAddress(_selfIPAddress.ToString());
        }
    }

    private void Start()
    {
        MainThreadDispatcher.SetMainThreadContext();
        _serverModel.Initialize(_serverCloseTime, _maxConnectCount);

        Initialize();
        ShowAddress();
    }

    private void Initialize()
    {
        SelfIPAddress = _serverModel.GetSelfIPAddress();
        string redirectURL = $"http://*:{_port}/";
        _serverView.UpdateResponseString(redirectURL);

        _listener = new();
        try
        {
            _listener.Prefixes.Add(redirectURL);
            _listener.Start();
            Debug.Log("Server started");
        }
        catch (Exception exception)
        {
            Debug.LogError(exception.Message);
            return;
        }
        //同時アクセスに対応できるように複数スレッドで実行する
        for (int i = 0; i < _maxConnectCount; i++) { AccessWaiting(); }
    }

    private void AccessWaiting()
    {
        Task.Run(async () =>
        {
            try
            {
                if (!_listener.IsListening) { _listener.Start(); }

                var context = await _listener.GetContextAsync();
                var response = context.Response;

                // 受け取ったリダイレクトURLをログに出力する
                Debug.Log($"redirectURI: {context.Request.Url}");

                // 受け取ったリダイレクトURLのクエリパラメータからcodeを取得する
                var query = context.Request.Url.Query;
                var code = HttpUtility.ParseQueryString(query).Get("code");

                string responseString = "";
                //クライアントからのリクエストを判定
                if (context.Request.HttpMethod == "POST")
                {
                    //送信されてきたデータ配列
                    var reader = new StreamReader(context.Request.InputStream).ReadToEnd().Split(',');
                    var requestData = reader[0].Split('&');
                    var id = requestData[0].Split('=')[1];
                    var requestMessage = requestData[1].Split('=')[1];

                    //受けたリクエストに対して処理を実行する
                    responseString = await _serverModel.ReceivePostRequest(id, requestMessage);
                }
                else if (context.Request.HttpMethod == "PUT")
                {
                    responseString = await _serverModel.ReceivePutRequest(new StreamReader(context.Request.InputStream).ReadToEnd());
                }
                else if (context.Request.HttpMethod == "GET")
                {
                    responseString = _serverModel.ReceiveGetRequest();
                }

                var buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                _responseOutput = response.OutputStream;
                await _responseOutput.WriteAsync(buffer, 0, buffer.Length);

                //UIの反映
                _ = await MainThreadDispatcher.RunAsync(async () =>
                {
                    await Task.Yield();

                    _serverView.UpdateResponseString(responseString);
                    _serverView.UpdateConnectCount(_serverModel.GetConnectCount());
                    _serverView.UpdateDB(_serverModel.GetLatestDataTable(0));
                    return "UI Thread Finish";
                });

                //複数端末からの処理を待機するために再起実行
                AccessWaiting();
            }
            catch (Exception exception) { Debug.LogError(exception.Message); }
        });
    }

    /// <summary> 初期アクセスしてくるクライアントにIPAddressを公開する </summary>
    private async void ShowAddress()
    {
        var udpClient = new UdpClient();
        udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _port));

        OnRequestReceived(await udpClient.ReceiveAsync(), udpClient);
    }

    private async void OnRequestReceived(UdpReceiveResult result, UdpClient client)
    {
        var remoteEP = result.RemoteEndPoint;
        var rcvData = result.Buffer;

        string clientMessage = Encoding.UTF8.GetString(rcvData);
        Debug.Log("Message from client: " + clientMessage);
        // クライアントにレスポンスを送信
        string responseString = _selfIPAddress.ToString();
        byte[] responseBytes = Encoding.UTF8.GetBytes(responseString);
        await client.SendAsync(responseBytes, responseBytes.Length, remoteEP);

        var message = Encoding.UTF8.GetString(rcvData);
        Debug.Log($"{remoteEP} {message}");

        var data = await client.ReceiveAsync();
        OnRequestReceived(data, client);
    }

    private void OnDestroy()
    {
        _responseOutput?.Close();
        _listener?.Stop();
    }
}
