using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Store.Services;

namespace Store.Components.Pages
{
    public partial class Chat
    {
        List<MessageState> messages = new();
        ElementReference writeMessageElement;
        string? userMessageText;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            Console.WriteLine("OnAfterRenderAsync");
            if (firstRender)
            {
                try
                {
                    await using var module = await JS.InvokeAsync<IJSObjectReference>("import", "./Components/Pages/Chat.razor.js");
                    await module.InvokeVoidAsync("submitOnEnter", writeMessageElement);
                }
                catch (JSDisconnectedException)
                {
                    // Not an error
                }
            }
        }

        void SendMessage()
        {
            if (!string.IsNullOrWhiteSpace(userMessageText))
            {
                // Add the user's message to the UI
                messages.Add(new MessageState(IsAssistant: false, Text: userMessageText));
                userMessageText = null;

                // Add the assistant's reply to the UI
                var reply = new MessageState(IsAssistant: true, Text: string.Empty);
                messages.Add(reply);
            }
        }

        private void HandleResponseCompleted(MessageState state)
        {
            // If it was cancelled before the response started, remove the message entirely
            // But if there was some text already, keep it
            if (string.IsNullOrEmpty(state.Text))
            {
                messages.Remove(state);
            }
        }

        public record MessageState(
            bool IsAssistant,
            string Text);
    }
}
