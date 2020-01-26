Simple message sender to vk.com (social network). Easy to configure, easy to use.  
Plugin provides Hooks to send messages and log it. Can be quickly used by any another plugins.

## Configuration
```json
{
  "Don't show warnings when messages not allowed": false,
  "Log events to file": true,
  "VK community token": "put_your_community_token_here"
}
```
### Community configuration
To get **community token** you should be Administrator or have access to manage group.
<details><summary>Show details</summary><p>

Open community page and click **Manage** menu item  
![](https://i.imgur.com/hjRmPSM.png)  

Then choose **API usage** and click **Create token** button  
![](https://i.imgur.com/NmEcuWu.png)  

In the dialog **allow access to community messages** and click **Create**. Go through instructions and copy generated token.  
![](https://i.imgur.com/0jhosde.png)
</p></details>

### VK user configuration
To avoid spam, by default VK blocked all messages from community to user.  
User should **allow messages** from community.
<details><summary>Show details</summary><p>

To allow messages, user need to open your community page and click **Allow messages**  
![](https://i.imgur.com/5zQY2BW.png)
</p></details>

## Plugin Hooks
### OnVkMessageSent
```c#
private void OnVkMessageSent(string vkUserId, string message)
{
    Puts($"Message '{message}' was sent to VK User: {vkUserId}");
}
```

### OnVkError
```c#
private void OnVkError(byte code, string description, int vkApiCode, string vkUserId)
{
    Puts($"Api error code: {vkApiCode}; Description: '{description}'; User: {vkUserId}");
}

private void OnVkError(byte code, string description)
{
    Puts($"Something going wrong: '{description}'");
}
```

### OnVkConnected
```c#
private void OnVkConnected()
{
    Puts("VK Api connected!");
}
```
## OMG! Errors
### Invalid community token
On start, plugin testing connection to VK. And if you have this long red message in console, that mean you have to check token value.
```
[VkComponent] vkUserId: -; Error '5': User authorization failed: invalid access_token (4).
```

### User doesn't allow messages
Don't worry if you have these yellow warnings about messages without some permissions, that mean recipient doesn't allow messages from community.  
If you don't like these messages, you can turn of them in plugin configuration. Btw you can always handle these errors by **OnVkError** hook with code **4**
```
[VkComponent] vkUserId: 4307666; Error '901': Can't send messages for users without permission
```

### OnVkError(byte code, ...)
Code values: 
1. Network problems
2. Token wasn't specified
3. Some VK Api errors
4. User doesn't allow messages

## Examples
Example [plugin on git](https://github.com/rust-plugins/VkApi/blob/master/Examples/VkApiExample.cs) with console command "**vk**" to test messages.
```
> vk 000000 "Hello, Universe!"
[VK API Example] Sending test message to 000000
[VK API Example] Message 'Hello, Universe!' was sent to VK User: 000000
```

### Use it in your super plugin
Two simple steps:  Add plugin reference as usual
```c#
[PluginReference]
Plugin VkApi;

private void Loaded()
{
    if (VkApi == null)
    {
        PrintError("VK Api plugin not found");
    }
}
```

And send messages when you want
```c#
private void MyAwesomeMethod()
{
    var vkUserId = "000000";
    var message = "Hey, bro!";

    VkApi.Call("SendText", vkUserId, message);
}
```