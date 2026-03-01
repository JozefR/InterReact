using ResearchPlatform.Contracts.Abstractions;

namespace AiResearchAssistant;

public sealed class AiResearchAssistantModule : IModule
{
    public string Name => "AiResearchAssistant";
    public string Description => "AI planning/summarization over structured research artifacts and experiments.";
}
