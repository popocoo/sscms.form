﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SSCMS.Form.Core;
using SSCMS.Form.Models;

namespace SSCMS.Form.Controllers.Admin
{
    public partial class TemplateHtmlController
    {
        [HttpGet, Route(Route)]
        public async Task<ActionResult<GetResult>> Get([FromQuery] GetRequest request)
        {
            if (!await _authManager.HasSitePermissionsAsync(request.SiteId, FormManager.PermissionsTemplates))
                return Unauthorized();

            var templateInfo = _formManager.GetTemplateInfo(request.Name);
            var html = _formManager.GetTemplateHtml(templateInfo);

            var isSystem = templateInfo.Publisher == "sscms";
            if (isSystem)
            {
                templateInfo = new TemplateInfo();
            }

            return new GetResult
            {
                TemplateInfo = templateInfo,
                TemplateHtml = html,
                IsSystem = isSystem
            };
        }
    }
}
