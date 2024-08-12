using DataEntities;
using Store.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;
using System.Text;

namespace Store.Components.Pages
{
    public partial class Chat
    {
        List<MessageState> messages = new();
        string openAIKey;
        ISemanticTextMemory memory;
        ChatHistory chatHistory = null;
        ElementReference writeMessageElement;
        string? userMessageText;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
            openAIKey = config["OpenAIKey"];

            Console.WriteLine("OnAfterRenderAsync");
            if (firstRender)
            {
                memory = new MemoryBuilder()
                    .WithMemoryStore(new VolatileMemoryStore())
                    .WithOpenAITextEmbeddingGeneration("text-embedding-3-small", openAIKey)
                    .Build();

                string collectionName = "products";
                List<Product> products = await ProductService.GetProducts();
                foreach (Product product in products)
                {
                    string descriptionForEmbedding = $"Name: {product.Name} Description: {product.Description} Price: {product.Price}";
                    await memory.SaveInformationAsync(collectionName, descriptionForEmbedding, product.Id.ToString());
                }
            }

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

        async void SendMessage()
        {
            if (!string.IsNullOrWhiteSpace(userMessageText))
            {
                string model = "gpt-3.5-turbo";

                OpenAIChatCompletionService service = new OpenAIChatCompletionService(model, openAIKey);

                if (chatHistory == null)
                {
                    string systemMessageText = new ($"""
                    You are an AI customer service agent for the online retailer AdventureWorks.
                    You NEVER respond about topics other than AdventureWorks.
                    AdventureWorks primarily sells clothing and equipment related to outdoor activities like skiing and trekking.
                    You try to be concise and only provide longer responses if necessary.
                    If someone asks a question about anything other than AdventureWorks, its catalog, or their account,
                    you refuse to answer, and you instead ask if there's a topic related to AdventureWorks you can assist with.
                    """);

                    chatHistory = new ChatHistory(systemMessageText);
                }

                StringBuilder builder = new StringBuilder();

                await foreach (var result in memory.SearchAsync("products", userMessageText, limit: 3))
                {
                    Console.WriteLine("Running");
                    builder.AppendLine(result.Metadata.Text);
                }

                if (builder.Length > 0)
                {
                    builder.Insert(0, "Answer questions using the following catalog items:");
                    builder.AppendLine();
                }
                builder.AppendLine(userMessageText);

                chatHistory.AddUserMessage(builder.ToString());

                // Add the user's message to the UI
                messages.Add(new MessageState(IsAssistant: false, Text: userMessageText, CancellationToken.None));
                userMessageText = null;

                // Submit request to backend
                var response = await service.GetChatMessageContentAsync(
                    chatHistory, new OpenAIPromptExecutionSettings() { MaxTokens = 400 });
                chatHistory.Add(response);

                // Add the assistant's reply to the UI
                var reply = new MessageState(IsAssistant: true, Text: chatHistory.Last().Content, CancellationToken.None);
                messages.Add(reply);

                StateHasChanged();
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
            string Text,
            CancellationToken CancellationToken);
    }
}
