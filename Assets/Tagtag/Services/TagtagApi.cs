using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Tagtag.Services
{
    public sealed class ApiFailure : Exception
    {
        public readonly long Status;
        public ApiFailure(string message, long status = 0) : base(message) { Status = status; }
    }

    public sealed class TagtagApi
    {
        private readonly ServiceConfiguration configuration;
        public TagtagApi(ServiceConfiguration configuration) { this.configuration = configuration; }

        public async Task<T> Call<T>(string method, string path, object body, string token = null)
        {
            if (!configuration.Configured) throw new ApiFailure("Shared stickers are not configured yet.");
            return await Json<T>(method, configuration.apiBaseUrl.TrimEnd('/') + path, body, token);
        }

        public static async Task<T> Json<T>(string method, string url, object body, string token = null)
        {
            using (var request = new UnityWebRequest(url, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                if (body != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);
                await Send(request);
                try { return JsonUtility.FromJson<T>(request.downloadHandler.text); }
                catch { throw new ApiFailure("The service returned an unreadable response. Please retry."); }
            }
        }

        public static async Task<byte[]> Download(string url)
        {
            RequireHttps(url);
            using (var request = UnityWebRequest.Get(url))
            {
                await Send(request);
                if (request.downloadHandler.data.Length > 16 * 1024 * 1024) throw new ApiFailure("This sticker's AR map is too large.");
                return request.downloadHandler.data;
            }
        }

        public static async Task Upload(string url, byte[] bytes)
        {
            RequireHttps(url);
            using (var request = UnityWebRequest.Put(url, bytes))
            {
                request.SetRequestHeader("Content-Type", "application/octet-stream");
                request.SetRequestHeader("x-goog-content-length-range", "1,16777216");
                await Send(request);
            }
        }

        public static async Task<T> Form<T>(string url, WWWForm form)
        {
            using (var request = UnityWebRequest.Post(url, form))
            {
                await Send(request);
                return JsonUtility.FromJson<T>(request.downloadHandler.text);
            }
        }

        private static void RequireHttps(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https")
                throw new ApiFailure("The service returned an invalid secure address.");
        }

        private static async Task Send(UnityWebRequest request)
        {
            request.timeout = 45;
            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            if (request.result == UnityWebRequest.Result.Success) return;
            string message = "Connection failed. Your draft is safe; please try again.";
            try
            {
                var error = JsonUtility.FromJson<ErrorEnvelope>(request.downloadHandler.text);
                if (!string.IsNullOrEmpty(error?.error?.message)) message = error.error.message;
            }
            catch { /* Non-JSON transport errors use the safe local message. */ }
            throw new ApiFailure(message, request.responseCode);
        }

        [Serializable] private sealed class ErrorEnvelope { public ErrorBody error; }
        [Serializable] private sealed class ErrorBody { public string code, message; }
    }

    [Serializable] public sealed class LocationRequest { public LocationFix location; }
    [Serializable] public sealed class SummaryList { public StickerSummary[] items; }
    [Serializable] public sealed class CollectionList { public CollectedSticker[] items; }
    [Serializable] public sealed class StickerResult { public StickerSummary sticker; }
    [Serializable] public sealed class CollectionResult { public CollectedSticker sticker; }
    [Serializable] public sealed class OkResult { public bool ok; }
    [Serializable] public sealed class PrepareRequest
    {
        public string operationId, presetId, place, teaser, note;
        public LocationFix location;
        public Vector3 position;
        public Quaternion rotation;
        public float widthMeters;
        public int mapBytes;
    }
    [Serializable] public sealed class PrepareResult { public string id, uploadUrl; }
    [Serializable] public sealed class FinalizeRequest { public string operationId; public LocationFix location; }
    [Serializable] public sealed class RecoverResult
    {
        public StickerSummary sticker;
        public string discoveryId, mapUrl;
        public long expiresAt;
        public Vector3 position;
        public Quaternion rotation;
        public float widthMeters;
    }
    [Serializable] public sealed class CollectRequest { public string discoveryId; public LocationFix location; }
    [Serializable] public sealed class ReportRequest { public string reason; }
    [Serializable] public sealed class BlockRequest { public string authorId; }
}
