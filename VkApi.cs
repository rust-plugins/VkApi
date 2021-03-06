using System;
using Oxide.Core.Plugins;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("VK API", "NickRimmer", "1.0.0")]
    [Description("Provided methods to send VK messages")]
    public class VkApi : CovalencePlugin
    {
        private VkComponent _vk;
        private PluginConfig _config;

        private void Init()
        {
            LogToFile("Initialization...");

            _config = Config.ReadObject<PluginConfig>();
            if (string.IsNullOrEmpty(_config.Token))
            {
                var message = "Community token required";
                PrintWarning(message);
                LogToFile(message);
                return;
            }

            _vk = VkComponent.Create(_config.Token, _config.MuteMessageDisallowed);
        }

        [HookMethod("SendText")]
        public void SendText(string vkUserId, string message)
        {
            LogToFile($"Sending message to {vkUserId}: {message}");
            _vk.SendTextMessage(vkUserId, message);
        }

        private void Unload()
        {
            LogToFile("Unloaded");
            GameObject.Destroy(_vk);
        }

        private void OnVkConnected()
        {
            var message = "VK Api connected!";
            LogToFile(message);
            Puts(message);
        }

        protected override void LoadDefaultConfig() 
            => Config.WriteObject(new PluginConfig(), true);

        private void LogToFile(string message)
        {
            if (_config?.LogToFile != true) return;
            
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            message = $"{timestamp} | {message}";
            LogToFile("common", message, this);
        }

        private void OnVkMessageSent(string vkUserId, string message)
            => LogToFile($"Message to user {vkUserId} sent: {message}");

        private void OnVkError(byte code, string description, int vkApiCode, string vkUserId)
            => LogToFile($"Error: {code}; Description: {description}; VK API code: {vkApiCode}; VK User id: {vkUserId}");
    
        #region Shared.Components

        private class VkComponent : MonoBehaviour
        {
            private const float PostDelay = .5f;
            private const string VkApiVersion = "5.37";
            private const string VkApiUrl = "https://api.vk.com/method";
    
            private readonly Queue<BaseRequest> _queue = new Queue<BaseRequest>();
            private string _communityToken;
            private bool _busy = false;
            private static bool _muteMessagesNotAllowed;
    
            private enum Errors : byte
            {
                Network = 1,
                TokenEmpty = 2,
                Api = 3,
                Disallowed = 4
            }
    
            public static VkComponent Create(string communityToken, bool muteMessagesNotAllowed)
            {
                _muteMessagesNotAllowed = muteMessagesNotAllowed;
                return new GameObject()
                    .AddComponent<VkComponent>()
                    .Configure(communityToken)
                    .ConnectionTest();
            }
    
            public VkComponent SendTextMessage(string vkUserId, string message, Action<string, string> callback = null)
            {
                var request = new PersonalMessageRequest(vkUserId, message);
                request.OnResponse(_ =>
                {
                    callback?.Invoke(vkUserId, message);
                    Interface.CallHook("OnVkMessageSent", vkUserId, message);
    
                });
                return AddQueue(request);
            }
    
            #region Boring things
    
            private VkComponent Configure(string communityToken)
            {
                if (communityToken == null) throw new ArgumentNullException(nameof(communityToken));
                _communityToken = communityToken;
    
                return this;
            }
    
            private VkComponent ConnectionTest() =>
                AddQueue(new SearchMessageRequest("*", 1).OnResponse(_ => Interface.CallHook("OnVkConnected")));
    
            private static string UrlEncode(string input)
            {
                var replaces = new Dictionary<string, string>
                {
                    {"#", "%23"},
                    {"$", "%24"},
                    {"&", "%26"},
                    {"+", "%2B"},
                    {",", "%2C"},
                    {"/", "%2F"},
                    {":", "%3A"},
                    {";", "%3B"},
                    {"=", "%3D"},
                    {"?", "%3F"},
                    {"@", "%40"},
                    {" ", "%20"}
                };
    
                return replaces.Aggregate(
                    input,
                    (result, rule) => result.Replace(rule.Key, rule.Value));
            }
    
            #endregion
    
            #region VK magic
    
            private VkComponent AddQueue(BaseRequest request)
            {
                _queue.Enqueue(request);
                if (!_busy) StartCoroutine(ProcessQueue());
                return this;
            }
    
            private IEnumerator ProcessQueue()
            {
                if (_busy) yield break;
                _busy = true;
    
                while (_queue.Any())
                {
                    var request = _queue.Dequeue();
                    yield return ProcessRequest(request);
                }
    
                _busy = false;
            }
    
            private IEnumerator ProcessRequest(BaseRequest request)
            {
                if (string.IsNullOrEmpty(_communityToken))
                {
                    Debug.LogError($"[{GetType().Name}] token wasn't specified");
                    Interface.CallHook("OnVkError", (byte)Errors.TokenEmpty, "Token wasn't specified");
                    yield break;
                }
    
                var url = $"{VkApiUrl}/{request}&v={VkApiVersion}&access_token={_communityToken}";
                var www = UnityWebRequest.Get(url);
    
                yield return www.SendWebRequest();
    
                var respText = www.downloadHandler?.text;
    
                if (www.isNetworkError || www.isHttpError)
                {
                    Debug.LogError($"[{GetType().Name}] {www.error} | {respText}");
                    Interface.CallHook("OnVkError", (byte)Errors.Network, "Network or HTTP error");
                }
                else if (string.IsNullOrEmpty(respText)) request.InvokeOnResponse(null);
                else if(IsResponseVkError(respText)) yield break;
                else request.InvokeOnResponse(respText);
    
                www.Dispose();
    
                // to avoid spam requests
                yield return new WaitForSeconds(PostDelay);
            }
    
            private bool IsResponseVkError(string json)
            {
                var respJson = JObject.Parse(json).First as JProperty;
                if (respJson?.Name.Equals("error", StringComparison.InvariantCultureIgnoreCase) != true)
                    return false;
    
                var eCode = respJson.Value["error_code"]?.Value<int>();
                var eMessage = respJson.Value["error_msg"]?.Value<string>();
                var eUserId = (respJson
                        .Value["request_params"]
                        ?.FirstOrDefault(x => x["key"]?.Value<string>() == "user_id")
                        ?["value"] as JValue)
                    ?.Value;
    
                var message = $"[{GetType().Name}] vkUserId: {eUserId ?? "-"}; Error '{eCode}': {eMessage}";
                if (eCode == 901) // user doesn't allow messages from community
                {
                    if (!_muteMessagesNotAllowed) Debug.LogWarning(message);
                    Interface.CallHook("OnVkError", (byte)Errors.Disallowed, eMessage, eCode, eUserId);
                }
                else
                {
                    Debug.LogError(message);
                    Interface.CallHook("OnVkError", (byte)Errors.Api, eMessage, eCode);
                }
    
                return true;
            }
    
            private void OnDestroy()
            {
                _queue.Clear();
            }
    
            private abstract class BaseRequest
            {
                private Action<string> _action = null;
    
                public void InvokeOnResponse(string json) => _action?.Invoke(json);
                public BaseRequest OnResponse(Action<string> action)
                {
                    _action = action;
                    return this;
                }
            }
    
            #endregion
    
            #region Requests
    
            private class PersonalMessageRequest : BaseRequest
            {
                private readonly string _message;
                private readonly string _vkUserId;
    
                public PersonalMessageRequest(string vkUserId, string message)
                {
                    if (string.IsNullOrEmpty(message)) throw new ArgumentNullException(nameof(message));
                    if (string.IsNullOrEmpty(vkUserId)) throw new ArgumentNullException(nameof(vkUserId));
    
                    _message = UrlEncode(message);
                    _vkUserId = vkUserId;
                }
    
                public override string ToString()
                    => $"messages.send?user_id={_vkUserId}&message={_message}";
            }
    
            private class SearchMessageRequest : BaseRequest
            {
                private readonly string _s;
                private readonly int _limit;
    
                public SearchMessageRequest(string s, int limit)
                {
                    if (s == null) throw new ArgumentNullException(nameof(s));
    
                    _s = UrlEncode(s);
                    _limit = limit;
                }
    
                public override string ToString()
                    => $"messages.search?count={_limit}&q={_s}";
            }
    
            #endregion
        }
    
        #endregion
    
        #region VkApi.Models

        private class PluginConfig
        {
            [JsonProperty("VK community token")]
            public string Token { get; set; }
    
            [JsonProperty("Don't show warnings when messages not allowed")]
            public bool MuteMessageDisallowed { get; set; }
    
            [JsonProperty("Log events to file")]
            public bool LogToFile { get; set; } = true;
        }
    
        #endregion
    }
}
