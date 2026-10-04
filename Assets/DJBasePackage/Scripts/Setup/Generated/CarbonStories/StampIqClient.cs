using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace CarbonStories
{
    [Serializable]
    public sealed class StampIqReply<T>
    {
        public bool success;
        public string message;
        public string error_code;
        public T data;
        public StampIqDetails details;
    }

    [Serializable] public sealed class StampIqDetails { public string status; public string next_action; }
    [Serializable] public sealed class StampIqGameConfig { public bool pre_questionnaire_enabled; public bool post_questionnaire_enabled; }
    [Serializable] public sealed class StampIqValidation { public string scan_token; }
    [Serializable] public sealed class StampIqSession
    {
        public string session_id;
        public string status;
        public string next_action;
    }

    public sealed class StampIqClient
    {
        private const string BaseUrl = "https://api.stampiq.sa/api/v1/games";
        private readonly string gameCode;
        private string apiKey;

        public StampIqClient(string gameCode) { this.gameCode = gameCode; }

        public IEnumerator LoadKey(Action<string> onError)
        {
            string path = Application.streamingAssetsPath.TrimEnd('/', '\\') + "/KEY.txt";
            if (!path.Contains("://") && !path.StartsWith("jar:"))
                path = new Uri(path).AbsoluteUri;
            using (UnityWebRequest request = UnityWebRequest.Get(path))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError("StampIQ key file is missing or unreadable.");
                    yield break;
                }
                apiKey = request.downloadHandler.text.Trim('\uFEFF', ' ', '\r', '\n', '\t');
                if (string.IsNullOrEmpty(apiKey)) onError("StampIQ key file is empty.");
            }
        }

        public IEnumerator Config(Action<StampIqReply<StampIqGameConfig>> done) =>
            Send("GET", "/" + UnityWebRequest.EscapeURL(gameCode) + "/config", null, done);

        public IEnumerator Validate(string code, Action<StampIqReply<StampIqValidation>> done) =>
            Send("POST", "/qr/validate", new { ticket_number = code, game_code = gameCode }, done);

        public IEnumerator CreateSession(string scanToken, Action<StampIqReply<StampIqSession>> done) =>
            Send("POST", "/sessions", new { game_code = gameCode, scan_token = scanToken }, done);

        public IEnumerator GetSession(string id, Action<StampIqReply<StampIqSession>> done) =>
            Send("GET", SessionPath(id), null, done);

        public IEnumerator Start(string id, Action<StampIqReply<StampIqSession>> done) =>
            Send("POST", SessionPath(id) + "/start", null, done);

        public IEnumerator SubmitResult(string id, object result, Action<StampIqReply<StampIqSession>> done) =>
            Send("POST", SessionPath(id) + "/result", result, done);

        public IEnumerator Complete(string id, Action<StampIqReply<StampIqSession>> done) =>
            Send("POST", SessionPath(id) + "/complete", null, done);

        public IEnumerator Abandon(string id, Action<StampIqReply<StampIqSession>> done) =>
            Send("POST", SessionPath(id) + "/abandon", null, done);

        private static string SessionPath(string id) => "/sessions/" + UnityWebRequest.EscapeURL(id);

        private IEnumerator Send<T>(string method, string route, object body, Action<StampIqReply<T>> done)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                done(new StampIqReply<T> { error_code = "UNAUTHORIZED", message = "StampIQ key is unavailable." });
                yield break;
            }
            byte[] bytes = body == null ? null : Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body));
            for (int attempt = 0; attempt < 3; attempt++)
            {
                using (var request = new UnityWebRequest(BaseUrl + route, method))
                {
                    if (bytes != null) request.uploadHandler = new UploadHandlerRaw(bytes);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Authorization", "Bearer " + apiKey);
                    request.SetRequestHeader("Accept", "application/json");
                    if (bytes != null) request.SetRequestHeader("Content-Type", "application/json");
                    request.timeout = 15;
                    yield return request.SendWebRequest();

                    StampIqReply<T> reply = null;
                    try { reply = JsonConvert.DeserializeObject<StampIqReply<T>>(request.downloadHandler.text); }
                    catch (JsonException) { /* Non-JSON network or proxy response. */ }
                    if (request.result == UnityWebRequest.Result.Success && reply != null && reply.success)
                    {
                        done(reply);
                        yield break;
                    }
                    bool transient = request.responseCode == 429 || request.responseCode >= 500 ||
                                     request.result == UnityWebRequest.Result.ConnectionError;
                    if (transient && attempt < 2)
                    {
                        yield return new WaitForSecondsRealtime(attempt + 1);
                        continue;
                    }
                    done(reply ?? new StampIqReply<T>
                    {
                        error_code = transient ? "NETWORK_ERROR" : "API_ERROR",
                        message = transient ? "Connection to StampIQ failed. Please try again." : "StampIQ returned an unreadable response."
                    });
                    yield break;
                }
            }
        }
    }
}
