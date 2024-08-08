using System;
using System.Net;
using System.Threading.Tasks;

public class DropBoxAuthorizationServer
{
    private readonly HttpListener _listener;
    private readonly string _redirectUri;
    private TaskCompletionSource<string> _tcs;

    public DropBoxAuthorizationServer(string redirectUri)
    {
        _listener = new HttpListener();
        _redirectUri = redirectUri;
        _listener.Prefixes.Add(redirectUri);
    }

    public void Start()
    {
        _listener.Start();
        Task.Run(() => Listen());
    }

    public void Stop()
    {
        _listener.Stop();
    }

    private async Task Listen()
    {
        while (_listener.IsListening)
        {
            var context = await _listener.GetContextAsync();
            var response = context.Response;

            string responseString = "<html><body>You can close this tab and return to the application.</body></html>";
            var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            var output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();

            if (context.Request.QueryString["code"] != null)
            {
                string code = context.Request.QueryString["code"];
                _tcs.SetResult(code);
            }
        }
    }

    public Task<string> WaitForCodeAsync()
    {
        _tcs = new TaskCompletionSource<string>();
        return _tcs.Task;
    }
}
