using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;

namespace RCD.AuthAPI.Tests.Pages;

internal static class PageTestHelper
{
    internal static PageContext Create(HttpContext? httpContext = null)
    {
        var ctx = httpContext ?? new DefaultHttpContext();
        return new PageContext(new ActionContext(
            ctx,
            new RouteData(),
            new PageActionDescriptor(),
            new ModelStateDictionary()));
    }
}
