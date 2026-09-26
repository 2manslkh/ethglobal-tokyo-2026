using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Tagtag.Services
{
    public sealed class ApiFailure : Exception
    {
        public readonly long Status;
        public readonly string Code;
        public readonly bool LocationSettingsRequired;
        public ApiFailure(string message, long status = 0, bool locationSettingsRequired = false, string code = null) : base(message)
        { Status = status; LocationSettingsRequired = locationSettingsRequired; Code = code; }
    }

    public sealed class TagtagApi
    {
        private readonly ServiceConfiguration configuration;
        public TagtagApi(ServiceConfiguration configuration) { this.configuration = configuration; }

        public async Task<T> Call<T>(string method, string path, object body, string token = null)
        {
            if (!configuration.Configured) throw new ApiFailure("Shared stickers are not configured yet.");
            if (!IsSecureUrl(configuration.apiBaseUrl) ||
                Uri.TryCreate(configuration.apiBaseUrl, UriKind.Absolute, out Uri baseUri) && !string.IsNullOrEmpty(baseUri.Query))
                throw new ApiFailure("The sticker service needs a secure HTTPS address.");
            return await Json<T>(method, configuration.apiBaseUrl.TrimEnd('/') + path, body, token);
        }

        public static async Task<T> Json<T>(string method, string url, object body, string token = null)
        {
            RequireHttps(url);
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
                try
                {
                    if (typeof(T) == typeof(PrepareResult))
                        return (T)(object)ParsePrepareResponse(request.downloadHandler.text);
                    return JsonUtility.FromJson<T>(request.downloadHandler.text);
                }
                catch (ApiFailure) { throw; }
                catch { throw new ApiFailure("The service returned an unreadable response. Please retry."); }
            }
        }

        public static PrepareResult ParsePrepareResponse(string json)
        {
            if (string.IsNullOrEmpty(json) || json.Length > 65536)
                throw new ApiFailure("The service returned invalid upload instructions. Please retry.");
            try
            {
                var response = JsonUtility.FromJson<PrepareResult>(json);
                if (response == null || string.IsNullOrEmpty(response.id))
                    throw new ApiFailure("The service returned invalid upload instructions. Please retry.");
                response.uploadHeaders = new UploadHeaderReader(json).Read();
                if (!string.IsNullOrEmpty(response.uploadUrl) && response.uploadHeaders.Length == 0)
                    throw new ApiFailure("The service returned invalid upload instructions. Please retry.");
                return response;
            }
            catch (ApiFailure) { throw; }
            catch { throw new ApiFailure("The service returned invalid upload instructions. Please retry."); }
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

        public static async Task Upload(string url, byte[] bytes, IReadOnlyList<SignedUploadHeader> headers)
        {
            RequireHttps(url);
            if (bytes == null || bytes.Length < 1 || bytes.Length > 16 * 1024 * 1024)
                throw new ApiFailure("This sticker's AR map is too large or empty.");
            if (headers == null || headers.Count == 0 || headers.Count > 16)
                throw new ApiFailure("The service returned invalid upload instructions. Please retry.");
            using (var request = UnityWebRequest.Put(url, bytes))
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (SignedUploadHeader header in headers)
                {
                    UploadHeaderReader.Validate(header?.name, header?.value);
                    if (!seen.Add(header.name)) throw new ApiFailure("The service returned duplicate upload headers.");
                    request.SetRequestHeader(header.name, header.value);
                }
                await Send(request);
            }
        }

        public static async Task<T> Form<T>(string url, WWWForm form)
        {
            RequireHttps(url);
            using (var request = UnityWebRequest.Post(url, form))
            {
                await Send(request);
                return JsonUtility.FromJson<T>(request.downloadHandler.text);
            }
        }

        public static bool IsSecureUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri uri) && uri.Scheme == Uri.UriSchemeHttps &&
                string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment);
        }

        private static void RequireHttps(string url)
        {
            if (!IsSecureUrl(url)) throw new ApiFailure("The service returned an invalid secure address.");
        }

        private static async Task Send(UnityWebRequest request)
        {
            request.timeout = 45;
            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            if (request.result == UnityWebRequest.Result.Success) return;
            string message = "Connection failed. Your draft is safe; please try again.";
            string code = null;
            try
            {
                var error = JsonUtility.FromJson<ErrorEnvelope>(request.downloadHandler.text);
                if (!string.IsNullOrEmpty(error?.error?.message)) message = error.error.message;
                code = error?.error?.code;
            }
            catch { /* Non-JSON transport errors use the safe local message. */ }
            throw new ApiFailure(message, request.responseCode, code: code);
        }

        [Serializable] private sealed class ErrorEnvelope { public ErrorBody error; }
        [Serializable] private sealed class ErrorBody { public string code, message; }

        private sealed class UploadHeaderReader
        {
            private readonly string json;
            private int offset;

            public UploadHeaderReader(string json) { this.json = json; }

            public SignedUploadHeader[] Read()
            {
                Expect('{');
                var headers = Array.Empty<SignedUploadHeader>();
                bool found = false;
                if (!Take('}'))
                {
                    do
                    {
                        string name = ReadString();
                        Expect(':');
                        if (name == "uploadHeaders")
                        {
                            if (found) Invalid();
                            found = true;
                            headers = ReadHeaders();
                        }
                        else SkipValue(0);
                    } while (Take(','));
                    Expect('}');
                }
                SkipSpace();
                if (!found || offset != json.Length) Invalid();
                return headers;
            }

            private SignedUploadHeader[] ReadHeaders()
            {
                Expect('{');
                var headers = new List<SignedUploadHeader>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!Take('}'))
                {
                    do
                    {
                        if (headers.Count >= 16) Invalid();
                        string name = ReadString();
                        Expect(':');
                        string value = ReadString();
                        Validate(name, value);
                        if (!seen.Add(name)) Invalid();
                        headers.Add(new SignedUploadHeader { name = name, value = value });
                    } while (Take(','));
                    Expect('}');
                }
                return headers.ToArray();
            }

            public static void Validate(string name, string value)
            {
                if (string.IsNullOrEmpty(name) || name.Length > 64 || string.IsNullOrEmpty(value) || value.Length > 1024 ||
                    !(name.Equals("content-type", StringComparison.OrdinalIgnoreCase) || name.StartsWith("x-goog-", StringComparison.OrdinalIgnoreCase))) Invalid();
                foreach (char character in name)
                    if (!((character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z') ||
                        (character >= '0' && character <= '9') || character == '-')) Invalid();
                foreach (char character in value)
                    if (character < 32 || character == 127) Invalid();
            }

            private void SkipValue(int depth)
            {
                if (depth > 8) Invalid();
                SkipSpace();
                if (Take('"')) { offset--; ReadString(); return; }
                if (Take('{'))
                {
                    if (!Take('}'))
                    {
                        do { ReadString(); Expect(':'); SkipValue(depth + 1); } while (Take(','));
                        Expect('}');
                    }
                    return;
                }
                if (Take('['))
                {
                    if (!Take(']'))
                    {
                        do { SkipValue(depth + 1); } while (Take(','));
                        Expect(']');
                    }
                    return;
                }
                int start = offset;
                while (offset < json.Length && json[offset] != ',' && json[offset] != '}' && json[offset] != ']' &&
                    json[offset] != ' ' && json[offset] != '\t' && json[offset] != '\n' && json[offset] != '\r') offset++;
                if (start == offset) Invalid();
            }

            private string ReadString()
            {
                Expect('"');
                var value = new StringBuilder();
                while (offset < json.Length)
                {
                    char character = json[offset++];
                    if (character == '"') return value.ToString();
                    if (character < 32) Invalid();
                    if (character == '\\')
                    {
                        if (offset >= json.Length) Invalid();
                        character = json[offset++];
                        if (character == 'u')
                        {
                            if (offset + 4 > json.Length) Invalid();
                            int code = 0;
                            for (int digit = 0; digit < 4; digit++)
                            {
                                int hex = Hex(json[offset++]);
                                if (hex < 0) Invalid();
                                code = code * 16 + hex;
                            }
                            character = (char)code;
                        }
                        else if (character == 'n') character = '\n';
                        else if (character == 'r') character = '\r';
                        else if (character == 't') character = '\t';
                        else if (character == 'b') character = '\b';
                        else if (character == 'f') character = '\f';
                        else if (character != '"' && character != '\\' && character != '/') Invalid();
                    }
                    value.Append(character);
                    if (value.Length > 65536) Invalid();
                }
                Invalid();
                return null;
            }

            private static int Hex(char character)
            {
                if (character >= '0' && character <= '9') return character - '0';
                if (character >= 'a' && character <= 'f') return character - 'a' + 10;
                if (character >= 'A' && character <= 'F') return character - 'A' + 10;
                return -1;
            }

            private bool Take(char character)
            {
                SkipSpace();
                if (offset < json.Length && json[offset] == character) { offset++; return true; }
                return false;
            }

            private void Expect(char character)
            {
                if (!Take(character)) Invalid();
            }

            private void SkipSpace()
            {
                while (offset < json.Length && (json[offset] == ' ' || json[offset] == '\t' || json[offset] == '\n' || json[offset] == '\r')) offset++;
            }

            private static void Invalid() { throw new ApiFailure("The service returned invalid upload instructions. Please retry."); }
        }
    }

    [Serializable] public sealed class LocationRequest { public LocationFix location; }
    [Serializable] public sealed class SummaryList { public StickerSummary[] items; }
    [Serializable] public sealed class CollectionList { public CollectedSticker[] items; }
    [Serializable] public sealed class StickerResult { public StickerSummary sticker; }
    [Serializable] public sealed class CollectionResult { public CollectedSticker sticker; }
    [Serializable] public sealed class OkResult { public bool ok; }
    [Serializable] public sealed class PrepareRequest
    {
        public ConfirmedLocation confirmedLocation;
        public bool locationConfirmed, hasPublicationLocation;
        public string operationId, presetId, designId, place, teaser, note;
        public LocationFix location;
        public Vector3 position;
        public Quaternion rotation;
        public float widthMeters;
        public int mapBytes;
    }
    [Serializable] public sealed class PublicationLocationResult
    {
        public bool found, locationConfirmed;
        public ConfirmedLocation publicationLocation;
    }
    [Serializable] public sealed class PrepareResult
    {
        public string id, uploadUrl;
        public ConfirmedLocation publicationLocation;
        [NonSerialized] public SignedUploadHeader[] uploadHeaders;
    }
    [Serializable] public sealed class SignedUploadHeader { public string name, value; }
    [Serializable] public sealed class FinalizeRequest { public string operationId; public LocationFix location; public ConfirmedLocation confirmedLocation; public bool locationConfirmed, hasPublicationLocation; }
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
