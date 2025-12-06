using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;

namespace PlantUMLEditorAI
{
    public class AISettings
    {

public int MaxOutputTokens { get; set; } = 2048;
        public string Endpoint { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Deployment { get; set; } = string.Empty;

        public string SourceName { get; set; } = "PlantUMLEditorAI";

    }
    public class AIAgentFactory
    {
     

        public AIAgent Create(AISettings settings, Func<
                AIAgent,
                FunctionInvocationContext,
                Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>>,
                CancellationToken,
                ValueTask<object?>> confirmMiddleware, Delegate[] tools)
        {

            string? apiKey = settings.Key;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "Azure OpenAI API key is required. Provide it via AzureAI:Key in appsettings.json or AZURE_OPENAI_KEY environment variable.");
            }

            if (string.IsNullOrEmpty(settings.Endpoint))
            {
                throw new InvalidOperationException(
                    "AzureAI:Endpoint is required in appsettings.json.");
            }

            if (string.IsNullOrEmpty(settings.Deployment))
            {
                throw new InvalidOperationException(
                    "AzureAI:Deployment is required in appsettings.json.");
            }



            // Create Azure OpenAI client with key-based authentication
            var azureOpenAIClient = new AzureOpenAIClient(
                new Uri(settings.Endpoint),
                new AzureKeyCredential(apiKey));

            var chatClient = azureOpenAIClient.GetChatClient(settings.Deployment);


            // Register function tools
            var toolOptions = new ChatOptions
            {
                Tools = tools.Select(d => (AITool) AIFunctionFactory.Create(d)).ToList(),
                 MaxOutputTokens = settings.MaxOutputTokens

            };

            const string instructions = @"You are a plant uml expert to help users create and edit plantuml.
        You have the ability read html content from current documents.
Use the available tools. Ensure you verify any edits. Use tools to read and edit the current diagram.
Ensure diagrams adhere to plantuml syntax. 
If you create a new diagram you do not need to repeat the diagram code back to the user.
examples:
* if user asks to show methods on a class or interface, you search the whole folder for the class or interface definition and read the methods.
* if user asks to add a relationship between two classes, you search the whole folder for the class definitions and add the relationship.
* if user asks to create a new component diagram, you create a new plantuml diagram with appropriate syntax and name it with a word with .component.puml
* if user asks to create a new sequence diagram, you create a new plantuml diagram with appropriate syntax and name it with a word with .seq.puml
* if user asks to create a new class diagram, you create a new plantuml diagram with appropriate syntax and name it with a word with .class.puml
* if user asks to create a new diagram, you create a new plantuml diagram with appropriate syntax and name it with a word with .puml
* if current document text is not uml, it is either md or text from an html document.
* when creating a sequence diagram, add this comment after @startuml: '@@novalidate. if this comment appears in a file, leave it!
* for sequence diagrams prefer the format: participant ""name of something"" as alias

";



            // Create agent with tools and RAG context provider
#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            var baseAgent = new ChatClientAgent(chatClient.AsIChatClient(), new ChatClientAgentOptions
            {
                Instructions = instructions,
                Name = "PlantUMLAI",
                ChatOptions = toolOptions,
                ChatMessageStoreFactory = ctx => new InMemoryChatMessageStore(
            new SummarizingChatReducer(chatClient.AsIChatClient(), 6, 10), 
            ctx.SerializedState,
            ctx.JsonSerializerOptions,
            InMemoryChatMessageStore.ChatReducerTriggerEvent.AfterMessageAdded)

            });
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

            // Add OpenTelemetry instrumentation (returns an AIAgent wrapper)
            var agent = baseAgent.AsBuilder()
                .Use(confirmMiddleware)
                
                
                .UseOpenTelemetry(sourceName: settings.SourceName)
                .Build();


            return agent;
        }

    }
}
