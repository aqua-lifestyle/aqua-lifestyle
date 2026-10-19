using Abp.Dependency;
using Abp.Web.Models;
using AqualLifeStyle.Web.Host.Health;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqualLifeStyle.Web.Host.Controllers
{
    [ApiController]
    [DontWrapResult]
    [Route("api/deployment-readiness")]
    [Authorize(AuthenticationSchemes = DeploymentReadinessAuthentication.Scheme,
        Policy = DeploymentReadinessAuthentication.Policy)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public sealed class DeploymentReadinessController : ControllerBase, ITransientDependency
    {
        private readonly DeploymentReadinessEvidence evidence;
        public DeploymentReadinessController(DeploymentReadinessEvidence evidence) { this.evidence = evidence; }

        [HttpGet]
        public ActionResult<DeploymentReadinessResponse> Get([FromQuery] string expectedRevision)
        {
            var response = evidence.Read(expectedRevision);
            return response.Status == "Ready" ? Ok(response) : StatusCode(503, response);
        }
    }
}
