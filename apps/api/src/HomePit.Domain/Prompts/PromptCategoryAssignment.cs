namespace HomePit.Domain.Prompts;

public sealed class PromptCategoryAssignment
{
    public Guid PromptId { get; set; }
    public Prompt? Prompt { get; set; }

    public Guid CategoryId { get; set; }
    public PromptCategory? Category { get; set; }
}
