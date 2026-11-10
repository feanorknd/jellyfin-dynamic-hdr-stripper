using System.Text;
using Jellyfin.Api.Controllers;
using Jellyfin.Api.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Jellyfin.Plugin.DynamicHdrStripper;

public sealed class HlsFilter(SessionPolicies policies) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor stopAction
            && stopAction.ControllerTypeInfo.AsType() == typeof(HlsSegmentController)
            && stopAction.MethodInfo.Name == nameof(HlsSegmentController.StopEncodingProcess))
        {
            var sessionId = context.HttpContext.Request.Query["PlaySessionId"].ToString();
            var stoppedPolicy = policies.Find(sessionId);
            await next().ConfigureAwait(false);
            if (stoppedPolicy?.UserId == context.HttpContext.User.GetUserId()
                && stoppedPolicy.DeviceId == context.HttpContext.User.GetDeviceId()) policies.Remove(sessionId);
            return;
        }
        if (context.ActionDescriptor is not ControllerActionDescriptor action || action.ControllerTypeInfo.AsType() != typeof(DynamicHlsController))
        { await next().ConfigureAwait(false); return; }
        var request = context.HttpContext.Request;
        var policy = policies.Find(request.Query["PlaySessionId"].ToString());
        if (policy is null)
        {
            if (request.Query.ContainsKey("dhdr")) { context.Result = new ConflictObjectResult("HDR session expired; request PlaybackInfo again."); return; }
            await next().ConfigureAwait(false); return;
        }
        var user = context.HttpContext.User;
        if (user.GetUserId() != policy.UserId || user.GetDeviceId() != policy.DeviceId
            || request.Query["MediaSourceId"].ToString() != policy.SourceId
            || !context.ActionArguments.TryGetValue("itemId", out var itemId) || itemId is not Guid id || id != policy.ItemId)
        { context.Result = new ForbidResult(); return; }
        if (request.Query["AllowVideoStreamCopy"].ToString().Equals("false", StringComparison.OrdinalIgnoreCase)
            || request.Query["Static"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase)
            || (int.TryParse(request.Query["VideoStreamIndex"], out var videoIndex) && videoIndex != policy.VideoStreamIndex)
            || (int.TryParse(request.Query["SubtitleStreamIndex"], out var subtitle) && subtitle >= 0
                && request.Query["SubtitleMethod"].ToString().Equals("Encode", StringComparison.OrdinalIgnoreCase)))
        { context.Result = new ConflictObjectResult("HDR session requires HEVC stream copy without subtitle burn-in."); return; }
        var executed = await next().ConfigureAwait(false);
        if (executed.Result is FileContentResult file && file.ContentType.Contains("mpegurl", StringComparison.OrdinalIgnoreCase))
        {
            var manifest = Encoding.UTF8.GetString(file.FileContents);
            if (manifest.Contains("#EXT-X-STREAM-INF:", StringComparison.Ordinal))
                file.FileContents = Encoding.UTF8.GetBytes(HlsManifest.Rewrite(manifest, policy));
        }
    }
}
