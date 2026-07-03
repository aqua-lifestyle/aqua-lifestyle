using System;
using System.Text.Json;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Exceptions;
using Microsoft.AspNetCore.Http;

namespace AqualLifeStyle.Web.Host.Startup
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;

        public ErrorHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                var errorResponse = ErrorResponseBuilder.Build(ex);

                context.Response.Clear();
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = errorResponse.StatusCode;

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                };

                var payload = JsonSerializer.Serialize(errorResponse, options);
                await context.Response.WriteAsync(payload);
            }
        }
    }
}
