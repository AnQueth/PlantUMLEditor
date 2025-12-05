using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;

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

        public AIAgent Create(AISettings settings, Delegate[] tools)
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
Assistant must not modify files without explicit user confirmation.
For any proposed file edit, produce a diff and ask 'Apply changes? (yes/no)'. New files do not require confirmation.
Only apply on an explicit 'yes'.
When showing diffs, wrap it in ````diff` blocks for clarity.
If you create a new diagram you do not need to repeat the diagram code back to the user.
";


            // Create agent with tools and RAG context provider
            var baseAgent = new ChatClientAgent(chatClient.AsIChatClient(), new ChatClientAgentOptions
            {
                Instructions = instructions,
                Name = "PlantUMLAI",
                ChatOptions = toolOptions
                 
            });

            // Add OpenTelemetry instrumentation (returns an AIAgent wrapper)
            var agent = baseAgent.AsBuilder()
                .UseOpenTelemetry(sourceName: settings.SourceName)
                .Build();


            return agent;
        }

    }
}
