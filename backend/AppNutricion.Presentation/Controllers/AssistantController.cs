using AppNutricion.BusinessLogic.Exceptions;
using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Presentation.Auth;
using AppNutricion.Presentation.Input;
using AppNutricion.Presentation.Output;
using AppNutricion.Presentation.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AppNutricion.Presentation.Controllers;

[ApiController]
[Authorize]
[Route("api/assistant")]
public class AssistantController(IAssistantManager assistantManager, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("conversations")]
    public async Task<ActionResult<ConversationOutput>> CreateConversation()
    {
        var userId = currentUser.RequireUserId();
        var conversation = await assistantManager.CreateConversationAsync(userId);
        return Created($"/api/assistant/conversations/{conversation.Id}/messages", ConversationOutput.From(conversation));
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<List<ConversationOutput>>> ListConversations()
    {
        var userId = currentUser.RequireUserId();
        return (await assistantManager.ListConversationsAsync(userId)).Select(ConversationOutput.From).ToList();
    }

    [HttpGet("conversations/{id:int}/messages")]
    public async Task<ActionResult<List<ChatMessageOutput>>> Messages(int id)
    {
        var userId = currentUser.RequireUserId();
        var history = await assistantManager.GetHistoryAsync(id, userId);
        return history is null ? NotFound() : ChatMessageOutput.From(history);
    }

    [HttpPost("conversations/{id:int}/messages")]
    [EnableRateLimiting(RateLimitPolicies.AssistantChat)]
    public async Task<ActionResult<AssistantReplyOutput>> Send(int id, SendMessageInput input, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        try
        {
            var reply = await assistantManager.SendAsync(userId, id, input.Content, cancellationToken);
            return reply is null ? NotFound() : AssistantReplyOutput.From(reply);
        }
        catch (ArgumentException ex) when (ex.ParamName is not null)
        {
            return this.ValidationProblemFor(ex);
        }
    }

    [HttpPost("suggestions/{id:int}/accept")]
    public async Task<ActionResult<SuggestionOutput>> Accept(int id)
    {
        var userId = currentUser.RequireUserId();
        try
        {
            var suggestion = await assistantManager.AcceptSuggestionAsync(id, userId);
            return suggestion is null ? NotFound() : SuggestionOutput.From(suggestion);
        }
        catch (ConflictException ex)
        {
            return this.ConflictProblemFor(ex);
        }
        catch (ArgumentException ex) when (ex.ParamName is not null)
        {
            return this.ValidationProblemFor(ex);
        }
    }

    [HttpPost("suggestions/{id:int}/ignore")]
    public async Task<ActionResult<SuggestionOutput>> Ignore(int id)
    {
        var userId = currentUser.RequireUserId();
        try
        {
            var suggestion = await assistantManager.IgnoreSuggestionAsync(id, userId);
            return suggestion is null ? NotFound() : SuggestionOutput.From(suggestion);
        }
        catch (ConflictException ex)
        {
            return this.ConflictProblemFor(ex);
        }
    }
}
