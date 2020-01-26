using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("VK API Example", "NickRimmer", "1.0.0")]
    [Description("Example plugin")]
    public class VkApiExample : CovalencePlugin
    {
        [PluginReference]
        Plugin VkApi;

        private void Loaded()
        {
            if (VkApi == null)
            {
                PrintError("VK Api plugin not found");
            }
        }

        [Command("vk")]
        private void Test1(IPlayer player, string command, string[] args)
        {
            if (args?.Length != 2)
            {
                PrintWarning("Please specify VK userId and message");
                return;
            }

            var vkUserId = args[0];
            var message = args[1];

            Puts($"Sending test message to {vkUserId}");
            VkApi.Call("SendText", vkUserId, message);
        }

        private void OnVkMessageSent(string vkUserId, string message)
        {
            Puts($"Message '{message}' was sent to VK User: {vkUserId}");
        }

        private void OnVkError(byte code, string description, int vkApiCode, string vkUserId)
        {
            Puts($"{code}: Api error code: {vkApiCode}; Description: '{description}'; VK User: {vkUserId}");
        }

        private void OnVkError(byte code, string description)
        {
            Puts($"Something going wrong: '{description}'");
        }
    }
}
