using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MultiTerminal.MCPServer.Services;

namespace MultiTerminal.API.Controllers
{
    /// <summary>
    /// <c>POST /api/projects/{projectId}/open</c>: bring the project's PM pane to the front, or launch the
    /// project where the Owner left off (its launch mode decides new / continue / ask) when no pane has it
    /// open. For MT Conductor (ClarionLive#83). No request body.
    /// <para>
    /// Its own controller, not part of <see cref="ProjectContextController"/>, on purpose: that controller
    /// is also mounted on the phone gateway (<c>GatewayControllerFeatureProvider</c>), and raising or
    /// launching windows on the desk is not something the phone should do through a side door.
    /// </para>
    /// <para>
    /// Responses: 200 <c>{result:"focused", projectId, projectName, paneTitle, floating, otherPanes}</c>;
    /// 202 <c>{result:"launching", projectId, projectName}</c> (the launch was started; MT may still ask its
    /// usual questions and the Owner may cancel); 404 unknown project; 503 when MT's window is not ready or
    /// the open failed.
    /// </para>
    /// <para>
    /// Windows only lets the foreground process hand focus to another one. A caller that wants MT to come
    /// to the front should call <c>AllowSetForegroundWindow</c> with MT's pid (from <c>GET /api/health</c>)
    /// before posting; otherwise MT may only flash in the taskbar.
    /// </para>
    /// </summary>
    [ApiController]
    [Route("api/projects")]
    public class ProjectOpenController : ControllerBase
    {
        private readonly ProjectOpenService _openService;

        public ProjectOpenController(ProjectOpenService openService)
        {
            _openService = openService;
        }

        [HttpPost("{projectId}/open")]
        public async Task<IActionResult> Open(string projectId)
        {
            var outcome = await _openService.OpenAsync(projectId).ConfigureAwait(false);
            return ToResponse(outcome);
        }

        /// <summary>The HTTP form of an outcome. Internal so the tests can map each outcome without a host.</summary>
        internal IActionResult ToResponse(ProjectOpenOutcome outcome)
        {
            switch (outcome.Result)
            {
                case ProjectOpenResult.Focused:
                    return Ok(new
                    {
                        result = "focused",
                        projectId = outcome.ProjectId,
                        projectName = outcome.ProjectName,
                        paneTitle = outcome.PaneTitle,
                        floating = outcome.Floating,
                        otherPanes = outcome.OtherPanes,
                    });
                case ProjectOpenResult.Launching:
                    return StatusCode(202, new
                    {
                        result = "launching",
                        projectId = outcome.ProjectId,
                        projectName = outcome.ProjectName,
                    });
                case ProjectOpenResult.NotFound:
                    return Problem(detail: outcome.Error ?? $"Project '{outcome.ProjectId}' not found", statusCode: 404);
                default:
                    return Problem(detail: outcome.Error ?? "Open failed", statusCode: 503);
            }
        }
    }
}
